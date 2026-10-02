using Alak.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Alak.Text.Tests;

/// <summary>
/// RTL/Bidi audit tests for FA pack (Arabic script).
/// Validates LTR/RTL mismatches, Bidi_Control chars, NFKC vs NFD stability, and Zero-Width joiners.
/// Spec: NRM-002, NRM-003, NRM-004, NRM-006, LNG-001.
/// </summary>
public class RtlAuditTests
{
    private static readonly RtlAuditCorpus Corpus = LoadCorpus();

    private static RtlAuditCorpus LoadCorpus()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "corpus", "fa_rtl_audit.json"));
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<RtlAuditCorpus>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    [Theory]
    [MemberData(nameof(BidiControlRemovalCases))]
    public void RTL_BidiControlRemoval(RtlAuditCase testCase)
    {
        var n = Normalizer.Normalize(testCase.Input);
        
        // Folded should not contain the bidi control character
        if (!string.IsNullOrEmpty(testCase.ExpectFoldedNotContains))
        {
            n.Folded.ShouldNotContain(testCase.ExpectFoldedNotContains);
        }
        
        // Text (display) should contain the original bidi control
        if (!string.IsNullOrEmpty(testCase.ExpectTextContains))
        {
            n.Text.ShouldContain(testCase.ExpectTextContains);
        }
        
        // Folded should match expected
        if (!string.IsNullOrEmpty(testCase.ExpectFolded))
        {
            n.Folded.ShouldBe(testCase.ExpectFolded);
        }
        
        // Folded should contain expected substring
        if (!string.IsNullOrEmpty(testCase.ExpectFoldedContains))
        {
            n.Folded.ShouldContain(testCase.ExpectFoldedContains);
        }
    }

    [Theory]
    [MemberData(nameof(ZwjHandlingCases))]
    public void RTL_ZeroWidthJoinerHandling(RtlAuditCase testCase)
    {
        var n = Normalizer.Normalize(testCase.Input);
        
        // ZWJ should be removed (not become space)
        if (!string.IsNullOrEmpty(testCase.ExpectFoldedNotContains))
        {
            n.Folded.ShouldNotContain(testCase.ExpectFoldedNotContains);
        }
        
        if (!string.IsNullOrEmpty(testCase.ExpectFolded))
        {
            n.Folded.ShouldBe(testCase.ExpectFolded);
        }
    }

    [Theory]
    [MemberData(nameof(NfcNfdStabilityCases))]
    public void RTL_NfcNfdStability(RtlAuditCase testCase)
    {
        var n = Normalizer.Normalize(testCase.Input);
        
        if (!string.IsNullOrEmpty(testCase.ExpectFolded))
        {
            n.Folded.ShouldBe(testCase.ExpectFolded);
        }
        
        if (!string.IsNullOrEmpty(testCase.ExpectFoldedNotContains))
        {
            n.Folded.ShouldNotContain(testCase.ExpectFoldedNotContains);
        }
        
        if (testCase.ExpectFoldedNotContainsAny != null)
        {
            foreach (var c in testCase.ExpectFoldedNotContainsAny)
            {
                n.Folded.ShouldNotContain(c);
            }
        }
    }

    [Theory]
    [MemberData(nameof(LetterUnificationCases))]
    public void RTL_LetterUnification(RtlAuditCase testCase)
    {
        var n = Normalizer.Normalize(testCase.Input);
        
        if (!string.IsNullOrEmpty(testCase.ExpectFolded))
        {
            n.Folded.ShouldBe(testCase.ExpectFolded);
        }
    }

    [Theory]
    [MemberData(nameof(MixedScriptCases))]
    public void RTL_MixedScriptHandling(RtlAuditCase testCase)
    {
        var n = Normalizer.Normalize(testCase.Input);
        
        if (!string.IsNullOrEmpty(testCase.ExpectFolded))
        {
            n.Folded.ShouldBe(testCase.ExpectFolded);
        }
        
        if (testCase.ExpectScriptRuns != null && testCase.ExpectScriptRuns.Length > 0)
        {
            var runs = ScriptLanguage.SplitIntoRuns(n.Folded);
            var scripts = runs.Select(r => r.Script.ToString()).ToArray();
            scripts.ShouldBe(testCase.ExpectScriptRuns);
        }
    }

    [Theory]
    [MemberData(nameof(SpanMappingCases))]
    public void RTL_SpanMapping(RtlAuditCase testCase)
    {
        var n = Normalizer.Normalize(testCase.Input);
        
        // Just verify that span mapping doesn't throw and works correctly
        // Map a known span in Folded back to original
        if (testCase.ExpectSpanMappingWorks)
        {
            // Find a non-empty span in Folded
            if (n.Folded.Length > 0)
            {
                var (start, end) = n.OriginalSpan(0, Math.Min(1, n.Folded.Length));
                start.ShouldBeGreaterThanOrEqualTo(0);
                end.ShouldBeGreaterThanOrEqualTo(start);
            }
        }
    }

    [Theory]
    [MemberData(nameof(EdgeCases))]
    public void RTL_EdgeCases(RtlAuditCase testCase)
    {
        var n = Normalizer.Normalize(testCase.Input);
        
        if (!string.IsNullOrEmpty(testCase.ExpectFolded))
        {
            n.Folded.ShouldBe(testCase.ExpectFolded);
        }
        
        if (!string.IsNullOrEmpty(testCase.ExpectText))
        {
            n.Text.ShouldBe(testCase.ExpectText);
        }
    }

    public static IEnumerable<object[]> BidiControlRemovalCases =>
        Corpus.Cases
            .Where(c => c.Category == "bidi_control_removal")
            .Select(c => new object[] { c });

    public static IEnumerable<object[]> ZwjHandlingCases =>
        Corpus.Cases
            .Where(c => c.Category == "zwj_handling")
            .Select(c => new object[] { c });

    public static IEnumerable<object[]> NfcNfdStabilityCases =>
        Corpus.Cases
            .Where(c => c.Category == "nfc_nfd_stability")
            .Select(c => new object[] { c });

    public static IEnumerable<object[]> LetterUnificationCases =>
        Corpus.Cases
            .Where(c => c.Category == "letter_unification")
            .Select(c => new object[] { c });

    public static IEnumerable<object[]> MixedScriptCases =>
        Corpus.Cases
            .Where(c => c.Category == "mixed_script")
            .Select(c => new object[] { c });

    public static IEnumerable<object[]> SpanMappingCases =>
        Corpus.Cases
            .Where(c => c.Category == "span_mapping")
            .Select(c => new object[] { c });

    public static IEnumerable<object[]> EdgeCases =>
        Corpus.Cases
            .Where(c => c.Category == "edge_case")
            .Select(c => new object[] { c });
}

/// <summary>
/// JSON models for RTL audit corpus
/// </summary>
public class RtlAuditCorpus
{
    public RtlAuditMetadata AuditMetadata { get; set; } = new();
    public List<RtlAuditCase> Cases { get; set; } = new();
}

public class RtlAuditMetadata
{
    public string Description { get; set; } = "";
    public List<string> SpecReferences { get; set; } = new();
    public string Generated { get; set; } = "";
    public int TotalCases { get; set; }
}

public class RtlAuditCase
{
    public string Id { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string Input { get; set; } = "";
    public string ExpectFolded { get; set; } = "";
    public string ExpectText { get; set; } = "";
    public string ExpectTextContains { get; set; } = "";
    public string ExpectFoldedContains { get; set; } = "";
    public string ExpectFoldedNotContains { get; set; } = "";
    public List<string> ExpectFoldedNotContainsAny { get; set; } = new();
    public string[] ExpectScriptRuns { get; set; } = Array.Empty<string>();
    public string DescriptionDetail { get; set; } = "";
    public bool ExpectSpanMappingWorks { get; set; }
}