# MCP Memory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship an MCP server endpoint on top of Fishbowl's existing notes substrate so Claude Code can read and write project-scoped developer knowledge, with team-scoped SQLite databases, scoped API keys, hybrid (FTS + vector) search, and a tag-based review workflow.

**Architecture:** Six sequential phases. Master stays green after every task. Each phase is a pause point. Direct-to-master, one logical commit per task. Schema migrations are additive per-phase (user DB v3 in Phase 1, system DB v2 in Phase 2, system DB v3 in Phase 3, user DB v4 in Phase 5).

**Tech Stack:** .NET 10 (`net10.0`), ASP.NET Core Minimal APIs, Dapper, Microsoft.Data.Sqlite, xUnit v3, Playwright. New dependencies: `sqlite-vec` (native extension, embedded in `Fishbowl.Data`), `Microsoft.ML.OnnxRuntime`, `Tokenizers.DotNet`. MCP transport: hand-rolled Streamable HTTP per spec 2025-03 (no SDK — it's ~300 LOC).

**Spec:** [`docs/superpowers/specs/2026-04-21-mcp-memory-design.md`](./2026-04-21-mcp-memory-design.md)

**Human checkpoint before commit** — the user's standing rule ([memory](../../../C:\Users\goosefx\.claude\projects\C--Users-goosefx-SynologyDrive-PROJECTS-the-fishbowl\memory\feedback_test_before_commit.md)) requires manual testing between "implementation done" and commit. Every task ends with a `Manual test` step before the `Commit` step. Never skip it.

---

## File structure

**New files:**

```
src/Fishbowl.Core/
  Models/
    Team.cs                             [Task 2.2]
    TeamMember.cs                       [Task 2.2]
    ApiKey.cs                           [Task 3.1]
    MemorySearchResult.cs               [Task 5.4]
  Repositories/
    ITeamRepository.cs                  [Task 2.3]
    IApiKeyRepository.cs                [Task 3.1]
  Search/
    IEmbeddingService.cs                [Task 5.2]
    ISearchService.cs                   [Task 5.4]
  Mcp/
    McpTool.cs                          [Task 4.3]
    McpContextClaims.cs                 [Task 3.2]

src/Fishbowl.Data/Repositories/
  TeamRepository.cs                     [Task 2.3]
  ApiKeyRepository.cs                   [Task 3.1]

src/Fishbowl.Data/Embedded/
  sqlite-vec/
    win-x64/vec0.dll                    [Task 5.1 — checked in]
    linux-x64/libvec0.so                [Task 5.1 — checked in]
    osx-arm64/libvec0.dylib             [Task 5.1 — checked in]
    osx-x64/libvec0.dylib               [Task 5.1 — checked in]

src/Fishbowl.Search/
  Fishbowl.Search.csproj                [Task 5.2 — activate empty shell]
  EmbeddingService.cs                   [Task 5.2]
  MiniLmPipeline.cs                     [Task 5.2]
  HybridSearchService.cs                [Task 5.4]
  SqliteVecLoader.cs                    [Task 5.1]
  ModelDownloader.cs                    [Task 5.2]

src/Fishbowl.Mcp/
  Fishbowl.Mcp.csproj                   [Task 4.1]
  Endpoints/
    McpEndpoint.cs                      [Task 4.2]
    McpJsonRpc.cs                       [Task 4.2]
  Tools/
    SearchMemoryTool.cs                 [Task 4.3]
    RememberTool.cs                     [Task 4.3]
    GetMemoryTool.cs                    [Task 4.3]
    UpdateMemoryTool.cs                 [Task 4.3]
    ListPendingTool.cs                  [Task 4.3]
  ToolRegistry.cs                       [Task 4.3]

src/Fishbowl.Api/Endpoints/
  TeamsApi.cs                           [Task 2.4]
  ApiKeysApi.cs                         [Task 3.3]

src/Fishbowl.Host/Auth/
  ApiKeyAuthenticationHandler.cs       [Task 3.2]
  ApiKeyAuthenticationOptions.cs       [Task 3.2]
  ScopedAuthorizationExtensions.cs     [Task 3.4]

src/Fishbowl.Data/Resources/js/views/
  fb-teams-settings-view.js             [Task 2.5]
  fb-keys-settings-view.js              [Task 3.3]

src/Fishbowl.Data/Resources/js/components/
  fb-review-actions.js                  [Task 6.1]

src/Fishbowl.Data.Tests/
  TagSystemFlagTests.cs                 [Task 1.1, 1.2]
  Repositories/TeamRepositoryTests.cs   [Task 2.3]
  Repositories/ApiKeyRepositoryTests.cs [Task 3.1]
  ContextRefTests.cs                    [Task 2.1]

src/Fishbowl.Host.Tests/
  TeamsApiTests.cs                      [Task 2.4]
  ApiKeysApiTests.cs                    [Task 3.3]
  ApiKeyAuthTests.cs                    [Task 3.2, 3.4]
  SchemaV3MigrationTests.cs             [Task 1.1]
  SystemSchemaV2MigrationTests.cs       [Task 2.2]
  SystemSchemaV3MigrationTests.cs       [Task 3.1]
  SchemaV4MigrationTests.cs             [Task 5.1]

src/Fishbowl.Mcp.Tests/
  Fishbowl.Mcp.Tests.csproj             [Task 4.2]
  McpEndpointTests.cs                   [Task 4.2]
  SearchMemoryToolTests.cs              [Task 4.3]
  RememberToolTests.cs                  [Task 4.4]
  SecretStripInvariantTests.cs          [Task 4.5]

src/Fishbowl.Search.Tests/
  Fishbowl.Search.Tests.csproj          [Task 5.2]
  EmbeddingServiceTests.cs              [Task 5.2]
  HybridSearchServiceTests.cs           [Task 5.4]
```

**Modified files:**

```
src/Fishbowl.Core/
  Models/Tag.cs                         [Task 1.1 — add IsSystem]
  Repositories/INoteRepository.cs       [Task 2.1 — ContextRef overloads; Task 4.4 — source hint]
  Repositories/ITagRepository.cs        [Task 2.1 — ContextRef overloads]
  Repositories/ITodoRepository.cs       [Task 2.1 — ContextRef overloads]

src/Fishbowl.Data/
  DatabaseFactory.cs                    [Task 1.1 — v3 migration; 2.1 — ContextRef; 2.2 — system v2; 3.1 — system v3; 5.1 — v4 + sqlite-vec]
  Repositories/TagRepository.cs         [Task 1.1 — EnsureExistsAsync emits is_system=0; 1.2 — guard Rename/Delete; 2.1 — ContextRef]
  Repositories/NoteRepository.cs        [Task 2.1 — ContextRef; 4.4 — auto-tag source:claude + review:pending; 5.3 — embed on write]
  Repositories/TodoRepository.cs        [Task 2.1 — ContextRef]
  Repositories/SystemRepository.cs      [Task 2.2, 3.1 — new tables]
  Fishbowl.Data.csproj                  [Task 5.1 — embed sqlite-vec binaries]

src/Fishbowl.Api/Endpoints/
  NotesApi.cs                           [Task 2.1 — resolve context from claims; Task 3.4 — scope enforcement]
  TagsApi.cs                            [Task 1.2 — 400 mapping for system tags; Task 2.1 — context resolution]
  TodoApi.cs                            [Task 2.1, 3.4]

src/Fishbowl.Host/
  Program.cs                            [Task 2.4 — MapTeamsApi; 3.2 — AddApiKeyScheme; 3.3 — MapApiKeysApi; 4.1 — MapMcpEndpoint; 5.1 — sqlite-vec init; 5.2 — register embedding service]
  Fishbowl.Host.csproj                  [Task 4.1 — reference Fishbowl.Mcp; 5.2 — reference Fishbowl.Search]

src/Fishbowl.Data/Resources/
  index.html                            [Task 2.5, 3.3, 6.1 — register new views/components]
  js/lib/api.js                         [Task 2.5 — fb.api.teams; 3.3 — fb.api.keys]
  js/views/fb-notes-view.js             [Task 6.1 — needs-review filter + fb-review-actions]

Fishbowl.sln                            [Task 4.1 — add Fishbowl.Mcp; 5.2 — activate Fishbowl.Search; 4.2, 5.2 — test projects]
CLAUDE.md                               [Task 6.2 — document new patterns: ContextRef, Bearer auth, MCP surface, system tags]
```

---

# Phase 1 — System tags (~1 day)

Goal: make system tags first-class. Add `is_system` column, seed `review:pending`, `review:approved`, `source:claude`, `source:human`, and reject rename/delete on system tags.

## Task 1.1: Schema v3 — `is_system` column + seed

**Files:**
- Modify: `src/Fishbowl.Core/Models/Tag.cs` (add `bool IsSystem { get; set; }`)
- Modify: `src/Fishbowl.Data/DatabaseFactory.cs` (new `ApplyUserV3` + version bump)
- Create: `src/Fishbowl.Host.Tests/SchemaV3MigrationTests.cs`
- Create: `src/Fishbowl.Data.Tests/TagSystemFlagTests.cs`

- [ ] **Step 1: Write the failing migration test**

`SchemaV3MigrationTests.cs` — structure mirrors existing `SchemaV2MigrationTests`. Seed a v2 user DB (notes + tags, `user_version = 2`), open via factory, assert:
- `PRAGMA user_version` returns 3.
- `tags` table has `is_system` column (query `pragma_table_info('tags')`).
- Four system-tag rows exist with `is_system = 1`: `review:pending`, `review:approved`, `source:claude`, `source:human`.
- Pre-existing user tags still present with `is_system = 0`.

- [ ] **Step 2: Verify it fails**

`dotnet test --filter "FullyQualifiedName~SchemaV3MigrationTests"` — missing column error.

- [ ] **Step 3: Implement migration**

In `DatabaseFactory.cs`, add `ApplyUserV3`:

```csharp
private void ApplyUserV3(IDbConnection connection)
{
    using var tx = connection.BeginTransaction();
    try
    {
        connection.Execute(
            "ALTER TABLE tags ADD COLUMN is_system INTEGER NOT NULL DEFAULT 0",
            transaction: tx);

        var now = DateTime.UtcNow.ToString("o");
        foreach (var (name, color) in SystemTags.Seeds)
        {
            connection.Execute(@"
                INSERT INTO tags(name, color, created_at, is_system) VALUES (@name, @color, @createdAt, 1)
                ON CONFLICT(name) DO UPDATE SET is_system = 1",
                new { name, color, createdAt = now }, transaction: tx);
        }
        tx.Commit();
    }
    catch { tx.Rollback(); throw; }
}
```

Add a version-3 branch in `EnsureUserInitialized` following the existing v2 pattern. Bump the constant that marks current version. Create `src/Fishbowl.Core/Util/SystemTags.cs` with the four seed tuples (name + palette slot).

- [ ] **Step 4: Update `Tag` model**

Add `public bool IsSystem { get; set; }`. Update `TagRepository.GetAllAsync` SQL to include `is_system AS IsSystem`. Tag API already returns `Tag` via JSON — the new field appears automatically.

- [ ] **Step 5: Run all tests**

`dotnet test` — schema tests pass, existing tag tests still pass. Fix any call sites that broke from the new property.

- [ ] **Step 6: Manual test**

Launch `dotnet run --project src/Fishbowl.Host`, log in, navigate to notes, check the tags dropdown — confirm the four system tags appear and are marked distinguishable (colour + any visual cue; could just be the colon in their name for now).

- [ ] **Step 7: Commit**

Message: `feat(tags): schema v3 adds is_system flag + seeded system tags`

## Task 1.2: Guard rename/delete on system tags

**Files:**
- Modify: `src/Fishbowl.Data/Repositories/TagRepository.cs`
- Modify: `src/Fishbowl.Data.Tests/TagSystemFlagTests.cs` (add guard cases)
- Modify: `src/Fishbowl.Api/Endpoints/TagsApi.cs` (400 on guard exception — already does for ArgumentException, verify)

- [ ] **Step 1: Write failing tests**

In `TagSystemFlagTests.cs`, cover:
- `RenameAsync` on a system tag throws `ArgumentException` with message `"cannot rename system tag"`.
- `DeleteAsync` on a system tag throws `ArgumentException` with message `"cannot delete system tag"`.
- `UpsertColorAsync` on a system tag succeeds (colour is presentation — editable).
- Non-system tags still rename/delete normally.

- [ ] **Step 2: Verify fails**

- [ ] **Step 3: Implement guards**

At the top of `RenameAsync` and `DeleteAsync`, query `SELECT is_system FROM tags WHERE name = @normalized` and throw if 1. Do this inside the transaction so the check is consistent with the write.

- [ ] **Step 4: Run all tests**

- [ ] **Step 5: Manual test**

In the UI, attempt to rename or delete `review:pending` — confirm the API returns a 400 and the UI shows the error. Try editing its colour via a user tag with the same mechanism as non-system tags — should work.

- [ ] **Step 6: Commit**

Message: `feat(tags): reject rename/delete on system tags`

---

# Phase 2 — Team context infrastructure (~3 days)

Goal: establish team-scoped SQLite files and make `DatabaseFactory` accept a `ContextRef`. No auth changes yet — teams are cookie-accessible only. After this phase, the personal flow is unchanged and a logged-in user can create a team and read/write notes in it via `/api/v1/teams/{id}/notes` (or similar).

## Task 2.1: `ContextRef` type + factory overload

**Files:**
- Create: `src/Fishbowl.Core/ContextRef.cs`
- Create: `src/Fishbowl.Data.Tests/ContextRefTests.cs`
- Modify: `src/Fishbowl.Data/DatabaseFactory.cs`

- [ ] **Step 1: Write failing tests**

`ContextRefTests.cs` — exercise the factory:
- `CreateContextConnection(ContextRef.User("abc"))` opens `users/abc.db` and runs all migrations (v1→v3).
- `CreateContextConnection(ContextRef.Team("fishbowl"))` opens `teams/fishbowl.db` and runs all user-DB migrations.
- `WithContextTransactionAsync(ContextRef.Team(...), ...)` commits/rolls back symmetrically.
- Rejects unknown `ContextType` values.

- [ ] **Step 2: Verify fails**

- [ ] **Step 3: Implement `ContextRef`**

```csharp
public readonly record struct ContextRef(ContextType Type, string Id)
{
    public static ContextRef User(string id) => new(ContextType.User, id);
    public static ContextRef Team(string id) => new(ContextType.Team, id);
}
public enum ContextType { User, Team }
```

Add `_teamsPath` to `DatabaseFactory` ctor (creates `teams/` dir like `users/`). Add:

```csharp
public IDbConnection CreateContextConnection(ContextRef ctx)
{
    var dbPath = ctx.Type switch
    {
        ContextType.User => Path.Combine(_usersPath, $"{ctx.Id}.db"),
        ContextType.Team => Path.Combine(_teamsPath, $"{ctx.Id}.db"),
        _ => throw new ArgumentException($"unknown context type: {ctx.Type}")
    };
    return OpenAndInitialize(dbPath, EnsureUserInitialized);
}

public Task WithContextTransactionAsync(ContextRef ctx, Func<IDbConnection, IDbTransaction, CancellationToken, Task> work, CancellationToken ct = default)
    => /* same body as WithUserTransactionAsync but uses CreateContextConnection */;
```

Keep `CreateConnection(string userId)` and `WithUserTransactionAsync` — they delegate to the new overloads with `ContextRef.User(userId)`. No breaking change.

- [ ] **Step 4: Refactor repositories to accept `ContextRef`**

For `INoteRepository`, `ITagRepository`, `ITodoRepository`: add overloads taking `ContextRef` alongside existing `string userId` methods. The `userId` methods delegate internally by calling the `ContextRef` version with `ContextRef.User(userId)`. Zero behavioural change for cookie callers.

- [ ] **Step 5: Run all tests**

All existing repository/migration tests must still pass — they use the `userId` overloads, which delegate through. New `ContextRefTests` pass.

- [ ] **Step 6: Manual test**

Not user-visible yet. Just `dotnet run` and verify the app starts and personal notes still work.

- [ ] **Step 7: Commit**

Message: `feat(data): ContextRef + factory overload for team-scoped DBs`

## Task 2.2: System DB v2 — `teams` + `team_members`

**Files:**
- Create: `src/Fishbowl.Core/Models/Team.cs`
- Create: `src/Fishbowl.Core/Models/TeamMember.cs`
- Modify: `src/Fishbowl.Data/DatabaseFactory.cs` (new `ApplySystemV2`, bump system `user_version`)
- Create: `src/Fishbowl.Host.Tests/SystemSchemaV2MigrationTests.cs`

- [ ] **Step 1: Write failing migration test**

Seed a v1 `system.db` (users + user_mappings + system_config only), open via factory, assert:
- `PRAGMA user_version` = 2.
- `teams` table exists with columns: id, slug UNIQUE, name, created_by, created_at.
- `team_members` table exists with composite PK (team_id, user_id), role check constraint.
- Existing rows in `users` table untouched.

- [ ] **Step 2: Verify fails**

- [ ] **Step 3: Implement `ApplySystemV2`**

Per the spec's SQL. Add v2 branch in `EnsureSystemInitialized`. No data migration needed — tables are new.

- [ ] **Step 4: Model classes**

`Team { string Id; string Slug; string Name; string CreatedBy; DateTime CreatedAt; }`

`TeamMember { string TeamId; string UserId; string Role; DateTime JoinedAt; }` plus `public enum TeamRole { Readonly, Member, Admin, Owner }` with a parser helper.

- [ ] **Step 5: Run all tests**

- [ ] **Step 6: Manual test**

Not user-visible yet. `dotnet run` smoke.

- [ ] **Step 7: Commit**

Message: `feat(system): schema v2 adds teams + team_members tables`

## Task 2.3: `ITeamRepository` + `TeamRepository`

**Files:**
- Create: `src/Fishbowl.Core/Repositories/ITeamRepository.cs`
- Create: `src/Fishbowl.Data/Repositories/TeamRepository.cs`
- Create: `src/Fishbowl.Data.Tests/Repositories/TeamRepositoryTests.cs`

- [ ] **Step 1: Write failing tests**

- `CreateAsync(userId, name)` → generates slug from name (lowercase, hyphenated, dedup if collision by appending short suffix), writes to `teams`, writes an `owner` row to `team_members`, returns populated `Team`.
- `ListByMemberAsync(userId)` → returns teams the user is in, including their role.
- `GetBySlugAsync(slug)` → lookup.
- `GetMembershipAsync(teamId, userId)` → returns role or null.
- `DeleteAsync(teamId, userId)` → only owner can delete; removes team_members rows; does **not** delete the `.db` file (for v1 — leave it as recoverable; separate "purge" action later).

- [ ] **Step 2: Verify fails**

- [ ] **Step 3: Implement `TeamRepository`**

Dapper on `system.db`. Slug generation: helper in `Fishbowl.Core/Util/SlugGenerator.cs`. All writes inside a `CreateSystemConnection()` transaction.

- [ ] **Step 4: Register in `Program.cs`**

`builder.Services.AddScoped<ITeamRepository, TeamRepository>();`

- [ ] **Step 5: Run all tests**

- [ ] **Step 6: Manual test**

No UI yet. Defer to Task 2.5.

- [ ] **Step 7: Commit**

Message: `feat(teams): ITeamRepository with membership-aware queries`

## Task 2.4: `TeamsApi` endpoints

**Files:**
- Create: `src/Fishbowl.Api/Endpoints/TeamsApi.cs`
- Modify: `src/Fishbowl.Host/Program.cs` (call `app.MapTeamsApi()`)
- Create: `src/Fishbowl.Host.Tests/TeamsApiTests.cs`

- [ ] **Step 1: Write failing endpoint tests**

Using `TestAuthHandler` / `X-Test-User-Id` (per CLAUDE.md):
- `POST /api/v1/teams` with `{ "name": "Fishbowl Dev" }` → 200 + Team object with slug `fishbowl-dev`.
- `GET /api/v1/teams` → lists the user's teams.
- `GET /api/v1/teams/{slug}` → 200 for member; 404 for non-member.
- `DELETE /api/v1/teams/{slug}` → 204 for owner; 403 for non-owner.
- Notes CRUD under `/api/v1/teams/{slug}/notes` mirrors `/api/v1/notes` but resolves to `ContextRef.Team(teamId)`. Membership check 403s non-members, writes 403 for readonly.

- [ ] **Step 2: Verify fails**

- [ ] **Step 3: Implement endpoints**

Follow the `MapNotesApi` pattern: `MapTeamsApi()` extension on `IEndpointRouteBuilder`, routes under `/api/v1/teams`. For the nested notes path, factor the existing `NotesApi` body so both `/api/v1/notes` and `/api/v1/teams/{slug}/notes` call the same handler with different `ContextRef` resolution.

- [ ] **Step 4: Wire into `Program.cs`**

After `app.MapNotesApi();` add `app.MapTeamsApi();`.

- [ ] **Step 5: Run all tests**

- [ ] **Step 6: Manual test**

Start app, log in, hit the endpoints with curl (`Authorization` comes from session cookie — copy from browser devtools), confirm 200s and 403s land as expected. Create a team, POST a note to it, GET it back.

- [ ] **Step 7: Commit**

Message: `feat(teams): /api/v1/teams CRUD + nested notes endpoint`

## Task 2.5: Teams settings view

**Files:**
- Create: `src/Fishbowl.Data/Resources/js/views/fb-teams-settings-view.js`
- Modify: `src/Fishbowl.Data/Resources/index.html` (register view, add route)
- Modify: `src/Fishbowl.Data/Resources/js/lib/api.js` (add `fb.api.teams.*`)

- [ ] **Step 1: Design API surface**

`fb.api.teams.list()`, `fb.api.teams.create({ name })`, `fb.api.teams.delete(slug)`. Follows the existing `fb.api.notes.*` pattern. 401s auto-redirect to `/login` (same wrapper).

- [ ] **Step 2: Build the view**

`fb-teams-settings-view` — light DOM (per CLAUDE.md conventions), mounts a list of teams with name + slug + role, a "Create team" form (name input + submit), and a delete button (confirm via `fb.dialog.confirm`). Register with router at `#/settings/teams`.

- [ ] **Step 3: Add settings nav entry**

If a settings hub exists, add a tile. Otherwise add directly to `fb-nav` alongside existing entries. Remember the "no dead links" rule — wire it to a working view.

- [ ] **Step 4: Manual test**

The whole flow: create a team → appears in the list → delete it → gone. Notes in the team context are a Phase-4 concern (no UI to switch contexts yet) — leave a note in the backlog.

- [ ] **Step 5: Commit**

Message: `feat(teams): settings view for creating and listing teams`

---

# Phase 3 — API keys & Bearer auth (~3 days)

Goal: a logged-in user can mint an API key scoped to a specific context, and that key authenticates HTTP requests to `/api/v1/*`. After this phase, `curl -H "Authorization: Bearer fb_live_..." https://localhost:7180/api/v1/notes` returns the notes of the context the key is bound to.

## Task 3.1: System DB v3 — `api_keys` table + `ApiKeyRepository`

**Files:**
- Modify: `src/Fishbowl.Data/DatabaseFactory.cs` (new `ApplySystemV3`)
- Create: `src/Fishbowl.Core/Models/ApiKey.cs`
- Create: `src/Fishbowl.Core/Repositories/IApiKeyRepository.cs`
- Create: `src/Fishbowl.Data/Repositories/ApiKeyRepository.cs`
- Create: `src/Fishbowl.Host.Tests/SystemSchemaV3MigrationTests.cs`
- Create: `src/Fishbowl.Data.Tests/Repositories/ApiKeyRepositoryTests.cs`

- [ ] **Step 1: Write failing migration test**

Seed system.db at v2, open via factory, assert v3 applied and `api_keys` table shape per spec.

- [ ] **Step 2: Write failing repository tests**

- `IssueAsync(userId, contextType, contextId, name, scopes)` → returns `(ApiKey record, string rawToken)` tuple. Raw token never stored, only hashed. Format: `fb_live_{22-char-base64url}`.
- `LookupAsync(rawToken)` → returns `ApiKey` or null. Must hit the prefix index, then constant-time compare the hash.
- `ListByUserAsync(userId)` → returns all non-revoked keys for the user (without the raw token).
- `RevokeAsync(keyId, userId)` → sets `revoked_at`; revoked keys no longer match lookups.
- `TouchLastUsedAsync(keyId)` → updates `last_used_at` (fire-and-forget after successful auth).

- [ ] **Step 3: Verify fails**

- [ ] **Step 4: Implement**

Token generator: `RandomNumberGenerator.GetBytes(16)` → `Base64UrlEncoder.Encode` → `"fb_live_" + encoded`. Hash: SHA-256 of the raw token. Prefix: first 12 chars (covers `fb_live_` + 4 random chars). Constant-time compare via `CryptographicOperations.FixedTimeEquals`.

- [ ] **Step 5: Register**

`AddScoped<IApiKeyRepository, ApiKeyRepository>()`.

- [ ] **Step 6: Run all tests**

- [ ] **Step 7: Manual test**

Not user-visible yet. Defer to Task 3.3.

- [ ] **Step 8: Commit**

Message: `feat(system): schema v3 + ApiKeyRepository with SHA-256 hashed tokens`

## Task 3.2: `ApiKeyAuthenticationHandler`

**Files:**
- Create: `src/Fishbowl.Host/Auth/ApiKeyAuthenticationHandler.cs`
- Create: `src/Fishbowl.Host/Auth/ApiKeyAuthenticationOptions.cs`
- Create: `src/Fishbowl.Core/Mcp/McpContextClaims.cs` (claim name constants)
- Modify: `src/Fishbowl.Host/Program.cs` (register scheme)
- Create: `src/Fishbowl.Host.Tests/ApiKeyAuthTests.cs`

- [ ] **Step 1: Write failing tests**

- `Authorization: Bearer <invalid>` → 401.
- `Authorization: Bearer <revoked>` → 401.
- `Authorization: Bearer <valid>` + personal scope → `ClaimsPrincipal` has `fishbowl_user_id` = key's user, `fishbowl_context_type` = "user", `fishbowl_context_id` = user id.
- `Authorization: Bearer <valid>` + team scope → context claims point at the team.
- `last_used_at` is updated after a successful auth (query DB and check).
- No `Authorization` header → falls through to cookie scheme (doesn't break existing cookie tests).

- [ ] **Step 2: Verify fails**

- [ ] **Step 3: Implement handler**

Extends `AuthenticationHandler<ApiKeyAuthenticationOptions>`. In `HandleAuthenticateAsync`:
1. Read `Authorization` header. Expect `Bearer fb_live_...`. If absent or malformed, `AuthenticateResult.NoResult()` (lets cookie scheme run).
2. Call `IApiKeyRepository.LookupAsync(token)`. Null → `Fail`. Revoked → `Fail`.
3. Build `ClaimsPrincipal` with `fishbowl_user_id`, `fishbowl_context_type`, `fishbowl_context_id`, and one claim per scope (`scope:read:notes` etc.).
4. Fire-and-forget `TouchLastUsedAsync` (don't await — don't let DB writes block auth).

Scheme name: `"ApiKey"`. Register in `Program.cs` via `authBuilder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", null)` and add it to the composite policy for API routes so both cookie and key schemes are tried.

- [ ] **Step 4: Context resolution helper**

In `src/Fishbowl.Core/Mcp/McpContextClaims.cs`:

```csharp
public static class McpContextClaims
{
    public const string UserId = "fishbowl_user_id";
    public const string ContextType = "fishbowl_context_type";
    public const string ContextId = "fishbowl_context_id";

    public static ContextRef Resolve(ClaimsPrincipal user)
    {
        var type = user.FindFirst(ContextType)?.Value;
        var id = user.FindFirst(ContextId)?.Value;
        if (type == "team" && !string.IsNullOrEmpty(id)) return ContextRef.Team(id);
        var uid = user.FindFirst(UserId)?.Value;
        if (!string.IsNullOrEmpty(uid)) return ContextRef.User(uid);
        throw new InvalidOperationException("principal has no resolvable context");
    }
}
```

- [ ] **Step 5: Run all tests**

- [ ] **Step 6: Manual test**

Issue a key via `ApiKeyRepository` in a test console app (or temporary test), curl `/api/v1/notes` — succeeds with valid key, 401 with invalid.

- [ ] **Step 7: Commit**

Message: `feat(auth): ApiKeyAuthenticationHandler with context claims`

## Task 3.3: API keys settings view + `ApiKeysApi`

**Files:**
- Create: `src/Fishbowl.Api/Endpoints/ApiKeysApi.cs`
- Create: `src/Fishbowl.Data/Resources/js/views/fb-keys-settings-view.js`
- Modify: `src/Fishbowl.Data/Resources/js/lib/api.js` (`fb.api.keys.*`)
- Modify: `src/Fishbowl.Data/Resources/index.html`
- Create: `src/Fishbowl.Host.Tests/ApiKeysApiTests.cs`

- [ ] **Step 1: Write failing API tests**

- `POST /api/v1/keys` with `{ name, contextType, contextId, scopes }` (cookie-auth only) → 200 with `{ id, prefix, rawToken, createdAt, ... }`. `rawToken` present exactly once.
- `GET /api/v1/keys` → lists non-revoked keys (no raw token; just prefix + name + scopes + last_used_at).
- `DELETE /api/v1/keys/{id}` → 204, subsequent GET omits the revoked key.
- Bearer-auth attempt on these endpoints → 403 (key management is cookie-only — a key cannot revoke itself or mint new keys).

- [ ] **Step 2: Verify fails**

- [ ] **Step 3: Implement `ApiKeysApi`**

`MapApiKeysApi()` extension. `.RequireAuthorization(p => p.AddAuthenticationSchemes("Cookies"))` to block Bearer.

- [ ] **Step 4: Build settings view**

`fb-keys-settings-view` — list of keys with prefix, name, scopes, revoke button. "New key" form with name input, context dropdown (personal + each team), scopes checkboxes. On create, show the raw token in a modal (`fb-dialog`) with a copy button and a warning: "this is the only time you will see this key". Route at `#/settings/keys`.

- [ ] **Step 5: Register in `Program.cs`**

- [ ] **Step 6: Manual test**

Log in → Settings → Keys → create a key named "Claude Code test" scoped to personal with `read:notes` + `write:notes`. Copy the token. `curl -H "Authorization: Bearer $TOKEN" https://localhost:7180/api/v1/notes` → returns notes. Revoke. Curl again → 401.

- [ ] **Step 7: Commit**

Message: `feat(auth): API keys settings view + /api/v1/keys endpoints`

## Task 3.4: Scope enforcement on existing endpoints

**Files:**
- Create: `src/Fishbowl.Host/Auth/ScopedAuthorizationExtensions.cs`
- Modify: `src/Fishbowl.Api/Endpoints/NotesApi.cs`
- Modify: `src/Fishbowl.Api/Endpoints/TagsApi.cs`
- Modify: `src/Fishbowl.Api/Endpoints/TodoApi.cs`
- Modify: `src/Fishbowl.Host.Tests/ApiKeyAuthTests.cs` (add scope-denial cases)

- [ ] **Step 1: Write failing tests**

- Bearer key scoped to `read:notes` only → `GET /api/v1/notes` 200, `POST /api/v1/notes` 403.
- Bearer key scoped to a team → `GET /api/v1/notes` returns **team's** notes, not the user's personal notes (context isolation).
- Personal key cannot access `/api/v1/teams/{otherTeam}/notes` (403).

- [ ] **Step 2: Verify fails**

- [ ] **Step 3: Implement scope helper**

```csharp
public static RouteHandlerBuilder RequireScope(this RouteHandlerBuilder b, string scope)
    => b.RequireAuthorization(p => p.RequireAssertion(ctx =>
        ctx.User.Identity?.AuthenticationType == "Cookies" ||  // cookie = full access
        ctx.User.HasClaim("scope", scope)));
```

- [ ] **Step 4: Apply to endpoints**

`NotesApi.MapGet(...)` → `.RequireScope("read:notes")`. Each handler also replaces `user.FindFirst("fishbowl_user_id").Value` with `McpContextClaims.Resolve(user)` and calls the new `ContextRef` repository overload. Context isolation is automatic — the claim is from the token, which is bound to a single context at issuance.

- [ ] **Step 5: Run all tests**

Existing cookie-auth tests still pass (cookies bypass scope check per the helper). Scope tests pass.

- [ ] **Step 6: Manual test**

Mint a key with only `read:notes`, curl GET (works), curl POST (403). Mint a team key, curl GET — returns team notes.

- [ ] **Step 7: Commit**

Message: `feat(auth): scope enforcement on notes/tags/todos endpoints`

---

# Phase 4 — MCP endpoint (~4 days)

Goal: Claude Code connects to Fishbowl over MCP, calls five tools end-to-end, secret content is structurally absent from every response. After this phase, the user's `claude_desktop_config.json` has a working Fishbowl entry.

## Task 4.1: `Fishbowl.Mcp` project scaffold

**Files:**
- Create: `src/Fishbowl.Mcp/Fishbowl.Mcp.csproj`
- Modify: `Fishbowl.sln` (add project)
- Modify: `src/Fishbowl.Host/Fishbowl.Host.csproj` (project reference)
- Modify: `src/Fishbowl.Host/Program.cs` (placeholder `MapMcpEndpoint` call)

- [ ] **Step 1: Project setup**

`net10.0`, references `Fishbowl.Core`, `Fishbowl.Data`. No NuGet dependencies beyond what Core/Data already use. Matches existing `Fishbowl.Api.csproj` structure.

- [ ] **Step 2: Hello-world endpoint**

`MapMcpEndpoint(this IEndpointRouteBuilder)` extension. For now: `POST /mcp` returning `{ "jsonrpc": "2.0", "result": { "serverInfo": { "name": "fishbowl", "version": "0.1" } } }`. No auth yet.

- [ ] **Step 3: Smoke test**

Start app, `curl -X POST http://localhost:7180/mcp -H "Content-Type: application/json" -d '{}'` returns the stub.

- [ ] **Step 4: Commit**

Message: `chore(mcp): Fishbowl.Mcp project scaffold`

## Task 4.2: Streamable HTTP transport + auth

**Files:**
- Create: `src/Fishbowl.Mcp/Endpoints/McpEndpoint.cs`
- Create: `src/Fishbowl.Mcp/Endpoints/McpJsonRpc.cs`
- Create: `src/Fishbowl.Mcp.Tests/Fishbowl.Mcp.Tests.csproj`
- Create: `src/Fishbowl.Mcp.Tests/McpEndpointTests.cs`

- [ ] **Step 1: Write failing tests**

- `POST /mcp` without `Authorization` → 401.
- `POST /mcp` with Bearer token lacking `read:notes` → `tools/list` still works (listing is free), but `tools/call` on `search_memory` returns a JSON-RPC error with code `-32603` (scope denied).
- `POST /mcp` with `method: "initialize"` → returns server capabilities.
- `POST /mcp` with `method: "tools/list"` → returns the five tool schemas.
- Notifications (JSON-RPC messages with no `id`) receive 202 Accepted with empty body.

- [ ] **Step 2: Implement JSON-RPC parser**

`McpJsonRpc` — simple dispatcher. Reads request body as JSON, extracts `jsonrpc`, `id`, `method`, `params`. Dispatches to registered handlers. On exception, emits proper error envelope. No SSE for v1 — synchronous responses only (Claude Code's current MCP client accepts this).

- [ ] **Step 3: Implement `McpEndpoint`**

`MapMcpEndpoint()` → registers `POST /mcp` with `.RequireAuthorization()` (default policy picks Bearer via `ApiKeyAuthenticationHandler`). Handler extracts `ContextRef` via `McpContextClaims.Resolve(user)` and passes it to the tool dispatcher.

- [ ] **Step 4: `initialize` + `tools/list` handlers**

`initialize` returns `{ "protocolVersion": "2025-03-26", "capabilities": { "tools": {} }, "serverInfo": { "name": "fishbowl", "version": "..." } }`.

`tools/list` returns an array derived from `ToolRegistry` (Task 4.3).

- [ ] **Step 5: Manual test**

`claude mcp add --transport http fishbowl http://localhost:7180/mcp --header "Authorization: Bearer $TOKEN"` (or direct edit of `claude_desktop_config.json`). In a Claude Code session, the tools should list. No tools/call yet — just listing.

- [ ] **Step 6: Commit**

Message: `feat(mcp): POST /mcp endpoint with Streamable HTTP + Bearer auth`

## Task 4.3: Five tools wired up

**Files:**
- Create: `src/Fishbowl.Core/Mcp/McpTool.cs` (interface)
- Create: `src/Fishbowl.Mcp/ToolRegistry.cs`
- Create: `src/Fishbowl.Mcp/Tools/SearchMemoryTool.cs`
- Create: `src/Fishbowl.Mcp/Tools/RememberTool.cs`
- Create: `src/Fishbowl.Mcp/Tools/GetMemoryTool.cs`
- Create: `src/Fishbowl.Mcp/Tools/UpdateMemoryTool.cs`
- Create: `src/Fishbowl.Mcp/Tools/ListPendingTool.cs`
- Create: `src/Fishbowl.Mcp.Tests/SearchMemoryToolTests.cs`

- [ ] **Step 1: Define `IMcpTool`**

```csharp
public interface IMcpTool
{
    string Name { get; }
    string Description { get; }
    JsonSchema InputSchema { get; }
    string RequiredScope { get; }
    Task<object> InvokeAsync(ContextRef ctx, JsonElement args, CancellationToken ct);
}
```

- [ ] **Step 2: Implement tools as thin adapters**

Each tool reads its args (JSON), calls the existing repository/search method against `ctx`, and returns a plain object. Contract for each:

| Tool | Args | Calls |
|---|---|---|
| `search_memory` | `{ query, limit?, include_pending? }` | `ISearchService.HybridSearchAsync` (Phase 5 makes this real; for now: FTS-only fallback using `NoteRepository` — search by `title MATCH` / `content MATCH` via notes_fts, returns top N) |
| `remember` | `{ title, content, tags? }` | `INoteRepository.CreateAsync` with a `source=claude` hint |
| `get_memory` | `{ id }` | `INoteRepository.GetByIdAsync` |
| `update_memory` | `{ id, title?, content?, tags? }` | `INoteRepository.UpdateAsync` with `source=claude` hint |
| `list_pending` | `{ limit? }` | `NoteRepository.GetAllAsync(ctx, tags: ["review:pending"], match: "all")` |

Each tool's `RequiredScope` is checked by the JSON-RPC dispatcher before invocation.

- [ ] **Step 3: Secret-strip at the serialisation edge**

Before returning a note (or note snippet), pass it through a `SecretStripper` helper that replaces `::secret`…`::end` blocks with `[secret content hidden]` and nulls out `content_secret`. Applied to every tool's return path.

- [ ] **Step 4: Register in DI**

`builder.Services.AddSingleton<IMcpTool, SearchMemoryTool>()` for each, plus a `ToolRegistry` that enumerates them.

- [ ] **Step 5: Write tool invocation tests**

End-to-end: seed a context DB, call `tools/call` over HTTP, assert the response.

- [ ] **Step 6: Run all tests**

- [ ] **Step 7: Manual test**

In Claude Code: "search my memory for 'database factory'" — should return real hits. "Remember: the DatabaseFactory uses per-file boundaries for user isolation" — should create a note (check in the UI). "List pending reviews" — should show the note we just created.

- [ ] **Step 8: Commit**

Message: `feat(mcp): five tools — search/remember/get/update/list_pending`

## Task 4.4: Auto-tagging write path

**Files:**
- Modify: `src/Fishbowl.Core/Repositories/INoteRepository.cs` (add `NoteSource` enum param)
- Modify: `src/Fishbowl.Data/Repositories/NoteRepository.cs`
- Modify: `src/Fishbowl.Api/Endpoints/NotesApi.cs` (pass `NoteSource.Human` for cookie writes, `NoteSource.Claude` for Bearer writes — derived from auth type)
- Modify: `src/Fishbowl.Mcp/Tools/RememberTool.cs`, `UpdateMemoryTool.cs`
- Create: `src/Fishbowl.Mcp.Tests/RememberToolTests.cs`

- [ ] **Step 1: Write failing tests**

- Note created via `RememberTool` has tags including `source:claude` and `review:pending`.
- Note updated via `UpdateMemoryTool` re-adds `review:pending` (clears `review:approved` if present).
- Note created via cookie auth gets `source:human` and no `review:pending`.
- Note edited via cookie auth that had `review:pending` has it stripped (approval-by-editing).

- [ ] **Step 2: Implement `NoteSource` handling**

```csharp
public enum NoteSource { Human, Claude }

Task<string> CreateAsync(ContextRef ctx, Note note, NoteSource source, CancellationToken ct = default);
```

In the repository, manipulate `note.Tags` before the existing write: ensure `source:claude` or `source:human`; ensure `review:pending` on Claude writes; strip `review:pending` and `review:approved` on human writes.

- [ ] **Step 3: Pass source from endpoints / tools**

`NotesApi` inspects `user.Identity.AuthenticationType` — `"Cookies"` → `NoteSource.Human`, `"ApiKey"` → `NoteSource.Claude`. MCP tools always pass `NoteSource.Claude`.

- [ ] **Step 4: Manual test**

From Claude Code: `remember` a note, then open the UI and check the tags — should show `source:claude` + `review:pending`. Edit the title in the UI → tags lose `review:pending` on save.

- [ ] **Step 5: Commit**

Message: `feat(mcp): auto-tag source:claude + review:pending on Bearer writes`

## Task 4.5: Secret-strip invariant tests

**Files:**
- Create: `src/Fishbowl.Mcp.Tests/SecretStripInvariantTests.cs`

- [ ] **Step 1: Write the suite**

Seed a note with:

```
# Title
Public text
::secret
supersecret-token-abc123
::end
More public
```

Round-trip through every tool that returns note content (`search_memory` snippets, `get_memory` full content, `list_pending` if it includes snippets, `update_memory` echo). Assert `"supersecret-token-abc123"` never appears in any response payload (case-insensitive regex over the serialised JSON).

Then: inspect the raw SQLite file — the secret plaintext is gone from `content` (it moves to `content_secret` on write); confirm the MCP response never serialises `content_secret`.

- [ ] **Step 2: Verify passes**

If anything fails, trace back to the specific tool and add the strip. This is a non-negotiable invariant (CONCEPT § *Core Philosophy*, § *MCP Server*).

- [ ] **Step 3: Commit**

Message: `test(mcp): secret-strip invariant across every tool return path`

---

# Phase 5 — Hybrid search + embeddings (~3 days)

Goal: `search_memory` returns semantically relevant results. `sqlite-vec` is embedded and loads on every context DB. Embeddings are generated on note write. All existing notes can be re-embedded on demand.

## Task 5.1: sqlite-vec extension + user DB v4

**Files:**
- Create: `src/Fishbowl.Data/Embedded/sqlite-vec/{platform}/vec0.*` (checked-in binaries; download from official sqlite-vec release matching the pinned version)
- Modify: `src/Fishbowl.Data/Fishbowl.Data.csproj` (embed as resources)
- Create: `src/Fishbowl.Search/SqliteVecLoader.cs`
- Modify: `src/Fishbowl.Data/DatabaseFactory.cs` (call loader on every context open; new `ApplyUserV4`)
- Create: `src/Fishbowl.Host.Tests/SchemaV4MigrationTests.cs`

- [ ] **Step 1: Acquire binaries**

Pin a sqlite-vec version (e.g. v0.1.x). Download the platform binaries from the official release. Verify hashes. Check into the repo. Note the version in a README next to the folder.

- [ ] **Step 2: Extract + load helper**

`SqliteVecLoader.LoadInto(SqliteConnection)`:
1. Detect platform (`RuntimeInformation.OSArchitecture` / `IsOSPlatform`).
2. Extract the matching embedded binary to `Path.GetTempPath() + "fishbowl-vec-{version}/"` if not already there.
3. `conn.EnableExtensions(true)` + `SELECT load_extension(@path)`.

- [ ] **Step 3: Call loader on every context open**

In `OpenAndInitialize`, before running migrations, call `SqliteVecLoader.LoadInto(conn)`.

- [ ] **Step 4: Schema v4 — `vec_notes`**

```csharp
private void ApplyUserV4(IDbConnection conn)
{
    conn.Execute(@"CREATE VIRTUAL TABLE vec_notes USING vec0(
        id TEXT PRIMARY KEY, embedding FLOAT[384])");
}
```

Add v4 branch in `EnsureUserInitialized`. No backfill — existing notes embed lazily on next write, or en masse via Settings action (Task 5.2.7).

- [ ] **Step 5: Migration test**

Seed a v3 context DB, open via factory, assert `vec_notes` virtual table exists and `vec_version()` is callable (proves the extension loaded).

- [ ] **Step 6: Manual test**

Launch app, confirm it doesn't crash with the extension load. Create a new note (the vec_notes row won't populate yet — that's Task 5.3).

- [ ] **Step 7: Commit**

Message: `feat(search): embed sqlite-vec + user DB v4 adds vec_notes`

## Task 5.2: Embedding service + model download

**Files:**
- Modify: `src/Fishbowl.Search/Fishbowl.Search.csproj` (activate; add `Microsoft.ML.OnnxRuntime`, `Tokenizers.DotNet`)
- Create: `src/Fishbowl.Search/IEmbeddingService.cs`
- Create: `src/Fishbowl.Search/EmbeddingService.cs`
- Create: `src/Fishbowl.Search/MiniLmPipeline.cs`
- Create: `src/Fishbowl.Search/ModelDownloader.cs`
- Modify: `src/Fishbowl.Host/Program.cs` (register + `IHostedService` for first-run download)
- Create: `src/Fishbowl.Search.Tests/Fishbowl.Search.Tests.csproj`
- Create: `src/Fishbowl.Search.Tests/EmbeddingServiceTests.cs`

- [ ] **Step 1: Project setup**

Activate the `Fishbowl.Search` shell. NuGet: `Microsoft.ML.OnnxRuntime`, `Tokenizers.DotNet`.

- [ ] **Step 2: Write failing test**

- `EmbeddingService.EmbedAsync("hello world")` → float[] of length 384, L2-normalised (sum of squares ≈ 1).
- Same input → identical output (deterministic).
- Semantically similar inputs (`"cat"` vs `"kitten"`) score higher cosine-similarity than unrelated (`"cat"` vs `"motorcycle"`).

- [ ] **Step 3: Implement `MiniLmPipeline`**

Tokenizer: load `tokenizer.json` via `Tokenizers.DotNet`. Model: `InferenceSession` over `all-MiniLM-L6-v2.onnx`. Token limit 128, mean pooling over token embeddings, L2 normalise.

- [ ] **Step 4: Implement `ModelDownloader`**

`EnsureModelAsync(modelsDir, CancellationToken)`:
1. If both files exist in `fishbowl-data/models/`, no-op.
2. Else download from HuggingFace (`https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/...`) with SHA-256 verification against pinned hashes.
3. Log progress every ~2MB.

Wire as `IHostedService`: on startup, kick off download in background. `EmbeddingService.EmbedAsync` throws `EmbeddingUnavailableException` if model not yet present. Callers (NoteRepository on write, HybridSearchService on query) catch this and gracefully degrade (skip embedding / fall back to FTS-only).

- [ ] **Step 5: Settings action — "Re-index all"**

On the settings page, add a button that calls `POST /api/v1/search/reindex`. Endpoint iterates all notes in the user's context, regenerates and `INSERT OR REPLACE`s their vec_notes row. Status: simple progress (count done / total) polled by the UI.

- [ ] **Step 6: Run all tests**

- [ ] **Step 7: Manual test**

Delete `fishbowl-data/models/`, start the app. Observe logs: download progress. After completion, create a note. Click "re-index all" and confirm vec_notes populates.

- [ ] **Step 8: Commit**

Message: `feat(search): EmbeddingService + ModelDownloader with background init`

## Task 5.3: Embedding generation on note write

**Files:**
- Modify: `src/Fishbowl.Data/Repositories/NoteRepository.cs` (embed inside the existing transaction)

- [ ] **Step 1: Write failing test**

After `CreateAsync`, the context DB's `vec_notes` contains a row with `id = note.id` and a non-zero vector. After `UpdateAsync`, the row is replaced (distance to the new content's embedding is smaller than to the old).

- [ ] **Step 2: Implement**

`NoteRepository` gains a dependency on `IEmbeddingService`. In `CreateAsync` / `UpdateAsync`, after the notes_fts write (still inside the transaction):

```csharp
string textForEmbedding = SecretStripper.Strip(note.Content)
    + " " + note.Title
    + " " + string.Join(' ', note.Tags);

try
{
    var vec = await _embeddings.EmbedAsync(textForEmbedding, token);
    await db.ExecuteAsync(
        "INSERT OR REPLACE INTO vec_notes(id, embedding) VALUES (@id, @v)",
        new { id = note.Id, v = vec.ToSqliteVecBlob() },
        transaction: tx, cancellationToken: token);
}
catch (EmbeddingUnavailableException)
{
    _logger.LogDebug("Embeddings not yet available; {Id} will be indexed on next re-index.", note.Id);
}
```

On delete: `DELETE FROM vec_notes WHERE id = @id` before the notes delete (mirror the notes_fts pattern).

- [ ] **Step 3: Run all tests**

All existing NoteRepository tests must still pass — they don't check vec_notes but shouldn't break from the new writes. Add new assertions on vec_notes contents for the existing roundtrip tests.

- [ ] **Step 4: Manual test**

Create a note, query `vec_notes` directly via `sqlite3 users/{id}.db "SELECT id, vec_version() FROM vec_notes"` — should show the row.

- [ ] **Step 5: Commit**

Message: `feat(search): embed notes on write via NoteRepository transaction`

## Task 5.4: `HybridSearchService` + wire into `SearchMemoryTool`

**Files:**
- Create: `src/Fishbowl.Core/Search/ISearchService.cs`
- Create: `src/Fishbowl.Core/Models/MemorySearchResult.cs`
- Create: `src/Fishbowl.Search/HybridSearchService.cs`
- Modify: `src/Fishbowl.Mcp/Tools/SearchMemoryTool.cs`
- Create: `src/Fishbowl.Search.Tests/HybridSearchServiceTests.cs`

- [ ] **Step 1: Write failing test**

Seed a context with 20 notes. Query `"how do migrations work"` — a note titled "Lazy migration pattern" (exact-keyword-miss, semantic-hit) ranks in the top 5, even though it doesn't contain the word "how" or "work".

- [ ] **Step 2: Implement**

```csharp
public async Task<List<MemorySearchResult>> HybridSearchAsync(
    ContextRef ctx, string query, int limit, bool includePending, CancellationToken ct)
{
    using var db = _factory.CreateContextConnection(ctx);

    // 1. Vector search
    var vec = await _embeddings.EmbedAsync(query, ct);
    var vecHits = (await db.QueryAsync<(string Id, double Distance)>(
        "SELECT id, distance FROM vec_notes WHERE embedding MATCH @q AND k = 50 ORDER BY distance",
        new { q = vec.ToSqliteVecBlob() })).ToList();

    // 2. FTS
    var ftsHits = (await db.QueryAsync<(string Id, double Score)>(@"
        SELECT n.id, bm25(notes_fts) AS score FROM notes_fts
        JOIN notes n ON n.rowid = notes_fts.rowid
        WHERE notes_fts MATCH @q LIMIT 50",
        new { q = query })).ToList();

    // 3. Normalise + merge
    var merged = MergeScores(vecHits, ftsHits, vectorWeight: 0.7, ftsWeight: 0.3);

    // 4. Optionally filter out review:pending
    if (!includePending) merged = FilterPending(merged, db);

    // 5. Fetch top N full records
    return merged.Take(limit).Select(ToResult).ToList();
}
```

`MergeScores`: min-max normalise both lists (distance → similarity for vec), score = weighted sum. If embedding fails (`EmbeddingUnavailableException`), log and return FTS-only results with a `degraded: true` flag on the response.

- [ ] **Step 3: Wire into `SearchMemoryTool`**

Replace the FTS-only placeholder from Task 4.3 with `ISearchService.HybridSearchAsync`.

- [ ] **Step 4: Register**

`builder.Services.AddScoped<ISearchService, HybridSearchService>()`.

- [ ] **Step 5: Run all tests**

- [ ] **Step 6: Manual test**

From Claude Code, search semantic queries that wouldn't keyword-match. Spot-check ranking quality.

- [ ] **Step 7: Commit**

Message: `feat(search): HybridSearchService — 70% semantic + 30% FTS`

---

# Phase 6 — Review UI (~1 day)

Goal: a saved "Needs review" filter in the notes view with approve/reject actions, so Claude-origin memories surface for human review in a single place.

## Task 6.1: Needs-review filter + approve action

**Files:**
- Modify: `src/Fishbowl.Data/Resources/js/views/fb-notes-view.js`
- Create: `src/Fishbowl.Data/Resources/js/components/fb-review-actions.js`
- Modify: `src/Fishbowl.Data/Resources/index.html` (register component)
- Modify: `src/Fishbowl.Ui.Tests/` (new Playwright scenario)

- [ ] **Step 1: Design the filter**

In `fb-notes-view`, add a saved-filter chip "Needs review". When active, it sends `?tag=review:pending&match=all` to `/api/v1/notes`. Each note row renders the `<fb-review-actions>` component.

- [ ] **Step 2: Build `fb-review-actions`**

Shadow DOM component with three buttons:
- **Approve** → `PATCH /api/v1/notes/{id}/tags` (or equivalent — reuse an existing mechanism if one exists; otherwise `PUT /api/v1/notes/{id}` with the new tag set). Removes `review:pending`; default doesn't add `review:approved` (per open question in spec — keep minimal; revisit if wanted).
- **Edit** → dispatches a `fb-review:edit` event the view handles by opening the normal editor.
- **Reject** → uses `fb.dialog.confirm` with destructive variant → `DELETE /api/v1/notes/{id}`.

- [ ] **Step 3: Playwright scenario**

In `UiSmokeTests.cs`, add a scenario: seed a note with `review:pending` via test-user auth, navigate to `#/notes?filter=needs-review`, click Approve, confirm the note disappears from the filter. Requires an MCP-auto-tag path in the seeder; alternatively, POST a note through the test client with `review:pending` directly.

- [ ] **Step 4: Manual test**

From Claude Code: "Remember: this is a test memory." Switch to the browser. In the notes view, click "Needs review" — the note appears. Click Approve — it disappears from the filter, stays in the main list without `review:pending`.

- [ ] **Step 5: Commit**

Message: `feat(ui): needs-review filter + approve/reject actions for Claude-origin notes`

## Task 6.2: Documentation refresh

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update guidance**

Add sections:
- **Context resolution**: endpoints use `McpContextClaims.Resolve(user)` → `ContextRef`. Repositories take `ContextRef` overloads.
- **Auth**: Bearer and cookie coexist; Bearer carries a `(userId, contextId)` binding via claims.
- **System tags**: list the four, note that rename/delete are forbidden on `is_system=1`.
- **MCP surface**: list the five tools, note `delete_memory` is deliberately absent.
- **Secret-strip invariant**: every MCP response must round-trip through `SecretStripper`; there is a test that enforces this.
- **sqlite-vec**: binaries live in `Fishbowl.Data/Embedded/sqlite-vec/`; loader runs on every context open.

- [ ] **Step 2: Manual test**

Read the updated file top-to-bottom. If any section would confuse a fresh agent, rewrite it.

- [ ] **Step 3: Commit**

Message: `docs(claude): document MCP memory patterns + context resolution`

---

## Deferrals — tracked, not done

These items from the spec's *Out of scope for v1* are listed here so they don't get forgotten when this plan finishes:

- `delete_memory` MCP tool — wait for demonstrated need.
- Cross-context search (fanout across multiple context DBs).
- Team member invite / role management UI (only owner-create + owner-delete exist after this plan).
- MCP stdio transport (Claude Code uses HTTP).
- Rate limiting + IP allowlist on API keys.
- Token rotation (revoke + recreate today).
- `review:approved` permanence — configurable; current default strips on approve.
- Model upgrade flow (new embedding model invalidates old vectors — one-shot re-embed).

Each of these can become its own short spec when the need arises.
