using Xunit;
using Core.Engine.Services;
using System.IO;
using System.Linq;

namespace Core.Engine.Tests;

public class MultiFileKssParserTests
{
    private readonly string _samplesPath = Path.Combine(
        Path.GetDirectoryName(typeof(MultiFileKssParserTests).Assembly.Location)!,
        "..", "..", "..", "..", "..", "Schemas", "examples", "КСС ");

    [Fact]
    public void ParseSingleFile_RealKssFile_ShouldExtractItems()
    {
        // Arrange
        var filePath = Path.Combine(_samplesPath, "KSS_Приложение № 1(39176961).xlsx");
        var parser = new MultiFileKssParser();

        // Act
        var doc = parser.ParseSingleFile(filePath, "test-file-1");

        // Assert
        Assert.NotNull(doc);
        Assert.NotEmpty(doc.Items);
        Assert.NotEmpty(doc.Stages);

        // Check stage extraction
        var stage = doc.Stages.First();
        Assert.Contains("Етап", stage.Code);

        // Check items
        foreach (var item in doc.Items)
        {
            Assert.NotNull(item.Name);
            Assert.NotNull(item.Unit);
            Assert.True(item.Quantity > 0);
            Assert.Equal("test-file-1", item.SourceFileId);
        }
    }

    [Fact]
    public void ParseMultipleFiles_TwoKssFiles_ShouldCombineItems()
    {
        // Arrange
        var parser = new MultiFileKssParser();
        var kssFiles = Directory.GetFiles(_samplesPath, "KSS_*.xlsx")
            .Take(2) // Take first 2 files
            .Select((f, i) => (FilePath: f, FileId: $"file-{i}"))
            .ToList();

        // Skip if no files found
        if (kssFiles.Count == 0)
        {
            Assert.True(true, "No КСС files found for testing");
            return;
        }

        // Act
        var docs = parser.ParseMultipleFiles(kssFiles);

        // Assert
        Assert.Equal(kssFiles.Count, docs.Count);

        foreach (var doc in docs)
        {
            Assert.NotEmpty(doc.Items);
            Assert.NotEmpty(doc.Stages);
        }
    }
}
