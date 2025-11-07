using Core.Engine.Models;

namespace Core.Engine.Services;

/// <summary>
/// LP Optimization request parameters
/// </summary>
public record OptimizationRequest
{
    public required List<MatchedItem> MatchedItems { get; init; }
    public required List<StageDto> Stages { get; init; }
    public double Lambda { get; init; } = 100000.0; // Very high L1 penalty - keeps coeffs very close to 1.0
    public double MinCoeff { get; init; } = 0.88; // Narrow range for proportional coefficients
    public double MaxCoeff { get; init; } = 1.12; // Narrow range for proportional coefficients (±12%)
    public double TargetGapPercentage { get; init; } = 0.01; // Target gap: 1% of forecast
}
