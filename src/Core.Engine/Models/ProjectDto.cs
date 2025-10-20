namespace Core.Engine.Models;

/// <summary>
/// Project metadata from BoQ documents
/// </summary>
public record ProjectDto
{
    public required string Name { get; init; }
    public required DateOnly Date { get; init; }
    public string? Employee { get; init; }
}
