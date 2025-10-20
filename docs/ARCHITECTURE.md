# Architecture Guide

Detailed architecture documentation за FirstMake Agent.

## Overview

FirstMake Agent е **local-first, three-tier application** за обработка на КСС документи и LP optimization на строителни оферти.

```
┌─────────────────────────────────────────────────────────────────┐
│                         Presentation Layer                       │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  React 18 SPA (Vite + TypeScript + Tailwind CSS)         │   │
│  │  • Upload Page  • Review Page  • Optimize Page           │   │
│  │  • Export Page  • Metrics Dashboard                      │   │
│  └─────────────┬────────────────────────────────────────────┘   │
│                │ HTTP/JSON                                       │
└────────────────┼───────────────────────────────────────────────┘
                 │
┌────────────────┼───────────────────────────────────────────────┐
│                │           API Layer (.NET 8)                   │
│  ┌─────────────▼─────────────┐    ┌────────────────────────┐   │
│  │     API (Port 5000)       │    │  AiGateway (5001)      │   │
│  │  • REST Endpoints         │    │  • PDF Parser          │   │
│  │  • Request Validation     │◄───┤  • OCR (Tesseract)     │   │
│  │  • Error Handling         │    │  • LLM Integration     │   │
│  │  • Observations Service   │    │  • XLSX/DOCX Parser    │   │
│  └─────────────┬─────────────┘    └────────────────────────┘   │
│                │                                                 │
└────────────────┼─────────────────────────────────────────────────┘
                 │
┌────────────────┼─────────────────────────────────────────────────┐
│                │          Business Logic Layer                    │
│  ┌─────────────▼──────────────────────────────────────────────┐  │
│  │              Core.Engine (.NET 8 Class Library)            │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐ │  │
│  │  │ FuzzyMatcher │  │ LpOptimizer  │  │  Normalizers     │ │  │
│  │  │ • Levenshtein│  │ • OR-Tools   │  │  • Text          │ │  │
│  │  │ • Unit Match │  │ • GLOP Solver│  │  • Unit          │ │  │
│  │  │ • Caching    │  │ • L1 Penalty │  │  • Diacritics    │ │  │
│  │  └──────────────┘  └──────────────┘  └──────────────────┘ │  │
│  └────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                 │
┌────────────────┼─────────────────────────────────────────────────┐
│                │             Data Layer                           │
│  ┌─────────────▼──────────────┐    ┌─────────────────────────┐  │
│  │  SQLite Database           │    │  External Services      │  │
│  │  • CachedMatches           │    │  • BG GPT API           │  │
│  │  • UserApprovals           │    │  • Tesseract OCR        │  │
│  │  • SessionData             │    └─────────────────────────┘  │
│  └────────────────────────────┘                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Component Details

### 1. Presentation Layer (UI)

**Technology Stack:**
- **Framework**: React 18.3 with TypeScript 5.5
- **Build Tool**: Vite 5.4
- **Styling**: Tailwind CSS 3.4
- **Components**: shadcn/ui (Radix UI primitives)
- **Routing**: React Router v6
- **State Management**: useState + sessionStorage
- **HTTP Client**: Fetch API

**Pages:**

```typescript
// src/UI/src/pages/
UploadPage.tsx       // File upload (XLSX/DOCX/PDF) → /parse
ReviewPage.tsx       // BoQ review → /extract → /match
OptimizePage.tsx     // Match approval → /optimize
ExportPage.tsx       // Result review → /export
MetricsPage.tsx      // Operations dashboard → /observations/metrics
```

**Component Structure:**

```
src/UI/src/
├── components/
│   ├── ui/              # shadcn/ui components
│   │   ├── Button.tsx
│   │   ├── Card.tsx
│   │   └── Badge.tsx
│   ├── FileUploader.tsx
│   ├── BoqTable.tsx
│   ├── MatchReviewer.tsx
│   └── ResultSummary.tsx
├── lib/
│   └── utils.ts         # Utility functions (cn, formatNumber)
├── pages/
│   └── (see above)
└── App.tsx              # Router setup
```

**State Flow:**

```typescript
// Upload → Review
sessionStorage.setItem('parsedData', JSON.stringify(data));
sessionStorage.setItem('boq', JSON.stringify(boq));

// Review → Optimize
sessionStorage.setItem('matchedItems', JSON.stringify(matched));

