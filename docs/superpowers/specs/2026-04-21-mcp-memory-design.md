# MCP Memory — Design

**Date:** 2026-04-21
**Status:** Proposed design; awaiting approval before an implementation plan is drafted.
**Scope:** Ship a Model Context Protocol (MCP) server endpoint backed by Fishbowl's existing notes substrate, so Claude Code (and future MCP clients) can store and retrieve project-scoped developer knowledge. Pulls forward roadmap items from v0.2 (Teams), v0.4 (API keys + Bearer auth), and v0.5 (MCP server) — plus wires up semantic search that was always part of v0.1.

## Why

Fishbowl's primary user (the maintainer, solo-hosted) is developing Fishbowl itself with Claude Code, and Claude Code is stateless between sessions. Without persistent memory, every conversation re-discovers the same project context, re-asks the same questions, and re-makes the same decisions. The official CONCEPT (§ *Open API & Agent Integration*) already specifies an MCP endpoint — this spec operationalises that vision earlier than the roadmap would naturally permit, because the product is its own most demanding user.

The design stays inside CONCEPT's boundaries: it does not invent a new entity type, a new DB shape, or a new auth model. It extends the patterns that already exist (notes, tags, FTS, user-DB-per-file, system.db for meta-data) and names things with the terms CONCEPT already uses ("context" for user/team space, "API key" for Bearer tokens, "space" for the owned store).

## Guiding principles

- **CONCEPT is the law.** Where CONCEPT says "this will be X", we build X — even if a slightly different shape would be easier.
- **Notes are the substrate.** Claude-written memories are notes. They participate in tags, FTS, embeddings, archive, pin — everything that already works for human-written notes. No parallel `memory_artifacts` table.
- **One context = one SQLite file.** Personal space = `users/{userId}.db`. Project space = `teams/{slug}.db`. The `DatabaseFactory` already knows how to do this — it just needs a second dimension.
- **Bearer token binds (user, context).** An API key is issued for a specific `(userId, contextId)` pair. The token *is* the context selector — individual MCP tool calls never take a context parameter.
- **Secrets are structurally absent.** `::secret` blocks are stripped before FTS and embeddings today; the MCP serializer re-applies the same stripping. No `secrets` scope exists in API keys. Not as a policy check — as a structural absence.
- **Review by tag, not by entity.** A reserved system tag `review:pending` marks freshly-written memories until the human approves them. System tags cannot be renamed or deleted.
- **No dead links or buttons.** If the review UI ships, it works end-to-end. If MCP tools are exposed, they return real data.

## Acceptance criteria

The MCP Memory feature is "done" when:

1. **API key management** exists in Settings UI: create named keys with scoped permissions, see list, revoke individually. Raw key shown exactly once at creation, hashed on disk.
2. **A team context can be created** for the current user (CLI-grade UI is enough for v1 — "New project space" button on the Settings page, with a slug input).
3. **`GET /api/v1/notes` etc. accept Bearer auth** and resolve to the context the token is bound to. Cookie auth continues to resolve to the personal context. Both codepaths hit the same endpoint logic.
4. **`POST /mcp` is live** with Streamable HTTP transport. It authenticates the same way — Bearer token in `Authorization` header, bound to `(userId, contextId)`.
5. **Five MCP tools work end-to-end** from Claude Code: `search_memory`, `remember`, `get_memory`, `list_pending`, `update_memory`. (Full list in § *MCP tool surface*.)
6. **System tags exist:** `review:pending`, `review:approved`, `source:claude`, `source:human`. They cannot be renamed or deleted through the Tags API. `source:*` is applied automatically on write; `review:pending` is applied automatically on writes from Bearer-authenticated clients.
7. **Hybrid search works:** `search_memory` does FTS5 + vector (70% semantic / 30% keyword per CONCEPT), over the context-scoped notes only. Secret content is absent from both indexes.
8. **Review queue renders** in the notes view: filter on `review:pending`, approve action swaps to `review:approved` or removes the tag (configurable default). Reject = delete.
9. **CI green** including new integration tests for: token auth on the REST API, MCP tool roundtrip, hybrid search relevance, system-tag-protection, and cross-context isolation (a token for team A cannot see team B notes).

