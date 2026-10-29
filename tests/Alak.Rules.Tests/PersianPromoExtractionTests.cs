using Alak.Core.Capture;
using Alak.Core.Model;
using Alak.Rules;
using Alak.Rules.Extraction;
using Alak.Text;

namespace Alak.Rules.Tests;

public class PersianPromoExtractionTests
{
    private static RawNotification MakeRaw(
        string title,
        string text,
        long postedAtUtcMs = 1_700_000_000_000, // Wed Nov 14 2023 22:13:20 UTC
        int tzOffsetMin = 210,                  // Iran Standard Time (+03:30)
        string package = "com.example.app") => new(
        SbnKey: "k1",
        Package: package,
        Id: 1,
        Tag: null,
        PostedAtUtcMs: postedAtUtcMs,
        TzOffsetMin: tzOffsetMin,
        ChannelId: null,
        ChannelName: null,
        AndroidCategory: null,
        Importance: 3,
        IsOngoing: false,
        IsGroupSummary: false,
        IsForegroundService: false,
        IsClearable: true,
        GroupKey: null,
        Title: title,
        Text: text,
        BigText: null,
        SubText: null,
        TextLines: Array.Empty<string>(),
        ActionLabels: Array.Empty<string>(),
        ConversationTitle: null,
        LooksRedactedByOs: false,
        UserHandle: 0);

    [Fact]
    public void ExactUserExample_ExtractedAndClassifiedAccurately()
    {
        // User example:
        // Title: 150 هزار تومن تخفیف دیجیکالا تا آخر امشب
        // Text: کد xyz کف خرید 1 میلیون
        const long postedAt = 1_700_000_000_000;
        const int tzOffset = 210;
        var n = MakeRaw("150 هزار تومن تخفیف دیجیکالا تا آخر امشب", "کد xyz کف خرید 1 میلیون", postedAt, tzOffset);

        var r = RulesEngine.Classify(n, PackSet.BuiltInDefault);

        // 1. Category is Offers with high confidence
        r.Category.ShouldBe(Category.Offers);
        r.Confidence.ShouldBeGreaterThan(0.70f);

        // 2. Extract PromoCodeEntity
        var code = r.Entities.OfType<PromoCodeEntity>().SingleOrDefault();
        code.ShouldNotBeNull();
        code!.Code.ShouldBe("xyz");

        // 3. Discount is 150,000 IRT Amount
        code.Discount.ShouldNotBeNull();
        code.Discount!.Type.ShouldBe(DiscountType.Amount);
        code.Discount.Value.ShouldBe(150000m);
        code.Discount.Currency.ShouldBe("IRT");

        // 4. MinSpend is 1,000,000 IRT
        code.MinSpend.ShouldBe(1000000m);

        // 5. ValidUntil is end of today (23:59:59)
        code.ValidUntil.ShouldNotBeNull();
        var localDay = DateTimeOffset.FromUnixTimeMilliseconds(postedAt).ToOffset(TimeSpan.FromMinutes(tzOffset));
        var expectedEndOfToday = new DateTimeOffset(localDay.Year, localDay.Month, localDay.Day, 23, 59, 59, localDay.Offset);
        code.ValidUntil.Value.ShouldBe(expectedEndOfToday);

        // 6. StoreHint is دیجیکالا
        code.StoreHint.ShouldBe("دیجیکالا");

        // 7. Verify relevant tags
        r.Tags.ShouldContain(t => t.Id == "promo-code");
        r.Tags.ShouldContain(t => t.Id == "discount");
        r.Tags.ShouldContain(t => t.Id == "expiring");
    }

    [Fact]
    public void DefaultOverload_ClassifiesWithoutExplicitPackSet()
    {
        var n = MakeRaw("150 هزار تومن تخفیف دیجیکالا تا آخر امشب", "کد xyz کف خرید 1 میلیون");
        var r = RulesEngine.Classify(n);

        r.Category.ShouldBe(Category.Offers);
        var code = r.Entities.OfType<PromoCodeEntity>().SingleOrDefault();
        code.ShouldNotBeNull();
        code!.Code.ShouldBe("xyz");
        code.Discount?.Value.ShouldBe(150000m);
    }

    [Fact]
    public void BuiltInFa_ClassifiesDirectly()
    {
        var n = MakeRaw("150 هزار تومن تخفیف دیجیکالا تا آخر امشب", "کد xyz کف خرید 1 میلیون");
        var r = RulesEngine.Classify(n, PackSet.BuiltInFa);

        r.Category.ShouldBe(Category.Offers);
        var code = r.Entities.OfType<PromoCodeEntity>().SingleOrDefault();
        code.ShouldNotBeNull();
        code!.Code.ShouldBe("xyz");
        code.MinSpend.ShouldBe(1000000m);
    }

