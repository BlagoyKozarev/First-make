namespace Core.Engine.Models;

/// <summary>
/// Complete Bill of Quantities (КСС)
/// </summary>
public record BoqDto
{
    public required ProjectDto Project { get; init; }
    public required List<StageDto> Stages { get; init; }
    public required List<ItemDto> Items { get; init; }
}
