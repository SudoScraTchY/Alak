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

    internal static bool IsPersianStopword(string t) => s_persianStopwords.Contains(t);

    private static readonly IReadOnlySet<string> s_caps = new HashSet<string>(new[]
    { "SALE", "OFF", "FREE", "NEW", "TODAY", "NOW", "CODE", "APP", "SHOP" }, StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> s_persianStopwords = new HashSet<string>(new[]
    {
        "خرید", "سفارش", "تومان", "تومن", "ریال", "هزار", "میلیون", "میلیارد",
        "کف", "سقف", "برای", "روی", "تا", "با", "از", "در", "به", "شد", "است",
        "تخفیف", "حراج", "فروش", "ویژه", "ارسال", "رایگان", "امشب", "امروز", "فردا"
    }, StringComparer.Ordinal);
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
        bool otpPresent,
        IReadOnlyList<ConceptHit>? globalHits = null,
        string field = "text")
    {
        var tokens = Tokens.Tokenize(n);
        var allHits = globalHits ?? hits;
        var negatives = allHits.Where(h => h.ConceptId == "promo.code.negative").ToList();
        var offerContext = allHits.Any(h => h.ConceptId is "promo.discount.word" or "promo.apply"
                                            or "promo.free_shipping" or "promo.cashback");

        foreach (var anchor in hits.Where(h => s_strong.Contains(h.ConceptId) || h.ConceptId == "promo.code.trigger.weak"))
        {
            var strong = s_strong.Contains(anchor.ConceptId);
            var isPersianAnchor = anchor.Term.Any(c => c is >= '\u0600' and <= '\u06FF');
            if (!strong)
            {
                // §8.10 #13: 'no code needed' suppresses weak-anchor extraction entirely;
                // weak anchors also require offer context (§8.6 E-01)
                var noCode = allHits.Any(h => h.ConceptId == "promo.no_code");
                if (noCode || !offerContext) continue;
            }
            var (aStart, aEnd) = anchor.Span;

            var textAfterAnchor = n.Folded[aEnd..Math.Min(n.Folded.Length, aEnd + 5)].TrimStart();
            var hasColonAfterAnchor = textAfterAnchor.StartsWith(':');
            var isForwardOnlyAnchor = hasColonAfterAnchor ||
                anchor.Term.StartsWith("با کد") ||
                anchor.Term.StartsWith("with code") ||
                anchor.Term.StartsWith("use code") ||
                anchor.Term.StartsWith("enter code") ||
                anchor.Term.StartsWith("apply code") ||
                anchor.Term.StartsWith("using code") ||
                anchor.Term.StartsWith("کد را وارد کنید") ||
                anchor.Term.StartsWith("با استفاده از کد");

            // window: up to 6 tokens after, up to 4 before (no line crossing)
            foreach (var tok in tokens)
            {
                var (ts, te) = tok.FoldedSpan;
                var after = ts >= aEnd && ts - aEnd <= 40;   // ~6 tokens
                var before = te <= aStart && aStart - te <= 25; // ~4 tokens
                if (!after && !before) continue;
                if (before && isForwardOnlyAnchor) continue;

                var candidate = tok.Value.Trim().Trim(':', '"', '\'', '«', '»', '`', '[', ']', '*', '·', ',', '.', '(', ')', '!', '?', ';');
                var trimmed = candidate;
                if (trimmed.Contains(',') || trimmed.Contains('،')) continue;

                var minLen = isPersianAnchor ? 3 : 4;
                if (trimmed.Length < minLen || trimmed.Length > 24) continue;

                var hasLatin = trimmed.Any(c => (c is >= 'a' and <= 'z') || (c is >= 'A' and <= 'Z'));
                var hasDigits = trimmed.Any(char.IsDigit);
                var hasLetters = trimmed.Any(char.IsLetter);

                if (isPersianAnchor && !hasLatin && !hasDigits) continue;

                var shape = 0f;
                if (after) shape += 0.6f;
                else if (before) shape -= 0.4f;

                if (hasLetters && hasDigits) shape += 1.5f;
                else if (hasDigits && !hasLetters)
                {
                    // numeric-only: strong anchor + offer context required, never with otp.*
                    if (!strong || !offerContext || otpPresent) continue;

                    // Numeric tokens must not be monetary amounts (e.g. 'تا 150000', '150000 تومان')
                    var textBefore = n.Folded[..ts].TrimEnd();
                    if (textBefore.EndsWith("تا") || textBefore.EndsWith("سقف") || textBefore.EndsWith("حداقل") ||
                        textBefore.EndsWith("حداکثر") || textBefore.EndsWith("کف") || textBefore.EndsWith("خرید"))
                    {
                        continue;
                    }

                    var textAfter = n.Folded[te..].TrimStart();
                    if (textAfter.StartsWith("تومان") || textAfter.StartsWith("تومن") || textAfter.StartsWith("ریال") ||
                        textAfter.StartsWith("تومانی") || textAfter.StartsWith("ریالی") ||
                        textAfter.StartsWith("هزار") || textAfter.StartsWith("میلیون") || textAfter.StartsWith("میلیارد") ||
                        textAfter.StartsWith("toman") || textAfter.StartsWith("rial") || textAfter.StartsWith("$") ||
                        textAfter.StartsWith("usd") || textAfter.StartsWith("eur"))
                    {
                        continue;
                    }

                    shape += 0.5f;
                }
                else if (trimmed.All(char.IsLetter) && trimmed.ToLowerInvariant() == trimmed)
                {
                    if (isPersianAnchor && hasLatin)
                    {
                        // Latin lowercase word like 'xyz' following a Persian anchor (e.g. 'کد xyz')
                        shape += 0.8f;
                    }
                    else
                    {
                        continue; // lowercase word in English
                    }
                }
                else if (trimmed.All(char.IsLetter) && !trimmed.All(char.IsUpper))
                {
                    // MixedCase/TitleCase word with only letters (e.g. 'Test', 'Body', 'Special')
                    if (isPersianAnchor && hasLatin && after)
                    {
                        shape += 0.4f;
                    }
                    else
                    {
                        // In English, TitleCase words like 'Test' are normal text, not promo codes
                        continue;
                    }
                }
                else shape += 0.4f;

                if (trimmed.All(char.IsUpper) && hasLetters) shape += 0.6f;
                var isStop = Tokens.IsStopwordCap(trimmed) || (isPersianAnchor && Tokens.IsPersianStopword(trimmed));
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
                        Kind: KindFor(allHits),
                        Discount: null,
                        MinSpend: null, Cap: null, Scope: null,
                        FirstOrder: allHits.Any(h => h.ConceptId == "promo.first_order"),
                        SingleUse: allHits.Any(h => h.ConceptId == "promo.single_use"),
                        ValidFrom: null, ValidUntil: null,
                        StoreHint: null,
                        Field: field,
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
    private static readonly Regex s_persianAmountDiscount = new(
        @"(?:(?<prefix>تخفیف|حراج|کسر|آف|off|تا)\s+)?(?<num>\d+(?:[.,]\d+)?)\s*(?<scale>هزار|میلیون|میلیارد|k|m)?\s*(?<curr>تومان|تومن|تومانی|ریال|ریالی)(?:\s+(?<suffix>تخفیف|تخفیفی|کسر|آف|off))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex s_persianCap = new(
        @"(?:تا\s+سقف|سقف|تا\s+حداکثر|حداکثر)\s*:?\s*(?<num>\d+(?:[.,]\d+)?)\s*(?<scale>هزار|میلیون|میلیارد|k|m)?(?:\s*(?<curr>تومان|تومن|تومانی|ریال|ریالی))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IEnumerable<DiscountEntity> Extract(NormalizedText n, string field = "text")
    {
        var folded = n.Folded;

        // check cap for percent discounts
        decimal? cap = null;
        var capMatch = s_persianCap.Match(folded);
        if (capMatch.Success)
        {
            var cNumStr = capMatch.Groups["num"].Value.Replace(",", "");
            if (decimal.TryParse(cNumStr, System.Globalization.CultureInfo.InvariantCulture, out var cNum))
            {
                var cScale = 1m;
                var cs = capMatch.Groups["scale"].Value.ToLowerInvariant();
                if (cs is "هزار" or "k") cScale = 1_000m;
                else if (cs is "میلیون" or "m") cScale = 1_000_000m;
                else if (cs is "میلیارد" or "b") cScale = 1_000_000_000m;
                cap = cNum * cScale;
            }
        }

        // percent: "25%", "25 percent", "50 percent"
        for (var i = 0; i < folded.Length; i++)
        {
            if (folded[i] != '%') continue;
            var start = i;
            while (start > 0 && (char.IsDigit(folded[start - 1]) || folded[start - 1] == '.')) start--;
            if (i > start && decimal.TryParse(folded[start..i], System.Globalization.CultureInfo.InvariantCulture, out var pct))
            {
                var (os, oe) = n.OriginalSpan(start, i + 1);
                yield return new DiscountEntity(DiscountType.Percent, pct, null, cap, field, (os, oe), 0.95f, "E-02");
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
                if (start < idx && decimal.TryParse(folded[start..idx], System.Globalization.CultureInfo.InvariantCulture, out var pct) && pct is > 0 and <= 100)
                {
                    var (os, oe) = n.OriginalSpan(start, wEnd);
                    yield return new DiscountEntity(DiscountType.Percent, pct, null, cap, field, (os, oe), 0.9f, "E-02");
                }
                idx = wEnd;
            }
        }

        // Amount discounts in Persian (e.g. "150 هزار تومن تخفیف", "150,000 تومان تخفیف")
        foreach (Match m in s_persianAmountDiscount.Matches(folded))
        {
            var hasPrefix = m.Groups["prefix"].Success;
            var hasSuffix = m.Groups["suffix"].Success;
            if (!hasPrefix && !hasSuffix) continue;

            var before = folded[..m.Index];
            if (before.EndsWith("تا سقف ", StringComparison.Ordinal) ||
                before.EndsWith("تا حداکثر ", StringComparison.Ordinal) ||
                before.EndsWith("سقف ", StringComparison.Ordinal))
            {
                continue;
            }

            var numStr = m.Groups["num"].Value.Replace(",", "");
            if (!decimal.TryParse(numStr, System.Globalization.CultureInfo.InvariantCulture, out var num))
                continue;

            var scale = 1m;
            var scaleStr = m.Groups["scale"].Value.ToLowerInvariant();
            if (scaleStr is "هزار" or "k") scale = 1_000m;
            else if (scaleStr is "میلیون" or "m") scale = 1_000_000m;
            else if (scaleStr is "میلیارد" or "b") scale = 1_000_000_000m;

            var curr = m.Groups["curr"].Value;
            var currCode = curr.Contains("ریال") ? "IRR" : "IRT";
            var amount = num * scale;

            var (os, oe) = n.OriginalSpan(m.Index, m.Index + m.Length);
            yield return new DiscountEntity(DiscountType.Amount, amount, currCode, null, field, (os, oe), 0.95f, "E-02");
        }

        // free shipping
        if (folded.Contains("free shipping") || folded.Contains("free delivery") || folded.Contains("ارسال رایگان") || folded.Contains("پست رایگان"))
        {
            var fs = folded.IndexOf("free shipping", StringComparison.Ordinal);
            if (fs < 0) fs = folded.IndexOf("free delivery", StringComparison.Ordinal);
            if (fs < 0) fs = folded.IndexOf("ارسال رایگان", StringComparison.Ordinal);
            if (fs < 0) fs = folded.IndexOf("پست رایگان", StringComparison.Ordinal);
            var len = fs >= 0 && (folded.Substring(fs).StartsWith("ارسال رایگان") || folded.Substring(fs).StartsWith("پست رایگان")) ? 11 : 13;
            var (os, oe) = n.OriginalSpan(fs, fs + len);
            yield return new DiscountEntity(DiscountType.FreeShipping, null, null, null, field, (os, oe), 0.95f, "E-02");
        }
        if (folded.Contains("cashback") || folded.Contains("cash back") || folded.Contains("بازگشت وجه") || folded.Contains("کش بک"))
        {
            var cb = folded.IndexOf("cash", StringComparison.Ordinal);
            if (cb < 0) cb = folded.IndexOf("بازگشت وجه", StringComparison.Ordinal);
            if (cb < 0) cb = folded.IndexOf("کش بک", StringComparison.Ordinal);
            var len = cb >= 0 && folded.Substring(cb).StartsWith("بازگشت وجه") ? 10 : 8;
            var (os, oe) = n.OriginalSpan(cb, cb + len);
            yield return new DiscountEntity(DiscountType.Cashback, null, null, null, field, (os, oe), 0.9f, "E-02");
        }
        if (folded.Contains("bogo") || folded.Contains("buy one get one") || folded.Contains("یکی بخر یکی ببر") || folded.Contains("1+1"))
            yield return new DiscountEntity(DiscountType.Bogo, null, null, null, field, default, 0.9f, "E-02");
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
