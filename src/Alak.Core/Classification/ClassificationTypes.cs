namespace Alak.Core.Classification;

using Model;

/// <summary>
/// One piece of positive evidence for a category decision (spec §6.6 / RUL-020).
/// The top ≤ 6 items become the explainability trace stored in evidence_json.
/// </summary>
/// <param name="Category">The category this evidence supports.</param>
/// <param name="Weight">Contribution weight; the result's confidence is clamped to 0–1.</param>
/// <param name="RuleId">Originating rule (e.g. "RUL-003", "E-01", "user-rule:7").</param>
/// <param name="ReasonKey">Key into the app's localized reason templates (never raw pack strings).</param>
/// <param name="Args">Optional values interpolated into the localized reason sentence.</param>
/// <param name="Span">Location of the triggering text in the ORIGINAL notification text.</param>
public sealed record Evidence(
    Category Category,
    float Weight,
    string RuleId,
    string ReasonKey,
    IReadOnlyList<string>? Args,
    (int Start, int End) Span);

/// <summary>A concept term hit produced by the pack matcher (spec §8, RUL-003).</summary>
public sealed record ConceptHit(
    string ConceptId,
    (int Start, int End) Span,
    float Weight,
    bool Fuzzy,
    string Term);

/// <summary>The outcome of classifying one notification (spec §6.6).</summary>
public sealed record ClassificationResult(
    Category Category,
    CategorySource Source,
    float Confidence,
    float Margin,
    bool NeedsReview,
    IReadOnlyList<Tag> Tags,
    IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<Entity> Entities,
    IReadOnlyList<ConceptHit>? ConceptHits = null)
{
    /// <summary>Concept hits triggered during classification (RUL-003 / WBP-02).</summary>
    public IReadOnlyList<ConceptHit> ConceptHits { get; init; } = ConceptHits ?? Array.Empty<ConceptHit>();

    /// <summary>Confidence clamped to 0–1.</summary>
    public float Confidence { get; init; } = Math.Clamp(Confidence, 0f, 1f);

    /// <summary>Margin clamped to 0–1.</summary>
    public float Margin { get; init; } = Math.Clamp(Margin, 0f, 1f);

    /// <summary>Explainability trace, strongest-first, capped at 6 items (RUL-020).</summary>
    public IReadOnlyList<Evidence> EvidenceTrace { get; } =
        Evidence.OrderByDescending(e => e.Weight).Take(6).ToArray();
}
