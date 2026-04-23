using System.Data;
using Dapper;
using Fishbowl.Core.Util;
using Fishbowl.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Fishbowl.Host.Tests;

public class SchemaV3MigrationTests : IDisposable
{
    private readonly string _dataDir;

    public SchemaV3MigrationTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "fishbowl_schema_v3_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_dataDir, "users"));
    }

    [Fact]
    public async Task ApplyV3_AddsThreeProtectionColumnsAndSeedsReservedTags()
    {
        var userId = "v2-user";
        var dbPath = Path.Combine(_dataDir, "users", $"{userId}.db");

        // Seed: hand-crafted v2 DB (notes + notes_fts + tags, no protection
        // columns) with one user-created tag. user_version stays at 2 so the
        // factory runs ApplyV3.
        using (var seed = new SqliteConnection($"Data Source={dbPath}"))
        {
            seed.Open();
            seed.Execute(@"
                CREATE TABLE notes (
                    id TEXT PRIMARY KEY, title TEXT NOT NULL, content TEXT,
                    content_secret BLOB, type TEXT NOT NULL DEFAULT 'note', tags TEXT,
                    created_by TEXT NOT NULL, created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL, pinned INTEGER DEFAULT 0,
                    archived INTEGER DEFAULT 0);");
            seed.Execute("CREATE VIRTUAL TABLE notes_fts USING fts5(title, content, tags);");
            seed.Execute(@"
                CREATE TABLE tags (
                    name TEXT PRIMARY KEY, color TEXT NOT NULL, created_at TEXT NOT NULL);");

            var now = DateTime.UtcNow.ToString("o");
            seed.Execute("INSERT INTO tags(name, color, created_at) VALUES ('work', 'blue', @now)",
                new { now });

            seed.Execute("PRAGMA user_version = 2");
        }
        SqliteConnection.ClearAllPools();

        var factory = new DatabaseFactory(_dataDir);
        using var db = factory.CreateConnection(userId);

        var version = await db.ExecuteScalarAsync<long>("PRAGMA user_version");
        Assert.True(version >= 3, $"expected version >= 3 after v3 migration, got {version}");

        // All three protection columns exist.
        var columns = (await db.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('tags')")).ToList();
        Assert.Contains("is_system", columns);
        Assert.Contains("user_assignable", columns);
        Assert.Contains("user_removable", columns);

        // Exactly the reserved system tags are present with is_system = 1.
        var sysRows = (await db.QueryAsync<(string Name, long IsSystem)>(
            "SELECT name AS Name, is_system AS IsSystem FROM tags WHERE is_system = 1 ORDER BY name"))
            .ToList();
        Assert.Equal(
            new[] { "review:pending", "source:mcp" },
            sysRows.Select(r => r.Name).ToArray());

        // Pre-existing user tag preserved, gets the column defaults (not system,
        // assignable, removable).
        var userTag = await db.QuerySingleAsync<(string Name, long IsSystem, long Assign, long Remove)>(
            @"SELECT name AS Name, is_system AS IsSystem,
                     user_assignable AS Assign, user_removable AS Remove
              FROM tags WHERE name = 'work'");
        Assert.Equal(0, userTag.IsSystem);
        Assert.Equal(1, userTag.Assign);
        Assert.Equal(1, userTag.Remove);
    }

    [Fact]
    public async Task ApplyV3_SeedsSystemTagsWithCorrectFlags()
    {
        var factory = new DatabaseFactory(_dataDir);
        using var db = factory.CreateConnection("flag-user");

        var pending = await db.QuerySingleAsync<(long Assign, long Remove)>(
            @"SELECT user_assignable AS Assign, user_removable AS Remove
              FROM tags WHERE name = 'review:pending'");
        Assert.Equal(0, pending.Assign);   // users can't assign manually
        Assert.Equal(1, pending.Remove);   // users remove to approve

        var mcp = await db.QuerySingleAsync<(long Assign, long Remove)>(
            @"SELECT user_assignable AS Assign, user_removable AS Remove
              FROM tags WHERE name = 'source:mcp'");
        Assert.Equal(0, mcp.Assign);       // only MCP writes attach it
        Assert.Equal(0, mcp.Remove);       // provenance — locked on
    }

    [Fact]
    public async Task ApplyV3_OnFreshDb_SeedsSystemTagsOnly()
    {
        var factory = new DatabaseFactory(_dataDir);
        using var db = factory.CreateConnection("fresh-user");

        var version = await db.ExecuteScalarAsync<long>("PRAGMA user_version");
        Assert.True(version >= 3, $"expected version >= 3 after v3 migration, got {version}");

        var rows = (await db.QueryAsync<(string Name, long IsSystem)>(
            "SELECT name AS Name, is_system AS IsSystem FROM tags ORDER BY name")).ToList();
        Assert.Equal(SystemTags.Seeds.Count, rows.Count);
        Assert.All(rows, r => Assert.Equal(1, r.IsSystem));
    }

    [Fact]
    public async Task ApplyV3_SystemTagColorsAreFromPalette()
    {
        var factory = new DatabaseFactory(_dataDir);
        using var db = factory.CreateConnection("palette-user");

        var colors = (await db.QueryAsync<string>(
            "SELECT color FROM tags WHERE is_system = 1")).ToList();
        Assert.All(colors, c => Assert.Contains(c, TagPalette.Slots));
    }

    [Fact]
    public async Task ApplyV3_SystemTagsHelperListsSameNames()
    {
        // Defence against drift: SystemTags.ReservedNames is the single source
        // of truth used by both the migration and the guards. Stays in sync
        // with what the migration actually seeds.
        var factory = new DatabaseFactory(_dataDir);
        using var db = factory.CreateConnection("helper-user");

        var seeded = (await db.QueryAsync<string>(
            "SELECT name FROM tags WHERE is_system = 1 ORDER BY name")).ToList();
        var reserved = SystemTags.ReservedNames.OrderBy(n => n).ToList();

        Assert.Equal(reserved, seeded);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dataDir))
        {
            try { Directory.Delete(_dataDir, true); } catch { }
        }
    }
}
