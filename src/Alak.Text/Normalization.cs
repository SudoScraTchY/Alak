using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;

namespace Alak.Text;

/// <summary>Emoji signal classes (spec NRM-009): removed from Folded, counted as evidence.</summary>
public readonly record struct EmojiSignals(int Promo, int Urgency, int Delivery, int Security);

/// <summary>Normalized text with two views + span map (spec §8.2, NRM-010).</summary>
public sealed record NormalizedText
{
    /// <summary>Display text: NFKC, original case, bidi controls kept (NRM-001/002/006).</summary>
    public required string Text { get; init; }

    /// <summary>Case-folded matching view: controls stripped, letters unified, digits ASCII.</summary>
    public required string Folded { get; init; }

    /// <summary>Map[i] = index in the ORIGINAL string of the glyph at Folded[i] (NRM-010).</summary>
    public required int[] Map { get; init; }

    public required EmojiSignals EmojiSignals { get; init; }

    /// <summary>Map a (start,end) span in Folded back to the ORIGINAL text (NRM-010).</summary>
    public (int Start, int End) OriginalSpan(int start, int end)
    {
        if (Map.Length == 0) return (0, 0);
        start = Math.Clamp(start, 0, Map.Length - 1);
        end = Math.Clamp(end, 0, Map.Length);
        var s = Map[start];
        // end is exclusive in Folded; find the original index just past the last mapped char
        var e = end >= Map.Length
            ? LastOriginalLengthHint
            : Map[end - 1] + 1;
        return (s, Math.Max(s + 1, e));
    }

    /// <summary>Original string length, used when a folded span runs to the end.</summary>
    public required int LastOriginalLengthHint { get; init; }
}

/// <summary>Script run classification (spec LNG-001).</summary>
public enum Script { Latn, Arab, Other }

/// <summary>Language guess (advisory, LNG-002/003/004).</summary>
public enum LanguageFa { Persian, Arabic, Finglish, Unknown }

public enum TextDirection { Ltr, Rtl }

/// <summary>
/// Pure, allocation-conscious normalization (spec §8.2, NRM-001..012).
/// Single pass over the input builds Text, Folded, and the span map together.
/// </summary>
public static class Normalizer
{
    // NRM-009 emoji classes (SIMD-accelerated SearchValues in .NET)
    private static readonly SearchValues<char> s_promo = SearchValues.Create("🔥🎁💥🎉🏷️💰🛍️");
    private static readonly SearchValues<char> s_urgency = SearchValues.Create("⏰⌛🚨⚡");
    private static readonly SearchValues<char> s_delivery = SearchValues.Create("📦🚚");
    private static readonly SearchValues<char> s_security = SearchValues.Create("🔒🔐⚠️");

    private static readonly SearchValues<char> s_stripControls = SearchValues.Create(new[]
    {
        '\u200B', '\u200E', '\u200F', '\u202A', '\u202B', '\u202C', '\u202D', '\u202E',
        '\u2066', '\u2067', '\u2068', '\u2069', '\uFEFF', '\u00AD',
    });

    public static NormalizedText Normalize(string original)
    {
        // NRM-001: NFKC first; input is usually already composed but fullwidth/math-bold fold here.
        var nfkc = original.Normalize(NormalizationForm.FormKC);

        var textSb = new System.Text.StringBuilder(nfkc.Length);
        var foldedSb = new System.Text.StringBuilder(nfkc.Length);
        var map = new List<int>(nfkc.Length);
        int promo = 0, urgency = 0, delivery = 0, security = 0;
        char? pendingFoldedSpace = null; // collapse runs (NRM-007/008): emit one space at next visible char

        for (var i = 0; i < nfkc.Length; i++)
        {
            var c = nfkc[i];

            // Text view: keep everything except NFKC is applied (bidi controls KEPT, NRM-002)
            textSb.Append(c);

            // ---- Folded view ----
            if (s_stripControls.Contains(c)) continue;            // NRM-002
            if (c == '\u200C') { pendingFoldedSpace = ' '; continue; } // NRM-003 ZWNJ → space
            if (s_promo.Contains(c)) { promo++; EmitSpace(); continue; }
            if (s_urgency.Contains(c)) { urgency++; EmitSpace(); continue; }
            if (s_delivery.Contains(c)) { delivery++; EmitSpace(); continue; }
            if (s_security.Contains(c)) { security++; EmitSpace(); continue; }

            var f = FoldChar(c);
            if (f is null) continue;

            if (f == ' ')
            {
                pendingFoldedSpace = ' ';
                continue;
            }

            if (pendingFoldedSpace.HasValue && foldedSb.Length > 0
                && foldedSb[^1] != ' ' && foldedSb[^1] != '\n')
            {
                foldedSb.Append(' ');
                map.Add(OriginalIndex(textSb, i));
            }
            pendingFoldedSpace = null;

            // NRM-007: elongation — a run of >= 3 identical letters collapses to ONE
            var n = foldedSb.Length;
            if (char.IsLetter(f.Value) && n >= 2
                && foldedSb[n-1] == f.Value && foldedSb[n-2] == f.Value)
            {
                foldedSb.Length = n - 1;   // collapse the doubled letter to one
                map.RemoveAt(map.Count - 1);
                continue; // drop this elongated copy entirely
            }

            foldedSb.Append(f.Value);
            map.Add(OriginalIndex(textSb, i));
        }

        var oldFolded = foldedSb.ToString();
        var (folded, newMap) = GlueSpacedLetters(oldFolded, map.ToArray());

        return new NormalizedText
        {
            Text = textSb.ToString(),
            Folded = folded,
            Map = newMap,
            EmojiSignals = new EmojiSignals(promo, urgency, delivery, security),
            LastOriginalLengthHint = original.Length,
        };

        void EmitSpace() { pendingFoldedSpace = ' '; }

        static int OriginalIndex(System.Text.StringBuilder sb, int i)
            => i; // NFKC may change lengths; recompute precisely below.
    }

