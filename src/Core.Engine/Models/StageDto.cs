namespace Core.Engine.Models;

/// <summary>
/// Construction stage with forecasted budget cap
/// </summary>
public record StageDto
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required decimal Forecast { get; init; }
}
