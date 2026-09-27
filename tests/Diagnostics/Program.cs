using System.Text.Json;
using Alak.Core.Capture;
using Alak.Rules;
using Alak.Text;

PackSet LoadPack(string path, string id)
{
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    var concepts = new Dictionary<string, IReadOnlyList<(string Term, float Weight)>>();
    foreach (var concept in doc.RootElement.GetProperty("concepts").EnumerateObject())
    {
        var weight = concept.Value.TryGetProperty("weight", out var w) ? (float)w.GetDouble() : 0.9f;
        var terms = new List<(string, float)>();
        foreach (var t in concept.Value.GetProperty("terms").EnumerateArray())
            terms.Add((t.GetString()!, weight));
        concepts[concept.Name] = terms;
    }
    return PackSet.Compile(new[] { new LanguagePack(id, concepts) });
}

var en = LoadPack("packs/en/lexicon.json", "en");
var title = "💥LAST CHANCE💥";
var text = "F R E E   S H I P P I N G on everything! no code needed. sale ends soon!";
var n = Normalizer.Normalize(text);
Console.WriteLine($"folded=[{n.Folded}]");
foreach (var h in en.Matcher.Scan(n.Folded))
    Console.WriteLine($"  hit {h.ConceptId} '{h.Term}' w={h.Weight}");
var raw = new RawNotification("k", "test.shop", 1, null, 1700000000000, 210, null,
    "Promotions", null, 3, false, false, false, true, null, title, text, null, null,
    Array.Empty<string>(), Array.Empty<string>(), null, false, 0);
var r = RulesEngine.Classify(raw, en);
Console.WriteLine($"→ {r.Category} conf={r.Confidence:F2} tags=[{string.Join(',', r.Tags.Select(t => t.Id))}]");
foreach (var e in r.Entities) Console.WriteLine($"  entity {e.GetType().Name}: {e}");
