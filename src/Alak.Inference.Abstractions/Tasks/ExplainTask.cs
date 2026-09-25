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
    public const string PromptVersion = "v1";

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
            return new T301ExplainResult(
                Summary: root.GetProperty("summary").GetString()!,
                Importance: root.GetProperty("importance").GetString()!,
                Action: TryGetString(root, "action"),
                CategorySuggestion: TryGetString(root, "category_suggestion"),
                Tags: TryGetStrings(root, "tags"),
                Facts: root.TryGetProperty("facts", out var facts)
                    ? facts.EnumerateArray().Select(f => new T3Fact(
                        Label: f.GetProperty("label").GetString() ?? "",
                        Value: f.GetProperty("value").GetString() ?? "")).ToArray()
                    : Array.Empty<T3Fact>());
        }
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
