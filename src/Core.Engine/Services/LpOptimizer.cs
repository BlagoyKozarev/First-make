using Core.Engine.Models;
using System.Diagnostics;
using Google.OrTools.LinearSolver;

namespace Core.Engine.Services;

/// <summary>
/// LP Optimizer using Google OR-Tools
/// Objective: maximize total value - λ * Σ|c - 1|
/// Constraints: per-stage budget caps, coefficient bounds
/// L1 penalty linearization: |c - 1| = d⁺ + d⁻
/// </summary>
public class LpOptimizer
{
    /// <summary>
    /// Run LP optimization with OR-Tools
    /// </summary>
    public OptimizationResult Optimize(OptimizationRequest request)
    {
        var sw = Stopwatch.StartNew();

        // Create OR-Tools solver (GLOP = Google Linear Optimization Package)
        using var solver = Solver.CreateSolver("GLOP");
        if (solver == null)
        {
            throw new InvalidOperationException("Could not create GLOP solver");
        }

        // Get unique (name, unit) pairs
        var uniquePairs = GetUniquePairs(request.MatchedItems);

        // Create decision variables: c_g for each (name, unit)
        var coeffVars = new Dictionary<(string name, string unit), Variable>();
        var dPlusVars = new Dictionary<(string name, string unit), Variable>();  // d⁺ for |c - 1|
        var dMinusVars = new Dictionary<(string name, string unit), Variable>(); // d⁻ for |c - 1|

        foreach (var pair in uniquePairs)
        {
            // c_g ∈ [minCoeff, maxCoeff]
            coeffVars[pair] = solver.MakeNumVar(request.MinCoeff, request.MaxCoeff, $"c_{pair.name}_{pair.unit}");

            // d⁺, d⁻ ≥ 0 for L1 linearization
            dPlusVars[pair] = solver.MakeNumVar(0, double.PositiveInfinity, $"d+_{pair.name}_{pair.unit}");
            dMinusVars[pair] = solver.MakeNumVar(0, double.PositiveInfinity, $"d-_{pair.name}_{pair.unit}");
        }

        // Constraint: c - 1 = d⁺ - d⁻ (linearization of |c - 1|)
        foreach (var pair in uniquePairs)
        {
            var constraint = solver.MakeConstraint(1.0, 1.0, $"abs_{pair.name}_{pair.unit}");
            constraint.SetCoefficient(coeffVars[pair], 1.0);
            constraint.SetCoefficient(dPlusVars[pair], -1.0);
            constraint.SetCoefficient(dMinusVars[pair], 1.0);
        }

        // Per-stage budget constraints: Σ(qty * basePrice * c) ≤ forecast
        foreach (var stage in request.Stages)
        {
            // Constraint: proposed ≤ forecast (don't exceed budget)
            var stageConstraint = solver.MakeConstraint(double.NegativeInfinity, (double)stage.Forecast, $"budget_{stage.Code}");

            var stageItems = request.MatchedItems.Where(m => m.Item.Stage == stage.Code);
            foreach (var item in stageItems)
            {
                var pair = (item.PriceEntry.Name, item.PriceEntry.Unit);
                var coeff = (double)(item.Item.Qty * item.PriceEntry.BasePrice);
                stageConstraint.SetCoefficient(coeffVars[pair], coeff);
            }
        }

        // Objective: maximize total value - λ * Σ(d⁺ + d⁻)
        // This encourages using max budget while keeping coefficients close to 1.0
        var objective = solver.Objective();

        // Total value term: Σ(qty * basePrice * c) - maximize to use budget
        foreach (var item in request.MatchedItems)
        {
            var pair = (item.PriceEntry.Name, item.PriceEntry.Unit);
            var valueCoeff = (double)(item.Item.Qty * item.PriceEntry.BasePrice);
            objective.SetCoefficient(coeffVars[pair], valueCoeff);
        }

        // L1 penalty term: -λ * Σ(d⁺ + d⁻) - keeps coefficients close to 1.0
        foreach (var pair in uniquePairs)
        {
            objective.SetCoefficient(dPlusVars[pair], -request.Lambda);
            objective.SetCoefficient(dMinusVars[pair], -request.Lambda);
        }

        objective.SetMaximization();

        // Solve!
        var resultStatus = solver.Solve();
        sw.Stop();

        // Extract solution
        var coeffs = new Dictionary<(string name, string unit), double>();
        if (resultStatus == Solver.ResultStatus.OPTIMAL || resultStatus == Solver.ResultStatus.FEASIBLE)
        {
            foreach (var pair in uniquePairs)
            {
                coeffs[pair] = coeffVars[pair].SolutionValue();
            }
        }
        else
        {
            // Infeasible - return naive c=1.0
            foreach (var pair in uniquePairs)
            {
                coeffs[pair] = 1.0;
            }
        }

        // Calculate results
        var perStage = CalculateStageSummaries(request, coeffs);
        var totalValue = perStage.Sum(s => s.Proposed);
        var penalty = CalculatePenalty(coeffs.Values, request.Lambda);
        var objectiveValue = resultStatus == Solver.ResultStatus.OPTIMAL
            ? solver.Objective().Value()
            : (double)totalValue - penalty;

        return new OptimizationResult
        {
            Coeffs = coeffs.Select(kv => new CoeffResult
            {
                Name = kv.Key.name,
                Unit = kv.Key.unit,
                C = kv.Value,
                BasePrice = GetBasePrice(request.MatchedItems, kv.Key.name, kv.Key.unit)
            }).ToList(),
            PerStage = perStage,
            Ok = perStage.All(s => s.Ok),
            TotalValue = totalValue,
            Penalty = penalty,
            Objective = objectiveValue,
            SolverStatus = resultStatus.ToString(),
            SolveDurationMs = sw.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Get unique (name, unit) pairs from matched items
    /// </summary>
    private static List<(string name, string unit)> GetUniquePairs(List<MatchedItem> items)
    {
        return items
            .Select(m => (m.PriceEntry.Name, m.PriceEntry.Unit))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Calculate per-stage summaries with proposed values
    /// </summary>
    private List<StageSummary> CalculateStageSummaries(
        OptimizationRequest request,
        Dictionary<(string name, string unit), double> coeffs)
    {
        var summaries = new List<StageSummary>();

        foreach (var stage in request.Stages)
        {
            var stageItems = request.MatchedItems
                .Where(m => m.Item.Stage == stage.Code)
                .ToList();

            var proposed = stageItems.Sum(item =>
            {
                var key = (item.PriceEntry.Name, item.PriceEntry.Unit);
                var c = coeffs.GetValueOrDefault(key, 1.0);
                var workPrice = Round2(item.PriceEntry.BasePrice * (decimal)c);
                return item.Item.Qty * workPrice;
            });

            summaries.Add(new StageSummary
            {
                Stage = stage.Code,
                Forecast = stage.Forecast,
                Proposed = proposed
            });
        }

        return summaries;
    }

    /// <summary>
    /// Calculate L1 penalty: λ * Σ|c - 1|
    /// </summary>
    private double CalculatePenalty(IEnumerable<double> coeffs, double lambda)
    {
        return lambda * coeffs.Sum(c => Math.Abs(c - 1.0));
    }

    /// <summary>
    /// Round to 2 decimal places (working price precision)
    /// </summary>
    private static decimal Round2(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Get base price for a (name, unit) pair
    /// </summary>
    private static decimal GetBasePrice(List<MatchedItem> items, string name, string unit)
    {
        var item = items.FirstOrDefault(i =>
            i.PriceEntry.Name == name && i.PriceEntry.Unit == unit);
        return item?.PriceEntry.BasePrice ?? 0;
    }
}
