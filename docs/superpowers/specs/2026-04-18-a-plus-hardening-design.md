# A+ Hardening — Design

**Date:** 2026-04-18
**Status:** Approved design; implementation plan to follow
**Scope:** Foundation hardening before new feature work. No new product capabilities.

## Why

The codebase is a strong B+: architecture is disciplined, tests are meaningful, CONCEPT.md is excellent. But CONCEPT.md describes a product that is roughly 5% implemented, and the 5% has correctness bugs, missing external contracts, and internal patterns that have not yet found their canonical form. Before any of the large feature chapters (search, Discord bot, calendar sync, teams, apps, triggers) start building on this foundation, the foundation itself must be A+. This spec defines what "A+" means concretely and how to get there.

## Acceptance criteria for A+

The codebase is A+ when all of the following hold:

1. **No known correctness bugs.** Every issue identified during the B+ review is fixed and has a regression test.
2. **Every external surface has a stable contract.** API endpoints are versioned, documented via OpenAPI, and plugin contracts are real interfaces (not `object`-typed TODOs).
3. **Every internal pattern has one canonical way.** Data access, transactions, logging, and async startup all follow a single, documented pattern that future features copy rather than reinvent.
4. **The product deliverables CONCEPT.md promises can be produced by pushing a tag.** Four single-file binaries (win-x64, linux-x64, osx-x64, osx-arm64) built automatically on `v*` tags and attached to a GitHub Release.
5. **CI runs on every push.** Build and tests pass on ubuntu-latest and windows-latest, plus `dotnet format --verify-no-changes` as a style gate.

## Guiding principles

- **Master stays green after every phase.** No half-merged refactors; each phase is a complete, deployable step.
- **External shape before internal refactors.** Locking what consumers see (phase 2) before reshaping internals (phase 3) avoids reworking contracts after callers exist.
- **No new features.** Every change either fixes a bug, establishes a pattern future features will copy, or hardens infra.
- **Direct-to-master, one logical commit per step.** Solo dev, no review overhead.
- **Stop-anywhere resilience.** After any phase completes, master is deployable and strictly better than before; feature work can interrupt and return.

## Total estimate

~8–10 working days across four phases.

---

## Phase 1 — Correctness sweep (~1 day)

Bug and safety fixes only. No refactoring.

### 1.1 Fix `ResourceProvider.ExistsAsync` embedded-resource probe

**File:** `src/Fishbowl.Data/ResourceProvider.cs:87-105`

`ExistsAsync` probes only the legacy dot-notation embedded path (`{AssemblyName}.Resources.{dotPath}`). `GetAsync` probes three path forms (as-is / backslashes / dot-notation). Because the `.csproj` uses `LogicalName="%(RecursiveDir)%(Filename)%(Extension)"`, actual manifest names match forms 1 or 2 on Windows but never form 3 — so `ExistsAsync` returns `false` for resources that `GetAsync` successfully loads.

**Fix:** extract a private `TryResolveEmbeddedStream(string path) -> Stream?` helper containing the three-fallback logic. Use it from both `GetAsync` and `ExistsAsync`. For `ExistsAsync`, dispose the stream immediately after confirming it resolved.

**Tests:** add unit tests for `ExistsAsync` covering subfolder paths with forward slash and backslash. Existing `GetAsync_ResolvesSubFolder*` tests remain.

### 1.2 Remove hardcoded OAuth credentials

**File:** `src/Fishbowl.Host/Program.cs:100-109`

The localhost/dev branch writes a placeholder-looking `ClientId` and an obviously-invented `ClientSecret` into `system.db` if no config exists. Even as placeholders, this shape invites a real secret to land in git history.

**Fix:** delete the auto-seeding branch. If `Google:ClientId` is absent from `system_config`, the `GoogleOptions` configure callback sets the options to empty strings; the `/login` endpoint's existing check already redirects to `/setup` in that case. Local development uses user-secrets or the setup flow — never source.

**Tests:**
- `AuthBehaviorTests.GetLoginChallenge_RedirectsToGoogle_Test` currently asserts `client_id=1049281787342...` from the auto-seed. Update it: seed the Google config explicitly in the test setup (via `ISystemRepository.SetConfigAsync` on the test host) and assert against those seeded values instead.
- Add `AuthBehaviorTests.GetLogin_RedirectsToSetup_WhenUnconfigured_Test` — when `system_config` has no `Google:ClientId`, `GET /login` returns a 302 to `/setup`.

