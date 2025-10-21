using Core.Engine.Models;
using Core.Engine.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Api.Data;
using Api.Services;
using Api.Middleware;
using Api.Validation;

var builder = WebApplication.CreateBuilder(args);

// Add SQLite database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=firstmake.db"));

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "FirstMake API",
        Version = "v1",
        Description = "Local-first BoQ processing and LP optimization for construction offers"
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register Core.Engine services
var unitsYamlPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "Schemas", "units.yaml");
builder.Services.AddSingleton(new UnitNormalizer(unitsYamlPath));
builder.Services.AddSingleton<FuzzyMatcher>();
builder.Services.AddSingleton<LpOptimizer>();

// Register Api services
builder.Services.AddSingleton<ExcelExportService>();
builder.Services.AddScoped<ObservationService>();
builder.Services.AddSingleton<ProjectSessionService>();

// Add Controllers for new multi-file API
builder.Services.AddControllers();

var app = builder.Build();

// Configure middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestSizeLimitMiddleware>();
app.UseMiddleware<RequestTimeoutMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Map controllers (new multi-file API)
app.MapControllers();

// ========== ENDPOINTS ==========

// Health check
app.MapGet("/healthz", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = "1.0.0"
}))
.WithName("HealthCheck")
.WithTags("System")
.Produces<object>(200);

// Parse files (stub - will be implemented in Task 6 with AiGateway)
app.MapPost("/parse", async ([FromForm] IFormFileCollection files) =>
{
    // TODO: Implement deterministic parsing (XLSX, DOCX, PDF) in Task 6
    return Results.Ok(new
    {
        message = "Parse endpoint (stub - will integrate with AiGateway in Task 6)",
        filesReceived = files.Count,
        files = files.Select(f => new { f.FileName, f.Length }).ToList()
    });
})
.WithName("ParseFiles")
.WithTags("Ingestion")
.DisableAntiforgery()
.Produces<object>(200);

// Extract BoQ (stub)
app.MapPost("/extract", async ([FromBody] object request) =>
{
    // TODO: Implement extraction pipeline with AiGateway
    return Results.Ok(new
    {
        message = "Extract endpoint (stub - will implement in Task 6)",
        note = "Will use playbooks/extract.yaml pipeline"
    });
})
.WithName("ExtractBoQ")
.WithTags("Ingestion")
.Produces<object>(200);

