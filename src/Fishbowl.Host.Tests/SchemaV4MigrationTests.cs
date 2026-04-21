using Dapper;
using Fishbowl.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Fishbowl.Host.Tests;

// v4 adds sqlite-vec support: the extension loads on every context open, and
// `vec_notes` virtual table is created alongside the existing notes/notes_fts
// pair. No data migration needed — notes embed lazily on next write.
public class SchemaV4MigrationTests : IDisposable
{
    private readonly string _dataDir;

    public SchemaV4MigrationTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "fishbowl_schema_v4_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_dataDir, "users"));
    }

    [Fact]
    public async Task ApplyV4_OnFreshDb_CreatesVecNotesAndSetsVersion4()
    {
        var factory = new DatabaseFactory(_dataDir);
        using var db = factory.CreateConnection("fresh-user");

        var version = await db.ExecuteScalarAsync<long>("PRAGMA user_version");
        Assert.Equal(4, version);

        var vecTable = await db.ExecuteScalarAsync<string?>(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'vec_notes'");
        Assert.Equal("vec_notes", vecTable);
    }

    [Fact]
    public async Task ApplyV4_LoadsVec0Extension_VecVersionCallable()
    {
        var factory = new DatabaseFactory(_dataDir);
        using var db = factory.CreateConnection("loader-user");

        // If the extension isn't loaded, vec_version() isn't registered and
        // this throws. Proves SqliteVecLoader ran on connection open.
        var vecVersion = await db.ExecuteScalarAsync<string>("SELECT vec_version()");
        Assert.False(string.IsNullOrWhiteSpace(vecVersion));
    }

    [Fact]
    public async Task ApplyV4_MigratesV3Db_PreservesExistingData()
    {
        var userId = "v3-user";
        var dbPath = Path.Combine(_dataDir, "users", $"{userId}.db");

        // Seed a minimal v3 DB. Schema mirrors the v1+v2+v3 shape at the point
        // v4 gets applied — we only need the tags table to check that
        // user tags survive the v4 upgrade.
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
                    name TEXT PRIMARY KEY, color TEXT NOT NULL, created_at TEXT NOT NULL,
                    is_system INTEGER NOT NULL DEFAULT 0,
                    user_assignable INTEGER NOT NULL DEFAULT 1,
                    user_removable INTEGER NOT NULL DEFAULT 1);");

            var now = DateTime.UtcNow.ToString("o");
            seed.Execute("INSERT INTO tags(name, color, created_at) VALUES ('work', 'blue', @now)",
                new { now });

            seed.Execute("PRAGMA user_version = 3");
        }
        SqliteConnection.ClearAllPools();

        var factory = new DatabaseFactory(_dataDir);
        using var db = factory.CreateConnection(userId);

        var version = await db.ExecuteScalarAsync<long>("PRAGMA user_version");
        Assert.Equal(4, version);

        var userTag = await db.ExecuteScalarAsync<string?>(
            "SELECT name FROM tags WHERE name = 'work'");
        Assert.Equal("work", userTag);

        var vecTable = await db.ExecuteScalarAsync<string?>(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'vec_notes'");
        Assert.Equal("vec_notes", vecTable);
    }

    [Fact]
    public async Task ApplyV4_VecNotesAcceptsInsertAndSelect()
    {
        // End-to-end sanity: the virtual table is writable with the expected
        // shape (TEXT id + FLOAT[384] embedding) and searchable via MATCH.
        var factory = new DatabaseFactory(_dataDir);
        using var db = factory.CreateConnection("roundtrip-user");

        var vec = new float[384];
        for (var i = 0; i < vec.Length; i++) vec[i] = 0.01f * i;
        var blob = new byte[vec.Length * 4];
        Buffer.BlockCopy(vec, 0, blob, 0, blob.Length);

        await db.ExecuteAsync(
            "INSERT INTO vec_notes(id, embedding) VALUES ('n1', @v)",
            new { v = blob });

        var rows = (await db.QueryAsync<string>(
            "SELECT id FROM vec_notes WHERE embedding MATCH @q AND k = 3",
            new { q = blob })).ToList();
        Assert.Contains("n1", rows);
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
