using Core.Engine.Models;
using System.Text.RegularExpressions;

namespace Core.Engine.Services;

/// <summary>
/// Parses Указания Word documents to extract forecast values
/// Expected structure:
/// - Section "7. Прогнозна стойност" contains forecast breakdown by stages
/// - Format: "Етап X: Y лв" or "Приложение № X: Y лв"
/// - Total: "Обща стойност: Z лв (без ДДС)"
/// </summary>
public class UkazaniaParser
{
    /// <summary>
    /// Parses Указания document using Python script
    /// (Word parsing requires python-docx library)
    /// </summary>
    public async Task<StageForecasts> ParseFromWordAsync(string filePath, string fileId)
    {
        // Use Python script to extract text from Word document
        var pythonScript = GeneratePythonScript(filePath);
        var scriptPath = Path.Combine(Path.GetTempPath(), $"parse_ukazania_{Guid.NewGuid()}.py");
        
        try
        {
            await File.WriteAllTextAsync(scriptPath, pythonScript);
            
            // Execute Python script
            var output = await ExecutePythonScriptAsync(scriptPath);
            
            // Parse output
            return ParseExtractedText(output, fileId);
        }
        finally
        {
            if (File.Exists(scriptPath))
            {
                File.Delete(scriptPath);
            }
        }
    }
    
    /// <summary>
    /// Parses already extracted text (for testing or AI extraction)
    /// </summary>
    public StageForecasts ParseFromText(string text, string fileId = "test")
    {
        return ParseExtractedText(text, fileId);
    }
    
    private string GeneratePythonScript(string wordFilePath)
    {
        var escapedPath = wordFilePath.Replace("\\", "\\\\").Replace("'", "\\'");
        
        return $@"
import sys
from docx import Document

try:
    doc = Document('{escapedPath}')
    
    # Find section containing ""прогнозн"" and ""етап""
    in_forecast_section = False
    output_lines = []
    
    for i, para in enumerate(doc.paragraphs):
        text = para.text.strip()
        text_lower = text.lower()
        
        # Check if entering forecast section
        if ""прогнозн"" in text_lower and ""стойност"" in text_lower:
            in_forecast_section = True
            output_lines.append(text)
            continue
        
        # If in forecast section, collect relevant lines
        if in_forecast_section:
            # Stop if we hit next major section
            if text and text[0].isdigit() and "". "" in text and len(text) < 50:
                # Looks like ""8. Next Section""
                break
            
            # Include lines with stages or totals
            if (""етап"" in text_lower or 
                ""приложение"" in text_lower or 
                ""обща"" in text_lower or 
                ""лв"" in text_lower):
                output_lines.append(text)
    
    # Print collected lines
    for line in output_lines:
        print(line)
        
except Exception as e:
    print(f""ERROR: {{e}}"", file=sys.stderr)
    sys.exit(1)
";
    }
    
    private async Task<string> ExecutePythonScriptAsync(string scriptPath)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"\"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        process.Start();
        
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();
        
        if (process.ExitCode != 0)
        {
            throw new Exception($"Python script failed: {error}");
        }
        
        return output;
    }
    
    private StageForecasts ParseExtractedText(string text, string sourceFileId = "unknown")
    {
        var stages = new Dictionary<string, StageForecast>();
        decimal totalForecast = 0;
        
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Parse stage forecast: "Етап 1: 128,203.60 лв" or "Приложение № 1: 128,203.60 лв"
            var stageMatch = Regex.Match(trimmed, 
                @"(Етап|Приложение\s*№?)\s*(\d+)\s*[:\-–—]\s*([\d,\s.]+)\s*лв", 
                RegexOptions.IgnoreCase);
            
            if (stageMatch.Success)
            {
                var stageNumber = stageMatch.Groups[2].Value;
                var stageCode = $"Етап {stageNumber}";
                var amountText = stageMatch.Groups[3].Value
                    .Replace(" ", "")
                    .Replace(",", ".")
                    .Replace("\u00A0", ""); // Non-breaking space
                
                if (decimal.TryParse(amountText,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal amount))
                {
                    stages[stageCode] = new StageForecast
                    {
                        Code = stageCode,
                        Name = $"Етап {stageNumber}",
                        Forecast = amount
                    };
                    
                    totalForecast += amount;
                }
                continue;
            }
            
            // Parse total: "Обща стойност: 2,688,791.39 лв (без ДДС)"
            var totalMatch = Regex.Match(trimmed,
                @"(Обща|Общо|Всичко)\s*(стойност)?\s*[:\-–—]?\s*([\d,\s.]+)\s*лв",
                RegexOptions.IgnoreCase);
            
            if (totalMatch.Success)
            {
                var amountText = totalMatch.Groups[3].Value
                    .Replace(" ", "")
                    .Replace(",", ".")
                    .Replace("\u00A0", "");
                
                if (decimal.TryParse(amountText,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal total))
                {
                    totalForecast = total;
                }
            }
        }
        
        // Validate: sum of stages should match total (within 1 лв tolerance)
        var stagesSum = stages.Values.Sum(s => s.Forecast);
        if (Math.Abs(stagesSum - totalForecast) > 1.0m && totalForecast > 0)
        {
            Console.WriteLine($"Warning: Stages sum ({stagesSum:F2}) != Total ({totalForecast:F2})");
        }
        
        return new StageForecasts
        {
            Stages = stages,
            TotalForecast = totalForecast > 0 ? totalForecast : stagesSum,
            SourceFileId = sourceFileId
        };
    }
}