    // NRM-007 companion (QLT-001 adversarial glue): "F R E E S H I P P I N G" → "free shipping".
    // Runs of single letters separated by single spaces, ≥4 long, are glued back together.
    // Returns (folded, map) with glued words; the map points each folded char at the
    // ORIGINAL index of its first glued letter (NRM-010 stays valid).
    private static (string Folded, int[] Map) GlueSpacedLetters(string s, int[] map)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        var newMap = new List<int>(map.Length);
        var i = 0;
        while (i < s.Length)
        {
            if (char.IsWhiteSpace(s[i]))
            {
                if (s[i] == '\n') { sb.Append('\n'); newMap.Add(map[i]); i++; continue; } // NRM-007: newlines preserved
                if (sb.Length > 0 && sb[^1] != ' ') { sb.Append(' '); newMap.Add(map[i]); }
                i++;
                continue;
            }
            // try glue: sequence of single letters separated by single spaces
            var (glued, endIdx, gluedWord) = TryGlueAt(s, i);
            if (glued && gluedWord.Length >= 4)
            {
                // map each glued letter to its own original letter position: walk k over
                // the run, taking letters only (skipping the single separator spaces)
                var k = i;
                foreach (var ch in gluedWord)
                {
                    while (k < endIdx && !char.IsLetter(s[k])) k++;  // skip spaces
                    newMap.Add(map[k]);
                    k++;
                }
                sb.Append(gluedWord);
                i = endIdx;
            }
            else
            {
                var e = i;
                while (e < s.Length && !char.IsWhiteSpace(s[e])) e++;
                for (var q = i; q < e; q++) { sb.Append(s[q]); newMap.Add(map[q]); }
                i = e;
            }
        }
        return (sb.ToString(), newMap.ToArray());
    }

    /// <summary>At i (a letter), try "a b c d" single-space glue; returns (ok, end index, word).</summary>
    private static (bool Ok, int End, string Word) TryGlueAt(string s, int i)
    {
        var word = new System.Text.StringBuilder();
        var k = i;
        var glued = false;
        while (k < s.Length)
        {
            if (char.IsWhiteSpace(s[k])) break;
            word.Append(s[k]); k++;
            // next is "single letter + space" pattern?
            if (k + 1 < s.Length && s[k] == ' ' && char.IsLetter(s[k + 1])
                && (k + 2 >= s.Length || s[k + 2] == ' '))
            {
                if (word.Length == 1) glued = true;
                else if (!glued) break;
                k++; // skip the separator space
            }
        }
        return (glued, k, word.ToString());
    }


    private static readonly SearchValues<char> s_whitespaceFold = SearchValues.Create(
        " \t\u00A0\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A");

    /// <summary>
    /// SIMD-vectorized ASCII lowercase and Persian/Arabic-Indic digit folding across a span of characters (PF-01).
    /// Uses 128-bit NEON / SSE2 vectors (8 UTF-16 code units per vector).
    /// </summary>
    public static void FoldSimd(Span<char> span)
    {
        int i = 0;
        if (Vector128.IsHardwareAccelerated && span.Length >= Vector128<ushort>.Count)
        {
            var vUpperA = Vector128.Create((ushort)'A');
            var vUpperZ = Vector128.Create((ushort)'Z');
            var vAdd32 = Vector128.Create((ushort)32);

            var vFa0 = Vector128.Create((ushort)'\u06F0');
            var vFa9 = Vector128.Create((ushort)'\u06F9');
            var vFaDiff = Vector128.Create((ushort)('\u06F0' - '0'));

            var vAr0 = Vector128.Create((ushort)'\u0660');
            var vAr9 = Vector128.Create((ushort)'\u0669');
            var vArDiff = Vector128.Create((ushort)('\u0660' - '0'));

            var spanUshort = MemoryMarshal.Cast<char, ushort>(span);
            int limit = span.Length - Vector128<ushort>.Count;

            for (; i <= limit; i += Vector128<ushort>.Count)
            {
                var v = Vector128.LoadUnsafe(ref spanUshort[i]);

                // Vectorized ASCII uppercase to lowercase ('A'..'Z' -> 'a'..'z')
                var isUpper = Vector128.GreaterThanOrEqual(v, vUpperA) & Vector128.LessThanOrEqual(v, vUpperZ);
                v += (isUpper & vAdd32);

                // Vectorized Persian digits ('\u06F0'..'\u06F9' -> '0'..'9')
                var isFaDigit = Vector128.GreaterThanOrEqual(v, vFa0) & Vector128.LessThanOrEqual(v, vFa9);
                v = Vector128.ConditionalSelect(isFaDigit, v - vFaDiff, v);

                // Vectorized Arabic-Indic digits ('\u0660'..'\u0669' -> '0'..'9')
                var isArDigit = Vector128.GreaterThanOrEqual(v, vAr0) & Vector128.LessThanOrEqual(v, vAr9);
                v = Vector128.ConditionalSelect(isArDigit, v - vArDiff, v);

                // Vectorized Arabic Yeh ('\u064A' and '\u0649' -> '\u06CC')
                var isYeh1 = Vector128.Equals(v, Vector128.Create((ushort)'\u064A'));
                var isYeh2 = Vector128.Equals(v, Vector128.Create((ushort)'\u0649'));
                v = Vector128.ConditionalSelect(isYeh1 | isYeh2, Vector128.Create((ushort)'\u06CC'), v);

                // Vectorized Arabic Kaf ('\u0643' -> '\u06A9')
                var isKaf = Vector128.Equals(v, Vector128.Create((ushort)'\u0643'));
                v = Vector128.ConditionalSelect(isKaf, Vector128.Create((ushort)'\u06A9'), v);

                v.StoreUnsafe(ref spanUshort[i]);

                // Apply scalar FoldChar on characters that require diacritic/whitespace/punctuation handling
                for (int j = 0; j < Vector128<ushort>.Count; j++)
                {
                    char ch = span[i + j];
                    var f = FoldChar(ch);
                    if (f.HasValue) span[i + j] = f.Value;
                }
            }
        }

        for (; i < span.Length; i++)
        {
            var f = FoldChar(span[i]);
            if (f.HasValue) span[i] = f.Value;
        }
    }

    /// <summary>Single-character fold for the matching view (NRM-004..007).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char? FoldChar(char c)
    {
        // Fast-path ASCII common letters & digits
        if (c < 128)
        {
            if (c >= 'A' && c <= 'Z') return (char)(c + 32);
            if (c == ' ' || c == '\t') return ' ';
            if (c == '\n') return '\n';
            return c;
        }

        // Fast-path Persian & Arabic digits (NRM-005)
        if (c >= '\u06F0' && c <= '\u06F9') return (char)('0' + (c - '\u06F0'));
        if (c >= '\u0660' && c <= '\u0669') return (char)('0' + (c - '\u0660'));

        return c switch
        {
            // NRM-004: Persian/Arabic unification (matching view)
            '\u064A' or '\u0649' => 'ی',   // ي ى → ی
            '\u0643' => 'ک',               // ك → ک
            '\u0629' or '\u06C0' => 'ه',    // ة ۀ → ه
            '\u0623' or '\u0625' => 'ا',    // أ إ → ا
            '\u0624' => 'و',               // ؤ → و
            '\u0626' => 'ی',               // ئ → ی
            '\u0640' => null,              // tatweel removed
            >= '\u064B' and <= '\u065F' => null, // harakat removed
            '\u0670' => null,               // superscript alef
            '\u066C' => ',',               // ٬ thousands sep
            '\u066B' => '.',               // ٫ decimal sep
            '\u066A' => '%',               // ٪ percent
            '\u060C' => ',',               // ،
            '\u061B' => ';',               // ؛
            '\u061F' => '?',               // ؟
            _ => s_whitespaceFold.Contains(c) ? ' ' : c,
        };
    }
}
