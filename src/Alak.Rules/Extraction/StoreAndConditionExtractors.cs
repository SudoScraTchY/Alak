using System.Text.RegularExpressions;
using Alak.Core.Capture;
using Alak.Text;

namespace Alak.Rules.Extraction;

/// <summary>
/// Condition / minimum spend extractor for promotional notifications.
/// Detects constraints such as 'کف خرید 1 میلیون' or 'حداقل خرید 100 هزار تومان'.
/// </summary>
public static class MinSpendExtractor
{
    private static readonly Regex s_minSpendRegex = new(
        @"(?:کف\s+(?:خرید|سفارش|مبلغ)?|حداقل\s+(?:خرید|سفارش|مبلغ|اعتبار)?|برای\s+خریدهای\s+(?:بالای|بیش\s+از)|برای\s+خرید\s+(?:بالای|بیش\s+از)|خرید(?:های)?\s+(?:بالای|بیش\s+از)|سفارش(?:های)?\s+(?:بالای|بیش\s+از)|min(?:imum)?\s+spend|orders?\s+over)\s*:?\s*(?<num>\d+(?:[.,]\d+)?)\s*(?<scale>هزار|میلیون|میلیارد|k|m)?(?:\s*(?<curr>تومان|تومن|تومانی|ریال|ریالی|dollars?|usd))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static decimal? Extract(NormalizedText n)
    {
        var match = s_minSpendRegex.Match(n.Folded);
        if (!match.Success) return null;

        var numStr = match.Groups["num"].Value.Replace(",", "");
        if (!decimal.TryParse(numStr, System.Globalization.CultureInfo.InvariantCulture, out var num))
            return null;

        var scale = 1m;
        var scaleStr = match.Groups["scale"].Value.ToLowerInvariant();
        if (scaleStr is "هزار" or "k") scale = 1_000m;
        else if (scaleStr is "میلیون" or "m") scale = 1_000_000m;
        else if (scaleStr is "میلیارد" or "b") scale = 1_000_000_000m;

        return num * scale;
    }
}

/// <summary>
/// Common merchant / store detection for notifications.
/// </summary>
public static class StoreExtractor
{
    private static readonly (string Canonical, string[] Aliases)[] s_merchants =
    {
        ("دیجیکالا", new[] { "دیجیکالا", "دیجی کالا", "دیجی‌کالا", "digikala", "com.digikala" }),
        ("دیجی‌پلاس", new[] { "دیجی‌پلاس", "دیجی پلاس", "digiplus" }),
        ("دیجی‌پی", new[] { "دیجی‌پی", "دیجی پی", "digipay" }),
        ("اسنپ‌فود", new[] { "اسنپ‌فود", "اسنپ فود", "snappfood", "cab.snapp.food" }),
        ("اسنپ‌مارکت", new[] { "اسنپ‌مارکت", "اسنپ مارکت", "snappmarket" }),
        ("اسنپ‌شاپ", new[] { "اسنپ‌شاپ", "اسنپ شاپ", "snappshop" }),
        ("اسنپ‌باکس", new[] { "اسنپ‌باکس", "اسنپ باکس", "snappbox" }),
        ("اسنپ‌دکتر", new[] { "اسنپ‌دکتر", "اسنپ دکتر", "snappdoctor" }),
        ("اسنپ‌تریپ", new[] { "اسنپ‌تریپ", "اسنپ تریپ", "snapptrip" }),
        ("اسنپ", new[] { "اسنپ", "snapp", "cab.snapp.passenger", "cab.snapp.driver" }),
        ("تپسی", new[] { "تپسی", "tapsi", "tap33", "taxi.tap30.passenger" }),
        ("اکالا", new[] { "اکالا", "اوکالا", "okala" }),
        ("باسلام", new[] { "باسلام", "basalam" }),
        ("کافه‌بازار", new[] { "کافه‌بازار", "کافه بازار", "cafebazaar" }),
        ("مایکت", new[] { "مایکت", "myket" }),
        ("سینماتیکت", new[] { "سینماتیکت", "سینما تیکت", "cinematicket" }),
        ("فیلیمو", new[] { "فیلیمو", "filimo" }),
        ("نماوا", new[] { "نماوا", "namava" }),
        ("خانومی", new[] { "خانومی", "khanoumi" }),
        ("بانی‌مد", new[] { "بانی‌مد", "بانی مد", "banimode" }),
        ("علی‌بابا", new[] { "علی‌بابا", "علی بابا", "alibaba" }),
        ("فلای‌تودی", new[] { "فلای‌تودی", "فلای تودی", "flytoday" }),
        ("جاباما", new[] { "جاباما", "jabama" }),
        ("اتاقک", new[] { "اتاقک", "otaghak" }),
        ("ازکی", new[] { "ازکی", "azki" }),
        ("بیمه‌دات‌کام", new[] { "بیمه‌دات‌کام", "بیمه دات کام", "bimeh" }),
        ("دیوار", new[] { "دیوار", "divar" }),
        ("شیپور", new[] { "شیپور", "sheypoor" }),
        ("ترب", new[] { "ترب", "torob" }),
        ("ایمالز", new[] { "ایمالز", "emalls" }),
    };

    public static string? DetectStore(RawNotification n, NormalizedText title, NormalizedText text)
    {
        var titleFolded = title.Folded;
        var textFolded = text.Folded;
        var pkg = n.Package?.ToLowerInvariant() ?? "";

        foreach (var (canonical, aliases) in s_merchants)
        {
            foreach (var alias in aliases)
            {
                var foldedAlias = Normalizer.Normalize(alias).Folded;
                if (!string.IsNullOrEmpty(foldedAlias) &&
                    (titleFolded.Contains(foldedAlias, StringComparison.Ordinal) ||
                     textFolded.Contains(foldedAlias, StringComparison.Ordinal) ||
                     (!string.IsNullOrEmpty(pkg) && pkg.Contains(alias, StringComparison.OrdinalIgnoreCase))))
                {
                    return canonical;
                }
            }
        }
        return null;
    }
}
