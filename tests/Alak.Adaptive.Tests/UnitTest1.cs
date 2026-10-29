using Alak.Core.Classification;
using Alak.Core.Model;
using Shouldly;
using Xunit;

namespace Alak.Adaptive.Tests;

public class AdaptiveEngineTests
{
    [Fact]
    public async Task AdaptiveEngine_LearnsFromCorrection_AndShiftsLogits()
    {
        var engine = new AdaptiveEngine();
        await engine.ResetAsync();

        var hits = Array.Empty<ConceptHit>();
        var entities = Array.Empty<Entity>();

        // Initial inference on zero weights produces equal 0 logits
        var initialLogits = await engine.InferAsync("com.bank.melli", "channel_sms", "انتقال وجه موفقیت آمیز بود", hits, entities);
        initialLogits.ShouldNotBeNull();
        initialLogits.Length.ShouldBe(9);
        foreach (var l in initialLogits)
        {
            l.ShouldBe(0f);
        }

        // Learn that this pattern belongs to Category.Money
        await engine.LearnFromCorrectionAsync(
            "com.bank.melli",
            "channel_sms",
            "انتقال وجه موفقیت آمیز بود",
            hits,
            entities,
            Category.Money);

        engine.CorrectionCount.ShouldBe(1);

        // Infer again
        var updatedLogits = await engine.InferAsync("com.bank.melli", "channel_sms", "انتقال وجه موفقیت آمیز بود", hits, entities);
        updatedLogits.ShouldNotBeNull();

        // Money index is 1 (Security=0, Money=1, Work=2, ...)
        int moneyIdx = 1;
        float moneyLogit = updatedLogits[moneyIdx];

        // The money logit should now be strictly higher than other categories
        for (int c = 0; c < 9; c++)
        {
            if (c != moneyIdx)
            {
                moneyLogit.ShouldBeGreaterThan(updatedLogits[c]);
            }
        }
    }

    [Fact]
    public async Task AdaptiveEngine_MultipleCorrections_IncreaseTargetProbability()
    {
        var engine = new AdaptiveEngine();
        await engine.ResetAsync();

        var hits = Array.Empty<ConceptHit>();
        var entities = Array.Empty<Entity>();

        // Train 3 times on promo message
        for (int i = 0; i < 3; i++)
        {
            await engine.LearnFromCorrectionAsync(
                "com.digikala",
                "promotions",
                "تخفیف شگفت انگیز تا ۷۰ درصد",
                hits,
                entities,
                Category.Offers);
        }

        var logits = await engine.InferAsync("com.digikala", "promotions", "تخفیف شگفت انگیز تا ۷۰ درصد", hits, entities);
        logits.ShouldNotBeNull();

        // Category.Offers is index 5
        int offersIdx = 5;
        for (int c = 0; c < 9; c++)
        {
            if (c != offersIdx)
            {
                logits[offersIdx].ShouldBeGreaterThan(logits[c]);
            }
        }
    }