## Total estimate

~14–18 working days across six phases. Individual phases are independently shippable — the feature grows in usefulness phase by phase.

---

## 1. Space model

CONCEPT already specifies exactly what we need (§ *Teams — Technical Details* and § *Apps*): personal data lives in `users/{userId}.db`, shared-context data lives in `teams/{slug}.db`, both with the identical fixed schema. The current `DatabaseFactory.CreateConnection(userId)` becomes a special case of a more general lookup.

### Disk layout

```
fishbowl-data/
  system.db                       ← users, user_mappings, teams, team_members, api_keys, system_config
  users/
    {userId}.db                   ← personal space (unchanged)
  teams/
    {slug}.db                     ← project / shared space (new)
  models/
    all-MiniLM-L6-v2.onnx         ← downloaded on first start
    tokenizer.json
```

### `DatabaseFactory` additions

Non-breaking: existing `CreateConnection(userId)` keeps working and opens the personal space. Two new methods:

```csharp
IDbConnection CreateContextConnection(ContextRef ctx);
Task WithContextTransactionAsync(ContextRef ctx, Func<...> work);
```

Where `ContextRef` is a discriminated shape:

```csharp
public readonly record struct ContextRef(ContextType Type, string Id);
public enum ContextType { User, Team }
```

Internally, `User → users/{Id}.db`, `Team → teams/{Id}.db`. The schema migration code runs identically against either file — it's the same schema. Team DBs get migrations v1 and v2 immediately on first open.

### `system.db` schema additions

Two new tables (schema v2 for `system.db`):

```sql
CREATE TABLE teams (
    id          TEXT PRIMARY KEY,        -- ULID
    slug        TEXT NOT NULL UNIQUE,    -- URL-safe, e.g. 'fishbowl-dev'
    name        TEXT NOT NULL,           -- human-readable, e.g. 'Fishbowl Development'
    created_by  TEXT NOT NULL REFERENCES users(id),
    created_at  TEXT NOT NULL
);

CREATE TABLE team_members (
    team_id    TEXT NOT NULL REFERENCES teams(id),
    user_id    TEXT NOT NULL REFERENCES users(id),
    role       TEXT NOT NULL CHECK(role IN ('owner','admin','member','readonly')),
    joined_at  TEXT NOT NULL,
    PRIMARY KEY (team_id, user_id)
);
```

Roles follow CONCEPT's table verbatim. For v1 we only enforce `readonly` vs not-readonly at the API-key level; full role enforcement arrives when human team members exist.

---

## 2. API keys & Bearer auth

CONCEPT § *API Keys* specifies the model: named keys, shown once, hashed at rest, revocable, scoped. We adopt it literally, with one addition (context binding).

### Storage

Third new table in `system.db` (still schema v2):

```sql
CREATE TABLE api_keys (
    id            TEXT PRIMARY KEY,           -- ULID
    user_id       TEXT NOT NULL REFERENCES users(id),
    context_type  TEXT NOT NULL CHECK(context_type IN ('user','team')),
    context_id    TEXT NOT NULL,              -- user_id for 'user', team_id for 'team'
    name          TEXT NOT NULL,              -- user-given label, e.g. "Claude Code fishbowl-dev"
    key_hash      TEXT NOT NULL,              -- SHA-256 of the raw token
    key_prefix    TEXT NOT NULL,              -- first 12 chars of the token for identification ('fb_live_abcd')
    scopes        TEXT NOT NULL,              -- JSON array of scope strings
    created_at    TEXT NOT NULL,
    last_used_at  TEXT,
    revoked_at    TEXT
);

CREATE INDEX idx_api_keys_prefix ON api_keys(key_prefix) WHERE revoked_at IS NULL;
```

### Token format

`fb_live_{22-char-base64url}` — matches CONCEPT's example format. Total length ~31 chars, prefix `fb_live_` marks live keys (later we may add `fb_test_` for test environments). The random component comes from `RandomNumberGenerator.GetBytes(16)` → base64url. Lookup uses the `key_prefix` index for O(log n) narrowing, then constant-time compare on `key_hash`.

### Scopes (v1)

```
read:notes      write:notes
read:tags       write:tags
read:tasks      write:tasks
read:events     write:events
```

