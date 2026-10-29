using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Alak.Inference.Abstractions.Tasks;

namespace Alak.Inference.Abstractions;

/// <summary>
/// Implements §9.4 / SLM-003: Grounding Guard.
/// Verifies that any promo code, amount, date, or key fact emitted by Tier 3 SLM
/// exists verbatim (after normalization) in the source notification text.
/// If a candidate fact or value is absent/hallucinated, it is dropped.
/// </summary>
public static class GroundingGuard
{
    private static readonly Regex NumberPattern = new(@"\d+(?:[.,]\d+)?", RegexOptions.Compiled);
    private static readonly Regex UrlPattern = new(@"https?://\S+|www\.\S+|\b\S+\.(?:com|ir|org|net|io)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Identifies promo codes, coupon codes, and alphanumeric identifiers:
    /// 1. Mixed alphanumeric tokens (e.g. SNAPP50, 50OFF, B123)
    /// 2. All-caps tokens of 3+ letters (e.g. DELICIOUS, SAVE, DISCOUNT, VIP)
    /// 3. Tokens containing hyphens or underscores (e.g. PROMO-10, SNAP_DAY)
    /// </summary>
    public static bool LooksLikeCode(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        var trimmed = token.Trim(' ', '.', ',', ':', ';', '!', '?', '(', ')', '[', ']', '"', '\'', '«', '»');
        if (trimmed.Length < 3 || trimmed.Length > 30) return false;

        bool hasLetter = trimmed.Any(char.IsLetter);
        bool hasDigit = trimmed.Any(char.IsDigit);

        if (hasLetter && hasDigit) return true;

        if ((trimmed.Contains('-') || trimmed.Contains('_')) && trimmed.Length >= 4)
            return true;

        if (trimmed.Length >= 3 && trimmed.All(c => c is >= 'A' and <= 'Z'))
            return true;

        return false;
    }

    /// <summary>
    /// Applies grounding guard to an Explain result.
    /// Drops any facts containing codes, numbers, dates, or values not present in the source text.
    /// </summary>
    public static T301ExplainResult Apply(T301ExplainResult result, string? sourceTitle, string? sourceText)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var combinedSource = $"{sourceTitle ?? string.Empty} {sourceText ?? string.Empty}";
        var normalizedSource = Normalize(combinedSource);

        // Filter facts: must be grounded in source text
        var groundedFacts = new List<T3Fact>();
        foreach (var fact in result.Facts)
        {
            if (IsGrounded(fact.Value, normalizedSource))
            {
                groundedFacts.Add(fact);
            }
        }

        // Validate Action: if it mentions a code, URL, or number that doesn't appear in source, drop the action
        string? action = result.Action;
        if (!string.IsNullOrWhiteSpace(action))
        {
            var tokens = action.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawTok in tokens)
            {
                var tok = rawTok.Trim(' ', '.', ',', ':', ';', '!', '?', '(', ')', '[', ']', '"', '\'', '«', '»');
                if (LooksLikeCode(tok) || UrlPattern.IsMatch(tok))
                {
                    var codeNorm = Normalize(tok);
                    if (!normalizedSource.Contains(codeNorm, StringComparison.OrdinalIgnoreCase))
                    {
                        // Action contains hallucinated code or URL
                        action = null;
                        break;
                    }
                }
            }
        }

