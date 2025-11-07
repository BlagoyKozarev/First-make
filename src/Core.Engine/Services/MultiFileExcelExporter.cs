using Core.Engine.Models;
using OfficeOpenXml;
using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace Core.Engine.Services;

/// <summary>
/// Multi-file Excel export service
/// Generates КСС Excel files with filled values and packages as ZIP
/// Includes summary file with all stages
/// </summary>
public class MultiFileExcelExporter
{
    private readonly SummaryExcelExporter _summaryExporter;
    private readonly PriceCheckExporter _priceCheckExporter;
    private readonly ILogger<MultiFileExcelExporter> _logger;

    public MultiFileExcelExporter(ILogger<MultiFileExcelExporter> logger, ILogger<PriceCheckExporter> priceCheckLogger)
    {
        _logger = logger;
        _summaryExporter = new SummaryExcelExporter();
        _priceCheckExporter = new PriceCheckExporter(priceCheckLogger);
    }

    /// <summary>
    /// Export all BOQ files with optimization results to ZIP
    /// Includes summary file as first file in ZIP
    /// </summary>
    public async Task<byte[]> ExportToZipAsync(
        ProjectSession session,
        IterationResult iteration,
        UnifiedMatchResult matchResult)
    {
        ExcelHelper.EnsureLicenseSet();

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // First file: Summary Excel with all stages
            var summaryBytes = await _summaryExporter.ExportSummaryAsync(session, iteration);
            var summaryEntry = archive.CreateEntry("00_ОБОБЩЕНИЕ.xlsx", System.IO.Compression.CompressionLevel.Optimal);
            using (var summaryStream = summaryEntry.Open())
            {
                await summaryStream.WriteAsync(summaryBytes, 0, summaryBytes.Length);
            }

            // Second file: Price verification file
            var priceCheckBytes = _priceCheckExporter.ExportPriceCheck(session, iteration, matchResult);
            var priceCheckEntry = archive.CreateEntry("01_ПРОВЕРКА_ЦЕНИ.xlsx", System.IO.Compression.CompressionLevel.Optimal);
            using (var priceCheckStream = priceCheckEntry.Open())
            {
                await priceCheckStream.WriteAsync(priceCheckBytes, 0, priceCheckBytes.Length);
            }

            // Then add all individual BOQ files
            foreach (var doc in session.BoqDocuments)
            {
                // Generate Excel for each BOQ file
                var excelBytes = await GenerateBoqExcelAsync(doc, iteration, matchResult, session);

                // Add to ZIP
                var fileName = $"{Path.GetFileNameWithoutExtension(doc.FileName)}_result.xlsx";
                var entry = archive.CreateEntry(fileName, System.IO.Compression.CompressionLevel.Optimal);

                using var entryStream = entry.Open();
                await entryStream.WriteAsync(excelBytes, 0, excelBytes.Length);
            }
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Generate single BOQ Excel file with results
    /// </summary>
    private async Task<byte[]> GenerateBoqExcelAsync(
        BoqDocument doc,
        IterationResult iteration,
        UnifiedMatchResult matchResult,
        ProjectSession session)
    {
        // Load original КСС file as template
        var originalFile = session.KssFiles.First(f => f.Id == doc.SourceFileId);

        using var package = new ExcelPackage(new FileInfo(originalFile.OriginalPath));

        // Process each worksheet
        foreach (var worksheet in package.Workbook.Worksheets)
        {
            await FillWorksheetAsync(worksheet, doc, iteration, matchResult);
        }

        return package.GetAsByteArray();
    }

    /// <summary>
    /// Fill worksheet with optimization results
    /// </summary>
    private async Task FillWorksheetAsync(
        ExcelWorksheet worksheet,
        BoqDocument doc,
        IterationResult iteration,
        UnifiedMatchResult matchResult)
    {
        // Find header row (contains "цена" and "стойност")
        int headerRow = FindHeaderRow(worksheet);
        if (headerRow == 0)
            return; // No header found, skip

        // Find column indices
        var colMap = MapColumns(worksheet, headerRow);
        if (!colMap.ContainsKey("price") || !colMap.ContainsKey("value"))
            return; // Required columns not found

        // Check if we need to add Коеф. and Работна цена columns
        bool needCoeffColumn = !colMap.ContainsKey("coeff");
        bool needWorkPriceColumn = !colMap.ContainsKey("workprice");

        int coeffCol = 0;
        int workPriceCol = 0;

        if (needCoeffColumn)
        {
            // Insert Коеф. column before цена
            coeffCol = colMap["price"];
            worksheet.InsertColumn(coeffCol, 1);
            worksheet.Cells[headerRow, coeffCol].Value = "Коеф.";

            // Update column indices after insertion
            foreach (var key in colMap.Keys.ToList())
            {
                if (colMap[key] >= coeffCol)
                    colMap[key]++;
            }
        }
        else
        {
            coeffCol = colMap["coeff"];
        }

        if (needWorkPriceColumn)
        {
            // Insert Работна цена column after Коеф.
            workPriceCol = coeffCol + 1;
            worksheet.InsertColumn(workPriceCol, 1);
            worksheet.Cells[headerRow, workPriceCol].Value = "Работна цена";

            // Update column indices after insertion
            foreach (var key in colMap.Keys.ToList())
            {
                if (colMap[key] >= workPriceCol)
                    colMap[key]++;
            }
            
            // Now the structure is: ... | Коеф.(E) | Работна цена(F) | Цена(G) | Стойност(H) | ...
            colMap["workprice"] = workPriceCol; // F
        }
        else
        {
            workPriceCol = colMap["workprice"];
        }
        
        // Update references after all insertions
        int priceCol = colMap["price"];   // G - Цена
        int valueCol = colMap["value"];   // H - Стойност

        // Fill data rows
        int dataStartRow = headerRow + 4; // Usually row 12 for data

        for (int row = dataStartRow; row <= worksheet.Dimension.End.Row; row++)
        {
            var nameCell = worksheet.Cells[row, colMap["name"]].Text?.Trim();
            if (string.IsNullOrWhiteSpace(nameCell))
                continue;

            // Find matching item
            var item = doc.Items.FirstOrDefault(i =>
                i.SourceSheet == worksheet.Name &&
                i.SourceRow == row);

            if (item == null)
                continue;

            var itemMatch = matchResult.ItemMatches.Values.FirstOrDefault(m => m.Item.Id == item.Id);
            if (itemMatch?.PriceEntry == null)
                continue;

            if (!iteration.Coefficients.TryGetValue(itemMatch.UnifiedKey, out var coeff))
                continue;

            // Fill Коеф. (column E)
            worksheet.Cells[row, coeffCol].Value = coeff.Coefficient;
            worksheet.Cells[row, coeffCol].Style.Numberformat.Format = "0.0000";

            // Fill Работна цена (column F) = базова цена × коефициент
            var workPrice = coeff.WorkPrice;
            worksheet.Cells[row, workPriceCol].Value = workPrice;
            worksheet.Cells[row, workPriceCol].Style.Numberformat.Format = "#,##0.00";

            // Fill Цена (column G) = Работна цена (F) закръглена до 2 знака
            var roundedPrice = Math.Round(workPrice, 2);
            worksheet.Cells[row, priceCol].Value = roundedPrice;
            worksheet.Cells[row, priceCol].Style.Numberformat.Format = "#,##0.00";

            // Fill Стойност (column H) = Количество (D) × Цена (G) закръглена
            var value = item.Quantity * roundedPrice;
            worksheet.Cells[row, valueCol].Value = value;
            worksheet.Cells[row, valueCol].Style.Numberformat.Format = "#,##0.00";
        }

        // Add summary rows at the end for this stage
        await AddStageSummaryRowsAsync(worksheet, doc, iteration, matchResult, colMap, dataStartRow);

        await Task.CompletedTask;
    }

    private int FindHeaderRow(ExcelWorksheet worksheet)
    {
        // Look for row containing "цена" and "стойност"
        for (int row = 1; row <= Math.Min(15, worksheet.Dimension?.End.Row ?? 0); row++)
        {
            bool hasPrice = false;
            bool hasValue = false;

            int maxCol = worksheet.Dimension?.End.Column ?? 0;
            for (int col = 1; col <= maxCol; col++)
            {
                var text = worksheet.Cells[row, col].Text?.ToLower() ?? "";
                if (text.Contains("цена") && !text.Contains("работна"))
                    hasPrice = true;
                if (text.Contains("стойност"))
                    hasValue = true;
            }

            if (hasPrice && hasValue)
                return row;
        }

        return 0;
    }

    private Dictionary<string, int> MapColumns(ExcelWorksheet worksheet, int headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
        {
            var header = worksheet.Cells[headerRow, col].Text?.Trim().ToLower() ?? "";

            if (string.IsNullOrWhiteSpace(header))
                continue;

            if (header.Contains("no") || header.Contains("№"))
                map["number"] = col;
            else if (header.Contains("наименование"))
                map["name"] = col;
            else if (header.Contains("мярка"))
                map["unit"] = col;
            else if (header.Contains("к-во") || header.Contains("количество"))
                map["quantity"] = col;
            else if (header.Contains("коеф"))
                map["coeff"] = col;
            else if (header.Contains("работна") && header.Contains("цена"))
                map["workprice"] = col;
            else if (header.Contains("цена") && !header.Contains("работна"))
                map["price"] = col;
            else if (header.Contains("стойност"))
                map["value"] = col;
        }

        return map;
    }

    /// <summary>
    /// Add 3 summary rows at the end of each worksheet showing:
    /// 1. Forecast value for the stage
    /// 2. Proposed total value
    /// 3. Gap (difference)
    /// </summary>
    private async Task AddStageSummaryRowsAsync(
        ExcelWorksheet worksheet,
        BoqDocument doc,
        IterationResult iteration,
        UnifiedMatchResult matchResult,
        Dictionary<string, int> colMap,
        int dataStartRow)
    {
        // Find the stage for this document
        var firstItem = doc.Items.FirstOrDefault(i => i.SourceSheet == worksheet.Name);
        if (firstItem == null)
            return;

        var stageCode = firstItem.StageCode;
        
        // Find the BOQ file result for this document
        var boqResult = iteration.BoqResults.Values.FirstOrDefault(b => b.FileId == doc.SourceFileId);
        if (boqResult == null)
            return;
            
        var stageSummary = boqResult.Stages.FirstOrDefault(s => s.StageCode == stageCode);
        if (stageSummary == null)
            return;

        // Calculate proposed total for this worksheet
        decimal proposedTotal = 0;
        for (int row = dataStartRow; row <= worksheet.Dimension.End.Row; row++)
        {
            var valueCell = worksheet.Cells[row, colMap["value"]];
            if (valueCell.Value is double dVal)
                proposedTotal += (decimal)dVal;
            else if (valueCell.Value is decimal decVal)
                proposedTotal += decVal;
        }

        // Find last data row
        int lastRow = worksheet.Dimension.End.Row;
        
        // Add 2 empty rows for spacing
        int summaryStartRow = lastRow + 2;

        // Row 1: Forecast
        worksheet.Cells[summaryStartRow, colMap["name"]].Value = "ПРОГНОЗНА СТОЙНОСТ ЗА ЕТАПА:";
        worksheet.Cells[summaryStartRow, colMap["name"]].Style.Font.Bold = true;
        worksheet.Cells[summaryStartRow, colMap["value"]].Value = stageSummary.Forecast;
        worksheet.Cells[summaryStartRow, colMap["value"]].Style.Numberformat.Format = "#,##0.00";
        worksheet.Cells[summaryStartRow, colMap["value"]].Style.Font.Bold = true;
        worksheet.Cells[summaryStartRow, colMap["value"]].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        worksheet.Cells[summaryStartRow, colMap["value"]].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);

        // Row 2: Proposed
        summaryStartRow++;
        worksheet.Cells[summaryStartRow, colMap["name"]].Value = "ПРЕДЛОЖЕНА СУМА ЗА ИЗПЪЛНЕНИЕ:";
        worksheet.Cells[summaryStartRow, colMap["name"]].Style.Font.Bold = true;
        worksheet.Cells[summaryStartRow, colMap["value"]].Value = proposedTotal;
        worksheet.Cells[summaryStartRow, colMap["value"]].Style.Numberformat.Format = "#,##0.00";
        worksheet.Cells[summaryStartRow, colMap["value"]].Style.Font.Bold = true;
        worksheet.Cells[summaryStartRow, colMap["value"]].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        worksheet.Cells[summaryStartRow, colMap["value"]].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);

