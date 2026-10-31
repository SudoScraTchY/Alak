using System.Text.Json;

namespace Alak.Inference.Abstractions.Tasks;

/// <summary>T3-01 "Explain": explain a single notification (spec §9.2).</summary>
public static class ExplainTask
{
    public const string TaskId = "T3-01";
    public const int MaxInputChars = 1200;
    public const int MaxTokens = 200;          // SLM-006
    public const float Temperature = 0.2f;     // SLM-006
    public const float TopP = 0.9f;            // SLM-006
    public const string PromptVersion = "v2";

    /// <summary>Output schema (JSON schema string) shown to the engine / used for validation.</summary>
    public const string OutputSchema = """
        {
          "type": "object",
          "required": ["summary", "importance"],
          "properties": {
            "summary": { "type": "string", "description": "≤ 25 words, plain language" },
            "importance": { "type": "string", "enum": ["low", "normal", "high"] },
            "action": { "type": ["string", "null"] },
            "category_suggestion": { "type": ["string", "null"] },
            "tags": { "type": "array", "items": { "type": "string" }, "maxItems": 4 },
            "facts": { "type": "array", "items": { "type": "object",
                       "properties": { "label": { "type": "string" }, "value": { "type": "string" } },
                       "required": ["label", "value"] }, "maxItems": 5 }
          }
        }
        """;

    public static T301ExplainResult ParseResult(string raw)
    {
        if (!JsonValidator.TryParse(raw, out var doc))
            throw new InvalidOperationException("T3-01: model output was not valid JSON (SLM-002).");
        using (doc)
        {
            var root = doc.RootElement;
            var summary = TryGetString(root, "summary");
            if (string.IsNullOrWhiteSpace(summary))
                throw new InvalidOperationException("T3-01: model output has no summary (SLM-002).");

            var importance = TryGetString(root, "importance")?.ToLowerInvariant();
            if (importance is not ("low" or "normal" or "high")) importance = "normal";

            var category = TryGetString(root, "category_suggestion")?.ToLowerInvariant();
            if (category is not (null or "offers" or "promo" or "work" or "people" or "money"
                or "orders" or "security" or "social" or "updates" or "other"))
            {
                category = null;
            }

            var tags = TryGetStrings(root, "tags").Take(4).ToArray();

            return new T301ExplainResult(
                Summary: summary!,
                Importance: importance,
                Action: TryGetString(root, "action"),
                CategorySuggestion: category,
                Tags: tags,
                Facts: ReadFacts(root));
        }
    }

    private static T3Fact[] ReadFacts(JsonElement root)
    {
        if (!root.TryGetProperty("facts", out var facts) || facts.ValueKind != JsonValueKind.Array)
            return Array.Empty<T3Fact>();

        var results = new List<T3Fact>();
        foreach (var f in facts.EnumerateArray())
        {
            if (results.Count >= 5) break;
            if (f.ValueKind != JsonValueKind.Object) continue;
            var label = TryGetString(f, "label");
            var value = TryGetString(f, "value");
            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value)) continue;
            results.Add(new T3Fact(label!, value!));
        }

        return results.ToArray();
    }

    internal static string? TryGetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String
            ? e.GetString() : null;

    internal static string[] TryGetStrings(JsonElement root, string name)
        => root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Array
            ? e.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                               .Select(x => x.GetString() ?? "").ToArray()
            : Array.Empty<string>();
}

public sealed record T301ExplainResult(
    string Summary,
    string Importance,
    string? Action,
    string? CategorySuggestion,
    string[] Tags,
    T3Fact[] Facts);

public sealed record T3Fact(string Label, string Value);
