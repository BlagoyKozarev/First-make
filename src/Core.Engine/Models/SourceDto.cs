namespace Core.Engine.Models;

/// <summary>
/// Evidence/provenance information for a BoQ item
/// </summary>
public record SourceDto
{
    public string? File { get; init; }
    public int? Page { get; init; }
    public string? Cell { get; init; }
    public double[]? Bbox { get; init; }
}
