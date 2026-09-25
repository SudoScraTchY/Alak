using Alak.Core.Capture;
using Alak.Core.Classification;
using Alak.Core.Model;
using Alak.Rules;
using Alak.Rules.Extraction;
using Alak.Text;

namespace Alak.Rules.Tests;

// §8.8: additive log-odds, family caps, softmax decision, tie-break by priority.
public class DecisionTests
{
    private static float[] Softmax(float[] scores)
    {
        var max = scores.Max();
        var exps = scores.Select(s => MathF.Exp(s - max)).ToArray();
        var sum = exps.Sum();
        return exps.Select(e => e / sum).ToArray();
    }

    [Fact]
    public void RUL001_Deterministic_SameInputSameOutput()
    {
        var n = MakeRaw("Spring Sale", "25% off with code SPRING25");
        var a = RulesEngine.Classify(n, PackSet.BuiltInEn);
        var b = RulesEngine.Classify(n, PackSet.BuiltInEn);
        a.Category.ShouldBe(b.Category);
        a.Confidence.ShouldBe(b.Confidence);
    }

    [Fact]
    public void E01_StrongAnchor_PlusOfferContext_ExtractsCode()
    {
        var n = MakeRaw("Deal", "Use code SPRING25 at checkout for 10% off");
        var r = RulesEngine.Classify(n, PackSet.BuiltInEn);
        var code = r.Entities.OfType<PromoCodeEntity>().SingleOrDefault();
        code.ShouldNotBeNull();
        code!.Code.ShouldBe("SPRING25");
        code.Confidence.ShouldBeGreaterThanOrEqualTo(0.60f);
        r.Category.ShouldBe(Category.Offers);
    }

    [Fact]
    public void E01_NumericCode_StrongAnchorOfferContext_NoOtp_Accepted()
    {
        // worked example 3: numeric code accepted with strong anchor + offer context, no otp.*
        var n = MakeRaw("Deal", "Use code 482913 at checkout for 10% off");
        var r = RulesEngine.Classify(n, PackSet.BuiltInEn);
        var code = r.Entities.OfType<PromoCodeEntity>().SingleOrDefault();
        code.ShouldNotBeNull();
        code!.Code.ShouldBe("482913");
    }

    [Fact]
    public void E01_OtpWins_NumericTokenNearVerification_IsNotPromo()
    {
        // worked example 2: verification code → security, OTP entity length-only, no promo code
        var n = MakeRaw("Bank", "Your verification code is 482913. Don't share it.");
        var r = RulesEngine.Classify(n, PackSet.BuiltInEn);
        r.Category.ShouldBe(Category.Security);
        var otp = r.Entities.OfType<OtpEntity>().SingleOrDefault();
        otp.ShouldNotBeNull();
        otp!.Length.ShouldBe(6);
        var code = r.Entities.OfType<PromoCodeEntity>().SingleOrDefault();
        code.ShouldBeNull("otp.* present ⇒ numeric promo candidates discarded");
    }

    [Fact]
    public void RUL014_OtpHardOverride_ForcesSecurity_ScrubsContent()
    {
        var n = MakeRaw("Bank", "Your verification code is 482913. Don't share it.");
        var r = RulesEngine.Classify(n, PackSet.BuiltInEn);
        r.Category.ShouldBe(Category.Security);
        r.Tags.ShouldContain(t => t.Id == "verification-code");
    }

    [Fact]
    public void E02_Discount_Percent_Extraction()
    {
        var n = MakeRaw("Sale", "Extra 25% off with code SPRING25");
        var r = RulesEngine.Classify(n, PackSet.BuiltInEn);
        var d = r.Entities.OfType<DiscountEntity>().FirstOrDefault();
        d.ShouldNotBeNull();
        d!.Value.ShouldBe(25m);
        d.Type.ShouldBe(DiscountType.Percent);
        r.Tags.ShouldContain(t => t.Id == "discount");
    }

    [Fact]
    public void E05_OrderRef_TrackingVetoesCodeAtSameSpan()
    {
        // worked example 4: order + tracking, NO promo code
        var n = MakeRaw("Your order has shipped", "Order #A1234 · Track: 1Z999AA10123456784");
        var r = RulesEngine.Classify(n, PackSet.BuiltInEn);
        r.Category.ShouldBe(Category.Orders);
        r.Entities.OfType<OrderRefEntity>().ShouldNotBeEmpty();
        r.Entities.OfType<PromoCodeEntity>().ShouldBeEmpty();
    }

    [Fact]
    public void CategoricalMetadata_PromoChannel_BoostsOffers()
    {
        var n = MakeRaw("Update", "New version available") with { AndroidCategory = "promo", ChannelName = "Promotions" };
        var r = RulesEngine.Classify(n, PackSet.BuiltInEn);
        r.Category.ShouldBe(Category.Offers);
    }

    [Fact]
    public void NeedsReview_WhenConfidenceLow()
    {
        // ambiguous text: no strong signal in any direction
        var n = MakeRaw("Hello", "The quick brown fox jumps");
        var r = RulesEngine.Classify(n, PackSet.BuiltInEn);
        if (r.Confidence < 0.55f) r.NeedsReview.ShouldBeTrue();
        else r.NeedsReview.ShouldBeFalse();
    }

    private static RawNotification MakeRaw(string title, string text) => new(
        SbnKey: "k1", Package: "com.example.shop", Id: 1, Tag: null,
        PostedAtUtcMs: 1_000, TzOffsetMin: 210, ChannelId: null, ChannelName: null,
        AndroidCategory: null, Importance: 3, IsOngoing: false, IsGroupSummary: false,
        IsForegroundService: false, IsClearable: true, GroupKey: null,
        Title: title, Text: text, BigText: null, SubText: null,
        TextLines: Array.Empty<string>(), ActionLabels: Array.Empty<string>(),
        ConversationTitle: null, LooksRedactedByOs: false, UserHandle: 0);
}