        // Row 3: Gap (should be >= 0)
        summaryStartRow++;
        var gap = stageSummary.Forecast - proposedTotal;
        worksheet.Cells[summaryStartRow, colMap["name"]].Value = "РАЗЛИКА (Прогноза - Предложена):";
        worksheet.Cells[summaryStartRow, colMap["name"]].Style.Font.Bold = true;
        worksheet.Cells[summaryStartRow, colMap["value"]].Value = gap;
        worksheet.Cells[summaryStartRow, colMap["value"]].Style.Numberformat.Format = "#,##0.00";
        worksheet.Cells[summaryStartRow, colMap["value"]].Style.Font.Bold = true;
        
        // Color based on gap: green if >= 0, red if < 0
        worksheet.Cells[summaryStartRow, colMap["value"]].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        if (gap >= 0)
        {
            worksheet.Cells[summaryStartRow, colMap["value"]].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
        }
        else
        {
            worksheet.Cells[summaryStartRow, colMap["value"]].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Export single file (for preview)
    /// </summary>
    public async Task<byte[]> ExportSingleFileAsync(
        BoqDocument doc,
        IterationResult iteration,
        UnifiedMatchResult matchResult,
        ProjectSession session)
    {
        return await GenerateBoqExcelAsync(doc, iteration, matchResult, session);
    }
}
