using System.Text.Json;
using Alak.Core.Model;
using Alak.Rules;
using Shouldly;
using Xunit;

namespace Alak.Rules.Tests;

/// <summary>
/// Unit tests for RUL-019 / RUL-009 / RUL-010 calibrated weights.json.
/// </summary>
public class WeightsConfigTests
{
    [Fact]
    public void WeightsConfig_LoadsFromEmbeddedOrDisk()
    {
        var config = WeightsConfig.Default;
        config.ShouldNotBeNull();
        config.Version.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void WeightsConfig_HasAllNineCategoriesInBias()
    {
        var config = WeightsConfig.Default;
        config.CategoryBias.ShouldNotBeNull();

        foreach (var category in Enum.GetValues<Category>())
        {
            config.CategoryBias.ContainsKey(category.ToString()).ShouldBeTrue(
                $"Missing calibrated bias weight for category: {category}");
            config.CategoryBiasVector[(int)category].ShouldBe(config.CategoryBias[category.ToString()]);
        }
        config.CategoryBiasVector.Length.ShouldBe(9);
    }

    [Fact]
    public void WeightsConfig_HasValidSoftmaxParameters()
    {
        var config = WeightsConfig.Default;
        config.Softmax.ShouldNotBeNull();
        config.Softmax.Temperature.ShouldBeGreaterThan(0f);
        config.Softmax.ConfidenceThreshold.ShouldBeGreaterThan(0f);
        config.Softmax.ReviewConfidenceThreshold.ShouldBeGreaterThan(config.Softmax.ConfidenceThreshold);
        config.Softmax.ReviewMarginThreshold.ShouldBeGreaterThan(0f);
    }

    [Fact]
    public void WeightsConfig_HasValidFamilyCaps()
    {
        var config = WeightsConfig.Default;
        config.FamilyCaps.ShouldNotBeNull();
        config.FamilyCaps.ShouldContainKey("lexicon");
        config.FamilyCaps["lexicon"].ShouldBe(3.0f);
        config.FamilyCaps.ShouldContainKey("entity");
        config.FamilyCaps["entity"].ShouldBe(6.0f);
    }

    [Fact]
    public void WeightsConfig_HasMetadataAndEntityEvidenceWeights()
    {
        var config = WeightsConfig.Default;
        config.MetadataEvidence.ShouldNotBeNull();
        config.MetadataEvidence.ShouldContainKey("promo");
        config.MetadataEvidence["promo"].ShouldContainKey("Offers");

        config.EntityEvidence.ShouldNotBeNull();
        config.EntityEvidence.ShouldContainKey("otp");
        config.EntityEvidence["otp"]["Security"].ShouldBe(6.0f);
    }
}
