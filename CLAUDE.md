# CLAUDE.md

Guidance for Claude Code in this repository.

## What this project is

Self-hosted personal memory + assistant. **`CONCEPT.md` is the target spec** — much of it (Discord bot, search, sync, scripting, teams, triggers) is future design. Today only `Fishbowl.Core`, `.Data`, `.Api`, and `.Host` have real implementations; other projects are empty shells. Align with CONCEPT.md — don't invent a different architecture. See `CONTRIBUTING.md` and `docs/superpowers/` for active work.

## Working style — adaptive programming

Find the closest existing solution and **extend it**, don't parallel it. Almost-fits → add overload/option/partial. Genuinely wrong → say so and migrate callers. **Never silently diverge.**

- **HTTP endpoints**: `MapXxxApi()` extensions on `IEndpointRouteBuilder` (see `NotesApi.cs`); read user via `fishbowl_user_id` claim, call a repository. Routes under `/api/v1/`.
- **Repositories**: interface in `Fishbowl.Core.Repositories`, impl in `Fishbowl.Data.Repositories`, `AddScoped`. Access via `DatabaseFactory.CreateConnection(userId)` or `CreateSystemConnection()`.
- **Multi-step writes**: `_dbFactory.WithUserTransactionAsync(userId, async (db, tx, ct) => …)` — see `NoteRepository` for `notes` + `notes_fts` syncing.
- **Schema changes**: add `ApplyVN` in `DatabaseFactory`, bump `PRAGMA user_version`. No EF Core, no migration runner.
- **UI resources/scripts/templates**: `IResourceProvider.GetAsync(path)` only (disk → DB → embedded). Never direct file I/O.
- **Plugins**: implement `IFishbowlPlugin`, register via `IFishbowlApi.AddBotClient/AddSyncProvider/AddScheduledJob` (`Fishbowl.Core.Plugins`).
- **Configuration**: in `system.db` via `ISystemRepository.Get/SetConfigAsync` — never `appsettings.json`, never env vars for runtime config.
- **Frontend backend calls**: `fb.api.notes.list()` etc. — 401s auto-redirect to `/login`. Never `fetch` directly from views/components.

## Commands

Target framework is **`net10.0`** across every project.

```bash
dotnet build Fishbowl.sln
dotnet run --project src/Fishbowl.Host           # https://localhost:7180
dotnet watch run --project src/Fishbowl.Host     # hot reload
dotnet test
dotnet test --filter "FullyQualifiedName~TestName"
```

`src/Directory.Build.targets` auto-runs `dotnet test --no-build` after any `*.Tests` compile (skip via `ContinuousIntegrationBuild=true`). Test stack is **xUnit v3**; cancellation tokens come from `TestContext.Current.CancellationToken`, not an ambient source.

## Architecture

**Dependency direction:** `Host → Api/Bot.Discord/Sync/Scheduler/Scripting → Data/Search → Core`. `Fishbowl.Host` is the **only publish target**; everything else is a library. No cycles, no upward refs.

**Two SQLite DBs, one `DatabaseFactory` (singleton):**
- `CreateSystemConnection()` → `fishbowl-data/system.db` — `users`, `user_mappings` (OAuth provider → internal id), `system_config`.
- `CreateConnection(userId)` → `fishbowl-data/users/{userId}.db` — `notes`, `events`, `todos`, `sync_sources`, `reminders`, `notes_fts`.

Lazy migrations keyed on `PRAGMA user_version`. Dapper raw SQL; IDs are ULIDs (`Ulid.NewUlid().ToString()`).

**User identity via custom claim.** Google OAuth + cookies. On first login, `Program.cs` (`OnTicketReceived`) creates a Fishbowl user (GUID), maps `(provider, providerId) → internalUserId` in `user_mappings`, adds a **`fishbowl_user_id`** claim. **Every API endpoint reads `user.FindFirst("fishbowl_user_id")?.Value`** — never `NameIdentifier`. Data isolation is by file boundary, not row filtering.

**OAuth creds in `system.db`, not `appsettings.json`.** `GoogleOptions` binds against `ConfigurationCache` populated at startup by `ConfigurationInitializer : IHostedService`. `POST /api/setup` writes both DB and cache so changes propagate without a restart. Unconfigured options use sentinel `"placeholder"`.

