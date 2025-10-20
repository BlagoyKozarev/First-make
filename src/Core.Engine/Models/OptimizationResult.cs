namespace Core.Engine.Models;

/// <summary>
/// Complete LP optimization result
/// </summary>
public record OptimizationResult
{
    public required List<CoeffResult> Coeffs { get; init; }
    public required List<StageSummary> PerStage { get; init; }
    public required bool Ok { get; init; }
    public decimal TotalValue { get; init; }
    public double Penalty { get; init; }
    public double Objective { get; init; }
    public string? SolverStatus { get; init; }
    public long SolveDurationMs { get; init; }
}
