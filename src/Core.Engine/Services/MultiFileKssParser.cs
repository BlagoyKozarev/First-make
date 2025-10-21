using Core.Engine.Models;
using OfficeOpenXml;
using System.Text.RegularExpressions;

namespace Core.Engine.Services;

/// <summary>
/// Parses multiple КСС Excel files
/// Expected structure per file:
/// - Row 1-7: Header info
/// - Row 8: Column headers (No. | Наименование | мярка | К-во | цена | стойност)
/// - Row 10: Stage section title (e.g., "Реконструкция на водопровод...")
/// - Row 12+: Data rows
/// </summary>
public class MultiFileKssParser
{
    public List<BoqDocument> ParseMultipleFiles(List<(string FilePath, string FileId)> files)
    {
        var documents = new List<BoqDocument>();
        
        foreach (var (filePath, fileId) in files)
        {
            try
            {
                var doc = ParseSingleFile(filePath, fileId);
                documents.Add(doc);
                Console.WriteLine($"Parsed {doc.Items.Count} items from {doc.FileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing КСС from {filePath}: {ex.Message}");
                throw;
            }
        }
        
        return documents;
    }
    
    public BoqDocument ParseSingleFile(string filePath, string fileId)
    {
        ExcelHelper.EnsureLicenseSet();
        
        var fileName = Path.GetFileName(filePath);
        var items = new List<BoqItemDto>();
        var stages = new List<StageDto>();
        
        using var package = new ExcelPackage(new FileInfo(filePath));
        
        // Parse all sheets
        foreach (var worksheet in package.Workbook.Worksheets)
        {
            var (sheetStages, sheetItems) = ParseWorksheet(worksheet, fileId, fileName);
            stages.AddRange(sheetStages);
            items.AddRange(sheetItems);
        }
        
        return new BoqDocument
        {
            Id = Guid.NewGuid().ToString(),
            FileName = fileName,
            SourceFileId = fileId,
            Stages = stages,
            Items = items
        };
    }
    
    private (List<StageDto> Stages, List<BoqItemDto> Items) ParseWorksheet(
        ExcelWorksheet worksheet, 
        string fileId, 
        string fileName)
    {
        var stages = new List<StageDto>();
        var items = new List<BoqItemDto>();
        
        // Find column indices (usually row 8)
        int headerRow = FindHeaderRow(worksheet);
        var colMap = MapColumns(worksheet, headerRow);
        
        // Find stage title (usually around row 10)
        string stageTitle = FindStageTitle(worksheet, headerRow + 1, headerRow + 4);
        string stageCode = ExtractStageCode(stageTitle, fileName);
        
        // Add stage (forecast will be populated later from Указания)
        stages.Add(new StageDto
        {
            Code = stageCode,
            Name = stageTitle,
            Forecast = 0 // Will be updated from Указания parsing
        });
        
        // Parse data rows (start after header + 3-4 rows)
        int dataStartRow = headerRow + 4;
        
        for (int row = dataStartRow; row <= worksheet.Dimension.End.Row; row++)
        {
            var item = ParseDataRow(worksheet, row, colMap, stageCode, fileId, worksheet.Name);
            if (item != null)
            {
                items.Add(item);
            }
        }
        
        return (stages, items);
    }
    
    private int FindHeaderRow(ExcelWorksheet worksheet)
    {
        // Look for row containing "Наименование" (usually row 8)
        for (int row = 1; row <= Math.Min(15, worksheet.Dimension.End.Row); row++)
        {
            for (int col = 1; col <= 8; col++)
            {
                var text = worksheet.Cells[row, col].Text;
                if (text.Contains("Наименование", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("наименование", StringComparison.OrdinalIgnoreCase))
                {
                    return row;
                }
            }
        }
        
        // Default to row 8
        return 8;
    }
    
    private Dictionary<string, int> MapColumns(ExcelWorksheet worksheet, int headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
        {
            var header = worksheet.Cells[headerRow, col].Text?.Trim().ToLower();
            
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
            else if (header.Contains("цена") && !header.Contains("работна"))
                map["price"] = col;
            else if (header.Contains("стойност"))
                map["value"] = col;
        }
        
        return map;
    }
    
    private string FindStageTitle(ExcelWorksheet worksheet, int startRow, int endRow)
    {
        // Look for stage description (long text row)
        for (int row = startRow; row <= endRow; row++)
        {
            for (int col = 1; col <= 3; col++)
            {
                var text = worksheet.Cells[row, col].Text?.Trim();
                if (!string.IsNullOrWhiteSpace(text) && 
                    text.Length > 20 && 
                    (text.Contains("Реконструкция") || text.Contains("Етап")))
                {
                    return text;
                }
            }
        }
        
        // Fallback to worksheet name
        return worksheet.Name;
    }
    
    private string ExtractStageCode(string stageTitle, string fileName)
    {
        // Try to extract "Етап X" or "Приложение № X"
        var etapMatch = Regex.Match(stageTitle, @"Етап\s*(\d+)", RegexOptions.IgnoreCase);
        if (etapMatch.Success)
        {
            return $"Етап {etapMatch.Groups[1].Value}";
        }
        
        var prilozenieMatch = Regex.Match(fileName, @"Приложение\s*№\s*(\d+)", RegexOptions.IgnoreCase);
        if (prilozenieMatch.Success)
        {
            return $"Етап {prilozenieMatch.Groups[1].Value}";
        }
        
        // Fallback to filename-based code
        return $"Етап_{Path.GetFileNameWithoutExtension(fileName)}";
    }
    
    private BoqItemDto? ParseDataRow(
        ExcelWorksheet worksheet, 
        int row, 
        Dictionary<string, int> colMap, 
        string stageCode,
        string fileId,
        string sheetName)
    {
        // Get name (required)
        if (!colMap.TryGetValue("name", out int nameCol))
            return null;
        
        var name = worksheet.Cells[row, nameCol].Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return null;
        
        // Skip if it's a section header (ALL CAPS, no quantity)
        if (name.ToUpper() == name && name.Length > 30)
            return null;
        
        // Get unit
        string unit = "";
        if (colMap.TryGetValue("unit", out int unitCol))
        {
            unit = worksheet.Cells[row, unitCol].Text?.Trim() ?? "";
        }
        
        // Get quantity (required)
        decimal quantity = 0;
        if (colMap.TryGetValue("quantity", out int qtyCol))
        {
            var qtyText = worksheet.Cells[row, qtyCol].Text?.Trim();
            if (string.IsNullOrWhiteSpace(qtyText))
                return null; // Skip rows without quantity
            
            qtyText = qtyText.Replace(",", ".");
            if (!decimal.TryParse(qtyText, 
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out quantity))
            {
                Console.WriteLine($"Warning: Could not parse quantity '{qtyText}' at row {row}");
                return null;
            }
        }
        else
        {
            return null; // No quantity column found
        }
        
        return new BoqItemDto
        {
            Id = Guid.NewGuid().ToString(),
            StageCode = stageCode,
            Name = name,
            Unit = unit,
            Quantity = quantity,
            SourceRow = row,
            SourceSheet = sheetName,
            SourceFileId = fileId
        };
    }
}
