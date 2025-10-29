using Core.Engine.Models;
using Google.OrTools.LinearSolver;
using System.Diagnostics;

namespace Core.Engine.Services;

/// <summary>
/// Multi-file LP Optimizer with unified coefficients
/// Same (Name, Unit) → same coefficient across ALL 19 КСС files
/// Per-stage constraints: Gap = Forecast - ProposedValue ≥ 0
/// </summary>
public class MultiFileOptimizer
{
    /// <summary>
    /// Optimize with unified coefficients across multiple BOQ files
    /// </summary>
    public IterationResult OptimizeMultiFile(ProjectSession session, UnifiedMatchResult matchResult, int iterationNumber)
    {
        if (session.Forecasts == null)
        {
            throw new InvalidOperationException("No forecasts available. Upload Указания first.");
        }

        var sw = Stopwatch.StartNew();

        // Create OR-Tools solver
        using var solver = Solver.CreateSolver("GLOP");
        if (solver == null)
        {
            throw new InvalidOperationException("Could not create GLOP solver");
        }

        // Get all unique (Name, Unit) pairs across all files
        var uniquePairs = matchResult.UnifiedMatches.Keys.ToList();

        // Create decision variables: one coefficient per unique (Name, Unit)
        var coeffVars = new Dictionary<string, Variable>();
        var dPlusVars = new Dictionary<string, Variable>();
        var dMinusVars = new Dictionary<string, Variable>();

        const double minCoeff = 0.4;
        const double maxCoeff = 2.0;

        foreach (var key in uniquePairs)
        {
            var match = matchResult.UnifiedMatches[key];
            if (match.PriceEntry == null)
                continue; // Skip unmatched items

            coeffVars[key] = solver.MakeNumVar(minCoeff, maxCoeff, $"c_{key}");
            dPlusVars[key] = solver.MakeNumVar(0, double.PositiveInfinity, $"dp_{key}");
            dMinusVars[key] = solver.MakeNumVar(0, double.PositiveInfinity, $"dm_{key}");
        }

        // L1 linearization: c - 1 = d⁺ - d⁻
        foreach (var key in coeffVars.Keys)
        {
            var constraint = solver.MakeConstraint(1.0, 1.0, $"abs_{key}");
            constraint.SetCoefficient(coeffVars[key], 1.0);
            constraint.SetCoefficient(dPlusVars[key], -1.0);
            constraint.SetCoefficient(dMinusVars[key], 1.0);
        }

        // Per-stage budget constraints: ProposedValue ≤ Forecast
        // Group items by stage across all BOQ files
        var stageConstraints = new Dictionary<string, Constraint>();

        foreach (var forecast in session.Forecasts.Stages.Values)
        {
            stageConstraints[forecast.Code] = solver.MakeConstraint(
                double.NegativeInfinity,
                (double)forecast.Forecast,
                $"budget_{forecast.Code}"
            );
        }

        // Add all items to their stage constraints
        foreach (var itemMatch in matchResult.ItemMatches.Values)
        {
            if (itemMatch.PriceEntry == null)
                continue;

            var stageCode = itemMatch.Item.StageCode;
            if (!stageConstraints.ContainsKey(stageCode))
            {
                // Stage not in forecasts - skip or create constraint with very high limit
                continue;
            }

            var key = itemMatch.UnifiedKey;
            if (!coeffVars.ContainsKey(key))
                continue;

            var valueCoeff = (double)(itemMatch.Item.Quantity * itemMatch.PriceEntry.BasePrice);
            stageConstraints[stageCode].SetCoefficient(coeffVars[key], valueCoeff);
        }

        // Objective: maximize ProposedValue - λ * Σ(d⁺ + d⁻)
        var objective = solver.Objective();
        objective.SetMaximization();

        const double lambda = 1000.0; // Penalty weight for deviating from coefficient = 1.0

        // Total value term
        foreach (var itemMatch in matchResult.ItemMatches.Values)
        {
            if (itemMatch.PriceEntry == null)
                continue;

            var key = itemMatch.UnifiedKey;
            if (!coeffVars.ContainsKey(key))
                continue;

            var valueCoeff = (double)(itemMatch.Item.Quantity * itemMatch.PriceEntry.BasePrice);
            objective.SetCoefficient(coeffVars[key], valueCoeff);
        }

        // L1 penalty term
        foreach (var key in coeffVars.Keys)
        {
            objective.SetCoefficient(dPlusVars[key], -lambda);
            objective.SetCoefficient(dMinusVars[key], -lambda);
        }

        // Solve
        var status = solver.Solve();
        sw.Stop();

        if (status != Solver.ResultStatus.OPTIMAL && status != Solver.ResultStatus.FEASIBLE)
        {
            throw new InvalidOperationException($"Optimization failed with status: {status}");
        }

        // Extract results
        var coefficients = new Dictionary<string, CoefficientEntry>();
        foreach (var key in coeffVars.Keys)
        {
            var match = matchResult.UnifiedMatches[key];
            coefficients[key] = new CoefficientEntry
            {
                Name = match.PriceEntry.Name,
                Unit = match.PriceEntry.Unit,
                BasePrice = match.PriceEntry.BasePrice,
                Coefficient = coeffVars[key].SolutionValue(),
                UnifiedKey = key
            };
        }

        // Calculate per-BOQ-file results
        var boqResults = new Dictionary<string, BoqFileResult>();

        foreach (var doc in session.BoqDocuments)
        {
            var docItems = matchResult.ItemMatches.Values
                .Where(m => m.Item.SourceFileId == doc.SourceFileId)
                .ToList();

            var stageResultsList = new List<StageResult>();

            foreach (var stage in doc.Stages)
            {
                var stageItems = docItems.Where(m => m.Item.StageCode == stage.Code).ToList();

                var itemResults = stageItems
                    .Select(m =>
                    {
                        if (m.PriceEntry != null && coefficients.ContainsKey(m.UnifiedKey))
                        {
                            var coeff = coefficients[m.UnifiedKey];
                            return new ItemResult
                            {
                                Name = m.Item.Name,
                                Unit = m.Item.Unit,
                                Quantity = m.Item.Quantity,
                                BasePrice = coeff.BasePrice,
                                Coefficient = coeff.Coefficient
                            };
                        }
                        else
                        {
                            return new ItemResult
                            {
                                Name = m.Item.Name,
                                Unit = m.Item.Unit,
                                Quantity = m.Item.Quantity,
                                BasePrice = 0,
                                Coefficient = 0
                            };
                        }
                    })
                    .ToList();

                var proposedValue = itemResults.Sum(ir => ir.Value);
                var forecast = session.Forecasts.Stages.TryGetValue(stage.Code, out var f) ? f.Forecast : 0;

                stageResultsList.Add(new StageResult
                {
                    StageCode = stage.Code,
                    StageName = stage.Name,
                    Forecast = forecast,
                    Proposed = proposedValue,
                    Items = itemResults
                });
            }

            boqResults[doc.SourceFileId] = new BoqFileResult
            {
                FileId = doc.SourceFileId,
                FileName = doc.FileName,
                Stages = stageResultsList,
                TotalProposed = stageResultsList.Sum(s => s.Proposed),
                TotalForecast = stageResultsList.Sum(s => s.Forecast)
            };
        }

        // Overall totals
        var overallProposed = boqResults.Values.Sum(b => b.TotalProposed);
        var overallForecast = session.Forecasts?.TotalForecast ?? 0;

        return new IterationResult
        {
            IterationNumber = iterationNumber,
            Timestamp = DateTime.UtcNow,
            Coefficients = coefficients,
            BoqResults = boqResults,
            OverallProposed = overallProposed,
            OverallForecast = overallForecast,
            SolverStatus = status.ToString(),
            SolveDurationMs = sw.ElapsedMilliseconds,
            Objective = solver.Objective().Value(),
            OutputFiles = new List<OutputFileMetadata>()
        };
    }

    /// <summary>
    /// Calculate item-level results (for detailed export)
    /// </summary>
    public List<ItemResult> CalculateItemResults(
        UnifiedMatchResult matchResult,
        Dictionary<string, CoefficientEntry> coefficients)
    {
        var results = new List<ItemResult>();

        foreach (var itemMatch in matchResult.ItemMatches.Values)
        {
            // For unmatched items, create zero entries
            if (itemMatch.PriceEntry == null)
            {
                results.Add(new ItemResult
                {
                    Name = itemMatch.Item.Name,
                    Unit = itemMatch.Item.Unit,
                    Quantity = itemMatch.Item.Quantity,
                    BasePrice = 0,
                    Coefficient = 0
                });
            }
            else
            {
                var coeff = coefficients[itemMatch.UnifiedKey];
                results.Add(new ItemResult
                {
                    Name = itemMatch.Item.Name,
                    Unit = itemMatch.Item.Unit,
                    Quantity = itemMatch.Item.Quantity,
                    BasePrice = coeff.BasePrice,
                    Coefficient = coeff.Coefficient
                });
            }
        }

        return results;
    }
}
