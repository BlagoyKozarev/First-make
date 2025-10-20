namespace Core.Engine.Models;

/// <summary>
/// BoQ item matched with a price base entry
/// </summary>
public record MatchedItem
{
    public required ItemDto Item { get; init; }
    public required PriceBaseEntry PriceEntry { get; init; }
    public double MatchScore { get; init; }
    public bool IsManualMatch { get; init; }
}