// Optimize → Export
sessionStorage.setItem('result', JSON.stringify(result));
```

**API Communication:**

```typescript
// Example: Optimize
const response = await fetch('http://localhost:5000/optimize', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(optimizationRequest),
});

const result: OptimizationResult = await response.json();
```

---

### 2. API Layer

#### 2.1 API Service (Port 5000)

**Technology Stack:**
- **.NET**: 8.0 (Minimal API)
- **ORM**: Entity Framework Core 8.0.10
- **Database**: SQLite 3
- **Excel**: EPPlus 7.5.0
- **Serialization**: System.Text.Json

**Project Structure:**

```
src/Api/
├── Program.cs                  # Entry point, endpoint definitions
├── Data/
│   ├── AppDbContext.cs         # EF Core DbContext
│   └── Migrations/             # Database migrations
├── Services/
│   ├── ExcelExportService.cs   # EPPlus XLSX generation
│   └── ObservationService.cs   # Telemetry and metrics
├── Middleware/
│   └── ErrorHandlingMiddleware.cs  # Exception handling, limits, timeouts
├── Validation/
│   └── ValidationHelpers.cs    # Input validation functions
└── appsettings.json            # Configuration
```

**Endpoints (10 total):**

```csharp
// Health
app.MapGet("/healthz", () => new { status = "healthy" });

// Core workflow
app.MapPost("/parse", async (IFormFile file, bool useOcr) => {...});
app.MapPost("/extract", async (ParsedData data) => {...});
app.MapPost("/match", async (MatchRequest req) => {...});
app.MapPost("/optimize", async (OptimizationRequest req) => {...});
app.MapPost("/export", async (ExportRequest req) => {...});

// Observations
app.MapPost("/observations", async (Observation obs) => {...});
app.MapGet("/observations/metrics", async (DateTime? since) => {...});
app.MapGet("/observations/recent", async (int limit = 100) => {...});
```

**Database Schema:**

```csharp
// AppDbContext.cs
public DbSet<CachedMatch> CachedMatches { get; set; }
public DbSet<UserApproval> UserApprovals { get; set; }
public DbSet<SessionData> SessionData { get; set; }

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Indexes for performance
    modelBuilder.Entity<CachedMatch>()
        .HasIndex(m => new { m.BoqItemName, m.Unit });
    
    modelBuilder.Entity<UserApproval>()
        .HasIndex(a => a.SessionId);
    
    modelBuilder.Entity<SessionData>()
        .HasIndex(s => s.CreatedAt);
}
```

**Middleware Pipeline:**

```csharp
// Program.cs
app.UseMiddleware<ExceptionHandlingMiddleware>();    // Global error handling
app.UseMiddleware<RequestSizeLimitMiddleware>();     // 100MB limit
app.UseMiddleware<RequestTimeoutMiddleware>();       // 5 minute timeout
```

**Services:**

```csharp
// ExcelExportService.cs
public async Task<byte[]> GenerateKssAsync(
    OptimizationResult result, 
    BoqDto boq, 
    string projectName)
{
    using var package = new ExcelPackage();
    var worksheet = package.Workbook.Worksheets.Add("КСС");
    
    // Headers, formatting, formulas
    // Returns XLSX as byte[]
}

// ObservationService.cs
public async Task LogAsync(Observation obs)
{
    // Hash filename with SHA256
    // Check for duplicates (within 1 hour)
    // Aggregate metrics
}
```

#### 2.2 AI Gateway (Port 5001)

**Technology Stack:**
- **.NET**: 8.0 (Minimal API)
- **PDF**: iText7 8.0.5
- **XLSX/DOCX**: DocumentFormat.OpenXml 3.1.0
- **OCR**: Tesseract wrapper (libtesseract)
- **LLM**: HTTP client to BG GPT API

**Project Structure:**

```
src/AiGateway/
├── Program.cs              # Entry point, endpoint definitions
├── Services/               # (future) Parser services
├── appsettings.json        # BG GPT API config, Tesseract config
└── AiGateway.http          # Test requests
```

**Endpoints:**

```csharp
// PDF parsing
app.MapPost("/pdf/layout", async (IFormFile file, bool useOcr) => {...});

// XLSX parsing
app.MapPost("/xlsx/parse", async (IFormFile file) => {...});

// DOCX parsing
app.MapPost("/docx/parse", async (IFormFile file) => {...});

