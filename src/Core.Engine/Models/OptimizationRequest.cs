using Core.Engine.Models;

namespace Core.Engine.Services;

/// <summary>
/// LP Optimization request parameters
/// </summary>
public record OptimizationRequest
{
    public required List<MatchedItem> MatchedItems { get; init; }
    public required List<StageDto> Stages { get; init; }
    public double Lambda { get; init; } = 1000.0; // L1 penalty weight
    public double MinCoeff { get; init; } = 0.4;
    public double MaxCoeff { get; init; } = 2.0;
}
