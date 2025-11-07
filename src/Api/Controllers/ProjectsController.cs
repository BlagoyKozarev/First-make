using Microsoft.AspNetCore.Mvc;
using Core.Engine.Models;
using Core.Engine.Services;
using Api.Services;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ProjectSessionService _sessionService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        ProjectSessionService sessionService,
        ILogger<ProjectsController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new project session
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ProjectSessionResponse>> CreateProject([FromBody] CreateProjectRequest request)
    {
        try
        {
            var session = await _sessionService.CreateSessionAsync(
                request.ObjectName,
                request.Employee,
                request.Date
            );

            _logger.LogInformation("Created project session {SessionId}", session.Id);
            
            var response = new ProjectSessionResponse
            {
                ProjectId = session.Id,
                Metadata = new ProjectMetadata
                {
                    ObjectName = session.ObjectName,
                    Employee = session.Employee,
                    Date = session.Date.ToString("yyyy-MM-dd")
                },
                KssFilesCount = session.KssFiles.Count,
                UkazaniaFilesCount = session.InstructionsFiles.Count,
                PriceBaseFilesCount = session.PriceBaseFiles.Count,
                HasTemplate = session.TemplateFile != null,
                HasMatchingResults = false, // Will be set after matching
                HasOptimizationResults = session.Iterations.Count > 0,
                CreatedAt = session.CreatedAt
            };
            
            return CreatedAtAction(nameof(GetProject), new { id = session.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project session");
            return StatusCode(500, new { error = "Failed to create project session" });
        }
    }

    /// <summary>
    /// Get project session by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectSessionResponse>> GetProject(string id)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(id);
            if (session == null)
            {
                return NotFound(new { error = "Project session not found" });
            }

            var response = new ProjectSessionResponse
            {
                ProjectId = session.Id,
                Metadata = new ProjectMetadata
                {
                    ObjectName = session.ObjectName,
                    Employee = session.Employee,
                    Date = session.Date.ToString("yyyy-MM-dd")
                },
                KssFilesCount = session.KssFiles.Count,
                UkazaniaFilesCount = session.InstructionsFiles.Count,
                PriceBaseFilesCount = session.PriceBaseFiles.Count,
                HasTemplate = session.TemplateFile != null,
                HasMatchingResults = false,
                HasOptimizationResults = session.Iterations.Count > 0,
                CreatedAt = session.CreatedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project session {SessionId}", id);
            return StatusCode(500, new { error = "Failed to get project session" });
        }
    }

    /// <summary>
    /// Upload КСС files
    /// </summary>
    [HttpPost("{id}/files/kss")]
    [RequestSizeLimit(100_000_000)] // 100MB
    public async Task<ActionResult> UploadKssFiles(string id, [FromForm] List<IFormFile> files)
    {
        try
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { error = "No files provided" });
            }

            if (files.Count > 40)
            {
                return BadRequest(new { error = "Maximum 40 КСС files allowed" });
            }

            // Log file details for debugging
            _logger.LogInformation("Uploading {Count} files to session {SessionId}", files.Count, id);
            foreach (var file in files)
            {
                _logger.LogInformation("File: {FileName}, Size: {Size} bytes, ContentType: {ContentType}", 
                    file.FileName, file.Length, file.ContentType);
            }

            var result = await _sessionService.UploadKssFilesAsync(id, files);
            
            _logger.LogInformation("Successfully uploaded {Count} КСС files to session {SessionId}", files.Count, id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Project session not found" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid file upload for session {SessionId}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading КСС files to session {SessionId}", id);
            
            // Provide more detailed error message
            var errorMessage = ex.Message;
            if (ex.Message.Contains("valid Package file"))
            {
                errorMessage = "Файлът не е валиден Excel (.xlsx) файл. Моля, уверете се, че качвате .xlsx файл, а не .xls или друг формат.";
            }
            else if (ex.Message.Contains("encrypted"))
            {
                errorMessage = "Файлът е защитен с парола. Моля, отворете файла в Excel, премахнете паролата и опитайте отново.";
            }
            
            return StatusCode(500, new { error = errorMessage, details = ex.Message });
        }
    }

    /// <summary>
    /// Upload forecast Excel files with stage budgets
    /// Expected format: Column A = Stage Code, Column B = Forecast Value
    /// </summary>
    [HttpPost("{id}/files/forecasts")]
    [RequestSizeLimit(50_000_000)] // 50MB
    public async Task<ActionResult> UploadForecastFiles(string id, [FromForm] List<IFormFile> files)
    {
        try
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { error = "Не са предоставени файлове" });
            }

            if (files.Count > 2)
            {
                return BadRequest(new { error = "Максимум 2 файла с прогнози" });
            }

            var result = await _sessionService.UploadForecastFilesAsync(id, files);
            
            _logger.LogInformation("Uploaded {Count} forecast file(s) to session {SessionId}", files.Count, id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Проектът не е намерен" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading forecast files to session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Upload Указания files (DEPRECATED - use /forecasts instead)
    /// </summary>
    [HttpPost("{id}/files/ukazania")]
    [RequestSizeLimit(50_000_000)] // 50MB
    [Obsolete("Use /files/forecasts endpoint instead")]
    public async Task<ActionResult> UploadUkazaniaFiles(string id, [FromForm] List<IFormFile> files)
    {
        // Redirect to new forecast upload endpoint
        return await UploadForecastFiles(id, files);
    }

    /// <summary>
    /// Set manual forecasts (alternative to uploading Указания)
    /// </summary>
    [HttpPost("{id}/forecasts/manual")]
    public async Task<ActionResult> SetManualForecasts(string id, [FromBody] ManualForecastsRequest request)
    {
        try
        {
            var result = await _sessionService.SetManualForecastsAsync(id, request.Forecasts);
            
            _logger.LogInformation("Set manual forecasts for {StageCount} stages in session {SessionId}", 
                request.Forecasts.Count, id);
            
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Project session not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting manual forecasts for session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get available stages from uploaded КСС files
    /// </summary>
    [HttpGet("{id}/stages")]
    public async Task<ActionResult> GetAvailableStages(string id)
    {
        try
        {
            var stages = await _sessionService.GetAvailableStagesAsync(id);
            return Ok(new { stages });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Project session not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stages for session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Upload Price Base files
    /// </summary>
    [HttpPost("{id}/files/pricebase")]
    [RequestSizeLimit(20_000_000)] // 20MB
    public async Task<ActionResult> UploadPriceBaseFiles(string id, [FromForm] List<IFormFile> files)
    {
        try
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { error = "No files provided" });
            }

            if (files.Count > 2)
            {
                return BadRequest(new { error = "Maximum 2 Price Base files allowed" });
            }

            var result = await _sessionService.UploadPriceBaseFilesAsync(id, files);
            
            _logger.LogInformation("Uploaded {Count} Price Base files to session {SessionId}", files.Count, id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Project session not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading Price Base files to session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Upload Template file
    /// </summary>
    [HttpPost("{id}/files/template")]
    [RequestSizeLimit(10_000_000)] // 10MB
    public async Task<ActionResult> UploadTemplateFile(string id, IFormFile file)
    {
        try
        {
            if (file == null)
            {
                return BadRequest(new { error = "No file provided" });
            }

            var result = await _sessionService.UploadTemplateFileAsync(id, file);
            
            _logger.LogInformation("Uploaded template file to session {SessionId}", id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Project session not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading template file to session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete project session
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteProject(string id)
    {
        try
        {
            var deleted = await _sessionService.DeleteSessionAsync(id);
            if (!deleted)
            {
                return NotFound(new { error = "Project session not found" });
            }

            _logger.LogInformation("Deleted project session {SessionId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project session {SessionId}", id);
            return StatusCode(500, new { error = "Failed to delete project session" });
        }
    }

    /// <summary>
    /// Trigger unified matching for all BOQ items
    /// </summary>
    [HttpPost("{id}/match")]
    public async Task<ActionResult> TriggerMatching(string id)
    {
        try
        {
            var result = await _sessionService.TriggerMatchingAsync(id);
            
            _logger.LogInformation("Matched {Matched}/{Total} items in session {SessionId}", 
                result.Statistics.MatchedItems, result.Statistics.TotalItems, id);
            
            // Return only statistics to match frontend expectations
            return Ok(result.Statistics);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Project session not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering matching for session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get unmatched candidates for manual review
    /// </summary>
    [HttpGet("{id}/match/candidates")]
    public async Task<ActionResult> GetUnmatchedCandidates(string id)
    {
        try
        {
            var candidates = await _sessionService.GetUnmatchedCandidatesAsync(id);
            
            // Transform to DTO format matching frontend expectations
            var candidatesDto = candidates.Select(c => new UnmatchedCandidateDto
            {
                UnifiedKey = c.UnifiedKey,
                ItemName = c.Name,
                ItemUnit = c.Unit,
                OccurrenceCount = c.OccurrenceCount,
                TopCandidates = c.TopMatches.Select(m => new TopCandidateDto
                {
                    Name = m.PriceEntry.Name,
                    Unit = m.PriceEntry.Unit,
                    Price = m.PriceEntry.BasePrice,
                    Score = m.Score
                }).ToList()
            }).ToList();
            
            return Ok(new
            {
                candidates = candidatesDto,
                summary = new
                {
                    totalUnmatchedPositions = candidates.Count,
                    totalAffectedItems = candidates.Sum(c => c.OccurrenceCount)
                }
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Project session not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unmatched candidates for session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Override match for specific position (applies to all items with same Name+Unit)
    /// </summary>
    [HttpPost("{id}/match/override")]
    public async Task<ActionResult> OverrideMatch(
        string id, 
        [FromBody] OverrideMatchRequest request)
    {
        try
        {
            var result = await _sessionService.OverrideMatchAsync(id, request.ItemId, request.PriceEntryId);
            
            _logger.LogInformation("Overridden match for item {ItemId} in session {SessionId}", 
                request.ItemId, id);
            
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error overriding match in session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Run optimization for current session
    /// </summary>
    [HttpPost("{id}/optimize")]
    public async Task<ActionResult> RunOptimization(string id)
    {
        try
        {
            var result = await _sessionService.RunOptimizationAsync(id);
            
            _logger.LogInformation("Optimization complete for session {SessionId}: Gap = {Gap:F2} лв", 
                id, result.OverallGap);
            
            // Transform to DTO matching frontend expectations
            var dto = MapIterationResultToDto(result);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running optimization for session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get latest iteration result
    /// </summary>
    [HttpGet("{id}/iterations/latest")]
    public async Task<ActionResult> GetLatestIteration(string id)
    {
        try
        {
            var result = await _sessionService.GetLatestIterationAsync(id);
            
            if (result == null)
            {
                return NotFound(new { error = "No optimization run yet" });
            }
            
            // Transform to DTO matching frontend expectations
            var dto = MapIterationResultToDto(result);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Project session not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest iteration for session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get currently selected iteration result (for export)
    /// </summary>
    [HttpGet("{id}/iterations/selected")]
    public async Task<ActionResult> GetSelectedIteration(string id)
    {
        try
        {
            var result = await _sessionService.GetSelectedIterationAsync(id);
            
            if (result == null)
            {
                return NotFound(new { error = "No optimization run yet" });
            }
            
            // Transform to DTO matching frontend expectations
            var dto = MapIterationResultToDto(result);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Project session not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting selected iteration for session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all iterations for a project
    /// </summary>
    [HttpGet("{id}/iterations")]
    public async Task<ActionResult> GetAllIterations(string id)
    {
        try
        {
            var iterations = await _sessionService.GetAllIterationsAsync(id);
            var dtos = iterations.Select(MapIterationResultToDto).ToList();
            return Ok(dtos);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Project session not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting iterations for session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Select a specific iteration for export
    /// </summary>
    [HttpPost("{id}/iterations/{iterationNumber}/select")]
    public async Task<ActionResult> SelectIteration(string id, int iterationNumber)
    {
        try
        {
            var result = await _sessionService.SelectIterationAsync(id, iterationNumber);
            var dto = MapIterationResultToDto(result);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting iteration {Iteration} for session {SessionId}", iterationNumber, id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Export results as ZIP with all 19 КСС files
    /// </summary>
    [HttpGet("{id}/export")]
    public async Task<ActionResult> ExportResults(string id)
    {
        try
        {
            var zipBytes = await _sessionService.ExportResultsAsync(id);
            var fileName = $"KSS_Results_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
            
            _logger.LogInformation("Exported results for session {SessionId}", id);
            
            return File(zipBytes, "application/zip", fileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting results for session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Export single file preview
    /// </summary>
    [HttpGet("{id}/export/preview/{fileId}")]
    public async Task<ActionResult> ExportPreview(string id, string fileId)
    {
        try
        {
            var excelBytes = await _sessionService.ExportSingleFileAsync(id, fileId);
            var fileName = $"Preview_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            
            return File(excelBytes, 
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                fileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting preview for session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }
    
    private static IterationResultDto MapIterationResultToDto(IterationResult result)
    {
        // Aggregate stage results across all files
        var stageAggregates = new Dictionary<string, (decimal Proposed, decimal Forecast)>();
        
        foreach (var boqResult in result.BoqResults.Values)
        {
            foreach (var stage in boqResult.Stages)
            {
                if (!stageAggregates.ContainsKey(stage.StageCode))
                {
                    stageAggregates[stage.StageCode] = (0m, 0m);
                }
                
                var current = stageAggregates[stage.StageCode];
                stageAggregates[stage.StageCode] = (
                    current.Proposed + stage.Proposed,
                    current.Forecast + stage.Forecast
                );
            }
        }
        
        return new IterationResultDto
        {
            IterationId = result.IterationNumber.ToString(),
            IterationNumber = result.IterationNumber,
            Timestamp = result.Timestamp.ToString("o"),
            OverallGap = result.OverallGap,
            TotalProposed = result.OverallProposed,
            TotalForecast = result.OverallForecast,
            StageBreakdown = stageAggregates.Select(kvp => new StageBreakdownDto
            {
                Stage = kvp.Key,
                Gap = kvp.Value.Forecast - kvp.Value.Proposed,
                Proposed = kvp.Value.Proposed,
                Forecast = kvp.Value.Forecast
            }).ToList(),
            FileBreakdown = result.BoqResults.Select(kvp => new FileBreakdownDto
            {
                FileName = kvp.Value.FileName,
                TotalProposed = kvp.Value.TotalProposed,
                StageGaps = kvp.Value.Stages.ToDictionary(
                    s => s.StageCode,
                    s => s.Gap
                )
            }).ToList(),
            SolverTimeMs = result.SolveDurationMs
        };
    }
}

public record CreateProjectRequest
{
    public required string ObjectName { get; init; }
    public required string Employee { get; init; }
    public DateTime Date { get; init; } = DateTime.Now;
}

public record ProjectSessionResponse
{
    public required string ProjectId { get; init; }
    public required ProjectMetadata Metadata { get; init; }
    public required int KssFilesCount { get; init; }
    public required int UkazaniaFilesCount { get; init; }
    public required int PriceBaseFilesCount { get; init; }
    public required bool HasTemplate { get; init; }
    public required bool HasMatchingResults { get; init; }
    public required bool HasOptimizationResults { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public record ProjectMetadata
{
    public required string ObjectName { get; init; }
    public required string Employee { get; init; }
    public required string Date { get; init; }
}

public record OverrideMatchRequest
{
    public required string ItemId { get; init; }
    public required string PriceEntryId { get; init; }
}

public record UnmatchedCandidateDto
{
    public required string UnifiedKey { get; init; }
    public required string ItemName { get; init; }
    public required string ItemUnit { get; init; }
    public required int OccurrenceCount { get; init; }
    public required List<TopCandidateDto> TopCandidates { get; init; }
}

public record TopCandidateDto
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required decimal Price { get; init; }
    public required decimal Score { get; init; }
}

public record IterationResultDto
{
    public required string IterationId { get; init; }
    public required int IterationNumber { get; init; }
    public required string Timestamp { get; init; }
    public required decimal OverallGap { get; init; }
    public required decimal TotalProposed { get; init; }
    public required decimal TotalForecast { get; init; }
    public required List<StageBreakdownDto> StageBreakdown { get; init; }
    public required List<FileBreakdownDto> FileBreakdown { get; init; }
    public required long SolverTimeMs { get; init; }
}

public record StageBreakdownDto
{
    public required string Stage { get; init; }
    public required decimal Gap { get; init; }
    public required decimal Proposed { get; init; }
    public required decimal Forecast { get; init; }
}

public record FileBreakdownDto
{
    public required string FileName { get; init; }
    public required decimal TotalProposed { get; init; }
    public required Dictionary<string, decimal> StageGaps { get; init; }
}

public record ManualForecastsRequest
{
    public required Dictionary<string, decimal> Forecasts { get; init; }
}

public record StageInfo
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required int ItemCount { get; init; }
}
