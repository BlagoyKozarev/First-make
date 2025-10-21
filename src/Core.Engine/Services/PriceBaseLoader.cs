using Core.Engine.Models;
using OfficeOpenXml;

namespace Core.Engine.Services;

/// <summary>
/// Parses Excel price base file (единични цени по опис)
/// Expected structure:
/// - Sheet: "Опис" or first sheet
/// - Row 2: Headers (Номер | Наименование | Мярка | Цена)
/// - Row 3+: Data rows
/// </summary>
public class PriceBaseLoader
{
    public List<PriceEntry> LoadFromExcel(string filePath, string fileId)
    {
        ExcelHelper.EnsureLicenseSet();
        
        var entries = new List<PriceEntry>();
        
        using var package = new ExcelPackage(new FileInfo(filePath));
        
        // Try to find "Опис" sheet, or use first sheet
        var worksheet = package.Workbook.Worksheets.FirstOrDefault(ws => 
            ws.Name.Contains("Опис", StringComparison.OrdinalIgnoreCase)) 
            ?? package.Workbook.Worksheets.First();
        
        // Start from row 3 (row 2 is headers)
        int row = 3;
        
        while (row <= worksheet.Dimension.End.Row)
        {
            // Column A: Номер (optional, skip)
            // Column B: Наименование
            // Column C: Мярка
            // Column D: Цена
            
            var name = worksheet.Cells[row, 2].Text?.Trim();
            var unit = worksheet.Cells[row, 3].Text?.Trim();
            var priceText = worksheet.Cells[row, 4].Text?.Trim();
            
            // Skip empty rows
            if (string.IsNullOrWhiteSpace(name))
            {
                row++;
                continue;
            }
            
            // Parse price
            if (!decimal.TryParse(priceText, out var price))
            {
                // Try parsing with comma as decimal separator
                priceText = priceText?.Replace(",", ".");
                if (!decimal.TryParse(priceText, 
                    System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    out price))
                {
                    // Log warning but continue
                    Console.WriteLine($"Warning: Could not parse price '{priceText}' at row {row}");
                    row++;
                    continue;
                }
            }
            
            entries.Add(new PriceEntry
            {
                Name = name,
                Unit = unit ?? "",
                BasePrice = price,
                SourceFileId = fileId,
                SourceRow = row
            });
            
            row++;
        }
        
        return entries;
    }
    
    /// <summary>
    /// Load from multiple Excel files and merge
    /// </summary>
    public List<PriceEntry> LoadFromMultipleFiles(List<(string FilePath, string FileId)> files)
    {
        var allEntries = new List<PriceEntry>();
        
        foreach (var (filePath, fileId) in files)
        {
            try
            {
                var entries = LoadFromExcel(filePath, fileId);
                allEntries.AddRange(entries);
                Console.WriteLine($"Loaded {entries.Count} price entries from {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading price base from {filePath}: {ex.Message}");
                throw;
            }
        }
        
        // Check for duplicates and warn
        var duplicates = allEntries
            .GroupBy(e => new { e.Name, e.Unit })
            .Where(g => g.Count() > 1)
            .ToList();
        
        if (duplicates.Any())
        {
            Console.WriteLine($"Warning: Found {duplicates.Count} duplicate entries across price base files:");
            foreach (var dup in duplicates.Take(5))
            {
                Console.WriteLine($"  - {dup.Key.Name} ({dup.Key.Unit}): {dup.Count()} occurrences");
            }
        }
        
        return allEntries;
    }
}
