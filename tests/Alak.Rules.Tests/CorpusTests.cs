using System.Text.Json;
using System.Text.Json.Serialization;
using Alak.Core.Capture;
using Alak.Core.Model;
using Alak.Rules;

namespace Alak.Rules.Tests;

/// <summary>
/// QLT-003 (EN subset, M3 scope): run every corpus case through the engine and
/// check the expected category/tags/entities. Grows toward 300 EN cases by M3 exit.
/// </summary>
public class CorpusTests
{
    private static readonly string s_root = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "corpus"));

    public static IEnumerable<object[]> EnCases() => Load("en");
    public static IEnumerable<object[]> FaCases() => Load("fa");
    public static IEnumerable<object[]> FaLatnCases() => Load("fa-latn");

    private static IEnumerable<object[]> Load(string lang)
    {
        var path = Path.Combine(s_root, lang, "cases.json");
        if (!File.Exists(path)) yield break;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var c in doc.RootElement.EnumerateArray())
            yield return new object[] { c.Deserialize<CorpusCase>()! };
    }

    [Theory]
    [MemberData(nameof(EnCases))]
    public void EN_CorpusCase(CorpusCase c)
    {
        var n = ToRaw(c);
        var r = RulesEngine.Classify(n, PackSet.Compile(new[] { EnPack() }), ToPrior(c));
        var expected = JsonSerializer.Deserialize<Expect>(c.Expect.GetRawText())!;
        r.Category.ShouldBe(Enum.Parse<Category>(expected.Category, ignoreCase: true),
            $"case {c.Id}");
        foreach (var tag in expected.Tags ?? new List<string>())
            r.Tags.ShouldContain(t => t.Id == tag, $"case {c.Id}: missing tag {tag}");
        foreach (var e in expected.Entities ?? new List<ExpectEntity>())
        {
            switch (e.Type)
            {
                case "promo_code":
                    var pc = r.Entities.OfType<PromoCodeEntity>().SingleOrDefault(x => x.Code == e.Code);
                    pc.ShouldNotBeNull($"case {c.Id}: promo code '{e.Code}' not extracted");
                    if (e.Discount?.Type == "percent")
                    {
                        pc!.Discount.ShouldNotBeNull($"case {c.Id}: discount not attached to code");
                        pc.Discount!.Type.ShouldBe(DiscountType.Percent);
                        pc.Discount.Value.ShouldBe(e.Discount.Value);
                    }
                    break;
                case "otp":
                    var o = r.Entities.OfType<OtpEntity>().FirstOrDefault();
                    o.ShouldNotBeNull($"case {c.Id}: otp not extracted");
                    if (e.Length is not null) o!.Length.ShouldBe(e.Length.Value, $"case {c.Id}: otp length");
                    break;
                case "order_ref":
                    r.Entities.OfType<OrderRefEntity>().ShouldNotBeEmpty($"case {c.Id}");
                    break;
                case "money":
                    var m = r.Entities.OfType<MoneyEntity>().FirstOrDefault();
                    m.ShouldNotBeNull($"case {c.Id}: money entity not extracted");
                    if (e.Value is not null) m!.Value.ShouldBe(e.Value.Value, $"case {c.Id}: money value");
                    if (e.Currency is not null) m.Currency.ShouldBe(e.Currency, $"case {c.Id}: currency");
                    if (e.Direction is not null) m.Direction.ToString().ToLowerInvariant().ShouldBe(e.Direction.ToLowerInvariant(), $"case {c.Id}: direction");
                    if (e.AccountHint is not null) m.AccountHint.ShouldBe(e.AccountHint, $"case {c.Id}: account_hint");
                    break;
                case "datetime":
                    r.Entities.OfType<DatetimeEntity>().ShouldNotBeEmpty($"case {c.Id}");
                    break;
                default:
                    throw new InvalidOperationException($"unknown entity type {e.Type} in case {c.Id}");
            }
        }
        if (expected.Entities is { Count: 0 } or null)
        { /* no entity assertion */ }
        else if (expected.NoPromoCode)
            r.Entities.OfType<PromoCodeEntity>().ShouldBeEmpty($"case {c.Id}: unexpected promo code");
    }

    [Theory]
    [MemberData(nameof(FaCases))]
    public void FA_CorpusCase(CorpusCase c)
    {
        // FA pack compiles alongside EN; the engine normalizes FA text (§8.2) so the
        // shared automaton matches. Category assertions use the same decision logic.
        var n = ToRaw(c);
        var packs = PackSet.Compile(new[] { FaPack() });
        var r = RulesEngine.Classify(n, packs, ToPrior(c));
        var expected = JsonSerializer.Deserialize<Expect>(c.Expect.GetRawText())!;
        r.Category.ShouldBe(Enum.Parse<Category>(expected.Category, ignoreCase: true),
            $"case {c.Id}: got {r.Category}, confidence {r.Confidence:F2}");
    }

    [Theory]
    [MemberData(nameof(FaLatnCases))]
    public void FA_LATN_CorpusCase(CorpusCase c)
    {
        // §8.9.3: fa-latn is an experimental extension pack (dependsOn: en) — it must
        // only activate on Finglish-only terms and never change EN scoring. Corpus
        // cases run with en + fa-latn compiled together (the shipped configuration).
        var n = ToRaw(c);
        var packs = PackSet.Compile(EnPlusFaLatnPack());
        var r = RulesEngine.Classify(n, packs, ToPrior(c));
        var expected = JsonSerializer.Deserialize<Expect>(c.Expect.GetRawText())!;
        r.Category.ShouldBe(Enum.Parse<Category>(expected.Category, ignoreCase: true),
            $"case {c.Id}: got {r.Category}, confidence {r.Confidence:F2}");
    }

    private static RulesEngine.AppPrior? ToPrior(CorpusCase c)
    {
        if (c.AppPrior is null or { Count: 0 }) return null;
        var counts = new int[9];
        var total = 0;
        foreach (var (k, v) in c.AppPrior)
        {
            if (Enum.TryParse<Category>(k, ignoreCase: true, out var cat) && cat != Category.Other)
            {
                counts[(int)cat] = v;
                total += v;
            }
        }
        return new RulesEngine.AppPrior(counts, total);
    }

    private static RawNotification ToRaw(CorpusCase c) => new(
        SbnKey: c.Id, Package: c.Package ?? "test.app", Id: 1, Tag: null,
        PostedAtUtcMs: 1_000_000, TzOffsetMin: c.TzOffsetMin ?? 210,
        ChannelId: null, ChannelName: c.ChannelName, AndroidCategory: c.AndroidCategory,
        Importance: 3, IsOngoing: false, IsGroupSummary: false, IsForegroundService: false,
        IsClearable: true, GroupKey: null,
        Title: c.Title, Text: c.Text, BigText: c.BigText, SubText: null,
        TextLines: c.TextLines ?? new List<string>(), ActionLabels: Array.Empty<string>(),
        ConversationTitle: null, LooksRedactedByOs: false, UserHandle: 0);

    internal static LanguagePack FaPack()
    {
        var lexiconPath = Path.Combine(s_root, "..", "..", "packs", "fa", "lexicon.json");
        return LoadPack(lexiconPath, "fa");
    }

    internal static LanguagePack FaLatnPack()
    {
        var lexiconPath = Path.Combine(s_root, "..", "..", "packs", "fa-latn", "lexicon.json");
        return LoadPack(lexiconPath, "fa-latn");
    }

    /// <summary>Extension-pack load order per §8.9.3: base pack (en) first, fa-latn after.</summary>
    internal static IReadOnlyList<LanguagePack> EnPlusFaLatnPack() => new[] { EnPack(), FaLatnPack() };

    private static LanguagePack EnPack()
    {
        var lexiconPath = Path.Combine(s_root, "..", "..", "packs", "en", "lexicon.json");
        return LoadPack(lexiconPath, "en");
    }

    internal static LanguagePack LoadPack(string lexiconPath, string id)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(lexiconPath));
        var dict = new Dictionary<string, IReadOnlyList<(string, float)>>();
        foreach (var concept in doc.RootElement.GetProperty("concepts").EnumerateObject())
        {
            var weight = concept.Value.TryGetProperty("weight", out var w) ? (float)w.GetDouble() : 0.9f;
            var terms = new List<(string, float)>();
            foreach (var t in concept.Value.GetProperty("terms").EnumerateArray())
            {
                if (t.ValueKind == JsonValueKind.String) terms.Add((t.GetString()!, weight));
                else terms.Add((t.GetProperty("t").GetString()!,
                    t.TryGetProperty("w", out var tw) ? (float)tw.GetDouble() : weight));
            }
            dict[concept.Name] = terms;
        }
        return new LanguagePack(id, dict);
    }

    // ---- corpus JSON model ----
    public sealed class CorpusCase
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("package")] public string? Package { get; set; }
        [JsonPropertyName("channel_name")] public string? ChannelName { get; set; }
        [JsonPropertyName("android_category")] public string? AndroidCategory { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("big_text")] public string? BigText { get; set; }
        [JsonPropertyName("tz_offset_min")] public int? TzOffsetMin { get; set; }
        [JsonPropertyName("text_lines")] public List<string>? TextLines { get; set; }
        [JsonPropertyName("app_prior")] public Dictionary<string, int>? AppPrior { get; set; }
        [JsonPropertyName("expect")] public JsonElement Expect { get; set; }
    }

    public sealed class Expect
    {
        [JsonPropertyName("category")] public string Category { get; set; } = "";
        [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
        [JsonPropertyName("entities")] public List<ExpectEntity>? Entities { get; set; }
        [JsonPropertyName("no_promo_code")] public bool NoPromoCode { get; set; }
    }

    public sealed class ExpectEntity
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("discount")] public ExpectDiscount? Discount { get; set; }
        [JsonPropertyName("value")] public decimal? Value { get; set; }
        [JsonPropertyName("kind")] public string? Kind { get; set; }
        [JsonPropertyName("currency")] public string? Currency { get; set; }
        [JsonPropertyName("direction")] public string? Direction { get; set; }
        [JsonPropertyName("account_hint")] public string? AccountHint { get; set; }
        [JsonPropertyName("length")] public int? Length { get; set; }
        [JsonPropertyName("role")] public string? Role { get; set; }
        [JsonPropertyName("time")] public string? Time { get; set; }
    }

    public sealed class ExpectDiscount
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("value")] public decimal Value { get; set; }
    }
}
