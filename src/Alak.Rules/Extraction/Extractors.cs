using System.Text.RegularExpressions;
using Alak.Core.Classification;
using Alak.Core.Model;
using Alak.Text;

namespace Alak.Rules.Extraction;

/// <summary>
/// Candidate utilities shared by extractors. Tokens are taken from the ORIGINAL
/// (case-preserved) text via the span map, so extracted values keep their sent form.
/// </summary>
internal static class Tokens
{
    internal readonly record struct Token(string Value, (int Start, int End) FoldedSpan);

    /// <summary>Tokenize the folded view; values come from original text via the map.</summary>
    internal static List<Token> Tokenize(NormalizedText n)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < n.Folded.Length)
        {
            if (char.IsWhiteSpace(n.Folded[i])) { i++; continue; }
            var start = i;
            while (i < n.Folded.Length && !char.IsWhiteSpace(n.Folded[i])) i++;
            tokens.Add(new Token(OriginalSlice(n, start, i), (start, i)));
        }
        return tokens;
    }

    internal static string OriginalSlice(NormalizedText n, int foldedStart, int foldedEnd)
    {
        if (foldedEnd <= foldedStart) return string.Empty;
        var (os, oe) = n.OriginalSpan(foldedStart, foldedEnd);
        return oe <= os ? string.Empty : n.Text[Math.Clamp(os, 0, n.Text.Length)..Math.Clamp(oe, 0, n.Text.Length)];
    }

    internal static bool LooksLikeUrl(string t)
        => t.Contains("://") || t.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
           || t.EndsWith(".com", StringComparison.OrdinalIgnoreCase)
           || t.EndsWith(".ir", StringComparison.OrdinalIgnoreCase)
           || t.EndsWith(".org", StringComparison.OrdinalIgnoreCase)
           || t.EndsWith(".net", StringComparison.OrdinalIgnoreCase);

    internal static bool IsStopwordCap(string t) => s_caps.Contains(t);

    private static readonly IReadOnlySet<string> s_caps = new HashSet<string>(new[]
    { "SALE", "OFF", "FREE", "NEW", "TODAY", "NOW", "CODE", "APP", "SHOP" }, StringComparer.Ordinal);
}

/// <summary>
/// E-01: promo code extraction — anchors, windows, shape scoring (spec §8.7 E-01).
/// </summary>
public static class PromoCodeExtractor
{
    private static readonly IReadOnlySet<string> s_strong = new HashSet<string>(
        new[] { "promo.code.trigger", "promo.code.referral", "promo.code.gift" });

