using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Alak.Core.Classification;

namespace Alak.Rules;

/// <summary>
/// The single concept-matching automaton (RUL-003): one Aho–Corasick built from all
/// enabled packs, rebuilt on pack change and swapped atomically (PCK-005).
/// Engine contains NO natural-language words — only concept IDs (§8.6).
/// </summary>
public sealed class ConceptMatcher
{
    private readonly record struct TermEntry(string ConceptId, float Weight, string Term);

    private sealed class Node
    {
        public Dictionary<char, Node> Children = new();
        public Node? Fail;
        public List<TermEntry> Outputs = new();
        public int Depth;
    }

    private readonly Node _root = new();
    private readonly int _termCount;

    public int TermCount => _termCount;

    public ConceptMatcher(IEnumerable<(string ConceptId, float Weight, string Term)> terms)
    {
        var count = 0;
        foreach (var (conceptId, weight, rawTerm) in terms)
        {
            // Terms arrive in FOLDED form (§8.2): lowercase, letters unified, digits ASCII.
            // '~' marks optional separators: split on ~ and allow zero-or-more space between
            // chunks (NRM-003: ZWNJ→space means 'می‌شود', 'میشود', 'می شود' all match one term).
            var chunks = rawTerm.Split('~', StringSplitOptions.RemoveEmptyEntries);
            AddTerm(_root, chunks, 0, new TermEntry(conceptId, weight, rawTerm), 0);
            count++;
        }
        BuildFailLinks();
        _termCount = count;
    }

    private static void AddTerm(Node node, string[] chunks, int ci, TermEntry entry, int depth)
    {
        if (ci == chunks.Length)
        {
            node.Outputs.Add(entry);
            return;
        }
        var current = node;
        var d = depth;
        foreach (var ch in chunks[ci])
        {
            if (!current.Children.TryGetValue(ch, out var next))
            {
                next = new Node { Depth = d + 1 };
                current.Children[ch] = next;
            }
            current = next;
            d = current.Depth;
        }
        if (ci < chunks.Length - 1)
        {
            // optional separator: '~' = zero-or-more spaces between chunks
            if (!current.Children.TryGetValue(' ', out var skip))
            {
                skip = new Node { Depth = current.Depth + 1 };
                current.Children[' '] = skip;
            }
            var skip2 = skip;
            var skipDepth = skip2.Depth;
            // zero spaces: continue directly
            AddTerm(current, chunks, ci + 1, entry, current.Depth);
            // one-or-more spaces: continue from the space node (further spaces fold via
            // the folded view's collapsed whitespace — runs are single spaces already)
            AddTerm(skip2, chunks, ci + 1, entry, skipDepth);
            return;
        }
        AddTerm(current, chunks, ci + 1, entry, current.Depth);
    }

    private void BuildFailLinks()
    {
        var queue = new Queue<Node>();
        _root.Fail = _root;
        foreach (var child in _root.Children.Values)
        {
            child.Fail = _root;
            queue.Enqueue(child);
        }
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var (ch, child) in node.Children)
            {
                var f = node.Fail;
                while (f is not null && f != _root && !f.Children.ContainsKey(ch)) f = f.Fail;
                child.Fail = f?.Children.TryGetValue(ch, out var next) == true && next != child ? next : _root;
                if (child.Fail == child) child.Fail = _root;
                child.Outputs.AddRange(child.Fail.Outputs);
                queue.Enqueue(child);
            }
        }
    }

    private static readonly SearchValues<char> s_asciiAlphaNumeric = SearchValues.Create(
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");

    /// <summary>Fast-path ASCII alphanumeric check with fallback to Unicode classification.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLetterOrDigitFast(char c)
    {
        if (c <= 127)
        {
            return s_asciiAlphaNumeric.Contains(c);
        }
        return char.IsLetterOrDigit(c);
    }

    /// <summary>
    /// SIMD vector check using Vector128 to evaluate whether characters in a span are alphanumeric.
    /// Hardware accelerated on ARM64 NEON and x64 AVX2/SSE2.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsAlphaNumericSimd(ReadOnlySpan<char> span)
    {
        int i = 0;
        if (Vector128.IsHardwareAccelerated && span.Length >= Vector128<ushort>.Count)
        {
            var vLowerA = Vector128.Create((ushort)'a');
            var vLowerZ = Vector128.Create((ushort)'z');
            var vUpperA = Vector128.Create((ushort)'A');
            var vUpperZ = Vector128.Create((ushort)'Z');
            var vDigit0 = Vector128.Create((ushort)'0');
            var vDigit9 = Vector128.Create((ushort)'9');
            var vAsciiMax = Vector128.Create((ushort)127);

            var spanUshort = MemoryMarshal.Cast<char, ushort>(span);
            int limit = span.Length - Vector128<ushort>.Count;

            for (; i <= limit; i += Vector128<ushort>.Count)
            {
                var v = Vector128.Create(spanUshort.Slice(i));

                var isLower = Vector128.GreaterThanOrEqual(v, vLowerA) & Vector128.LessThanOrEqual(v, vLowerZ);
                var isUpper = Vector128.GreaterThanOrEqual(v, vUpperA) & Vector128.LessThanOrEqual(v, vUpperZ);
                var isDigit = Vector128.GreaterThanOrEqual(v, vDigit0) & Vector128.LessThanOrEqual(v, vDigit9);

                if ((isLower | isUpper | isDigit) != Vector128<ushort>.Zero)
                    return true;

                var isNonAscii = Vector128.GreaterThan(v, vAsciiMax);
                if (isNonAscii != Vector128<ushort>.Zero)
                {
                    for (int j = 0; j < Vector128<ushort>.Count; j++)
                    {
                        char ch = span[i + j];
                        if (ch > 127 && char.IsLetterOrDigit(ch))
                            return true;
                    }
                }
            }
        }

        for (; i < span.Length; i++)
        {
            if (IsLetterOrDigitFast(span[i])) return true;
        }
        return false;
    }

    /// <summary>Scan folded text; yields every concept hit with its span in the folded view.
    /// A hit counts only at word boundaries: the char before the match and the char after
    /// must not be a letter or digit (RUL-003, prevents 'address'→'ad', 'saved'→'save').</summary>
    public IEnumerable<ConceptHit> Scan(string folded)
    {
        var current = _root;
        for (var i = 0; i < folded.Length; i++)
        {
            var c = folded[i];
            // optional-separator tolerance: a run of spaces may satisfy a '~' boundary;
            // handled structurally by the trie (space-edge + direct-edge), so normal walk.
            while (current != _root && !current.Children.ContainsKey(c)) current = current.Fail ?? _root;
            if (current.Children.TryGetValue(c, out var next)) current = next;
            else current = _root;
            if (current.Outputs.Count > 0)
            {
                var consumed = TermLength(current);
                var start = i - consumed + 1;
                var end = i + 1;
                foreach (var hit in current.Outputs)
                {
                    // word-boundary: preceding char (if any) must not be alphanumeric
                    if (start > 0 && IsLetterOrDigitFast(folded[start - 1])) continue;
                    // following char (if any) must not be alphanumeric
                    if (end < folded.Length && IsLetterOrDigitFast(folded[end])) continue;
                    yield return new ConceptHit(hit.ConceptId, (start, end), hit.Weight, Fuzzy: false, hit.Term);
                }
            }
        }
    }

    private static int TermLength(Node node) => node.Depth;
}
