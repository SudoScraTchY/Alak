using Alak.Core.Abstractions;
using Alak.Core.Capture;
using Alak.Core.Classification;
using Alak.Core.Model;

namespace Alak.Core.Tests;

// §6.1: exactly one primary category per notification; priority order breaks ties (RUL-018).
public class CategoryTests
{
    [Fact]
    public void RUL016_PriorityOrder_IsSafetyFirst()
    {
        // security > money > work > orders > people > offers > social > updates > other
        CategoryExtensions.Priority(Category.Security)
            .ShouldBeLessThan(CategoryExtensions.Priority(Category.Money));
        CategoryExtensions.Priority(Category.Money)
            .ShouldBeLessThan(CategoryExtensions.Priority(Category.Work));
        CategoryExtensions.Priority(Category.Work)
            .ShouldBeLessThan(CategoryExtensions.Priority(Category.Orders));
        CategoryExtensions.Priority(Category.Orders)
            .ShouldBeLessThan(CategoryExtensions.Priority(Category.People));
        CategoryExtensions.Priority(Category.People)
            .ShouldBeLessThan(CategoryExtensions.Priority(Category.Offers));
        CategoryExtensions.Priority(Category.Offers)
            .ShouldBeLessThan(CategoryExtensions.Priority(Category.Social));
        CategoryExtensions.Priority(Category.Social)
            .ShouldBeLessThan(CategoryExtensions.Priority(Category.Updates));
        CategoryExtensions.Priority(Category.Updates)
            .ShouldBeLessThan(CategoryExtensions.Priority(Category.Other));
    }

    [Fact]
    public void CategoryKebabIds_RoundTrip_PerSpec61()
    {
        foreach (var (cat, id) in new[] {
            (Category.Offers, "offers"), (Category.Work, "work"), (Category.People, "people"),
            (Category.Money, "money"), (Category.Orders, "orders"), (Category.Security, "security"),
            (Category.Social, "social"), (Category.Updates, "updates"), (Category.Other, "other"),
        })
        {
            cat.ToKebabId().ShouldBe(id);
            CategoryExtensions.TryParse(id, out var parsed).ShouldBeTrue();
            parsed.ShouldBe(cat);
        }
        CategoryExtensions.TryParse("nonsense", out _).ShouldBeFalse();
    }
}

// §6.2: tags are lower-kebab-case ASCII; unknown tags are rejected at the boundary.
public class TagTests
{
    [Theory]
    [InlineData("promo-code")]
    [InlineData("free-shipping")]
    [InlineData("ride-pin")]
    [InlineData("verification-code")]
    public void PCK002_KnownTags_Accepted(string tag)
        => Tag.IsValid(tag).ShouldBeTrue();

    [Theory]
    [InlineData("Invalid Tag!")]
    [InlineData("UPPER")]
    [InlineData("trailing-")]
    [InlineData("double--dash")]
    [InlineData("")]
    public void PCK002_InvalidTags_Rejected(string tag)
        => Tag.IsValid(tag).ShouldBeFalse();

    [Fact]
    public void PCK002_VocabularyContains_SpecInitialSet()
    {
        foreach (var t in new[] { "promo-code", "discount", "free-shipping", "cashback",
            "flash-sale", "expiring", "referral", "gift-credit", "no-code-needed",
            "newsletter", "unsubscribe-available", "urgent", "meeting", "mention",
            "review-requested", "build-status", "incident", "invoice", "payment-sent",
            "payment-received", "bill-due", "low-balance", "tax", "delivery",
            "out-for-delivery", "delivered", "ride", "ride-pin", "flight", "booking",
            "verification-code", "login-alert", "password-change", "missed-call",
            "message", "group-message", "like", "follow", "update-available",
            "system", "weather", "news", "reminder" })
            Tag.IsKnown(t).ShouldBeTrue();
    }
}

// §6.3: entity records; otp never carries a value.
public class EntityTests
{
    [Fact]
    public void E01_PromoCodeEntity_Shape()
    {
        var e = new PromoCodeEntity(
            Code: "SPRING25", Kind: PromoCodeKind.Promo, Discount: null, MinSpend: null,
            Cap: null, Scope: null, FirstOrder: false, SingleUse: false,
            ValidFrom: null, ValidUntil: null, StoreHint: null);
        e.Code.ShouldBe("SPRING25");
        e.Kind.ShouldBe(PromoCodeKind.Promo);
    }

    [Fact]
    public void E04_OtpEntity_HasLengthOnly_NeverValue()
    {
        var otp = new OtpEntity(Length: 6);
        otp.Length.ShouldBe(6);
        // §6.3: value never persisted — the record has no member that can hold one.
        typeof(OtpEntity).GetProperties().Select(p => p.Name)
            .ShouldNotContain("Value");
        typeof(OtpEntity).GetProperties().Select(p => p.Name)
            .ShouldNotContain("Code");
    }

