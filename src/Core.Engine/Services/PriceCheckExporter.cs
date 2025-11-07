using Core.Engine.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Microsoft.Extensions.Logging;

namespace Core.Engine.Services;

/// <summary>
/// Exports a price verification file showing all price entries with working prices per stage
/// </summary>
public class PriceCheckExporter
{
    private readonly ILogger<PriceCheckExporter> _logger;

    public PriceCheckExporter(ILogger<PriceCheckExporter> logger)
    {
        _logger = logger;
    }

    public byte[] ExportPriceCheck(
        ProjectSession session,
        IterationResult iteration,
        UnifiedMatchResult matchResult)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Проверка цени");

        // Get all unique price entries from the price base
        var allPriceEntries = session.PriceBase.ToList();

        // Get all unique stages from BOQ documents
        var allStages = session.BoqDocuments
            .SelectMany(doc => doc.Items.Select(i => i.StageCode))
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        _logger.LogInformation("PriceCheck: Found {StageCount} stages, {PriceCount} price entries", 
            allStages.Count, allPriceEntries.Count);
        _logger.LogInformation("PriceCheck: Coefficients count = {CoeffCount}", iteration.Coefficients.Count);

        // Build a lookup: (Name, Unit, Stage) -> Working Price
        // Use the SAME logic as MultiFileExcelExporter: itemMatch.UnifiedKey -> coeff.WorkPrice
        var workingPriceLookup = new Dictionary<(string Name, string Unit, string Stage), decimal>();
        var matchedPriceEntries = new HashSet<(string Name, string Unit)>();

        foreach (var boqDoc in session.BoqDocuments)
        {
            foreach (var item in boqDoc.Items)
            {
                // Find the item match using the SAME logic as MultiFileExcelExporter
                var itemMatch = matchResult.ItemMatches.Values.FirstOrDefault(m => m.Item.Id == item.Id);
                
                if (itemMatch?.PriceEntry == null)
                    continue;

                // Get coefficient using UnifiedKey (same as export logic)
                if (!iteration.Coefficients.TryGetValue(itemMatch.UnifiedKey, out var coeff))
                {
                    _logger.LogWarning("PriceCheck: No coefficient found for UnifiedKey '{Key}' (Name: '{Name}', Unit: '{Unit}')", 
                        itemMatch.UnifiedKey, itemMatch.PriceEntry.Name, itemMatch.PriceEntry.Unit);
                    continue;
                }

                // Mark this price entry as matched
                var key = (itemMatch.PriceEntry.Name, itemMatch.PriceEntry.Unit);
                matchedPriceEntries.Add(key);

                // Get working price from coefficient (same as column F in export files)
                var workingPrice = coeff.WorkPrice;
                var lookupKey = (itemMatch.PriceEntry.Name, itemMatch.PriceEntry.Unit, item.StageCode);
                
                // Store the working price for this stage
                workingPriceLookup[lookupKey] = workingPrice;
            }
        }

        _logger.LogInformation("PriceCheck: Built lookup with {LookupCount} entries, {MatchedCount} matched entries", 
            workingPriceLookup.Count, matchedPriceEntries.Count);

        // Headers
        int col = 1;
        worksheet.Cells[1, col++].Value = "№";
        worksheet.Cells[1, col++].Value = "Наименование";
        worksheet.Cells[1, col++].Value = "Мярка";
        worksheet.Cells[1, col++].Value = "Базова цена";

        // Add stage columns
        var stageColumnStart = col;
        foreach (var stage in allStages)
        {
            worksheet.Cells[1, col++].Value = $"Етап {stage}";
        }

        // Style header row
        using (var range = worksheet.Cells[1, 1, 1, col - 1])
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            range.Style.Border.BorderAround(ExcelBorderStyle.Medium);
        }

        // Data rows
        int row = 2;
        int index = 1;

        foreach (var priceEntry in allPriceEntries.OrderBy(p => p.Name))
        {
            col = 1;
            worksheet.Cells[row, col++].Value = index++;
            worksheet.Cells[row, col++].Value = priceEntry.Name;
            worksheet.Cells[row, col++].Value = priceEntry.Unit;
            worksheet.Cells[row, col++].Value = priceEntry.BasePrice;
            worksheet.Cells[row, col - 1].Style.Numberformat.Format = "#,##0.00";

            // Check if this price entry is matched
            var isMatched = matchedPriceEntries.Contains((priceEntry.Name, priceEntry.Unit));

            // Collect all working prices for this entry across all stages
            var stagePrices = new List<decimal?>();
            
            // Fill stage columns
            for (int i = 0; i < allStages.Count; i++)
            {
                var stage = allStages[i];
                var lookupKey = (priceEntry.Name, priceEntry.Unit, stage);

                if (workingPriceLookup.TryGetValue(lookupKey, out var workingPrice))
                {
                    worksheet.Cells[row, stageColumnStart + i].Value = workingPrice;
                    worksheet.Cells[row, stageColumnStart + i].Style.Numberformat.Format = "#,##0.00";
                    stagePrices.Add(workingPrice);
                }
                else
                {
                    worksheet.Cells[row, stageColumnStart + i].Value = "-";
                    stagePrices.Add(null);
                }
            }

            // Check if there are different prices across stages (should not happen if coeff is same)
            var nonNullPrices = stagePrices.Where(p => p.HasValue).Select(p => p.Value).ToList();
            var hasPriceDifference = false;
            
            if (nonNullPrices.Count > 1)
            {
                var minPrice = nonNullPrices.Min();
                var maxPrice = nonNullPrices.Max();
                // Allow small rounding differences (0.01)
                hasPriceDifference = (maxPrice - minPrice) > 0.01m;
            }

            // Highlight rows:
            // 1. Yellow if unmatched (not used in any stage)
            // 2. Orange if has price differences across stages (should not happen!)
            if (!isMatched)
            {
                using (var rowRange = worksheet.Cells[row, 1, row, stageColumnStart + allStages.Count - 1])
                {
                    rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Yellow);
                }
            }
            else if (hasPriceDifference)
            {
                using (var rowRange = worksheet.Cells[row, 1, row, stageColumnStart + allStages.Count - 1])
                {
                    rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Orange);
                }
            }

            row++;
        }

        // Auto-fit columns
        worksheet.Cells.AutoFitColumns();

        // Make sure column widths are reasonable
        for (int i = 1; i <= col + allStages.Count - 1; i++)
        {
            if (worksheet.Column(i).Width > 50)
            {
                worksheet.Column(i).Width = 50;
            }
        }

        return package.GetAsByteArray();
    }
}
