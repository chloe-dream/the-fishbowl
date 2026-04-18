# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

The Fishbowl is a self-hosted personal memory + assistant application. **`CONCEPT.md` is the full product/architecture spec** — much of it (Discord bot, search, sync, scripting, teams, apps, triggers) describes the target design, not what is built. Today, only `Fishbowl.Core`, `Fishbowl.Data`, `Fishbowl.Api`, and `Fishbowl.Host` have real implementations; the other projects (`Bot.Discord`, `Sync`, `Scheduler`, `Scripting`, `Search`) are empty shells waiting for their feature work. When making changes, align with CONCEPT.md — do not invent a different architecture.

See `CONTRIBUTING.md` for the one-page feature-work recipe. See `docs/superpowers/` for active design specs and implementation plans.

## Working style — adaptive programming

Before writing new code, find the closest existing solution in this codebase and **extend it**, don't parallel it. Concretely:

- **HTTP endpoints** inherit the `MapXxxApi()` extension pattern on `IEndpointRouteBuilder` (see `src/Fishbowl.Api/Endpoints/NotesApi.cs`); they read the user via the `fishbowl_user_id` claim and call a repository. Routes are versioned under `/api/v1/`.
- **Repositories** have an interface in `Fishbowl.Core.Repositories` and an implementation in `Fishbowl.Data.Repositories`, registered as `AddScoped`. Data access goes through `DatabaseFactory.CreateConnection(userId)` (per-user DB) or `CreateSystemConnection()` (global `system.db`).
- **Multi-step writes** use Dapper with an explicit transaction — `WithUserTransactionAsync` on `DatabaseFactory` once Phase 3 lands; before that, inline `BeginTransaction`/`Commit`/`Rollback`.
- **Schema changes** add an `ApplyVN` method in `DatabaseFactory` and bump `PRAGMA user_version` — no EF Core, no migration runner.
- **UI resources, scripts, templates** go through `IResourceProvider.GetAsync(path)` (disk → database → embedded fallback), never direct file I/O.
- **Plugins** implement `IFishbowlPlugin` and register capabilities via `IFishbowlApi.AddBotClient / AddSyncProvider / AddScheduledJob` (contracts live in `Fishbowl.Core.Plugins`).
- **Configuration** goes in `system.db` via `ISystemRepository.Get/SetConfigAsync(key)` — never `appsettings.json` and never environment variables for anything the user sets at runtime.
- **Tests** use `WebApplicationFactory<Program>` for integration, the `X-Test-User-Id` header via `TestAuthHandler` for auth scoping, and xUnit v3's `TestContext.Current.CancellationToken`.

If an existing pattern almost-fits, add an overload / option / partial — don't reinvent a local copy. If the existing pattern is genuinely wrong, say so explicitly and propose migrating callers. **Never silently diverge.**

## Commands

All commands run from the repo root. Target framework is **`net10.0`** across every project — the SDK must support .NET 10.

```bash
dotnet build Fishbowl.sln                              # build everything
dotnet run --project src/Fishbowl.Host                 # launches at https://localhost:7180 (Dev profile)
dotnet watch run --project src/Fishbowl.Host           # hot reload during dev
dotnet test                                            # run all tests
dotnet test src/Fishbowl.Host.Tests                    # run one test project
dotnet test --filter "FullyQualifiedName~TestName"     # run a single test by name
dotnet publish src/Fishbowl.Host -c Release -p:PublishSingleFile=true -p:SelfContained=true -r <rid>
```

**Tests auto-run after local builds.** `src/Directory.Build.targets` defines a `RunTestsOnBuild` target that executes `dotnet test --no-build` after any `*.Tests` project finishes compiling, unless `ContinuousIntegrationBuild=true`. If you just want to compile, set that property or build a non-test project.

Test stack is **xUnit v3** (pre-release `0.7.0-pre.15`). Cancellation tokens come from `TestContext.Current.CancellationToken`, not an ambient source.

## Architecture

### Dependency direction (enforced by `.csproj` references)

```
Host  →  Api, Bot.Discord, Sync, Scheduler, Scripting
         │
         ↓
         Data, Search
         ↓
         Core   (Core references no other Fishbowl project)
```