### 1.3 Populate `notes_fts` on Note writes

**File:** `src/Fishbowl.Data/Repositories/NoteRepository.cs`

The `notes_fts` FTS5 virtual table is created in the user DB schema but never populated. Any search feature built later will silently return zero results.

**Fix:** on `CreateAsync`, `UpdateAsync`, and `DeleteAsync`, mirror the title/content/tags into `notes_fts` in the same transaction as the primary write. Use the transaction helper introduced in phase 3 once available; for phase 1, use inline `BeginTransaction` / `Commit`. Tags flatten to a space-joined string for FTS indexing.

**Tests:** add a `NoteRepositoryTests.Create_PopulatesFts_Test` that inserts a note and asserts `SELECT rowid FROM notes_fts WHERE notes_fts MATCH 'term'` returns a row. Same pattern for update (changed content is findable, old content is not) and delete (row removed).

### 1.4 Delete `Class1.cs` stubs

**Files:** `Class1.cs` in each of:
- `src/Fishbowl.Core/`
- `src/Fishbowl.Data/`
- `src/Fishbowl.Api/`
- `src/Fishbowl.Search/`
- `src/Fishbowl.Sync/`
- `src/Fishbowl.Scheduler/`
- `src/Fishbowl.Scripting/`
- `src/Fishbowl.Bot.Discord/`

Placeholder files from `dotnet new`. Delete. Projects remain (empty `.csproj` files are fine).

**Tests:** build succeeds with no files changed beyond deletions.

---

## Phase 2 — External contracts (~2–3 days)

Lock down everything future consumers (API clients, plugin authors, CI) will touch.

### 2.1 Version the API

**Files:** `src/Fishbowl.Api/Endpoints/NotesApi.cs`, `src/Fishbowl.Api/Endpoints/TodoApi.cs`, all integration tests, `CLAUDE.md`.

Change `MapGroup("/api/notes")` → `MapGroup("/api/v1/notes")`; same for todos. Update `CLAUDE.md` and every test URL.

**Tests:** existing `ApiIntegrationTests` updated to new paths. Add one test verifying `/api/notes` (no version) returns 404.

### 2.2 OpenAPI specification at `/api/openapi.json`

**Files:** `src/Fishbowl.Host/Program.cs`, `src/Fishbowl.Host/Fishbowl.Host.csproj`.

Use .NET 10's built-in `Microsoft.AspNetCore.OpenApi` package. Register with `builder.Services.AddOpenApi()` and `app.MapOpenApi("/api/openapi.json")`. Annotate each endpoint with `.WithName()`, `.WithSummary()`, and response type metadata (`.Produces<Note>()`, `.Produces(401)`, etc.).

**Tests:** `ApiIntegrationTests.OpenApi_DocumentAvailable_Test` — GET `/api/openapi.json` returns 200 with a JSON body that contains the expected paths.

### 2.3 Define plugin contracts in Core

**File:** `src/Fishbowl.Core/IFishbowlPlugin.cs` (expand into multiple files under `src/Fishbowl.Core/Plugins/`).

Replace the `object`-typed TODOs in `IFishbowlApi` with minimal-but-real interfaces:

```csharp
public interface IBotClient
{
    string Name { get; }                              // "discord", "telegram"
    Task SendAsync(string userId, string message, CancellationToken ct);
    IAsyncEnumerable<IncomingMessage> ReceiveAsync(CancellationToken ct);
}

public record IncomingMessage(string UserId, string Text, DateTime ReceivedAt);

public interface ISyncProvider
{
    string Name { get; }                              // "google-calendar", "ical"
    Task<SyncResult> PullAsync(string userId, SyncSource source, CancellationToken ct);
    Task PushAsync(string userId, SyncTarget target, IEnumerable<Event> events, CancellationToken ct);
}

public record SyncResult(int Added, int Updated, int Removed);

public interface IScheduledJob
{
    string Name { get; }
    string CronExpression { get; }                    // standard 5-field cron
    Task ExecuteAsync(CancellationToken ct);
}
```

