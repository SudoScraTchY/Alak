using System.Diagnostics;
using Alak.Core.Capture;
using Alak.Core.Model;
using Alak.Rules;
using Alak.Text;

namespace Alak.Rules.Tests;

// RUL-002: ≤20ms p95 / ≤40ms p99 per notification (≤1.2KB text) — desktop proxy.
// RUL-003: one automaton over all enabled packs.
public class PerformanceTests
{
    private static RawNotification Make(string title, string text) => new(
        SbnKey: "k", Package: "com.example.shop", Id: 1, Tag: null,
        PostedAtUtcMs: 1_000, TzOffsetMin: 210, ChannelId: null, ChannelName: "Promotions",
        AndroidCategory: null, Importance: 3, IsOngoing: false, IsGroupSummary: false,
        IsForegroundService: false, IsClearable: true, GroupKey: null,
        Title: title, Text: text, BigText: null, SubText: null,
        TextLines: Array.Empty<string>(), ActionLabels: Array.Empty<string>(),
        ConversationTitle: null, LooksRedactedByOs: false, UserHandle: 0);

    [Fact]
    public void RUL002_Budget_P95Under20Ms_P99Under40Ms()
    {
        var packs = PackSet.BuiltInEn;
        // 1.2 KB text block with realistic promo content
        var text = string.Join(" ",
            "Spring Sale Extra 25% off with code SPRING25 at checkout ends tonight.",
            "Free shipping on orders over 50 dollars.",
            "Buy one get one on selected items.",
            string.Concat(Enumerable.Repeat("Terms and conditions apply. See website for details. ", 20)));

        var samples = new List<double>();
        // warmup (JIT + automaton caches)
        for (var i = 0; i < 50; i++)
            RulesEngine.Classify(Make("Warmup", text), packs);

        var sw = Stopwatch.StartNew();
        const int N = 500;
        for (var i = 0; i < N; i++)
        {
            sw.Restart();
            RulesEngine.Classify(Make($"Spring Sale {i}", text), packs);
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        var p95 = samples[(int)(N * 0.95)];
        var p99 = samples[(int)(N * 0.99)];
        // Desktop CPU is faster than R1; spec budget on R1 gets headroom here.
        // We assert the desktop p95 < 20ms which is the spec budget itself.
        Assert.True(p95 < 20.0, $"p95 was {p95:F2}ms (budget 20ms); p99 was {p99:F2}ms (budget 40ms)");
        Assert.True(p99 < 40.0);
    }

    [Fact]
    public void RUL003_OneAutomaton_MultiPack_TermsCounted()
    {
        var en = new LanguagePack("en", new Dictionary<string, IReadOnlyList<(string, float)>>
        {
            ["promo.code.trigger"] = new[] { ("use code", 1f) },
            ["promo.discount.word"] = new[] { ("sale", 0.8f) },
        });
        var fa = new LanguagePack("fa", new Dictionary<string, IReadOnlyList<(string, float)>>
        {
            ["promo.code.trigger"] = new[] { ("کد~تخفیف", 1f) },
            ["promo.discount.word"] = new[] { ("حراج", 0.9f) },
        });
        var set = PackSet.Compile(new[] { en, fa });
        set.Matcher.TermCount.ShouldBe(4);

        // folded FA text matches the FA pack term; EN matches EN term — one automaton serves both
        var faHits = set.Matcher.Scan(Normalizer.Normalize("با کد تخفیف ۲۰٪ حراج").Folded).ToList();
        faHits.ShouldContain(h => h.ConceptId == "promo.code.trigger");
        faHits.ShouldContain(h => h.ConceptId == "promo.discount.word");

        var enHits = set.Matcher.Scan(Normalizer.Normalize("use code SPRING25 sale").Folded).ToList();
        enHits.ShouldContain(h => h.ConceptId == "promo.code.trigger");
    }

    [Fact]
    public void RUL003_OptionalSeparator_MatchesGluedAndSpaced()
    {
        // NRM-003: ZWNJ→space, so 'کد‌تخفیف' (ZWNJ) and 'کد تخفیف' (space) and 'کدتخفیف' (glued)
        // must all hit the single term 'کد~تخفیف'.
        var fa = new LanguagePack("fa", new Dictionary<string, IReadOnlyList<(string, float)>>
        {
            ["promo.code.trigger"] = new[] { ("کد~تخفیف", 1f) },
        });
        var set = PackSet.Compile(new[] { fa });

        foreach (var variant in new[] { "کد تخفیف", "کد‌تخفیف", "کدتخفیف" })
        {
            var n = Normalizer.Normalize(variant);
            var hits = set.Matcher.Scan(n.Folded).ToList();
            hits.ShouldNotBeEmpty($"'{variant}' (folded: '{n.Folded}') must match کد~تخفیف");
        }
    }

    [Fact]
    public void ContainsAlphaNumericSimd_MultiChunkAndBoundaries_EvaluatesCorrectly()
    {
        // 1. Empty and short spans (< 8 chars)
        ConceptMatcher.ContainsAlphaNumericSimd("").ShouldBeFalse();
        ConceptMatcher.ContainsAlphaNumericSimd("   ").ShouldBeFalse();
        ConceptMatcher.ContainsAlphaNumericSimd("---").ShouldBeFalse();
        ConceptMatcher.ContainsAlphaNumericSimd("a").ShouldBeTrue();
        ConceptMatcher.ContainsAlphaNumericSimd("--9").ShouldBeTrue();

        // 2. Exact 8-char span
        ConceptMatcher.ContainsAlphaNumericSimd("        ").ShouldBeFalse();
        ConceptMatcher.ContainsAlphaNumericSimd("-------Z").ShouldBeTrue();
        ConceptMatcher.ContainsAlphaNumericSimd("A-------").ShouldBeTrue();

        // 3. Multi-chunk span (> 8 chars): alphanumeric in subsequent chunk (index >= 8)
        // This validates that the SIMD loop does not early-return false on first non-matching chunk
        ConceptMatcher.ContainsAlphaNumericSimd("        --------x").ShouldBeTrue();
        ConceptMatcher.ContainsAlphaNumericSimd("        --------9").ShouldBeTrue();
        ConceptMatcher.ContainsAlphaNumericSimd("-----------------").ShouldBeFalse();

        // 4. Non-ASCII Unicode letters and digits across chunks
        ConceptMatcher.ContainsAlphaNumericSimd("        --------س").ShouldBeTrue();
        ConceptMatcher.ContainsAlphaNumericSimd("        --------۵").ShouldBeTrue();
        ConceptMatcher.ContainsAlphaNumericSimd("کد تخفیف ویژه").ShouldBeTrue();
    }
}

