using System.Text.Json;
using System.Text.Json.Serialization;
using Alak.Core.Capture;
using Alak.Core.Model;
using Alak.Rules;

// QLT-003 metric harness: run a corpus through Tier 1, report per-category
// accuracy, macro-F1, promo-code precision/recall, OTP recall, and list misses.
// Usage: dotnet run -c Release -- <corpus.json> <lexicon.json> [lang]

var corpusPath = args.Length > 0 ? args[0] : Path.Combine("..", "..", "tests", "corpus", "en", "cases.json");
var lexPath = args.Length > 1 ? args[1] : Path.Combine("..", "..", "packs", "en", "lexicon.json");
var lang = args.Length > 2 ? args[2] : "en";

// load pack
using var doc = JsonDocument.Parse(File.ReadAllText(lexPath));
var concepts = new Dictionary<string, IReadOnlyList<(string Term, float Weight)>>();
foreach (var concept in doc.RootElement.GetProperty("concepts").EnumerateObject())
{
    var weight = concept.Value.TryGetProperty("weight", out var w) ? (float)w.GetDouble() : 0.9f;
    var terms = new List<(string, float)>();
    foreach (var t in concept.Value.GetProperty("terms").EnumerateArray())
    {
        if (t.ValueKind == JsonValueKind.String) terms.Add((t.GetString()!, weight));
        else if (t.ValueKind == JsonValueKind.Array) terms.Add((t[0].GetString()!, (float)t[1].GetDouble()));
    }
    concepts[concept.Name] = terms;
}
var packs = PackSet.Compile(new[] { new LanguagePack(lang, concepts) });

// load corpus
var cases = JsonDocument.Parse(File.ReadAllText(corpusPath)).RootElement.EnumerateArray().ToList();

var cats = Enum.GetValues<Category>().Cast<Category>().ToList();
var tp = new Dictionary<Category, int>(); var fp = new Dictionary<Category, int>(); var fn = new Dictionary<Category, int>();
foreach (var c in cats) { tp[c] = 0; fp[c] = 0; fn[c] = 0; }

// promo-code + otp metrics
int promoTp = 0, promoFp = 0, promoFn = 0, otpTp = 0, otpFn = 0;

foreach (var el in cases)
{
    var c = el;
    var id = c.GetProperty("id").GetString()!;
    var title = c.TryGetProperty("title", out var t) ? t.GetString() : null;
    var text = c.TryGetProperty("text", out var x) ? x.GetString() : null;
    var channel = c.TryGetProperty("channel_name", out var ch) && ch.ValueKind != JsonValueKind.Null ? ch.GetString() : null;
    var expect = JsonSerializer.Deserialize<Expect>(c.GetProperty("expect").GetRawText())!;
    var expectedCat = Enum.Parse<Category>(expect.Category, ignoreCase: true);

    var n = new RawNotification(SbnKey: id, Package: "eval", Id: 1, Tag: null,
        PostedAtUtcMs: 1_700_000_000_000, TzOffsetMin: 210, ChannelId: null,
        ChannelName: channel, AndroidCategory: null, Importance: 3, IsOngoing: false,
        IsGroupSummary: false, IsForegroundService: false, IsClearable: true, GroupKey: null,
        Title: title, Text: text, BigText: null, SubText: null,
        TextLines: Array.Empty<string>(), ActionLabels: Array.Empty<string>(),
        ConversationTitle: null, LooksRedactedByOs: false, UserHandle: 0);

    RulesEngine.AppPrior? prior = null;
    if (c.TryGetProperty("app_prior", out var ap) && ap.ValueKind == JsonValueKind.Object)
    {
        var counts = new int[9]; var total = 0;
        foreach (var kv in ap.EnumerateObject())
            if (Enum.TryParse<Category>(kv.Name, ignoreCase: true, out var cat2) && cat2 != Category.Other)
            { counts[(int)cat2] = kv.Value.GetInt32(); total += kv.Value.GetInt32(); }
        prior = new RulesEngine.AppPrior(counts, total);
    }

    var r = RulesEngine.Classify(n, packs, prior);
    if (r.Category == expectedCat) tp[expectedCat]++;
    else { fp[r.Category]++; fn[expectedCat]++; Console.WriteLine($"MISS {id}: expected {expectedCat} got {r.Category} ({r.Confidence:F2}) :: {title} / {text}"); }

    // promo-code metric: expected promo_code entity vs extracted
    var expPromo = (expect.Entities ?? []).Any(e => e.Type == "promo_code");
    var gotPromo = r.Entities.OfType<PromoCodeEntity>().Any();
    if (expPromo && gotPromo) promoTp++;
    else if (!expPromo && gotPromo) { promoFp++; Console.WriteLine($"PROMO-FP {id}: {title} / {text}"); }
    else if (expPromo && !gotPromo) { promoFn++; Console.WriteLine($"PROMO-FN {id}: {title} / {text}"); }

    var expOtp = (expect.Entities ?? []).Any(e => e.Type == "otp");
    var gotOtp = r.Entities.OfType<OtpEntity>().Any();
    if (expOtp && gotOtp) otpTp++;
    else if (expOtp && !gotOtp) { otpFn++; Console.WriteLine($"OTP-FN {id}: {title} / {text}"); }
}