    [Fact]
    public void E03_MoneyEntity_DistinguishesIrrAndIrt()
    {
        var m = new MoneyEntity(Value: 2500000m, Currency: "IRR",
            Direction: MoneyDirection.Debit, AccountHint: "4417");
        m.Currency.ShouldBe("IRR");
        m.Value.ShouldBe(2500000m);
    }
}

// §6.6: classification result + evidence trace (RUL-020 explainability).
public class ClassificationResultTests
{
    [Fact]
    public void RUL020_EvidenceTrace_CappedAtSix()
    {
        var evidence = Enumerable.Range(0, 10)
            .Select(i => new Evidence(Category.Offers, 0.1f * (i + 1), $"rule-{i}", "reason.key", null, (i, i + 4)))
            .ToArray();
        var result = new ClassificationResult(
            Category.Offers, CategorySource.Rules, 0.8f, 0.2f, NeedsReview: false,
            Tags: Array.Empty<Tag>(), Evidence: evidence, Entities: Array.Empty<Entity>());
        result.EvidenceTrace.Count.ShouldBe(6);
        result.EvidenceTrace[0].RuleId.ShouldBe("rule-9"); // strongest first
    }

    [Fact]
    public void RUL020_ConfidenceClamped_01()
    {
        var result = new ClassificationResult(
            Category.Other, CategorySource.Rules, 1.7f, -0.5f, NeedsReview: true,
            Tags: Array.Empty<Tag>(), Evidence: Array.Empty<Evidence>(), Entities: Array.Empty<Entity>());
        result.Confidence.ShouldBe(1f);
        result.Margin.ShouldBe(0f);
    }
}

// ARC-003: logic time is always via IClock.
public class ClockTests
{
    [Fact]
    public void ARC003_FixedClock_ControlsNow()
    {
        IClock clock = new FixedClock(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero));
        clock.UtcNow().UtcDateTime.ShouldBe(new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc));
    }
}

// §7.1: RawNotification DTO — exact spec shape, immutable, no Android types.
public class RawNotificationTests
{
    private static RawNotification Make() => new(
        SbnKey: "key-1", Package: "test.app", Id: 42, Tag: null,
        PostedAtUtcMs: 1_750_000_000_000L, TzOffsetMin: 210,
        ChannelId: "ch", ChannelName: "Promos", AndroidCategory: "promo", Importance: 3,
        IsOngoing: false, IsGroupSummary: false, IsForegroundService: false, IsClearable: true,
        GroupKey: null, Title: "Sale", Text: "25% off", BigText: null, SubText: null,
        TextLines: Array.Empty<string>(), ActionLabels: new[] { "Buy" },
        ConversationTitle: null, LooksRedactedByOs: false, UserHandle: 10);

    [Fact]
    public void CAP003_RawNotification_IsImmutableWithExactShape()
    {
        var n = Make();
        n.SbnKey.ShouldBe("key-1");
        n.Package.ShouldBe("test.app");
        n.PostedAtUtcMs.ShouldBe(1_750_000_000_000L);
        n.ActionLabels.Count.ShouldBe(1);
        Should.Throw<ArgumentException>(() =>
            new RawNotification(SbnKey: "", Package: "p", Id: 0, Tag: null,
                PostedAtUtcMs: 0, TzOffsetMin: 0, ChannelId: null, ChannelName: null,
                AndroidCategory: null, Importance: 0, IsOngoing: false, IsGroupSummary: false,
                IsForegroundService: false, IsClearable: false, GroupKey: null, Title: null,
                Text: null, BigText: null, SubText: null, TextLines: Array.Empty<string>(),
                ActionLabels: Array.Empty<string>(), ConversationTitle: null,
                LooksRedactedByOs: false, UserHandle: 0));
    }

    [Fact]
    public void CAP005_DefaultIgnoreFilter_MatchesSpec()
    {
        var ongoing = Make() with { IsOngoing = true };
        var fg = Make() with { IsForegroundService = true };
        var summary = Make() with { IsGroupSummary = true };
        var nav = Make() with { AndroidCategory = "navigation" };
        DefaultIgnoreFilter.ShouldIgnore(ongoing).ShouldBeTrue();
        DefaultIgnoreFilter.ShouldIgnore(fg).ShouldBeTrue();
        DefaultIgnoreFilter.ShouldIgnore(summary).ShouldBeTrue();
        DefaultIgnoreFilter.ShouldIgnore(nav).ShouldBeTrue();
        DefaultIgnoreFilter.ShouldIgnore(Make()).ShouldBeFalse();
    }
}
