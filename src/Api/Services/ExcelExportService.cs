using OfficeOpenXml;
using OfficeOpenXml.Style;
using Core.Engine.Models;
using System.IO.Compression;
using System.Drawing;

namespace Api.Services;

public class ExcelExportService
{
    public ExcelExportService()
    {
        // Set EPPlus license context (NonCommercial or Commercial)
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    /// <summary>
    /// Export optimization result to a single XLSX file
    /// </summary>
    public async Task<byte[]> ExportToExcelAsync(OptimizationResult result, BoqDto boq, string projectName)
    {
        using var package = new ExcelPackage();
        
        // Sheet 1: Summary
        CreateSummarySheet(package, result, boq, projectName);
        
        // Sheet 2: Stage Details
        CreateStageDetailsSheet(package, result, boq);
        
        // Sheet 3: Item Details
        CreateItemDetailsSheet(package, result, boq);
        
        return await package.GetAsByteArrayAsync();
    }

    /// <summary>
    /// Export optimization result split by stages into separate files, packaged in ZIP
    /// </summary>
    public async Task<byte[]> ExportToZipAsync(OptimizationResult result, BoqDto boq, string projectName)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // Group items by stage
            var itemsByStage = boq.Items.GroupBy(i => i.Stage);
            
            // Build coefficient lookup by stage
            var coeffsByStage = result.Coeffs
                .GroupBy(c => c.Name) // Group by item name (which includes stage info in real data)
                .ToDictionary(g => g.Key, g => g.First().C);
            
            foreach (var stageGroup in itemsByStage)
            {
                var stageName = stageGroup.Key;
                var stageItems = stageGroup.ToList();
                
                // Find stage summary
                var stageSummary = result.PerStage.FirstOrDefault(s => s.Stage == stageName);
                if (stageSummary == null) continue;
                
                // Create Excel for this stage
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add(stageName);
                
                CreateStageWorksheet(worksheet, stageSummary, stageItems, coeffsByStage);
                
                // Add to ZIP
                var fileName = $"{SanitizeFileName(stageName)}.xlsx";
                var entry = archive.CreateEntry(fileName);
                
                using var entryStream = entry.Open();
                await package.SaveAsAsync(entryStream);
            }
            
            // Add summary file
            using (var summaryPackage = new ExcelPackage())
            {
                CreateSummarySheet(summaryPackage, result, boq, projectName);
                var summaryEntry = archive.CreateEntry("_Summary.xlsx");
                using var summaryStream = summaryEntry.Open();
                await summaryPackage.SaveAsAsync(summaryStream);
            }
        }
        
