using Xunit;
using Fishbowl.Data;
using Fishbowl.Core;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;

namespace Fishbowl.Tests;

public class ResourceProviderTests : IDisposable
{
    private readonly string _tempModsDir;

    public ResourceProviderTests()
    {
        _tempModsDir = Path.Combine(Path.GetTempPath(), "fishbowl_tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempModsDir);
    }

    [Fact]
    public async Task GetAsync_DiskOverridesEmbedded_Test()
    {
        // Arrange
        var testPath = "test.txt";
        var diskContent = "Disk Content";
        File.WriteAllText(Path.Combine(_tempModsDir, testPath), diskContent);

        var provider = new ResourceProvider(
            modsPath: _tempModsDir, 
            embeddedAssembly: typeof(ResourceProvider).Assembly
        );

        // Act
        var resource = await provider.GetAsync(testPath);

        // Assert
        Assert.NotNull(resource);
        Assert.Equal(ResourceSource.Disk, resource.Source);
        using var reader = new StreamReader(resource.Content);
        var content = await reader.ReadToEndAsync();
        Assert.Equal(diskContent, content);
    }

    [Fact]
    public async Task GetAsync_ReturnsEmbeddedWhenDiskMissing_Test()
    {
        // Arrange
        // (test.txt IS embedded in Fishbowl.Data)
        var provider = new ResourceProvider(
            modsPath: _tempModsDir, 
            embeddedAssembly: typeof(ResourceProvider).Assembly
        );

        // Act
        var resource = await provider.GetAsync("test.txt");

        // Assert
        Assert.NotNull(resource);
        Assert.Equal(ResourceSource.Embedded, resource.Source);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullWhenNotFound_Test()
    {
        // Arrange
        var provider = new ResourceProvider(_tempModsDir);

        // Act
        var resource = await provider.GetAsync("non-existent-file.xyz");

        // Assert
        Assert.Null(resource);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempModsDir))
        {
            Directory.Delete(_tempModsDir, true);
        }
    }
}