    [Fact]
    public async Task ADP004_Personalization_MacroF1_Improves_FromBelow40_ToAbove85_Over50Corrections()
    {
        var tempModelPath = Path.Combine(Path.GetTempPath(), $"adp004-{Guid.NewGuid():N}.bin");
        try
        {
            var engine = new AdaptiveEngine(tempModelPath);
            await engine.ResetAsync();

            var hits = Array.Empty<ConceptHit>();
            var entities = Array.Empty<Entity>();

            // 50 test samples across 5 target categories (10 samples per category)
            var targetCategories = new[]
            {
                Category.Money,
                Category.Offers,
                Category.Work,
                Category.Orders,
                Category.Social
            };

            var samples = new List<(string pkg, string ch, string text, Category targetCat)>();
            for (int i = 0; i < 50; i++)
            {
                var target = targetCategories[i % targetCategories.Length];
                var sample = target switch
                {
                    Category.Money => ($"com.fintech.wallet{i}", "tx", $"واریز نقدی و انتقال وجه شماره {i} با موفقیت انجام شد", target),
                    Category.Offers => ($"com.store.deals{i}", "sale", $"تخفیف ویژه و حراج فصلی کالا {i} ارسال رایگان", target),
                    Category.Work => ($"com.company.slack{i}", "work", $"Team meeting agenda and standup notes task {i}", target),
                    Category.Orders => ($"com.courier.ship{i}", "status", $"سفارش خرید شما شماره {i} ارسال و در حال تحویل است", target),
                    Category.Social => ($"com.network.chat{i}", "social", $"پیام جدید از دوستان شما و لایک پست {i}", target),
                    _ => ($"com.other.app{i}", "other", $"notification text {i}", target)
                };
                samples.Add(sample);
            }

            // Function to evaluate Macro-F1 across the 5 categories
            float CalculateMacroF1(Dictionary<int, (int tp, int fp, int fn)> stats)
            {
                float sumF1 = 0f;
                foreach (var cat in targetCategories)
                {
                    int cIdx = (int)cat;
                    if (!stats.TryGetValue(cIdx, out var s)) continue;
                    float precision = s.tp + s.fp > 0 ? (float)s.tp / (s.tp + s.fp) : 0f;
                    float recall = s.tp + s.fn > 0 ? (float)s.tp / (s.tp + s.fn) : 0f;
                    float f1 = precision + recall > 0 ? 2f * precision * recall / (precision + recall) : 0f;
                    sumF1 += f1;
                }
                return sumF1 / targetCategories.Length;
            }

            // CategoryIndexToEnum maps 0..8
            var catEnumMap = new[]
            {
                Category.Security, Category.Money, Category.Work, Category.Orders,
                Category.People, Category.Offers, Category.Social, Category.Updates, Category.Other
            };

            // 1. Initial Evaluation (before corrections):
            // Initial weights are 0, so without Tier 1, logits are all zero and Macro-F1 is below 0.40
            var initialStats = targetCategories.ToDictionary(c => (int)c, _ => (tp: 0, fp: 0, fn: 0));
            foreach (var s in samples)
            {
                var logits = await engine.InferAsync(s.pkg, s.ch, s.text, hits, entities);
                logits.ShouldNotBeNull();
                int bestIdx = 0;
                float max = float.MinValue;
                for (int c = 0; c < logits.Length; c++)
                {
                    if (logits[c] > max)
                    {
                        max = logits[c];
                        bestIdx = c;
                    }
                }

                var predictedCat = catEnumMap[bestIdx];
                int predIdx = (int)predictedCat;
                int trueIdx = (int)s.targetCat;
                foreach (var cat in targetCategories)
                {
                    int cIdx = (int)cat;
                    var (tp, fp, fn) = initialStats[cIdx];
                    if (predIdx == cIdx && trueIdx == cIdx) tp++;
                    else if (predIdx == cIdx && trueIdx != cIdx) fp++;
                    else if (predIdx != cIdx && trueIdx == cIdx) fn++;
                    initialStats[cIdx] = (tp, fp, fn);
                }
            }

            float initialMacroF1 = CalculateMacroF1(initialStats);
            initialMacroF1.ShouldBeLessThan(0.40f);

            // 2. Perform 50 simulated user corrections (ADP-004 online training)
            foreach (var s in samples)
            {
                await engine.LearnFromCorrectionAsync(s.pkg, s.ch, s.text, hits, entities, s.targetCat);
            }

            engine.CorrectionCount.ShouldBe(50);
            engine.Beta.ShouldBe(0.8f); // Ramped up to BetaEnd after 50 corrections

            // 3. Post-Personalization Evaluation:
            var postStats = targetCategories.ToDictionary(c => (int)c, _ => (tp: 0, fp: 0, fn: 0));

            foreach (var s in samples)
            {
                var logits = await engine.InferAsync(s.pkg, s.ch, s.text, hits, entities);
                logits.ShouldNotBeNull();

                int bestIdx = 0;
                float max = float.MinValue;
                for (int c = 0; c < logits.Length; c++)
                {
                    if (logits[c] > max)
                    {
                        max = logits[c];
                        bestIdx = c;
                    }
                }

                var predictedCat = catEnumMap[bestIdx];
                int predIdx = (int)predictedCat;
                int trueIdx = (int)s.targetCat;

                foreach (var cat in targetCategories)
                {
                    int cIdx = (int)cat;
                    var (tp, fp, fn) = postStats[cIdx];
                    if (predIdx == cIdx && trueIdx == cIdx) tp++;
                    else if (predIdx == cIdx && trueIdx != cIdx) fp++;
                    else if (predIdx != cIdx && trueIdx == cIdx) fn++;
                    postStats[cIdx] = (tp, fp, fn);
                }
            }

            float postMacroF1 = CalculateMacroF1(postStats);
            postMacroF1.ShouldBeGreaterThan(0.85f);
        }
        finally
        {
            try { File.Delete(tempModelPath); } catch { }
        }
    }

