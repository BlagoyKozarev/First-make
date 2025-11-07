using Core.Engine.Models;
using Core.Engine.Services;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

namespace Api.Services;

/// <summary>
/// Manages project sessions with file uploads and processing
/// </summary>
public class ProjectSessionService
{
    private readonly ConcurrentDictionary<string, ProjectSession> _sessions = new();
    private readonly ConcurrentDictionary<string, UnifiedMatcher> _matchers = new();
    private readonly ConcurrentDictionary<string, UnifiedMatchResult> _matchResults = new();
    private readonly ConcurrentDictionary<string, object> _sessionLocks = new();
    private readonly string _uploadsPath;
    private readonly MultiFileKssParser _kssParser;
    private readonly PriceBaseLoader _priceBaseLoader;
    private readonly UkazaniaParser _ukazaniaParser;
    private readonly ForecastExcelParser _forecastParser;
    private readonly FuzzyMatcher _fuzzyMatcher;
    private readonly MultiFileOptimizer _optimizer;
    private readonly MultiFileExcelExporter _exporter;
    private readonly ILogger<ProjectSessionService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public ProjectSessionService(
        IConfiguration configuration,
        FuzzyMatcher fuzzyMatcher,
        ILogger<ProjectSessionService> logger,
        ILoggerFactory loggerFactory)
    {
        _uploadsPath = configuration["UploadsPath"] ?? Path.Combine(Path.GetTempPath(), "firstmake-uploads");
        Directory.CreateDirectory(_uploadsPath);
        
        _kssParser = new MultiFileKssParser();
        _priceBaseLoader = new PriceBaseLoader();
        _ukazaniaParser = new UkazaniaParser();
        _forecastParser = new ForecastExcelParser();
        _fuzzyMatcher = fuzzyMatcher;
        _optimizer = new MultiFileOptimizer(loggerFactory.CreateLogger<MultiFileOptimizer>());
        _exporter = new MultiFileExcelExporter(
            loggerFactory.CreateLogger<MultiFileExcelExporter>(),
            loggerFactory.CreateLogger<PriceCheckExporter>());
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public Task<ProjectSession> CreateSessionAsync(string objectName, string employee, DateTime date)
    {
        var sessionId = Guid.NewGuid().ToString();
        var sessionPath = Path.Combine(_uploadsPath, sessionId);
        Directory.CreateDirectory(sessionPath);

        var session = new ProjectSession
        {
            Id = sessionId,
            ObjectName = objectName,
            Employee = employee,
            Date = date,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _sessions[sessionId] = session;
        
        return Task.FromResult(session);
    }

    public Task<ProjectSession?> GetSessionAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public async Task<UploadResult> UploadKssFilesAsync(string sessionId, List<IFormFile> files)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        var sessionLock = _sessionLocks.GetOrAdd(sessionId, _ => new object());

        var sessionPath = Path.Combine(_uploadsPath, sessionId, "kss");
        Directory.CreateDirectory(sessionPath);

        var fileMetadataList = new List<FileMetadata>();
        var filePaths = new List<(string FilePath, string FileId)>();

        foreach (var file in files)
        {
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Invalid file type: {file.FileName}. Only .xlsx files allowed.");
            }

            var fileId = Guid.NewGuid().ToString();
            var filePath = Path.Combine(sessionPath, $"{fileId}_{file.FileName}");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var metadata = new FileMetadata
            {
                Id = fileId,
                FileName = file.FileName,
                OriginalPath = filePath,
                SizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow
            };

            fileMetadataList.Add(metadata);
            filePaths.Add((filePath, fileId));
        }

        // Parse КСС files
        _logger.LogInformation("Parsing {Count} КСС files for session {SessionId}", files.Count, sessionId);
        var boqDocuments = _kssParser.ParseMultipleFiles(filePaths);

        // Update session with lock
        lock (sessionLock)
        {
            // Re-fetch session inside lock
            if (!_sessions.TryGetValue(sessionId, out session))
            {
                throw new KeyNotFoundException("Session not found");
            }

            var updatedSession = session with
            {
                KssFiles = session.KssFiles.Concat(fileMetadataList).ToList(),
                BoqDocuments = session.BoqDocuments.Concat(boqDocuments).ToList(),
                UpdatedAt = DateTime.UtcNow
            };

            _sessions[sessionId] = updatedSession;
        }

        return new UploadResult
        {
            Success = true,
            FilesUploaded = files.Count,
            ItemsParsed = boqDocuments.Sum(d => d.Items.Count),
            Message = $"Successfully uploaded and parsed {files.Count} КСС files"
        };
    }

