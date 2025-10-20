using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Core.Engine.Services;

namespace FirstMake.Performance;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class TextNormalizerBenchmarks
{
    private TextNormalizer _normalizer = null!;
    private string[] _testStrings = null!;

    [GlobalSetup]
    public void Setup()
    {
        _normalizer = new TextNormalizer();
        _testStrings = new[]
        {
            "Бетон C16/20 с арматура, вибриран",
            "ИЗКОПНИ РАБОТИ на ръка в почви I-IV категория",
            "Мазилка с циментов разтвор 1:3, дебелина 15mm",
            "Боядисване с латексова боя - 2 пласта",
            "ПВЦ дограма двукамерна - размер 100/120cm"
        };
    }

    [Benchmark]
    public void Normalize_Short_Text()
    {
        _normalizer.Normalize("Бетон C16/20");
    }

    [Benchmark]
    public void Normalize_Medium_Text()
    {
        _normalizer.Normalize("Мазилка с циментов разтвор 1:3, дебелина 15mm");
    }

    [Benchmark]
    public void Normalize_Long_Text()
    {
        _normalizer.Normalize("ИЗКОПНИ РАБОТИ на ръка в почви I-IV категория с товарене на самосвали и транспорт до 5 км");
    }

    [Benchmark]
    public void Normalize_100_Strings()
    {
        for (int i = 0; i < 100; i++)
        {
            _normalizer.Normalize(_testStrings[i % _testStrings.Length]);
        }
    }

    [Benchmark]
    public void Normalize_With_Numbers()
    {
        _normalizer.Normalize("Бетон C16/20 с арматура 10kg/m3, дебелина 150mm");
    }

    [Benchmark]
    public void Normalize_With_Special_Chars()
    {
        _normalizer.Normalize("Мазилка с циментов разтвор 1:3 (вътрешна), дебелина 15-20mm");
    }
}
