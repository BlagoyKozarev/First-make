using Core.Engine.Models;
using Core.Engine.Services;

namespace Api.Validation;

/// <summary>
/// Validation helpers for API requests
/// </summary>
public static class ValidationHelpers
{
    private static readonly string[] AllowedFileExtensions = { ".xlsx", ".docx", ".pdf" };
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB

    /// <summary>
    /// Validate uploaded file
    /// </summary>
    public static (bool IsValid, string? Error) ValidateFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return (false, "File is empty");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return (false, $"File size exceeds maximum of {MaxFileSizeBytes / (1024 * 1024)}MB");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedFileExtensions.Contains(extension))
        {
            return (false, $"File type '{extension}' not allowed. Allowed types: {string.Join(", ", AllowedFileExtensions)}");
        }

        return (true, null);
    }

    /// <summary>
    /// Validate BoQ data structure
    /// </summary>
    public static (bool IsValid, string? Error) ValidateBoq(BoqDto boq)
    {
        if (boq == null)
        {
            return (false, "BoQ data is required");
        }

        if (boq.Items == null || !boq.Items.Any())
        {
            return (false, "BoQ must contain at least one item");
        }

        if (boq.Items.Count > 10000)
        {
            return (false, "BoQ contains too many items (max: 10,000)");
        }

        if (boq.Stages == null || !boq.Stages.Any())
        {
            return (false, "BoQ must contain at least one stage");
        }

        // Validate items
        for (int i = 0; i < boq.Items.Count; i++)
        {
            var item = boq.Items[i];

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                return (false, $"Item {i + 1}: Name is required");
            }

            if (item.Name.Length > 500)
            {
                return (false, $"Item {i + 1}: Name too long (max: 500 characters)");
            }

            if (string.IsNullOrWhiteSpace(item.Unit))
            {
                return (false, $"Item {i + 1}: Unit is required");
            }

            if (item.Qty <= 0)
            {
                return (false, $"Item {i + 1}: Quantity must be greater than zero");
            }

            if (item.Qty > 1_000_000)
            {
                return (false, $"Item {i + 1}: Quantity too large (max: 1,000,000)");
            }

            if (string.IsNullOrWhiteSpace(item.Stage))
            {
                return (false, $"Item {i + 1}: Stage is required");
            }
        }

        // Validate stages
        for (int i = 0; i < boq.Stages.Count; i++)
        {
            var stage = boq.Stages[i];

            if (string.IsNullOrWhiteSpace(stage.Code))
            {
                return (false, $"Stage {i + 1}: Code is required");
            }

            if (stage.Forecast <= 0)
            {
                return (false, $"Stage {i + 1}: Forecast must be greater than zero");
            }

            if (stage.Forecast > 1_000_000_000)
            {
                return (false, $"Stage {i + 1}: Forecast too large (max: 1 billion)");
            }
        }

        return (true, null);
    }

    /// <summary>
    /// Validate optimization request
    /// </summary>
    public static (bool IsValid, string? Error) ValidateOptimizationRequest(OptimizationRequest request)
    {
        if (request == null)
        {
            return (false, "Request is required");
        }

        // Validate matched items
        if (request.MatchedItems == null || !request.MatchedItems.Any())
        {
            return (false, "At least one matched item is required");
        }

        if (request.MatchedItems.Count > 10000)
        {
            return (false, "Too many items (max: 10,000)");
        }

        // Validate stages
        if (request.Stages == null || !request.Stages.Any())
        {
            return (false, "At least one stage is required");
        }

        if (request.Stages.Count > 100)
        {
            return (false, "Too many stages (max: 100)");
        }

        foreach (var stage in request.Stages)
        {
            if (stage.Forecast <= 0)
            {
                return (false, $"Stage '{stage.Code}': Forecast must be greater than zero");
            }
        }

        // Validate lambda
        if (request.Lambda < 0)
        {
            return (false, "Lambda must be non-negative");
        }

        if (request.Lambda > 1_000_000)
        {
            return (false, "Lambda too large (max: 1,000,000)");
        }

        // Validate coefficient bounds
        if (request.MinCoeff < 0)
        {
            return (false, "Coefficient minimum must be non-negative");
        }

        if (request.MaxCoeff > 10)
        {
            return (false, "Coefficient maximum too large (max: 10)");
        }

        if (request.MinCoeff >= request.MaxCoeff)
        {
            return (false, "Coefficient minimum must be less than maximum");
        }

        return (true, null);
    }

    /// <summary>
    /// Validate price base entries
    /// </summary>
    public static (bool IsValid, string? Error) ValidatePriceBase(List<PriceBaseEntry> priceBase)
    {
        if (priceBase == null || !priceBase.Any())
        {
            return (false, "Price base is required");
        }

        if (priceBase.Count > 100000)
        {
            return (false, "Price base too large (max: 100,000 entries)");
        }

        for (int i = 0; i < priceBase.Count; i++)
        {
            var entry = priceBase[i];

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                return (false, $"Entry {i + 1}: Name is required");
            }

            if (entry.Name.Length > 500)
            {
                return (false, $"Entry {i + 1}: Name too long (max: 500 characters)");
            }

            if (string.IsNullOrWhiteSpace(entry.Unit))
            {
                return (false, $"Entry {i + 1}: Unit is required");
            }

            if (entry.BasePrice < 0)
            {
                return (false, $"Entry {i + 1}: Base price cannot be negative");
            }

            if (entry.BasePrice > 1_000_000)
            {
                return (false, $"Entry {i + 1}: Base price too large (max: 1,000,000)");
            }
        }

        return (true, null);
    }
}
