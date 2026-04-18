# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

The Fishbowl is a self-hosted personal memory + assistant application. **`CONCEPT.md` is the full product/architecture spec** — much of it (Discord bot, search, sync, scripting, teams, apps, triggers) describes the target design, not what is built. Today, only `Fishbowl.Core`, `Fishbowl.Data`, `Fishbowl.Api`, and `Fishbowl.Host` have real implementations; the other projects (`Bot.Discord`, `Sync`, `Scheduler`, `Scripting`, `Search`) contain placeholder `Class1.cs` files. When making changes, align with CONCEPT.md — do not invent a different architecture.

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

`GoogleOptions` is configured lazily via `AddOptions<GoogleOptions>().Configure<ISystemRepository, IWebHostEnvironment>(...)` that reads `Google:ClientId` / `Google:ClientSecret` from `system_config`. The setup flow (`GET /setup` + `POST /api/setup`) writes them. When running `Development`/`Localhost` without creds, placeholder dev values are auto-seeded into `system.db` — do not hardcode real credentials into `appsettings*.json`.

### API paths must return 401, not redirect

`OnRedirectToLogin` is overridden so requests under `/api/v1` get `401 Unauthorized` instead of a 302 to Google. This is intentional and covered by `AuthBehaviorTests.GetApiNotes_Returns401_NotRedirect_Test` — do not remove it (it avoids CORS/preflight chaos for API clients).

### `IResourceProvider` serves the web UI (and will serve mods)

`Fishbowl.Data.ResourceProvider` implements a three-tier lookup: **disk (`fishbowl-mods/{path}`) → database (not yet wired) → embedded resources** in `Fishbowl.Data.dll`. The Host's fallback route (`MapFallback`) delegates all non-API paths to it. Embedded resources are defined in `Fishbowl.Data.csproj` with `<EmbeddedResource Include="Resources\**\*" LogicalName="%(RecursiveDir)%(Filename)%(Extension)" />`. Because MSBuild's `RecursiveDir` produces Windows-style paths on Windows, the provider tries three fallbacks per lookup (`as-is` → backslashes → legacy dot-notation) — keep this fallback chain if touching that code. Resources are memory-cached; no hot-reload today (FileSystemWatcher mentioned in CONCEPT.md is not yet implemented).

### Plugins load in isolated `AssemblyLoadContext`s

`Fishbowl.Host.Plugins.PluginLoadContext` uses `AssemblyDependencyResolver` inside a collectible ALC so plugin DLLs in `fishbowl-mods/plugins/` can ship their own dependency versions without colliding with the host. Plugins implement `Fishbowl.Core.IFishbowlPlugin`. The `IFishbowlApi` surface is currently stubbed (`object` parameters with `TODO`s) — when implementing real plugin features, define the concrete `IBotClient` / `ISyncProvider` / `IScheduledJob` interfaces in `Fishbowl.Core` first.

## Testing conventions

- Integration tests use `WebApplicationFactory<Program>` — this requires `Program.cs` to end with `public partial class Program { }` so the test project can reference it. Leave that line in place.
- `Fishbowl.Host.Tests/ApiIntegrationTests.cs` swaps in a `TestAuthHandler` that reads the **`X-Test-User-Id`** header and synthesizes both `NameIdentifier` and `fishbowl_user_id` claims. Use this pattern for any new authenticated endpoint test — don't try to mock Google OAuth.
- Integration tests create a fresh temp data directory per fixture and override the `DatabaseFactory` singleton. On disposal they call `SqliteConnection.ClearAllPools()` before deleting the directory — do this too, otherwise Windows holds the file open and `Directory.Delete` fails.
- When testing in the `Testing` environment, `Program.cs` suppresses the `StartupBranding` banner — do not add console output that assumes a TTY.

## Conventions worth knowing

- **`Class1.cs` files mark not-yet-implemented projects.** They are placeholders; replace them when building out `Search`, `Sync`, `Scheduler`, `Scripting`, or `Bot.Discord`.
- SQLite `DateTime` is stored as ISO-8601 strings (`DateTime.UtcNow.ToString("o")`). Booleans as `INTEGER DEFAULT 0/1`. Collections (e.g. `Note.Tags`) are JSON-serialized via `System.Text.Json`. Dapper `QuerySingleOrDefaultAsync<dynamic>` is used with manual row → model mapping in repositories.
- Secrets in notes use a `::secret` markdown block and a separate `content_secret BLOB` column encrypted client-side. **Never include secret content in FTS indexing, embeddings, or chat responses** — this is a non-negotiable product rule from CONCEPT.md.
- The "If the file exists on disk, use it; otherwise use the default" override pattern (see CONCEPT.md "Modding") applies uniformly to components, styles, scripts, templates, and plugins. Do not introduce registration manifests or whitelists for mods.
