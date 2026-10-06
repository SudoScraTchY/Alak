using Alak.Core.Classification;
using Alak.Core.Model;
using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Alak.Adaptive;

/// <summary>
/// Feature hashing utilities for the adaptive engine.
/// </summary>
internal static class FeatureHasher
{
    private const uint D = 1 << 16; // 65536 feature slots
    private const uint Seed = 0x9E3779B9; // Golden ratio hash seed

    /// <summary>
    /// Hash a feature string to a signed index in [0, D-1] using signed hashing trick.
    /// Returns (index, sign) where sign is +1 or -1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int index, int sign) Hash(string feature)
    {
        uint hash = Seed;
        foreach (byte b in System.Text.Encoding.UTF8.GetBytes(feature))
        {
            hash = (hash ^ b) * 0x1000001B; // FNV-1a
        }
        int index = (int)(hash & (D - 1));
        int sign = ((hash >> 16) & 1) == 1 ? 1 : -1;
        return (index, sign);
    }

    /// <summary>
    /// Hash a feature string to a signed index with a namespace prefix.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int index, int sign) Hash(string ns, string feature)
    {
        return Hash(ns + ":" + feature);
    }
}

/// <summary>
/// Tier 2 Adaptive Engine implementation (spec §9.1).
/// Multinomial logistic regression with AdaGrad, signed hashing trick.
/// </summary>
public sealed class AdaptiveEngine : IAdaptiveEngine
{
    // Model dimensions
    private const int D = 1 << 16; // 65536 feature slots
    private const int C = 9;       // 9 categories
    private const int ModelVersion = 1;
    private const string ModelFileName = "adaptive-v1.bin";

    // Learning parameters
    private const float AdaGradEps = 1e-6f;
    private const float L2Lambda = 1e-4f;
    private const float CorrectionWeight = 8f;  // ADP-004: user corrections weight ×8
    private const float PseudoLabelWeight = 1f; // High-confidence Tier 1 weight ×1
    private const float PseudoLabelCap = 0.3f;  // Cap at 30% of updates
    private const float BetaStart = 0.3f;
    private const float BetaEnd = 0.8f;
    private const int BetaRampCorrections = 50;

    // Model state
    private readonly float[] _weights;        // D × C, row-major: w[feature * C + category]
    private readonly float[] _gradSumSq;      // AdaGrad accumulator (same shape)
    private int _correctionCount;
    private int _pseudoLabelCount;
    private int _totalUpdates;
    private bool _isLoaded;
    private readonly string _modelPath;

    // Category index mapping (matches Category enum order by priority)
    private static readonly Category[] CategoryIndexToEnum =
    [
        Category.Security, // 0
        Category.Money,    // 1
        Category.Work,     // 2
        Category.Orders,   // 3
        Category.People,   // 4
        Category.Offers,   // 5
        Category.Social,   // 6
        Category.Updates,  // 7
        Category.Other     // 8
    ];

    private static readonly Dictionary<Category, int> CategoryEnumToIndex = new()
    {
        [Category.Security] = 0,
        [Category.Money] = 1,
        [Category.Work] = 2,
        [Category.Orders] = 3,
        [Category.People] = 4,
        [Category.Offers] = 5,
        [Category.Social] = 6,
        [Category.Updates] = 7,
        [Category.Other] = 8
    };

    public AdaptiveEngine()
    {
        _weights = new float[D * C];
        _gradSumSq = new float[D * C];
        _modelPath = Path.Combine(FileSystem.AppDataDirectory, ModelFileName);
        _isLoaded = false;
    }

    public float Beta
    {
        get
        {
            if (_correctionCount == 0) return BetaStart;
            if (_correctionCount >= BetaRampCorrections) return BetaEnd;
            return BetaStart + (BetaEnd - BetaStart) * (_correctionCount / (float)BetaRampCorrections);
        }
    }

    public int CorrectionCount => _correctionCount;
    public bool IsReady => _isLoaded;

    public async Task LearnFromCorrectionAsync(string package, string channel, string foldedText, IReadOnlyList<ConceptHit> conceptHits, IReadOnlyList<Entity> entities, Category correctedCategory, string? androidCategory = null)
    {
        if (!_isLoaded) await LoadAsync();
        
        var features = ExtractFeatures(package, channel, foldedText, conceptHits, entities, androidCategory);
        await UpdateWeightsAsync(features, correctedCategory, CorrectionWeight);
        _correctionCount++;
        _totalUpdates++;
        
        // Save periodically (every 5 corrections)
        if (_correctionCount % 5 == 0)
            await SaveAsync();
    }

    public async Task LearnFromHighConfidenceAsync(string package, string channel, string foldedText, IReadOnlyList<ConceptHit> conceptHits, IReadOnlyList<Entity> entities, Category tier1Category, float tier1Confidence, string? androidCategory = null)
    {
        if (!_isLoaded) await LoadAsync();
        
        // Cap pseudo-labels at 30% of total updates
        if (_totalUpdates > 0 && (float)_pseudoLabelCount / _totalUpdates >= PseudoLabelCap)
            return;

        var features = ExtractFeatures(package, channel, foldedText, conceptHits, entities, androidCategory);
        await UpdateWeightsAsync(features, tier1Category, PseudoLabelWeight);
        _pseudoLabelCount++;
        _totalUpdates++;
    }

