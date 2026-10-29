using System;
using Alak.Inference.Abstractions.Tasks;
using Shouldly;
using Xunit;

namespace Alak.Inference.Abstractions.Tests;

public class GroundingGuardTests
{
    [Fact]
    public void SLM003_VerbatimFacts_ArePreserved()
    {
        var title = "کد تخفیف اسنپ فود";
        var text = "سفارش شما با کد تخفیف DELICIOUS شامل ۵۰ هزار تومان تخفیف شد. مهلت تا ساعت ۲۴.";

        var rawResult = new T301ExplainResult(
            Summary: "تخفیف ۵۰ هزار تومانی اسنپ فود",
            Importance: "normal",
            Action: "استفاده از تخفیف",
            CategorySuggestion: "offers",
            Tags: new[] { "تخفیف" },
            Facts: new[]
            {
                new T3Fact("کد تخفیف", "DELICIOUS"),
                new T3Fact("مبلغ", "۵۰ هزار تومان"),
                new T3Fact("مهلت", "ساعت ۲۴")
            }
        );

        var guarded = GroundingGuard.Apply(rawResult, title, text);

        guarded.Facts.Length.ShouldBe(3);
        guarded.Facts[0].Value.ShouldBe("DELICIOUS");
        guarded.Facts[1].Value.ShouldBe("۵۰ هزار تومان");
        guarded.Facts[2].Value.ShouldBe("ساعت ۲۴");
        guarded.Action.ShouldBe("استفاده از تخفیف");
    }

    [Fact]
    public void SLM003_HallucinatedPromoCode_IsDropped()
    {
        var title = "سفارش ثبت شد";
        var text = "سفارش غذای شما ثبت گردید و پیک در مسیر است.";

        // Model hallucinates a promo code "SAVE90" and false action
        var rawResult = new T301ExplainResult(
            Summary: "سفارش ثبت شد",
            Importance: "normal",
            Action: "از کد تخفیف SAVE90 در سفارش بعدی استفاده کنید",
            CategorySuggestion: "orders",
            Tags: new[] { "orders" },
            Facts: new[]
            {
                new T3Fact("وضعیت", "ثبت گردید"),
                new T3Fact("کد تخفیف", "SAVE90"), // Hallucination!
                new T3Fact("تخفیف", "50% off")   // Hallucination!
            }
        );

        var guarded = GroundingGuard.Apply(rawResult, title, text);

        // SAVE90 and 50% must be dropped
        guarded.Facts.Length.ShouldBe(1);
        guarded.Facts[0].Label.ShouldBe("وضعیت");
        guarded.Facts[0].Value.ShouldBe("ثبت گردید");

        // Action had hallucinated code SAVE90, so it should be null
        guarded.Action.ShouldBeNull();
    }

    [Fact]
    public void SLM003_PersianDigits_MatchLatinDigits()
    {
        var title = "بانک ملت";
        var text = "واریز ۵۰۰۰۰ ریال به حساب شما";

        var rawResult = new T301ExplainResult(
            Summary: "واریز مبلغ",
            Importance: "high",
            Action: null,
            CategorySuggestion: "money",
            Tags: new[] { "money" },
            Facts: new[]
            {
                new T3Fact("مبلغ", "50000 ریال") // Latin 50000 vs Persian ۵۰۰۰۰ in text
            }
        );

        var guarded = GroundingGuard.Apply(rawResult, title, text);

        guarded.Facts.Length.ShouldBe(1);
        guarded.Facts[0].Value.ShouldBe("50000 ریال");
    }

    [Fact]
    public void SLM003_CodeDetails_HallucinatedNumbers_SetToNull()
    {
        var title = "دیجی کالا";
        var text = "با خرید بالای ۱۰۰ هزار تومان ارسال رایگان دریافت کنید.";

        // Model hallucinates cap of 500000 and valid_until 2026-12-31
        var rawDetails = new T304CodeDetailsResult(
            MinSpend: 100000,
            Cap: 500000, // Absent in text!
            ValidUntil: "2026-12-31", // Absent in text!
            Conditions: new[] { "ارسال رایگان", "خرید حضوری فقط" } // "خرید حضوری فقط" absent
        );

        var guarded = GroundingGuard.Apply(rawDetails, title, text);

        guarded.MinSpend.ShouldBe(100000);
        guarded.Cap.ShouldBeNull();
        guarded.ValidUntil.ShouldBeNull();
        guarded.Conditions.Length.ShouldBe(1);
        guarded.Conditions[0].ShouldBe("ارسال رایگان");
    }

    [Fact]
    public void SLM003_EnglishProseAction_IsNotFalsePositive()
    {
        var title = "Digikala Delivery";
        var text = "Your order #12345 has been delivered by courier.";

        var rawResult = new T301ExplainResult(
            Summary: "Order 12345 delivered by courier.",
            Importance: "high",
            Action: "Check your order delivery status in app",
            CategorySuggestion: "orders",
            Tags: new[] { "orders" },
            Facts: new[]
            {
                new T3Fact("Order Number", "12345"),
                new T3Fact("Status", "Delivered by courier")
            }
        );

        var guarded = GroundingGuard.Apply(rawResult, title, text);

        // Prose action should NOT be dropped simply because English words are 4+ characters
        guarded.Action.ShouldBe("Check your order delivery status in app");
        guarded.Facts.Length.ShouldBe(2);
        guarded.Facts[0].Value.ShouldBe("12345");
        guarded.Facts[1].Value.ShouldBe("Delivered by courier");
    }

    [Fact]
    public void SLM003_HallucinatedUrl_IsDropped()
    {
        var title = "Bank Alert";
        var text = "Suspicious login detected on your account.";

        var rawResult = new T301ExplainResult(
            Summary: "Suspicious login detected",
            Importance: "high",
            Action: "Verify your account at https://phishing-bank-security.com",
            CategorySuggestion: "security",
            Tags: new[] { "security" },
            Facts: new[]
            {
                new T3Fact("Security Link", "https://phishing-bank-security.com")
            }
        );

        var guarded = GroundingGuard.Apply(rawResult, title, text);

        guarded.Action.ShouldBeNull();
        guarded.Facts.Length.ShouldBe(0);
    }

    [Fact]
    public void SLM003_FormattedNumbersWithThousandsSeparators_Match()
    {
        var title = "اسنپ فود";
        var text = "سفارش شما با تخفیف ۵۰۰۰۰ تومان ثبت شد.";

        // Candidate has comma thousands separator (۵۰,۰۰۰), source has plain ۵۰۰۰۰
        var rawResult = new T301ExplainResult(
            Summary: "سفارش ثبت شد",
            Importance: "normal",
            Action: null,
            CategorySuggestion: "food",
            Tags: new[] { "food" },
            Facts: new[]
            {
                new T3Fact("مبلغ تخفیف", "۵۰,۰۰۰ تومان"),
                new T3Fact("مبلغ لاتین", "50,000 تومان")
            }
        );

        var guarded = GroundingGuard.Apply(rawResult, title, text);

        guarded.Facts.Length.ShouldBe(2);
        guarded.Facts[0].Value.ShouldBe("۵۰,۰۰۰ تومان");
        guarded.Facts[1].Value.ShouldBe("50,000 تومان");
    }
}

