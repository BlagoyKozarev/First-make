using Core.Engine.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace Core.Engine.Services;

/// <summary>
/// Export summary Excel file with all stages and overall totals
/// </summary>
public class SummaryExcelExporter
{
    /// <summary>
    /// Generate summary Excel file with all stages
    /// </summary>
    public async Task<byte[]> ExportSummaryAsync(
        ProjectSession session,
        IterationResult iteration)
    {
        ExcelHelper.EnsureLicenseSet();

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Обобщение");

        // Title
        worksheet.Cells["A1"].Value = "ОБОБЩЕН ФАЙЛ ЗА ОПТИМИЗАЦИЯ";
        worksheet.Cells["A1:E1"].Merge = true;
        worksheet.Cells["A1"].Style.Font.Bold = true;
        worksheet.Cells["A1"].Style.Font.Size = 16;
        worksheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        // Project info
        worksheet.Cells["A2"].Value = $"Обект: {session.ObjectName}";
        worksheet.Cells["A2:E2"].Merge = true;
        worksheet.Cells["A3"].Value = $"Дата: {session.Date:dd.MM.yyyy}";
        worksheet.Cells["A3:E3"].Merge = true;
        worksheet.Cells["A4"].Value = $"Итерация: #{iteration.IterationNumber}";
        worksheet.Cells["A4:E4"].Merge = true;

        // Headers
        int headerRow = 6;
        worksheet.Cells[headerRow, 1].Value = "№";
        worksheet.Cells[headerRow, 2].Value = "Етап";
        worksheet.Cells[headerRow, 3].Value = "Прогнозна стойност (лв)";
        worksheet.Cells[headerRow, 4].Value = "Предложена сума за изпълнение (лв)";
        worksheet.Cells[headerRow, 5].Value = "Gap (лв)";

        // Style headers
        using (var range = worksheet.Cells[headerRow, 1, headerRow, 5])
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
            range.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        }

        // Collect all stages across all BOQ files
        var allStages = new Dictionary<string, (string Name, decimal Forecast, decimal Proposed)>();

        foreach (var boqResult in iteration.BoqResults.Values)
        {
            foreach (var stage in boqResult.Stages)
            {
                if (!allStages.ContainsKey(stage.StageCode))
                {
                    allStages[stage.StageCode] = (stage.StageName, 0m, 0m);
                }

                var current = allStages[stage.StageCode];
                allStages[stage.StageCode] = (
                    current.Name,
                    current.Forecast + stage.Forecast,
                    current.Proposed + stage.Proposed
                );
            }
        }

        // Sort stages by code
        var sortedStages = allStages.OrderBy(s => s.Key).ToList();

        // Data rows
        int dataRow = headerRow + 1;
        int stageNumber = 1;
        decimal totalForecast = 0;
        decimal totalProposed = 0;

        foreach (var stage in sortedStages)
        {
            var gap = stage.Value.Forecast - stage.Value.Proposed;

            worksheet.Cells[dataRow, 1].Value = stageNumber;
            worksheet.Cells[dataRow, 2].Value = stage.Value.Name;
            worksheet.Cells[dataRow, 3].Value = stage.Value.Forecast;
            worksheet.Cells[dataRow, 4].Value = stage.Value.Proposed;
            worksheet.Cells[dataRow, 5].Value = gap;

            // Format numbers
            worksheet.Cells[dataRow, 3].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[dataRow, 4].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[dataRow, 5].Style.Numberformat.Format = "#,##0.00";

            // Color gap based on value
            if (gap >= 0)
            {
                worksheet.Cells[dataRow, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[dataRow, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                worksheet.Cells[dataRow, 5].Style.Font.Color.SetColor(System.Drawing.Color.DarkGreen);
            }
            else
            {
                worksheet.Cells[dataRow, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[dataRow, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
                worksheet.Cells[dataRow, 5].Style.Font.Color.SetColor(System.Drawing.Color.DarkRed);
            }

            totalForecast += stage.Value.Forecast;
            totalProposed += stage.Value.Proposed;

            dataRow++;
            stageNumber++;
        }

        // Summary rows (3 rows at the end)
        int summaryRow = dataRow + 1;

        // Row 1: Total Forecast
        worksheet.Cells[summaryRow, 2].Value = "ОБЩА ПРОГНОЗА:";
        worksheet.Cells[summaryRow, 2].Style.Font.Bold = true;
        worksheet.Cells[summaryRow, 3].Value = totalForecast;
        worksheet.Cells[summaryRow, 3].Style.Numberformat.Format = "#,##0.00";
        worksheet.Cells[summaryRow, 3].Style.Font.Bold = true;
        worksheet.Cells[summaryRow, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[summaryRow, 3].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);

        // Row 2: Total Proposed
        summaryRow++;
        worksheet.Cells[summaryRow, 2].Value = "ОБЩА ПРЕДЛОЖЕНА СУМА ЗА ИЗПЪЛНЕНИЕ:";
        worksheet.Cells[summaryRow, 2].Style.Font.Bold = true;
        worksheet.Cells[summaryRow, 3].Value = totalProposed;
        worksheet.Cells[summaryRow, 3].Style.Numberformat.Format = "#,##0.00";
        worksheet.Cells[summaryRow, 3].Style.Font.Bold = true;
        worksheet.Cells[summaryRow, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[summaryRow, 3].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);

        // Row 3: Total Gap
        summaryRow++;
        var overallGap = totalForecast - totalProposed;
        worksheet.Cells[summaryRow, 2].Value = "ОБЩ GAP:";
        worksheet.Cells[summaryRow, 2].Style.Font.Bold = true;
        worksheet.Cells[summaryRow, 3].Value = overallGap;
        worksheet.Cells[summaryRow, 3].Style.Numberformat.Format = "#,##0.00";
        worksheet.Cells[summaryRow, 3].Style.Font.Bold = true;
        worksheet.Cells[summaryRow, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;

        if (overallGap >= 0)
        {
            worksheet.Cells[summaryRow, 3].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
            worksheet.Cells[summaryRow, 3].Style.Font.Color.SetColor(System.Drawing.Color.DarkGreen);
        }
        else
        {
            worksheet.Cells[summaryRow, 3].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
            worksheet.Cells[summaryRow, 3].Style.Font.Color.SetColor(System.Drawing.Color.DarkRed);
        }

        // Gap percentage
        summaryRow++;
        var gapPercentage = totalForecast > 0 ? (overallGap / totalForecast) : 0;
        worksheet.Cells[summaryRow, 2].Value = "GAP %:";
        worksheet.Cells[summaryRow, 2].Style.Font.Bold = true;
        worksheet.Cells[summaryRow, 3].Value = gapPercentage;
        worksheet.Cells[summaryRow, 3].Style.Numberformat.Format = "0.00%";
        worksheet.Cells[summaryRow, 3].Style.Font.Bold = true;

        // Auto-fit columns
        worksheet.Column(1).Width = 8;
        worksheet.Column(2).Width = 40;
        worksheet.Column(3).Width = 25;
        worksheet.Column(4).Width = 30;
        worksheet.Column(5).Width = 20;

        await Task.CompletedTask;
        return package.GetAsByteArray();
    }
}