    public static IEnumerable<PromoCodeEntity> Extract(
        NormalizedText n,
        IReadOnlyList<ConceptHit> hits,
        bool otpPresent)
    {
        var tokens = Tokens.Tokenize(n);
        var negatives = hits.Where(h => h.ConceptId == "promo.code.negative").ToList();
        var offerContext = hits.Any(h => h.ConceptId is "promo.discount.word" or "promo.apply"
                                            or "promo.free_shipping" or "promo.cashback");

        foreach (var anchor in hits.Where(h => s_strong.Contains(h.ConceptId) || h.ConceptId == "promo.code.trigger.weak"))
        {
            var strong = s_strong.Contains(anchor.ConceptId);
            var (aStart, aEnd) = anchor.Span;

            // window: up to 6 tokens after, up to 4 before (no line crossing)
            foreach (var tok in tokens)
            {
                var (ts, te) = tok.FoldedSpan;
                var after = ts >= aEnd && ts - aEnd <= 40;   // ~6 tokens
                var before = te <= aStart && aStart - te <= 25; // ~4 tokens
                if (!after && !before) continue;

                var candidate = tok.Value.Trim().Trim(':', '"', '\'', '«', '»', '`', '[', ']', '*', '·', ',', '.', '(', ')');
                var trimmed = candidate;
                if (trimmed.Length is < 4 or > 24) continue;

                var shape = 0f;
                var hasLetters = trimmed.Any(char.IsLetter);
                var hasDigits = trimmed.Any(char.IsDigit);
                if (hasLetters && hasDigits) shape += 1.5f;
                else if (hasDigits && !hasLetters)
                {
                    // numeric-only: strong anchor + offer context required, never with otp.*
                    if (!strong || !offerContext || otpPresent) continue;
                    shape += 0.5f;
                }
                else if (trimmed.All(char.IsLetter) && trimmed.ToLowerInvariant() == trimmed) continue; // lowercase word
                else shape += 0.4f;

                if (trimmed.All(char.IsUpper) && hasLetters) shape += 0.6f;
                var isStop = Tokens.IsStopwordCap(trimmed);
                if (isStop) continue;
                if (Tokens.LooksLikeUrl(trimmed)) continue;

                // negative contexts subtract
                var nearNegative = negatives.Any(neg =>
                    Math.Abs(neg.Span.Start - ts) < 30);
                var conf = Sigmoid(strong ? 1.2f : 0.4f, shape, offerContext ? 0.8f : 0f,
                                   nearNegative ? -1.5f : 0f);
                if (conf >= 0.60f)
                {
                    var (os, oe) = n.OriginalSpan(ts, te);
                    yield return new PromoCodeEntity(
                        Code: trimmed,
                        Kind: KindFor(hits),
                        Discount: null,
                        MinSpend: null, Cap: null, Scope: null,
                        FirstOrder: hits.Any(h => h.ConceptId == "promo.first_order"),
                        SingleUse: hits.Any(h => h.ConceptId == "promo.single_use"),
                        ValidFrom: null, ValidUntil: null,
                        StoreHint: null,
                        Field: "text",
                        Span: (os, oe),
                        Confidence: conf,
                        RuleId: "E-01");
                }
            }
        }
    }

    private static PromoCodeKind KindFor(IReadOnlyList<ConceptHit> hits)
    {
        if (hits.Any(h => h.ConceptId == "promo.code.referral")) return PromoCodeKind.Referral;
        if (hits.Any(h => h.ConceptId is "promo.code.gift")) return PromoCodeKind.Gift;
        if (hits.Any(h => h.ConceptId == "promo.code.trigger" && h.Term.Contains("voucher"))) return PromoCodeKind.Voucher;
        return PromoCodeKind.Promo;
    }

    private static float Sigmoid(params float[] xs)
    {
        var z = xs.Sum();
        return 1f / (1f + MathF.Exp(-z));
    }
}

/// <summary>E-06: OTP detection (privacy critical). Entity carries length only.</summary>
public static class OtpExtractor
{
    public static OtpEntity? Extract(NormalizedText n, IReadOnlyList<ConceptHit> hits)
    {
        var trigger = hits.FirstOrDefault(h => h.ConceptId is "otp.trigger" or "otp.warning");
        if (trigger is null) return null;

        var tokens = Tokens.Tokenize(n);
        foreach (var tok in tokens)
        {
            var digits = tok.Value.Trim('.', ',', '!', ':').Count(char.IsDigit);
            var clean = tok.Value.Trim('.', ',', '!', ':');
            if (clean.Length >= 4 && clean.Length <= 8
                && digits == clean.Length
                && Math.Abs(tok.FoldedSpan.Start - trigger.Span.End) < 40)
            {
                var (os, oe) = n.OriginalSpan(tok.FoldedSpan.Start, tok.FoldedSpan.End);
                return new OtpEntity(clean.Length, "text", (os, oe), 0.99f, "E-06");
            }
            // alphanumeric G-123456
            if (clean.Length is >= 6 and <= 10 && clean.Count(char.IsDigit) >= 5
                && clean.Contains('-') && char.IsLetter(clean[0])
                && Math.Abs(tok.FoldedSpan.Start - trigger.Span.End) < 40)
            {
                var (os, oe) = n.OriginalSpan(tok.FoldedSpan.Start, tok.FoldedSpan.End);
                return new OtpEntity(clean.Length, "text", (os, oe), 0.9f, "E-06");
            }
        }
        return null;
    }
}

