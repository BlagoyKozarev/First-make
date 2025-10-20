using Core.Engine.Models;
using Fastenshtein;

namespace Core.Engine.Services;

/// <summary>
/// Fuzzy matching service for BoQ items to price base entries
/// Uses Levenshtein distance and token-sort matching
/// </summary>
public class FuzzyMatcher
{
    private readonly UnitNormalizer _unitNormalizer;
    private const double MinScoreThreshold = 0.6; // 60% similarity minimum

    public FuzzyMatcher(UnitNormalizer unitNormalizer)
    {
        _unitNormalizer = unitNormalizer;
    }

    /// <summary>
    /// Find best matches for an item from price base
    /// </summary>
    public List<CandidateMatch> FindCandidates(
        ItemDto item,
        IEnumerable<PriceBaseEntry> priceBase,
        int topN = 5)
    {
        var normalizedItemUnit = _unitNormalizer.Normalize(item.Unit);
        var candidates = new List<CandidateMatch>();

        foreach (var entry in priceBase)
        {
            var normalizedEntryUnit = _unitNormalizer.Normalize(entry.Unit);
            
            // Unit must match (or be equivalent)
            if (!string.Equals(normalizedItemUnit, normalizedEntryUnit, StringComparison.OrdinalIgnoreCase))
                continue;

            var score = CalculateScore(item.Name, entry);
            
            if (score >= MinScoreThreshold)
            {
                candidates.Add(new CandidateMatch
                {
                    Entry = entry,
                    Score = score
                });
            }
        }

        // Sort by score descending and take top N
        return candidates
            .OrderByDescending(c => c.Score)
            .Take(topN)
            .ToList();
    }

    /// <summary>
    /// Calculate similarity score between item name and price entry
    /// Considers both main name and aliases
    /// </summary>
    private double CalculateScore(string itemName, PriceBaseEntry entry)
    {
        var normalizedItem = TextNormalizer.Normalize(itemName);
        var scores = new List<double>();

        // Score against main name
        var mainScore = LevenshteinSimilarity(normalizedItem, TextNormalizer.Normalize(entry.Name));
        scores.Add(mainScore);

        // Score against aliases if available
        if (entry.Aliases != null)
        {
            foreach (var alias in entry.Aliases)
            {
                var aliasScore = LevenshteinSimilarity(normalizedItem, TextNormalizer.Normalize(alias));
                scores.Add(aliasScore);
            }
        }

        // Token-sort score (for phrases with word order differences)
        var tokenScore = TokenSortSimilarity(itemName, entry.Name);
        scores.Add(tokenScore);

        // Return max score (best match wins)
        return scores.Max();
    }

    /// <summary>
    /// Levenshtein distance-based similarity (0.0 to 1.0)
    /// </summary>
    private static double LevenshteinSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2))
            return 1.0;
        
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            return 0.0;

        var lev = new Levenshtein(s1);
        var distance = lev.DistanceFrom(s2);
        var maxLen = Math.Max(s1.Length, s2.Length);
        
        return 1.0 - ((double)distance / maxLen);
    }

    /// <summary>
    /// Token-sort similarity: sort words and compare
    /// </summary>
    private static double TokenSortSimilarity(string s1, string s2)
    {
        var tokens1 = string.Join(" ", TextNormalizer.TokenSort(s1));
        var tokens2 = string.Join(" ", TextNormalizer.TokenSort(s2));
        
        return LevenshteinSimilarity(tokens1, tokens2);
    }
}

/// <summary>
/// Candidate match with score
/// </summary>
public record CandidateMatch
{
    public required PriceBaseEntry Entry { get; init; }
    public required double Score { get; init; }
}
