using Alak.Core.Classification;
using Alak.Core.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alak.Adaptive;

/// <summary>
/// Tier 2 Adaptive Engine interface (spec §9.1).
/// On-device multinomial logistic regression with AdaGrad online learning.
/// Features: hashed word unigrams/bigrams, char 3-5-grams, package, channel, category, concept hits, entities, emoji, script flags, length bucket.
/// D = 2^16 feature slots × C = 9 categories.
/// </summary>
public interface IAdaptiveEngine
{
    /// <summary>
    /// Learn from a user correction (weight ×8 per ADP-004).
    /// </summary>
    Task LearnFromCorrectionAsync(string package, string channel, string foldedText, IReadOnlyList<ConceptHit> conceptHits, IReadOnlyList<Entity> entities, Category correctedCategory, string? androidCategory = null, bool autoSave = false);

    /// <summary>
    /// Learn from a high-confidence Tier 1 result (confidence ≥ 0.90, weight ×1, capped to 30% of updates).
    /// </summary>
    Task LearnFromHighConfidenceAsync(string package, string channel, string foldedText, IReadOnlyList<ConceptHit> conceptHits, IReadOnlyList<Entity> entities, Category tier1Category, float tier1Confidence, string? androidCategory = null);

    /// <summary>
    /// Get Tier 2 logits for blending with Tier 1.
    /// Returns logits per category (C=9), or null if model not ready.
    /// </summary>
    Task<float[]?> InferAsync(string package, string channel, string foldedText, IReadOnlyList<ConceptHit> conceptHits, IReadOnlyList<Entity> entities, string? androidCategory = null);

    /// <summary>
    /// Current β value for blending (0.3 with seed only, rises to 0.8 after 50 corrections).
    /// </summary>
    float Beta { get; }

    /// <summary>
    /// Number of user corrections applied.
    /// </summary>
    int CorrectionCount { get; }

    /// <summary>
    /// Reset personalization (delete personal weights and corrections).
    /// </summary>
    Task ResetAsync();

    /// <summary>
    /// Save model to disk (atomic write: temp + rename).
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Load model from disk.
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Load weights from disk with graceful corruption recovery (WBP-03).
    /// </summary>
    Task LoadWeightsAsync();

    /// <summary>
    /// Blend Tier 1 and Tier 2 decisions, strictly maintaining security invariance (Category.Security never overridden).
    /// </summary>
    Category BlendDecision(Category tier1Category, float tier1Confidence, float[]? tier2Logits);

    /// <summary>
    /// Whether the engine has a valid model loaded.
    /// </summary>
    bool IsReady { get; }
}