    [Fact]
    public void RelativeExpiry_Tomorrow_SetsEndOfTomorrow()
    {
        const long postedAt = 1_700_000_000_000;
        const int tzOffset = 210;
        var n = MakeRaw("50 هزار تومان تخفیف اسنپ‌فود", "کد food50 تا آخر فردا معتبر است", postedAt, tzOffset);

        var r = RulesEngine.Classify(n);

        var code = r.Entities.OfType<PromoCodeEntity>().SingleOrDefault();
        code.ShouldNotBeNull();
        code!.Code.ShouldBe("food50");
        code.StoreHint.ShouldBe("اسنپ‌فود");

        code.ValidUntil.ShouldNotBeNull();
        var localDay = DateTimeOffset.FromUnixTimeMilliseconds(postedAt).ToOffset(TimeSpan.FromMinutes(tzOffset));
        var tomorrow = localDay.AddDays(1);
        var expectedTomorrow = new DateTimeOffset(tomorrow.Year, tomorrow.Month, tomorrow.Day, 23, 59, 59, localDay.Offset);
        code.ValidUntil.Value.ShouldBe(expectedTomorrow);
    }

    [Theory]
    [InlineData("کف خرید 100 هزار تومان", 100000)]
    [InlineData("حداقل خرید 2 میلیون", 2000000)]
    [InlineData("برای خریدهای بالای 500,000 تومان", 500000)]
    [InlineData("کف خرید ۱ میلیون", 1000000)]
    public void MinSpendExtractor_ParsesPersianConstraints(string text, decimal expected)
    {
        var n = Normalizer.Normalize(text);
        var minSpend = MinSpendExtractor.Extract(n);
        minSpend.ShouldBe(expected);
    }

    [Theory]
    [InlineData("دیجی‌کالا", "دیجیکالا")]
    [InlineData("دیجیکالا", "دیجیکالا")]
    [InlineData("اسنپ فود", "اسنپ‌فود")]
    [InlineData("تپسی", "تپسی")]
    [InlineData("اکالا", "اکالا")]
    [InlineData("باسلام", "باسلام")]
    public void StoreExtractor_DetectsKnownMerchants(string merchantInText, string expectedCanonical)
    {
        var n = MakeRaw("تخفیف ویژه", $"خرید از {merchantInText} با تخفیف");
        var title = Normalizer.Normalize(n.Title!);
        var text = Normalizer.Normalize(n.Text!);
        var detected = StoreExtractor.DetectStore(n, title, text);
        detected.ShouldBe(expectedCanonical);
    }

    [Fact]
    public void AmountDiscount_WithCommaSeparatedRials()
    {
        var n = MakeRaw("حراج", "150,000 ریال تخفیف با کد OFF15");
        var r = RulesEngine.Classify(n);

        var d = r.Entities.OfType<DiscountEntity>().SingleOrDefault();
        d.ShouldNotBeNull();
        d!.Type.ShouldBe(DiscountType.Amount);
        d.Value.ShouldBe(150000m);
        d.Currency.ShouldBe("IRR");
    }

    [Fact]
    public void DigikalaNotification_ExtractsCorrectPromoCode_NotDiscountAmount()
    {
        var n = MakeRaw("تخفیف آخر هفته‌ات رسید🎁", "تا ۱۵۰,۰۰۰ تومان با کد: DDJHG\nبرای خرید بالای ۱ میلیون تومان🛍️ \nفقط امروز👇", package: "com.digikala");
        var r = RulesEngine.Classify(n);

        var promo = r.Entities.OfType<PromoCodeEntity>().FirstOrDefault();
        promo.ShouldNotBeNull();
        promo!.Code.ShouldBe("DDJHG");
        promo.Discount.ShouldNotBeNull();
        promo.Discount!.Value.ShouldBe(150000m);
    }

    [Fact]
    public void EnglishNotification_ExtractsPromoCodeAfterTrigger_NotNormalCapitalizedWordBefore()
    {
        var n = MakeRaw("Test Title", "Test Message Body with promo code SAVE50");
        var r = RulesEngine.Classify(n);

        var promo = r.Entities.OfType<PromoCodeEntity>().FirstOrDefault();
        promo.ShouldNotBeNull();
        promo!.Code.ShouldBe("SAVE50");
    }
}
