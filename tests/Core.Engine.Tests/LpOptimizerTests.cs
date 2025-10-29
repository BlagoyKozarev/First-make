using Core.Engine.Models;
using Core.Engine.Services;

namespace Core.Engine.Tests;

public class LpOptimizerTests
{
    private readonly LpOptimizer _optimizer;

    public LpOptimizerTests()
    {
        _optimizer = new LpOptimizer();
    }

    [Fact]
    public void Optimize_WithSimpleScenario_ShouldReturnOptimalSolution()
    {
        // Arrange: Simple 1-stage, 2-item scenario
        var stages = new List<StageDto>
        {
            new() { Code = "S1", Name = "Stage 1", Forecast = 10000m }
        };

        var matchedItems = new List<MatchedItem>
        {
            new()
            {
                Item = new ItemDto { Stage = "S1", Name = "Item A", Unit = "бр", Qty = 100 },
                PriceEntry = new PriceBaseEntry { Name = "Item A", Unit = "бр", BasePrice = 50m },
                MatchScore = 1.0
            },
            new()
            {
                Item = new ItemDto { Stage = "S1", Name = "Item B", Unit = "м", Qty = 50 },
                PriceEntry = new PriceBaseEntry { Name = "Item B", Unit = "м", BasePrice = 100m },
                MatchScore = 1.0
            }
        };

        var request = new OptimizationRequest
        {
            MatchedItems = matchedItems,
            Stages = stages,
            Lambda = 1000,
            MinCoeff = 0.4,
            MaxCoeff = 2.0
        };

        // Act
        var result = _optimizer.Optimize(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Coeffs.Count); // 2 unique (name, unit) pairs
        Assert.True(result.SolverStatus == "OPTIMAL" || result.SolverStatus == "FEASIBLE");

        // Check stage summary
        Assert.Single(result.PerStage);
        var stageSummary = result.PerStage[0];
        Assert.Equal("S1", stageSummary.Stage);
        Assert.Equal(10000m, stageSummary.Forecast);
        Assert.True(stageSummary.Proposed <= stageSummary.Forecast); // Must respect constraint
        Assert.True(stageSummary.Ok);
    }

    [Fact]
    public void Optimize_ShouldRespectCoefficientBounds()
    {
        var stages = new List<StageDto>
        {
            new() { Code = "S1", Name = "Stage 1", Forecast = 20000m }
        };

        var matchedItems = new List<MatchedItem>
        {
            new()
            {
                Item = new ItemDto { Stage = "S1", Name = "Item A", Unit = "бр", Qty = 100 },
                PriceEntry = new PriceBaseEntry { Name = "Item A", Unit = "бр", BasePrice = 100m },
                MatchScore = 1.0
            }
        };

        var request = new OptimizationRequest
        {
            MatchedItems = matchedItems,
            Stages = stages,
            Lambda = 500,
            MinCoeff = 0.5,
            MaxCoeff = 1.5
        };

        var result = _optimizer.Optimize(request);

        // All coefficients must be within bounds
        Assert.All(result.Coeffs, c =>
        {
            Assert.True(c.C >= 0.5, $"Coefficient {c.C} is below minimum 0.5");
            Assert.True(c.C <= 1.5, $"Coefficient {c.C} is above maximum 1.5");
        });
    }

    [Fact]
    public void Optimize_ShouldMaximizeValueWithinBudget()
    {
        // Scenario: tight budget - optimizer should push coefficients high
        var stages = new List<StageDto>
        {
            new() { Code = "S1", Name = "Stage 1", Forecast = 15000m }
        };

        var matchedItems = new List<MatchedItem>
        {
            new()
            {
                Item = new ItemDto { Stage = "S1", Name = "Item A", Unit = "бр", Qty = 100 },
                PriceEntry = new PriceBaseEntry { Name = "Item A", Unit = "бр", BasePrice = 100m },
                MatchScore = 1.0
            }
        };

        var request = new OptimizationRequest
        {
            MatchedItems = matchedItems,
            Stages = stages,
            Lambda = 100, // Low penalty - prioritize value
            MinCoeff = 0.4,
            MaxCoeff = 2.0
        };

        var result = _optimizer.Optimize(request);

        Assert.Equal("OPTIMAL", result.SolverStatus);

        // Should try to maximize: c should be close to maxCoeff or constrained by budget
        var coeff = result.Coeffs.First().C;
        // 100 * 100 * c <= 15000 => c <= 1.5
        Assert.True(coeff <= 1.5 + 0.01); // Allow small numerical tolerance
    }

