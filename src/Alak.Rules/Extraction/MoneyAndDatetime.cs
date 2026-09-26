using Alak.Core.Classification;
using Alak.Core.Model;
using Alak.Text;

namespace Alak.Rules.Extraction;

/// <summary>E-03: money amounts with currency and direction (spec §8.7 E-03).</summary>
public static class MoneyExtractor
{
    public static IEnumerable<MoneyEntity> Extract(NormalizedText n, IReadOnlyList<ConceptHit> hits)
    {
        var folded = n.Folded;
        // $45.00 / €5 / £20 in front; "45.00 dollars" / "2500000 rial" behind
        var dir = Direction(hits);

        for (var i = 0; i < folded.Length; i++)
        {
            if (folded[i] is not ('$' or '€' or '£')) continue;
            var j = i + 1;
            while (j < folded.Length && (char.IsDigit(folded[j]) || folded[j] == '.' || folded[j] == ',')) j++;
            if (j > i + 1 && decimal.TryParse(folded[(i + 1)..j], out var amount))
            {
                var cur = folded[i] switch { '$' => "USD", '€' => "EUR", '£' => "GBP", _ => "" };
                var (os, oe) = n.OriginalSpan(i, j);
                yield return new MoneyEntity(amount, cur, dir, AccountHint(hits, folded, j), "text", (os, oe), 0.95f, "E-03");
            }
        }

        // Named currencies: "<amount> ریال|تومان|rial|toman|dollars|eur|usd|درهم|yuan" and
        // Persian "N هزار تومان" (N thousand toman), "N میلیون تومان" (N million).
        // Folded view has ASCII digits and , . separators (NRM-005), ZWNJ → space (NRM-003).
        (string Word, string Code, decimal Mult)[] currencies =
        {
            ("ریال", "IRR", 1m), ("rials", "IRR", 1m), ("rial", "IRR", 1m),
            ("تومان", "IRT", 1m), ("toman", "IRT", 1m), ("tumans", "IRT", 1m),
            ("dollars", "USD", 1m), ("dollar", "USD", 1m),
            ("euros", "EUR", 1m), ("euro", "EUR", 1m),
            ("درهم", "AED", 1m), ("دینار", "IQD", 1m),
        };
        foreach (var (word, code, mult) in currencies)
        {
            var idx = 0;
            while ((idx = folded.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
            {
                // digits before the currency word, allowing one space between them
                var end = idx;
                var start = end;
                if (start > 0 && char.IsWhiteSpace(folded[start - 1])) start--; // one space ok
                while (start > 0 && (char.IsDigit(folded[start - 1]) || folded[start - 1] is ',' or '.')) start--;
                // scale words: "هزار" (thousand) / "میلیون" (million) between number and تومان
                decimal scale = 1m;
                var k = start;
                if (start > 0)
                {
                    var between = folded[..(start - 1)];
                    if (between.EndsWith("هزار", StringComparison.Ordinal)) { scale = 1_000m; k -= 5; }
                    else if (between.EndsWith("میلیون", StringComparison.Ordinal)) { scale = 1_000_000m; k -= 7; }
                }
                if (end > start && decimal.TryParse(folded[start..end].Replace(",", ""), out var amt))
                {
                    var (os, oe) = n.OriginalSpan(Math.Max(0, k), end + word.Length);
                    yield return new MoneyEntity(amt * scale * mult, code, dir, null, "text", (os, oe), 0.9f, "E-03");
                }
                idx = end + word.Length;
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

/// <summary>E-04: date/time extraction (DAT-001..006 subset: clock times and "tonight").</summary>
public static class DatetimeExtractor
{
    public static IEnumerable<DatetimeEntity> Extract(NormalizedText n, long postedAtUtcMs, int tzOffsetMin)
    {
        var folded = n.Folded;
        var localDay = DateTimeOffset.FromUnixTimeMilliseconds(postedAtUtcMs)
            .ToOffset(TimeSpan.FromMinutes(tzOffsetMin));
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
                        Field: "text", Span: (os, oe), Confidence: 0.9f, RuleId: "E-04");
                }
                i = k;
            }
        }
    }
}