**API paths return 401, not 302.** `OnRedirectToLogin` is overridden so `/api` requests get `401` instead of redirect to Google — covered by `AuthBehaviorTests.GetApiNotes_Returns401_NotRedirect_Test`. OpenAPI doc at `/api/openapi.json`.

**`/setup` locked after config.** Once `Google:ClientId` is set, `GET /setup` and `POST /api/setup` return `404` (not 302 — harder to bypass). Validates: ClientId ends `.apps.googleusercontent.com`, ClientSecret ≥ 20 chars. No antiforgery (only responds when unconfigured).

**`IResourceProvider` serves the web UI and mods.** Three-tier: disk (`fishbowl-mods/{path}`) → DB (not yet wired) → embedded in `Fishbowl.Data.dll`. `MapFallback` handles non-API paths; unmatched `/api/*` short-circuits to 404 (so it doesn't serve `index.html`). The private `TryOpenEmbeddedStream` helper tries three path forms because MSBuild's `RecursiveDir` produces Windows-style paths on Windows — keep it.

**Plugins in isolated ALCs.** `PluginLoadContext` uses `AssemblyDependencyResolver` in a collectible ALC so `fishbowl-mods/plugins/` DLLs ship their own deps. `PluginLoader.LoadPlugins` runs at startup (`Plugins:Path`, default `fishbowl-mods/plugins`); failures logged and skipped.

**UI is vanilla JS SPA — no framework, no build step.** `index.html` is the shell; views mount in `#app-root` via `fb.router.register("#/path", "tag-name", { label, icon })`. **Views use light DOM** (so `app.css` applies); **components use Shadow DOM** (style isolation). Theme via CSS custom props on `:root` (`--accent`, `--accent-warm`, `--danger`, `--glass`, `--border`, `--text-muted`). System components prefixed `fb-`; mods use `usr_` in `fishbowl-mods/components/`. `fb.icons.register(name, path)` extends icons. `/login` and `/setup` stay server-rendered. Component spec: `docs/superpowers/specs/2026-04-19-ui-foundation-design.md`.

**UI smoke test (`Fishbowl.Ui.Tests`)** launches `Fishbowl.Host` as a real subprocess on a free port for Playwright (not `TestServer`). A gated bypass (`FISHBOWL_PLAYWRIGHT_TEST` env + `Testing` environment) injects a test user. **Never set `FISHBOWL_PLAYWRIGHT_TEST` outside that fixture.**

## Testing

- `WebApplicationFactory<Program>` requires `public partial class Program { }` at end of `Program.cs` — leave it.
- `TestAuthHandler` reads **`X-Test-User-Id`** and synthesizes both `NameIdentifier` and `fishbowl_user_id`. Use this for any auth'd endpoint test — never mock Google OAuth.
- Fresh temp data dir per fixture, override `DatabaseFactory` singleton, **call `SqliteConnection.ClearAllPools()` before deleting** (Windows holds the file open otherwise).
- `Testing` env suppresses `StartupBranding` — don't add console output that assumes a TTY.

## Conventions

- **Dapper snake_case via `DapperConventions.Install()`** in `DatabaseFactory` static ctor — `created_at` → `CreatedAt` works automatically. `JsonTagsHandler` handles `List<string>` ↔ JSON for `notes.tags`. Use `QueryAsync<T>`, never `QueryAsync<dynamic>` with manual mappers.
- **FTS5 must stay synced.** `notes_fts.rowid` maps to `notes.rowid` via `(SELECT rowid FROM notes WHERE id = @Id)`. On delete, remove from `notes_fts` BEFORE `notes`. Tags are space-joined string (not JSON).
- **Logging via `ILogger<T>?` defaulting to `NullLogger<T>.Instance`** so tests can construct objects directly. **Never log PII** (email, name, content, secrets, tokens).
- SQLite `DateTime` stored ISO-8601 (`.ToString("o")`); booleans as `INTEGER 0/1`; IDs are ULIDs.
- Secrets use `::secret` markdown block + separate `content_secret BLOB` (client-encrypted). **Never include secret content in FTS, embeddings, or chat responses** — non-negotiable.
- Modding rule: "disk file wins, else default" — uniform across components/styles/scripts/templates/plugins. No registration manifests or whitelists.

## CI

`ci.yml` runs on push to master and PRs (Linux + Windows): `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`. `release.yml` on `v*` tag publishes single-file binaries for `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`.
