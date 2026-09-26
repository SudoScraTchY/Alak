using System.Text.Json;
using Alak.Core.Capture;
using Alak.Core.Model;
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
foreach (var t in new[] { "Address saved", "Your zip code 90210 was saved to your profile" })
{
    var norm = Normalizer.Normalize(t);
    Console.WriteLine($"[{t}] → [{norm.Folded}]");
    foreach (var h in en.Matcher.Scan(norm.Folded))
        Console.WriteLine($"    hit: {h.ConceptId} '{h.Term}' span={h.Span} w={h.Weight}");
}
