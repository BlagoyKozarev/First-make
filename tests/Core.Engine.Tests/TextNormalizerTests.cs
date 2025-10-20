using Core.Engine.Services;

namespace Core.Engine.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("  Hello World  ", "hello world")]
    [InlineData("Изкопни   работи", "изкопни работи")]
    [InlineData("Test-123_Value", "test 123 value")]
    [InlineData("бр.", "бр")]
    [InlineData("кв.м.", "кв м")]
    public void Normalize_ShouldTrimLowercaseAndCollapse(string input, string expected)
    {
        var result = TextNormalizer.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TokenSort_ShouldSortTokensAlphabetically()
    {
        var tokens = TextNormalizer.TokenSort("работи изкопни механизирани");
        Assert.Equal(new[] { "изкопни", "механизирани", "работи" }, tokens);
    }
}