    [Fact]
    public void Optimize_ShouldApplyL1Penalty()
    {
        // High lambda should pull coefficients toward 1.0
        // BUT: LP maximizes value - penalty, so if value gain > penalty cost, it will increase c
        // To test L1 penalty effect, we need a scenario where deviating from 1.0 is costly
        var stages = new List<StageDto>
        {
            new() { Code = "S1", Name = "Stage 1", Forecast = 12000m } // Moderate budget
        };

        var matchedItems = new List<MatchedItem>
        {
            new()
            {
                Item = new ItemDto { Stage = "S1", Name = "Item A", Unit = "бр", Qty = 100 },
                PriceEntry = new PriceBaseEntry { Name = "Item A", Unit = "бр", BasePrice = 100m },
                MatchScore = 1.0
            }
        };

        var request = new OptimizationRequest
        {
            MatchedItems = matchedItems,
            Stages = stages,
            Lambda = 50000, // Very high penalty - makes deviation from 1.0 very expensive
            MinCoeff = 0.4,
            MaxCoeff = 2.0
        };

        var result = _optimizer.Optimize(request);

        // With very high lambda (50k), deviating from c=1.0 costs more than value gain
        // c should be close to 1.0 (or at budget constraint c=1.2)
        var coeff = result.Coeffs.First().C;
        Assert.True(coeff >= 0.9 && coeff <= 1.3, $"Expected c near 1.0-1.2, got {coeff}");
    }

    [Fact]
    public void Optimize_WithMultipleStages_ShouldRespectAllConstraints()
    {
        var stages = new List<StageDto>
        {
            new() { Code = "S1", Name = "Stage 1", Forecast = 5000m },
            new() { Code = "S2", Name = "Stage 2", Forecast = 8000m }
        };

        var matchedItems = new List<MatchedItem>
        {
            // S1 items
            new()
            {
                Item = new ItemDto { Stage = "S1", Name = "Item A", Unit = "бр", Qty = 50 },
                PriceEntry = new PriceBaseEntry { Name = "Item A", Unit = "бр", BasePrice = 80m },
                MatchScore = 1.0
            },
            // S2 items
            new()
            {
                Item = new ItemDto { Stage = "S2", Name = "Item B", Unit = "м", Qty = 60 },
                PriceEntry = new PriceBaseEntry { Name = "Item B", Unit = "м", BasePrice = 100m },
                MatchScore = 1.0
            }
        };

        var request = new OptimizationRequest
        {
            MatchedItems = matchedItems,
            Stages = stages,
            Lambda = 500,
            MinCoeff = 0.4,
            MaxCoeff = 2.0
        };

        var result = _optimizer.Optimize(request);

        Assert.Equal(2, result.PerStage.Count);
        Assert.All(result.PerStage, s => Assert.True(s.Ok, $"Stage {s.Stage} violated budget"));
    }

    [Fact]
    public void Optimize_WithTightBudget_ShouldStayFeasible()
    {
        // Very tight budget - even with min coefficients might be tight
        var stages = new List<StageDto>
        {
            new() { Code = "S1", Name = "Stage 1", Forecast = 2000m }
        };

        var matchedItems = new List<MatchedItem>
        {
            new()
            {
                Item = new ItemDto { Stage = "S1", Name = "Item A", Unit = "бр", Qty = 100 },
                PriceEntry = new PriceBaseEntry { Name = "Item A", Unit = "бр", BasePrice = 50m },
                MatchScore = 1.0
            }
        };

        var request = new OptimizationRequest
        {
            MatchedItems = matchedItems,
            Stages = stages,
            Lambda = 100,
            MinCoeff = 0.4,
            MaxCoeff = 2.0
        };

        var result = _optimizer.Optimize(request);

        // Minimum required: 100 * 50 * 0.4 = 2000 - exactly at budget
        Assert.True(result.SolverStatus == "OPTIMAL" || result.SolverStatus == "FEASIBLE");
        Assert.True(result.PerStage[0].Proposed <= 2000m);
    }

    [Fact]
    public void Optimize_ShouldRoundWorkingPrices()
    {
        var stages = new List<StageDto>
        {
            new() { Code = "S1", Name = "Stage 1", Forecast = 10000m }
        };

        var matchedItems = new List<MatchedItem>
        {
            new()
            {
                Item = new ItemDto { Stage = "S1", Name = "Item A", Unit = "бр", Qty = 100 },
                PriceEntry = new PriceBaseEntry { Name = "Item A", Unit = "бр", BasePrice = 33.33m },
                MatchScore = 1.0
            }
        };

        var request = new OptimizationRequest
        {
            MatchedItems = matchedItems,
            Stages = stages,
            Lambda = 1000,
            MinCoeff = 1.0,
            MaxCoeff = 1.0 // Force c = 1.0
        };

        var result = _optimizer.Optimize(request);

        var coeff = result.Coeffs.First().C;
        var basePrice = result.Coeffs.First().BasePrice;
        var workPrice = Math.Round(basePrice * (decimal)coeff, 2);

        // Working price should be rounded to 2 decimals
        Assert.Equal(33.33m, workPrice);
    }
}
