namespace Core.Engine.Models;

/// <summary>
/// Complete project session with multiple КСС files
/// </summary>
public record ProjectSession
{
    public required string Id { get; init; }
    public required string ObjectName { get; init; }
    public required string Employee { get; init; }
    public required DateTime Date { get; init; }

    // Input files metadata
    public List<FileMetadata> InstructionsFiles { get; init; } = new();
    public List<FileMetadata> PriceBaseFiles { get; init; } = new();
    public List<FileMetadata> KssFiles { get; init; } = new();
    public FileMetadata? TemplateFile { get; init; }

    // Extracted data
    public StageForecasts? Forecasts { get; init; }
    public List<PriceEntry> PriceBase { get; init; } = new();
    public List<BoqDocument> BoqDocuments { get; init; } = new();

    // Iterations
    public List<IterationResult> Iterations { get; init; } = new();
    public int CurrentIteration { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record FileMetadata
{
    public required string Id { get; init; }
    public required string FileName { get; init; }
    public required string OriginalPath { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime UploadedAt { get; init; }
}

public record BoqDocument
{
    public required string Id { get; init; }
    public required string FileName { get; init; }
    public required string SourceFileId { get; init; }
    public required List<StageDto> Stages { get; init; }
    public required List<BoqItemDto> Items { get; init; }
}

public record BoqItemDto
{
    public required string Id { get; init; }
    public required string StageCode { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required decimal Quantity { get; init; }
    public int SourceRow { get; init; }
    public string? SourceSheet { get; init; }
    public required string SourceFileId { get; init; }
}

public record StageForecasts
{
    public required Dictionary<string, StageForecast> Stages { get; init; }
    public required decimal TotalForecast { get; init; }
    public required string SourceFileId { get; init; }
}

public record StageForecast
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required decimal Forecast { get; init; }
}

public record PriceEntry
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required decimal BasePrice { get; init; }
    public required string SourceFileId { get; init; }
    public int SourceRow { get; init; }
}

public record IterationResult
{
    public required int IterationNumber { get; init; }
    public required DateTime Timestamp { get; init; }

    // Unified coefficients (same for all BOQ files)
    public required Dictionary<string, CoefficientEntry> Coefficients { get; init; }

    // Results per BOQ file
    public required Dictionary<string, BoqFileResult> BoqResults { get; init; }

    // Overall aggregated results
    public required decimal OverallProposed { get; init; }
    public required decimal OverallForecast { get; init; }
    public decimal OverallGap => OverallForecast - OverallProposed;
    public bool Ok => OverallGap >= 0;

    // Optimization metadata
    public required string SolverStatus { get; init; }
    public required long SolveDurationMs { get; init; }
    public required double Objective { get; init; }

    // Generated output files
    public required List<OutputFileMetadata> OutputFiles { get; init; }
}

public record CoefficientEntry
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required double Coefficient { get; init; }
    public required decimal BasePrice { get; init; }
    public required string UnifiedKey { get; init; }
    public string Key => $"{Name}|{Unit}";
    public decimal WorkPrice => BasePrice * (decimal)Coefficient;
}

public record BoqFileResult
{
    public required string FileName { get; init; }
    public required string FileId { get; init; }
    public required List<StageResult> Stages { get; init; }
    public required decimal TotalProposed { get; init; }
    public required decimal TotalForecast { get; init; }
    public decimal TotalGap => TotalForecast - TotalProposed;
    public bool Ok => TotalGap >= 0;
}

public record StageResult
{
    public required string StageCode { get; init; }
    public required string StageName { get; init; }
    public required decimal Forecast { get; init; }
    public required decimal Proposed { get; init; }
    public decimal Gap => Forecast - Proposed;
    public bool Ok => Gap >= 0;
    public required List<ItemResult> Items { get; init; }
}

public record ItemResult
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal BasePrice { get; init; }
    public required double Coefficient { get; init; }
    public decimal WorkPrice => BasePrice * (decimal)Coefficient;
    public decimal Value => Quantity * WorkPrice;
}

public record OutputFileMetadata
{
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime GeneratedAt { get; init; }
}
