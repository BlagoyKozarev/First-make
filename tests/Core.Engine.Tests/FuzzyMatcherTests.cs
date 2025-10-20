using Core.Engine.Models;
using Core.Engine.Services;

namespace Core.Engine.Tests;

public class FuzzyMatcherTests
{
    private readonly FuzzyMatcher _matcher;
    private readonly List<PriceBaseEntry> _priceBase;

    public FuzzyMatcherTests()
    {
        // Create test normalizer
        var testYaml = @"
aliases:
  - canonical: ""м3""
    variants:
      - ""m3""
      - ""куб.м""
";
        var tempPath = Path.GetTempFileName();
        File.WriteAllText(tempPath, testYaml);
        var normalizer = new UnitNormalizer(tempPath);
        File.Delete(tempPath);

        _matcher = new FuzzyMatcher(normalizer);

        _priceBase = new List<PriceBaseEntry>
        {
            new() { Name = "Изкопни работи - механизирани", Unit = "м3", BasePrice = 45.50m },
            new() { Name = "Изкопни работи - ръчни", Unit = "м3", BasePrice = 85.00m, Aliases = new List<string> { "ръчен изкоп" } },
            new() { Name = "Бетон C25/30", Unit = "м3", BasePrice = 120.00m },
            new() { Name = "Тухла керамична", Unit = "бр", BasePrice = 0.75m }
        };
    }

    [Fact]
    public void FindCandidates_ShouldMatchByUnitFirst()
    {
        var item = new ItemDto
        {
            Stage = "S1",
            Name = "Изкопи механизирани",
            Unit = "м3",
            Qty = 100
        };

        var candidates = _matcher.FindCandidates(item, _priceBase, topN: 3);

        // Should only return м3 items, not бр items
        Assert.All(candidates, c => Assert.Equal("м3", c.Entry.Unit));
        Assert.True(candidates.Count <= 3);
    }

    [Fact]
    public void FindCandidates_ShouldRankByScore()
    {
        var item = new ItemDto
        {
            Stage = "S1",
            Name = "механизирани изкопни работи", // Word order different
            Unit = "м3",
            Qty = 50
        };

        var candidates = _matcher.FindCandidates(item, _priceBase, topN: 2);

        Assert.NotEmpty(candidates);
        // First candidate should be "Изкопни работи - механизирани"
        Assert.Contains("механизирани", candidates[0].Entry.Name.ToLowerInvariant());
    }

    [Fact]
    public void FindCandidates_ShouldMatchAliases()
    {
        var item = new ItemDto
        {
            Stage = "S1",
            Name = "ръчен изкоп",
            Unit = "м3",
            Qty = 25
        };

        var candidates = _matcher.FindCandidates(item, _priceBase, topN: 1);

        Assert.NotEmpty(candidates);
        Assert.Contains("ръчни", candidates[0].Entry.Name);
        Assert.True(candidates[0].Score >= 0.6);
    }
}