// LLM extraction (calls BG GPT)
app.MapPost("/llm/extract", async (ExtractionRequest req) => {...});
```

**PDF Parsing Flow:**

```csharp
// iText7 text extraction
using var reader = new PdfReader(stream);
using var document = new PdfDocument(reader);

var strategy = new LocationTextExtractionStrategy();
for (int i = 1; i <= document.GetNumberOfPages(); i++)
{
    var page = document.GetPage(i);
    var text = PdfTextExtractor.GetTextFromPage(page, strategy);
    // Collect text blocks with positions
}

// If useOcr=true, fallback to Tesseract
if (string.IsNullOrWhiteSpace(text))
{
    using var engine = new TesseractEngine(datapath, "bul+eng");
    var result = engine.Process(image);
    text = result.GetText();
}
```

**LLM Integration:**

```csharp
// BG GPT API call
var request = new
{
    model = "gpt-4o-mini",
    messages = new[]
    {
        new { role = "system", content = systemPrompt },
        new { role = "user", content = userPrompt }
    },
    temperature = 0.0,
    max_tokens = 4000,
    response_format = new { type = "json_object" }
};

var response = await httpClient.PostAsJsonAsync(
    "https://api.raicommerce.net/v1/chat/completions",
    request
);

var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();
return result.choices[0].message.content; // JSON string
```

---

### 3. Business Logic Layer (Core.Engine)

**Technology Stack:**
- **.NET**: 8.0 (Class Library)
- **LP Solver**: Google.OrTools 9.11.4210
- **Fuzzy Matching**: Fastenshtein 1.0.0.8
- **YAML**: YamlDotNet 16.1.3

**Project Structure:**

```
src/Core.Engine/
├── Models/                    # DTOs (11 files)
│   ├── BoqDto.cs
│   ├── ItemDto.cs
│   ├── MatchedItem.cs
│   ├── OptimizationRequest.cs
│   ├── OptimizationResult.cs
│   ├── PriceBaseEntry.cs
│   ├── ProjectDto.cs
│   ├── SourceDto.cs
│   ├── StageDto.cs
│   ├── StageSummary.cs
│   └── CoeffResult.cs
└── Services/                  # Business logic (4 files)
    ├── FuzzyMatcher.cs
    ├── LpOptimizer.cs
    ├── TextNormalizer.cs
    └── UnitNormalizer.cs
```

#### 3.1 FuzzyMatcher

**Purpose:** Съпоставяне на BoQ позиции с каталожни позиции.

**Algorithm:**

```csharp
public List<MatchCandidate> Match(ItemDto boqItem, List<PriceBaseEntry> catalogue)
{
    var normalized = TextNormalizer.Normalize(boqItem.Name);
    var candidates = new List<MatchCandidate>();
    
    foreach (var entry in catalogue)
    {
        // 1. Unit normalization
        var unitMatch = UnitNormalizer.AreEquivalent(boqItem.Unit, entry.Unit);
        
        // 2. Text matching
        var entryNormalized = TextNormalizer.Normalize(entry.Name);
        
        // Exact match
        if (normalized == entryNormalized && unitMatch)
        {
            candidates.Add(new MatchCandidate { Score = 100.0, ... });
            continue;
        }
        
        // Levenshtein distance
        var distance = Fastenshtein.Levenshtein.Distance(normalized, entryNormalized);
        var maxLen = Math.Max(normalized.Length, entryNormalized.Length);
        var score = (1.0 - (double)distance / maxLen) * 100.0;
        
        // Apply unit match bonus
        if (unitMatch) score += 10.0;
        
        candidates.Add(new MatchCandidate { Score = score, ... });
    }
    
    // Return top 5 candidates
    return candidates.OrderByDescending(c => c.Score).Take(5).ToList();
}
```

**Caching Strategy:**

```csharp
// Check cache in API layer
var cacheKey = $"{boqItem.Name}|{boqItem.Unit}";
var cached = await db.CachedMatches
    .Where(m => m.BoqItemName == boqItem.Name && m.Unit == boqItem.Unit)
    .FirstOrDefaultAsync();

if (cached != null && cached.ExpiresAt > DateTime.UtcNow)
{
    return JsonSerializer.Deserialize<List<MatchCandidate>>(cached.CandidatesJson);
}

