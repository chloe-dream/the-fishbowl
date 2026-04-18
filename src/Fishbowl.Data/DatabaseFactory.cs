using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fishbowl.Data;

public class DatabaseFactory
{
    private readonly string _dataRoot;
    private readonly string _usersPath;
    private readonly string _systemDbPath;
    private readonly ILogger<DatabaseFactory> _logger;

    static DatabaseFactory()
    {
        Fishbowl.Data.Dapper.DapperConventions.Install();
    }

    public DatabaseFactory(string dataRoot = "fishbowl-data", ILogger<DatabaseFactory>? logger = null)
    {
        _logger = logger ?? NullLogger<DatabaseFactory>.Instance;

        // Ensure absolute or relative path is handled correctly
        _dataRoot = Path.GetFullPath(dataRoot);
        _usersPath = Path.Combine(_dataRoot, "users");
        _systemDbPath = Path.Combine(_dataRoot, "system.db");

        if (!Directory.Exists(_usersPath))
        {
            Directory.CreateDirectory(_usersPath);
        }
    }

    public IDbConnection CreateConnection(string userId)
    {
        var dbPath = Path.Combine(_usersPath, $"{userId}.db");
        return OpenAndInitialize(dbPath, EnsureUserInitialized);
    }

    public IDbConnection CreateSystemConnection()
    {
        return OpenAndInitialize(_systemDbPath, EnsureSystemInitialized);
    }

    public async Task WithUserTransactionAsync(
        string userId,
        Func<IDbConnection, IDbTransaction, CancellationToken, Task> work,
        CancellationToken ct = default)
    {
        using var db = CreateConnection(userId);
        using var tx = db.BeginTransaction();
        try
        {
            await work(db, tx, ct);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<T> WithUserTransactionAsync<T>(
        string userId,
        Func<IDbConnection, IDbTransaction, CancellationToken, Task<T>> work,
        CancellationToken ct = default)
    {
        using var db = CreateConnection(userId);
        using var tx = db.BeginTransaction();
        try
        {
            var result = await work(db, tx, ct);
            tx.Commit();
            return result;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private IDbConnection OpenAndInitialize(string dbPath, Action<IDbConnection> initializer)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        connection.Open();

        initializer(connection);

        return connection;
    }

    private void EnsureUserInitialized(IDbConnection connection)
    {
        var version = connection.ExecuteScalar<long>("PRAGMA user_version");

        if (version < 1)
        {
            ApplyUserInitialSchema(connection);
            connection.Execute("PRAGMA user_version = 1");
            _logger.LogInformation("Applied user schema v1 to {DbPath}", ((SqliteConnection)connection).DataSource);
        }
    }

    private void EnsureSystemInitialized(IDbConnection connection)
    {
        var version = connection.ExecuteScalar<long>("PRAGMA user_version");

        if (version < 1)
        {
            ApplySystemInitialSchema(connection);
            connection.Execute("PRAGMA user_version = 1");
            _logger.LogInformation("Applied system schema v1");
        }
    }

    private void ApplyUserInitialSchema(IDbConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            // Notes
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

            // Events
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

            // Sync Sources
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS sync_sources (
                    id          TEXT PRIMARY KEY,
                    type        TEXT NOT NULL,
                    config      TEXT NOT NULL,
                    last_synced TEXT,
                    enabled     INTEGER DEFAULT 1
                );", transaction: transaction);

            // Reminders
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS reminders (
                    id            TEXT PRIMARY KEY,
                    event_id      TEXT NOT NULL REFERENCES events(id),
                    scheduled_at  TEXT NOT NULL,
                    sent_at       TEXT,
                    channel_type  TEXT NOT NULL,
                    channel_id    TEXT NOT NULL
                );", transaction: transaction);

            // Todos
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS todos (
                    id            TEXT PRIMARY KEY,
                    title         TEXT NOT NULL,
                    description   TEXT,
                    due_at        TEXT,
                    reminder_at   TEXT,
                    source        TEXT,
                    created_by    TEXT NOT NULL,
                    created_at    TEXT NOT NULL,
                    updated_at    TEXT NOT NULL,
                    completed_at  TEXT
                );", transaction: transaction);

            // FTS
            connection.Execute(@"
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

    private void ApplySystemInitialSchema(IDbConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS users (
                    id          TEXT PRIMARY KEY,
                    name        TEXT,
                    email       TEXT,
                    avatar_url  TEXT,
                    created_at  TEXT NOT NULL
                );", transaction: transaction);

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS user_mappings (
                    provider    TEXT NOT NULL,
                    provider_id TEXT NOT NULL,
                    user_id     TEXT NOT NULL REFERENCES users(id),
                    PRIMARY KEY (provider, provider_id)
                );", transaction: transaction);

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS system_config (
                    key         TEXT PRIMARY KEY,
                    value       TEXT,
                    updated_at  TEXT NOT NULL
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
