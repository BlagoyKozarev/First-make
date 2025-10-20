namespace Core.Engine.Models;

/// <summary>
/// Per-stage budget summary
/// </summary>
public record StageSummary
{
    public required string Stage { get; init; }
    public required decimal Forecast { get; init; }
    public required decimal Proposed { get; init; }
    public decimal Gap => Forecast - Proposed;
    public bool Ok => Proposed <= Forecast;
}
