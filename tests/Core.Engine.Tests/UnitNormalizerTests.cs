using Core.Engine.Services;

namespace Core.Engine.Tests;

public class UnitNormalizerTests
{
    private readonly UnitNormalizer _normalizer;

    public UnitNormalizerTests()
    {
        // Create test units.yaml
        var testYaml = @"
aliases:
  - canonical: ""бр""
    variants:
      - ""бр.""
      - ""брой""
      - ""pcs""
  - canonical: ""м2""
    variants:
      - ""m2""
      - ""кв.м""
      - ""кв. м.""
";
        var tempPath = Path.GetTempFileName();
        File.WriteAllText(tempPath, testYaml);
        _normalizer = new UnitNormalizer(tempPath);
        File.Delete(tempPath);
    }

    [Theory]
    [InlineData("бр", "бр")]
    [InlineData("бр.", "бр")]
    [InlineData("брой", "бр")]
    [InlineData("pcs", "бр")]
    [InlineData("м2", "м2")]
    [InlineData("кв.м", "м2")]
    [InlineData("кв. м.", "м2")]
    public void Normalize_ShouldMapToCanonical(string input, string expected)
    {
        var result = _normalizer.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("бр", "брой", true)]
    [InlineData("м2", "кв.м", true)]
    [InlineData("бр", "м2", false)]
    public void AreEquivalent_ShouldCheckEquivalence(string unit1, string unit2, bool expected)
    {
        var result = _normalizer.AreEquivalent(unit1, unit2);
        Assert.Equal(expected, result);
    }
}