// Otherwise, perform matching and cache result
var candidates = fuzzyMatcher.Match(boqItem, priceBase);
db.CachedMatches.Add(new CachedMatch
{
    BoqItemName = boqItem.Name,
    Unit = boqItem.Unit,
    CandidatesJson = JsonSerializer.Serialize(candidates),
    CreatedAt = DateTime.UtcNow,
    ExpiresAt = DateTime.UtcNow.AddDays(30)
});
await db.SaveChangesAsync();
```

#### 3.2 LpOptimizer

**Purpose:** Linear Programming оптимизация на коефициенти.

**Mathematical Model:**

```
Minimize: Σ(pos[i] + neg[i]) * lambda

Subject to:
  coeff[i] = 1 + pos[i] - neg[i]              for all i
  minCoeff <= coeff[i] <= maxCoeff            for all i
  pos[i] >= 0, neg[i] >= 0                    for all i
  
  (optional) Σ(coeff[i] * price[i] * qty[i]) = stageTarget[s]  for stage s

Where:
  - coeff[i]: Coefficient for item i
  - pos[i], neg[i]: L1 linearization variables (|x-1| = pos + neg)
  - lambda: Penalty weight for deviations
  - minCoeff, maxCoeff: Bounds (typically 0.4, 2.0)
```

**Implementation:**

```csharp
public async Task<OptimizationResult> OptimizeAsync(OptimizationRequest req)
{
    var solver = Solver.CreateSolver("GLOP"); // Linear Programming
    if (solver == null) throw new Exception("GLOP solver unavailable");
    
    int n = req.MatchedItems.Count;
    var coeffVars = new Variable[n];
    var posVars = new Variable[n];
    var negVars = new Variable[n];
    
    // Create variables
    for (int i = 0; i < n; i++)
    {
        coeffVars[i] = solver.MakeNumVar(req.MinCoeff, req.MaxCoeff, $"coeff_{i}");
        posVars[i] = solver.MakeNumVar(0, double.PositiveInfinity, $"pos_{i}");
        negVars[i] = solver.MakeNumVar(0, double.PositiveInfinity, $"neg_{i}");
        
        // Constraint: coeff[i] = 1 + pos[i] - neg[i]
        var ct = solver.MakeConstraint(1.0, 1.0);
        ct.SetCoefficient(coeffVars[i], 1.0);
        ct.SetCoefficient(posVars[i], -1.0);
        ct.SetCoefficient(negVars[i], 1.0);
    }
    
    // Objective: minimize L1 penalty
    var objective = solver.Objective();
    for (int i = 0; i < n; i++)
    {
        objective.SetCoefficient(posVars[i], req.Lambda);
        objective.SetCoefficient(negVars[i], req.Lambda);
    }
    objective.SetMinimization();
    
    // Solve
    var status = solver.Solve();
    
    if (status == Solver.ResultStatus.OPTIMAL || status == Solver.ResultStatus.FEASIBLE)
    {
        var coeffs = coeffVars.Select(v => v.SolutionValue()).ToList();
        return new OptimizationResult
        {
            Feasible = true,
            ObjectiveValue = objective.Value(),
            Coeffs = coeffs,
            StageSummaries = ComputeStageSummaries(req, coeffs)
        };
    }
    
    return new OptimizationResult { Feasible = false };
}
```

**Stage Summaries:**

```csharp
private List<StageSummary> ComputeStageSummaries(
    OptimizationRequest req, 
    List<double> coeffs)
{
    var summaries = new List<StageSummary>();
    
    foreach (var stage in req.Stages)
    {
        var stageItems = req.MatchedItems
            .Where((m, i) => m.BoqItem.StageId == stage.Id)
            .Select((m, i) => new { Match = m, Index = i })
            .ToList();
        
        double totalBase = stageItems.Sum(x => 
            x.Match.BoqItem.Quantity * x.Match.UserSelected.CatalogueItem.BasePrice);
        
        double totalOptimized = stageItems.Sum(x => 
            x.Match.BoqItem.Quantity * x.Match.UserSelected.CatalogueItem.BasePrice * coeffs[x.Index]);
        
        double avgCoeff = stageItems.Average(x => coeffs[x.Index]);
        
        summaries.Add(new StageSummary
        {
            StageId = stage.Id,
            StageName = stage.Name,
            TotalBase = totalBase,
            TotalOptimized = totalOptimized,
            AvgCoeff = avgCoeff
        });
    }
    
    return summaries;
}
```

#### 3.3 Normalizers

**TextNormalizer:**

```csharp
public static string Normalize(string text)
{
    if (string.IsNullOrWhiteSpace(text)) return "";
    
    // 1. Lowercase
    var result = text.ToLowerInvariant();
    
    // 2. Remove diacritics (ă → a, ș → s)
    result = RemoveDiacritics(result);
    
    // 3. Remove extra whitespace
    result = Regex.Replace(result, @"\s+", " ").Trim();
    
    // 4. Remove punctuation
    result = Regex.Replace(result, @"[^\w\s]", "");
    
    return result;
}

