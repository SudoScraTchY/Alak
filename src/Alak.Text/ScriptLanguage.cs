using System.Globalization;

namespace Alak.Text;

/// <summary>One script run (spec LNG-001): digits/punctuation attach to the surrounding run.</summary>
public sealed record ScriptRun(Script Script, string Text, int Start, int Length);

/// <summary>A probabilistic language guess (spec LNG-002/003/004 — advisory only).</summary>
public sealed record LanguageGuess(LanguageFa Language, float Probability);

/// <summary>Script runs and language/direction detection (spec §8.3).</summary>
public static class ScriptLanguage
{
    // Persian-only letters (LNG-002): پ چ ژ گ — plus ی/ک in their Persian codepoints
    private static readonly IReadOnlySet<char> s_persianOnly = new HashSet<char>("پچژگ");

    // Persian function words (normalized forms)
    private static readonly IReadOnlySet<string> s_faWords = new HashSet<string>(new[]
    { "است", "را", "که", "برای", "با", "در" }, StringComparer.Ordinal);

    // Arabic function words
    private static readonly IReadOnlySet<string> s_arWords = new HashSet<string>(new[]
    { "في", "من", "على", "هذا", "التي", "الذي", "الى", "على" }, StringComparer.Ordinal);

    /// <summary>
    /// Split into script runs (LNG-001). Digits/punctuation attach to the previous run
    /// (or the next one when they lead the text).
    /// </summary>
    public static ScriptRun[] SplitIntoRuns(string folded)
    {
        var runs = new List<ScriptRun>();
        if (string.IsNullOrEmpty(folded)) return runs.ToArray();

        var current = Script.Other;
        var start = 0;

        Script Classify(char c) => c switch
        {
            >= 'a' and <= 'z' or >= 'A' and <= 'Z' => Script.Latn,
            >= '\u0600' and <= '\u06FF' or >= '\u0750' and <= '\u077F' => Script.Arab,
            _ => Script.Other,
        };

        for (var i = 0; i < folded.Length; i++)
        {
            var s = Classify(folded[i]);
            if (s == Script.Other) continue; // attach to current run
            if (s != current)
            {
                if (current != Script.Other && i > start)
                    runs.Add(new ScriptRun(current, folded[start..i], start, i - start));
                current = s;
                start = i;
            }
        }
        if (current != Script.Other && folded.Length > start)
            runs.Add(new ScriptRun(current, folded[start..folded.Length], start, folded.Length - start));
        return runs.ToArray();
    }

    /// <summary>
    /// Guess the language of one text (LNG-002/003). Works on normalized text.
    /// Advisory: never throws, always returns a guess.
    /// </summary>
    public static LanguageGuess Guess(string text)
    {
        var folded = text; // caller passes normalized text
        var runs = SplitIntoRuns(folded);

        var arChars = 0; var latnChars = 0;
        foreach (var r in runs)
            if (r.Script == Script.Arab) arChars += r.Length;
            else if (r.Script == Script.Latn) latnChars += r.Length;

        if (arChars == 0 && latnChars == 0)
            return new LanguageGuess(LanguageFa.Unknown, 0.5f);

        if (arChars >= latnChars)
        {
            // LNG-002: Persian vs Arabic decision on the Arabic-script content
            var hasPersianOnly = folded.IndexOfAny(new[] { 'پ', 'چ', 'ژ', 'گ' }) >= 0;
            var words = folded.Split(' ', '\n', '.', ',', '!', '?', ';', ':');
            var faHits = words.Count(w => s_faWords.Contains(w));
            var arHits = words.Count(w => s_arWords.Contains(w));

            var hasTaaMarbuta = text.IndexOf('ة') >= 0; // ة in RAW text (pre-unification)
            float pFa = 0.5f;
            if (hasPersianOnly) pFa += 0.2f;
            pFa += 0.05f * faHits;
            pFa -= 0.08f * arHits;
            if (faHits > 0 && arHits == 0) pFa += 0.15f;
            if (arHits > 0 && faHits == 0) pFa -= 0.1f;
            if (hasTaaMarbuta) pFa -= 0.1f;
            pFa = Math.Clamp(pFa, 0.05f, 0.95f);
            return pFa >= 0.5f
                ? new LanguageGuess(LanguageFa.Persian, pFa)
                : new LanguageGuess(LanguageFa.Arabic, 1f - pFa);
        }

        // Latin-run: Finglish is decided by the caller using lexicon hits (LNG-003);
        // without packs we cannot tell — report Unknown with low confidence.
        return new LanguageGuess(LanguageFa.Unknown, 0.3f);
    }

    /// <summary>First-strong-character direction for display (LNG-005).</summary>
    public static TextDirection FirstStrongDirection(string text)
    {
        foreach (var c in text)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat is UnicodeCategory.OtherLetter or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.UppercaseLetter or UnicodeCategory.ModifierLetter)
            {
                // Strong RTL: Arabic block letters (plus Hebrew, not expected here)
                if (c >= '\u0600' && c <= '\u06FF') return TextDirection.Rtl;
                return TextDirection.Ltr;
            }
        }
        return TextDirection.Ltr; // default
    }
}
