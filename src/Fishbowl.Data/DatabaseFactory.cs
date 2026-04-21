using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;
using Fishbowl.Core;
using Fishbowl.Core.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fishbowl.Data;

public class DatabaseFactory
{
    private readonly string _dataRoot;
    private readonly string _usersPath;
    private readonly string _teamsPath;
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
        _teamsPath = Path.Combine(_dataRoot, "teams");
        _systemDbPath = Path.Combine(_dataRoot, "system.db");

        if (!Directory.Exists(_usersPath)) Directory.CreateDirectory(_usersPath);
        if (!Directory.Exists(_teamsPath)) Directory.CreateDirectory(_teamsPath);
    }

    // Preferred primary entrypoint: one context resolves to one file. Personal
    // and team spaces share an identical schema and identical migrations —
    // CONCEPT.md § Teams is explicit that team DBs are structurally identical
    // to user DBs, so EnsureUserInitialized applies to both.
    //
    // Context connections also load sqlite-vec (`vec0`) before migrations run;
    // v4 creates a `vec_notes` virtual table so the extension must be live
    // during EnsureUserInitialized, not just at query time.
    public IDbConnection CreateContextConnection(ContextRef ctx)
    {
        var dbPath = ctx.Type switch
        {
            ContextType.User => Path.Combine(_usersPath, $"{ctx.Id}.db"),
            ContextType.Team => Path.Combine(_teamsPath, $"{ctx.Id}.db"),
            _ => throw new ArgumentException($"Unknown context type: {ctx.Type}", nameof(ctx)),
        };
        return OpenAndInitialize(dbPath, EnsureUserInitialized, loadVec: true);
    }

    // Legacy entrypoint — kept so cookie-auth call sites (which only know a
    // userId) stay minimal-diff. Delegates to the context-aware method.
    public IDbConnection CreateConnection(string userId)
        => CreateContextConnection(ContextRef.User(userId));

    public IDbConnection CreateSystemConnection()
    {
        return OpenAndInitialize(_systemDbPath, EnsureSystemInitialized, loadVec: false);
    }

    public async Task WithContextTransactionAsync(
        ContextRef ctx,
        Func<IDbConnection, IDbTransaction, CancellationToken, Task> work,
        CancellationToken ct = default)
    {
        using var db = CreateContextConnection(ctx);
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

    public async Task<T> WithContextTransactionAsync<T>(
        ContextRef ctx,
        Func<IDbConnection, IDbTransaction, CancellationToken, Task<T>> work,
        CancellationToken ct = default)
    {
        using var db = CreateContextConnection(ctx);
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

    public Task WithUserTransactionAsync(
        string userId,
        Func<IDbConnection, IDbTransaction, CancellationToken, Task> work,
        CancellationToken ct = default)
        => WithContextTransactionAsync(ContextRef.User(userId), work, ct);

    public Task<T> WithUserTransactionAsync<T>(
        string userId,
        Func<IDbConnection, IDbTransaction, CancellationToken, Task<T>> work,
        CancellationToken ct = default)
        => WithContextTransactionAsync(ContextRef.User(userId), work, ct);

    private IDbConnection OpenAndInitialize(string dbPath, Action<IDbConnection> initializer, bool loadVec)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        connection.Open();

        if (loadVec) SqliteVecLoader.LoadInto(connection);

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
            version = 1;
        }

        if (version < 2)
        {
            ApplyUserV2(connection);
            connection.Execute("PRAGMA user_version = 2");
            _logger.LogInformation("Applied user schema v2 to {DbPath}", ((SqliteConnection)connection).DataSource);
            version = 2;
        }

        if (version < 3)
        {
            ApplyUserV3(connection);
            connection.Execute("PRAGMA user_version = 3");
            _logger.LogInformation("Applied user schema v3 to {DbPath}", ((SqliteConnection)connection).DataSource);
            version = 3;
        }
        else
        {
            // V3 was reshaped during pre-commit iteration (three-flag model,
            // new seed set). If this DB was opened against an older v3 shape,
            // reconcile without bumping user_version — v3 stays the current
            // version until it ships. Harmless on a correct v3 DB.
            ReconcileUserV3(connection);
        }

        if (version < 4)
        {
            ApplyUserV4(connection);
            connection.Execute("PRAGMA user_version = 4");
            _logger.LogInformation("Applied user schema v4 to {DbPath}", ((SqliteConnection)connection).DataSource);
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
            version = 1;
        }

        if (version < 2)
        {
            ApplySystemV2(connection);
            connection.Execute("PRAGMA user_version = 2");
            _logger.LogInformation("Applied system schema v2");
            version = 2;
        }

        if (version < 3)
        {
            ApplySystemV3(connection);
            connection.Execute("PRAGMA user_version = 3");
            _logger.LogInformation("Applied system schema v3");
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

    private void ApplyUserV2(IDbConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            // Tag metadata: notes still hold tag *names* in their JSON `tags`
            // column; this table adds per-tag presentation (color slot) and
            // makes "list all tags" cheap.
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS tags (
                    name        TEXT PRIMARY KEY,
                    color       TEXT NOT NULL,
                    created_at  TEXT NOT NULL
                );", transaction: transaction);

            // Backfill from existing notes' JSON tags arrays so v1 users keep
            // the tags they already have and get deterministic default colors.
            var existing = connection.Query<string>(
                @"SELECT DISTINCT je.value
                  FROM notes, json_each(notes.tags) je
                  WHERE je.value IS NOT NULL AND je.value != ''",
                transaction: transaction).ToList();

            var now = DateTime.UtcNow.ToString("o");
            foreach (var name in existing)
            {
                connection.Execute(
                    "INSERT OR IGNORE INTO tags(name, color, created_at) VALUES (@name, @color, @createdAt)",
                    new { name, color = TagPalette.DefaultFor(name), createdAt = now },
                    transaction: transaction);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private void ApplyUserV3(IDbConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            // Three independent per-tag flags (see Fishbowl.Core.Util.SystemTags):
            //  is_system       — name is load-bearing; rename/delete rejected.
            //  user_assignable — UI dropdown offers this tag to pick. When 0,
            //                    only system/MCP writes can attach it to a note.
            //  user_removable  — fb-tag-chip renders a × for this tag. When 0,
            //                    the tag is locked onto whatever note holds it.
            AddColumnIfMissing(connection, transaction, "tags", "is_system",       "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing(connection, transaction, "tags", "user_assignable", "INTEGER NOT NULL DEFAULT 1");
            AddColumnIfMissing(connection, transaction, "tags", "user_removable",  "INTEGER NOT NULL DEFAULT 1");

            ReconcileSystemTagRows(connection, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // sqlite-vec virtual table — one row per note, 384-dim float embedding
    // (MiniLM-L6-v2 output size). `id` mirrors `notes.id` so joins are direct.
    // No backfill here: existing notes embed lazily on next write, or in bulk
    // via the Settings "re-index all" action.
    private void ApplyUserV4(IDbConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            connection.Execute(@"
                CREATE VIRTUAL TABLE IF NOT EXISTS vec_notes USING vec0(
                    id TEXT PRIMARY KEY,
                    embedding FLOAT[384]
                );", transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // Idempotent: runs on every open of an already-v3 DB. Safe because it
    // only touches system-tagged rows and reconciles them against the current
    // SystemTags.Seeds. Drops the `is_system` flag from any stale reserved
    // names that were seeded by an earlier v3 iteration — they become regular
    // user tags that can be renamed/deleted normally.
    private void ReconcileUserV3(IDbConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            // If a pre-reshape v3 only added `is_system`, fill in the other two.
            AddColumnIfMissing(connection, transaction, "tags", "user_assignable", "INTEGER NOT NULL DEFAULT 1");
            AddColumnIfMissing(connection, transaction, "tags", "user_removable",  "INTEGER NOT NULL DEFAULT 1");

            ReconcileSystemTagRows(connection, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void AddColumnIfMissing(
        IDbConnection connection, IDbTransaction tx, string table, string column, string ddlType)
    {
        var exists = connection.ExecuteScalar<long>(
            $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = @col",
            new { col = column }, transaction: tx);
        if (exists == 0)
        {
            connection.Execute($"ALTER TABLE {table} ADD COLUMN {column} {ddlType}", transaction: tx);
        }
    }

    private static void ReconcileSystemTagRows(IDbConnection connection, IDbTransaction tx)
    {
        var now = DateTime.UtcNow.ToString("o");
        var reserved = Fishbowl.Core.Util.SystemTags.ReservedNames;

        // Demote any existing is_system rows whose names are no longer in the
        // reserved set (stale iteration artefacts become regular user tags).
        connection.Execute(@"
            UPDATE tags SET is_system = 0, user_assignable = 1, user_removable = 1
            WHERE is_system = 1 AND name NOT IN @reserved",
            new { reserved }, transaction: tx);

        // Upsert each current seed into the correct shape.
        foreach (var spec in Fishbowl.Core.Util.SystemTags.Seeds)
        {
            connection.Execute(
                @"INSERT OR IGNORE INTO tags(name, color, created_at, is_system, user_assignable, user_removable)
                  VALUES (@name, @color, @createdAt, 1, @assign, @remove)",
                new
                {
                    name = spec.Name,
                    color = spec.Color,
                    createdAt = now,
                    assign = spec.UserAssignable ? 1 : 0,
                    remove = spec.UserRemovable ? 1 : 0
                }, transaction: tx);
            connection.Execute(
                @"UPDATE tags SET is_system = 1, user_assignable = @assign, user_removable = @remove
                  WHERE name = @name",
                new
                {
                    name = spec.Name,
                    assign = spec.UserAssignable ? 1 : 0,
                    remove = spec.UserRemovable ? 1 : 0
                }, transaction: tx);
        }
    }

    private void ApplySystemV2(IDbConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            // Teams: shared workspaces with their own fixed-schema .db file
            // under `fishbowl-data/teams/{id}.db`. Slug is URL-safe, unique
            // per-deployment (a single Fishbowl instance can't have two teams
            // named 'fishbowl-dev').
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS teams (
                    id          TEXT PRIMARY KEY,
                    slug        TEXT NOT NULL UNIQUE,
                    name        TEXT NOT NULL,
                    created_by  TEXT NOT NULL REFERENCES users(id),
                    created_at  TEXT NOT NULL
                );", transaction: transaction);

            // Membership + role. Composite PK = (team, user). CHECK constraint
            // mirrors TeamRole enum in Fishbowl.Core.Models — keep the two in
            // sync.
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS team_members (
                    team_id    TEXT NOT NULL REFERENCES teams(id),
                    user_id    TEXT NOT NULL REFERENCES users(id),
                    role       TEXT NOT NULL CHECK(role IN ('readonly','member','admin','owner')),
                    joined_at  TEXT NOT NULL,
                    PRIMARY KEY (team_id, user_id)
                );", transaction: transaction);

            // Hot-path queries: "what teams am I in?" and "is this user in
            // this team?". The composite PK covers team-side lookups; this
            // index covers user-side lookups.
            connection.Execute(
                "CREATE INDEX IF NOT EXISTS idx_team_members_user ON team_members(user_id)",
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private void ApplySystemV3(IDbConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            // Bearer tokens minted by users to authenticate programmatic
            // clients (Claude Code, curl, etc). `key_hash` stores SHA-256 of
            // the raw token; `key_prefix` is the first 12 chars of the raw
            // token (covers "fb_live_" + 4 random chars) and backs the index
            // that narrows candidates before the constant-time compare.
            // `scopes` is a JSON array — handler in Fishbowl.Data.Dapper
            // keeps List<string> ↔ JSON round-tripping transparent.
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS api_keys (
                    id            TEXT PRIMARY KEY,
                    user_id       TEXT NOT NULL REFERENCES users(id),
                    context_type  TEXT NOT NULL CHECK(context_type IN ('user','team')),
                    context_id    TEXT NOT NULL,
                    name          TEXT NOT NULL,
                    key_hash      TEXT NOT NULL,
                    key_prefix    TEXT NOT NULL,
                    scopes        TEXT NOT NULL,
                    created_at    TEXT NOT NULL,
                    last_used_at  TEXT,
                    revoked_at    TEXT
                );", transaction: transaction);

            // Partial index — only live keys. Revoked keys stay in the table
            // for audit but don't pollute the lookup hot path.
            connection.Execute(
                "CREATE INDEX IF NOT EXISTS idx_api_keys_prefix ON api_keys(key_prefix) WHERE revoked_at IS NULL",
                transaction: transaction);

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