/// <summary>E-02: percent/amount discounts, free shipping, BOGO, cashback, credit.</summary>
public static class DiscountExtractor
{
    public static IEnumerable<DiscountEntity> Extract(NormalizedText n)
    {
        // percent: "25%", "25 percent", "50 percent"
        var folded = n.Folded;
        for (var i = 0; i < folded.Length; i++)
        {
            if (folded[i] != '%') continue;
            var start = i;
            while (start > 0 && (char.IsDigit(folded[start - 1]) || folded[start - 1] == '.')) start--;
            if (i > start && decimal.TryParse(folded[start..i], out var pct))
            {
                var (os, oe) = n.OriginalSpan(start, i + 1);
                yield return new DiscountEntity(DiscountType.Percent, pct, null, null, "text", (os, oe), 0.95f, "E-02");
            }
        }
        // "50 percent off" / "۳۰ درصد تخفیف": number + percent-word (E-02)
        var percentWords = new[] { "percent", "pct", "درصد" };
        foreach (var pw in percentWords)
        {
            var idx = 0;
            while ((idx = folded.IndexOf(' ' + pw, idx, StringComparison.Ordinal)) >= 0)
            {
                var wEnd = idx + 1 + pw.Length;
                if (wEnd < folded.Length && char.IsLetterOrDigit(folded[wEnd])) { idx = wEnd; continue; }
                var start = idx;
                while (start > 0 && (char.IsDigit(folded[start - 1]) || folded[start - 1] == '.')) start--;
                if (start < idx && decimal.TryParse(folded[start..idx], out var pct) && pct is > 0 and <= 100)
                {
                    var (os, oe) = n.OriginalSpan(start, wEnd);
                    yield return new DiscountEntity(DiscountType.Percent, pct, null, null, "text", (os, oe), 0.9f, "E-02");
                }
                idx = wEnd;
            }
        }
        // free shipping
        if (folded.Contains("free shipping") || folded.Contains("free delivery"))
        {
            var fs = folded.IndexOf("free shipping", StringComparison.Ordinal);
            if (fs < 0) fs = folded.IndexOf("free delivery", StringComparison.Ordinal);
            var (os, oe) = n.OriginalSpan(fs, fs + 13);
            yield return new DiscountEntity(DiscountType.FreeShipping, null, null, null, "text", (os, oe), 0.95f, "E-02");
        }
        if (folded.Contains("cashback") || folded.Contains("cash back"))
        {
            var cb = folded.IndexOf("cash", StringComparison.Ordinal);
            var (os, oe) = n.OriginalSpan(cb, cb + 8);
            yield return new DiscountEntity(DiscountType.Cashback, null, null, null, "text", (os, oe), 0.9f, "E-02");
        }
        if (folded.Contains("bogo") || folded.Contains("buy one get one"))
            yield return new DiscountEntity(DiscountType.Bogo, null, null, null, "text", default, 0.9f, "E-02");
    }
}

/// <summary>E-05: order/tracking/PIN references. These veto promo-code candidates.</summary>
public static class OrderRefExtractor
{
    private static readonly Regex s_order = new(@"(?:order\s*#?\s*|order\s+number\s*:?\s*)([a-z0-9\-]{3,20})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex s_ups = new(@"\b1Z[0-9A-Z]{16}\b", RegexOptions.Compiled);

    public static IEnumerable<OrderRefEntity> Extract(NormalizedText n)
    {
        foreach (Match m in s_order.Matches(n.Text))
        {
            var (os, oe) = n.OriginalSpan(m.Index, m.Index + m.Length);
            yield return new OrderRefEntity(OrderRefKind.Order, m.Groups[1].Value, "text", (os, oe), 0.9f, "E-05");
        }
        foreach (Match m in s_ups.Matches(n.Text))
        {
            var (os, oe) = n.OriginalSpan(m.Index, m.Index + m.Length);
            yield return new OrderRefEntity(OrderRefKind.Tracking, m.Value, "text", (os, oe), 0.95f, "E-05");
        }
    }
}