Console.WriteLine();
Console.WriteLine("== per-category ==");
var f1s = new List<float>();
foreach (var c in cats.Where(c => c != Category.Other))
{
    var prec = tp[c] + fp[c] > 0 ? (float)tp[c] / (tp[c] + fp[c]) : 0;
    var rec = tp[c] + fn[c] > 0 ? (float)tp[c] / (tp[c] + fn[c]) : 0;
    var f1 = prec + rec > 0 ? 2 * prec * rec / (prec + rec) : 0;
    f1s.Add(f1);
    Console.WriteLine($"  {c,-10} tp={tp[c],3} fp={fp[c],3} fn={fn[c],3} P={prec:F2} R={rec:F2} F1={f1:F2}");
}
var macroF1 = f1s.Average();
Console.WriteLine($"macro-F1 (8 cats) = {macroF1:F3}");
var promoP = promoTp + promoFp > 0 ? (float)promoTp / (promoTp + promoFp) : 1;
var promoR = promoTp + promoFn > 0 ? (float)promoTp / (promoTp + promoFn) : 1;
var otpR = otpTp + otpFn > 0 ? (float)otpTp / (otpTp + otpFn) : 1;
Console.WriteLine($"promo-code P={promoP:F3} R={promoR:F3}  (tp={promoTp} fp={promoFp} fn={promoFn})");
Console.WriteLine($"otp recall={otpR:F3}  (tp={otpTp} fn={otpFn})");
Console.WriteLine($"QLT-003 gates: macro-F1≥0.92: {(macroF1 >= 0.92f ? "PASS" : "FAIL")}  promo P≥0.98: {(promoP >= 0.98f ? "PASS" : "FAIL")}  promo R≥0.92: {(promoR >= 0.92f ? "PASS" : "FAIL")}  otp R≥0.995: {(otpR >= 0.995f ? "PASS" : "FAIL")}");

public record Expect(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("tags")] List<string>? Tags,
    [property: JsonPropertyName("entities")] List<ExpectEntity>? Entities,
    [property: JsonPropertyName("not_entities")] List<string>? NotEntities);
public record ExpectEntity(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("discount")] DiscountExpect? Discount,
    [property: JsonPropertyName("length")] int? Length,
    [property: JsonPropertyName("value")] decimal? Value,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("direction")] string? Direction,
    [property: JsonPropertyName("account_hint")] string? AccountHint,
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("time")] string? Time);
public record DiscountExpect(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("value")] decimal Value);
