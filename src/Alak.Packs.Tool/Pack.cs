using System.Text.Json;

namespace Alak.Packs.Tool;

/// <summary>Pack file model + loader (PCK-001, schemaVersion 1).</summary>
public sealed class Pack
{
    public required string Root { get; init; }
    public required PackManifest Manifest { get; init; }
    public required Dictionary<string, Concept> Concepts { get; init; }

    public static Pack Load(string root)
    {
        var manifest = JsonSerializer.Deserialize<PackManifest>(
            File.ReadAllText(Path.Combine(root, "pack.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip })
            ?? throw new InvalidOperationException($"pack.json in {root} is empty");

        var lexiconPath = Path.Combine(root, "lexicon.json");
        if (!File.Exists(lexiconPath)) throw new InvalidOperationException("lexicon.json missing");
        var lexicon = JsonSerializer.Deserialize<Lexicon>(
            File.ReadAllText(lexiconPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("lexicon.json is empty");

        return new Pack { Root = root, Manifest = manifest, Concepts = lexicon.Concepts };
    }
}

public sealed class PackManifest
{
    public int SchemaVersion { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string NativeName { get; set; } = "";
    public string Version { get; set; } = "";
    public string? MinAppVersion { get; set; }
    public List<string> Scripts { get; set; } = new();
    public string Direction { get; set; } = "ltr";
    public List<string> Calendars { get; set; } = new();
    public List<string> Weekend { get; set; } = new();
    public Dictionary<string, List<string>>? SuffixSets { get; set; }
    public List<string> DependsOn { get; set; } = new();
    public int Rules { get; set; }
}

public sealed class Lexicon
{
    public Dictionary<string, Concept> Concepts { get; set; } = new();
}

/// <summary>Concept entry: terms may be plain strings or {t, w} objects.</summary>
public sealed class Concept
{
    public double Weight { get; set; } = 0.9;
    public List<JsonElement> Terms { get; set; } = new();

    public IEnumerable<(string Term, double Weight)> Flatten()
    {
        foreach (var t in Terms)
        {
            if (t.ValueKind == JsonValueKind.String)
                yield return (t.GetString()!, Weight);
            else if (t.ValueKind == JsonValueKind.Object)
            {
                var term = t.GetProperty("t").GetString()!;
                var w = t.TryGetProperty("w", out var wEl) ? wEl.GetDouble() : Weight;
                yield return (term, w);
            }
        }
    }
}
