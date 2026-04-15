using Xunit;
using Fishbowl.Data;
using Fishbowl.Core;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace Fishbowl.Tests;

public class ResourceProviderTests : IDisposable
{
    private readonly string _tempModsDir;
    private readonly IMemoryCache _cache;

    public ResourceProviderTests()
    {
        _tempModsDir = Path.Combine(Path.GetTempPath(), "fishbowl_tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempModsDir);
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public async Task GetAsync_DiskOverridesEmbedded_Test()
    {
        // Arrange
        var testPath = "test.txt";
        var diskContent = "Disk Content";
        File.WriteAllText(Path.Combine(_tempModsDir, testPath), diskContent);

        var provider = new ResourceProvider(
            cache: _cache,
            modsPath: _tempModsDir, 
            embeddedAssembly: typeof(ResourceProvider).Assembly
        );

        // Act
        var resource = await provider.GetAsync(testPath);

        // Assert
        Assert.NotNull(resource);
        Assert.Equal(ResourceSource.Disk, resource.Source);
        var content = Encoding.UTF8.GetString(resource.Data);
        Assert.Equal(diskContent, content);
    }

    [Fact]
    public async Task GetAsync_CachesResourceAfterFirstRead_Test()
    {
        // Arrange
        var testPath = "cache_test.txt";
        var initialContent = "Initial Content";
        var filePath = Path.Combine(_tempModsDir, testPath);
        File.WriteAllText(filePath, initialContent);

        var provider = new ResourceProvider(_cache, _tempModsDir);

        // Act 1: First read (should hit disk)
        var firstResource = await provider.GetAsync(testPath);
        Assert.Equal(initialContent, Encoding.UTF8.GetString(firstResource!.Data));

        // Act 2: Modify disk
        File.WriteAllText(filePath, "Modified Content");

        // Act 3: Read again (should hit cache)
        var secondResource = await provider.GetAsync(testPath);

        // Assert
        Assert.Equal(initialContent, Encoding.UTF8.GetString(secondResource!.Data));
        Assert.Equal(ResourceSource.Disk, secondResource.Source); // Source is still Disk because that's where it was cached from
    }

    [Fact]
    public async Task ExistsAsync_UsesCache_Test()
    {
        // Arrange
        var testPath = "exists_cache_test.txt";
        var filePath = Path.Combine(_tempModsDir, testPath);
        File.WriteAllText(filePath, "exists");

        var provider = new ResourceProvider(_cache, _tempModsDir);
        await provider.GetAsync(testPath); // Cache it

        // Act
        File.Delete(filePath); // Delete from disk
        var exists = await provider.ExistsAsync(testPath);

        // Assert
        Assert.True(exists, "Should return true even if deleted from disk, because it is cached.");
    }

    [Fact]
    public async Task GetAsync_ReturnsEmbeddedWhenDiskMissing_Test()
    {
        // Arrange
        // (test.txt IS embedded in Fishbowl.Data)
        var provider = new ResourceProvider(
            cache: _cache,
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
        var provider = new ResourceProvider(_cache, _tempModsDir);

        // Act
        var resource = await provider.GetAsync("non-existent-file.xyz");

        // Assert
        Assert.Null(resource);
    }

    public void Dispose()
    {
        _cache.Dispose();
        if (Directory.Exists(_tempModsDir))
        {
            Directory.Delete(_tempModsDir, true);
        }
    }
}