        return result with
        {
            Action = action,
            Facts = groundedFacts.ToArray()
        };
    }

    /// <summary>
    /// Applies grounding guard to a CodeDetails result.
    /// Drops min_spend, cap, or valid_until if the numbers/dates do not exist in the source text.
    /// </summary>
    public static T304CodeDetailsResult Apply(T304CodeDetailsResult result, string? sourceTitle, string? sourceText)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var combinedSource = $"{sourceTitle ?? string.Empty} {sourceText ?? string.Empty}";
        var normalizedSource = Normalize(combinedSource);

        double? minSpend = result.MinSpend;
        if (minSpend.HasValue && !IsNumberGrounded(minSpend.Value, normalizedSource))
        {
            minSpend = null;
        }

        double? cap = result.Cap;
        if (cap.HasValue && !IsNumberGrounded(cap.Value, normalizedSource))
        {
            cap = null;
        }

        string? validUntil = result.ValidUntil;
        if (!string.IsNullOrWhiteSpace(validUntil) && !IsGrounded(validUntil, normalizedSource))
        {
            validUntil = null;
        }

        var groundedConditions = result.Conditions
            .Where(c => IsGrounded(c, normalizedSource))
            .ToArray();

        return new T304CodeDetailsResult(minSpend, cap, validUntil, groundedConditions);
    }

    /// <summary>
    /// Checks if a candidate string is grounded in the normalized source text.
    /// </summary>
    public static bool IsGrounded(string? candidate, string normalizedSource)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return true;

        var normalizedCandidate = Normalize(candidate);
        if (string.IsNullOrWhiteSpace(normalizedCandidate)) return true;

        // 1. Direct substring match after normalization
        if (normalizedSource.Contains(normalizedCandidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. Check if all critical codes, URLs, and numbers in candidate exist in normalizedSource
        var tokens = candidate.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var codesInCandidate = tokens
            .Select(t => t.Trim(' ', '.', ',', ':', ';', '!', '?', '(', ')', '[', ']', '"', '\'', '«', '»'))
            .Where(t => LooksLikeCode(t) || UrlPattern.IsMatch(t))
            .Select(Normalize)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        var numbers = NumberPattern.Matches(candidate).Select(m => Normalize(m.Value)).ToList();

        if (codesInCandidate.Count == 0 && numbers.Count == 0)
        {
            // Pure semantic phrase with no numbers or codes: check if major words (>= 3 chars) appear
            var words = normalizedCandidate.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(w => w.Length >= 3)
                .ToList();

            if (words.Count == 0) return true;
            int matchedWords = words.Count(w => normalizedSource.Contains(w, StringComparison.OrdinalIgnoreCase));
            return (double)matchedWords / words.Count >= 0.5;
        }

        // Any code or URL present in candidate MUST appear in normalizedSource
        foreach (var c in codesInCandidate)
        {
            if (!normalizedSource.Contains(c, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var n in numbers)
        {
            if (!normalizedSource.Contains(n, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNumberGrounded(double num, string normalizedSource)
    {
        var rawInt = ((long)num).ToString();
        if (normalizedSource.Contains(rawInt, StringComparison.Ordinal))
            return true;

        var rawFloat = num.ToString("G");
        if (normalizedSource.Contains(rawFloat, StringComparison.Ordinal))
            return true;

        // Support Persian/English magnitude words: e.g. 100 هزار (100 thousand) -> 100,000
        if (num >= 1000 && (long)num % 1000 == 0)
        {
            var thousands = ((long)num / 1000).ToString();
            if (normalizedSource.Contains(thousands, StringComparison.Ordinal) &&
                (normalizedSource.Contains("هزار", StringComparison.OrdinalIgnoreCase) ||
                 normalizedSource.Contains("k", StringComparison.OrdinalIgnoreCase) ||
                 normalizedSource.Contains("thousand", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        if (num >= 1_000_000 && (long)num % 1_000_000 == 0)
        {
            var millions = ((long)num / 1_000_000).ToString();
            if (normalizedSource.Contains(millions, StringComparison.Ordinal) &&
                (normalizedSource.Contains("میلیون", StringComparison.OrdinalIgnoreCase) ||
                 normalizedSource.Contains("m", StringComparison.OrdinalIgnoreCase) ||
                 normalizedSource.Contains("million", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Normalizes text for bilingual Persian/English matching:
    /// - Normalizes Persian/Arabic digits to ASCII digits (0-9).
    /// - Normalizes Arabic Yeh/Kaf to Persian.
    /// - Strips zero-width characters and punctuation.
    /// - Normalizes whitespace and folds case.
    /// </summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // Strip thousands separators between digits (e.g. 50,000 or ۵۰,۰۰۰ -> 50000)
            if ((c == ',' || c == '\u066C' || c == '\u060C') &&
                i > 0 && i + 1 < text.Length &&
                char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]))
            {
                continue;
            }

            // Persian digits: ۱ (0x06F1) -> 1
            if (c >= '\u06F0' && c <= '\u06F9')
            {
                sb.Append((char)('0' + (c - '\u06F0')));
            }
            // Arabic-Indic digits: ١ (0x0661) -> 1
            else if (c >= '\u0660' && c <= '\u0669')
            {
                sb.Append((char)('0' + (c - '\u0660')));
            }
            // Arabic Yeh -> Persian Yeh
            else if (c == '\u064A' || c == '\u0649')
            {
                sb.Append('\u06CC');
            }
            // Arabic Kaf -> Persian Keheh
            else if (c == '\u0643')
            {
                sb.Append('\u06A9');
            }
            // Strip zero-width non-joiner and zero-width spaces
            else if (c == '\u200C' || c == '\u200B' || c == '\uFEFF')
            {
                sb.Append(' ');
            }
            // Replace punctuation with spaces
            else if (char.IsPunctuation(c) && c != '-' && c != '_')
            {
                sb.Append(' ');
            }
            else
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        // Collapse excess whitespace
        var normalized = sb.ToString();
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", words);
    }
}
