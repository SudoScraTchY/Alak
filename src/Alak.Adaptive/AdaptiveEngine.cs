using Alak.Core.Classification;
using Alak.Core.Model;
using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.IO;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;

namespace Alak.Adaptive;

/// <summary>
/// Feature hashing utilities for the adaptive engine (zero-allocation span implementation).
/// </summary>
internal static class FeatureHasher
{
    private const uint D = 1 << 16; // 65536 feature slots
    private const uint Seed = 0x9E3779B9; // Golden ratio hash seed

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint HashUtf8Span(uint hash, ReadOnlySpan<char> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            char c = span[i];
            if (c < 0x80)
            {
                hash = (hash ^ (byte)c) * 0x1000001B;
            }
            else if (c < 0x800)
            {
                hash = (hash ^ (byte)(0xC0 | (c >> 6))) * 0x1000001B;
                hash = (hash ^ (byte)(0x80 | (c & 0x3F))) * 0x1000001B;
            }
            else if (char.IsHighSurrogate(c) && i + 1 < span.Length && char.IsLowSurrogate(span[i + 1]))
            {
                int codePoint = char.ConvertToUtf32(c, span[++i]);
                hash = (hash ^ (byte)(0xF0 | (codePoint >> 18))) * 0x1000001B;
                hash = (hash ^ (byte)(0x80 | ((codePoint >> 12) & 0x3F))) * 0x1000001B;
                hash = (hash ^ (byte)(0x80 | ((codePoint >> 6) & 0x3F))) * 0x1000001B;
                hash = (hash ^ (byte)(0x80 | (codePoint & 0x3F))) * 0x1000001B;
            }
            else if (char.IsSurrogate(c))
            {
                // Unpaired surrogate: UTF-8 replacement character U+FFFD (0xEF, 0xBF, 0xBD)
                hash = (hash ^ 0xEF) * 0x1000001B;
                hash = (hash ^ 0xBF) * 0x1000001B;
                hash = (hash ^ 0xBD) * 0x1000001B;
            }
            else
            {
                hash = (hash ^ (byte)(0xE0 | (c >> 12))) * 0x1000001B;
                hash = (hash ^ (byte)(0x80 | ((c >> 6) & 0x3F))) * 0x1000001B;
                hash = (hash ^ (byte)(0x80 | (c & 0x3F))) * 0x1000001B;
            }
        }
        return hash;
    }

    /// <summary>
    /// Hash a feature span to a signed index in [0, D-1] using signed hashing trick.
    /// Operates on ReadOnlySpan<char> directly without allocating byte[] arrays via Encoding.UTF8.GetBytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int index, int sign) Hash(ReadOnlySpan<char> feature)
    {
        uint hash = HashUtf8Span(Seed, feature);
        int index = (int)(hash & (D - 1));
        int sign = ((hash >> 16) & 1) == 1 ? 1 : -1;
        return (index, sign);
    }

    /// <summary>
    /// Hash a feature string to a signed index in [0, D-1].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int index, int sign) Hash(string feature) => Hash(feature.AsSpan());

    /// <summary>
    /// Hash a feature span with a namespace prefix without string concatenation or heap allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int index, int sign) Hash(ReadOnlySpan<char> ns, ReadOnlySpan<char> feature)
    {
        uint hash = HashUtf8Span(Seed, ns);
        hash = (hash ^ 0x3A) * 0x1000001B; // ':'
        hash = HashUtf8Span(hash, feature);
        int index = (int)(hash & (D - 1));
        int sign = ((hash >> 16) & 1) == 1 ? 1 : -1;
        return (index, sign);
    }

    /// <summary>
    /// Hash a feature string with a namespace prefix without string concatenation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int index, int sign) Hash(string ns, string feature) => Hash(ns.AsSpan(), feature.AsSpan());

    /// <summary>
    /// Hash a bigram (w1 + " " + w2) with namespace "w2" with zero allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int index, int sign) HashBigram(ReadOnlySpan<char> w1, ReadOnlySpan<char> w2)
    {
        uint hash = HashUtf8Span(Seed, "w2".AsSpan());
        hash = (hash ^ 0x3A) * 0x1000001B; // ':'
        hash = HashUtf8Span(hash, w1);
        hash = (hash ^ (byte)' ') * 0x1000001B;
        hash = HashUtf8Span(hash, w2);
        int index = (int)(hash & (D - 1));
        int sign = ((hash >> 16) & 1) == 1 ? 1 : -1;
        return (index, sign);
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

    public AdaptiveEngine(string? modelPath = null)
    {
        _weights = new float[D * C];
        _gradSumSq = new float[D * C];
        _modelPath = modelPath ?? GetDefaultModelPath();
        _isLoaded = false;
    }

    private static string GetDefaultModelPath()
    {
        try
        {
            return Path.Combine(FileSystem.AppDataDirectory, ModelFileName);
        }
        catch
        {
            return Path.Combine(AppContext.BaseDirectory, ModelFileName);
        }
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

    public async Task LearnFromCorrectionAsync(string package, string channel, string foldedText, IReadOnlyList<ConceptHit> conceptHits, IReadOnlyList<Entity> entities, Category correctedCategory, string? androidCategory = null, bool autoSave = false)
    {
        if (!_isLoaded) await LoadAsync();
        
        var features = ExtractFeatures(package, channel, foldedText, conceptHits, entities, androidCategory);
        await UpdateWeightsAsync(features, correctedCategory, CorrectionWeight);
        _correctionCount++;
        _totalUpdates++;
        
        // Save periodically if autoSave enabled
        if (autoSave && _correctionCount % 5 == 0)
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
        var dir = Path.GetDirectoryName(_modelPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = _modelPath + ".tmp";
        bool success = false;
        try
        {
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(fs))
            {
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

                writer.Flush();
                await fs.FlushAsync();
            }

            if (File.Exists(_modelPath))
                File.Replace(tempPath, _modelPath, null);
            else
                File.Move(tempPath, _modelPath);

            success = true;
        }
        finally
        {
            if (!success && File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    public Task LoadWeightsAsync() => LoadAsync();

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
        catch (Exception ex)
        {
            // Corrupted model - reset gracefully with an audit log (WBP-03)
            System.Diagnostics.Debug.WriteLine($"[AdaptiveEngine] Model corruption detected ({ex.Message}). Resetting weights gracefully.");
            Array.Clear(_weights, 0, _weights.Length);
            Array.Clear(_gradSumSq, 0, _gradSumSq.Length);
            _correctionCount = 0;
            _pseudoLabelCount = 0;
            _totalUpdates = 0;
            _isLoaded = true; // Still mark as loaded (zero weights)

            try
            {
                if (File.Exists(_modelPath))
                    File.Delete(_modelPath);
            }
            catch { }
        }
    }

    /// <summary>
    /// Blend Tier 1 and Tier 2 decisions, strictly maintaining security invariance (Category.Security never overridden).
    /// </summary>
    public Category BlendDecision(Category tier1Category, float tier1Confidence, float[]? tier2Logits)
    {
        // SPEC §9.1: Tier 2 can never move an item OUT OF security
        if (tier1Category == Category.Security)
            return Category.Security;

        if (tier2Logits == null || tier2Logits.Length != C || _correctionCount == 0)
            return tier1Category;

        float maxLogit = float.MinValue;
        int bestIdx = -1;
        float beta = Beta;

        for (int i = 0; i < C; i++)
        {
            var cat = CategoryIndexToEnum[i];
            float tier1Logit = cat == tier1Category ? tier1Confidence * 4.0f : 0f;
            float blended = tier1Logit + (beta * tier2Logits[i]);
            if (blended > maxLogit)
            {
                maxLogit = blended;
                bestIdx = i;
            }
        }

        if (bestIdx >= 0)
        {
            return CategoryIndexToEnum[bestIdx];
        }

        return tier1Category;
    }

    /// <summary>
    /// Extract features from notification data (spec §9.1 feature list).
    /// Returns list of (featureIndex, sign) pairs with zero heap allocations for tokens/ngrams.
    /// </summary>
    private static List<(int index, int sign)> ExtractFeatures(string package, string channel, string foldedText, IReadOnlyList<ConceptHit> conceptHits, IReadOnlyList<Entity> entities, string? androidCategory)
    {
        var features = new List<(int index, int sign)>(64);

        // 1. Package ID (high signal)
        if (!string.IsNullOrEmpty(package))
            features.Add(FeatureHasher.Hash("pkg".AsSpan(), package.AsSpan()));

        // 2. Channel ID
        if (!string.IsNullOrEmpty(channel))
            features.Add(FeatureHasher.Hash("ch".AsSpan(), channel.AsSpan()));

        // 3. Android category
        if (!string.IsNullOrEmpty(androidCategory))
            features.Add(FeatureHasher.Hash("acat".AsSpan(), androidCategory.AsSpan()));

        // 4. Word unigrams, bigrams, and char 3-5-grams with zero string allocations and no word count limit
        var textSpan = foldedText.AsSpan();
        ReadOnlySpan<char> prevWord = default;

        foreach (var wordRange in textSpan.Split(' '))
        {
            var word = textSpan[wordRange].Trim();
            if (word.IsEmpty) continue;

            // Unigrams
            if (word.Length >= 2)
                features.Add(FeatureHasher.Hash("w1".AsSpan(), word));

            // Bigrams
            if (!prevWord.IsEmpty)
                features.Add(FeatureHasher.HashBigram(prevWord, word));

            // Character 3-5-grams inside tokens
            for (int len = 3; len <= 5 && len <= word.Length; len++)
            {
                var ns = len switch { 3 => "c3".AsSpan(), 4 => "c4".AsSpan(), _ => "c5".AsSpan() };
                for (int i = 0; i <= word.Length - len; i++)
                {
                    var ngram = word.Slice(i, len);
                    features.Add(FeatureHasher.Hash(ns, ngram));
                }
            }

            prevWord = word;
        }

        // 7. Tier 1 concept hits (concept IDs)
        foreach (var hit in conceptHits)
        {
            features.Add(FeatureHasher.Hash("concept".AsSpan(), hit.ConceptId.AsSpan()));
        }

        // 8. Entity types present
        foreach (var entity in entities)
        {
            features.Add(FeatureHasher.Hash("entity".AsSpan(), entity.GetType().Name.AsSpan()));
        }

        // 9. Emoji signal classes
        if (foldedText.Contains("🔥") || foldedText.Contains("⚡") || foldedText.Contains("💥"))
            features.Add(FeatureHasher.Hash("emoji".AsSpan(), "urgent".AsSpan()));
        if (foldedText.Contains("💰") || foldedText.Contains("💵") || foldedText.Contains("💸"))
            features.Add(FeatureHasher.Hash("emoji".AsSpan(), "money".AsSpan()));
        if (foldedText.Contains("🎁") || foldedText.Contains("🎉"))
            features.Add(FeatureHasher.Hash("emoji".AsSpan(), "promo".AsSpan()));
        if (foldedText.Contains("⚠️") || foldedText.Contains("🚨") || foldedText.Contains("❗"))
            features.Add(FeatureHasher.Hash("emoji".AsSpan(), "alert".AsSpan()));

        // 10. Script flags
        bool hasPersian = false, hasArabic = false, hasLatin = false, hasDigits = false;
        foreach (char c in textSpan)
        {
            if (c >= 0x0600 && c <= 0x06FF) hasPersian = true;
            else if (c >= 0x0750 && c <= 0x077F) hasArabic = true;
            else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) hasLatin = true;
            else if (char.IsDigit(c)) hasDigits = true;
        }

        if (hasPersian) features.Add(FeatureHasher.Hash("script".AsSpan(), "fa".AsSpan()));
        if (hasArabic) features.Add(FeatureHasher.Hash("script".AsSpan(), "ar".AsSpan()));
        if (hasLatin) features.Add(FeatureHasher.Hash("script".AsSpan(), "latn".AsSpan()));
        if (hasDigits) features.Add(FeatureHasher.Hash("script".AsSpan(), "digits".AsSpan()));

        // 11. Coarse length bucket
        var lenStr = textSpan.Length switch
        {
            < 20 => "0".AsSpan(),
            < 50 => "1".AsSpan(),
            < 100 => "2".AsSpan(),
            < 200 => "3".AsSpan(),
            _ => "4".AsSpan()
        };
        features.Add(FeatureHasher.Hash("len".AsSpan(), lenStr));

        return features;
    }

    /// <summary>
    /// Compute logits for all categories given feature indices using SIMD / Vector128 in-register accumulation.
    /// </summary>
    private float[] ComputeLogits(List<(int index, int sign)> features)
    {
        float[] logits = new float[C];
        if (Vector128.IsHardwareAccelerated)
        {
            var log0 = Vector128<float>.Zero;
            var log1 = Vector128<float>.Zero;
            float log8 = 0f;

            foreach (var (index, sign) in features)
            {
                int baseIdx = index * C;
                var w0 = Vector128.Create(_weights.AsSpan(baseIdx, 4));
                var w1 = Vector128.Create(_weights.AsSpan(baseIdx + 4, 4));
                float w8 = _weights[baseIdx + 8];

                if (sign == 1)
                {
                    log0 += w0;
                    log1 += w1;
                    log8 += w8;
                }
                else
                {
                    log0 -= w0;
                    log1 -= w1;
                    log8 -= w8;
                }
            }

            log0.CopyTo(logits.AsSpan(0, 4));
            log1.CopyTo(logits.AsSpan(4, 4));
            logits[8] = log8;
        }
        else
        {
            Span<float> logitsSpan = logits.AsSpan();
            foreach (var (index, sign) in features)
            {
                var weightSlice = _weights.AsSpan(index * C, C);
                if (sign == 1)
                {
                    TensorPrimitives.Add(logitsSpan, weightSlice, logitsSpan);
                }
                else
                {
                    TensorPrimitives.Subtract(logitsSpan, weightSlice, logitsSpan);
                }
            }
        }

        return logits;
    }

    /// <summary>
    /// Update weights using AdaGrad for a single training example with vectorized SoftMax and Vector128 SIMD.
    /// </summary>
    private Task UpdateWeightsAsync(List<(int index, int sign)> features, Category targetCategory, float weight)
    {
        int targetIdx = CategoryEnumToIndex[targetCategory];
        
        // Compute logits and vectorized softmax
        float[] logits = ComputeLogits(features);
        float[] probs = new float[C];
        TensorPrimitives.SoftMax(logits, probs);

        // Precompute base gradient: (probs[c] - target_indicator) * weight
        // Invariant across all features for this training sample, avoiding redundant calculations.
        Span<float> baseGrad = stackalloc float[C];
        for (int c = 0; c < C; c++)
        {
            baseGrad[c] = (probs[c] - (c == targetIdx ? 1f : 0f)) * weight;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            var l2Vec = Vector128.Create(L2Lambda);
            var epsVec = Vector128.Create(AdaGradEps);
            var lrNumVec = Vector128.Create(0.01f);

            var bgVec0 = Vector128.Create(baseGrad.Slice(0, 4));
            var bgVec1 = Vector128.Create(baseGrad.Slice(4, 4));
            float bgRem = baseGrad[8];

            var negBgVec0 = -bgVec0;
            var negBgVec1 = -bgVec1;
            float negBgRem = -bgRem;

            foreach (var (index, sign) in features)
            {
                int baseIdx = index * C;

                // Lanes 0..3 (ARM NEON / SSE/AVX 128-bit)
                var effBg0 = sign == 1 ? bgVec0 : negBgVec0;
                var w0 = Vector128.Create(_weights.AsSpan(baseIdx, 4));
                var grad0 = effBg0 + l2Vec * w0;
                var gSq0 = Vector128.Create(_gradSumSq.AsSpan(baseIdx, 4)) + grad0 * grad0;
                gSq0.CopyTo(_gradSumSq.AsSpan(baseIdx, 4));
                var lr0 = lrNumVec / Vector128.Sqrt(gSq0 + epsVec);
                (w0 - lr0 * grad0).CopyTo(_weights.AsSpan(baseIdx, 4));

                // Lanes 4..7 (ARM NEON / SSE/AVX 128-bit)
                var effBg1 = sign == 1 ? bgVec1 : negBgVec1;
                var w1 = Vector128.Create(_weights.AsSpan(baseIdx + 4, 4));
                var grad1 = effBg1 + l2Vec * w1;
                var gSq1 = Vector128.Create(_gradSumSq.AsSpan(baseIdx + 4, 4)) + grad1 * grad1;
                gSq1.CopyTo(_gradSumSq.AsSpan(baseIdx + 4));
                var lr1 = lrNumVec / Vector128.Sqrt(gSq1 + epsVec);
                (w1 - lr1 * grad1).CopyTo(_weights.AsSpan(baseIdx + 4, 4));

                // Remainder lane 8 (scalar)
                float effBg8 = sign == 1 ? bgRem : negBgRem;
                float grad8 = effBg8 + L2Lambda * _weights[baseIdx + 8];
                _gradSumSq[baseIdx + 8] += grad8 * grad8;
                float lr8 = 0.01f / MathF.Sqrt(_gradSumSq[baseIdx + 8] + AdaGradEps);
                _weights[baseIdx + 8] -= lr8 * grad8;
            }
        }
        else
        {
            // Scalar fallback
            foreach (var (index, sign) in features)
            {
                int baseIdx = index * C;
                for (int c = 0; c < C; c++)
                {
                    float effBg = sign == 1 ? baseGrad[c] : -baseGrad[c];
                    float grad = effBg + L2Lambda * _weights[baseIdx + c];
                    _gradSumSq[baseIdx + c] += grad * grad;
                    float adaGradLr = 0.01f / MathF.Sqrt(_gradSumSq[baseIdx + c] + AdaGradEps);
                    _weights[baseIdx + c] -= adaGradLr * grad;
                }
            }
        }
        
        return Task.CompletedTask;
    }
}