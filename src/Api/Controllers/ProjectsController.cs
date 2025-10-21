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
    public async Task<ActionResult<ProjectSession>> CreateProject([FromBody] CreateProjectRequest request)
    {
        try
        {
            var session = await _sessionService.CreateSessionAsync(
                request.ObjectName,
                request.Employee,
                request.Date
            );

            _logger.LogInformation("Created project session {SessionId}", session.Id);
            return CreatedAtAction(nameof(GetProject), new { id = session.Id }, session);
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
    public async Task<ActionResult<ProjectSession>> GetProject(string id)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(id);
            if (session == null)
            {
                return NotFound(new { error = "Project session not found" });
            }

            return Ok(session);
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

            if (files.Count > 25)
            {
                return BadRequest(new { error = "Maximum 25 КСС files allowed" });
            }

            var result = await _sessionService.UploadKssFilesAsync(id, files);
            
            _logger.LogInformation("Uploaded {Count} КСС files to session {SessionId}", files.Count, id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Project session not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading КСС files to session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Upload Указания files
    /// </summary>
    [HttpPost("{id}/files/ukazania")]
    [RequestSizeLimit(50_000_000)] // 50MB
    public async Task<ActionResult> UploadUkazaniaFiles(string id, [FromForm] List<IFormFile> files)
    {
        try
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { error = "No files provided" });
            }

            if (files.Count > 2)
            {
                return BadRequest(new { error = "Maximum 2 Указания files allowed" });
            }

            var result = await _sessionService.UploadUkazaniaFilesAsync(id, files);
            
            _logger.LogInformation("Uploaded {Count} Указания files to session {SessionId}", files.Count, id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Project session not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading Указания files to session {SessionId}", id);
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
            
            return Ok(result);
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
            
            return Ok(new
            {
                candidates,
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
    public async Task<ActionResult<IterationResult>> RunOptimization(string id)
    {
        try
        {
            var result = await _sessionService.RunOptimizationAsync(id);
            
            _logger.LogInformation("Optimization complete for session {SessionId}: Gap = {Gap:F2} лв", 
                id, result.OverallGap);
            
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
            _logger.LogError(ex, "Error running optimization for session {SessionId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get latest iteration result
    /// </summary>
    [HttpGet("{id}/iterations/latest")]
    public async Task<ActionResult<IterationResult>> GetLatestIteration(string id)
    {
        try
        {
            var result = await _sessionService.GetLatestIterationAsync(id);
            
            if (result == null)
            {
                return NotFound(new { error = "No optimization run yet" });
            }
            
            return Ok(result);
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
}

public record CreateProjectRequest
{
    public required string ObjectName { get; init; }
    public required string Employee { get; init; }
    public DateTime Date { get; init; } = DateTime.Now;
}

public record OverrideMatchRequest
{
    public required string ItemId { get; init; }
    public required string PriceEntryId { get; init; }
}