`Fishbowl.Host` is the **only publish target** — it composes DI, wires auth, and maps endpoints. Everything else is a library. Do not add project references that create cycles or make lower layers depend on upper ones.

### Two SQLite databases, one `DatabaseFactory`

`Fishbowl.Data.DatabaseFactory` (registered as a singleton) is the sole entry point to SQLite:

- `CreateSystemConnection()` → `fishbowl-data/system.db` — holds `users`, `user_mappings` (OAuth provider → internal user id), and `system_config` (key/value for Google OAuth creds etc.).
- `CreateConnection(userId)` → `fishbowl-data/users/{userId}.db` — one file per user containing `notes`, `events`, `todos`, `sync_sources`, `reminders`, and the `notes_fts` FTS5 virtual table.

Each call opens a fresh `SqliteConnection` and runs lazy migrations keyed on `PRAGMA user_version`. **There is no EF Core and no migration runner.** To add a new schema version, extend `EnsureUserInitialized` / `EnsureSystemInitialized` with `if (version < N) ApplyVN(conn);` and bump the pragma. Repositories use Dapper with raw SQL; IDs are ULIDs (`Ulid.NewUlid().ToString()`).

### User identity flows through a custom claim

Auth is Google OAuth + cookies. On first Google login, `Program.cs` (`OnTicketReceived`) creates an internal Fishbowl user (GUID), maps `(provider, providerId) → internalUserId` in `user_mappings`, and adds a **`fishbowl_user_id`** claim. **Every API endpoint reads `user.FindFirst("fishbowl_user_id")?.Value`** — never `NameIdentifier` — and passes it to repositories, which route to the correct per-user SQLite file. Data isolation is enforced by the file boundary, not by row-level filtering.

### OAuth credentials live in `system.db`, not `appsettings.json`

