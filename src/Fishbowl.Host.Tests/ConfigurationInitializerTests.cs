using Fishbowl.Core.Repositories;
using Fishbowl.Data;
using Fishbowl.Host.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fishbowl.Host.Tests;

public class ConfigurationInitializerTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigurationInitializerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fishbowl_cfg_init_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task Initializer_PopulatesCache_FromSystemDb_Test()
    {
        // Arrange — write config into system.db directly
        var factory = new DatabaseFactory(_tempDir);
        var repo = new Fishbowl.Data.Repositories.SystemRepository(factory);
        await repo.SetConfigAsync("Google:ClientId", "id-from-db", TestContext.Current.CancellationToken);
        await repo.SetConfigAsync("Google:ClientSecret", "secret-from-db", TestContext.Current.CancellationToken);

        var cache = new ConfigurationCache();
        var initializer = new ConfigurationInitializer(repo, cache, NullLogger<ConfigurationInitializer>.Instance);

        // Act
        await initializer.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("id-from-db", cache.Get("Google:ClientId"));
        Assert.Equal("secret-from-db", cache.Get("Google:ClientSecret"));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }
}
