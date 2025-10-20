using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Core.Engine.Services;
using Core.Engine.Models;

namespace FirstMake.Performance;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class FuzzyMatcherBenchmarks
{
    private FuzzyMatcher _matcher = null!;
    private List<ItemDto> _boqItems = null!;
    private List<PriceBaseEntry> _priceBase = null!;

    [GlobalSetup]
    public void Setup()
    {
        _matcher = new FuzzyMatcher();

        // Generate test data
        _boqItems = GenerateBoqItems(100);
        _priceBase = GeneratePriceBase(1000);
    }

    [Benchmark]
    public void Match_100_Items()
    {
        foreach (var item in _boqItems)
        {
            _matcher.FindBestMatches(item.Description, _priceBase.Select(p => p.Description), 5);
        }
    }

    [Benchmark]
    public void Match_Single_Item_Against_1000()
    {
        _matcher.FindBestMatches(
            "Бетон C16/20 с арматура, вибриран", 
            _priceBase.Select(p => p.Description), 
            5
        );
    }

    [Benchmark]
    public void Match_Single_Item_Against_100()
    {
        _matcher.FindBestMatches(
            "Бетон C16/20 с арматура, вибриран",
            _priceBase.Take(100).Select(p => p.Description),
            5
        );
    }

    [Benchmark]
    public void Match_With_Caching()
    {
        var item = _boqItems[0];
        // First call - no cache
        _matcher.FindBestMatches(item.Description, _priceBase.Select(p => p.Description), 5);
        
        // Second call - should be cached
        _matcher.FindBestMatches(item.Description, _priceBase.Select(p => p.Description), 5);
    }

    private List<ItemDto> GenerateBoqItems(int count)
    {
        var items = new List<ItemDto>();
        var descriptions = new[]
        {
            "Бетон C16/20 с арматура",
            "Изкопни работи на ръка",
            "Мазилка с циментов разтвор",
            "Замазка на под с циментов разтвор",
            "Боядисване с латекс",
            "Теракот 30/30 см",
            "Фаянс 20/25 см бял",
            "ПВЦ дограма 100/120 см",
            "Входна врата метална",
            "Покривни керемиди глинени"
        };

        for (int i = 0; i < count; i++)
        {
            items.Add(new ItemDto
            {
                Description = descriptions[i % descriptions.Length] + $" - вариант {i}",
                Unit = "м3",
                Quantity = 10.5
            });
        }

        return items;
    }

    private List<PriceBaseEntry> GeneratePriceBase(int count)
    {
        var entries = new List<PriceBaseEntry>();
        var descriptions = new[]
        {
            "Бетон C16/20 вибриран с арматура",
            "Изкопни работи механизирани",
            "Изкопни работи ръчни",
            "Мазилка вътрешна с циментов разтвор",
            "Мазилка външна с циментов разтвор",
            "Замазка на под",
            "Боядисване с латексова боя",
            "Теракотни плочки 30х30",
            "Теракотни плочки 40х40",
            "Фаянс бял",
            "ПВЦ дограма двукамерна",
            "Врата входна метална",
            "Керемиди покривни"
        };

        for (int i = 0; i < count; i++)
        {
            entries.Add(new PriceBaseEntry
            {
                Code = $"PB-{i:D4}",
                Description = descriptions[i % descriptions.Length] + (i > descriptions.Length ? $" - тип {i / descriptions.Length}" : ""),
                Unit = "м3",
                UnitPrice = 100.0 + (i % 50) * 10
            });
        }

        return entries;
    }
}
