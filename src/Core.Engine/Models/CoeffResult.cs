namespace Core.Engine.Models;

/// <summary>
/// Coefficient result per (name, unit) pair
/// </summary>
public record CoeffResult
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required double C { get; init; }
    public decimal BasePrice { get; init; }
}
