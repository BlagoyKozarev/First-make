using Core.Engine.Models;

namespace Core.Engine.Services;

/// <summary>
/// Unified matching service for multi-file BOQ processing
/// Ensures same (Name, Unit) pairs get same match across all files
/// </summary>
public class UnifiedMatcher
{
    private readonly FuzzyMatcher _fuzzyMatcher;
    private readonly Dictionary<string, MatchResult> _unifiedMatches;
    private readonly object _lock = new object();

    public UnifiedMatcher(FuzzyMatcher fuzzyMatcher)
    {
        _fuzzyMatcher = fuzzyMatcher;
        _unifiedMatches = new Dictionary<string, MatchResult>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Match all BOQ items from multiple documents against price base
    /// Ensures unified matching: same (Name, Unit) → same PriceEntry
    /// </summary>
    public UnifiedMatchResult MatchAll(List<BoqDocument> boqDocuments, List<PriceEntry> priceBase)
    {
        lock (_lock)
        {
            // Clear previous matches to allow re-matching
            _unifiedMatches.Clear();
            
            var itemMatches = new Dictionary<string, ItemMatch>();
            var unmatchedItems = new List<BoqItemDto>();

            // Process all items from all documents
            foreach (var doc in boqDocuments)
            {
                foreach (var item in doc.Items)
                {
                    var key = GetUnifiedKey(item.Name, item.Unit);

                    // Check if we already matched this (Name, Unit) combination
                    if (_unifiedMatches.TryGetValue(key, out var existingMatch))
                    {
                        // Reuse existing match
                        itemMatches[item.Id] = new ItemMatch
                        {
                            ItemId = item.Id,
                            Item = item,
                            PriceEntry = existingMatch.PriceEntry,
                            Score = existingMatch.Score,
                            IsManual = false,
                            UnifiedKey = key
                        };
                    }
                    else
                    {
                        // Find new match
                        var candidates = _fuzzyMatcher.FindCandidates(
                            new ItemDto
                            {
                                Stage = item.StageCode,
                                Name = item.Name,
                                Unit = item.Unit,
                                Qty = item.Quantity
                            },
                            priceBase.Select(p => new PriceBaseEntry
                            {
                                Name = p.Name,
                                Unit = p.Unit,
                                BasePrice = p.BasePrice
                            }).ToList(),
                            topN: 5
                        );

                        if (candidates.Any() && candidates.First().Score >= 0.6)
                        {
                            var best = candidates.First();
                            var priceEntry = priceBase.First(p =>
                                p.Name == best.Entry.Name && p.Unit == best.Entry.Unit);

                            var matchResult = new MatchResult
                            {
                                PriceEntry = priceEntry,
                                Score = (decimal)best.Score
                            };

                            // Store unified match
                            _unifiedMatches[key] = matchResult;

                            itemMatches[item.Id] = new ItemMatch
                            {
                                ItemId = item.Id,
                                Item = item,
                                PriceEntry = priceEntry,
                                Score = (decimal)best.Score,
                                IsManual = false,
                                UnifiedKey = key
                            };
                        }
                        else
                        {
                            // No good match found
                            unmatchedItems.Add(item);

                            itemMatches[item.Id] = new ItemMatch
                            {
                                ItemId = item.Id,
                                Item = item,
                                PriceEntry = null,
                                Score = 0,
                                IsManual = false,
                                UnifiedKey = key
                            };
                        }
                    }
                }
            }

            return new UnifiedMatchResult
            {
                ItemMatches = itemMatches,
                UnifiedMatches = new Dictionary<string, MatchResult>(_unifiedMatches, StringComparer.OrdinalIgnoreCase),
                UnmatchedItems = unmatchedItems,
                Statistics = new MatchStatistics
                {
                    TotalItems = itemMatches.Count,
                    MatchedItems = itemMatches.Count(m => m.Value.PriceEntry != null),
                    UnmatchedItems = unmatchedItems.Count,
                    UniquePositions = _unifiedMatches.Count,
                    AverageScore = itemMatches.Where(m => m.Value.Score > 0).Average(m => (double?)m.Value.Score) ?? 0
                }
            };
        }
    }

    /// <summary>
    /// Override match for specific item (manual matching)
    /// Updates unified match so all items with same (Name, Unit) get new match
    /// </summary>
    public void OverrideMatch(string itemId, PriceEntry priceEntry, UnifiedMatchResult currentResult)
    {
        lock (_lock)
        {
            if (!currentResult.ItemMatches.TryGetValue(itemId, out var itemMatch))
            {
                throw new KeyNotFoundException($"Item {itemId} not found in match results");
            }

            var key = itemMatch.UnifiedKey;

            // Update unified match
            _unifiedMatches[key] = new MatchResult
            {
                PriceEntry = priceEntry,
                Score = 1.0m // Manual match = perfect score
            };

            // Update all items with same key
            foreach (var match in currentResult.ItemMatches.Values.Where(m => m.UnifiedKey == key))
            {
                match.PriceEntry = priceEntry;
                match.Score = 1.0m;
                match.IsManual = true;
            }
        }
    }

    /// <summary>
    /// Get unified candidates for manual matching
    /// Returns all unique (Name, Unit) combinations that need manual review
    /// </summary>
    public List<UnifiedCandidate> GetUnmatchedCandidates(UnifiedMatchResult result, List<PriceEntry> priceBase)
    {
        lock (_lock)
        {
            var unmatchedKeys = result.UnmatchedItems
                .Select(item => GetUnifiedKey(item.Name, item.Unit))
                .Distinct()
                .ToList();

            var candidates = new List<UnifiedCandidate>();

            foreach (var key in unmatchedKeys)
            {
                var sampleItem = result.UnmatchedItems.First(i => GetUnifiedKey(i.Name, i.Unit) == key);

                var topCandidates = _fuzzyMatcher.FindCandidates(
                    new ItemDto
                    {
                        Stage = sampleItem.StageCode,
                        Name = sampleItem.Name,
                        Unit = sampleItem.Unit,
                        Qty = sampleItem.Quantity
                    },
                    priceBase.Select(p => new PriceBaseEntry
                    {
                        Name = p.Name,
                        Unit = p.Unit,
                        BasePrice = p.BasePrice
                    }).ToList(),
                    topN: 5
                );

                candidates.Add(new UnifiedCandidate
                {
                    UnifiedKey = key,
                    Name = sampleItem.Name,
                    Unit = sampleItem.Unit,
                    OccurrenceCount = result.ItemMatches.Values.Count(m => m.UnifiedKey == key),
                    TopMatches = topCandidates.Select(c =>
                    {
                        var priceEntry = priceBase.First(p => p.Name == c.Entry.Name && p.Unit == c.Entry.Unit);
                        return new PriceCandidateMatch
                        {
                            PriceEntry = priceEntry,
                            Score = (decimal)c.Score
                        };
                    }).ToList()
                });
            }

            return candidates.OrderByDescending(c => c.OccurrenceCount).ToList();
        }
    }

    private string GetUnifiedKey(string name, string unit)
    {
        return $"{name?.Trim().ToLowerInvariant()}|{unit?.Trim().ToLowerInvariant()}";
    }
}

public record UnifiedMatchResult
{
    public required Dictionary<string, ItemMatch> ItemMatches { get; init; }
    public required Dictionary<string, MatchResult> UnifiedMatches { get; init; }
    public required List<BoqItemDto> UnmatchedItems { get; init; }
    public required MatchStatistics Statistics { get; init; }
}

public class ItemMatch
{
    public required string ItemId { get; init; }
    public required BoqItemDto Item { get; init; }
    public PriceEntry? PriceEntry { get; set; }
    public decimal Score { get; set; }
    public bool IsManual { get; set; }
    public required string UnifiedKey { get; init; }
}

public record MatchResult
{
    public required PriceEntry PriceEntry { get; init; }
    public required decimal Score { get; init; }
}

public record MatchStatistics
{
    public required int TotalItems { get; init; }
    public required int MatchedItems { get; init; }
    public required int UnmatchedItems { get; init; }
    public required int UniquePositions { get; init; }
    public required double AverageScore { get; init; }
}

public record UnifiedCandidate
{
    public required string UnifiedKey { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required int OccurrenceCount { get; init; }
    public required List<PriceCandidateMatch> TopMatches { get; init; }
}

public record PriceCandidateMatch
{
    public required PriceEntry PriceEntry { get; init; }
    public required decimal Score { get; init; }
}
