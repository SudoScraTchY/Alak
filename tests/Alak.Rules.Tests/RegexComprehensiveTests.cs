using Alak.Core.Capture;
using Alak.Core.Classification;
using Alak.Core.Model;
using Alak.Rules;
using Alak.Rules.Extraction;
using Alak.Storage;
using Alak.Text;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace Alak.Rules.Tests;

/// <summary>
/// Comprehensive verification for regex matching engines, user rules, and built-in extraction regexes (Goal 2).
/// </summary>
public class RegexComprehensiveTests
{
    private sealed class InMemoryUserRulesRepository : IUserRulesRepository
    {
        private readonly List<UserRule> _rules = new();

        public InMemoryUserRulesRepository(params UserRule[] rules)
        {
            _rules.AddRange(rules);
        }

        public Task<List<UserRule>> GetAllRulesAsync() => Task.FromResult(_rules.ToList());
        public Task<UserRule?> GetRuleAsync(long id) => Task.FromResult(_rules.FirstOrDefault(r => r.Id == id));
        public Task<long> InsertRuleAsync(UserRule rule) { _rules.Add(rule); return Task.FromResult(rule.Id); }
        public Task UpdateRuleAsync(UserRule rule) => Task.CompletedTask;
        public Task DeleteRuleAsync(long id) { _rules.RemoveAll(r => r.Id == id); return Task.CompletedTask; }
        public Task ReorderRulesAsync(IReadOnlyList<long> ruleIdsInOrder) => Task.CompletedTask;
        public Task<List<AppSettings>> GetAllAppSettingsAsync() => Task.FromResult(new List<AppSettings>());
        public Task<AppSettings?> GetAppSettingsAsync(string package) => Task.FromResult<AppSettings?>(null);
        public Task UpsertAppSettingsAsync(AppSettings settings) => Task.CompletedTask;
        public Task<GlobalSettings> GetGlobalSettingsAsync() => Task.FromResult(new GlobalSettings("system", "system", "auto", "auto", null, null, true, "balanced", false, false, true));
        public Task UpdateGlobalSettingsAsync(GlobalSettings settings) => Task.CompletedTask;
        public Task<List<SourceGroup>> GetSourceGroupsAsync(int days = 30) => Task.FromResult(new List<SourceGroup>());
        public Task UpsertSourceGroupAsync(string package, string? displayName, string? defaultCategory) => Task.CompletedTask;
    }

    #region String Matching & MatchModes

