using System.Text;
using System.Text.RegularExpressions;

namespace Core.Engine.Services;

/// <summary>
/// Text normalization for matching: trim, lowercase, collapse punctuation/whitespace
/// </summary>
public static partial class TextNormalizer
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
    
    [GeneratedRegex(@"[^\p{L}\p{N}\s]")]
    private static partial Regex PunctuationRegex();

    /// <summary>
    /// Normalize text for fuzzy matching
    /// </summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Lowercase
        var normalized = text.ToLowerInvariant();
        
        // Remove punctuation (keep letters, numbers, spaces)
        normalized = PunctuationRegex().Replace(normalized, " ");
        
        // Collapse whitespace
        normalized = WhitespaceRegex().Replace(normalized, " ");
        
        return normalized.Trim();
    }

    /// <summary>
    /// Token-based normalization for token-sort matching
    /// </summary>
    public static string[] TokenSort(string text)
    {
        var normalized = Normalize(text);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Array.Sort(tokens, StringComparer.Ordinal);
        return tokens;
    }
}
