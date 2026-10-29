namespace Alak.Rules;

using System.Text.Json;
using System.Text.Json.Serialization;
using Alak.Core.Model;

/// <summary>
/// Calibrated weights and parameters for Tier 1 rules engine (RUL-009, RUL-010, RUL-019).
/// </summary>
public sealed record WeightsConfig(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("category_bias")] Dictionary<string, float> CategoryBias,
    [property: JsonPropertyName("family_caps")] Dictionary<string, float> FamilyCaps,
    [property: JsonPropertyName("softmax")] SoftmaxConfig Softmax,
    [property: JsonPropertyName("metadata_evidence")] Dictionary<string, Dictionary<string, float>> MetadataEvidence,
    [property: JsonPropertyName("entity_evidence")] Dictionary<string, Dictionary<string, float>> EntityEvidence)
{
    private static WeightsConfig? s_default;
    public static WeightsConfig Default => s_default ??= LoadDefault();

    private float[]? _categoryBiasVector;
    [JsonIgnore]
    public float[] CategoryBiasVector
    {
        get
        {
            if (_categoryBiasVector is not null) return _categoryBiasVector;
            var vec = new float[9];
            if (CategoryBias is not null)
            {
                foreach (var (k, v) in CategoryBias)
                {
                    if (Enum.TryParse<Category>(k, ignoreCase: true, out var cat))
                        vec[(int)cat] = v;
                }
            }
            return _categoryBiasVector = vec;
        }
    }

    public static WeightsConfig LoadDefault()
    {
        var asm = typeof(WeightsConfig).Assembly;
        using var stream = asm.GetManifestResourceStream("Alak.Rules.Resources.weights.json")
                        ?? asm.GetManifestResourceStream("Resources.weights.json");
        if (stream is not null)
        {
            return JsonSerializer.Deserialize<WeightsConfig>(stream)!;
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Resources", "weights.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Alak.Rules", "Resources", "weights.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Resources", "weights.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Alak.Rules", "Resources", "weights.json"),
        };
        foreach (var p in candidates)
        {
            try
            {
                var full = Path.GetFullPath(p);
                if (File.Exists(full))
                {
                    using var fs = File.OpenRead(full);
                    return JsonSerializer.Deserialize<WeightsConfig>(fs)!;
                }
            }
            catch { }
        }

        throw new InvalidOperationException("Could not load weights.json from embedded resource or disk.");
    }
}

public sealed record SoftmaxConfig(
    [property: JsonPropertyName("temperature")] float Temperature,
    [property: JsonPropertyName("confidence_threshold")] float ConfidenceThreshold,
    [property: JsonPropertyName("review_confidence_threshold")] float ReviewConfidenceThreshold,
    [property: JsonPropertyName("review_margin_threshold")] float ReviewMarginThreshold);