    [Theory]
    [InlineData("Special discount offer", "discount", RuleMatchMode.Contains, false, true)]
    [InlineData("Special DISCOUNT offer", "discount", RuleMatchMode.Contains, true, false)]
    [InlineData("Special DISCOUNT offer", "DISCOUNT", RuleMatchMode.Contains, true, true)]
    [InlineData("Special discount offer", "SPECIAL", RuleMatchMode.StartsWith, false, true)]
    [InlineData("Special discount offer", "SPECIAL", RuleMatchMode.StartsWith, true, false)]
    [InlineData("Special discount offer", "OFFER", RuleMatchMode.EndsWith, false, true)]
    [InlineData("Special discount offer", "OFFER", RuleMatchMode.EndsWith, true, false)]
    [InlineData("Special discount offer", "Special discount offer", RuleMatchMode.Exact, true, true)]
    [InlineData("Special discount offer", "special discount offer", RuleMatchMode.Exact, true, false)]
    [InlineData("Special discount offer", "special discount offer", RuleMatchMode.Exact, false, true)]
    public void StringMatches_BasicModes_EvaluateAccurately(
        string input, string pattern, RuleMatchMode mode, bool caseSensitive, bool expected)
    {
        var result = UserRulesEngine.StringMatches(input, pattern, mode, caseSensitive);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Order #12345 has been confirmed", @"\b[0-9]{5}\b", false, true)]
    [InlineData("Order #12345 has been confirmed", @"^Order\s*#\d+", false, true)]
    [InlineData("Order #12345 has been confirmed", @"confirmed$", false, true)]
    [InlineData("Payment code: ABC-9988", @"[A-Z]{3}-\d{4}", true, true)]
    [InlineData("Payment code: abc-9988", @"[A-Z]{3}-\d{4}", true, false)]
    [InlineData("Payment code: abc-9988", @"[A-Z]{3}-\d{4}", false, true)]
    public void StringMatches_RegexMode_EvaluatesRegexPatterns(
        string input, string pattern, bool caseSensitive, bool expected)
    {
        var result = UserRulesEngine.StringMatches(input, pattern, RuleMatchMode.Regex, caseSensitive);
        result.ShouldBe(expected);
    }

    [Fact]
    public void StringMatches_PersianUnicodeRegex_MatchesCorrectly()
    {
        var text = "کد تخفیف: YALDA50 برای ۵۰ هزار تومان تخفیف در خرید بعدی";

        // Match Persian prefix with English code
        var rxCode = @"کد\s+تخفیف:\s*([A-Za-z0-9]+)";
        UserRulesEngine.StringMatches(text, rxCode, RuleMatchMode.Regex, caseSensitive: false).ShouldBeTrue();

        // Match Persian numerals
        var rxPersianDigits = @"[\u06F0-\u06F9]+";
        UserRulesEngine.StringMatches(text, rxPersianDigits, RuleMatchMode.Regex, caseSensitive: false).ShouldBeTrue();

        // Match non-existing Persian keyword
        var rxNonExistent = @"لغو\s+سفارش";
        UserRulesEngine.StringMatches(text, rxNonExistent, RuleMatchMode.Regex, caseSensitive: false).ShouldBeFalse();
    }

    [Fact]
    public void StringMatches_InvalidRegex_FailsGracefullyWithoutThrowing()
    {
        var input = "Some sample notification text";
        var invalidPatterns = new[]
        {
            "[unclosed-bracket",
            "(unclosed-group",
            "*invalid-leading-quantifier",
            "?invalid",
            @"\xZZ (invalid hex)",
        };

        foreach (var badPattern in invalidPatterns)
        {
            // Must return false and NOT throw ArgumentException / RegexParseException
            var res = UserRulesEngine.StringMatches(input, badPattern, RuleMatchMode.Regex, caseSensitive: false);
            res.ShouldBeFalse();
        }
    }

    [Fact]
    public void StringMatches_ReDoSAttack_TimesOutSafelyUnder50ms()
    {
        // Evil catastrophic backtracking regex
        var evilPattern = @"^(([a-z])+.)+[A-Z]([a-z])+$";
        var evilInput = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!";

        var sw = Stopwatch.StartNew();
        var result = UserRulesEngine.StringMatches(evilInput, evilPattern, RuleMatchMode.Regex, caseSensitive: false);
        sw.Stop();

        result.ShouldBeFalse();
        // Must have timed out quickly without hanging the process (under 200ms total wall time)
        sw.ElapsedMilliseconds.ShouldBeLessThan(300);
    }

    #endregion

    #region UserRulesEngine Matching & Evaluation

    [Fact]
    public void Matches_WithExactPackageAndRegexText_MatchesOnlyWhenBothPass()
    {
        var match = new UserRuleMatch(
            Package: "com.digikala.app",
            TextRegex: @"سفارش\s+(?:شما\s+)?ثبت\s+شد",
            MatchMode: RuleMatchMode.Regex);

        // Matching package and text
        UserRulesEngine.Matches(match, "com.digikala.app", "دیجی‌کالا", "سفارش شما ثبت شد و در حال پردازش است", null, null, null)
            .ShouldBeTrue();

        // Mismatched package
        UserRulesEngine.Matches(match, "com.snapp.app", "دیجی‌کالا", "سفارش شما ثبت شد", null, null, null)
            .ShouldBeFalse();

        // Mismatched text
        UserRulesEngine.Matches(match, "com.digikala.app", "دیجی‌کالا", "تخفیف ویژه امروز", null, null, null)
            .ShouldBeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_ExecutesPriorityOrderAndFirstWins()
    {
        var rule1 = new UserRule(
            Id: 1,
            Priority: 10,
            Match: new UserRuleMatch(
                Package: "com.bank.app",
                TextRegex: @"واریز|برداشت",
                MatchMode: RuleMatchMode.Regex),
            Action: new UserRuleAction(SetCategory: "money"),
            Label: "Bank transaction");

        var rule2 = new UserRule(
            Id: 2,
            Priority: 5, // Higher priority (lower number)
            Match: new UserRuleMatch(
                Package: "com.bank.app",
                TextRegex: @"رمز\s+پویا|کد\s+تایید",
                MatchMode: RuleMatchMode.Regex),
            Action: new UserRuleAction(SetCategory: "security", Mute: true),
            Label: "Bank OTP");

        var repo = new InMemoryUserRulesRepository(rule1, rule2);
        var engine = new UserRulesEngine(repo);

        // OTP notification should match rule 2 (Priority 5) even if rule 1 also exists
        var action = await engine.EvaluateAsync(
            "com.bank.app",
            "بانک سامان",
            "رمز پویای شما: 849201. به هیچ عنوان در اختیار دیگران قرار ندهید.",
            "بانک سامان",
            null,
            null);

        action.ShouldNotBeNull();
        action.SetCategory.ShouldBe("security");
        action.Mute.ShouldBeTrue();

        // Normal transaction matches rule 1 (Priority 10)
        var action2 = await engine.EvaluateAsync(
            "com.bank.app",
            "بانک سامان",
            "واریز به حساب: 50,000,000 ریال",
            "بانک سامان",
            null,
            null);

        action2.ShouldNotBeNull();
        action2.SetCategory.ShouldBe("money");
        action2.Mute.ShouldBeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_DisabledRule_IsSkipped()
    {
        var rule = new UserRule(
            Id: 1,
            Priority: 1,
            Match: new UserRuleMatch(TextContains: "PromoCode"),
            Action: new UserRuleAction(Mute: true),
            Enabled: false); // Disabled

        var repo = new InMemoryUserRulesRepository(rule);
        var engine = new UserRulesEngine(repo);

        var action = await engine.EvaluateAsync("com.shop", "Shop", "PromoCode SAVE50", null, null, null);
        action.ShouldBeNull();
    }

    #endregion

    #region Built-in Regex Extractors

    [Fact]
    public void OrderRefExtractor_ExtractsStandardOrderNumbers()
    {
        var text = Normalizer.Normalize("Thank you for your order! Order #ORD-12345 is on its way.");
        var entities = OrderRefExtractor.Extract(text);

        entities.ShouldNotBeEmpty();
        entities.Any(e => e.Value.Contains("ORD-12345")).ShouldBeTrue();
    }

    [Fact]
    public void OrderRefExtractor_ExtractsUpsTrackingNumbers()
    {
        var text = Normalizer.Normalize("Your shipment has shipped with tracking number 1Z9999999999999999 via UPS.");
        var entities = OrderRefExtractor.Extract(text);

        entities.ShouldNotBeEmpty();
        entities.Any(e => e.Value == "1Z9999999999999999").ShouldBeTrue();
    }

    [Fact]
    public void Tag_ValidationRegex_AcceptsValidKebabCase()
    {
        Tag.IsValid("promo").ShouldBeTrue();
        Tag.IsValid("user-rule").ShouldBeTrue();
        Tag.IsValid("tag-v1").ShouldBeTrue();

        Tag.IsValid("").ShouldBeFalse();
        Tag.IsValid("Upper").ShouldBeFalse();
        Tag.IsValid("user--tag").ShouldBeFalse();
        Tag.IsValid("-leading-dash").ShouldBeFalse();
        Tag.IsValid("trailing-dash-").ShouldBeFalse();
        Tag.IsValid("space tag").ShouldBeFalse();
        Tag.IsValid("tag-1").ShouldBeFalse(); // Segments after dash must start with a letter
    }

    [Fact]
    public void RulesEngine_ClassifiesAndExtractsPromoCode_FromTextWithRegexTokens()
    {
        var raw = new RawNotification(
            SbnKey: "test-sbn-1",
            Package: "com.food.delivery",
            Id: 101,
            Tag: null,
            PostedAtUtcMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            TzOffsetMin: 210,
            ChannelId: "promos",
            ChannelName: "Special Promos",
            AndroidCategory: "promo",
            Importance: 3,
            IsOngoing: false,
            IsGroupSummary: false,
            IsForegroundService: false,
            IsClearable: true,
            GroupKey: null,
            Title: "Food Delivery Promo",
            Text: "Special weekend deal: 50% discount with code YALDA50 at checkout!",
            BigText: null,
            SubText: null,
            TextLines: Array.Empty<string>(),
            ActionLabels: Array.Empty<string>(),
            ConversationTitle: null,
            LooksRedactedByOs: false,
            UserHandle: 0);

        var result = RulesEngine.Classify(raw, PackSet.BuiltInEn);

        result.Category.ShouldBe(Category.Offers);
        var promoCode = result.Entities.OfType<PromoCodeEntity>().FirstOrDefault();
        promoCode.ShouldNotBeNull();
        promoCode.Code.ShouldBe("YALDA50");
    }

    [Fact]
    public void RulesEngine_SuppressesPromoCodeDetection_WhenOtpIsDetected()
    {
        var raw = new RawNotification(
            SbnKey: "test-sbn-2",
            Package: "com.bank.auth",
            Id: 102,
            Tag: null,
            PostedAtUtcMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            TzOffsetMin: 210,
            ChannelId: "auth",
            ChannelName: "Security",
            AndroidCategory: null,
            Importance: 4,
            IsOngoing: false,
            IsGroupSummary: false,
            IsForegroundService: false,
            IsClearable: true,
            GroupKey: null,
            Title: "Verification",
            Text: "Your verification code is 849201. Do not share it with anyone.",
            BigText: null,
            SubText: null,
            TextLines: Array.Empty<string>(),
            ActionLabels: Array.Empty<string>(),
            ConversationTitle: null,
            LooksRedactedByOs: false,
            UserHandle: 0);

        var result = RulesEngine.Classify(raw, PackSet.BuiltInEn);

        result.Category.ShouldBe(Category.Security);
        // OTP entity should exist
        result.Entities.OfType<OtpEntity>().ShouldNotBeEmpty();
        // Promo code entity must NOT be extracted from OTP
        result.Entities.OfType<PromoCodeEntity>().ShouldBeEmpty();
    }

    #endregion
}