`GoogleOptions` binds against `ConfigurationCache` (not `ISystemRepository` directly) via `AddOptions<GoogleOptions>().Configure<ConfigurationCache>(...)`. The cache is populated at host startup by `ConfigurationInitializer : IHostedService` reading `system_config`. `POST /api/setup` writes to both the DB and the cache so changes propagate without a restart. When unconfigured, options carry the sentinel `"placeholder"` (Google's built-in validation rejects empty strings). Never hardcode credentials in `appsettings*.json` or source.

### API paths must return 401, not redirect

`OnRedirectToLogin` is overridden so requests under `/api` get `401 Unauthorized` instead of a 302 to Google. This is intentional and covered by `AuthBehaviorTests.GetApiNotes_Returns401_NotRedirect_Test` — do not remove it (avoids CORS/preflight chaos for API clients). All public API routes live under `/api/v1/`; the OpenAPI document is at `/api/openapi.json`.

### `/setup` is locked after configuration

Once `Google:ClientId` is present in the cache, both `GET /setup` and `POST /api/setup` return `404` (not 302 — harder to bypass). `POST /api/setup` also validates: ClientId must end with `.apps.googleusercontent.com`, ClientSecret must be at least 20 chars. There is **no antiforgery** on `/api/setup` — CSRF requires a logged-in user, and `/setup` only responds when unconfigured.

### `IResourceProvider` serves the web UI (and will serve mods)

`Fishbowl.Data.ResourceProvider` implements a three-tier lookup: **disk (`fishbowl-mods/{path}`) → database (not yet wired) → embedded resources** in `Fishbowl.Data.dll`. The Host's fallback route (`MapFallback`) delegates non-API paths to it; `/api/*` requests that don't match a mapped endpoint are short-circuited to 404 so they don't accidentally serve `index.html`. Embedded resources are defined in `Fishbowl.Data.csproj` with `<EmbeddedResource Include="Resources\**\*" LogicalName="%(RecursiveDir)%(Filename)%(Extension)" />`. Because MSBuild's `RecursiveDir` produces Windows-style paths on Windows, the private `TryOpenEmbeddedStream` helper (used by both `GetAsync` and `ExistsAsync`) tries three path forms — keep that helper if you touch the code. Resources are memory-cached; no hot-reload today.

### Plugins load in isolated `AssemblyLoadContext`s

`Fishbowl.Host.Plugins.PluginLoadContext` uses `AssemblyDependencyResolver` inside a collectible ALC so plugin DLLs in `fishbowl-mods/plugins/` can ship their own dependency versions without colliding with the host. Plugins implement `IFishbowlPlugin` and register capabilities through `IFishbowlApi.AddBotClient / AddSyncProvider / AddScheduledJob` — contracts live in `Fishbowl.Core.Plugins`. `Fishbowl.Host.Plugins.PluginLoader.LoadPlugins(...)` runs at startup (configurable path via `Plugins:Path`, default `fishbowl-mods/plugins`); plugin load failures are logged and skipped — one bad plugin doesn't kill the host.

## Testing conventions

- Integration tests use `WebApplicationFactory<Program>` — this requires `Program.cs` to end with `public partial class Program { }` so the test project can reference it. Leave that line in place.
- `Fishbowl.Host.Tests/ApiIntegrationTests.cs` swaps in a `TestAuthHandler` that reads the **`X-Test-User-Id`** header and synthesizes both `NameIdentifier` and `fishbowl_user_id` claims. Use this pattern for any new authenticated endpoint test — don't try to mock Google OAuth.
- Integration tests create a fresh temp data directory per fixture and override the `DatabaseFactory` singleton. On disposal they call `SqliteConnection.ClearAllPools()` before deleting the directory — do this too, otherwise Windows holds the file open and `Directory.Delete` fails.
- When testing in the `Testing` environment, `Program.cs` suppresses the `StartupBranding` banner — do not add console output that assumes a TTY.

## Conventions worth knowing

- **Dapper uses typed queries with snake_case convention.** `DapperConventions.Install()` (called from `DatabaseFactory`'s static ctor) enables `MatchNamesWithUnderscores`, so `created_at` → `CreatedAt` works automatically. `JsonTagsHandler` serializes `List<string>` ↔ JSON text for the `notes.tags` column. Never write `QueryAsync<dynamic>` with a manual row-mapper — use `QueryAsync<T>` directly.
- **Multi-step writes use the transaction helper.** `_dbFactory.WithUserTransactionAsync(userId, async (db, tx, ct) => { ... })` wraps begin/commit/rollback. `NoteRepository.CreateAsync/UpdateAsync/DeleteAsync` use it for `notes` + `notes_fts` syncing — copy that pattern for any repo that touches more than one table.
- **FTS5 writes must stay in sync with the primary table.** `notes_fts.rowid` maps to `notes.rowid` via `(SELECT rowid FROM notes WHERE id = @Id)`. On delete, remove from `notes_fts` BEFORE `notes` (else the subquery returns nothing). Tags in `notes_fts` are a space-joined flat string (not JSON).
- **Logging via `ILogger<T>`.** Every service that does non-trivial work takes an optional `ILogger<T>? logger = null` parameter defaulting to `NullLogger<T>.Instance`, so tests that construct objects directly still work. DI auto-injects the real logger in production. **Never log PII (email, name, content, secret values, OAuth tokens).**
- SQLite `DateTime` is stored as ISO-8601 strings (`DateTime.UtcNow.ToString("o")`). Booleans as `INTEGER DEFAULT 0/1` (Dapper handles int↔bool natively via `Convert.ToBoolean`). IDs are ULIDs (`Ulid.NewUlid().ToString()`).
- Secrets in notes use a `::secret` markdown block and a separate `content_secret BLOB` column encrypted client-side. **Never include secret content in FTS indexing, embeddings, or chat responses** — non-negotiable product rule from CONCEPT.md.
- "If the file exists on disk, use it; otherwise use the default" — the override pattern from CONCEPT.md "Modding" applies uniformly to components, styles, scripts, templates, and plugins. Do not introduce registration manifests or whitelists for mods.

## CI

GitHub Actions:
- `ci.yml` runs on every push to master and every PR. Matrix: `ubuntu-latest` + `windows-latest`. Gates: `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`. Keep these green.
- `release.yml` runs on `v*` tag push or manual dispatch. Publishes four single-file binaries (`win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`) and attaches them to a GitHub Release on tag push only.
