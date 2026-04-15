using Xunit;
using Microsoft.Data.Sqlite;
using Fishbowl.Data;
using Dapper;
using System.IO;
using System.Data;

namespace Fishbowl.Tests;

public class DatabaseFactoryTests : IDisposable
{
    private readonly string _tempDbDir;

    public DatabaseFactoryTests()
    {
        _tempDbDir = Path.Combine(Path.GetTempPath(), "fishbowl_db_tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDbDir);
    }

    [Fact]
    public void CreateConnection_CreatesPhysicalFile_Test()
    {
        // Arrange
        var userId = "test_user_1";
        var factory = new DatabaseFactory(_tempDbDir);

        // Act
        using var connection = factory.CreateConnection(userId);

        // Assert
        var dbPath = Path.Combine(_tempDbDir, $"{userId}.db");
        Assert.True(File.Exists(dbPath));
    }

    [Fact]
    public void EnsureInitialized_CreatesSchema_Test()
    {
        // Arrange
        var userId = "test_user_2";
        var factory = new DatabaseFactory(_tempDbDir);

        // Act
        using var connection = factory.CreateConnection(userId);

        // Assert
        var tables = connection.Query<string>("SELECT name FROM sqlite_master WHERE type='table'").ToList();
        
        Assert.Contains("notes", tables);
        Assert.Contains("events", tables);
        Assert.Contains("sync_sources", tables);
        Assert.Contains("reminders", tables);
    }

    [Fact]
    public void EnsureInitialized_SetsUserVersion_Test()
    {
        // Arrange
        var userId = "test_user_3";
        var factory = new DatabaseFactory(_tempDbDir);

        // Act
        using var connection = factory.CreateConnection(userId);

        // Assert
        var version = connection.ExecuteScalar<int>("PRAGMA user_version");
        Assert.Equal(1, version);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDbDir))
        {
            Directory.Delete(_tempDbDir, true);
        }
    }
}
