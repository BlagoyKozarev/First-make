namespace Core.Engine.Models;

/// <summary>
/// Base price entry from catalogue
/// </summary>
public record PriceBaseEntry
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required decimal BasePrice { get; init; }
    public string? Category { get; init; }
    public List<string>? Aliases { get; init; }
}
