using Alak.Core.Capture;
using Alak.Core.Classification;
using Alak.Core.Model;
using Alak.Rules;
using Alak.Rules.Extraction;
using Alak.Text;

namespace Alak.Rules.Tests;

/// <summary>
/// §8.9.3 activation rule: the experimental fa-latn (Finglish) extension pack
/// activates ONLY when a Finglish-only term hits and must never reduce English
/// precision (pure-EN text scores identically with vs without the pack loaded).
/// </summary>
public class FaLatnActivationTests
{
    private static RawNotification MakeRaw(string title, string text, string? channel = null) => new(
        SbnKey: "k1", Package: "com.example.shop", Id: 1, Tag: null,
        PostedAtUtcMs: 1_000, TzOffsetMin: 210, ChannelId: null, ChannelName: channel,
        AndroidCategory: null, Importance: 3, IsOngoing: false, IsGroupSummary: false,
        IsForegroundService: false, IsClearable: true, GroupKey: null,
        Title: title, Text: text, BigText: null, SubText: null,
        TextLines: Array.Empty<string>(), ActionLabels: Array.Empty<string>(),
        ConversationTitle: null, LooksRedactedByOs: false, UserHandle: 0);

    private static LanguagePack LoadPack(string lexiconDir, string id)
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", lexiconDir));
        using var doc = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, "lexicon.json")));
        var dict = new Dictionary<string, IReadOnlyList<(string, float)>>();
        foreach (var concept in doc.RootElement.GetProperty("concepts").EnumerateObject())
        {
            var weight = concept.Value.TryGetProperty("weight", out var w) ? (float)w.GetDouble() : 0.9f;
            var terms = new List<(string, float)>();
            foreach (var t in concept.Value.GetProperty("terms").EnumerateArray())
            {
                if (t.ValueKind == System.Text.Json.JsonValueKind.String)
                    terms.Add((t.GetString()!, weight));
                else if (t.ValueKind == System.Text.Json.JsonValueKind.Array)
                    terms.Add((t[0].GetString()!, (float)t[1].GetDouble()));
            }
            dict[concept.Name] = terms;
        }
        return new LanguagePack(id, dict);
    }

    private static LanguagePack En() => LoadPack("packs/en", "en");
    private static LanguagePack FaLatn() => LoadPack("packs/fa-latn", "fa-latn");

    private static PackSet EnOnly() => PackSet.Compile(new[] { En() });
    private static PackSet EnPlusLatn() => PackSet.Compile(new[] { En(), FaLatn() });

    [Fact]
    public void FinglishOffer_ExtractsPromoCodeAndClassifiesOffers()
    {
        // §8.9.3 worked example: 'kod takhfif TAKHFIF25 ta payan emshab'
        var n = MakeRaw("foroosh vizhe", "kod takhfif TAKHFIF25 ta payan emshab");
        var r = RulesEngine.Classify(n, EnPlusLatn());
        r.Category.ShouldBe(Category.Offers,
            $"got {r.Category} conf={r.Confidence:F2}; evidence: {string.Join(", ", r.Evidence.Select(e => e.RuleId))}");
        var code = r.Entities.OfType<PromoCodeEntity>().SingleOrDefault();
        code.ShouldNotBeNull("Finglish promo code TAKHFIF25 must be extracted");
        code!.Code.ShouldBe("TAKHFIF25");
    }

    [Fact]
    public void FinglishMoney_ClassifiesMoney()
    {
        var n = MakeRaw("bank", "variz 500000 toman be kart dide shod. mojoodi kafi dard");
        var r = RulesEngine.Classify(n, EnPlusLatn());
        r.Category.ShouldBe(Category.Money);
    }

    [Fact]
    public void FinglishOrder_ClassifiesOrders()
    {
        var n = MakeRaw("sefaresh", "sefaresh shoma amadeh ersal ast");
        var r = RulesEngine.Classify(n, EnPlusLatn());
        r.Category.ShouldBe(Category.Orders);
    }

    // ---- §8.9.3 gate: EN precision must not change ----

    [Theory]
    [InlineData("Spring Sale", "25% off with code SPRING25")]
    [InlineData("Your login", "Your verification code is 555123. Do not share it.")]
    [InlineData("Package update", "System update available. Tap to install.")]
    [InlineData("Message", "Hey, are we still on for tonight?")]
    public void PureEnglish_ScoresIdentical_WithOrWithoutFaLatn(string title, string text)
    {
        var n = MakeRaw(title, text);
        var without = RulesEngine.Classify(n, EnOnly());
        var with = RulesEngine.Classify(n, EnPlusLatn());
        with.Category.ShouldBe(without.Category,
            $"EN drift: {without.Category} -> {with.Category} on '{text}'");
        with.Confidence.ShouldBe(without.Confidence, 0.0001f);
        with.Tags.Count.ShouldBe(without.Tags.Count);
        with.Entities.Count.ShouldBe(without.Entities.Count,
            $"entity drift on '{text}': {without.Entities.Count} -> {with.Entities.Count}");
    }

    [Fact]
    public void FinglishOnlyTerm_IsNotInEnPack()
    {
        // Structural proof of the gate: every fa-latn term is Finglish-only — none
        // appears in the en pack lexicon, so no EN hit can ever come from fa-latn.
        var latn = FaLatn();
        var en = En();
        var enTerms = en.Concepts.Values.SelectMany(v => v).Select(t => t.Term).ToHashSet();
        var overlapping = latn.Concepts.Values.SelectMany(v => v)
            .Where(t => enTerms.Contains(t.Term))
            .Select(t => t.Term).ToList();
        overlapping.ShouldBeEmpty($"fa-latn terms overlapping en: {string.Join(", ", overlapping)}");
    }
}
