using System.Text.Json;
using System.Text.RegularExpressions;

namespace Alak.Packs.Tool;

/// <summary>PCK-001/PCK-004: schema + safety validation for a pack directory.</summary>
public sealed class PackValidator
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public bool Validate(Pack pack)
    {
        // manifest basics (PCK-001: schemaVersion 1)
        if (pack.Manifest.SchemaVersion != 1) Errors.Add($"schemaVersion must be 1, got {pack.Manifest.SchemaVersion}");
        if (string.IsNullOrWhiteSpace(pack.Manifest.Id)) Errors.Add("id missing");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(pack.Manifest.Id, "^[a-z][a-z0-9-]{1,15}$"))
            Errors.Add($"id '{pack.Manifest.Id}' must be lowercase kebab");
        if (string.IsNullOrWhiteSpace(pack.Manifest.Version)) Errors.Add("version missing");
        if (pack.Manifest.Direction is not ("ltr" or "rtl")) Errors.Add($"direction must be ltr/rtl, got '{pack.Manifest.Direction}'");

        // concept ids from the catalog (§8.6); engine-owned namespaces
        var prefixOk = new[] { "promo.", "ad.", "otp.", "security.", "money.", "work.", "people.",
            "orders.", "social.", "updates.", "channel.", "time." };
        var termCount = 0;
        foreach (var (cid, concept) in pack.Concepts)
        {
            if (!prefixOk.Any(p => cid.StartsWith(p, StringComparison.Ordinal)))
                Errors.Add($"concept '{cid}' is outside the §8.6 catalog namespaces");
            var flattened = concept.Flatten().ToList();
            termCount += flattened.Count;
            if (flattened.Count == 0) Errors.Add($"concept '{cid}' has no terms");
            foreach (var (term, w) in flattened)
            {
                if (string.IsNullOrWhiteSpace(term)) Errors.Add($"concept '{cid}' has an empty term");
                if (w is < 0 or > 1.5) Errors.Add($"term '{term}' in '{cid}': weight {w} out of range [0, 1.5]");
                // PCK-004 safety limits
                if (term.Length > 200) Errors.Add($"term '{term}' exceeds 200 chars");
            }
        }

        // PCK-004: rule-count limit (patterns.json signal/pattern rules)
        var patternsPath = Path.Combine(pack.Root, "patterns.json");
        if (File.Exists(patternsPath))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(patternsPath));
            var rules = 0;
            if (doc.RootElement.TryGetProperty("signalRules", out var sr))
                rules += sr.GetArrayLength();
            if (doc.RootElement.TryGetProperty("patternRules", out var pr))
                rules += pr.GetArrayLength();
            if (rules > 5_000) Errors.Add($"pack has {rules} rules; limit is 5000 (PCK-004)");

            // regexes must compile with NonBacktracking and are length-limited
            if (doc.RootElement.TryGetProperty("patternRules", out var prs))
                foreach (var rule in prs.EnumerateArray())
                {
                    if (!rule.TryGetProperty("regex", out var rx)) continue;
                    var pattern = rx.GetString()!;
                    if (pattern.Length > 2_000) { Errors.Add($"regex in '{rule.GetProperty("id").GetString()}' exceeds 2000 chars"); continue; }
                    try { new Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.Compiled); }
                    catch (ArgumentException ex) { Errors.Add($"regex in '{rule.GetProperty("id").GetString()}' fails NonBacktracking compilation: {ex.Message}"); }
                }
        }

        if (termCount > 50_000) Errors.Add($"pack has {termCount} terms; limit is 50000");

        // PCK-006: concepts need 5+ terms to be considered complete
        foreach (var (cid, concept) in pack.Concepts)
            if (concept.Flatten().Count() < 5)
                Warnings.Add($"concept '{cid}' has fewer than 5 terms — flagged incomplete (PCK-006)");

        return Errors.Count == 0;
    }
}