    public async Task<float[]?> InferAsync(string package, string channel, string foldedText, IReadOnlyList<ConceptHit> conceptHits, IReadOnlyList<Entity> entities, string? androidCategory = null)
    {
        if (!_isLoaded) await LoadAsync();
        if (!_isLoaded) return null; // Still not loaded (no seed model)

        var features = ExtractFeatures(package, channel, foldedText, conceptHits, entities, androidCategory);
        return ComputeLogits(features);
    }

    public async Task ResetAsync()
    {
        Array.Clear(_weights, 0, _weights.Length);
        Array.Clear(_gradSumSq, 0, _gradSumSq.Length);
        _correctionCount = 0;
        _pseudoLabelCount = 0;
        _totalUpdates = 0;
        _isLoaded = false;
        
        if (File.Exists(_modelPath))
            File.Delete(_modelPath);
        
        await Task.CompletedTask;
    }

    public async Task SaveAsync()
    {
        var tempPath = _modelPath + ".tmp";
        try
        {
            using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(fs);

            // Header
            writer.Write((uint)0x414C414B); // Magic "ALAK"
            writer.Write(ModelVersion);
            writer.Write(D);
            writer.Write(C);
            writer.Write(_correctionCount);
            writer.Write(_pseudoLabelCount);
            writer.Write(_totalUpdates);

            // Weights
            for (int i = 0; i < _weights.Length; i++)
            {
                writer.Write(_weights[i]);
            }

            // Grad sum squares (AdaGrad state)
            for (int i = 0; i < _gradSumSq.Length; i++)
            {
                writer.Write(_gradSumSq[i]);
            }

            // Checksum (simple sum)
            uint checksum = 0;
            for (int i = 0; i < _weights.Length; i++)
            {
                checksum += (uint)(_weights[i] * 1000000);
            }
            writer.Write(checksum);

            await fs.FlushAsync();
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                if (File.Exists(_modelPath))
                    File.Replace(tempPath, _modelPath, null);
                else
                    File.Move(tempPath, _modelPath);
            }
        }
    }

    public async Task LoadAsync()
    {
        if (!File.Exists(_modelPath))
        {
            // Initialize with zero weights (no seed model yet)
            // In production, would load embedded seed model here
            _isLoaded = true;
            return;
        }

        try
        {
            using var fs = new FileStream(_modelPath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            uint magic = reader.ReadUInt32();
            if (magic != 0x414C414B)
                throw new InvalidDataException("Invalid model file magic");

            int version = reader.ReadInt32();
            if (version != ModelVersion)
                throw new InvalidDataException($"Model version mismatch: expected {ModelVersion}, got {version}");

            int d = reader.ReadInt32();
            int c = reader.ReadInt32();
            if (d != D || c != C)
                throw new InvalidDataException($"Model dimension mismatch: expected {D}×{C}, got {d}×{c}");

            _correctionCount = reader.ReadInt32();
            _pseudoLabelCount = reader.ReadInt32();
            _totalUpdates = reader.ReadInt32();

            // Read weights
            for (int i = 0; i < _weights.Length; i++)
            {
                _weights[i] = reader.ReadSingle();
            }

            // Read AdaGrad state
            for (int i = 0; i < _gradSumSq.Length; i++)
            {
                _gradSumSq[i] = reader.ReadSingle();
            }

            uint checksum = reader.ReadUInt32();
            
            // Verify checksum
            uint computedChecksum = 0;
            for (int i = 0; i < _weights.Length; i++)
            {
                computedChecksum += (uint)(_weights[i] * 1000000);
            }
            if (checksum != computedChecksum)
                throw new InvalidDataException("Model checksum mismatch");

            _isLoaded = true;
        }
        catch
        {
            // Corrupted model - reset
            Array.Clear(_weights, 0, _weights.Length);
            Array.Clear(_gradSumSq, 0, _gradSumSq.Length);
            _correctionCount = 0;
            _pseudoLabelCount = 0;
            _totalUpdates = 0;
            _isLoaded = true; // Still mark as loaded (zero weights)
        }
    }

    /// <summary>
    /// Extract features from notification data (spec §9.1 feature list).
    /// Returns list of (featureIndex, sign) pairs.
    /// </summary>
    private static List<(int index, int sign)> ExtractFeatures(string package, string channel, string foldedText, IReadOnlyList<ConceptHit> conceptHits, IReadOnlyList<Entity> entities, string? androidCategory)
    {
        var features = new List<(int index, int sign)>(64);

        // 1. Package ID (high signal)
        if (!string.IsNullOrEmpty(package))
            features.Add(FeatureHasher.Hash("pkg", package));

        // 2. Channel ID
        if (!string.IsNullOrEmpty(channel))
            features.Add(FeatureHasher.Hash("ch", channel));

        // 3. Android category
        if (!string.IsNullOrEmpty(androidCategory))
            features.Add(FeatureHasher.Hash("acat", androidCategory));

        // 4. Word unigrams from folded text
        var words = foldedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            if (word.Length >= 2) // Skip very short tokens
                features.Add(FeatureHasher.Hash("w1", word));
        }

        // 5. Word bigrams
        for (int i = 0; i < words.Length - 1; i++)
        {
            var bigram = words[i] + " " + words[i + 1];
            features.Add(FeatureHasher.Hash("w2", bigram));
        }

        // 6. Character 3-5-grams inside tokens (helps Persian morphology and Finglish)
        foreach (var word in words)
        {
            for (int len = 3; len <= 5 && len <= word.Length; len++)
            {
                for (int i = 0; i <= word.Length - len; i++)
                {
                    var ngram = word.Substring(i, len);
                    features.Add(FeatureHasher.Hash($"c{len}", ngram));
                }
            }
        }

        // 7. Tier 1 concept hits (concept IDs)
        foreach (var hit in conceptHits)
        {
            features.Add(FeatureHasher.Hash("concept", hit.ConceptId));
        }

        // 8. Entity types present
        foreach (var entity in entities)
        {
            features.Add(FeatureHasher.Hash("entity", entity.GetType().Name));
        }

        // 9. Emoji signal classes (simple heuristic)
        if (foldedText.Contains("🔥") || foldedText.Contains("⚡") || foldedText.Contains("💥"))
            features.Add(FeatureHasher.Hash("emoji", "urgent"));
        if (foldedText.Contains("💰") || foldedText.Contains("💵") || foldedText.Contains("💸"))
            features.Add(FeatureHasher.Hash("emoji", "money"));
        if (foldedText.Contains("🎁") || foldedText.Contains("🎉") || foldedText.Contains("🔥"))
            features.Add(FeatureHasher.Hash("emoji", "promo"));
        if (foldedText.Contains("⚠️") || foldedText.Contains("🚨") || foldedText.Contains("❗"))
            features.Add(FeatureHasher.Hash("emoji", "alert"));

        // 10. Script flags
        bool hasPersian = foldedText.Any(c => c >= 0x0600 && c <= 0x06FF);
        bool hasArabic = foldedText.Any(c => c >= 0x0750 && c <= 0x077F);
        bool hasLatin = foldedText.Any(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));
        bool hasDigits = foldedText.Any(char.IsDigit);
        
        if (hasPersian) features.Add(FeatureHasher.Hash("script", "fa"));
        if (hasArabic) features.Add(FeatureHasher.Hash("script", "ar"));
        if (hasLatin) features.Add(FeatureHasher.Hash("script", "latn"));
        if (hasDigits) features.Add(FeatureHasher.Hash("script", "digits"));

        // 11. Coarse length bucket
        int lenBucket = foldedText.Length switch
        {
            < 20 => 0,
            < 50 => 1,
            < 100 => 2,
            < 200 => 3,
            _ => 4
        };
        features.Add(FeatureHasher.Hash("len", lenBucket.ToString()));

        return features;
    }

    /// <summary>
    /// Compute logits for all categories given feature indices.
    /// </summary>
    private float[] ComputeLogits(List<(int index, int sign)> features)
    {
        float[] logits = new float[C];
        
        foreach (var (index, sign) in features)
        {
            int baseIdx = index * C;
            for (int c = 0; c < C; c++)
            {
                logits[c] += sign * _weights[baseIdx + c];
            }
        }
        
        return logits;
    }

    /// <summary>
    /// Update weights using AdaGrad for a single training example.
    /// </summary>
    private Task UpdateWeightsAsync(List<(int index, int sign)> features, Category targetCategory, float weight)
    {
        int targetIdx = CategoryEnumToIndex[targetCategory];
        
        // Compute logits and softmax
        float[] logits = ComputeLogits(features);
        
        // Softmax
        float maxLogit = logits[0];
        for (int c = 1; c < C; c++)
            if (logits[c] > maxLogit) maxLogit = logits[c];
        
        float sumExp = 0;
        for (int c = 0; c < C; c++)
            sumExp += MathF.Exp(logits[c] - maxLogit);
        
        float[] probs = new float[C];
        for (int c = 0; c < C; c++)
            probs[c] = MathF.Exp(logits[c] - maxLogit) / sumExp;

        // Gradient: (predicted - target) * weight
        // For target class: (probs[target] - 1) * weight
        // For others: probs[c] * weight
        foreach (var (index, sign) in features)
        {
            int baseIdx = index * C;
            for (int c = 0; c < C; c++)
            {
                float grad = (probs[c] - (c == targetIdx ? 1f : 0f)) * weight * sign;
                
                // Add L2 regularization gradient
                grad += L2Lambda * _weights[baseIdx + c];
                
                // AdaGrad update
                _gradSumSq[baseIdx + c] += grad * grad;
                float adaGradLr = 0.01f / MathF.Sqrt(_gradSumSq[baseIdx + c] + AdaGradEps);
                _weights[baseIdx + c] -= adaGradLr * grad;
            }
        }
        
        return Task.CompletedTask;
    }
}