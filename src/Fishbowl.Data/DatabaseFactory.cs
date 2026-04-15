using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;

namespace Fishbowl.Data;

public class DatabaseFactory
{
    private readonly string _basePath;

    public DatabaseFactory(string basePath = "fishbowl-data/users")
    {
        _basePath = basePath;
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    public IDbConnection CreateConnection(string userId)
    {
        var dbPath = Path.Combine(_basePath, $"{userId}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        connection.Open();
        
        // Ensure the database is initialized
        EnsureInitialized(connection);
        
        return connection;
    }

    private void EnsureInitialized(IDbConnection connection)
    {
        var version = connection.ExecuteScalar<long>("PRAGMA user_version");
        
        // Initial Version (0) -> Version 1
        if (version < 1)
        {
            ApplyInitialSchema(connection);
            connection.Execute("PRAGMA user_version = 1");
        }
        
        // Future migrations would go here:
        // if (version < 2) { ApplyV2(connection); connection.Execute("PRAGMA user_version = 2"); }
    }

    private void ApplyInitialSchema(IDbConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            // Based on concept.md Core Tables
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS notes (
                    id          TEXT PRIMARY KEY,
                    title       TEXT NOT NULL,
                    content     TEXT,
                    content_secret BLOB,
                    type        TEXT NOT NULL DEFAULT 'note',
                    tags        TEXT,
                    created_by  TEXT NOT NULL,
                    created_at  TEXT NOT NULL,
                    updated_at  TEXT NOT NULL,
                    pinned      INTEGER DEFAULT 0,
                    archived    INTEGER DEFAULT 0
                );", transaction: transaction);

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS events (
                    id              TEXT PRIMARY KEY,
                    title           TEXT NOT NULL,
                    description     TEXT,
                    start_at        TEXT NOT NULL,
                    end_at          TEXT,
                    all_day         INTEGER DEFAULT 0,
                    rrule           TEXT,
                    location        TEXT,
                    reminder_minutes INTEGER,
                    external_id     TEXT,
                    external_source TEXT,
                    created_by      TEXT NOT NULL,
                    created_at      TEXT NOT NULL,
                    updated_at      TEXT NOT NULL
                );", transaction: transaction);

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS sync_sources (
                    id          TEXT PRIMARY KEY,
                    type        TEXT NOT NULL,
                    config      TEXT NOT NULL,
                    last_synced TEXT,
                    enabled     INTEGER DEFAULT 1
                );", transaction: transaction);

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS reminders (
                    id            TEXT PRIMARY KEY,
                    event_id      TEXT NOT NULL REFERENCES events(id),
                    scheduled_at  TEXT NOT NULL,
                    sent_at       TEXT,
                    channel_type  TEXT NOT NULL,
                    channel_id    TEXT NOT NULL
                );", transaction: transaction);

            connection.Execute(@"
                -- FTS5 virtual table for full-text search
                CREATE VIRTUAL TABLE IF NOT EXISTS notes_fts USING fts5(
                    title,
                    content,
                    tags
                );", transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