Update `IFishbowlApi`:

```csharp
public interface IFishbowlApi
{
    void AddBotClient(IBotClient client);
    void AddSyncProvider(ISyncProvider provider);
    void AddScheduledJob(IScheduledJob job);
}
```

`SyncSource` / `SyncTarget` / `Event` types live in `Fishbowl.Core.Models` (`Event` already exists). `SyncSource` and `SyncTarget` are new records carrying the config JSON from the `sync_sources` table.

**Tests:** `Fishbowl.Core.Tests` adds compile-only tests: a fake `IBotClient` / `ISyncProvider` / `IScheduledJob` implementation compiles against the interfaces. No runtime behavior tested here — these are contracts.

### 2.4 Wire the plugin loader at startup

**File:** `src/Fishbowl.Host/Program.cs`.

After DI registration but before `app.Build()`, add a block that:
1. Reads `fishbowl-mods/plugins/*.dll`.
2. For each: instantiate a `PluginLoadContext`, load the assembly, find all `IFishbowlPlugin` types, instantiate, call `Register(services, apiImpl)` where `apiImpl` is a host-side implementation of `IFishbowlApi` that registers the passed clients/providers/jobs into DI.
3. Log each loaded plugin at Information level.
4. Any plugin whose `Register` throws is logged as an error and skipped — one bad plugin doesn't kill the host.

**Tests:** `Fishbowl.Host.Tests` adds `PluginAutoLoadTests` that:
- Compiles a minimal plugin via Roslyn (reuse pattern from `PluginIsolationTests`).
- Writes it to a temp `fishbowl-mods/plugins/` directory.
- Points the Host's plugin-scan path at the temp directory (via environment variable or test config).
- Boots the app.
- Asserts the plugin's registered `IBotClient` / `ISyncProvider` / `IScheduledJob` is resolvable from DI.

### 2.5 GitHub Actions CI

**File:** `.github/workflows/ci.yml` (new).

```yaml
name: CI
on:
  push:
    branches: [master]
  pull_request:

jobs:
  build-test:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet format --verify-no-changes
      - run: dotnet restore Fishbowl.sln
      - run: dotnet build Fishbowl.sln -c Release --no-restore -p:ContinuousIntegrationBuild=true
      - run: dotnet test Fishbowl.sln -c Release --no-build --logger "trx;LogFileName=test-results.trx"
      - if: always()
        uses: dorny/test-reporter@v1
        with:
          name: tests-${{ matrix.os }}
          path: '**/test-results.trx'
          reporter: dotnet-trx
```

The `ContinuousIntegrationBuild=true` flag suppresses the `Directory.Build.targets` auto-test-on-build so test output is reported once via the explicit `dotnet test` step.

**Tests:** the CI workflow itself runs as verification. Initial commit includes a trivial change to confirm the pipeline goes green.

---

## Phase 3 — Internal patterns (~3–4 days)

Establish the canonical ways of doing common things. Every future feature copies these patterns.

### 3.1 Typed Dapper mapping with snake_case convention

**Files:** `src/Fishbowl.Data/DatabaseFactory.cs`, `src/Fishbowl.Data/Repositories/NoteRepository.cs`, `src/Fishbowl.Data/Repositories/TodoRepository.cs`.

Add a static initializer that sets `Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true` once per process. This maps `created_at` to `CreatedAt`, `created_by` to `CreatedBy`, etc., automatically.

Replace `QuerySingleOrDefaultAsync<dynamic>` + manual `MapRowToNote` in `NoteRepository` with `QuerySingleOrDefaultAsync<Note>`. The columns that don't round-trip cleanly — `tags` (JSON text ↔ `List<string>`), `content_secret` (BLOB ↔ `byte[]`, already clean), `pinned` / `archived` (int ↔ bool), ISO strings ↔ `DateTime` — get handled via a Dapper `SqlMapper.TypeHandler<T>` registered alongside the convention setup.

