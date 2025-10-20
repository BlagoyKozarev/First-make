using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// ============================================================================
// Health check
// ============================================================================
app.MapGet("/healthz", () => new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    service = "AiGateway"
})
.WithName("HealthCheck")
.WithOpenApi();

// ============================================================================
// POST /ocr - Tesseract OCR for scanned documents
// ============================================================================
app.MapPost("/ocr", async (IFormFile file) =>
{
    // TODO Task 6: Implement Tesseract OCR wrapper
    // 1. Save uploaded file to /tmp
    // 2. Run: tesseract {inputPath} stdout -l bul+eng --psm 6
    // 3. Parse output text
    // 4. Return { text: string, confidence: float }
    
    await Task.CompletedTask;
    return Results.Ok(new
    {
        text = "[OCR STUB] This will use Tesseract to extract text from images/scans",
        confidence = 0.0,
        language = "bul+eng"
    });
})
.WithName("OcrExtract")
.WithOpenApi()
.DisableAntiforgery();

// ============================================================================
// POST /pdf/layout - Extract text and table structures from PDF
// ============================================================================
app.MapPost("/pdf/layout", async (IFormFile file) =>
{
    try
    {
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        using var pdfReader = new PdfReader(memoryStream);
        using var pdfDocument = new PdfDocument(pdfReader);
        
        var pageTexts = new List<object>();
        var totalChars = 0;

        for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
        {
            var page = pdfDocument.GetPage(i);
            var strategy = new SimpleTextExtractionStrategy();
            var text = PdfTextExtractor.GetTextFromPage(page, strategy);
            
            totalChars += text.Length;
            pageTexts.Add(new
            {
                pageNumber = i,
                text = text,
                charCount = text.Length
            });
        }

        var textDensity = totalChars / (double)pdfDocument.GetNumberOfPages();
        var isScanned = textDensity < 100; // Low text density suggests scanned PDF

        return Results.Ok(new
        {
            fileName = file.FileName,
            pageCount = pdfDocument.GetNumberOfPages(),
            pages = pageTexts,
            totalCharacters = totalChars,
            textDensity = textDensity,
            isScanned = isScanned,
            needsOcr = isScanned
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("PdfLayoutExtract")
.WithOpenApi()
.DisableAntiforgery();

// ============================================================================
// POST /xlsx/parse - Deterministic XLSX parsing with OpenXML
// ============================================================================
app.MapPost("/xlsx/parse", async (IFormFile file) =>
{
    try
    {
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        using var spreadsheet = SpreadsheetDocument.Open(memoryStream, false);
        var workbookPart = spreadsheet.WorkbookPart;
        var sheets = workbookPart?.Workbook.Sheets?.Elements<Sheet>().ToList();

        if (sheets == null || sheets.Count == 0)
        {
            return Results.BadRequest(new { error = "No sheets found in XLSX" });
        }

        var sheetData = new List<object>();

        foreach (var sheet in sheets)
        {
            var worksheetPart = (WorksheetPart)workbookPart!.GetPartById(sheet.Id!);
            var rows = worksheetPart.Worksheet.Descendants<Row>().ToList();

            var rowsData = rows.Select(row => new
            {
                rowIndex = row.RowIndex?.Value ?? 0,
                cells = row.Elements<Cell>().Select(cell => new
                {
                    cellReference = cell.CellReference?.Value,
                    value = GetCellValue(cell, workbookPart)
                }).ToList()
            }).ToList();

            sheetData.Add(new
            {
                sheetName = sheet.Name?.Value,
                sheetId = sheet.SheetId?.Value,
                rowCount = rows.Count,
                rows = rowsData
            });
        }

        return Results.Ok(new
        {
            fileName = file.FileName,
            sheetCount = sheets.Count,
            sheets = sheetData
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("XlsxParse")
.WithOpenApi()
.DisableAntiforgery();

// ============================================================================
// POST /docx/parse - Deterministic DOCX parsing with OpenXML
// ============================================================================
app.MapPost("/docx/parse", async (IFormFile file) =>
{
    try
    {
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        using var wordDoc = WordprocessingDocument.Open(memoryStream, false);
        var body = wordDoc.MainDocumentPart?.Document.Body;

        if (body == null)
        {
            return Results.BadRequest(new { error = "No body found in DOCX" });
        }

        var paragraphs = body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
            .Select((p, idx) => new
            {
                index = idx,
                text = p.InnerText
            })
            .Where(p => !string.IsNullOrWhiteSpace(p.text))
            .ToList();

        var tables = body.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>()
            .Select((t, idx) => new
            {
                tableIndex = idx,
                rowCount = t.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>().Count(),
                rows = t.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>()
                    .Select(row => new
                    {
                        cells = row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>()
                            .Select(cell => cell.InnerText)
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();

        return Results.Ok(new
        {
            fileName = file.FileName,
            paragraphCount = paragraphs.Count,
            paragraphs = paragraphs,
            tableCount = tables.Count,
            tables = tables
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("DocxParse")
.WithOpenApi()
.DisableAntiforgery();

// ============================================================================
// POST /llm/extract - LLM-based extraction with JSON-mode prompts
// ============================================================================
app.MapPost("/llm/extract", async (HttpRequest request) =>
{
    // TODO Task 6: Implement BG GPT-7B client integration
    // 1. Load playbook from /Schemas/playbooks/extract.yaml
    // 2. Load system/user prompts from /Schemas/prompts/
    // 3. Call LLM API with JSON-mode
    // 4. Validate against /Schemas/boq.schema.json
    // 5. Retry up to 3 times if validation fails
    
    await Task.CompletedTask;
    return Results.Ok(new
    {
        message = "[LLM STUB] This will call BG GPT-7B with JSON schema",
        model = "bg-gpt-7b",
        jsonMode = true,
        schemaRef = "/Schemas/boq.schema.json"
    });
})
.WithName("LlmExtract")
.WithOpenApi();

// ============================================================================
// POST /llm/rerank - Optional LLM-based candidate reranking
// ============================================================================
app.MapPost("/llm/rerank", async (HttpRequest request) =>
{
    // TODO Task 6: Implement semantic reranking with embeddings
    // 1. Accept { boqItemName: string, candidates: MatchedItem[] }
    // 2. Generate embeddings for boqItemName and all candidate names
    // 3. Calculate cosine similarity
    // 4. Return reranked candidates with semantic scores
    
    await Task.CompletedTask;
    return Results.Ok(new
    {
        message = "[RERANK STUB] This will use embeddings to rerank fuzzy matches",
        model = "bge-m3" // multilingual embeddings
    });
})
.WithName("LlmRerank")
.WithOpenApi();

app.Run();

// ============================================================================
// Helper: Get cell value from XLSX (handles SharedStringTable)
// ============================================================================
static string GetCellValue(Cell cell, WorkbookPart workbookPart)
{
    if (cell.CellValue == null) return string.Empty;

    var value = cell.CellValue.InnerText;

    if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
    {
        var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;
        if (sharedStringTable != null)
        {
            return sharedStringTable.ElementAt(int.Parse(value)).InnerText;
        }
    }

    return value;
}
