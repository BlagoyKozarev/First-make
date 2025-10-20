namespace Core.Engine.Models;

/// <summary>
/// Line item from BoQ with quantity and unit
/// </summary>
public record ItemDto
{
    public required string Stage { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required decimal Qty { get; init; }
    public SourceDto? Source { get; init; }
}