No `secrets` scope. No `admin` scope. Scopes are an allowlist — missing = denied.

### Authentication middleware

New `ApiKeyAuthenticationHandler` registered alongside the existing cookie scheme. Dispatch is by header: `Authorization: Bearer fb_live_...` → API-key scheme, else cookie. Both schemes populate the same `ClaimsPrincipal` shape — in particular, both set `fishbowl_user_id`. API-key auth additionally sets `fishbowl_context_type` and `fishbowl_context_id` claims.

### Endpoint resolution

Every existing endpoint that reads `fishbowl_user_id` keeps working for cookie users. New helper:

```csharp
static ContextRef ResolveContext(ClaimsPrincipal user);
// Bearer path: reads context_type + context_id claims → (Team, teamId) or (User, userId)
// Cookie path: reads user_id claim → (User, userId)
```

Repositories that today take `string userId` gain a `ContextRef`-taking overload (thin adapter calling the factory's context-aware method). The `userId`-taking overload is kept for cookie-auth call sites to stay minimal-diff.

---

## 3. MCP endpoint

CONCEPT § *MCP Server* specifies URL `http://localhost:5000/mcp` (we're on 7180, same pattern). We implement the Streamable HTTP transport per the MCP spec March 2025 revision.

### Project placement

New project: `src/Fishbowl.Mcp/` — sits alongside `Fishbowl.Api`, references `Fishbowl.Core` + `Fishbowl.Data` + `Fishbowl.Search`. Wired into `Fishbowl.Host` the same way `Fishbowl.Api` is wired. Separate project because:
- MCP tool definitions are not HTTP endpoints — separate mental model, keep them apart.
- Lets future transports (stdio for local-only clients) live in the same project without polluting `Fishbowl.Api`.

### Transport

Streamable HTTP per MCP spec: client POSTs JSON-RPC messages to `/mcp`, server responds inline or upgrades to SSE for streaming responses. Auth is Bearer token — reuses `ApiKeyAuthenticationHandler` directly.

### MCP tool surface (v1)

Five tools. Names and arg shapes deliberately boring — AI agents work better with predictable APIs.

| Tool | Purpose | Scopes required |
|---|---|---|
| `search_memory` | Hybrid (FTS + vector) search; returns ranked results with snippets | `read:notes` |
| `remember` | Create a new note; auto-tagged `source:claude` + `review:pending` | `write:notes` |
| `get_memory` | Fetch full note by id | `read:notes` |
| `update_memory` | Update existing note; re-tags `review:pending` if content changed | `write:notes` |
| `list_pending` | List all notes tagged `review:pending` in this context | `read:notes` |

Notably absent: `delete_memory`. v1 doesn't let Claude delete. Rejection of pending memories is a human action in the UI — this is a deliberate Schneier's-law guardrail.

### Tool call sketch — `search_memory`

Input:
```json
{ "query": "how does DatabaseFactory handle migrations", "limit": 10, "include_pending": false }
```

Output:
```json
{
  "results": [
    {
      "id": "01HXYZ...",
      "title": "Lazy migrations via PRAGMA user_version",
      "snippet": "…each connection checks the version on open…",
      "tags": ["architecture", "source:human"],
      "score": 0.87,
      "updated_at": "2026-03-14T12:00:00Z"
    }
  ]
}
```

`include_pending` defaults to `false` so Claude doesn't feed its own unreviewed output back into subsequent queries. Explicit opt-in when wanted.

### Secret-strip invariant

Before any MCP tool returns a note, `::secret` block bytes are replaced with a fixed marker `[secret content hidden]` in `content`, and the `content_secret` blob is never serialised. The REST API already does this for notes fetches — same code path, same test coverage. Added invariant test: "a note with a secret block round-trips through every MCP tool without the secret plaintext appearing in the output."

---

## 4. System tags

Schema v3 for context DBs (both user and team, same migration):

```sql
ALTER TABLE tags ADD COLUMN is_system INTEGER NOT NULL DEFAULT 0;

-- Seed reserved names on migration:
INSERT OR IGNORE INTO tags(name, color, created_at, is_system)
VALUES
  ('review:pending',  '…', '…', 1),
  ('review:approved', '…', '…', 1),
  ('source:claude',   '…', '…', 1),
  ('source:human',    '…', '…', 1);
```

### Write path

`NoteRepository.CreateAsync` / `UpdateAsync` receive the calling `ClaimsPrincipal` (or a lightweight `source` hint). When the caller is Bearer-authenticated, the repo ensures `source:claude` and `review:pending` are in the note's tag set. When cookie-authenticated (a human editing in the UI), it ensures `source:human` and strips `review:pending`. Done in-transaction with the existing `_tags.EnsureExistsAsync` call.

### Tag API hardening

`RenameAsync` and `DeleteAsync` on `ITagRepository` gain a check: if `is_system = 1`, throw `ArgumentException("system tag cannot be renamed/deleted")`. The API maps this to 400 as it already does. Colors on system tags remain editable (that's just presentation).

### UI: review inbox

Notes view gains a saved filter "Needs review" on `review:pending`. Notes in this view render a small action bar: "Approve" (removes `review:pending`, optionally adds `review:approved`), "Edit" (opens the normal editor), "Reject" (opens the existing delete-confirm `fb-dialog`). No new component — reuses the existing notes list.

---

## 5. Embeddings & hybrid search

Already in v0.1 MVP scope per CONCEPT. We build exactly what CONCEPT specifies — no deviation, no alternatives.

### Model

`all-MiniLM-L6-v2` via ONNX Runtime, 384-dim L2-normalised, `Tokenizers.DotNet` tokenizer, 128-token limit. Downloaded to `fishbowl-data/models/` on first start. If absent at search time, hybrid search degrades to FTS-only (not an error — logged and noted in the response).

### Storage

Per CONCEPT, `sqlite-vec` extension loaded into every context DB:

```sql
CREATE VIRTUAL TABLE vec_notes USING vec0(
    id TEXT PRIMARY KEY,
    embedding FLOAT[384]
);
```

`sqlite-vec` ships as a native shared library. Loaded at `DatabaseFactory` startup via `SqliteConnection.EnableExtensions(true)` + `SELECT load_extension(...)`. Platform-specific binaries (`vec0.dll` / `libvec0.so` / `libvec0.dylib`) are embedded in `Fishbowl.Data.dll` as resources and extracted to a temp path on first use (same pattern other .NET projects use).

### Write path

On `NoteRepository.CreateAsync` / `UpdateAsync`, after the notes + notes_fts writes complete, the transaction also:
1. Strips `::secret` blocks from content.
2. Feeds title + tags + stripped content through the embedding pipeline (truncated/chunked to 128 tokens).
3. `INSERT OR REPLACE INTO vec_notes` with the resulting vector.

Embedding generation is synchronous in v1 — it's fast (~20ms for a note) and keeps the consistency story simple. If we hit latency issues, a follow-up can move this to a background queue.

### Search path

`ISearchService.HybridSearchAsync(contextRef, query, limit)`:
1. Embed the query.
2. Run vector search: `SELECT id, distance FROM vec_notes WHERE embedding MATCH ? ORDER BY distance LIMIT 50`.
3. Run FTS search: `SELECT id, bm25(...) AS score FROM notes_fts WHERE notes_fts MATCH ? LIMIT 50`.
4. Merge and rank: `final_score = 0.7 * (1 - normalized_vec_distance) + 0.3 * normalized_fts_score`.
5. Fetch full records for the top `limit` ids. Return with snippets.

All inside the context DB connection — no cross-DB fanout in v1. A future "search all my spaces" call is a fanout loop in `ISearchService`, but it's not on the v1 acceptance list.

---

## 6. Phases

Each phase is independently mergeable. Master stays deployable after every phase.

### Phase 1 — System tags (~1 day)

Purely additive to the tag work already in flight. Adds `is_system` column + seed data + Rename/Delete guardrails. No new APIs, no UI. Unblocks Phase 4's write path.

### Phase 2 — Team context infrastructure (~3 days)

`ContextRef` type, factory additions (`CreateContextConnection`, `WithContextTransactionAsync`), `teams` + `team_members` in `system.db` v2, minimal team-CRUD endpoints under `/api/v1/teams/` (cookie-auth, creator-owner-only), minimal UI in Settings (list, create, delete). No API keys yet — teams only accessible via cookie.

Repositories gain `ContextRef` overloads. Existing `userId`-taking overloads stay (cookie auth continues to use them). Tests: existing user-DB tests re-parametrised to run against both User and Team context.

### Phase 3 — API keys & Bearer auth (~3 days)

`api_keys` table, `ApiKeyAuthenticationHandler`, scope enforcement, Settings UI for key management. Reuse existing REST endpoints (`/api/v1/notes` etc.) with Bearer auth. After this phase: any MCP-capable or curl-capable tool can hit the REST API with a scoped key.

Acceptance: `curl -H "Authorization: Bearer fb_live_..." https://localhost:7180/api/v1/notes` returns the notes of the team the key is bound to.

### Phase 4 — MCP endpoint (~4 days)

`Fishbowl.Mcp` project, Streamable HTTP transport, five tools wired to existing repositories, secret-strip invariant tests, write-path auto-tagging (`source:claude` + `review:pending`). OpenAPI-style JSON schema published at `GET /mcp/tools` (debug aid).

Acceptance: `claude_desktop_config.json` entry for Fishbowl works; `search_memory`, `remember`, `get_memory` all functional in a real Claude Code session against a test team DB.

### Phase 5 — Hybrid search (~3 days)

`Fishbowl.Search` project activated (currently empty shell per CLAUDE.md), sqlite-vec loader, embedding service, `vec_notes` migration (user DB v3), embedding generation in the note write path, hybrid-search implementation in `search_memory`. Model download flow.

Acceptance: `search_memory` returns semantically relevant results for queries that don't keyword-match. Model download completes on first launch with a visible progress indicator.

### Phase 6 — Review UI (~1 day)

"Needs review" saved filter in the notes view, approve action, reject = delete. End-to-end manual test: Claude remembers something → shows in review inbox → user approves → tag updates.

### Out of scope for v1

- `delete_memory` MCP tool (humans review; deletion is a human action).
- Cross-context search (querying multiple spaces in one call).
- Team member management UI beyond owner-only (no invite, no role edits).
- MCP stdio transport (HTTP is enough for Claude Code today).
- Token rotation flow (revoke + recreate works for now).
- Rate limiting on API keys.
- Key-scoped IP allowlist (CONCEPT mentions it; not blocking for v1).
- Audit log of MCP writes (`source:claude` is visible on every note; full log deferred).
- "Project as App" — Apps have dynamic schemas; knowledge is fixed-schema notes. Don't conflate.

---

## Open questions

These are design decisions we deferred — they need an answer before or during implementation, but not before approving this spec.

1. **Embedding trigger for existing notes.** When `vec_notes` is introduced to an existing user DB, do we embed all existing notes eagerly (blocks first startup after upgrade) or lazily (vector rows appear as notes are touched)? Recommendation: lazy + a "re-embed all" button in settings.
2. **`review:approved` — keep it or strip it?** Two valid designs. "Keep" gives a permanent audit trail of Claude-origin approved notes. "Strip" keeps the tag cloud clean. Recommendation: strip by default, settings toggle to keep.
3. **Scope of `list_pending`.** Just titles + ids + timestamps (cheap), or with snippets (more useful but bigger payload)? Recommendation: titles + tags + timestamps, and clients call `get_memory` for full content.
4. **Model download UX in headless mode.** The current branding sees a terminal; a Raspberry Pi install doesn't. First-launch download should succeed silently and log progress rather than block a TTY prompt.
5. **Key-prefix length.** `fb_live_` + how many bytes? Choose once — changing later is annoying because existing keys grandfather in. Recommendation: 16 random bytes → 22 base64url chars, prefix `fb_live_` → total 30 chars.

---

## References

- `CONCEPT.md` § *Apps*, § *Teams*, § *Teams — Technical Details*, § *Open API & Agent Integration*, § *MCP Server*.
- `CLAUDE.md` § *Architecture* — for the existing DatabaseFactory / Program.cs / claim conventions.
- MCP spec — Streamable HTTP transport (2025-03 revision): https://spec.modelcontextprotocol.io/
- `docs/superpowers/specs/2026-04-19-ui-foundation-design.md` — for UI conventions the review inbox follows.
