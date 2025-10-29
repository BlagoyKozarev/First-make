using Xunit;
using Core.Engine.Services;
using System.IO;

namespace Core.Engine.Tests;

public class UkazaniaParserTests
{
    private readonly string _samplesPath = "/workspaces/First-make/Schemas/examples/КСС ";

    [Fact]
    public void ParseFromText_ForecastSection_ShouldExtract19Stages()
    {
        // Arrange
        var parser = new UkazaniaParser();
        var sampleText = @"
7. Прогнозна стойност
Етап 1: 128,203.60 лв
Етап 2: 72,898.04 лв
Етап 3: 124,537.89 лв
Етап 4: 155,321.45 лв
Етап 5: 89,432.12 лв
Етап 6: 201,876.33 лв
Етап 7: 67,543.21 лв
Етап 8: 143,298.76 лв
Етап 9: 178,654.92 лв
Етап 10: 95,123.45 лв
Етап 11: 112,890.34 лв
Етап 12: 186,543.27 лв
Етап 13: 78,901.23 лв
Етап 14: 234,567.89 лв
Етап 15: 145,678.90 лв
Етап 16: 98,765.43 лв
Етап 17: 167,890.12 лв
Етап 18: 175,722.24 лв
Етап 19: 31,943.20 лв
Обща стойност: 2,688,791.39 лв (без ДДС)
";

        // Act
        var forecasts = parser.ParseFromText(sampleText);

        // Assert
        Assert.NotNull(forecasts);
        Assert.Equal(19, forecasts.Stages.Count);
        Assert.Equal(2688791.39m, forecasts.TotalForecast);

        // Check first stage
        var stage1 = forecasts.Stages["Етап 1"];
        Assert.Equal(128203.60m, stage1.Forecast);
    }

    [Fact(Skip = "Requires Python and python-docx installed")]
    public async Task ParseFromWordAsync_RealFile_ShouldExtract19Stages()
    {
        // Arrange
        var filePath = Path.Combine(_samplesPath, "Указания 2024 Г.(39176775).docx");
        if (!File.Exists(filePath))
        {
            Assert.True(true, "Указания file not found");
            return;
        }

        var parser = new UkazaniaParser();

        // Act
        var forecasts = await parser.ParseFromWordAsync(filePath, "test-file-1");

        // Assert
        Assert.NotNull(forecasts);
        Assert.Equal(19, forecasts.Stages.Count);
        Assert.True(forecasts.TotalForecast > 2000000); // Should be ~2.6M
    }
}
