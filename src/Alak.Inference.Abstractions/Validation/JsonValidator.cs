using System.Text.Json;

namespace Alak.Inference.Abstractions;

/// <summary>
/// Tolerant JSON extraction/validation for SLM output (SLM-002). Small local models
/// wrap JSON in chatter and code fences and sometimes add trailing commas; the
/// pipeline retries once, so extraction must be robust and failure must be clean.
/// </summary>
public static class JsonValidator
{
    /// <summary>
    /// Extract the first balanced JSON object from arbitrary model output.
    /// Strips ``` fences, scans for the first '{', and tracks nesting while
    /// honouring string state. Returns null when no complete object exists.
    /// </summary>
    public static string? ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Strip fenced code blocks but keep the content: ```json {...} ```
        var text = raw;
        var fenceStart = text.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart >= 0)
        {
            var afterFirstFence = fenceStart + 3;
            // skip optional language tag (e.g. "json\n")
            var nl = text.IndexOf('\n', afterFirstFence);
            if (nl >= 0) afterFirstFence = nl + 1;
            var fenceEnd = text.IndexOf("```", afterFirstFence, StringComparison.Ordinal);
            if (fenceEnd >= 0)
                text = text[afterFirstFence..fenceEnd];
        }

        var start = text.IndexOf('{');
        if (start < 0) return null;

        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (escaped) { escaped = false; continue; }
            if (inString)
            {
                if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }
            switch (c)
            {
                case '"': inString = true; break;
                case '{': depth++; break;
                case '}':
                    depth--;
                    if (depth == 0)
                        return RepairTrailingCommas(text[start..(i + 1)]);
                    break;
            }
        }
        return null;
    }

    /// <summary>
    /// Try to parse the first balanced JSON object embedded in <paramref name="raw"/>;
    /// returns false with no exception on invalid output (SLM-002 retry hook).
    /// </summary>
    public static bool TryParse(string raw, out JsonDocument doc)
    {
        doc = null!;
        var json = ExtractJsonObject(raw);
        if (json is null) return false;
        try
        {
            doc = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Remove trailing commas before '}' or ']' — a common SLM slip.</summary>
    private static string RepairTrailingCommas(string json)
    {
        var chars = json.ToCharArray();
        for (var i = 0; i < chars.Length - 1; i++)
        {
            if (chars[i] != ',') continue;
            var j = i + 1;
            while (j < chars.Length && char.IsWhiteSpace(chars[j])) j++;
            if (j < chars.Length && (chars[j] == '}' || chars[j] == ']'))
                chars[i] = ' ';
        }
        return new string(chars);
    }
}
