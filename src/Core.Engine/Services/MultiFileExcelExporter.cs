using Core.Engine.Models;
using OfficeOpenXml;
using System.IO.Compression;

namespace Core.Engine.Services;

/// <summary>
/// Multi-file Excel export service
/// Generates 19 КСС Excel files with filled values and packages as ZIP
/// </summary>
public class MultiFileExcelExporter
{
    /// <summary>
    /// Export all BOQ files with optimization results to ZIP
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
            
            colMap["price"]++;
            colMap["value"]++;
        }
        else
        {
            workPriceCol = colMap["workprice"];
        }

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

            // Fill Коеф.
            worksheet.Cells[row, coeffCol].Value = coeff.Coefficient;
            worksheet.Cells[row, coeffCol].Style.Numberformat.Format = "0.0000";

            // Fill Работна цена
            worksheet.Cells[row, workPriceCol].Value = coeff.WorkPrice;
            worksheet.Cells[row, workPriceCol].Style.Numberformat.Format = "#,##0.00";

            // Fill цена (same as Работна цена)
            worksheet.Cells[row, colMap["price"]].Value = coeff.WorkPrice;
            worksheet.Cells[row, colMap["price"]].Style.Numberformat.Format = "#,##0.00";

            // Fill стойност
            var value = item.Quantity * coeff.WorkPrice;
            worksheet.Cells[row, colMap["value"]].Value = value;
            worksheet.Cells[row, colMap["value"]].Style.Numberformat.Format = "#,##0.00";
        }

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
