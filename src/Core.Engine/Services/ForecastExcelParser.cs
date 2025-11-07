using Core.Engine.Models;
using OfficeOpenXml;

namespace Core.Engine.Services;

/// <summary>
/// Parser for forecast Excel files containing stage budgets
/// Expected format: Column A = Stage Code, Column B = Forecast Value
/// </summary>
public class ForecastExcelParser
{
    /// <summary>
    /// Parse forecast Excel file and extract stage budgets
    /// </summary>
    public StageForecasts ParseForecastFile(string filePath)
    {
        ExcelHelper.EnsureLicenseSet();

        var stages = new Dictionary<string, StageForecast>();
        decimal totalForecast = 0;

        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();

        if (worksheet == null)
            throw new InvalidOperationException("Excel файлът не съдържа работни листове");

        // Find header row (contains "етап" and "прогноз" or similar)
        int headerRow = FindHeaderRow(worksheet);
        if (headerRow == 0)
        {
            Console.WriteLine($"DEBUG: No header row found in forecast file. Worksheet has {worksheet.Dimension?.End.Row ?? 0} rows");
            throw new InvalidOperationException("Не е намерен хедър ред с колони 'Етап' и 'Прогноза'");
        }

        Console.WriteLine($"DEBUG: Found header row at {headerRow}");

        // Find column indices
        int stageCol = FindStageColumn(worksheet, headerRow);
        int forecastCol = FindForecastColumn(worksheet, headerRow);

        Console.WriteLine($"DEBUG: Stage column: {stageCol}, Forecast column: {forecastCol}");

        if (stageCol == 0 || forecastCol == 0)
        {
            throw new InvalidOperationException("Не са намерени колони 'Етап' и 'Прогноза'");
        }

        // Parse data rows
        int dataStartRow = headerRow + 1;
        int parsedCount = 0;

        Console.WriteLine($"DEBUG: Starting to parse from row {dataStartRow} to {worksheet.Dimension.End.Row}");

        for (int row = dataStartRow; row <= worksheet.Dimension.End.Row; row++)
        {
            var stageCode = worksheet.Cells[row, stageCol].Text?.Trim();
            var forecastText = worksheet.Cells[row, forecastCol].Text?.Trim();

            Console.WriteLine($"DEBUG: Row {row}: Stage='{stageCode}', Forecast='{forecastText}'");

            if (string.IsNullOrWhiteSpace(stageCode) || string.IsNullOrWhiteSpace(forecastText))
                continue;

            // Remove all spaces (including non-breaking spaces) and thousand separators
            var cleanedForecast = forecastText
                .Replace(" ", "")           // Regular space
                .Replace("\u00A0", "")      // Non-breaking space
                .Replace("\u202F", "")      // Narrow non-breaking space
                .Replace(",", "");          // Comma (in case used as thousands separator)

            if (decimal.TryParse(cleanedForecast, System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out decimal forecastValue))
            {
                stages[stageCode] = new StageForecast
                {
                    Code = stageCode,
                    Name = $"Етап {stageCode}",
                    Forecast = forecastValue
                };
                totalForecast += forecastValue;
                parsedCount++;
                Console.WriteLine($"DEBUG: Parsed stage {stageCode} with forecast {forecastValue}");
            }
            else
            {
                Console.WriteLine($"DEBUG: Failed to parse forecast value '{forecastText}' (cleaned: '{cleanedForecast}') as decimal");
            }
        }

        Console.WriteLine($"DEBUG: Total parsed: {parsedCount} stages");

        if (parsedCount == 0)
            throw new InvalidOperationException("Не са намерени валидни данни за прогнози");

        var fileId = Guid.NewGuid().ToString();

        return new StageForecasts
        {
            Stages = stages,
            TotalForecast = totalForecast,
            SourceFileId = fileId
        };
    }

    private int FindHeaderRow(ExcelWorksheet worksheet)
    {
        // Look for row containing "етап" and "прогноз"
        for (int row = 1; row <= Math.Min(10, worksheet.Dimension?.End.Row ?? 0); row++)
        {
            bool hasStage = false;
            bool hasForecast = false;

            int maxCol = Math.Min(10, worksheet.Dimension?.End.Column ?? 0);
            for (int col = 1; col <= maxCol; col++)
            {
                var text = worksheet.Cells[row, col].Text?.ToLower() ?? "";
                Console.WriteLine($"DEBUG FindHeaderRow: Row {row}, Col {col}: '{text}'");
                if (text.Contains("етап"))
                    hasStage = true;
                if (text.Contains("прогноз"))
                    hasForecast = true;
            }

            if (hasStage && hasForecast)
            {
                Console.WriteLine($"DEBUG: Found header at row {row}");
                return row;
            }
        }

        Console.WriteLine("DEBUG: No header row found");
        return 0;
    }

    private int FindStageColumn(ExcelWorksheet worksheet, int headerRow)
    {
        for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
        {
            var header = worksheet.Cells[headerRow, col].Text?.ToLower() ?? "";
            Console.WriteLine($"DEBUG FindStageColumn: Col {col}: '{header}'");
            if (header.Contains("етап") || header.Contains("номер") || header.Contains("код"))
            {
                Console.WriteLine($"DEBUG: Found stage column at {col}");
                return col;
            }
        }
        Console.WriteLine("DEBUG: No stage column found");
        return 0;
    }

    private int FindForecastColumn(ExcelWorksheet worksheet, int headerRow)
    {
        for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
        {
            var header = worksheet.Cells[headerRow, col].Text?.ToLower() ?? "";
            Console.WriteLine($"DEBUG FindForecastColumn: Col {col}: '{header}'");
            if (header.Contains("прогноз") || header.Contains("стойност") || header.Contains("бюджет"))
            {
                Console.WriteLine($"DEBUG: Found forecast column at {col}");
                return col;
            }
        }
        Console.WriteLine("DEBUG: No forecast column found");
        return 0;
    }
}