    /// <summary>
    /// Upload forecast Excel files containing stage budgets
    /// Expected format: Column A = Stage Code, Column B = Forecast Value
    /// </summary>
    public async Task<UploadResult> UploadForecastFilesAsync(string sessionId, List<IFormFile> files)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        var sessionLock = _sessionLocks.GetOrAdd(sessionId, _ => new object());

        var sessionPath = Path.Combine(_uploadsPath, sessionId, "forecasts");
        Directory.CreateDirectory(sessionPath);

        var fileMetadataList = new List<FileMetadata>();
        StageForecasts? forecasts = null;

        foreach (var file in files)
        {
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) &&
                !file.FileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Невалиден тип файл: {file.FileName}. Позволени са само .xlsx/.xls файлове.");
            }

            var fileId = Guid.NewGuid().ToString();
            var filePath = Path.Combine(sessionPath, $"{fileId}_{file.FileName}");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var metadata = new FileMetadata
            {
                Id = fileId,
                FileName = file.FileName,
                OriginalPath = filePath,
                SizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow
            };

            fileMetadataList.Add(metadata);

            // Parse forecast Excel file (use first file only)
            if (forecasts == null)
            {
                _logger.LogInformation("Parsing forecast Excel file {FileName} for session {SessionId}", file.FileName, sessionId);
                try
                {
                    forecasts = _forecastParser.ParseForecastFile(filePath);
                    _logger.LogInformation("Parsed {Count} stage forecasts from {FileName}", forecasts.Stages.Count, file.FileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse forecast Excel file {FileName}", file.FileName);
                    throw new InvalidOperationException($"Грешка при парсване на файла с прогнози: {ex.Message}");
                }
            }
        }

        // Update session with lock
        lock (sessionLock)
        {
            if (!_sessions.TryGetValue(sessionId, out session))
            {
                throw new KeyNotFoundException("Session not found");
            }

            var updatedSession = session with
            {
                InstructionsFiles = session.InstructionsFiles.Concat(fileMetadataList).ToList(),
                Forecasts = forecasts ?? session.Forecasts,
                UpdatedAt = DateTime.UtcNow
            };

            _sessions[sessionId] = updatedSession;
        }

        return new UploadResult
        {
            Success = true,
            FilesUploaded = files.Count,
            ItemsParsed = forecasts?.Stages.Count ?? 0,
            Message = $"Успешно качени {files.Count} файл(а) с прогнози, извлечени {forecasts?.Stages.Count ?? 0} прогнози по етапи"
        };
    }

    /// <summary>
    /// DEPRECATED: Old method for Ukazania Word files - kept for backward compatibility
    /// Use UploadForecastFilesAsync instead
    /// </summary>
    [Obsolete("Use UploadForecastFilesAsync instead")]
    public async Task<UploadResult> UploadUkazaniaFilesAsync(string sessionId, List<IFormFile> files)
    {
        // Redirect to new forecast upload method
        return await UploadForecastFilesAsync(sessionId, files);
    }

    public async Task<UploadResult> UploadPriceBaseFilesAsync(string sessionId, List<IFormFile> files)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        var sessionLock = _sessionLocks.GetOrAdd(sessionId, _ => new object());

        var sessionPath = Path.Combine(_uploadsPath, sessionId, "pricebase");
        Directory.CreateDirectory(sessionPath);

        var fileMetadataList = new List<FileMetadata>();
        var filePaths = new List<(string FilePath, string FileId)>();

        foreach (var file in files)
        {
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Invalid file type: {file.FileName}. Only .xlsx files allowed.");
            }

            var fileId = Guid.NewGuid().ToString();
            var filePath = Path.Combine(sessionPath, $"{fileId}_{file.FileName}");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var metadata = new FileMetadata
            {
                Id = fileId,
                FileName = file.FileName,
                OriginalPath = filePath,
                SizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow
            };

            fileMetadataList.Add(metadata);
            filePaths.Add((filePath, fileId));
        }

        // Parse Price Base files
        _logger.LogInformation("Parsing {Count} Price Base files for session {SessionId}", files.Count, sessionId);
        var priceBase = _priceBaseLoader.LoadFromMultipleFiles(filePaths);

        // Update session with lock
        lock (sessionLock)
        {
            if (!_sessions.TryGetValue(sessionId, out session))
            {
                throw new KeyNotFoundException("Session not found");
            }

            var updatedSession = session with
            {
                PriceBaseFiles = session.PriceBaseFiles.Concat(fileMetadataList).ToList(),
                PriceBase = priceBase,
                UpdatedAt = DateTime.UtcNow
            };

            _sessions[sessionId] = updatedSession;
        }

        return new UploadResult
        {
            Success = true,
            FilesUploaded = files.Count,
            ItemsParsed = priceBase.Count,
            Message = $"Successfully uploaded and parsed {files.Count} Price Base file(s), loaded {priceBase.Count} price entries"
        };
    }

    public async Task<UploadResult> UploadTemplateFileAsync(string sessionId, IFormFile file)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        var sessionLock = _sessionLocks.GetOrAdd(sessionId, _ => new object());

        var sessionPath = Path.Combine(_uploadsPath, sessionId, "template");
        Directory.CreateDirectory(sessionPath);

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Invalid file type: {file.FileName}. Only .xlsx files allowed.");
        }

        var fileId = Guid.NewGuid().ToString();
        var filePath = Path.Combine(sessionPath, $"{fileId}_{file.FileName}");

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var metadata = new FileMetadata
        {
            Id = fileId,
            FileName = file.FileName,
            OriginalPath = filePath,
            SizeBytes = file.Length,
            UploadedAt = DateTime.UtcNow
        };

        // Update session with lock
        lock (sessionLock)
        {
            if (!_sessions.TryGetValue(sessionId, out session))
            {
                throw new KeyNotFoundException("Session not found");
            }

            var updatedSession = session with
            {
                TemplateFile = metadata,
                UpdatedAt = DateTime.UtcNow
            };

            _sessions[sessionId] = updatedSession;
        }

        return new UploadResult
        {
            Success = true,
            FilesUploaded = 1,
            ItemsParsed = 0,
            Message = "Successfully uploaded template file"
        };
    }

    public Task<bool> DeleteSessionAsync(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out _))
        {
            return Task.FromResult(false);
        }

        // Delete uploaded files
        var sessionPath = Path.Combine(_uploadsPath, sessionId);
        if (Directory.Exists(sessionPath))
        {
            try
            {
                Directory.Delete(sessionPath, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete session directory {SessionPath}", sessionPath);
            }
        }

        return Task.FromResult(true);
    }

    public Task<object> SetManualForecastsAsync(string sessionId, Dictionary<string, decimal> forecasts)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        var sessionLock = _sessionLocks.GetOrAdd(sessionId, _ => new object());

        lock (sessionLock)
        {
            if (!_sessions.TryGetValue(sessionId, out session))
            {
                throw new KeyNotFoundException("Session not found");
            }

            var stageForecastsDict = forecasts.ToDictionary(
                kvp => kvp.Key,
                kvp => new StageForecast
                {
                    Code = kvp.Key,
                    Name = $"Етап {kvp.Key}",
                    Forecast = kvp.Value
                }
            );

            var updatedSession = session with
            {
                Forecasts = new StageForecasts
                {
                    Stages = stageForecastsDict,
                    TotalForecast = forecasts.Values.Sum(),
                    SourceFileId = "manual-input"
                },
                UpdatedAt = DateTime.UtcNow
            };

            _sessions[sessionId] = updatedSession;
        }

        return Task.FromResult<object>(new
        {
            success = true,
            stagesCount = forecasts.Count,
            totalForecast = forecasts.Values.Sum(),
            message = $"Въведени са прогнозни стойности за {forecasts.Count} етапа"
        });
    }

    public Task<List<object>> GetAvailableStagesAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        if (session.BoqDocuments.Count == 0)
        {
            throw new InvalidOperationException("No КСС files uploaded yet");
        }

        // Get all unique stages from all BOQ documents
        var stageGroups = session.BoqDocuments
            .SelectMany(doc => doc.Items)
            .GroupBy(item => item.StageCode)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                code = g.Key,
                name = $"Етап {g.Key}",
                itemCount = g.Count(),
                currentForecast = session.Forecasts?.Stages.GetValueOrDefault(g.Key)?.Forecast
            })
            .Cast<object>()
            .ToList();

        return Task.FromResult(stageGroups);
    }

    public Task<UnifiedMatchResult> TriggerMatchingAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        if (session.BoqDocuments.Count == 0)
        {
            throw new InvalidOperationException("No КСС files uploaded yet");
        }

        if (session.PriceBase.Count == 0)
        {
            throw new InvalidOperationException("No Price Base uploaded yet");
        }

        // Create or get matcher for this session
        var matcher = _matchers.GetOrAdd(sessionId, _ => new UnifiedMatcher(_fuzzyMatcher));

        // Perform unified matching
        _logger.LogInformation("Starting unified matching for session {SessionId}: {BoqCount} BOQ files, {PriceCount} price entries",
            sessionId, session.BoqDocuments.Count, session.PriceBase.Count);

        var result = matcher.MatchAll(session.BoqDocuments, session.PriceBase);

        // Store result
        _matchResults[sessionId] = result;

        _logger.LogInformation("Matching complete: {Matched}/{Total} items matched, {Unique} unique positions",
            result.Statistics.MatchedItems, result.Statistics.TotalItems, result.Statistics.UniquePositions);

        return Task.FromResult(result);
    }

    public Task<List<UnifiedCandidate>> GetUnmatchedCandidatesAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        if (!_matchResults.TryGetValue(sessionId, out var matchResult))
        {
            throw new InvalidOperationException("No matching performed yet. Call /match first.");
        }

        if (!_matchers.TryGetValue(sessionId, out var matcher))
        {
            throw new InvalidOperationException("Matcher not initialized");
        }

        var candidates = matcher.GetUnmatchedCandidates(matchResult, session.PriceBase);

        return Task.FromResult(candidates);
    }

    public Task<UnifiedMatchResult> OverrideMatchAsync(string sessionId, string itemId, string priceEntryId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        if (!_matchResults.TryGetValue(sessionId, out var matchResult))
        {
            throw new InvalidOperationException("No matching performed yet. Call /match first.");
        }

        if (!_matchers.TryGetValue(sessionId, out var matcher))
        {
            throw new InvalidOperationException("Matcher not initialized");
        }

        // Find price entry
        var priceEntry = session.PriceBase.FirstOrDefault(p => 
            $"{p.Name}|{p.Unit}".GetHashCode().ToString() == priceEntryId ||
            p.SourceFileId == priceEntryId);

        if (priceEntry == null)
        {
            // Try to find by source row (alternative ID)
            priceEntry = session.PriceBase.FirstOrDefault(p => p.SourceRow.ToString() == priceEntryId);
        }

        if (priceEntry == null)
        {
            throw new KeyNotFoundException($"Price entry {priceEntryId} not found");
        }

        // Override match (affects all items with same Name+Unit)
        matcher.OverrideMatch(itemId, priceEntry, matchResult);

        _logger.LogInformation("Overridden match for item {ItemId} to price entry {PriceEntry}",
            itemId, $"{priceEntry.Name} ({priceEntry.Unit})");

        return Task.FromResult(matchResult);
    }

    public Task<IterationResult> RunOptimizationAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        var sessionLock = _sessionLocks.GetOrAdd(sessionId, _ => new object());

        if (!_matchResults.TryGetValue(sessionId, out var matchResult))
        {
            throw new InvalidOperationException("No matching performed yet. Call /match first.");
        }

        // If no forecasts, generate dummy forecasts based on total values
        if (session.Forecasts == null)
        {
            _logger.LogWarning("No forecasts available for session {SessionId}. Generating dummy forecasts based on total values.", sessionId);
            
            // Calculate total value per stage
            var stageTotals = new Dictionary<string, decimal>();
            foreach (var itemMatch in matchResult.ItemMatches.Values)
            {
                if (itemMatch.PriceEntry != null)
                {
                    var stageCode = itemMatch.Item.StageCode;
                    var value = itemMatch.Item.Quantity * itemMatch.PriceEntry.BasePrice;
                    stageTotals[stageCode] = stageTotals.GetValueOrDefault(stageCode) + value;
                }
            }
            
            // Create dummy forecasts with 120% of calculated values (to allow headroom)
            var dummyStages = stageTotals.ToDictionary(
                kvp => kvp.Key,
                kvp => new StageForecast
                {
                    Code = kvp.Key,
                    Name = $"Stage {kvp.Key}",
                    Forecast = kvp.Value * 1.2m
                }
            );
            
            session = session with
            {
                Forecasts = new StageForecasts
                {
                    Stages = dummyStages,
                    TotalForecast = stageTotals.Values.Sum() * 1.2m,
                    SourceFileId = "auto-generated"
                }
            };
            
            _sessions[sessionId] = session;
        }

        // Determine iteration number
        var iterationNumber = session.Iterations.Count + 1;
        var previousIteration = session.Iterations.LastOrDefault();

        _logger.LogInformation("Starting optimization iteration {Iteration} for session {SessionId}",
            iterationNumber, sessionId);

        // Run optimization (pass previous iteration for adaptive tuning)
        var result = _optimizer.OptimizeMultiFile(session, matchResult, iterationNumber, previousIteration);

        // Update session with new iteration with lock
        lock (sessionLock)
        {
            if (!_sessions.TryGetValue(sessionId, out session))
            {
                throw new KeyNotFoundException("Session not found");
            }

            var updatedSession = session with
            {
                Iterations = session.Iterations.Append(result).ToList(),
                // Auto-select first iteration, but don't change selection for subsequent iterations
                CurrentIteration = session.CurrentIteration == 0 ? 1 : session.CurrentIteration,
                UpdatedAt = DateTime.UtcNow
            };

            _sessions[sessionId] = updatedSession;
        }

        _logger.LogInformation("Optimization complete: Gap = {Gap:F2} лв, Solver time = {Time}ms",
            result.OverallGap, result.SolveDurationMs);

        return Task.FromResult(result);
    }

    public Task<IterationResult?> GetLatestIterationAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        var latest = session.Iterations.LastOrDefault();
        return Task.FromResult(latest);
    }

    public Task<IterationResult?> GetSelectedIterationAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        // Return the currently selected iteration (or latest if none selected)
        var selected = session.CurrentIteration > 0
            ? session.Iterations.FirstOrDefault(i => i.IterationNumber == session.CurrentIteration)
            : session.Iterations.LastOrDefault();
            
        return Task.FromResult(selected);
    }

    public Task<List<IterationResult>> GetAllIterationsAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        return Task.FromResult(session.Iterations.ToList());
    }

    public Task<IterationResult> SelectIterationAsync(string sessionId, int iterationNumber)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        var iteration = session.Iterations.FirstOrDefault(i => i.IterationNumber == iterationNumber);
        if (iteration == null)
        {
            throw new ArgumentException($"Iteration {iterationNumber} not found");
        }

        // Update session to mark this as the selected iteration for export
        var sessionLock = _sessionLocks.GetOrAdd(sessionId, _ => new object());
        lock (sessionLock)
        {
            var updatedSession = session with
            {
                CurrentIteration = iterationNumber,
                UpdatedAt = DateTime.UtcNow
            };
            _sessions[sessionId] = updatedSession;
        }

        _logger.LogInformation("Selected iteration {Iteration} for session {SessionId}", iterationNumber, sessionId);
        return Task.FromResult(iteration);
    }

    public async Task<byte[]> ExportResultsAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        if (!_matchResults.TryGetValue(sessionId, out var matchResult))
        {
            throw new InvalidOperationException("No matching performed yet");
        }

        // Use the currently selected iteration (or latest if not selected)
        var selectedIteration = session.CurrentIteration > 0
            ? session.Iterations.FirstOrDefault(i => i.IterationNumber == session.CurrentIteration)
            : session.Iterations.LastOrDefault();
            
        if (selectedIteration == null)
        {
            throw new InvalidOperationException("No optimization run yet");
        }

        _logger.LogInformation("Exporting {Count} КСС files for session {SessionId}, iteration {Iteration}",
            session.BoqDocuments.Count, sessionId, selectedIteration.IterationNumber);

        return await _exporter.ExportToZipAsync(session, selectedIteration, matchResult);
    }

    public async Task<byte[]> ExportSingleFileAsync(string sessionId, string fileId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Session not found");
        }

        if (!_matchResults.TryGetValue(sessionId, out var matchResult))
        {
            throw new InvalidOperationException("No matching performed yet");
        }

        // Use the currently selected iteration (or latest if not selected)
        var selectedIteration = session.CurrentIteration > 0
            ? session.Iterations.FirstOrDefault(i => i.IterationNumber == session.CurrentIteration)
            : session.Iterations.LastOrDefault();
            
        if (selectedIteration == null)
        {
            throw new InvalidOperationException("No optimization run yet");
        }

        var doc = session.BoqDocuments.FirstOrDefault(d => d.SourceFileId == fileId);
        if (doc == null)
        {
            throw new KeyNotFoundException($"BOQ file {fileId} not found");
        }

        return await _exporter.ExportSingleFileAsync(doc, selectedIteration, matchResult, session);
    }
}

public record UploadResult
{
    public required bool Success { get; init; }
    public required int FilesUploaded { get; init; }
    public required int ItemsParsed { get; init; }
    public required string Message { get; init; }
}