private static string RemoveDiacritics(string text)
{
    var normalizedString = text.Normalize(NormalizationForm.FormD);
    var stringBuilder = new StringBuilder();
    
    foreach (var c in normalizedString)
    {
        var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
        if (unicodeCategory != UnicodeCategory.NonSpacingMark)
        {
            stringBuilder.Append(c);
        }
    }
    
    return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
}
```

**UnitNormalizer:**

```csharp
// Loaded from units.yaml
private static Dictionary<string, string> _unitMappings;

public static bool AreEquivalent(string unit1, string unit2)
{
    var canonical1 = GetCanonical(unit1);
    var canonical2 = GetCanonical(unit2);
    return canonical1 == canonical2;
}

private static string GetCanonical(string unit)
{
    var normalized = unit.Trim().ToLowerInvariant();
    
    // Check if it's already canonical
    if (_unitMappings.ContainsValue(normalized))
        return normalized;
    
    // Look up alias
    if (_unitMappings.TryGetValue(normalized, out var canonical))
        return canonical;
    
    return normalized; // Unknown unit, return as-is
}
```

---

### 4. Data Layer

#### 4.1 SQLite Database

**Connection String:**
```
Data Source=/var/lib/firstmake/data/firstmake.db
```

**Tables:**

**CachedMatches:**
```sql
CREATE TABLE CachedMatches (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BoqItemName TEXT NOT NULL,
    Unit TEXT NOT NULL,
    CandidatesJson TEXT NOT NULL,  -- Serialized List<MatchCandidate>
    CreatedAt DATETIME NOT NULL,
    ExpiresAt DATETIME
);

CREATE INDEX IX_CachedMatch_BoqItemName_Unit 
ON CachedMatches(BoqItemName, Unit);

CREATE INDEX IX_CachedMatch_CreatedAt 
ON CachedMatches(CreatedAt);
```

**UserApprovals:**
```sql
CREATE TABLE UserApprovals (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SessionId TEXT NOT NULL,
    BoqItemId TEXT NOT NULL,
    BoqItemName TEXT NOT NULL,
    SelectedCatalogueItemId TEXT NOT NULL,
    SelectedCatalogueItemName TEXT NOT NULL,
    BasePrice DECIMAL(18,2) NOT NULL,
    Unit TEXT NOT NULL,
    MatchScore REAL NOT NULL,
    ApprovedAt DATETIME NOT NULL
);

CREATE INDEX IX_UserApproval_SessionId 
ON UserApprovals(SessionId);

CREATE INDEX IX_UserApproval_BoqItemId 
ON UserApprovals(BoqItemId);
```

**SessionData:**
```sql
CREATE TABLE SessionData (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SessionId TEXT NOT NULL,
    SourceFileName TEXT NOT NULL,
    BoqDataJson TEXT NOT NULL,  -- Serialized BoqDto
    CreatedAt DATETIME NOT NULL,
    CompletedAt DATETIME
);

CREATE INDEX IX_SessionData_CreatedAt 
ON SessionData(CreatedAt);
```

#### 4.2 External Services

**BG GPT API:**
- **Base URL**: `https://api.raicommerce.net/v1`
- **Model**: `gpt-4o-mini`
- **Purpose**: LLM extraction от parsнати документи
- **Authentication**: Bearer token (API key)

**Tesseract OCR:**
- **Languages**: Bulgarian + English (`bul+eng`)
- **Purpose**: OCR за сканирани PDF документи
- **Installation**: System package (tesseract-ocr)

---

## Design Patterns

### 1. Repository Pattern (Implicit)

Entity Framework Core DbContext acts as repository:

```csharp
public class AppDbContext : DbContext
{
    public DbSet<CachedMatch> CachedMatches { get; set; }
    public DbSet<UserApproval> UserApprovals { get; set; }
    public DbSet<SessionData> SessionData { get; set; }
}
```

### 2. Service Layer Pattern

