using Xunit;
using Core.Engine.Services;
using System.IO;
using System.Linq;

namespace Core.Engine.Tests;

public class PriceBaseLoaderTests
{
    private readonly string _samplesPath = "/workspaces/First-make/Schemas/examples/КСС ";
    
    [Fact]
    public void LoadFromExcel_RealFile_ShouldParse86Entries()
    {
        // Arrange
        var filePath = Path.Combine(_samplesPath, "единични цени по опис 01.xlsx");
        var loader = new PriceBaseLoader();
        
        // Act
        var priceBase = loader.LoadFromExcel(filePath, "test-file-1");
        
        // Assert
        Assert.NotNull(priceBase);
        Assert.Equal(86, priceBase.Count); // Expected 86 entries
        
        // Check first entry structure
        var firstEntry = priceBase.First();
        Assert.NotNull(firstEntry.Name);
        Assert.NotNull(firstEntry.Unit);
        Assert.True(firstEntry.BasePrice > 0);
    }
    
    [Fact]
    public void LoadFromMultipleFiles_TwoFiles_ShouldMergeWithoutDuplicates()
    {
        // Arrange
        var filePath = Path.Combine(_samplesPath, "единични цени по опис 01.xlsx");
        var loader = new PriceBaseLoader();
        
        var files = new List<(string FilePath, string FileId)>
        {
            (filePath, "file-1"),
            (filePath, "file-2") // Same file twice for testing
        };
        
        // Act
        var priceBase = loader.LoadFromMultipleFiles(files);
        
        // Assert
        Assert.NotNull(priceBase);
        Assert.Equal(86, priceBase.Count); // Should deduplicate
    }
}
