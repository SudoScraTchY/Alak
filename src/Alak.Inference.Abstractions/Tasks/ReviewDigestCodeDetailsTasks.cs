using System.Text.Json;

namespace Alak.Inference.Abstractions.Tasks;

/// <summary>T3-02 "Review": batch re-review of needs_review items (spec §9.2).</summary>
public static class ReviewTask
{
    public const string TaskId = "T3-02";
    public const int MaxItems = 20;
    public const int MaxCharsPerItem = 300;
    public const int MaxTokens = 400;          // SLM-006
    public const float Temperature = 0.2f;
    public const float TopP = 0.9f;
    public const string PromptVersion = "v1";

    public const string OutputSchema = """
        {
          "type": "object",
          "required": ["items"],
          "properties": {
            "items": { "type": "array", "items": { "type": "object",
              "required": ["id", "category", "confidence"],
              "properties": {
                "id": { "type": "integer" },
                "category": { "type": "string",
                              "enum": ["offers","work","people","money","orders","security","social","updates","other"] },
                "confidence": { "type": "string", "enum": ["low", "medium", "high"] }
              } } }
          }
        }
        """;

    public static T302ReviewResult ParseResult(string raw)
    {
        if (!JsonValidator.TryParse(raw, out var doc))
            throw new InvalidOperationException("T3-02: model output was not valid JSON (SLM-002).");
        using (doc)
        {
            var items = doc.RootElement.GetProperty("items").EnumerateArray()
                .Select(i => new T302Item(
                    Id: i.GetProperty("id").GetInt32(),
                    Category: i.GetProperty("category").GetString()!,
                    Confidence: i.GetProperty("confidence").GetString()!))
                .ToArray();
            return new T302ReviewResult(items);
        }
    }
}

public sealed record T302ReviewResult(T302Item[] Items);
public sealed record T302Item(int Id, string Category, string Confidence);

/// <summary>T3-03 "Digest": summarize the day (spec §9.2).</summary>
public static class DigestTask
{
    public const string TaskId = "T3-03";
    public const int MaxInputTokens = 1500;
    public const int MaxTokens = 500;          // SLM-006
    public const float Temperature = 0.2f;
    public const float TopP = 0.9f;
    public const string PromptVersion = "v1";

    public const string OutputSchema = """
        {
          "type": "object",
          "required": ["headline", "sections"],
          "properties": {
            "headline": { "type": "string" },
            "sections": { "type": "array", "items": { "type": "object",
              "required": ["category", "bullets"],
              "properties": {
                "category": { "type": "string",
                              "enum": ["offers","work","people","money","orders","security","social","updates","other"] },
                "bullets": { "type": "array", "items": { "type": "string" }, "maxItems": 5 }
              } } },
            "follow_ups": { "type": "array", "items": { "type": "string" }, "maxItems": 3 }
          }
        }
        """;

    public static T303DigestResult ParseResult(string raw)
    {
        if (!JsonValidator.TryParse(raw, out var doc))
            throw new InvalidOperationException("T3-03: model output was not valid JSON (SLM-002).");
        using (doc)
        {
            var root = doc.RootElement;
            var sections = root.GetProperty("sections").EnumerateArray()
                .Select(s => new T303Section(
                    Category: s.GetProperty("category").GetString()!,
                    Bullets: s.GetProperty("bullets").EnumerateArray()
                        .Select(b => b.GetString() ?? "").ToArray()))
                .ToArray();
            var followUps = root.TryGetProperty("follow_ups", out var fu)
                ? fu.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
                : Array.Empty<string>();
            return new T303DigestResult(
                Headline: root.GetProperty("headline").GetString()!, sections, followUps);
        }
    }
}

public sealed record T303DigestResult(string Headline, T303Section[] Sections, string[] FollowUps);
public sealed record T303Section(string Category, string[] Bullets);

/// <summary>T3-04 "code_details": fill gaps on a wallet code (spec §9.2).</summary>
public static class CodeDetailsTask
{
    public const string TaskId = "T3-04";
    public const int MaxTokens = 200;          // SLM-006
    public const float Temperature = 0.2f;
    public const float TopP = 0.9f;
    public const string PromptVersion = "v1";

    public const string OutputSchema = """
        {
          "type": "object",
          "properties": {
            "min_spend": { "type": ["number", "null"] },
            "cap": { "type": ["number", "null"] },
            "valid_until": { "type": ["string", "null"], "format": "iso-date-time" },
            "conditions": { "type": "array", "items": { "type": "string" }, "maxItems": 4 }
          }
        }
        """;

    public static T304CodeDetailsResult ParseResult(string raw)
    {
        if (!JsonValidator.TryParse(raw, out var doc))
            throw new InvalidOperationException("T3-04: model output was not valid JSON (SLM-002).");
        using (doc)
        {
            var root = doc.RootElement;
            return new T304CodeDetailsResult(
                MinSpend: root.TryGetProperty("min_spend", out var ms) && ms.ValueKind == JsonValueKind.Number
                    ? ms.GetDouble() : (double?)null,
                Cap: root.TryGetProperty("cap", out var cap) && cap.ValueKind == JsonValueKind.Number
                    ? cap.GetDouble() : (double?)null,
                ValidUntil: root.TryGetProperty("valid_until", out var vu) && vu.ValueKind == JsonValueKind.String
                    ? vu.GetString() : null,
                Conditions: root.TryGetProperty("conditions", out var conds) && conds.ValueKind == JsonValueKind.Array
                    ? conds.EnumerateArray().Select(c => c.GetString() ?? "").ToArray()
                    : Array.Empty<string>());
        }
    }
}

public sealed record T304CodeDetailsResult(
    double? MinSpend,
    double? Cap,
    string? ValidUntil,
    string[] Conditions);