// Match items to price base
app.MapPost("/match", async (
    [FromBody] MatchRequest request,
    [FromServices] FuzzyMatcher matcher,
    [FromServices] ObservationService observationService) =>
{
    var startTime = DateTime.UtcNow;
    try
    {
        var matchedItems = new List<MatchedItem>();

        foreach (var item in request.BoQ.Items)
        {
            var candidates = matcher.FindCandidates(item, request.PriceBase, topN: 5);
            
            if (candidates.Any())
            {
                // Take best match
                var best = candidates.First();
                matchedItems.Add(new MatchedItem
                {
                    Item = item,
                    PriceEntry = best.Entry,
                    MatchScore = best.Score,
                    IsManualMatch = false
                });
            }
            else
            {
                // No match found - will need manual resolution
                matchedItems.Add(new MatchedItem
                {
                    Item = item,
                    PriceEntry = new PriceBaseEntry 
                    { 
                        Name = "UNMATCHED", 
                        Unit = item.Unit, 
                        BasePrice = 0 
                    },
                    MatchScore = 0,
                    IsManualMatch = false
                });
            }
        }

        var durationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
        var avgScore = matchedItems.Where(m => m.MatchScore > 0).Average(m => (double?)m.MatchScore) ?? 0;

        // Log observation
        _ = observationService.LogOperationAsync(new OperationObservation
        {
            OperationType = "Match",
            Success = true,
            DurationMs = durationMs,
            SourceFileName = request.BoQ.Project.Name,
            Metadata = new Dictionary<string, object>
            {
                ["totalItems"] = matchedItems.Count,
                ["matchedItems"] = matchedItems.Count(m => m.MatchScore > 0),
                ["unmatchedItems"] = matchedItems.Count(m => m.MatchScore == 0),
                ["averageScore"] = avgScore,
                ["totalCandidates"] = matchedItems.Count * 5
            }
        });

        return Results.Ok(new
        {
            matchedItems,
            summary = new
            {
                total = matchedItems.Count,
                matched = matchedItems.Count(m => m.MatchScore > 0),
                unmatched = matchedItems.Count(m => m.MatchScore == 0)
            }
        });
    }
    catch (Exception ex)
    {
        var durationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
        
        // Log failed observation
        _ = observationService.LogOperationAsync(new OperationObservation
        {
            OperationType = "Match",
            Success = false,
            DurationMs = durationMs,
            ErrorMessage = ex.Message
        });

        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("MatchItems")
.WithTags("Matching")
.Produces<object>(200)
.Produces<object>(400);

// Optimize with LP
app.MapPost("/optimize", async (
    [FromBody] OptimizationRequest request,
    [FromServices] LpOptimizer optimizer) =>
{
    try
    {
        var result = optimizer.Optimize(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("Optimize")
.WithTags("Optimization")
.Produces<OptimizationResult>(200)
.Produces<object>(400);

// Export КСС
app.MapPost("/export", async (
    [FromBody] ExportRequest request,
    [FromServices] ExcelExportService exportService) =>
{
    try
    {
        byte[] fileBytes;
        string fileName;
        string contentType;

        if (request.SplitByStage)
        {
            // Export as ZIP with separate files per stage
            fileBytes = await exportService.ExportToZipAsync(
                request.Result, 
                request.Boq, 
                request.ProjectName ?? "Project"
            );
            fileName = $"{request.ProjectName ?? "Export"}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
            contentType = "application/zip";
        }
        else
        {
            // Export as single XLSX
            fileBytes = await exportService.ExportToExcelAsync(
                request.Result,
                request.Boq,
                request.ProjectName ?? "Project"
            );
            fileName = $"{request.ProjectName ?? "Export"}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        }

        return Results.File(fileBytes, contentType, fileName);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("ExportKSS")
.WithTags("Export")
.Produces<FileResult>(200)
.Produces<object>(400);

// Observations endpoint
app.MapPost("/observations", async (
    [FromBody] OperationObservation observation,
    [FromServices] ObservationService observationService) =>
{
    try
    {
        var id = await observationService.LogOperationAsync(observation);
        return Results.Ok(new
        {
            observationId = id,
            isDuplicate = observation.IsDuplicate,
            originalSessionId = observation.OriginalSessionId
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("LogObservation")
.WithTags("Telemetry")
.Produces<object>(200)
.Produces<object>(400);

// Get metrics
app.MapGet("/observations/metrics", async (
    [FromServices] ObservationService observationService,
    [FromQuery] DateTime? since) =>
{
    try
    {
        var metrics = await observationService.GetMetricsAsync(since);
        return Results.Ok(metrics);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetMetrics")
.WithTags("Telemetry")
.Produces<ObservationMetrics>(200)
.Produces<object>(400);

// Get recent observations
app.MapGet("/observations/recent", async (
    [FromServices] ObservationService observationService,
    [FromQuery] int limit = 100) =>
{
    try
    {
        var observations = await observationService.GetRecentObservationsAsync(limit);
        return Results.Ok(observations);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetRecentObservations")
.WithTags("Telemetry")
.Produces<List<OperationObservation>>(200)
.Produces<object>(400);

app.Run();

// ========== REQUEST/RESPONSE MODELS ==========

record MatchRequest(BoqDto BoQ, List<PriceBaseEntry> PriceBase);
record ExportRequest(
    OptimizationResult Result, 
    BoqDto Boq,
    string? ProjectName,
    bool SplitByStage
);
