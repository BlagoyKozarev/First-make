using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Service for tracking operations, metrics, and telemetry
/// </summary>
public class ObservationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ObservationService> _logger;

    public ObservationService(AppDbContext db, ILogger<ObservationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Log an operation observation with metrics
    /// </summary>
    public async Task<Guid> LogOperationAsync(OperationObservation observation)
    {
        var id = Guid.NewGuid();
        var metadata = JsonSerializer.Serialize(observation);

        // Check for duplicates using input hash
        if (!string.IsNullOrEmpty(observation.InputHash))
        {
            var existing = await _db.SessionData
                .Where(s => s.BoqDataJson.Contains(observation.InputHash))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (existing != null && (DateTime.UtcNow - existing.CreatedAt).TotalMinutes < 60)
            {
                _logger.LogInformation(
                    "Duplicate operation detected. Hash: {Hash}, Original session: {SessionId}",
                    observation.InputHash, existing.SessionId
                );
                observation.IsDuplicate = true;
                observation.OriginalSessionId = existing.SessionId;
            }
        }

        // Store in session data
        var session = new SessionData
        {
            SessionId = id.ToString(),
            SourceFileName = observation.SourceFileName ?? "unknown",
            BoqDataJson = metadata,
            CreatedAt = DateTime.UtcNow
        };

        _db.SessionData.Add(session);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Operation logged: {Operation} ({Duration}ms, Success: {Success})",
            observation.OperationType, observation.DurationMs, observation.Success
        );

        return id;
    }

    /// <summary>
    /// Compute SHA256 hash of input data for duplicate detection
    /// </summary>
    public string ComputeInputHash(object input)
    {
        var json = JsonSerializer.Serialize(input);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Get aggregated metrics for dashboard
    /// </summary>
    public async Task<ObservationMetrics> GetMetricsAsync(DateTime? since = null)
    {
        var cutoff = since ?? DateTime.UtcNow.AddDays(-7);

        var sessions = await _db.SessionData
            .Where(s => s.CreatedAt >= cutoff)
            .ToListAsync();

        var observations = sessions
            .Select(s =>
            {
                try
                {
                    return JsonSerializer.Deserialize<OperationObservation>(s.BoqDataJson);
                }
                catch
                {
                    return null;
                }
            })
            .Where(o => o != null)
            .ToList();

        var successfulOps = observations.Where(o => o!.Success).ToList();
        var failedOps = observations.Where(o => !o!.Success).ToList();

        var metrics = new ObservationMetrics
        {
            TotalOperations = observations.Count,
            SuccessfulOperations = successfulOps.Count,
            FailedOperations = failedOps.Count,
            DuplicateOperations = observations.Count(o => o!.IsDuplicate),
            AverageDurationMs = observations.Any() 
                ? observations.Average(o => o!.DurationMs) 
                : 0,
            OperationsByType = observations
                .GroupBy(o => o!.OperationType)
                .ToDictionary(g => g.Key, g => g.Count()),
            Since = cutoff,
            Until = DateTime.UtcNow
        };

        // Extract operation-specific metrics
        var matchOps = observations
            .Where(o => o!.OperationType == "Match")
            .Select(o => o!.Metadata)
            .Where(m => m != null)
            .ToList();

        if (matchOps.Any())
        {
            metrics.AverageMatchScore = matchOps
                .Where(m => m.ContainsKey("averageScore"))
                .Select(m => Convert.ToDouble(m["averageScore"]))
                .DefaultIfEmpty(0)
                .Average();

            metrics.AverageMatchCandidates = matchOps
                .Where(m => m.ContainsKey("totalCandidates"))
                .Select(m => Convert.ToInt32(m["totalCandidates"]))
                .DefaultIfEmpty(0)
                .Average();
        }

        var optimizeOps = observations
            .Where(o => o!.OperationType == "Optimize")
            .Select(o => o!.Metadata)
            .Where(m => m != null)
            .ToList();

        if (optimizeOps.Any())
        {
            metrics.AverageOptimizationObjective = optimizeOps
                .Where(m => m.ContainsKey("objective"))
                .Select(m => Convert.ToDouble(m["objective"]))
                .DefaultIfEmpty(0)
                .Average();

            metrics.OptimizationSuccessRate = optimizeOps
                .Where(m => m.ContainsKey("solverStatus") && m["solverStatus"]?.ToString() == "OPTIMAL")
                .Count() / (double)optimizeOps.Count;
        }

        return metrics;
    }

    /// <summary>
    /// Get recent observations for audit trail
    /// </summary>
    public async Task<List<OperationObservation>> GetRecentObservationsAsync(int limit = 100)
    {
        var sessions = await _db.SessionData
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var observations = new List<OperationObservation>();

        foreach (var session in sessions)
        {
            try
            {
                var obs = JsonSerializer.Deserialize<OperationObservation>(session.BoqDataJson);
                if (obs != null)
                {
                    observations.Add(obs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize observation from session {SessionId}", session.SessionId);
            }
        }

        return observations;
    }

    /// <summary>
    /// Clean up old observations (data retention)
    /// </summary>
    public async Task<int> CleanupOldObservationsAsync(int retentionDays = 90)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        
        var oldSessions = await _db.SessionData
            .Where(s => s.CreatedAt < cutoff)
            .ToListAsync();

        _db.SessionData.RemoveRange(oldSessions);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Cleaned up {Count} old observations older than {Days} days", 
            oldSessions.Count, retentionDays);

        return oldSessions.Count;
    }
}

/// <summary>
/// Operation observation record
/// </summary>
public class OperationObservation
{
    public required string OperationType { get; set; } // "Parse", "Extract", "Match", "Optimize", "Export"
    public required bool Success { get; set; }
    public required long DurationMs { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? SourceFileName { get; set; }
    public string? InputHash { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public bool IsDuplicate { get; set; }
    public string? OriginalSessionId { get; set; }
}

/// <summary>
/// Aggregated metrics
/// </summary>
public class ObservationMetrics
{
    public int TotalOperations { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public int DuplicateOperations { get; set; }
    public double AverageDurationMs { get; set; }
    public Dictionary<string, int> OperationsByType { get; set; } = new();
    public double? AverageMatchScore { get; set; }
    public double? AverageMatchCandidates { get; set; }
    public double? AverageOptimizationObjective { get; set; }
    public double? OptimizationSuccessRate { get; set; }
    public DateTime Since { get; set; }
    public DateTime Until { get; set; }
}
