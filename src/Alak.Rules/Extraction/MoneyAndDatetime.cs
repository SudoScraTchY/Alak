using Alak.Core.Classification;
using Alak.Core.Model;
using Alak.Text;

namespace Alak.Rules.Extraction;

/// <summary>E-03: money amounts with currency and direction (spec §8.7 E-03).</summary>
public static class MoneyExtractor
{
    public static IEnumerable<MoneyEntity> Extract(NormalizedText n, IReadOnlyList<ConceptHit> hits, string field = "text")
    {
        var folded = n.Folded;
        // $45.00 / €5 / £20 in front; "45.00 dollars" / "2500000 rial" behind
        var dir = Direction(hits);

        for (var i = 0; i < folded.Length; i++)
        {
            if (folded[i] is not ('$' or '€' or '£')) continue;
            var j = i + 1;
            while (j < folded.Length && (char.IsDigit(folded[j]) || folded[j] == '.' || folded[j] == ',')) j++;
            if (j > i + 1 && decimal.TryParse(folded[(i + 1)..j], System.Globalization.CultureInfo.InvariantCulture, out var amount))
            {
                var cur = folded[i] switch { '$' => "USD", '€' => "EUR", '£' => "GBP", _ => "" };
                var (os, oe) = n.OriginalSpan(i, j);
                yield return new MoneyEntity(amount, cur, dir, AccountHint(hits, folded, j), field, (os, oe), 0.95f, "E-03");
            }
        }

        // Named currencies: "<amount> ریال|تومان|تومن|rial|toman|dollars|eur|usd|درهم|yuan" and
        // Persian "N هزار تومان" (N thousand toman), "N میلیون تومان" (N million).
        // Folded view has ASCII digits and , . separators (NRM-005), ZWNJ → space (NRM-003).
        (string Word, string Code, decimal Mult)[] currencies =
        {
            ("ریال", "IRR", 1m), ("rials", "IRR", 1m), ("rial", "IRR", 1m),
            ("تومان", "IRT", 1m), ("تومن", "IRT", 1m), ("toman", "IRT", 1m), ("tumans", "IRT", 1m),
            ("dollars", "USD", 1m), ("dollar", "USD", 1m),
            ("euros", "EUR", 1m), ("euro", "EUR", 1m),
            ("درهم", "AED", 1m), ("دینار", "IQD", 1m),
        };
        foreach (var (word, code, mult) in currencies)
        {
            var idx = 0;
            while ((idx = folded.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
            {
                var checkIdx = idx;
                while (checkIdx > 0 && char.IsWhiteSpace(folded[checkIdx - 1])) checkIdx--;

                decimal scale = 1m;
                if (folded[..checkIdx].EndsWith("هزار", StringComparison.Ordinal)) { scale = 1_000m; checkIdx -= 4; }
                else if (folded[..checkIdx].EndsWith("میلیون", StringComparison.Ordinal)) { scale = 1_000_000m; checkIdx -= 6; }
                else if (folded[..checkIdx].EndsWith("میلیارد", StringComparison.Ordinal)) { scale = 1_000_000_000m; checkIdx -= 7; }

                while (checkIdx > 0 && char.IsWhiteSpace(folded[checkIdx - 1])) checkIdx--;
                var numEnd = checkIdx;
                while (checkIdx > 0 && (char.IsDigit(folded[checkIdx - 1]) || folded[checkIdx - 1] is ',' or '.')) checkIdx--;
                var numStart = checkIdx;

                var beforeAmt = folded[..Math.Max(0, numStart)];
                if (beforeAmt.EndsWith("خرید بالای ", StringComparison.Ordinal) ||
                    beforeAmt.EndsWith("خریدهای بالای ", StringComparison.Ordinal) ||
                    beforeAmt.EndsWith("سفارش بالای ", StringComparison.Ordinal) ||
                    beforeAmt.EndsWith("کف خرید ", StringComparison.Ordinal) ||
                    beforeAmt.EndsWith("حداقل خرید ", StringComparison.Ordinal) ||
                    beforeAmt.EndsWith("تخفیف ", StringComparison.Ordinal))
                {
                    idx += word.Length;
                    continue;
                }
                var afterWord = folded[(idx + word.Length)..];
                if (afterWord.TrimStart().StartsWith("تخفیف", StringComparison.Ordinal))
                {
                    idx += word.Length;
                    continue;
                }

                if (numEnd > numStart && decimal.TryParse(folded[numStart..numEnd].Replace(",", ""), System.Globalization.CultureInfo.InvariantCulture, out var amt))
                {
                    var (os, oe) = n.OriginalSpan(Math.Max(0, numStart), idx + word.Length);
                    yield return new MoneyEntity(amt * scale * mult, code, dir, null, field, (os, oe), 0.9f, "E-03");
                }
                idx += word.Length;
            }
        }
    }

    private static MoneyDirection Direction(IReadOnlyList<ConceptHit> hits)
    {
        if (hits.Any(h => h.ConceptId == "money.debit")) return MoneyDirection.Debit;
        if (hits.Any(h => h.ConceptId is "money.credit" or "money.refund")) return MoneyDirection.Credit;
        if (hits.Any(h => h.ConceptId == "money.balance")) return MoneyDirection.Balance;
        return MoneyDirection.Unknown;
    }

    private static string? AccountHint(IReadOnlyList<ConceptHit> hits, string folded, int from)
    {
        // "ending 4417", "card *4417" → last 4 only (SEC-004)
        var m = System.Text.RegularExpressions.Regex.Match(folded[from..], @"(?:ending|card\s*\*?|كارت)\s*\*?\s*(\d{4})\b");
        return m.Success ? m.Groups[1].Value : null;
    }
}

/// <summary>E-04: date/time extraction (DAT-001..006 subset: clock times and relative Persian expiry dates).</summary>
public static class DatetimeExtractor
{
    private static readonly (string Pattern, bool IsTomorrow)[] s_relativeExpiryPhrases =
    {
        ("تا پایان امشب", false),
        ("تا پایان امروز", false),
        ("تا آخر امشب", false),
        ("تا اخر امشب", false),
        ("تا آخر امروز", false),
        ("تا اخر امروز", false),
        ("فقط امروز", false),
        ("تا آخر فردا", true),
        ("تا اخر فردا", true),
        ("تا فردا شب", true),
        ("تا پایان فردا", true),
        ("تا فردا", true),
        ("امشب", false),
        ("امروز", false),
        ("فردا", true),
    };

    public static IEnumerable<DatetimeEntity> Extract(NormalizedText n, long postedAtUtcMs, int tzOffsetMin, string field = "text")
    {
        var folded = n.Folded;
        var localDay = DateTimeOffset.FromUnixTimeMilliseconds(postedAtUtcMs)
            .ToOffset(TimeSpan.FromMinutes(tzOffsetMin));

        // Relative Persian expiry expressions (امشب, تا آخر امشب, فردا, ...)
        var matchedSpans = new List<(int Start, int End)>();
        foreach (var (phrase, isTomorrow) in s_relativeExpiryPhrases)
        {
            var idx = 0;
            while ((idx = folded.IndexOf(phrase, idx, StringComparison.Ordinal)) >= 0)
            {
                var end = idx + phrase.Length;
                if (phrase is "امشب" or "امروز" or "فردا")
                {
                    var validBefore = idx == 0 || char.IsWhiteSpace(folded[idx - 1]) || char.IsPunctuation(folded[idx - 1]);
                    var validAfter = end == folded.Length || char.IsWhiteSpace(folded[end]) || char.IsPunctuation(folded[end]);
                    if (!validBefore || !validAfter)
                    {
                        idx = end;
                        continue;
                    }
                }

                if (matchedSpans.Any(s => idx >= s.Start && end <= s.End))
                {
                    idx = end;
                    continue;
                }

                matchedSpans.Add((idx, end));
                var targetDay = isTomorrow ? localDay.AddDays(1) : localDay;
                var expiry = new DateTimeOffset(targetDay.Year, targetDay.Month, targetDay.Day, 23, 59, 59, localDay.Offset);
                var (os, oe) = n.OriginalSpan(idx, end);
                yield return new DatetimeEntity(
                    Role: DatetimeRole.Expiry,
                    Instant: expiry,
                    Precision: DatetimePrecision.Time,
                    Calendar: DatetimeCalendar.Gregorian,
                    Ambiguous: false,
                    Field: field,
                    Span: (os, oe),
                    Confidence: 0.95f,
                    RuleId: "E-04");

                idx = end;
            }
        }

        // HH:MM (Persian digits normalized to ASCII in the folded view)
        for (var i = 0; i < folded.Length - 4; i++)
        {
            if (!char.IsDigit(folded[i])) continue;
            var j = i;
            while (j < folded.Length && char.IsDigit(folded[j])) j++;
            if (j < folded.Length && folded[j] == ':' && char.IsDigit(folded[j + 1]))
            {
                var k = j + 1;
                while (k < folded.Length && char.IsDigit(folded[k])) k++;
                if (int.TryParse(folded[i..j], out var hh) && int.TryParse(folded[(j + 1)..k], out var mm)
                    && hh is >= 0 and <= 23 && mm is >= 0 and <= 59)
                {
                    var (os, oe) = n.OriginalSpan(i, k);
                    var local = new DateTimeOffset(localDay.Year, localDay.Month, localDay.Day, hh, mm, 0, localDay.Offset);
                    yield return new DatetimeEntity(
                        Role: DatetimeRole.Start, Instant: local,
                        Precision: DatetimePrecision.Time, Calendar: DatetimeCalendar.Gregorian,
                        Ambiguous: false,
                        Field: field, Span: (os, oe), Confidence: 0.9f, RuleId: "E-04");
                }
                i = k;
            }
        }
    }
}