Business logic isolated in services:

```csharp
// Core.Engine.Services
public class FuzzyMatcher { ... }
public class LpOptimizer { ... }

// Api.Services
public class ExcelExportService { ... }
public class ObservationService { ... }
```

### 3. DTO Pattern

All data transfer uses strongly-typed DTOs:

```csharp
// Core.Engine.Models
public record BoqDto(ProjectDto Project, List<StageDto> Stages, List<ItemDto> Items);
public record ItemDto(string Id, string StageId, string Name, double Quantity, string Unit);
```

### 4. Middleware Pattern

ASP.NET Core middleware for cross-cutting concerns:

```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestSizeLimitMiddleware>();
app.UseMiddleware<RequestTimeoutMiddleware>();
```

### 5. Strategy Pattern (Future)

Different parsing strategies за различни file types:

```csharp
public interface IFileParser
{
    Task<ParsedData> ParseAsync(Stream fileStream);
}

public class PdfParser : IFileParser { ... }
public class XlsxParser : IFileParser { ... }
public class DocxParser : IFileParser { ... }
```

---

## Security Considerations

### 1. Input Validation

- **File size**: Max 50MB per file
- **Request size**: Max 100MB total
- **Array limits**: Max 10,000 BoQ items, 100,000 price base entries
- **String lengths**: Max 500 chars for item names
- **Numeric ranges**: Positive quantities, reasonable price ranges

### 2. Error Handling

- **Global exception middleware**: Catches all unhandled exceptions
- **Structured errors**: Consistent JSON error format
- **Logging**: All errors logged with context
- **No sensitive data**: Stack traces not exposed to clients

### 3. Rate Limiting (Future)

```csharp
// Add to Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});
```

### 4. Authentication (Future)

```csharp
// JWT authentication for multi-user deployment
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { ... });
```

---

## Performance Optimizations

### 1. Database Indexing

Strategic indexes за frequent queries:

```csharp
modelBuilder.Entity<CachedMatch>()
    .HasIndex(m => new { m.BoqItemName, m.Unit });  // Composite index
```

### 2. Caching Strategy

- **Match results**: Cached 30 days
- **Duplicate detection**: SHA256 hash + 1 hour window
- **EF Core**: Second-level cache (future)

### 3. Asynchronous Operations

All I/O operations use async/await:

```csharp
public async Task<OptimizationResult> OptimizeAsync(...)
{
    // CPU-bound work on background thread
    return await Task.Run(() => solver.Solve());
}
```

### 4. Streaming Large Files

```csharp
// Stream Excel export
return Results.File(
    stream: new MemoryStream(excelBytes),
    contentType: "application/vnd.openxmlformats...",
    fileDownloadName: $"{projectName}_КСС.xlsx"
);
```

---

## Testing Strategy

### Unit Tests (26 tests)

```csharp
// Core.Engine.Tests
FuzzyMatcherTests.cs        // 8 tests
LpOptimizerTests.cs         // 10 tests
TextNormalizerTests.cs      // 4 tests
UnitNormalizerTests.cs      // 4 tests
```

### Integration Tests (Future)

```csharp
// API endpoint tests
[Fact]
public async Task Parse_ValidXlsx_Returns200()
{
    var client = _factory.CreateClient();
    var content = new MultipartFormDataContent();
    // ...
    var response = await client.PostAsync("/parse", content);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

### E2E Tests (Future)

- Playwright for UI testing
- Full workflow: Upload → Review → Optimize → Export

---

## Deployment Architecture

### Development

```
Developer Laptop
├── Docker Desktop
│   ├── firstmake-api (5000)
│   ├── firstmake-ai (5001)
│   └── firstmake-ui (5174)
└── VS Code + DevContainer
```

### Production (Self-Hosted)

```
Linux Server (Ubuntu 24.04)
├── Nginx (80, 443)
│   ├── Reverse proxy to API
│   ├── Reverse proxy to AI Gateway
│   └── Static files (React build)
├── Systemd Services
│   ├── firstmake-api.service
│   └── firstmake-ai.service
└── SQLite Database
    └── /var/lib/firstmake/data/firstmake.db
```

### Production (Docker)

```
Docker Host
├── nginx container (80, 443)
├── api container (5000)
├── ai-gateway container (5001)
└── Volumes
    ├── data/ (SQLite)
    └── logs/
```

---

**Last Updated:** 2025-01-13