        return memoryStream.ToArray();
    }

    private void CreateSummarySheet(ExcelPackage package, OptimizationResult result, BoqDto boq, string projectName)
    {
        var worksheet = package.Workbook.Worksheets.Add("Обобщение");
        
        int row = 1;
        
        // Header
        worksheet.Cells[row, 1].Value = "КСС ОБОБЩЕНИЕ";
        worksheet.Cells[row, 1].Style.Font.Size = 16;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        row += 2;
        
        // Project info
        worksheet.Cells[row, 1].Value = "Проект:";
        worksheet.Cells[row, 2].Value = projectName;
        worksheet.Cells[row, 2].Style.Font.Bold = true;
        row++;
        
        worksheet.Cells[row, 1].Value = "Дата:";
        worksheet.Cells[row, 2].Value = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        row++;
        
        worksheet.Cells[row, 1].Value = "Solver статус:";
        worksheet.Cells[row, 2].Value = result.SolverStatus ?? "Unknown";
        worksheet.Cells[row, 2].Style.Font.Bold = true;
        row++;
        
        worksheet.Cells[row, 1].Value = "Обективна стойност:";
        worksheet.Cells[row, 2].Value = (double)result.TotalValue;
        worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0.00 лв";
        worksheet.Cells[row, 2].Style.Font.Bold = true;
        worksheet.Cells[row, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[row, 2].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
        row += 2;
        
        // Stage summary table
        worksheet.Cells[row, 1].Value = "Етап";
        worksheet.Cells[row, 2].Value = "Прогноза (лв)";
        worksheet.Cells[row, 3].Value = "Предложение (лв)";
        worksheet.Cells[row, 4].Value = "Разлика (лв)";
        worksheet.Cells[row, 5].Value = "Статус";
        
        var headerRange = worksheet.Cells[row, 1, row, 5];
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
        headerRange.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
        headerRange.Style.Border.BorderAround(ExcelBorderStyle.Thick);
        row++;
        
        foreach (var stage in result.PerStage)
        {
            worksheet.Cells[row, 1].Value = stage.Stage;
            worksheet.Cells[row, 2].Value = (double)stage.Forecast;
            worksheet.Cells[row, 3].Value = (double)stage.Proposed;
            worksheet.Cells[row, 4].Value = (double)stage.Gap;
            worksheet.Cells[row, 5].Value = stage.Ok ? "✓" : "✗";
            
            worksheet.Cells[row, 2, row, 4].Style.Numberformat.Format = "#,##0.00";
            
            // Color code gap
            if (stage.Gap > 0)
            {
                worksheet.Cells[row, 4].Style.Font.Color.SetColor(Color.Red);
            }
            else if (stage.Gap < 0)
            {
                worksheet.Cells[row, 4].Style.Font.Color.SetColor(Color.Green);
            }
            
            row++;
        }
        
        // Auto-fit columns
        worksheet.Cells.AutoFitColumns();
    }

    private void CreateStageDetailsSheet(ExcelPackage package, OptimizationResult result, BoqDto boq)
    {
        var worksheet = package.Workbook.Worksheets.Add("Детайли по етапи");
        
        int row = 1;
        
        // Header
        worksheet.Cells[row, 1].Value = "Код етап";
        worksheet.Cells[row, 2].Value = "Наименование";
        worksheet.Cells[row, 3].Value = "Прогноза (лв)";
        worksheet.Cells[row, 4].Value = "Предложение (лв)";
        worksheet.Cells[row, 5].Value = "Брой позиции";
        
        StyleHeader(worksheet.Cells[row, 1, row, 5]);
        row++;
        
        var stageGroups = boq.Items.GroupBy(i => i.Stage);
        
        foreach (var stageGroup in stageGroups)
        {
            var stageName = stageGroup.Key;
            var stageSummary = result.PerStage.FirstOrDefault(s => s.Stage == stageName);
            
            worksheet.Cells[row, 1].Value = stageName;
            worksheet.Cells[row, 2].Value = stageName; // TODO: Get full stage name
            worksheet.Cells[row, 3].Value = (double)(stageSummary?.Forecast ?? 0);
            worksheet.Cells[row, 4].Value = (double)(stageSummary?.Proposed ?? 0);
            worksheet.Cells[row, 5].Value = stageGroup.Count();
            
            worksheet.Cells[row, 3, row, 4].Style.Numberformat.Format = "#,##0.00";
            
            row++;
        }
        
        worksheet.Cells.AutoFitColumns();
    }

    private void CreateItemDetailsSheet(ExcelPackage package, OptimizationResult result, BoqDto boq)
    {
        var worksheet = package.Workbook.Worksheets.Add("Позиции");
        
        int row = 1;
        
        // Header
        worksheet.Cells[row, 1].Value = "№";
        worksheet.Cells[row, 2].Value = "Етап";
        worksheet.Cells[row, 3].Value = "Наименование";
        worksheet.Cells[row, 4].Value = "Мярка";
        worksheet.Cells[row, 5].Value = "Количество";
        worksheet.Cells[row, 6].Value = "Ед. цена (лв)";
        worksheet.Cells[row, 7].Value = "Коефициент";
        worksheet.Cells[row, 8].Value = "Крайна цена (лв)";
        worksheet.Cells[row, 9].Value = "Обща стойност (лв)";
        
        StyleHeader(worksheet.Cells[row, 1, row, 9]);
        row++;
        
        // Build coefficient lookup
        var coeffsByName = result.Coeffs.ToDictionary(c => $"{c.Name}|{c.Unit}", c => c);
        
        int itemNumber = 1;
        foreach (var item in boq.Items.OrderBy(i => i.Stage))
        {
            var key = $"{item.Name}|{item.Unit}";
            var coeffResult = coeffsByName.GetValueOrDefault(key);
            var coefficient = coeffResult?.C ?? 1.0;
            var basePrice = coeffResult?.BasePrice ?? 100.0m; 
            var finalPrice = basePrice * (decimal)coefficient;
            var totalValue = finalPrice * item.Qty;
            
            worksheet.Cells[row, 1].Value = itemNumber++;
            worksheet.Cells[row, 2].Value = item.Stage;
            worksheet.Cells[row, 3].Value = item.Name;
            worksheet.Cells[row, 4].Value = item.Unit;
            worksheet.Cells[row, 5].Value = (double)item.Qty;
            worksheet.Cells[row, 6].Value = (double)basePrice;
            worksheet.Cells[row, 7].Value = coefficient;
            worksheet.Cells[row, 8].Value = (double)finalPrice;
            worksheet.Cells[row, 9].Value = (double)totalValue;
            
            worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 7].Style.Numberformat.Format = "0.000";
            worksheet.Cells[row, 8].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 9].Style.Numberformat.Format = "#,##0.00";
            
            row++;
        }
        
        // Add totals row
        worksheet.Cells[row, 1].Value = "ОБЩО:";
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 9].Formula = $"=SUM(I2:I{row - 1})";
        worksheet.Cells[row, 9].Style.Font.Bold = true;
        worksheet.Cells[row, 9].Style.Numberformat.Format = "#,##0.00";
        worksheet.Cells[row, 9].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[row, 9].Style.Fill.BackgroundColor.SetColor(Color.Yellow);
        
        worksheet.Cells.AutoFitColumns();
    }

    private void CreateStageWorksheet(ExcelWorksheet worksheet, StageSummary stage, List<ItemDto> items, Dictionary<string, double> coeffsByName)
    {
        int row = 1;
        
        // Stage header
        worksheet.Cells[row, 1].Value = $"ЕТАП: {stage.Stage}";
        worksheet.Cells[row, 1].Style.Font.Size = 14;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        row += 2;
        
        // Summary
        worksheet.Cells[row, 1].Value = "Прогноза:";
        worksheet.Cells[row, 2].Value = (double)stage.Forecast;
        worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0.00 лв";
        row++;
        
        worksheet.Cells[row, 1].Value = "Предложение:";
        worksheet.Cells[row, 2].Value = (double)stage.Proposed;
        worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0.00 лв";
        worksheet.Cells[row, 2].Style.Font.Bold = true;
        row += 2;
        
        // Items table
        worksheet.Cells[row, 1].Value = "№";
        worksheet.Cells[row, 2].Value = "Наименование";
        worksheet.Cells[row, 3].Value = "Мярка";
        worksheet.Cells[row, 4].Value = "Количество";
        worksheet.Cells[row, 5].Value = "Ед. цена";
        worksheet.Cells[row, 6].Value = "Обща стойност";
        
        StyleHeader(worksheet.Cells[row, 1, row, 6]);
        row++;
        
        int itemNum = 1;
        foreach (var item in items)
        {
            var key = $"{item.Name}|{item.Unit}";
            var coefficient = coeffsByName.GetValueOrDefault(key, 1.0);
            var basePrice = 100.0m; // TODO: Get from matched item
            var finalPrice = basePrice * (decimal)coefficient;
            
            worksheet.Cells[row, 1].Value = itemNum++;
            worksheet.Cells[row, 2].Value = item.Name;
            worksheet.Cells[row, 3].Value = item.Unit;
            worksheet.Cells[row, 4].Value = (double)item.Qty;
            worksheet.Cells[row, 5].Value = (double)finalPrice;
            worksheet.Cells[row, 6].Value = (double)(item.Qty * finalPrice);
            
            worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00";
            
            row++;
        }
        
        worksheet.Cells.AutoFitColumns();
    }

    private void StyleHeader(ExcelRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(79, 129, 189)); // Blue
        range.Style.Font.Color.SetColor(Color.White);
        range.Style.Border.BorderAround(ExcelBorderStyle.Thick);
        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    }

    private string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}