Concretely:
- `JsonTagsHandler : SqlMapper.TypeHandler<List<string>>` — serializes/deserializes JSON for the `tags` column.
- `IsoDateTimeHandler : SqlMapper.TypeHandler<DateTime>` — reads ISO-8601 string, writes `"o"` format. (SQLite has no real `DATETIME`, so Dapper's default converts via `DateTime.Parse` on any string column read as `DateTime` — this handler just makes the write side explicit.)
- `BoolIntHandler` — unnecessary; Dapper already converts `INTEGER 0/1` to `bool` via `Convert.ToBoolean`. Document this, don't add a handler.

**Tests:** round-trip tests in `NoteRepositoryTests` — create a note with tags / pinned / archived / dates, read back, assert equality on all fields. Existing tests continue to pass.

### 3.2 Transaction helper on `DatabaseFactory`

**File:** `src/Fishbowl.Data/DatabaseFactory.cs`.

Add:

```csharp
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
```

And a generic overload returning `Task<T>`.

Retrofit `NoteRepository.CreateAsync` / `UpdateAsync` / `DeleteAsync` (now writing both `notes` and `notes_fts`) to use the helper.

**Tests:** `DatabaseFactoryTests.WithUserTransaction_RollsBackOnException_Test` — helper runs work that throws after inserting a row; assert row is not persisted.

### 3.3 `ILogger<T>` across all layers

**Files:** every class that performs a non-trivial operation.

Inject `ILogger<T>` via constructor in:
- `DatabaseFactory` — log migrations applied (`Applied schema v{Version} to {DbPath}`).
- `ResourceProvider` — log at Debug which tier resolved a request (`Resource {Path} served from {Source}`).
- `NoteRepository` / `TodoRepository` / `SystemRepository` — log at Debug for writes, at Warning for failed updates/deletes.
- Plugin loader (new in phase 2.4) — log at Information per plugin loaded, at Error per plugin that failed.
- Auth flow in `Program.cs` — log at Information when a new user is provisioned (`Provisioned user {UserId} via {Provider}`).

No PII (email, name) in log messages. No secrets ever. Never log full OAuth tokens or the contents of `::secret` blocks.

**Tests:** no direct tests for log output (brittle). Confirm via manual run or CI log inspection that expected events appear at expected levels.

### 3.4 Async startup init for OAuth config

**File:** `src/Fishbowl.Host/Program.cs`.

Replace the two `.GetAwaiter().GetResult()` calls in `AddOptions<GoogleOptions>.Configure` with a proper async initialization pattern.

Introduce a `ConfigurationCache` singleton that holds a snapshot of relevant `system_config` values. Add a `ConfigurationInitializer : IHostedService` that populates the cache via `ISystemRepository` in `StartAsync` — this runs once before the server listens.

Change the `GoogleOptions` configure to read from `ConfigurationCache` synchronously (the cache is already populated by the time the options are resolved on the first request).

When `POST /api/setup` completes, it updates the cache in-memory so subsequent auth challenges see the new values without a restart.

**Tests:** `ConfigurationInitializerTests` — initializer populates cache from a fresh `system.db`. `AuthBehaviorTests` updated so the setup flow → login challenge end-to-end works without a restart.

---

## Phase 4 — Product infra (~2–3 days)

### 4.1 Release pipeline

**File:** `.github/workflows/release.yml` (new).

```yaml
name: Release
on:
  push:
    tags: ['v*']
  workflow_dispatch:

jobs:
  build:
    strategy:
      matrix:
        include:
          - rid: win-x64
            os: windows-latest
            ext: .exe
          - rid: linux-x64
            os: ubuntu-latest
            ext: ''
          - rid: osx-x64
            os: macos-latest
            ext: ''
          - rid: osx-arm64
            os: macos-latest
            ext: ''
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: >
          dotnet publish src/Fishbowl.Host
          -c Release
          -r ${{ matrix.rid }}
          -p:PublishSingleFile=true
          -p:SelfContained=true
          -p:IncludeNativeLibrariesForSelfExtract=true
          -o publish
      - run: mv publish/Fishbowl.Host${{ matrix.ext }} publish/fishbowl-${{ matrix.rid }}${{ matrix.ext }}
        shell: bash
      - uses: actions/upload-artifact@v4
        with:
          name: fishbowl-${{ matrix.rid }}
          path: publish/fishbowl-${{ matrix.rid }}${{ matrix.ext }}

  release:
    needs: build
    runs-on: ubuntu-latest
    if: startsWith(github.ref, 'refs/tags/v')
    steps:
      - uses: actions/download-artifact@v4
        with:
          path: artifacts
      - uses: softprops/action-gh-release@v2
        with:
          files: artifacts/**/*
          generate_release_notes: true
```

`workflow_dispatch` trigger enables a dry-run without tagging.

**Tests:** manually dispatch the workflow once after phase 4 completes; verify all four artifacts upload successfully. Don't publish a release until the actual first version is ready.

### 4.2 `/setup` flow hardening

**File:** `src/Fishbowl.Host/Program.cs`.

- **Input validation on `POST /api/setup`**: `ClientId` non-empty, ends with `.apps.googleusercontent.com`. `ClientSecret` non-empty, minimum length 20 chars. Return `400 Bad Request` with a specific error message otherwise.
- **Antiforgery**: `builder.Services.AddAntiforgery()`, `app.UseAntiforgery()`, and require a token on `POST /api/setup`. The GET `/setup` HTML page includes the token in a hidden field.
- **Lockout after configuration**: once `Google:ClientId` is present and non-empty in `system_config`, both `GET /setup` and `POST /api/setup` return `404 Not Found`. No redirect (harder to bypass with header manipulation).

**Tests:**
- `AuthBehaviorTests.Setup_Returns404_WhenConfigured_Test`.
- `SetupFlowTests.PostSetup_Rejects_WhenAntiforgeryMissing_Test`.
- `SetupFlowTests.PostSetup_Rejects_InvalidClientIdFormat_Test`.

### 4.3 `CONTRIBUTING.md`

**File:** `CONTRIBUTING.md` (new, repo root).

One page, sections:
- **Layering rules** — the dependency direction diagram; when to put code in Core vs Data vs Api vs Host.
- **Adding a schema change** — bump `CurrentVersion` in `DatabaseFactory`, add `ApplyVN(conn)` method, add test. No data migration helpers yet; introduce when needed.
- **Adding an endpoint** — use `ILogger<T>`, scope every query by `fishbowl_user_id` claim, route under `/api/v1`, annotate with OpenAPI metadata.
- **Adding a plugin** — implement `IFishbowlPlugin.Register(services, api)`; use `api.AddBotClient` / `AddSyncProvider` / `AddScheduledJob` to contribute capabilities.
- **Testing conventions** — xUnit v3, `WebApplicationFactory<Program>` for integration, `TestAuthHandler` with `X-Test-User-Id` header, temp data dirs, `SqliteConnection.ClearAllPools()` on dispose.

### 4.4 Loose ends

Sweep remaining TODOs from phases 1–3. Bump README.md to include a real "Running locally" section (the current README is effectively empty). Verify CLAUDE.md still reflects reality — update anything that drifted during the hardening work.

---

## Out of scope for this pass

Called out explicitly so they don't creep in:

- Search implementation (FTS5 query logic, semantic search, embedding model download). Only FTS5 **population** is in scope (phase 1.3).
- Discord bot implementation. Only the `IBotClient` **contract** is in scope (phase 2.3).
- Calendar sync implementation. Only the `ISyncProvider` **contract** is in scope (phase 2.3).
- Scheduler implementation. Only the `IScheduledJob` **contract** is in scope (phase 2.3).
- Hash-versioned resource URLs / `window.__fishbowl_assets` manifest (CONCEPT.md "Content-Hash Versioning"). Defer to the web UI work that will actually consume it.
- Secrets / `::secret` block handling. Defer until note editing UI exists.
- Teams, Apps, Triggers — not in this pass under any interpretation.
- Loopback-only restriction on `/setup` — explicitly rejected during design; the "stranger configures first" scenario is mostly theoretical for home self-hosting.

## Open questions

None remaining. All prior open questions resolved during brainstorming:
- **CI scope**: ubuntu + windows, with `dotnet format` gate.
- **Release pipeline**: yes, tag-triggered, four RIDs.
- **Logging**: built-in `ILogger<T>`.
- **Branching**: direct-to-master, one logical commit per step.
- **Plugin contract depth**: minimal-viable (enough for Discord bot and Google Calendar sync to build against).
- **`/setup` loopback-only**: rejected.
