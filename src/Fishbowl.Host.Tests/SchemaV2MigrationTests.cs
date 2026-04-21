using System.Data;
using Dapper;
using Fishbowl.Core.Util;
using Fishbowl.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Fishbowl.Host.Tests;

public class SchemaV2MigrationTests : IDisposable
{
    private readonly string _dataDir;

    public SchemaV2MigrationTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "fishbowl_schema_v2_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_dataDir, "users"));
    }

    [Fact]
    public async Task ApplyV2_BackfillsTagsFromExistingNotes()
    {
        var userId = "v1-user";
        var dbPath = Path.Combine(_dataDir, "users", $"{userId}.db");

        // Seed: hand-crafted v1 DB (notes + notes_fts only) with two notes
        // carrying overlapping tag arrays. user_version stays at 1 so the
        // factory must run ApplyV2.
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

            var now = DateTime.UtcNow.ToString("o");
            seed.Execute(@"INSERT INTO notes(id, title, content, type, tags, created_by, created_at, updated_at)
                           VALUES (@id, @t, '', 'note', @tags, @uid, @now, @now)",
                new { id = "n1", t = "One", tags = "[\"work\",\"urgent\"]", uid = userId, now });
            seed.Execute(@"INSERT INTO notes(id, title, content, type, tags, created_by, created_at, updated_at)
                           VALUES (@id, @t, '', 'note', @tags, @uid, @now, @now)",
                new { id = "n2", t = "Two", tags = "[\"personal\",\"urgent\"]", uid = userId, now });

            seed.Execute("PRAGMA user_version = 1");
        }
        SqliteConnection.ClearAllPools();

        // Open via factory — triggers ApplyV2 + backfill.
        var factory = new DatabaseFactory(_dataDir);
        using var db = factory.CreateConnection(userId);

        // Factory chains v1 → v2 → v3. We're testing v2's backfill behaviour,
        // but the factory always runs every pending migration on open, so the
        // final user_version is CurrentVersion, not 2.
        var version = await db.ExecuteScalarAsync<long>("PRAGMA user_version");
        Assert.Equal(3, version);

        var noteCount = await db.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM notes");
        Assert.Equal(2, noteCount);

        // Filter to non-system tags — v3 seeds reserved system tags which
        // aren't part of the v2 backfill we're testing here.
        var tags = (await db.QueryAsync<(string Name, string Color)>(
            "SELECT name, color FROM tags WHERE is_system = 0 ORDER BY name")).ToList();
        Assert.Equal(3, tags.Count);
        Assert.Equal(new[] { "personal", "urgent", "work" }, tags.Select(t => t.Name).ToArray());
        Assert.All(tags, t => Assert.Contains(t.Color, TagPalette.Slots));

        // Default colors are deterministic — re-running DefaultFor must match.
        foreach (var t in tags)
        {
            Assert.Equal(TagPalette.DefaultFor(t.Name), t.Color);
        }
    }

    [Fact]
    public async Task ApplyV2_OnFreshDb_CreatesEmptyTagsTable()
    {
        var factory = new DatabaseFactory(_dataDir);
        using var db = factory.CreateConnection("fresh-user");

        var version = await db.ExecuteScalarAsync<long>("PRAGMA user_version");
        Assert.Equal(3, version);

        var tableExists = await db.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='tags'");
        Assert.Equal(1, tableExists);

        // V2 creates an empty tags table; V3 seeds the four reserved system
        // tags. The non-system row count is what V2 asserted here.
        var userRowCount = await db.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM tags WHERE is_system = 0");
        Assert.Equal(0, userRowCount);
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