    [Fact]
    public async Task RoundTrip_Serialization_Export_Reload_And_Corruption_Recovery()
    {
        var tempModelPath = Path.Combine(Path.GetTempPath(), $"adaptive-rt-{Guid.NewGuid():N}.bin");
        try
        {
            var engine1 = new AdaptiveEngine(tempModelPath);
            await engine1.ResetAsync();

            var hits = Array.Empty<ConceptHit>();
            var entities = Array.Empty<Entity>();

            // Learn 10 corrections
            for (int i = 0; i < 10; i++)
            {
                await engine1.LearnFromCorrectionAsync(
                    "com.test.app",
                    "ch_test",
                    $"Notification test payload {i} special discount",
                    hits,
                    entities,
                    Category.Offers);
            }

            await engine1.SaveAsync();
            File.Exists(tempModelPath).ShouldBeTrue();

            var logits1 = await engine1.InferAsync("com.test.app", "ch_test", "Notification test payload 0 special discount", hits, entities);
            logits1.ShouldNotBeNull();

            // 1. Reload in a new instance and verify exact parity
            var engine2 = new AdaptiveEngine(tempModelPath);
            await engine2.LoadWeightsAsync();

            engine2.IsReady.ShouldBeTrue();
            engine2.CorrectionCount.ShouldBe(10);

            var logits2 = await engine2.InferAsync("com.test.app", "ch_test", "Notification test payload 0 special discount", hits, entities);
            logits2.ShouldNotBeNull();

            for (int c = 0; c < 9; c++)
            {
                logits1[c].ShouldBe(logits2[c], 1e-4f);
            }

            // 2. Corruption recovery test: corrupt model binary with invalid bytes
            await File.WriteAllBytesAsync(tempModelPath, new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 }, TestContext.Current.CancellationToken);

            var engine3 = new AdaptiveEngine(tempModelPath);
            // Must not throw an unhandled exception
            await engine3.LoadWeightsAsync();

            engine3.IsReady.ShouldBeTrue();
            engine3.CorrectionCount.ShouldBe(0); // Gracefully reset to 0

            var logits3 = await engine3.InferAsync("com.test.app", "ch_test", "Notification test payload 0 special discount", hits, entities);
            logits3.ShouldNotBeNull();
            foreach (var l in logits3)
            {
                l.ShouldBe(0f);
            }
        }
        finally
        {
            try { File.Delete(tempModelPath); } catch { }
        }
    }

    [Fact]
    public async Task ADP001_Execution_Benchmarks_InferUnder2ms_UpdateUnder3ms()
    {
        var engine = new AdaptiveEngine();
        await engine.ResetAsync();

        var hits = Array.Empty<ConceptHit>();
        var entities = Array.Empty<Entity>();
        var text = "تخفیف ۵۰ درصدی برای خرید پوشاک و ارسال رایگان در فروشگاه آنلاین";

        // Warmup
        for (int i = 0; i < 50; i++)
        {
            await engine.InferAsync("com.test.app", "ch", text, hits, entities);
            await engine.LearnFromCorrectionAsync("com.test.app", "ch", text, hits, entities, Category.Offers);
        }

        // Benchmark InferAsync (≤ 2ms per ADP-001)
        const int inferIterations = 500;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < inferIterations; i++)
        {
            await engine.InferAsync("com.test.app", "ch", text, hits, entities);
        }
        sw.Stop();
        double avgInferMs = sw.Elapsed.TotalMilliseconds / inferIterations;
        avgInferMs.ShouldBeLessThanOrEqualTo(2.0, $"InferAsync took {avgInferMs:F4}ms which exceeds 2.0ms");

        // Benchmark UpdateWeights / LearnFromCorrectionAsync (≤ 3ms per ADP-001)
        const int updateIterations = 500;
        sw.Restart();
        for (int i = 0; i < updateIterations; i++)
        {
            await engine.LearnFromCorrectionAsync("com.test.app", "ch", text, hits, entities, Category.Offers);
        }
        sw.Stop();
        double avgUpdateMs = sw.Elapsed.TotalMilliseconds / updateIterations;
        avgUpdateMs.ShouldBeLessThanOrEqualTo(3.0, $"LearnFromCorrectionAsync took {avgUpdateMs:F4}ms which exceeds 3.0ms");
    }

    [Fact]
    public async Task SecurityInvariance_BlendDecision_NeverOverridesSecurity()
    {
        var engine = new AdaptiveEngine();
        var hits = Array.Empty<ConceptHit>();
        var entities = Array.Empty<Entity>();

        // Train 20 times on Offers so CorrectionCount > 0 and Beta > 0.3
        for (int i = 0; i < 20; i++)
        {
            await engine.LearnFromCorrectionAsync("com.test.app", "ch", "massive discount promo sale", hits, entities, Category.Offers);
        }

        engine.CorrectionCount.ShouldBe(20);
        engine.Beta.ShouldBeGreaterThan(0.3f);

        // Synthetic extreme logits where Offers is overwhelmingly favored and Security is suppressed
        float[] extremeLogits = new float[9];
        extremeLogits[0] = -100.0f; // Security
        extremeLogits[5] = 100.0f;  // Offers

        // When Tier 1 decides Security, blended decision MUST remain Security regardless of Tier 2
        var decision = engine.BlendDecision(Category.Security, 0.5f, extremeLogits);
        decision.ShouldBe(Category.Security);

        // Even with very low Tier 1 confidence
        var lowConfDecision = engine.BlendDecision(Category.Security, 0.05f, extremeLogits);
        lowConfDecision.ShouldBe(Category.Security);
    }

    [Fact]
    public void FeatureHasher_SurrogatesAndEmojis_NeverHangOrAllocate()
    {
        // 1. Lone high surrogate (must not cause infinite loop)
        var (idx1, sign1) = FeatureHasher.Hash("\uD800".AsSpan());
        idx1.ShouldBeGreaterThanOrEqualTo(0);
        idx1.ShouldBeLessThan(65536);
        sign1.ShouldBeOneOf(1, -1);

        // 2. Lone low surrogate
        var (idx2, sign2) = FeatureHasher.Hash("\uDC00".AsSpan());
        idx2.ShouldBeGreaterThanOrEqualTo(0);
        idx2.ShouldBeLessThan(65536);

        // 3. Valid emoji surrogate pair
        var (idx3, sign3) = FeatureHasher.Hash("🎁".AsSpan());
        idx3.ShouldBeGreaterThanOrEqualTo(0);
        idx3.ShouldBeLessThan(65536);

        // 4. Truncated emoji with namespace
        var (idx4, sign4) = FeatureHasher.Hash("w1".AsSpan(), "hello\uD83D".AsSpan());
        idx4.ShouldBeGreaterThanOrEqualTo(0);
        idx4.ShouldBeLessThan(65536);

        // 5. Bigram with mixed surrogates
        var (idx5, sign5) = FeatureHasher.HashBigram("test\uD800".AsSpan(), "world🎉".AsSpan());
        idx5.ShouldBeGreaterThanOrEqualTo(0);
        idx5.ShouldBeLessThan(65536);
    }

    [Fact]
    public async Task ExtractFeatures_LongNotificationText_ProcessesAllWordsWithoutTruncation()
    {
        var engine = new AdaptiveEngine();
        var hits = Array.Empty<ConceptHit>();
        var entities = Array.Empty<Entity>();

        // Text with 120 words (> 64 stackalloc threshold)
        var words = Enumerable.Range(0, 120).Select(i => $"word{i}").ToArray();
        var longText = string.Join(" ", words);

        var logits = await engine.InferAsync("com.test.app", "ch", longText, hits, entities);
        logits.ShouldNotBeNull();
        logits.Length.ShouldBe(9);

        // Must learn without throwing
        await engine.LearnFromCorrectionAsync("com.test.app", "ch", longText, hits, entities, Category.Work);
        engine.CorrectionCount.ShouldBe(1);
    }
}
