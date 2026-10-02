using System.Text.Json;
using Xunit;
using Shouldly;

namespace Alak.Rules.Tests;

/// <summary>
/// QLT-001 / M10-prep (GOAL E): Verify adversarial EN corpus structure.
/// Ensures the adversarial corpus file exists, has ≥ 60 cases, covers all 10
/// adversarial_type categories, and each case conforms to the expected schema.
/// </summary>
public class AdversarialCorpusTests
{
    private static readonly string s_root = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "corpus"));

    private const string AdversarialPath = "en/adversarial.json";

    private static readonly string[] ExpectedAdversarialTypes =
    {
        "obfuscation",
        "mixed_scripts",
        "punctuation_glue",
        "rtl_marks",
        "emoji_only",
        "empty_title",
        "large_body",
        "truncated_text",
        "inbox_style_lines",
        "messaging_style_threads"
    };

    [Fact]
    public void AdversarialFile_Exists()
    {
        var path = Path.Combine(s_root, AdversarialPath);
        File.Exists(path).ShouldBeTrue($"Adversarial corpus file not found at {path}");
    }

    [Fact]
    public void AdversarialFile_HasMinimumCases()
    {
        var path = Path.Combine(s_root, AdversarialPath);
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var count = doc.RootElement.GetArrayLength();
        count.ShouldBeGreaterThanOrEqualTo(60, $"Expected ≥ 60 adversarial cases, got {count}");
    }

    [Fact]
    public void AdversarialFile_CoversAllTenAdversarialTypes()
    {
        var path = Path.Combine(s_root, AdversarialPath);
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        var foundTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.TryGetProperty("expect", out var expect) &&
                expect.TryGetProperty("adversarial_type", out var typeProp))
            {
                foundTypes.Add(typeProp.GetString()!);
            }
        }

        foreach (var expected in ExpectedAdversarialTypes)
        {
            foundTypes.ShouldContain(expected, $"Missing adversarial_type: {expected}");
        }
    }

    [Fact]
    public void AdversarialFile_EveryCaseHasRequiredSchema()
    {
        var path = Path.Combine(s_root, AdversarialPath);
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        var requiredFields = new[] { "id", "package", "channel_name", "title", "text", "expect" };
        var expectRequiredFields = new[] { "category", "adversarial_type" };

        var errors = new List<string>();
        int index = 0;

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            index++;
            var caseId = element.TryGetProperty("id", out var idProp) ? idProp.GetString() : $"index-{index}";

            // Check top-level required fields
            foreach (var field in requiredFields)
            {
                if (!element.TryGetProperty(field, out _))
                {
                    errors.Add($"{caseId}: missing required field '{field}'");
                }
            }

            // Check expect object
            if (element.TryGetProperty("expect", out var expect))
            {
                foreach (var field in expectRequiredFields)
                {
                    if (!expect.TryGetProperty(field, out _))
                    {
                        errors.Add($"{caseId}: expect missing required field '{field}'");
                    }
                }

                // Validate category is a known category
                if (expect.TryGetProperty("category", out var catProp))
                {
                    var cat = catProp.GetString();
                    var validCategories = new[] { "offers", "work", "people", "money", "orders", "security", "social", "updates", "other" };
                    if (!validCategories.Contains(cat, StringComparer.OrdinalIgnoreCase))
                    {
                        errors.Add($"{caseId}: invalid category '{cat}'");
                    }
                }

                // Validate adversarial_type is one of the expected 10
                if (expect.TryGetProperty("adversarial_type", out var typeProp))
                {
                    var type = typeProp.GetString();
                    if (!ExpectedAdversarialTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                    {
                        errors.Add($"{caseId}: unknown adversarial_type '{type}'");
                    }
                }
            }

            // Verify id follows expected pattern
            if (element.TryGetProperty("id", out var idProp2))
            {
                var id = idProp2.GetString();
                if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("en-adv-", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{caseId}: id should start with 'en-adv-'");
                }
            }
        }

        errors.ShouldBeEmpty($"Schema validation failed:\n{string.Join("\n", errors)}");
    }

    [Fact]
    public void AdversarialFile_NoDuplicateIds()
    {
        var path = Path.Combine(s_root, AdversarialPath);
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<string>();

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.TryGetProperty("id", out var idProp))
            {
                var id = idProp.GetString()!;
                if (!ids.Add(id))
                {
                    duplicates.Add(id);
                }
            }
        }

        duplicates.ShouldBeEmpty($"Duplicate ids found: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void AdversarialFile_EachTypeHasAtLeastSixCases()
    {
        var path = Path.Combine(s_root, AdversarialPath);
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.TryGetProperty("expect", out var expect) &&
                expect.TryGetProperty("adversarial_type", out var typeProp))
            {
                var type = typeProp.GetString()!;
                typeCounts[type] = typeCounts.GetValueOrDefault(type, 0) + 1;
            }
        }

        foreach (var expected in ExpectedAdversarialTypes)
        {
            var count = typeCounts.GetValueOrDefault(expected, 0);
            count.ShouldBeGreaterThanOrEqualTo(6, $"Adversarial type '{expected}' has only {count} cases (minimum 6)");
        }
    }
}