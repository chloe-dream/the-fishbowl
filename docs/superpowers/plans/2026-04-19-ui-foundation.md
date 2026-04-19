# UI Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Fishbowl's ad-hoc UI with a full component library and view system adapted from the Dream Tools project, delivering a hub-and-tool-pages SPA.

**Architecture:** Single-page app with hash-based router inside `index.html`, served via the existing `IResourceProvider` (disk → DB → embedded). Pre-auth pages (`/login`, `/setup`) stay server-rendered with tokens-only styling. Every system component uses the `fb-` prefix and Shadow DOM; user mods use `usr_` and drop into `fishbowl-mods/`.

**Tech Stack:** Vanilla HTML + CSS + JavaScript (no framework, no build step). Web Components with Shadow DOM. Hash-based router. Google Fonts (Inter + Outfit). Playwright for the smoke test. xUnit v3 for the test project. Backend endpoint additions use ASP.NET Core Minimal APIs.

**Spec:** [`docs/superpowers/specs/2026-04-19-ui-foundation-design.md`](../specs/2026-04-19-ui-foundation-design.md)

**Reference source (adapt, don't copy verbatim):** `C:\Users\goosefx\SynologyDrive\PROJECTS\dream-tools\shared\web-components\*.js` and `C:\Users\goosefx\SynologyDrive\PROJECTS\dream-tools\ui.css`. Implementers may `Read` these files as templates. Adaptations per task below.

---

## File Structure

**New files:**

```
src/Fishbowl.Data/Resources/
  css/
    app.css                           [replaces existing index.css; Task 1.2]
  js/
    lib/
      globals.js                      [Task 1.3]
      icons.js                        [Task 1.3]
      router.js                       [Task 1.4]
      api.js                          [Task 1.4]
    components/
      fb-icon.js                      [Task 1.5]
      fb-footer.js                    [Task 1.5]
      fb-nav.js                       [Task 3.1]
      fb-section.js                   [Task 4.1]
      fb-toggle.js                    [Task 4.1]
      fb-segmented-control.js         [Task 4.1]
      fb-window.js                    [Task 5.1]
      fb-slider.js                    [Task 5.2]
      fb-hud.js                       [Task 5.2]
      fb-log.js                       [Task 5.3]
      fb-terminal.js                  [Task 5.3]
      fb-loader.js                    [Task 5.4]
    views/
      fb-hub-view.js                  [Task 3.2]
      fb-notes-view.js                [Task 4.2]
      fb-todos-view.js                [Task 4.3]

src/Fishbowl.Api/Endpoints/
  VersionApi.cs                       [Task 1.1]

src/Fishbowl.Ui.Tests/
  Fishbowl.Ui.Tests.csproj            [Task 4.4]
  UiSmokeTests.cs                     [Task 4.4]
  PlaywrightFixture.cs                [Task 4.4]

docs/
  ui-manual-test-checklist.md         [Task 5.5]
```

**Modified files:**

```
src/Fishbowl.Data/Resources/
  index.html                          [Task 3.3; becomes SPA shell]
  login.html                          [Task 2.1]
  setup.html                          [Task 2.2]
src/Fishbowl.Host/Program.cs          [Task 1.1 registers version endpoint; Task 3.3 may adjust fallback]
src/Fishbowl.Host.Tests/
  ApiIntegrationTests.cs              [Task 1.1 adds version-endpoint test]
.github/workflows/ci.yml              [Task 4.5 appends UI test job]
Fishbowl.sln                          [Task 4.4 adds Fishbowl.Ui.Tests project]
CLAUDE.md                             [Task 5.5 refreshes with UI conventions]
```

**Deleted files:**

```
src/Fishbowl.Data/Resources/
  css/index.css                       [Task 1.2 — superseded by app.css]
  js/app.js                           [Task 3.3 — behaviors move into view components]
```

---

# Phase 1 — Foundation (~1 day)

Goal: install the plumbing (backend version endpoint, design tokens, globals, icons, router, api, two simplest components). No visible UX change yet.

## Task 1.1: Version endpoint

**Files:**
- Create: `src/Fishbowl.Api/Endpoints/VersionApi.cs`
- Modify: `src/Fishbowl.Host/Program.cs`
- Modify: `src/Fishbowl.Host.Tests/ApiIntegrationTests.cs`

- [ ] **Step 1: Write the failing test**

Append to `src/Fishbowl.Host.Tests/ApiIntegrationTests.cs`:

```csharp
[Fact]
public async Task Get_Version_ReturnsVersionString_Test()
{
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/v1/version", TestContext.Current.CancellationToken);

    Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.True(body!.ContainsKey("version"));
    Assert.False(string.IsNullOrEmpty(body["version"]));
}
```

Add `using System.Net.Http.Json;` and `using System.Collections.Generic;` to the top if missing.

- [ ] **Step 2: Verify it fails**

Run: `dotnet test src/Fishbowl.Host.Tests --filter "FullyQualifiedName~Get_Version"`

Expected: 404 — endpoint doesn't exist yet.

- [ ] **Step 3: Create the endpoint module**

Create `src/Fishbowl.Api/Endpoints/VersionApi.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Fishbowl.Api.Endpoints;

public static class VersionApi
{
    public static RouteGroupBuilder MapVersionApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1");

        group.MapGet("/version", () =>
            Results.Ok(new { version = "0.1.0-alpha" }))
        .WithName("GetVersion")
        .WithSummary("Returns the running server version.")
        .Produces<object>();

        return group;
    }
}
```

- [ ] **Step 4: Wire into Program.cs**

In `src/Fishbowl.Host/Program.cs`, find the lines:

```csharp
app.MapNotesApi();
app.MapTodoApi();
```

Add directly above them:

```csharp
app.MapVersionApi();
```

- [ ] **Step 5: Run tests — expect pass**

Run: `dotnet test src/Fishbowl.Host.Tests`

Expected: all Host.Tests pass (16 now).

- [ ] **Step 6: Commit**

```bash
git add src/Fishbowl.Api/Endpoints/VersionApi.cs src/Fishbowl.Host/Program.cs src/Fishbowl.Host.Tests/ApiIntegrationTests.cs
git commit -m "feat: add GET /api/v1/version endpoint

Trivial stub returning { version: \"0.1.0-alpha\" }. The UI footer
will surface this value to the user.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 1.2: CSS foundation — tokens, base, orb, glass, tile

**Files:**
- Create: `src/Fishbowl.Data/Resources/css/app.css`
- Delete: `src/Fishbowl.Data/Resources/css/index.css`

- [ ] **Step 1: Read Dream Tools reference for layout classes**

Run: `Read` on `C:\Users\goosefx\SynologyDrive\PROJECTS\dream-tools\ui.css` (400 lines). Skim for: scrollbar styling, button classes (`.btn`, `.tool-btn`, `.btn.primary`, `.nav-icon-btn`), sidebar/main-layout rules.

- [ ] **Step 2: Create `app.css`**

Create `src/Fishbowl.Data/Resources/css/app.css` with the following. This is the full file — ~280 lines. Fishbowl-specific token additions beyond Dream Tools: `--accent-warm`, `--danger` split out from the generic red.

```css
/* ==========================================================================
   Fishbowl — App-wide styles
   Design tokens at :root. Shadow-DOM components inherit via CSS custom
   properties.
   ========================================================================== */

:root {
    --bg:           #0a0a0a;
    --panel:        #1e1e1e;
    --accent:       #3b82f6;
    --accent-edit:  #a855f7;
    --accent-warm:  #f97316;
    --danger:       #ef4444;
    --border:       rgba(255, 255, 255, 0.08);
    --text:         #f8fafc;
    --text-muted:   #64748b;
    --glass:        rgba(15, 23, 42, 0.7);
    --bg-dark:      #000000;
}

* { box-sizing: border-box; }

html, body {
    margin: 0;
    padding: 0;
    min-height: 100vh;
    background: var(--bg);
    color: var(--text);
    font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
    font-size: 14px;
    line-height: 1.5;
    -webkit-font-smoothing: antialiased;
}

h1, h2, h3, h4, h5, h6 {
    font-family: 'Outfit', sans-serif;
    font-weight: 700;
    letter-spacing: -0.02em;
    margin: 0;
}

a { color: var(--accent); text-decoration: none; }
a:hover { text-decoration: underline; }

button {
    font-family: inherit;
    cursor: pointer;
    border: none;
    background: none;
    color: inherit;
}

input, textarea {
    font-family: inherit;
    color: inherit;
}

/* Decorative background orb — used on hub, login, setup */
.orb {
    position: fixed;
    width: 50vw;
    height: 50vw;
    background: radial-gradient(circle, rgba(59, 130, 246, 0.05) 0%, transparent 70%);
    border-radius: 50%;
    top: -10vw;
    right: -10vw;
    z-index: -1;
    pointer-events: none;
}

/* Glassmorphic panel */
.glass {
    background: rgba(15, 23, 42, 0.85);
    backdrop-filter: blur(12px);
    -webkit-backdrop-filter: blur(12px);
    border: 1px solid rgba(255, 255, 255, 0.1);
}

/* Logo mark — blue top, orange bottom (goldfish) */
.fb-logo-mark {
    display: flex;
    flex-direction: column;
    gap: 2px;
    width: 14px;
    height: 20px;
}
.fb-logo-mark .top    { flex: 1; background: var(--accent);      border-radius: 10px 10px 0 0; }
.fb-logo-mark .bottom { flex: 1; background: var(--accent-warm); border-radius: 0 0 10px 10px; }

/* SPA mount point */
#app-root {
    padding-top: 50px; /* leave room for fixed <fb-nav> */
    min-height: 100vh;
}

/* Hub container */
.hub-container {
    max-width: 1400px;
    margin: 0 auto;
    padding: 4rem 2rem 2rem;
}
.hub-container header {
    text-align: center;
    margin-bottom: 3rem;
}
.hub-container h1 {
    font-size: clamp(2.5rem, 8vw, 4rem);
    font-weight: 800;
    background: linear-gradient(135deg, #fff 0%, var(--accent) 100%);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
    letter-spacing: -0.03em;
    margin-bottom: 0.5rem;
}
.hub-container .intro-text {
    color: var(--text-muted);
    font-size: 1.125rem;
}

/* Tile grid */
.grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
    grid-auto-rows: 280px;
    gap: 1.5rem;
}

.tile {
    position: relative;
    background: rgba(30, 41, 59, 0.7);
    backdrop-filter: blur(12px);
    -webkit-backdrop-filter: blur(12px);
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 24px;
    padding: 2rem;
    text-decoration: none;
    color: var(--text);
    display: flex;
    flex-direction: column;
    justify-content: flex-end;
    transition: all 0.4s cubic-bezier(0.175, 0.885, 0.32, 1.275);
    overflow: hidden;
    cursor: pointer;
}
.tile fb-icon {
    --icon-size: 48px;
    margin-bottom: auto;
    color: var(--accent);
    transition: transform 0.4s ease;
}
.tile h2 {
    font-size: 1.5rem;
    font-weight: 700;
    margin-bottom: 0.25rem;
}
.tile p {
    color: var(--text-muted);
    font-size: 0.9rem;
    margin: 0;
}
.tile:hover {
    transform: translateY(-8px) scale(1.02);
    border-color: var(--accent);
    box-shadow: 0 20px 40px rgba(0, 0, 0, 0.4), 0 0 20px rgba(59, 130, 246, 0.2);
}
.tile:hover fb-icon {
    transform: scale(1.1) rotate(5deg);
}

/* Tool-page layout */
.tool-layout {
    display: flex;
    height: calc(100vh - 50px);
    overflow: hidden;
}
.tool-sidebar {
    width: 260px;
    background: var(--panel);
    border-right: 1px solid var(--border);
    padding: 1.5rem 1rem;
    overflow-y: auto;
    flex-shrink: 0;
}
.tool-main {
    flex: 1;
    display: flex;
    overflow: hidden;
}

/* Custom scrollbar (for sidebars and list panes) */
.tool-sidebar::-webkit-scrollbar,
.list-pane::-webkit-scrollbar {
    width: 6px;
}
.tool-sidebar::-webkit-scrollbar-thumb,
.list-pane::-webkit-scrollbar-thumb {
    background: rgba(255, 255, 255, 0.1);
    border-radius: 3px;
}
.tool-sidebar::-webkit-scrollbar-thumb:hover,
.list-pane::-webkit-scrollbar-thumb:hover {
    background: rgba(255, 255, 255, 0.2);
}

/* Base button classes (used inside Shadow DOM too via :host-context support) */
.btn {
    padding: 0.5rem 1rem;
    border-radius: 8px;
    background: var(--glass);
    color: var(--text);
    border: 1px solid var(--border);
    font-size: 0.875rem;
    transition: all 0.2s;
}
.btn:hover {
    background: rgba(255, 255, 255, 0.08);
    border-color: rgba(255, 255, 255, 0.2);
}
.btn.primary {
    background: var(--accent);
    border-color: var(--accent);
    color: white;
}
.btn.primary:hover {
    background: #2563eb;
    border-color: #2563eb;
}
.btn.danger {
    background: var(--danger);
    border-color: var(--danger);
    color: white;
}

/* Centered pre-auth card (login + setup) */
.auth-shell {
    min-height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 2rem;
}
.auth-card {
    width: 100%;
    max-width: 440px;
    background: rgba(30, 41, 59, 0.7);
    backdrop-filter: blur(12px);
    -webkit-backdrop-filter: blur(12px);
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 24px;
    padding: 3rem 2.5rem;
    text-align: center;
}
.auth-card .fb-logo-mark {
    width: 28px;
    height: 40px;
    margin: 0 auto 1rem;
}
.auth-card h1 {
    font-family: 'Outfit', sans-serif;
    font-size: 2rem;
    font-weight: 800;
    letter-spacing: -0.02em;
    background: linear-gradient(135deg, #fff 0%, var(--accent) 100%);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
    margin-bottom: 0.5rem;
}
.auth-card .tagline {
    color: var(--text-muted);
    margin-bottom: 2rem;
    font-size: 0.95rem;
}
.auth-card .form-group {
    text-align: left;
    margin-bottom: 1rem;
}
.auth-card label {
    display: block;
    font-size: 0.8rem;
    color: var(--text-muted);
    margin-bottom: 0.3rem;
    text-transform: uppercase;
    letter-spacing: 0.05em;
}
.auth-card input[type="text"],
.auth-card input[type="password"] {
    width: 100%;
    padding: 0.6rem 0.8rem;
    background: rgba(0, 0, 0, 0.3);
    border: 1px solid var(--border);
    border-radius: 8px;
    font-size: 0.95rem;
    transition: border-color 0.2s;
}
.auth-card input:focus {
    outline: none;
    border-color: var(--accent);
}
.auth-card .providers {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
}
.auth-card .provider-btn {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 0.6rem;
    padding: 0.75rem 1rem;
    background: rgba(255, 255, 255, 0.05);
    border: 1px solid var(--border);
    border-radius: 10px;
    color: var(--text);
    font-size: 0.95rem;
    transition: all 0.2s;
}
.auth-card .provider-btn:hover {
    background: rgba(255, 255, 255, 0.1);
    border-color: var(--accent);
}
.auth-card .error {
    color: var(--danger);
    font-size: 0.85rem;
    margin-top: 0.5rem;
}
```

- [ ] **Step 3: Delete the old `index.css`**

Run: `rm src/Fishbowl.Data/Resources/css/index.css`

- [ ] **Step 4: Verify build + tests still pass**

Run: `dotnet test Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: all tests pass. The old CSS is unused by any test; the new file is embedded via the existing `<EmbeddedResource Include="Resources\**\*" />` glob.

- [ ] **Step 5: Commit**

```bash
git add src/Fishbowl.Data/Resources/css/app.css
git rm src/Fishbowl.Data/Resources/css/index.css
git commit -m "feat: replace index.css with app.css (design tokens + base styles)

New tokens: --accent-warm (goldfish orange), --danger (red reserved
for destructive). Adds .orb, .glass, .fb-logo-mark, .tile, .grid,
.tool-layout, .auth-shell/.auth-card classes used throughout the
new UI.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 1.3: Globals and icon dictionary

**Files:**
- Create: `src/Fishbowl.Data/Resources/js/lib/globals.js`
- Create: `src/Fishbowl.Data/Resources/js/lib/icons.js`

- [ ] **Step 1: Create `globals.js`**

Create `src/Fishbowl.Data/Resources/js/lib/globals.js`:

```js
/**
 * Fishbowl — global namespace.
 * Loaded first. Populated incrementally by other lib scripts.
 *
 * Mods use window.fb to interact with the system without rebuilding
 * a component (e.g. fb.icons.register, fb.router.navigate).
 */
(function () {
    if (window.fb) return; // idempotent
    window.fb = {
        version: null,                  // populated by /api/v1/version fetch
        api:     null,                  // populated by api.js
        router:  null,                  // populated by router.js
        icons:   null                   // populated by icons.js
    };
})();
```

- [ ] **Step 2: Create `icons.js` with the default dictionary**

Create `src/Fishbowl.Data/Resources/js/lib/icons.js`. This defines the curated ~40 icon set. All paths use `viewBox="0 0 24 24"`, `stroke="currentColor"`, `stroke-width="2"`, `stroke-linecap="round"`, `stroke-linejoin="round"`, `fill="none"` (the `<fb-icon>` component wraps these path strings in the SVG envelope).

Icons with heavy paths (`fish`, `bowl`, etc.) use simplified geometry; if an icon looks wrong in practice, replace its path string — the contract is just a map.

```js
/**
 * Fishbowl — icon registry.
 *
 * Each entry is an SVG path-element string assumed to live inside
 *   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor"
 *        stroke-width="2" stroke-linecap="round" stroke-linejoin="round">...</svg>
 *
 * Mods can call fb.icons.register("my-icon", "<path d='...'/>") to add entries
 * or override existing ones.
 */
(function () {
    const registry = new Map();

    const defaults = {
        // brand
        "fish":          '<path d="M3 12c4-5 9-5 13 0-4 5-9 5-13 0z"/><path d="M16 12l4-2v4l-4-2z"/><circle cx="8" cy="11" r="0.5" fill="currentColor"/>',
        "bowl":          '<path d="M4 10h16v8a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2v-8z"/><path d="M3 10h18"/><path d="M8 6v4"/><path d="M16 6v4"/>',
        // content
        "note":          '<rect x="4" y="3" width="16" height="18" rx="2"/><path d="M8 8h8"/><path d="M8 12h8"/><path d="M8 16h5"/>',
        "pencil":        '<path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 1 1 3 3L7 19l-4 1 1-4 12.5-12.5z"/>',
        "check":         '<polyline points="20 6 9 17 4 12"/>',
        "plus":          '<line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>',
        "trash":         '<polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>',
        "search":        '<circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>',
        "tag":           '<path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"/><line x1="7" y1="7" x2="7.01" y2="7"/>',
        "pin":           '<path d="M12 17v5"/><path d="M9 10.76V6a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v4.76l2.24 2.24H6.76L9 10.76z"/>',
        "archive":       '<polyline points="21 8 21 21 3 21 3 8"/><rect x="1" y="3" width="22" height="5"/><line x1="10" y1="12" x2="14" y2="12"/>',
        // secrets + security
        "lock":          '<rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/>',
        "unlock":        '<rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 9.9-1"/>',
        "key":           '<path d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0l3 3L22 7l-3-3m-3.5 3.5L19 4"/>',
        "eye":           '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>',
        "eye-off":       '<path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/>',
        // calendar + time
        "calendar":      '<rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>',
        "clock":         '<circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>',
        // contacts + docs
        "contact":       '<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>',
        "document":      '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/>',
        "attachment":    '<path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48"/>',
        // system
        "settings":      '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/>',
        "backup":        '<path d="M3 12v7a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><polyline points="8 8 12 4 16 8"/><line x1="12" y1="4" x2="12" y2="16"/>',
        "sync":          '<polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/>',
        "cloud":         '<path d="M18 10h-1.26A8 8 0 1 0 9 20h9a5 5 0 0 0 0-10z"/>',
        "download":      '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/>',
        "upload":        '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/>',
        // nav + controls
        "menu":          '<line x1="3" y1="6" x2="21" y2="6"/><line x1="3" y1="12" x2="21" y2="12"/><line x1="3" y1="18" x2="21" y2="18"/>',
        "close":         '<line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>',
        "chevron-right": '<polyline points="9 18 15 12 9 6"/>',
        "chevron-left":  '<polyline points="15 18 9 12 15 6"/>',
        "chevron-down":  '<polyline points="6 9 12 15 18 9"/>',
        "chevron-up":    '<polyline points="18 15 12 9 6 15"/>',
        "home":          '<path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/>',
        "star":          '<polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/>',
        "copy":          '<rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>',
        "link":          '<path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/>',
        "external-link": '<path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" y1="14" x2="21" y2="3"/>',
        // status
        "info":          '<circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/>',
        "warn":          '<path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/>',
        "error":         '<circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/>',
        "success":       '<path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/>',
        // external
        "github":        '<path d="M9 19c-5 1.5-5-2.5-7-3m14 6v-3.87a3.37 3.37 0 0 0-.94-2.61c3.14-.35 6.44-1.54 6.44-7A5.44 5.44 0 0 0 20 4.77 5.07 5.07 0 0 0 19.91 1S18.73.65 16 2.48a13.38 13.38 0 0 0-7 0C6.27.65 5.09 1 5.09 1A5.07 5.07 0 0 0 5 4.77a5.44 5.44 0 0 0-1.5 3.78c0 5.42 3.3 6.61 6.44 7A3.37 3.37 0 0 0 9 18.13V22"/>'
    };

    Object.entries(defaults).forEach(([name, path]) => registry.set(name, path));

    fb.icons = {
        register(name, pathString) {
            registry.set(name, pathString);
        },
        get(name) {
            return registry.get(name) || null;
        },
        has(name) {
            return registry.has(name);
        }
    };
})();
```

- [ ] **Step 2: Verify files load (build passes)**

Run: `dotnet build Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: build succeeds. Files are embedded via the `Resources\**\*` glob — no csproj change needed.

- [ ] **Step 3: Commit**

```bash
git add src/Fishbowl.Data/Resources/js/lib/globals.js src/Fishbowl.Data/Resources/js/lib/icons.js
git commit -m "feat: add window.fb namespace and icon registry

globals.js sets up window.fb (populated incrementally by subsequent
lib scripts). icons.js registers ~40 curated SVG paths and exposes
fb.icons.register(name, path) for mod extension.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 1.4: Router and API wrapper

**Files:**
- Create: `src/Fishbowl.Data/Resources/js/lib/router.js`
- Create: `src/Fishbowl.Data/Resources/js/lib/api.js`

- [ ] **Step 1: Create `router.js`**

```js
/**
 * Fishbowl — hash-based SPA router.
 * Views are custom elements registered via fb.router.register("#/hash", "tag-name").
 * On hashchange, the root mount point's innerHTML is swapped to the matching tag.
 */
(function () {
    const routes = new Map();    // "#/notes" → "fb-notes-view"
    let rootElement = null;

    function currentHash() {
        return window.location.hash || "#/";
    }

    function render() {
        if (!rootElement) return;
        const hash = currentHash();
        const tag = routes.get(hash) || routes.get("#/");
        if (!tag) {
            rootElement.innerHTML = "";
            return;
        }
        // Clear + remount. Views are self-destructing: the browser disconnects
        // the removed element and connects the new one.
        rootElement.innerHTML = `<${tag}></${tag}>`;
    }

    fb.router = {
        register(hash, tagName) {
            routes.set(hash, tagName);
            // If mount() already happened and this registration matches the
            // current hash, render immediately (handles late-loading views).
            if (rootElement && currentHash() === hash) render();
        },
        routes() {
            return Array.from(routes.entries()); // [[hash, tag], ...]
        },
        current() {
            return currentHash();
        },
        navigate(hash) {
            window.location.hash = hash;
        },
        mount(selector) {
            rootElement = document.querySelector(selector);
            if (!rootElement) {
                console.error(`[fb.router] mount: no element matches ${selector}`);
                return;
            }
            window.addEventListener("hashchange", render);
            render();
        }
    };
})();
```

- [ ] **Step 2: Create `api.js`**

```js
/**
 * Fishbowl — fetch wrapper for /api/v1/*.
 * 401 responses redirect to /login. Non-OK responses throw ApiError.
 */
(function () {
    const base = "/api/v1";

    class ApiError extends Error {
        constructor(status, body) {
            super(`API error ${status}: ${body}`);
            this.status = status;
            this.body = body;
        }
    }

    async function request(path, options = {}) {
        const res = await fetch(base + path, {
            headers: { "Content-Type": "application/json", ...(options.headers || {}) },
            ...options
        });
        if (res.status === 401) {
            window.location.href = "/login";
            // Throw so callers' await chains don't continue as if success.
            throw new ApiError(401, "Unauthenticated");
        }
        if (!res.ok) {
            const body = await res.text().catch(() => "");
            throw new ApiError(res.status, body);
        }
        if (res.status === 204) return undefined;
        const contentType = res.headers.get("content-type") || "";
        if (contentType.includes("application/json")) return res.json();
        return res.text();
    }

    const crud = (resource) => ({
        list:   ()        => request(`/${resource}`),
        get:    (id)      => request(`/${resource}/${encodeURIComponent(id)}`),
        create: (body)    => request(`/${resource}`,                      { method: "POST",   body: JSON.stringify(body) }),
        update: (id, body) => request(`/${resource}/${encodeURIComponent(id)}`, { method: "PUT",    body: JSON.stringify(body) }),
        delete: (id)      => request(`/${resource}/${encodeURIComponent(id)}`, { method: "DELETE" })
    });

    fb.api = {
        notes: crud("notes"),
        todos: crud("todos"),
        version: () => request("/version"),
        providers: () => fetch("/api/auth/providers").then(r => r.json())
    };
    fb.ApiError = ApiError;
})();
```

- [ ] **Step 3: Verify build**

Run: `dotnet build Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/Fishbowl.Data/Resources/js/lib/router.js src/Fishbowl.Data/Resources/js/lib/api.js
git commit -m "feat: add hash router and API client

router.js — minimal hashchange-based router. Views register via
fb.router.register(hash, tagName); fb.router.mount(selector) wires it
to a DOM mount point.

api.js — thin fetch wrapper exposing fb.api.{notes,todos}.{list,get,
create,update,delete}, fb.api.version(), and fb.api.providers().
401s redirect to /login; non-OK responses throw ApiError.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 1.5: `<fb-icon>` and `<fb-footer>` components

**Files:**
- Create: `src/Fishbowl.Data/Resources/js/components/fb-icon.js`
- Create: `src/Fishbowl.Data/Resources/js/components/fb-footer.js`

- [ ] **Step 1: Read Dream Tools reference**

Run: `Read` on `C:\Users\goosefx\SynologyDrive\PROJECTS\dream-tools\shared\web-components\icon.js` (58 lines).

Key adaptations:
- Rename tag `dream-icon` → `fb-icon`.
- Read icon paths from `fb.icons.get(name)` (not a local ICONS const).
- Default `--icon-size` stays `24px`.

- [ ] **Step 2: Create `fb-icon.js`**

```js
/**
 * <fb-icon name="...">
 *
 * Inline SVG icon. Resolves `name` against fb.icons registry (icons.js).
 * Size via CSS custom property --icon-size (default 24px).
 * Color via currentColor (inherits from parent).
 *
 * Attributes:
 *   name — lookup key in fb.icons registry.
 *
 * Example:
 *   <fb-icon name="note"></fb-icon>
 *   <fb-icon name="cube" style="--icon-size: 48px;"></fb-icon>
 */
class FbIcon extends HTMLElement {
    static get observedAttributes() { return ["name"]; }

    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    connectedCallback() { this.render(); }
    attributeChangedCallback() { this.render(); }

    render() {
        const name = this.getAttribute("name");
        const path = (window.fb && fb.icons) ? fb.icons.get(name) : null;

        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    display: inline-flex;
                    width: var(--icon-size, 24px);
                    height: var(--icon-size, 24px);
                    vertical-align: middle;
                }
                svg {
                    width: 100%;
                    height: 100%;
                    display: block;
                }
            </style>
            ${path
                ? `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor"
                        stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                     ${path}
                   </svg>`
                : ``}
        `;
    }
}

customElements.define("fb-icon", FbIcon);
```

- [ ] **Step 3: Create `fb-footer.js`**

Read `C:\Users\goosefx\SynologyDrive\PROJECTS\dream-tools\shared\web-components\footer.js` for reference. Adaptations:
- Rename tag.
- Remove Discord / X social icons.
- Show `fb.version` + optional GitHub link (only if `fb.system.github_url` is set, which it isn't yet — so the link is hidden until the backend learns this config).

```js
/**
 * <fb-footer>
 *
 * Minimal footer: version string + optional GitHub link.
 * The GitHub link only appears when window.fb.system?.githubUrl is set
 * (not yet wired — honors the "no dead links" rule).
 *
 * Auto-fetches the version from /api/v1/version on connect.
 */
class FbFooter extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    async connectedCallback() {
        this.render("loading...");
        try {
            const v = await fb.api.version();
            fb.version = v?.version ?? "unknown";
            this.render(fb.version);
        } catch {
            this.render("offline");
        }
    }

    render(versionText) {
        const githubUrl = window.fb?.system?.githubUrl ?? null;
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    display: block;
                    text-align: center;
                    padding: 1.5rem 1rem;
                    color: var(--text-muted, #64748b);
                    font-size: 0.75rem;
                    letter-spacing: 0.05em;
                    text-transform: uppercase;
                    border-top: 1px solid var(--border, rgba(255,255,255,0.08));
                    margin-top: 4rem;
                }
                a {
                    color: inherit;
                    text-decoration: none;
                    margin-left: 0.5rem;
                }
                a:hover { color: var(--accent, #3b82f6); }
                .version { opacity: 0.7; }
            </style>
            <span>THE FISHBOWL</span>
            <span class="version"> · v${versionText}</span>
            ${githubUrl ? `<a href="${githubUrl}" target="_blank" rel="noopener">GITHUB</a>` : ``}
        `;
    }
}

customElements.define("fb-footer", FbFooter);
```

- [ ] **Step 4: Verify build passes**

Run: `dotnet build Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/Fishbowl.Data/Resources/js/components/fb-icon.js src/Fishbowl.Data/Resources/js/components/fb-footer.js
git commit -m "feat: add <fb-icon> and <fb-footer> web components

<fb-icon name=\"...\"> resolves against fb.icons registry, renders
inline SVG. Size via --icon-size CSS custom property.

<fb-footer> fetches /api/v1/version on connect, shows a minimal
strip with version + optional GitHub link (hidden until configured,
per the no-dead-links principle).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

# Phase 2 — Login + setup polish (~1 day)

Goal: first visible change. Pre-auth pages look like part of the product.

## Task 2.1: Rebuild `login.html`

**Files:**
- Modify: `src/Fishbowl.Data/Resources/login.html`

- [ ] **Step 1: Read existing to preserve logic**

Run: `Read` on `src/Fishbowl.Data/Resources/login.html` — note any existing JS that fetches `/api/auth/providers`, form actions, etc.

- [ ] **Step 2: Rewrite `login.html`**

Replace entire file contents:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Login · The Fishbowl</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&family=Outfit:wght@600;700;800&display=swap" rel="stylesheet">
    <link rel="stylesheet" href="/css/app.css">
</head>
<body>
    <div class="orb"></div>

    <div class="auth-shell">
        <div class="auth-card">
            <div class="fb-logo-mark">
                <div class="top"></div>
                <div class="bottom"></div>
            </div>
            <h1>THE FISHBOWL</h1>
            <p class="tagline">Your memory lives here. You don't.</p>

            <div class="providers" id="providers">
                <!-- Populated by JS -->
            </div>
            <p id="error" class="error" style="display:none;"></p>
        </div>
    </div>

    <script>
    (async function () {
        const providersEl = document.getElementById("providers");
        const errorEl = document.getElementById("error");

        try {
            const res = await fetch("/api/auth/providers");
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const providers = await res.json();

            if (!providers || providers.length === 0) {
                // Unconfigured — go to setup
                window.location.href = "/setup";
                return;
            }

            providersEl.innerHTML = providers.map(p => `
                <a class="provider-btn" href="/login/challenge/${p.id}">
                    <span>Continue with ${p.name}</span>
                </a>
            `).join("");
        } catch (err) {
            errorEl.textContent = "Unable to load providers: " + err.message;
            errorEl.style.display = "block";
        }
    })();
    </script>
</body>
</html>
```

- [ ] **Step 3: Run all existing tests**

Run: `dotnet test Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: all tests pass (`AuthBehaviorTests.GetLogin_ReturnsOk_Page_Test` asserts `Assert.Contains("The Fishbowl", content)` — our new page contains `THE FISHBOWL` which matches case-insensitively... wait, `Assert.Contains` is case-sensitive. Check the existing assertion carefully.

If the existing assertion uses `"The Fishbowl"` (with that exact casing) and the new page uses `THE FISHBOWL`, the test will fail. If it fails, update the test assertion in `AuthBehaviorTests.cs`:

```csharp
Assert.Contains("THE FISHBOWL", content);
```

- [ ] **Step 4: Commit**

```bash
git add src/Fishbowl.Data/Resources/login.html src/Fishbowl.Host.Tests/AuthBehaviorTests.cs
git commit -m "feat: rebuild login.html with design tokens

Centered glass card with logo mark, gradient title, auto-populated
provider list (redirects to /setup when empty). No <fb-nav>.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 2.2: Rebuild `setup.html`

**Files:**
- Modify: `src/Fishbowl.Data/Resources/setup.html`

- [ ] **Step 1: Read existing to preserve the form contract**

Run: `Read` on `src/Fishbowl.Data/Resources/setup.html` — note field names, the POST target (`/api/setup`), and any existing validation.

- [ ] **Step 2: Rewrite `setup.html`**

Replace entire file contents:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Setup · The Fishbowl</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&family=Outfit:wght@600;700;800&display=swap" rel="stylesheet">
    <link rel="stylesheet" href="/css/app.css">
</head>
<body>
    <div class="orb"></div>

    <div class="auth-shell">
        <div class="auth-card">
            <div class="fb-logo-mark">
                <div class="top"></div>
                <div class="bottom"></div>
            </div>
            <h1>WELCOME</h1>
            <p class="tagline">Let's configure Google sign-in to get started.</p>

            <form id="setup-form">
                <div class="form-group">
                    <label for="clientId">Google Client ID</label>
                    <input type="text"
                           id="clientId"
                           name="clientId"
                           placeholder="xxxxx.apps.googleusercontent.com"
                           required>
                </div>
                <div class="form-group">
                    <label for="clientSecret">Google Client Secret</label>
                    <input type="password"
                           id="clientSecret"
                           name="clientSecret"
                           placeholder="at least 20 characters"
                           required>
                </div>
                <button type="submit" class="btn primary" style="width:100%; margin-top:1rem;">
                    Save &amp; Continue
                </button>
                <p id="error" class="error" style="display:none;"></p>
            </form>
        </div>
    </div>

    <script>
    (function () {
        const form = document.getElementById("setup-form");
        const errorEl = document.getElementById("error");

        function showError(msg) {
            errorEl.textContent = msg;
            errorEl.style.display = "block";
        }

        form.addEventListener("submit", async (e) => {
            e.preventDefault();
            errorEl.style.display = "none";

            const clientId = document.getElementById("clientId").value.trim();
            const clientSecret = document.getElementById("clientSecret").value;

            // Client-side mirrors server-side rules from Task 4.2 of A+ hardening.
            if (!clientId.endsWith(".apps.googleusercontent.com")) {
                showError("Client ID must end with .apps.googleusercontent.com");
                return;
            }
            if (clientSecret.length < 20) {
                showError("Client Secret must be at least 20 characters.");
                return;
            }

            try {
                const res = await fetch("/api/setup", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ clientId, clientSecret })
                });
                if (!res.ok) {
                    const body = await res.text();
                    showError("Server rejected: " + body);
                    return;
                }
                window.location.href = "/";
            } catch (err) {
                showError("Request failed: " + err.message);
            }
        });
    })();
    </script>
</body>
</html>
```

- [ ] **Step 3: Run all tests**

Run: `dotnet test Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: all tests pass. `SetupFlowTests` hits `/api/setup` (JSON body), which the new form still submits. `Setup_Returns404_WhenConfigured_Test` / `PostSetup_Rejects_*_Test` are unaffected by the HTML change.

- [ ] **Step 4: Commit**

```bash
git add src/Fishbowl.Data/Resources/setup.html
git commit -m "feat: rebuild setup.html with design tokens

Centered glass card; client-side validation mirrors server rules
(ClientId suffix + min secret length). Submits JSON to POST /api/setup,
redirects to / on success.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

# Phase 3 — Hub + nav (~2 days)

Goal: new `index.html` shell, `<fb-nav>`, hub view with tiles. After this phase the running app is visibly the new UI.

## Task 3.1: `<fb-nav>` component

**Files:**
- Create: `src/Fishbowl.Data/Resources/js/components/fb-nav.js`

- [ ] **Step 1: Read Dream Tools reference**

Run: `Read` on `C:\Users\goosefx\SynologyDrive\PROJECTS\dream-tools\shared\web-components\navigation.js` (396 lines). This is the largest port. Study:
- Ribbon layout (left: logo mark + brand + app name; center/right: toolbar slot).
- Slide-out panel: `transform: translateX(-110%)` → `translateX(0)`, 0.5s cubic-bezier.
- Active-item styling (`rgba(59,130,246,0.15)` background + accent border).
- `toolbar` named slot.

Adaptations:
- Tag: `fb-nav`.
- Brand text: "THE FISHBOWL" + ` · ${app-name}` when the attribute is set.
- Logo mark: use `.fb-logo-mark` (blue top, orange bottom) inside the ribbon, not the Dream Tools variant.
- Slide-out nav list: **computed from `fb.router.routes()`** (not a hardcoded list). Each route shows an `<fb-icon>` + a human label. Label comes from a second registration argument; extend `fb.router.register(hash, tagName, label, iconName)` to accept these. **Update router.js accordingly** — see Step 3 below.
- **No footer inside the panel** (no social links, no KinkyDevs logo). The panel is just the nav list.
- Close-on-backdrop-click + close-on-Esc.
- `isActive(hash)` → compares to `fb.router.current()`.

- [ ] **Step 2: Extend `router.js` to accept label/icon**

Update `src/Fishbowl.Data/Resources/js/lib/router.js`'s `register` + `routes` methods:

```js
// In the IIFE, change:
const routes = new Map();    // "#/notes" → { tag, label, icon }
```

And update `register`:

```js
register(hash, tagName, options = {}) {
    routes.set(hash, {
        tag: tagName,
        label: options.label || tagName,
        icon:  options.icon  || null
    });
    if (rootElement && currentHash() === hash) render();
},
routes() {
    return Array.from(routes.entries()).map(([hash, info]) => ({ hash, ...info }));
},
```

And update `render` to read `.tag`:

```js
function render() {
    if (!rootElement) return;
    const hash = currentHash();
    const entry = routes.get(hash) || routes.get("#/");
    if (!entry) { rootElement.innerHTML = ""; return; }
    rootElement.innerHTML = `<${entry.tag}></${entry.tag}>`;
}
```

- [ ] **Step 3: Create `fb-nav.js`**

Write `src/Fishbowl.Data/Resources/js/components/fb-nav.js`. Use the Dream Tools `navigation.js` as a starting template, applying the adaptations above. Key structure:

```js
/**
 * <fb-nav app-name="NOTES">
 *   <button slot="toolbar">...</button>
 * </fb-nav>
 *
 * Fixed 50px ribbon with glassmorphic background + slide-out 300px panel.
 * Panel nav list is computed from fb.router.routes().
 *
 * Attributes:
 *   app-name — uppercase text after "THE FISHBOWL ·" in the brand area.
 *
 * Slots:
 *   toolbar — right-aligned content inside the ribbon.
 */
class FbNav extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: "open" });
        this.isOpen = false;
    }

    connectedCallback() {
        this.render();
        this.attachHandlers();
    }

    render() {
        const appName = this.getAttribute("app-name") || "";
        const routes = (fb.router?.routes() || []).filter(r => r.hash !== "#/");
        const current = fb.router?.current() || "#/";

        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    position: fixed;
                    top: 0; left: 0; right: 0;
                    z-index: 9999;
                    --accent: #3b82f6;
                    --accent-warm: #f97316;
                }
                .ribbon {
                    height: 50px;
                    display: flex;
                    align-items: center;
                    padding: 0 1rem;
                    background: rgba(15, 23, 42, 0.85);
                    backdrop-filter: blur(12px);
                    -webkit-backdrop-filter: blur(12px);
                    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
                }
                .menu-btn {
                    background: none;
                    border: none;
                    color: #f8fafc;
                    cursor: pointer;
                    padding: 0.25rem 0.5rem;
                    margin-right: 0.5rem;
                    font-size: 1.2rem;
                    border-radius: 6px;
                }
                .menu-btn:hover { background: rgba(255,255,255,0.08); }
                .logo-mark {
                    display: flex; flex-direction: column; gap: 2px;
                    width: 12px; height: 18px; margin-right: 0.75rem;
                }
                .logo-mark .top    { flex: 1; background: var(--accent);      border-radius: 8px 8px 0 0; }
                .logo-mark .bottom { flex: 1; background: var(--accent-warm); border-radius: 0 0 8px 8px; }
                .brand {
                    font-family: 'Outfit', sans-serif;
                    font-weight: 800;
                    font-size: 0.95rem;
                    letter-spacing: 0.08em;
                    color: #f8fafc;
                    display: flex; align-items: center; gap: 0.5rem;
                }
                .brand .sep { color: rgba(255,255,255,0.3); }
                .brand .app-name { color: var(--accent); }
                .spacer { flex: 1; }
                .toolbar { display: flex; gap: 0.5rem; }

                .backdrop {
                    position: fixed;
                    top: 50px; left: 0; right: 0; bottom: 0;
                    background: rgba(0,0,0,0.5);
                    opacity: 0;
                    visibility: hidden;
                    transition: opacity 0.3s, visibility 0.3s;
                    z-index: 1;
                }
                .backdrop.open { opacity: 1; visibility: visible; }

                .panel {
                    position: fixed;
                    top: 50px; left: 0; bottom: 0;
                    width: 300px;
                    background: rgba(15, 23, 42, 0.95);
                    backdrop-filter: blur(20px);
                    -webkit-backdrop-filter: blur(20px);
                    border-right: 1px solid rgba(255,255,255,0.1);
                    transform: translateX(-110%);
                    visibility: hidden;
                    transition: transform 0.5s cubic-bezier(0.77, 0, 0.175, 1), visibility 0.5s;
                    z-index: 2;
                    padding: 1.5rem 0;
                    overflow-y: auto;
                }
                .panel.open {
                    transform: translateX(0);
                    visibility: visible;
                }
                .nav-list { list-style: none; padding: 0; margin: 0; }
                .nav-item {
                    display: flex;
                    align-items: center;
                    gap: 0.75rem;
                    padding: 0.75rem 1.5rem;
                    color: #cbd5e1;
                    text-decoration: none;
                    font-size: 0.95rem;
                    cursor: pointer;
                    transition: background 0.2s;
                }
                .nav-item:hover { background: rgba(255,255,255,0.05); color: #f8fafc; }
                .nav-item.active {
                    background: rgba(59, 130, 246, 0.15);
                    border-left: 3px solid var(--accent);
                    color: var(--accent);
                    padding-left: calc(1.5rem - 3px);
                }
                .nav-item fb-icon { --icon-size: 18px; }
            </style>
            <nav class="ribbon">
                <button class="menu-btn" aria-label="Menu">
                    <fb-icon name="menu"></fb-icon>
                </button>
                <a class="brand" href="#/">
                    <span class="logo-mark"><span class="top"></span><span class="bottom"></span></span>
                    <span>THE FISHBOWL</span>
                    ${appName ? `<span class="sep">·</span><span class="app-name">${appName}</span>` : ``}
                </a>
                <div class="spacer"></div>
                <div class="toolbar"><slot name="toolbar"></slot></div>
            </nav>
            <div class="backdrop"></div>
            <aside class="panel">
                <ul class="nav-list">
                    ${routes.map(r => `
                        <li>
                            <a class="nav-item ${r.hash === current ? "active" : ""}" href="${r.hash}">
                                ${r.icon ? `<fb-icon name="${r.icon}"></fb-icon>` : ""}
                                <span>${r.label}</span>
                            </a>
                        </li>
                    `).join("")}
                </ul>
            </aside>
        `;
    }

    attachHandlers() {
        const menuBtn = this.shadowRoot.querySelector(".menu-btn");
        const backdrop = this.shadowRoot.querySelector(".backdrop");
        const panel = this.shadowRoot.querySelector(".panel");

        const toggle = () => {
            this.isOpen = !this.isOpen;
            backdrop.classList.toggle("open", this.isOpen);
            panel.classList.toggle("open", this.isOpen);
        };
        menuBtn.addEventListener("click", toggle);
        backdrop.addEventListener("click", toggle);

        this._escHandler = (e) => { if (e.key === "Escape" && this.isOpen) toggle(); };
        document.addEventListener("keydown", this._escHandler);

        // Re-render on route change so the active highlight updates.
        this._hashHandler = () => this.render();
        window.addEventListener("hashchange", this._hashHandler);
    }

    disconnectedCallback() {
        if (this._escHandler)  document.removeEventListener("keydown",    this._escHandler);
        if (this._hashHandler) window.removeEventListener("hashchange",   this._hashHandler);
    }
}

customElements.define("fb-nav", FbNav);
```

- [ ] **Step 4: Verify build**

Run: `dotnet build Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/Fishbowl.Data/Resources/js/lib/router.js src/Fishbowl.Data/Resources/js/components/fb-nav.js
git commit -m "feat: add <fb-nav> with slide-out panel driven by router

Ribbon + slide-out panel. Panel nav list computed from
fb.router.routes() — routes self-register their label + icon so
the nav is always in sync with the registered views. Active item
highlighted; Esc and backdrop click close the panel.

Extends router.register(hash, tag, { label, icon }) signature.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 3.2: `<fb-hub-view>` with tiles

**Files:**
- Create: `src/Fishbowl.Data/Resources/js/views/fb-hub-view.js`

- [ ] **Step 1: Create `fb-hub-view.js`**

```js
/**
 * <fb-hub-view>  (mounted at #/)
 *
 * Landing view. Gradient title + subtitle + 2-tile grid.
 * Tile content is static here for v1 (Notes, Todos). Feature work adds tiles
 * as features become real — never show a tile for something that doesn't work.
 *
 * No <fb-nav> slide-out from the hub — the tiles are the navigation.
 */
class FbHubView extends HTMLElement {
    connectedCallback() {
        this.innerHTML = `
            <div class="orb"></div>
            <div class="hub-container">
                <header>
                    <h1>THE FISHBOWL</h1>
                    <p class="intro-text">Your memory lives here. You don't.</p>
                </header>
                <main class="grid">
                    <a class="tile" href="#/notes">
                        <fb-icon name="note"></fb-icon>
                        <div>
                            <h2>Notes</h2>
                            <p>Write freely. Find anything.</p>
                        </div>
                    </a>
                    <a class="tile" href="#/todos">
                        <fb-icon name="check"></fb-icon>
                        <div>
                            <h2>Todos</h2>
                            <p>Fast to-dos, always at hand.</p>
                        </div>
                    </a>
                </main>
            </div>
            <fb-footer></fb-footer>
        `;
    }
}

customElements.define("fb-hub-view", FbHubView);
fb.router.register("#/", "fb-hub-view", { label: "Home", icon: "home" });
```

Note: hub view does NOT use Shadow DOM — it's a full-page layout that needs to inherit site-wide styles (`.orb`, `.hub-container`, `.tile`, `.grid`) from `app.css`. Views use light DOM; components use Shadow DOM.

- [ ] **Step 2: Verify build**

Run: `dotnet build Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Fishbowl.Data/Resources/js/views/fb-hub-view.js
git commit -m "feat: add <fb-hub-view> — landing page with 2 tiles

Static hub for v1: Notes + Todos tiles only (no dead links).
Registers itself at #/ on load. Renders in light DOM (inherits
app.css layout classes).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 3.3: Rewrite `index.html` as SPA shell

**Files:**
- Modify: `src/Fishbowl.Data/Resources/index.html`
- Delete: `src/Fishbowl.Data/Resources/js/app.js`

- [ ] **Step 1: Rewrite `index.html`**

Replace entire file contents with:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>The Fishbowl</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&family=Outfit:wght@600;700;800&display=swap" rel="stylesheet">
    <link rel="stylesheet" href="/css/app.css">
</head>
<body>
    <!-- lib (load order matters: globals → icons → router+api, then components, then views) -->
    <script defer src="/js/lib/globals.js"></script>
    <script defer src="/js/lib/icons.js"></script>
    <script defer src="/js/lib/router.js"></script>
    <script defer src="/js/lib/api.js"></script>

    <!-- components -->
    <script defer src="/js/components/fb-icon.js"></script>
    <script defer src="/js/components/fb-footer.js"></script>
    <script defer src="/js/components/fb-nav.js"></script>

    <!-- views -->
    <script defer src="/js/views/fb-hub-view.js"></script>

    <!-- shell -->
    <fb-nav></fb-nav>
    <div id="app-root"></div>

    <script defer>
        window.addEventListener("DOMContentLoaded", () => {
            // Router mounts views into #app-root. Views register themselves
            // at script load time (defer preserves the script order above).
            if (fb.router) fb.router.mount("#app-root");
        });
    </script>
</body>
</html>
```

- [ ] **Step 2: Delete the old `app.js`**

Run: `rm src/Fishbowl.Data/Resources/js/app.js`

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: all backend tests pass. (UI smoke test comes in Task 4.4.)

- [ ] **Step 4: Manually verify**

Run: `dotnet run --project src/Fishbowl.Host` (in a separate shell).

Browse to `https://localhost:7180/`. Expected: the fallback handler serves `index.html`. Page loads. `<fb-nav>` ribbon at top. Hub appears with 2 tiles. Clicking Notes → URL changes to `#/notes`, the hub view disappears, app-root is empty (notes view not built yet — this is expected; comes in Phase 4). Stop the server after checking.

If it doesn't work, common issues:
- Script path typos in `<script defer src="..." />`.
- `fb` is undefined on a component because of script load order — ensure `globals.js` is first.

- [ ] **Step 5: Commit**

```bash
git add src/Fishbowl.Data/Resources/index.html
git rm src/Fishbowl.Data/Resources/js/app.js
git commit -m "feat: rewrite index.html as SPA shell

Single page with <fb-nav>, #app-root (router mount point), and
all lib/component/view scripts loaded via defer in the right
order. Old app.js deleted — view behaviors live inside their
respective view components now.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

# Phase 4 — Notes + Todos views (~2–3 days)

Goal: the two working features mount in the new shell. Smoke test lands.

## Task 4.1: `<fb-section>`, `<fb-toggle>`, `<fb-segmented-control>`

**Files:**
- Create: `src/Fishbowl.Data/Resources/js/components/fb-section.js`
- Create: `src/Fishbowl.Data/Resources/js/components/fb-toggle.js`
- Create: `src/Fishbowl.Data/Resources/js/components/fb-segmented-control.js`

- [ ] **Step 1: Read Dream Tools components reference**

Run: `Read` on `C:\Users\goosefx\SynologyDrive\PROJECTS\dream-tools\shared\web-components\components.js` for sections: `DreamSection`, `DreamToggle`, `DreamSegmentedControl`. (Look near the top of the 948-line file — components are defined sequentially.)

- [ ] **Step 2: Create `fb-section.js`**

```js
/**
 * <fb-section title="FILTERS">
 *   <slot>...</slot>
 * </fb-section>
 *
 * Sidebar group separator with uppercase title + thin border.
 *
 * Attributes:
 *   title — uppercase heading text.
 */
class FbSection extends HTMLElement {
    static get observedAttributes() { return ["title"]; }

    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    connectedCallback()      { this.render(); }
    attributeChangedCallback() { this.render(); }

    render() {
        const title = this.getAttribute("title") || "";
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    display: block;
                    margin-bottom: 1.5rem;
                }
                .title {
                    font-family: 'Outfit', sans-serif;
                    font-size: 0.7rem;
                    font-weight: 700;
                    letter-spacing: 0.1em;
                    color: #64748b;
                    text-transform: uppercase;
                    margin-bottom: 0.75rem;
                    padding-bottom: 0.5rem;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.08);
                }
                .body {
                    display: flex;
                    flex-direction: column;
                    gap: 0.5rem;
                }
            </style>
            <div class="title">${title}</div>
            <div class="body"><slot></slot></div>
        `;
    }
}

customElements.define("fb-section", FbSection);
```

- [ ] **Step 3: Create `fb-toggle.js`**

```js
/**
 * <fb-toggle label="Pinned" checked>
 *
 * Custom switch. Fires `change` events with `e.detail` = boolean.
 *
 * Attributes:
 *   label   — text to the left of the switch.
 *   checked — presence = on.
 */
class FbToggle extends HTMLElement {
    static get observedAttributes() { return ["label", "checked"]; }

    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    connectedCallback()      { this.render(); this.attach(); }
    attributeChangedCallback() { if (this.shadowRoot.firstChild) this.render(); }

    get checked() { return this.hasAttribute("checked"); }
    set checked(v) {
        if (v) this.setAttribute("checked", "");
        else   this.removeAttribute("checked");
    }

    render() {
        const label = this.getAttribute("label") || "";
        const on = this.checked;
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    display: flex;
                    align-items: center;
                    justify-content: space-between;
                    padding: 0.25rem 0;
                    cursor: pointer;
                    color: #f8fafc;
                    font-size: 0.875rem;
                }
                .label { user-select: none; }
                .switch {
                    position: relative;
                    width: 28px;
                    height: 14px;
                    background: rgba(255, 255, 255, 0.15);
                    border-radius: 7px;
                    transition: background 0.2s;
                }
                .knob {
                    position: absolute;
                    top: 1px; left: 1px;
                    width: 12px; height: 12px;
                    background: #f8fafc;
                    border-radius: 50%;
                    transition: transform 0.2s, background 0.2s;
                }
                :host([checked]) .switch { background: var(--accent, #3b82f6); }
                :host([checked]) .knob   { transform: translateX(14px); }
            </style>
            <span class="label">${label}</span>
            <div class="switch"><div class="knob"></div></div>
        `;
    }

    attach() {
        this.addEventListener("click", () => {
            this.checked = !this.checked;
            this.dispatchEvent(new CustomEvent("change", {
                detail: this.checked,
                bubbles: true,
                composed: true
            }));
        });
    }
}

customElements.define("fb-toggle", FbToggle);
```

- [ ] **Step 4: Create `fb-segmented-control.js`**

```js
/**
 * <fb-segmented-control value="all">
 *   <button data-value="today">Today</button>
 *   <button data-value="week">Week</button>
 *   <button data-value="all">All</button>
 * </fb-segmented-control>
 *
 * Button group; one active at a time. Active state tracked via `value` attribute.
 *
 * Attributes:
 *   value — currently-active data-value.
 *
 * Events:
 *   change — e.detail = new value (string).
 */
class FbSegmentedControl extends HTMLElement {
    static get observedAttributes() { return ["value"]; }

    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    connectedCallback()      { this.render(); this.attach(); }
    attributeChangedCallback() { if (this.shadowRoot.firstChild) this.applyActive(); }

    get value() { return this.getAttribute("value") || ""; }
    set value(v) {
        this.setAttribute("value", v);
        this.dispatchEvent(new CustomEvent("change", { detail: v, bubbles: true, composed: true }));
    }

    render() {
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    display: inline-flex;
                    background: rgba(0, 0, 0, 0.3);
                    border-radius: 8px;
                    padding: 2px;
                    gap: 2px;
                }
                ::slotted(button) {
                    padding: 0.35rem 0.75rem;
                    border: none;
                    background: transparent;
                    color: #cbd5e1;
                    border-radius: 6px;
                    cursor: pointer;
                    font-size: 0.85rem;
                    font-family: inherit;
                    transition: all 0.15s;
                }
                ::slotted(button:hover) { color: #f8fafc; }
                ::slotted(button.active) {
                    background: var(--accent, #3b82f6);
                    color: white;
                }
            </style>
            <slot></slot>
        `;
        this.applyActive();
    }

    applyActive() {
        const value = this.value;
        for (const btn of this.querySelectorAll("button[data-value]")) {
            btn.classList.toggle("active", btn.dataset.value === value);
        }
    }

    attach() {
        this.addEventListener("click", (e) => {
            const btn = e.target.closest("button[data-value]");
            if (!btn) return;
            this.value = btn.dataset.value;
        });
    }
}

customElements.define("fb-segmented-control", FbSegmentedControl);
```

- [ ] **Step 5: Verify build**

Run: `dotnet build Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/Fishbowl.Data/Resources/js/components/fb-section.js src/Fishbowl.Data/Resources/js/components/fb-toggle.js src/Fishbowl.Data/Resources/js/components/fb-segmented-control.js
git commit -m "feat: add <fb-section>, <fb-toggle>, <fb-segmented-control>

Three sidebar building blocks used by notes and todos views.
Shadow DOM; events bubble via bubbles:true + composed:true.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4.2: `<fb-notes-view>`

**Files:**
- Create: `src/Fishbowl.Data/Resources/js/views/fb-notes-view.js`
- Modify: `src/Fishbowl.Data/Resources/index.html` (add the script tag)

- [ ] **Step 1: Create `fb-notes-view.js`**

```js
/**
 * <fb-notes-view>  (mounted at #/notes)
 *
 * Three-pane layout: sidebar (filters) + list-pane + editor-pane.
 * Data loaded on connect via fb.api.notes.list().
 *
 * Light-DOM so app.css classes apply.
 */
class FbNotesView extends HTMLElement {
    constructor() {
        super();
        this.notes = [];
        this.selectedId = null;
        this.filters = { pinned: false, archived: false };
    }

    connectedCallback() {
        this.render();
        this.loadNotes();
    }

    async loadNotes() {
        try {
            this.notes = await fb.api.notes.list();
            this.renderList();
        } catch (err) {
            console.error("[fb-notes-view] list failed:", err);
        }
    }

    render() {
        this.innerHTML = `
            <div class="tool-layout">
                <aside class="tool-sidebar">
                    <fb-section title="Filters">
                        <fb-toggle label="Pinned only" id="filter-pinned"></fb-toggle>
                        <fb-toggle label="Show archived" id="filter-archived"></fb-toggle>
                    </fb-section>
                    <fb-section title="Actions">
                        <button class="btn primary" id="new-btn">
                            <fb-icon name="plus"></fb-icon> New note
                        </button>
                    </fb-section>
                </aside>
                <div class="tool-main">
                    <div class="list-pane" style="width:320px; border-right:1px solid var(--border); overflow-y:auto;">
                        <div id="note-list"></div>
                    </div>
                    <div class="editor-pane" style="flex:1; padding:2rem; overflow-y:auto;">
                        <div id="editor-empty" class="empty-state" style="color:var(--text-muted); text-align:center; margin-top:4rem;">
                            <fb-icon name="note" style="--icon-size: 64px;"></fb-icon>
                            <p>Select a note to start writing.</p>
                        </div>
                        <div id="editor" style="display:none;">
                            <input id="title" class="title-input" placeholder="Note title" style="width:100%; background:none; border:none; color:var(--text); font-size:1.5rem; font-family:'Outfit'; font-weight:700; margin-bottom:1rem; outline:none;"/>
                            <textarea id="content" placeholder="Start writing..." style="width:100%; height:calc(100vh - 260px); background:none; border:1px solid var(--border); border-radius:8px; padding:1rem; color:var(--text); font-family:inherit; resize:vertical; outline:none;"></textarea>
                            <div style="display:flex; justify-content:flex-end; margin-top:1rem;">
                                <button class="btn danger" id="delete-btn">
                                    <fb-icon name="trash"></fb-icon> Delete
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;

        this.querySelector("#new-btn").addEventListener("click", () => this.createNote());
        this.querySelector("#filter-pinned").addEventListener("change", (e) => {
            this.filters.pinned = e.detail;
            this.renderList();
        });
        this.querySelector("#filter-archived").addEventListener("change", (e) => {
            this.filters.archived = e.detail;
            this.renderList();
        });

        const title = this.querySelector("#title");
        const content = this.querySelector("#content");
        title.addEventListener("blur",   () => this.saveSelected());
        content.addEventListener("blur", () => this.saveSelected());

        this.querySelector("#delete-btn").addEventListener("click", () => this.deleteSelected());
    }

    renderList() {
        const filtered = this.notes.filter(n => {
            if (this.filters.pinned && !n.pinned) return false;
            if (!this.filters.archived && n.archived) return false;
            return true;
        });

        const list = this.querySelector("#note-list");
        list.innerHTML = filtered.map(n => `
            <div class="note-item" data-id="${n.id}" style="
                padding: 0.75rem 1rem;
                border-bottom: 1px solid var(--border);
                cursor: pointer;
                ${n.id === this.selectedId ? "background: rgba(59,130,246,0.1);" : ""}
            ">
                <div style="font-weight:600; font-size:0.95rem;">${escapeHtml(n.title || "(untitled)")}</div>
                <div style="color:var(--text-muted); font-size:0.8rem; margin-top:0.25rem; white-space:nowrap; overflow:hidden; text-overflow:ellipsis;">
                    ${escapeHtml((n.content || "").slice(0, 80))}
                </div>
            </div>
        `).join("");

        list.querySelectorAll(".note-item").forEach(el => {
            el.addEventListener("click", () => this.select(el.dataset.id));
        });
    }

    select(id) {
        this.selectedId = id;
        const note = this.notes.find(n => n.id === id);
        if (!note) return;
        this.querySelector("#editor-empty").style.display = "none";
        this.querySelector("#editor").style.display = "block";
        this.querySelector("#title").value   = note.title   || "";
        this.querySelector("#content").value = note.content || "";
        this.renderList();
    }

    async saveSelected() {
        if (!this.selectedId) return;
        const note = this.notes.find(n => n.id === this.selectedId);
        if (!note) return;
        const newTitle   = this.querySelector("#title").value;
        const newContent = this.querySelector("#content").value;
        if (newTitle === note.title && newContent === note.content) return;
        note.title   = newTitle;
        note.content = newContent;
        try {
            await fb.api.notes.update(note.id, note);
            this.renderList();
        } catch (err) {
            console.error("[fb-notes-view] update failed:", err);
        }
    }

    async createNote() {
        try {
            const created = await fb.api.notes.create({ title: "New note", content: "" });
            // The server returns the note with its ID populated.
            this.notes.unshift(created);
            this.selectedId = created.id;
            this.renderList();
            this.select(created.id);
            this.querySelector("#title").focus();
        } catch (err) {
            console.error("[fb-notes-view] create failed:", err);
        }
    }

    async deleteSelected() {
        if (!this.selectedId) return;
        if (!confirm("Delete this note?")) return;
        const id = this.selectedId;
        try {
            await fb.api.notes.delete(id);
            this.notes = this.notes.filter(n => n.id !== id);
            this.selectedId = null;
            this.querySelector("#editor-empty").style.display = "block";
            this.querySelector("#editor").style.display = "none";
            this.renderList();
        } catch (err) {
            console.error("[fb-notes-view] delete failed:", err);
        }
    }
}

function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[c]));
}

customElements.define("fb-notes-view", FbNotesView);
fb.router.register("#/notes", "fb-notes-view", { label: "Notes", icon: "note" });
```

- [ ] **Step 2: Add the script tag to `index.html`**

In `src/Fishbowl.Data/Resources/index.html`, inside the `<!-- views -->` section, add:

```html
<script defer src="/js/views/fb-notes-view.js"></script>
```

(Place after `<script defer src="/js/views/fb-hub-view.js"></script>`.)

Also add the three component scripts from Task 4.1 to the `<!-- components -->` section:

```html
<script defer src="/js/components/fb-section.js"></script>
<script defer src="/js/components/fb-toggle.js"></script>
<script defer src="/js/components/fb-segmented-control.js"></script>
```

(Place after `<script defer src="/js/components/fb-nav.js"></script>`.)

- [ ] **Step 3: Run backend tests**

Run: `dotnet test Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: all backend tests pass (no changes to API).

- [ ] **Step 4: Manual verify**

Run dev server; browse to `/#/notes`. Create a note, edit it, delete it.

- [ ] **Step 5: Commit**

```bash
git add src/Fishbowl.Data/Resources/js/views/fb-notes-view.js src/Fishbowl.Data/Resources/index.html
git commit -m "feat: add <fb-notes-view> — three-pane notes UI

Sidebar (filters + new-note button) + list-pane + editor-pane.
Loads via fb.api.notes.list(), saves on blur. Registers at #/notes
with label \"Notes\" and icon \"note\" so <fb-nav> picks it up.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4.3: `<fb-todos-view>`

**Files:**
- Create: `src/Fishbowl.Data/Resources/js/views/fb-todos-view.js`
- Modify: `src/Fishbowl.Data/Resources/index.html` (add the script tag)

- [ ] **Step 1: Create `fb-todos-view.js`**

Structurally identical to notes-view but with due-date + completed filtering and a checkbox per item. Full file:

```js
/**
 * <fb-todos-view>  (mounted at #/todos)
 *
 * Sidebar (filters) + list-pane (with checkbox per item) + editor-pane.
 */
class FbTodosView extends HTMLElement {
    constructor() {
        super();
        this.todos = [];
        this.selectedId = null;
        this.filters = { hideCompleted: true, when: "all" };
    }

    connectedCallback() {
        this.render();
        this.loadTodos();
    }

    async loadTodos() {
        try {
            this.todos = await fb.api.todos.list();
            this.renderList();
        } catch (err) {
            console.error("[fb-todos-view] list failed:", err);
        }
    }

    render() {
        this.innerHTML = `
            <div class="tool-layout">
                <aside class="tool-sidebar">
                    <fb-section title="Filters">
                        <fb-toggle label="Hide completed" id="filter-completed" checked></fb-toggle>
                        <div style="margin-top:0.75rem;">
                            <fb-segmented-control id="filter-when" value="all">
                                <button data-value="today">Today</button>
                                <button data-value="week">Week</button>
                                <button data-value="all">All</button>
                            </fb-segmented-control>
                        </div>
                    </fb-section>
                    <fb-section title="Actions">
                        <button class="btn primary" id="new-btn">
                            <fb-icon name="plus"></fb-icon> New todo
                        </button>
                    </fb-section>
                </aside>
                <div class="tool-main">
                    <div class="list-pane" style="width:360px; border-right:1px solid var(--border); overflow-y:auto;">
                        <div id="todo-list"></div>
                    </div>
                    <div class="editor-pane" style="flex:1; padding:2rem; overflow-y:auto;">
                        <div id="editor-empty" class="empty-state" style="color:var(--text-muted); text-align:center; margin-top:4rem;">
                            <fb-icon name="check" style="--icon-size: 64px;"></fb-icon>
                            <p>Select a todo to edit.</p>
                        </div>
                        <div id="editor" style="display:none;">
                            <input id="title" placeholder="What needs doing?" style="width:100%; background:none; border:none; color:var(--text); font-size:1.25rem; font-family:'Outfit'; font-weight:600; margin-bottom:1rem; outline:none;"/>
                            <label style="display:block; color:var(--text-muted); font-size:0.8rem; text-transform:uppercase; letter-spacing:0.05em; margin-bottom:0.3rem;">Due</label>
                            <input id="due-at" type="datetime-local" style="padding:0.5rem; background:rgba(0,0,0,0.3); border:1px solid var(--border); border-radius:8px; color:var(--text); font-family:inherit; margin-bottom:1rem;"/>
                            <div style="margin-bottom:1rem;">
                                <fb-toggle id="completed" label="Completed"></fb-toggle>
                            </div>
                            <button class="btn danger" id="delete-btn">
                                <fb-icon name="trash"></fb-icon> Delete
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        this.querySelector("#new-btn").addEventListener("click", () => this.createTodo());
        this.querySelector("#filter-completed").addEventListener("change", (e) => {
            this.filters.hideCompleted = e.detail;
            this.renderList();
        });
        this.querySelector("#filter-when").addEventListener("change", (e) => {
            this.filters.when = e.detail;
            this.renderList();
        });

        this.querySelector("#title").addEventListener("blur",     () => this.saveSelected());
        this.querySelector("#due-at").addEventListener("change",  () => this.saveSelected());
        this.querySelector("#completed").addEventListener("change", (e) => this.toggleCompleted(e.detail));
        this.querySelector("#delete-btn").addEventListener("click", () => this.deleteSelected());
    }

    renderList() {
        const now = new Date();
        const inDays = (n) => { const d = new Date(now); d.setDate(d.getDate() + n); return d; };

        const filtered = this.todos.filter(t => {
            if (this.filters.hideCompleted && t.completedAt) return false;
            if (this.filters.when === "all") return true;
            if (!t.dueAt) return this.filters.when === "all";
            const due = new Date(t.dueAt);
            if (this.filters.when === "today") return due <= inDays(1);
            if (this.filters.when === "week")  return due <= inDays(7);
            return true;
        });

        const list = this.querySelector("#todo-list");
        list.innerHTML = filtered.map(t => `
            <div class="todo-item" data-id="${t.id}" style="
                padding: 0.75rem 1rem;
                border-bottom: 1px solid var(--border);
                cursor: pointer;
                display: flex;
                gap: 0.6rem;
                ${t.id === this.selectedId ? "background: rgba(59,130,246,0.1);" : ""}
                ${t.completedAt ? "opacity: 0.5;" : ""}
            ">
                <input type="checkbox" data-check="${t.id}" ${t.completedAt ? "checked" : ""} style="margin-top:0.3rem;"/>
                <div style="flex:1;">
                    <div style="font-weight:500; font-size:0.95rem; ${t.completedAt ? "text-decoration:line-through;" : ""}">${escapeHtml(t.title || "(untitled)")}</div>
                    ${t.dueAt ? `<div style="color:var(--text-muted); font-size:0.75rem; margin-top:0.2rem;">${new Date(t.dueAt).toLocaleString()}</div>` : ""}
                </div>
            </div>
        `).join("");

        list.querySelectorAll(".todo-item").forEach(el => {
            el.addEventListener("click", (e) => {
                // Don't treat checkbox clicks as selection.
                if (e.target.matches("input[type=checkbox]")) return;
                this.select(el.dataset.id);
            });
        });
        list.querySelectorAll("input[data-check]").forEach(cb => {
            cb.addEventListener("change", async (e) => {
                const id = cb.dataset.check;
                const todo = this.todos.find(t => t.id === id);
                if (!todo) return;
                todo.completedAt = cb.checked ? new Date().toISOString() : null;
                await fb.api.todos.update(id, todo);
                this.renderList();
            });
        });
    }

    select(id) {
        this.selectedId = id;
        const t = this.todos.find(x => x.id === id);
        if (!t) return;
        this.querySelector("#editor-empty").style.display = "none";
        this.querySelector("#editor").style.display = "block";
        this.querySelector("#title").value  = t.title || "";
        this.querySelector("#due-at").value = t.dueAt ? new Date(t.dueAt).toISOString().slice(0, 16) : "";
        this.querySelector("#completed").checked = !!t.completedAt;
        this.renderList();
    }

    async saveSelected() {
        if (!this.selectedId) return;
        const t = this.todos.find(x => x.id === this.selectedId);
        if (!t) return;
        const newTitle = this.querySelector("#title").value;
        const dueVal   = this.querySelector("#due-at").value;
        t.title = newTitle;
        t.dueAt = dueVal ? new Date(dueVal).toISOString() : null;
        await fb.api.todos.update(t.id, t);
        this.renderList();
    }

    async toggleCompleted(completed) {
        if (!this.selectedId) return;
        const t = this.todos.find(x => x.id === this.selectedId);
        if (!t) return;
        t.completedAt = completed ? new Date().toISOString() : null;
        await fb.api.todos.update(t.id, t);
        this.renderList();
    }

    async createTodo() {
        try {
            const created = await fb.api.todos.create({ title: "New todo" });
            this.todos.unshift(created);
            this.selectedId = created.id;
            this.renderList();
            this.select(created.id);
            this.querySelector("#title").focus();
        } catch (err) {
            console.error("[fb-todos-view] create failed:", err);
        }
    }

    async deleteSelected() {
        if (!this.selectedId) return;
        if (!confirm("Delete this todo?")) return;
        const id = this.selectedId;
        await fb.api.todos.delete(id);
        this.todos = this.todos.filter(x => x.id !== id);
        this.selectedId = null;
        this.querySelector("#editor-empty").style.display = "block";
        this.querySelector("#editor").style.display = "none";
        this.renderList();
    }
}

function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[c]));
}

customElements.define("fb-todos-view", FbTodosView);
fb.router.register("#/todos", "fb-todos-view", { label: "Todos", icon: "check" });
```

- [ ] **Step 2: Add the script tag**

In `src/Fishbowl.Data/Resources/index.html`, inside `<!-- views -->`:

```html
<script defer src="/js/views/fb-todos-view.js"></script>
```

(After the notes-view script tag.)

- [ ] **Step 3: Run tests + manual verify**

Run: `dotnet test Fishbowl.sln -p:ContinuousIntegrationBuild=true` (expect pass).
Run dev server; browse to `/#/todos`. Create, toggle complete, edit, delete.

- [ ] **Step 4: Commit**

```bash
git add src/Fishbowl.Data/Resources/js/views/fb-todos-view.js src/Fishbowl.Data/Resources/index.html
git commit -m "feat: add <fb-todos-view> with checkbox list + date filter

Mirror of notes-view but with checkbox-on-item, dueAt editor, and
a segmented-control date filter (today/week/all). Registers at
#/todos.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4.4: Playwright smoke test project

**Files:**
- Create: `src/Fishbowl.Ui.Tests/Fishbowl.Ui.Tests.csproj`
- Create: `src/Fishbowl.Ui.Tests/PlaywrightFixture.cs`
- Create: `src/Fishbowl.Ui.Tests/UiSmokeTests.cs`
- Modify: `Fishbowl.sln`

- [ ] **Step 1: Create `Fishbowl.Ui.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Playwright" Version="1.49.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.6" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.4.0" />
    <PackageReference Include="xunit.v3" Version="0.7.0-pre.15" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Fishbowl.Host\Fishbowl.Host.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create `PlaywrightFixture.cs`**

`WebApplicationFactory<Program>` uses in-memory `TestServer`, which Playwright can't hit over real HTTP. Instead, launch `Fishbowl.Host` as a subprocess on a free port and wait for it to become ready.

```csharp
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;

namespace Fishbowl.Ui.Tests;

/// <summary>
/// Launches Fishbowl.Host as a subprocess on a free port and a headless
/// Chromium so the Playwright smoke test can hit real HTTP. Installs
/// Chromium on first use.
/// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    private Process? _hostProcess;
    public string BaseUrl { get; private set; } = string.Empty;
    public IPlaywright? Playwright { get; private set; }
    public IBrowser? Browser { get; private set; }

    public async ValueTask InitializeAsync()
    {
        // Install chromium (no-op if already cached)
        var installExit = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        if (installExit != 0)
            throw new InvalidOperationException($"Playwright chromium install failed (exit {installExit})");

        // Pick a free loopback port
        var port = FindFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        // Launch Fishbowl.Host via `dotnet run`. Assume it's built; --no-build avoids rebuild.
        var hostProjectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Fishbowl.Host"));

#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{hostProjectPath}\" --no-build -c {configuration} --urls {BaseUrl}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";

        _hostProcess = new Process { StartInfo = psi };
        _hostProcess.Start();

        // Wait up to 60s for the host to respond
        await WaitForHttpReady(BaseUrl + "/api/v1/version", TimeSpan.FromSeconds(60));

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser    != null) await Browser.CloseAsync();
        if (Playwright != null) Playwright.Dispose();
        if (_hostProcess != null && !_hostProcess.HasExited)
        {
            _hostProcess.Kill(entireProcessTree: true);
            await _hostProcess.WaitForExitAsync();
            _hostProcess.Dispose();
        }
    }

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForHttpReady(string url, TimeSpan timeout)
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow + timeout;
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var res = await http.GetAsync(url);
                if (res.IsSuccessStatusCode) return;
            }
            catch (Exception ex) { last = ex; }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Server at {url} did not become ready within {timeout}. Last error: {last?.Message}");
    }
}
```

**Note to implementer:** the subprocess must be able to find the built output of `Fishbowl.Host`. In CI (Task 4.5), build the solution first so `--no-build` works. Locally, also run `dotnet build Fishbowl.sln` before `dotnet test src/Fishbowl.Ui.Tests`.

- [ ] **Step 3: Create `UiSmokeTests.cs`**

```csharp
using Microsoft.Playwright;

namespace Fishbowl.Ui.Tests;

public class UiSmokeTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fixture;

    public UiSmokeTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Hub_LoadsAndNavigatesToNotes_Test()
    {
        var context = await _fixture.Browser!.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync(_fixture.BaseUrl);

        // Hub view renders with the two tiles.
        var notesTile = page.Locator("a.tile[href='#/notes']");
        var todosTile = page.Locator("a.tile[href='#/todos']");
        await notesTile.WaitForAsync(new LocatorWaitForOptions { Timeout = 3000 });
        Assert.True(await notesTile.IsVisibleAsync());
        Assert.True(await todosTile.IsVisibleAsync());

        // Click Notes tile -> hash changes, notes view mounts.
        await notesTile.ClickAsync();
        await page.WaitForURLAsync(url => url.EndsWith("#/notes"), new PageWaitForURLOptions { Timeout = 3000 });
        Assert.EndsWith("#/notes", page.Url);

        var notesView = page.Locator("fb-notes-view");
        await notesView.WaitForAsync(new LocatorWaitForOptions { Timeout = 3000 });
        Assert.True(await notesView.IsVisibleAsync());

        await context.CloseAsync();
    }
}
```

- [ ] **Step 4: Add project to the solution**

Run:

```bash
dotnet sln Fishbowl.sln add src/Fishbowl.Ui.Tests/Fishbowl.Ui.Tests.csproj
```

- [ ] **Step 5: Install Playwright browsers locally once**

Run: `dotnet build src/Fishbowl.Ui.Tests -p:ContinuousIntegrationBuild=true`

Then run the test once locally (it will download Chromium first time):

```bash
dotnet test src/Fishbowl.Ui.Tests
```

Expected: test passes. First run takes ~30s to download Chromium; subsequent runs are ~5s.

- [ ] **Step 6: Commit**

```bash
git add src/Fishbowl.Ui.Tests/ Fishbowl.sln
git commit -m "test: add Fishbowl.Ui.Tests with Playwright smoke test

Boots Host via WebApplicationFactory on a real Kestrel port, launches
Chromium headless, verifies: hub renders with 2 tiles, clicking Notes
navigates to #/notes and mounts <fb-notes-view>.

Runs on ubuntu-latest in CI (Task 4.5) — single-OS for speed.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4.5: Run the UI smoke test in CI

**Files:**
- Modify: `.github/workflows/ci.yml`

- [ ] **Step 1: Append a UI-smoke job**

At the end of `.github/workflows/ci.yml`, after the existing `build-test` job, add:

```yaml
  ui-smoke:
    runs-on: ubuntu-latest
    needs: build-test
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore Fishbowl.sln

      - name: Build UI test project
        run: dotnet build src/Fishbowl.Ui.Tests -c Release --no-restore -p:ContinuousIntegrationBuild=true

      - name: Install Playwright browsers
        run: pwsh src/Fishbowl.Ui.Tests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium

      - name: Run smoke test
        run: dotnet test src/Fishbowl.Ui.Tests -c Release --no-build --logger "trx;LogFileName=ui-smoke.trx" --results-directory TestResults

      - name: Upload results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: ui-smoke-results
          path: TestResults/*.trx
```

- [ ] **Step 2: Commit and push**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: run Playwright UI smoke test on ubuntu-latest

Separate job after build-test. Installs chromium via the Playwright
ps1 shim, runs the smoke test, uploads results.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"

git push origin master
```

- [ ] **Step 3: Verify CI goes green**

Open GitHub Actions. Both `build-test` (ubuntu + windows) and `ui-smoke` (ubuntu) must be green. If `ui-smoke` fails, read the job log — the most common cause is a missing `@types/*` lib or browsers-not-installed; follow the Playwright error message.

---

# Phase 5 — Remaining library + manual-test doc (~2 days)

Goal: ship the remaining components so future features have them available. No required feature integration, but the components must compile and be registered.

## Task 5.1: `<fb-window>`

**Files:**
- Create: `src/Fishbowl.Data/Resources/js/components/fb-window.js`
- Modify: `src/Fishbowl.Data/Resources/index.html`

- [ ] **Step 1: Read Dream Tools reference**

Run: `Read` on `C:\Users\goosefx\SynologyDrive\PROJECTS\dream-tools\shared\web-components\window.js` (270 lines). Study:
- `open` attribute, `open()`/`close()`/`toggle()` methods, `bringToFront()` z-index bump.
- Drag (on `document`, not the element) + resize (corner handle) + wheel-stopPropagation.
- Visual: `border-radius: 16px 16px 2px 16px`, macOS-style red close button.

Adaptations:
- Tag rename.
- Use `--accent` etc. tokens.
- No semantic/behavioural changes; straight port with renamed selectors.

- [ ] **Step 2: Create `fb-window.js`**

Port the Dream Tools window.js adapted. (Full ~270-line file; for brevity the template structure isn't reproduced here — implementer reads the reference file and renames `dream-window` → `fb-window`, updates CSS custom property names to match Fishbowl tokens, removes any Dream-specific styling constants.)

Key invariants to preserve:
- `attachShadow({ mode: "open" })`.
- `static get observedAttributes()` includes `open`, `title`, `width`, `height`, `top`, `left`.
- Drag listener on `document` via stored `this.onMouseMove` / `this.onMouseUp` (not inline).
- `this.addEventListener("wheel", e => e.stopPropagation(), { passive: true });`
- Minimum size 200×150 enforced in resize handler.
- z-index starts at 1000; `bringToFront()` uses a static counter on the class.
- Fire `open`/`close` custom events.

- [ ] **Step 3: Add script tag to `index.html`**

Inside `<!-- components -->`:

```html
<script defer src="/js/components/fb-window.js"></script>
```

- [ ] **Step 4: Smoke-check the port**

Add a temporary manual check: open the dev server, open the browser console, run:

```js
const w = document.createElement("fb-window");
w.setAttribute("title", "Test");
w.setAttribute("width", "400px");
w.setAttribute("height", "300px");
w.setAttribute("left", "200px");
w.setAttribute("top", "100px");
document.body.appendChild(w);
w.open();
```

Verify: window appears, drag + resize + close + wheel-scroll-inside-window all work. If any fail, fix in the port.

- [ ] **Step 5: Run all tests**

Run: `dotnet test Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add src/Fishbowl.Data/Resources/js/components/fb-window.js src/Fishbowl.Data/Resources/index.html
git commit -m "feat: add <fb-window> — draggable, resizable, stackable

Port of dream-window, adapted to Fishbowl tokens. Used for future
settings and data-editing overlays. Non-modal: multiple windows can
coexist. bringToFront() raises z-index above others.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 5.2: `<fb-slider>` and `<fb-hud>`

**Files:**
- Create: `src/Fishbowl.Data/Resources/js/components/fb-slider.js`
- Create: `src/Fishbowl.Data/Resources/js/components/fb-hud.js`
- Modify: `src/Fishbowl.Data/Resources/index.html`

- [ ] **Step 1: Create `fb-slider.js`**

Read `components.js` from dream-tools for `DreamSlider` reference. Full port:

```js
/**
 * <fb-slider label="Depth" min="0" max="100" step="1" value="50" suffix="px">
 * <fb-slider label="Quality" min="0" max="2" step="1" value="1" labels="Low,Medium,High">
 *
 * Range slider with live value display. Events:
 *   input  — fired on drag (live); e.detail = string value.
 *   change — fired on release; e.detail = string value.
 */
class FbSlider extends HTMLElement {
    static get observedAttributes() { return ["label", "min", "max", "step", "value", "suffix", "labels"]; }

    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    connectedCallback()        { this.render(); this.attach(); }
    attributeChangedCallback() { if (this.shadowRoot.firstChild) this.render(); }

    render() {
        const label  = this.getAttribute("label")  || "";
        const min    = this.getAttribute("min")    || "0";
        const max    = this.getAttribute("max")    || "100";
        const step   = this.getAttribute("step")   || "1";
        const value  = this.getAttribute("value")  || min;
        const suffix = this.getAttribute("suffix") || "";
        const labels = (this.getAttribute("labels") || "").split(",").map(s => s.trim()).filter(Boolean);

        const displayValue = labels.length ? (labels[parseInt(value, 10)] || value) : `${value}${suffix}`;

        this.shadowRoot.innerHTML = `
            <style>
                :host { display: block; }
                .row { display: flex; justify-content: space-between; font-size: 0.8rem; color: #cbd5e1; margin-bottom: 0.3rem; }
                .val { color: var(--accent, #3b82f6); font-weight: 600; }
                input[type="range"] {
                    width: 100%;
                    -webkit-appearance: none;
                    height: 4px;
                    background: rgba(255,255,255,0.1);
                    border-radius: 2px;
                    outline: none;
                }
                input[type="range"]::-webkit-slider-thumb {
                    -webkit-appearance: none;
                    width: 14px; height: 14px;
                    border-radius: 50%;
                    background: var(--accent, #3b82f6);
                    cursor: pointer;
                }
                input[type="range"]::-moz-range-thumb {
                    width: 14px; height: 14px;
                    border-radius: 50%;
                    background: var(--accent, #3b82f6);
                    cursor: pointer;
                    border: none;
                }
            </style>
            <div class="row">
                <span>${label}</span>
                <span class="val">${displayValue}</span>
            </div>
            <input type="range" min="${min}" max="${max}" step="${step}" value="${value}"/>
        `;
    }

    attach() {
        const input = this.shadowRoot.querySelector("input");
        if (!input) return;
        const fire = (type) => this.dispatchEvent(new CustomEvent(type, {
            detail: input.value,
            bubbles: true,
            composed: true
        }));
        input.addEventListener("input",  () => { this.setAttribute("value", input.value); fire("input");  });
        input.addEventListener("change", () => { this.setAttribute("value", input.value); fire("change"); });
    }
}

customElements.define("fb-slider", FbSlider);
```

- [ ] **Step 2: Create `fb-hud.js`**

```js
/**
 * <fb-hud position="top-right">...</fb-hud>
 *
 * Small info overlay positioned absolutely inside a relative parent.
 * Hidden when empty (MutationObserver watches textContent).
 *
 * Attributes:
 *   position — top-left | top-right | bottom-left | bottom-right (default top-right)
 */
class FbHud extends HTMLElement {
    static get observedAttributes() { return ["position"]; }

    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    connectedCallback() {
        this.render();
        this.updateVisibility();
        this.observer = new MutationObserver(() => this.updateVisibility());
        this.observer.observe(this, { childList: true, subtree: true, characterData: true });
    }

    disconnectedCallback() {
        if (this.observer) this.observer.disconnect();
    }

    attributeChangedCallback() { if (this.shadowRoot.firstChild) this.render(); }

    updateVisibility() {
        this.style.display = this.textContent.trim() ? "block" : "none";
    }

    render() {
        const pos = this.getAttribute("position") || "top-right";
        const positions = {
            "top-left":     "top: 1rem; left: 1rem;",
            "top-right":    "top: 1rem; right: 1rem;",
            "bottom-left":  "bottom: 1rem; left: 1rem;",
            "bottom-right": "bottom: 1rem; right: 1rem;"
        };
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    position: absolute;
                    ${positions[pos] || positions["top-right"]}
                    background: rgba(15, 23, 42, 0.85);
                    backdrop-filter: blur(8px);
                    -webkit-backdrop-filter: blur(8px);
                    border: 1px solid rgba(255,255,255,0.1);
                    border-radius: 8px;
                    padding: 0.5rem 0.75rem;
                    color: #cbd5e1;
                    font-size: 0.8rem;
                    font-family: 'Courier New', monospace;
                    pointer-events: none;
                    z-index: 10;
                }
            </style>
            <slot></slot>
        `;
    }
}

customElements.define("fb-hud", FbHud);
```

- [ ] **Step 3: Add script tags**

Inside `<!-- components -->` section of `index.html`:

```html
<script defer src="/js/components/fb-slider.js"></script>
<script defer src="/js/components/fb-hud.js"></script>
```

- [ ] **Step 4: Verify build**

Run: `dotnet build Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/Fishbowl.Data/Resources/js/components/fb-slider.js src/Fishbowl.Data/Resources/js/components/fb-hud.js src/Fishbowl.Data/Resources/index.html
git commit -m "feat: add <fb-slider> and <fb-hud>

Slider — range input with live value display, optional text labels,
fires input + change events.

Hud — absolute info overlay inside a relative parent; auto-hides
when empty via MutationObserver.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 5.3: `<fb-log>` and `<fb-terminal>`

**Files:**
- Create: `src/Fishbowl.Data/Resources/js/components/fb-log.js`
- Create: `src/Fishbowl.Data/Resources/js/components/fb-terminal.js`
- Modify: `src/Fishbowl.Data/Resources/index.html`

- [ ] **Step 1: Create `fb-log.js`**

```js
/**
 * <fb-log>
 *
 * Structured timestamped entries. Colors: info=green, warn=amber, error=red.
 *
 * Methods:
 *   add(text, level)   — level: "info" | "warn" | "error"
 *   clear()
 *   copy()             — copies all entries to clipboard
 */
class FbLog extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: "open" });
        this.entries = [];
    }

    connectedCallback() {
        this.render();
    }

    add(text, level = "info") {
        const ts = new Date().toLocaleTimeString();
        this.entries.push({ ts, text, level });
        this.renderBody();
    }

    clear() {
        this.entries = [];
        this.renderBody();
    }

    async copy() {
        const text = this.entries.map(e => `[${e.ts}] ${e.level.toUpperCase()}: ${e.text}`).join("\n");
        try {
            await navigator.clipboard.writeText(text);
            this.flashCopied();
        } catch {
            // ignore
        }
    }

    flashCopied() {
        const btn = this.shadowRoot.getElementById("copy-btn");
        if (!btn) return;
        const original = btn.textContent;
        btn.textContent = "Copied!";
        setTimeout(() => { btn.textContent = original; }, 1500);
    }

    render() {
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    display: flex;
                    flex-direction: column;
                    background: rgba(0, 0, 0, 0.3);
                    border-radius: 8px;
                    border: 1px solid var(--border, rgba(255,255,255,0.08));
                    font-size: 0.8rem;
                    font-family: 'Inter', sans-serif;
                    height: 100%;
                    min-height: 120px;
                }
                .hdr {
                    display: flex;
                    justify-content: space-between;
                    padding: 0.4rem 0.75rem;
                    border-bottom: 1px solid var(--border, rgba(255,255,255,0.08));
                    color: #64748b;
                    font-size: 0.7rem;
                    text-transform: uppercase;
                    letter-spacing: 0.05em;
                }
                .hdr button { font-size: 0.7rem; color: #cbd5e1; cursor: pointer; border: none; background: none; padding: 0.1rem 0.4rem; margin-left: 0.3rem; border-radius: 4px; }
                .hdr button:hover { color: #f8fafc; background: rgba(255,255,255,0.08); }
                .body {
                    flex: 1;
                    overflow-y: auto;
                    padding: 0.5rem 0.75rem;
                    color: #cbd5e1;
                }
                .entry {
                    padding: 0.2rem 0;
                    animation: slideIn 0.2s ease-out;
                }
                @keyframes slideIn {
                    from { transform: translateY(5px); opacity: 0; }
                    to   { transform: translateY(0);   opacity: 1; }
                }
                .entry .ts { color: #64748b; margin-right: 0.5rem; }
                .entry.info  { color: #22c55e; }
                .entry.warn  { color: #f59e0b; }
                .entry.error { color: var(--danger, #ef4444); }
            </style>
            <div class="hdr">
                <span>LOG</span>
                <div>
                    <button id="copy-btn">Copy</button>
                    <button id="clear-btn">Clear</button>
                </div>
            </div>
            <div class="body" id="body"></div>
        `;
        this.shadowRoot.getElementById("copy-btn").addEventListener("click", () => this.copy());
        this.shadowRoot.getElementById("clear-btn").addEventListener("click", () => this.clear());
        this.renderBody();
    }

    renderBody() {
        const body = this.shadowRoot.getElementById("body");
        if (!body) return;
        body.innerHTML = this.entries.map(e => `
            <div class="entry ${e.level}">
                <span class="ts">${e.ts}</span>
                <span>${escapeHtml(e.text)}</span>
            </div>
        `).join("");
        body.scrollTop = body.scrollHeight;
    }
}

function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[c]));
}

customElements.define("fb-log", FbLog);
```

- [ ] **Step 2: Create `fb-terminal.js`**

Same shape as `fb-log` with three differences: darker background (`#0f172a`), monospace font (`Courier New`), a fourth level (`success` = bright green). Write the full file following the `fb-log.js` pattern:

```js
/**
 * <fb-terminal>
 *
 * Terminal-style output. Darker background, monospace, colored line levels.
 *
 * Methods:
 *   append(text, level)  — level: "normal" | "warn" | "error" | "success"
 *   clear()
 *   copy()
 */
class FbTerminal extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: "open" });
        this.lines = [];
    }

    connectedCallback() { this.render(); }

    append(text, level = "normal") {
        this.lines.push({ text, level });
        this.renderBody();
    }
    clear() { this.lines = []; this.renderBody(); }

    async copy() {
        const text = this.lines.map(l => l.text).join("\n");
        try {
            await navigator.clipboard.writeText(text);
            this.flashCopied();
        } catch { /* ignore */ }
    }

    flashCopied() {
        const btn = this.shadowRoot.getElementById("copy-btn");
        if (!btn) return;
        const original = btn.textContent;
        btn.textContent = "Copied!";
        setTimeout(() => { btn.textContent = original; }, 1500);
    }

    render() {
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    display: flex;
                    flex-direction: column;
                    background: #0f172a;
                    border-radius: 8px;
                    border: 1px solid var(--border, rgba(255,255,255,0.08));
                    font-family: 'Courier New', 'Lucida Console', monospace;
                    font-size: 0.85rem;
                    height: 100%;
                    min-height: 140px;
                }
                .hdr {
                    display: flex;
                    justify-content: space-between;
                    padding: 0.4rem 0.75rem;
                    border-bottom: 1px solid rgba(255,255,255,0.08);
                    color: #64748b;
                    font-family: 'Inter', sans-serif;
                    font-size: 0.7rem;
                    text-transform: uppercase;
                    letter-spacing: 0.05em;
                }
                .hdr button { font-family: 'Inter', sans-serif; font-size: 0.7rem; color: #cbd5e1; cursor: pointer; border: none; background: none; padding: 0.1rem 0.4rem; margin-left: 0.3rem; border-radius: 4px; }
                .hdr button:hover { color: #f8fafc; background: rgba(255,255,255,0.08); }
                .body { flex: 1; overflow-y: auto; padding: 0.5rem 0.75rem; color: #22c55e; }
                .line.normal  { color: #22c55e; }
                .line.warn    { color: #eab308; }
                .line.error   { color: var(--danger, #ef4444); }
                .line.success { color: #4ade80; font-weight: 600; }
            </style>
            <div class="hdr">
                <span>TERMINAL</span>
                <div>
                    <button id="copy-btn">Copy</button>
                    <button id="clear-btn">Clear</button>
                </div>
            </div>
            <div class="body" id="body"></div>
        `;
        this.shadowRoot.getElementById("copy-btn").addEventListener("click", () => this.copy());
        this.shadowRoot.getElementById("clear-btn").addEventListener("click", () => this.clear());
        this.renderBody();
    }

    renderBody() {
        const body = this.shadowRoot.getElementById("body");
        if (!body) return;
        body.innerHTML = this.lines.map(l => `
            <div class="line ${l.level}">${escapeHtml(l.text)}</div>
        `).join("");
        body.scrollTop = body.scrollHeight;
    }
}

function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[c]));
}

customElements.define("fb-terminal", FbTerminal);
```

- [ ] **Step 3: Add script tags**

Inside `<!-- components -->`:

```html
<script defer src="/js/components/fb-log.js"></script>
<script defer src="/js/components/fb-terminal.js"></script>
```

- [ ] **Step 4: Verify build**

Run: `dotnet build Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/Fishbowl.Data/Resources/js/components/fb-log.js src/Fishbowl.Data/Resources/js/components/fb-terminal.js src/Fishbowl.Data/Resources/index.html
git commit -m "feat: add <fb-log> and <fb-terminal>

Log — structured timestamped entries with info/warn/error colors;
Copy + Clear buttons. Used later by sync / import flows.

Terminal — darker monospace variant with normal/warn/error/success.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 5.4: `<fb-loader>`

**Files:**
- Create: `src/Fishbowl.Data/Resources/js/components/fb-loader.js`
- Modify: `src/Fishbowl.Data/Resources/index.html`

- [ ] **Step 1: Create `fb-loader.js`**

```js
/**
 * <fb-loader>
 *
 * Full-screen blocking overlay with two concentric spinning rings
 * (blue + orange, matching the logo mark). Used for heavy processing.
 *
 * Methods:
 *   show(title, subtitle)  — displays overlay
 *   hide()                 — fades out over 300ms
 */
class FbLoader extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    connectedCallback() { this.render(); }

    show(title = "Loading...", subtitle = "") {
        this.render(title, subtitle);
        const overlay = this.shadowRoot.querySelector(".overlay");
        if (overlay) {
            overlay.style.display = "flex";
            requestAnimationFrame(() => overlay.style.opacity = "1");
        }
    }

    hide() {
        const overlay = this.shadowRoot.querySelector(".overlay");
        if (!overlay) return;
        overlay.style.opacity = "0";
        setTimeout(() => { overlay.style.display = "none"; }, 300);
    }

    render(title = "", subtitle = "") {
        this.shadowRoot.innerHTML = `
            <style>
                .overlay {
                    position: fixed;
                    inset: 0;
                    background: rgba(10, 10, 10, 0.85);
                    backdrop-filter: blur(8px);
                    -webkit-backdrop-filter: blur(8px);
                    display: none;
                    flex-direction: column;
                    align-items: center;
                    justify-content: center;
                    z-index: 10000;
                    opacity: 0;
                    transition: opacity 0.3s;
                }
                .spinner {
                    position: relative;
                    width: 64px;
                    height: 64px;
                    margin-bottom: 1.5rem;
                }
                .ring {
                    position: absolute;
                    inset: 0;
                    border: 3px solid transparent;
                    border-radius: 50%;
                }
                .ring.outer {
                    border-top-color: var(--accent, #3b82f6);
                    animation: spin 1.2s linear infinite;
                }
                .ring.inner {
                    inset: 8px;
                    border-top-color: var(--accent-warm, #f97316);
                    animation: spin 0.9s linear infinite reverse;
                }
                @keyframes spin {
                    to { transform: rotate(360deg); }
                }
                .title {
                    font-family: 'Outfit', sans-serif;
                    font-weight: 700;
                    color: #f8fafc;
                    font-size: 1.1rem;
                }
                .sub {
                    margin-top: 0.3rem;
                    color: #64748b;
                    font-size: 0.85rem;
                }
            </style>
            <div class="overlay">
                <div class="spinner">
                    <div class="ring outer"></div>
                    <div class="ring inner"></div>
                </div>
                <div class="title">${escapeHtml(title)}</div>
                ${subtitle ? `<div class="sub">${escapeHtml(subtitle)}</div>` : ""}
            </div>
        `;
    }
}

function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[c]));
}

customElements.define("fb-loader", FbLoader);
```

- [ ] **Step 2: Add script tag**

Inside `<!-- components -->`:

```html
<script defer src="/js/components/fb-loader.js"></script>
```

- [ ] **Step 3: Verify build**

Run: `dotnet build Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/Fishbowl.Data/Resources/js/components/fb-loader.js src/Fishbowl.Data/Resources/index.html
git commit -m "feat: add <fb-loader>

Full-screen blocking overlay with two concentric rings (blue + orange)
spinning in opposite directions. show(title, subtitle)/hide(). Used
by future import and sync flows.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 5.5: Manual test checklist + CLAUDE.md refresh + final push

**Files:**
- Create: `docs/ui-manual-test-checklist.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Create the manual test checklist**

Create `docs/ui-manual-test-checklist.md`:

```markdown
# UI Manual Test Checklist

Run through this list before cutting a release, or after any change that touches multiple components.

## Foundation

- [ ] `https://localhost:7180/` loads the hub with two tiles (Notes, Todos).
- [ ] Fonts render as Inter (body) + Outfit (headings).
- [ ] Orb background gradient visible in top-right.
- [ ] Footer shows `THE FISHBOWL · vX.Y.Z` (version from `/api/v1/version`).

## Navigation

- [ ] Clicking Notes tile navigates to `#/notes`; Notes view appears.
- [ ] Clicking Todos tile navigates to `#/todos`; Todos view appears.
- [ ] Clicking the brand ("THE FISHBOWL") in `<fb-nav>` returns to hub.
- [ ] Menu icon opens slide-out panel; shows Notes + Todos with icons.
- [ ] Active route highlighted in panel.
- [ ] Clicking a panel item navigates + closes the panel.
- [ ] Escape key closes the panel.
- [ ] Clicking the backdrop closes the panel.

## Login + Setup

- [ ] `/logout` clears the session and lands back at `/`.
- [ ] `/login` with no providers configured redirects to `/setup`.
- [ ] `/setup` rejects `not-a-google-id` with inline error.
- [ ] `/setup` rejects a short client-secret with inline error.
- [ ] `/setup` succeeds with valid inputs and redirects to `/`.
- [ ] Both pages show the orb background + logo mark + gradient title.

## Notes

- [ ] Creating a new note focuses the title input.
- [ ] Typing in title/content saves on blur (check by reloading and re-selecting).
- [ ] Deleting a note removes it from the list and clears the editor.
- [ ] Pinned-only toggle filters the list.
- [ ] Show-archived toggle filters the list.

## Todos

- [ ] New todo appears at top; title is editable.
- [ ] Checkbox toggles completion; strike-through + faded.
- [ ] Hide-completed toggle hides completed items.
- [ ] Today/Week/All filter behaves correctly.
- [ ] Due-date editor saves on change.
- [ ] Delete removes the todo.

## Windows (phase 5 components)

- [ ] Creating a `<fb-window>` from the console shows a draggable window.
- [ ] Drag moves it; resize handle resizes; minimum 200×150 enforced.
- [ ] Close button dismisses.
- [ ] Opening a second window stacks above (click its title to bring to front).
- [ ] Scrolling inside a window does NOT scroll the page behind it.

## Library smoke

- [ ] `customElements.get("fb-icon")` etc. returns a class for all 12 components.
- [ ] `<fb-icon name="note">` renders.
- [ ] `fb.icons.register("test", "<rect x='0' y='0' width='24' height='24'/>"); <fb-icon name="test">` renders the test shape.
- [ ] `<fb-log>` (or `<fb-terminal>`): instantiate in console, call `.add(...)` / `.append(...)`, entries appear with correct colors and Copy/Clear work.
- [ ] `<fb-loader>`: instantiate and call `.show("Working…", "hold on")`; overlay appears, `.hide()` fades it out.
```

- [ ] **Step 2: Refresh `CLAUDE.md`**

Open `CLAUDE.md`. Make these additions (keep the file under 200 lines — trim other sections if approaching the limit).

In the "Architecture" section, add a new sub-section after `### Plugins load in isolated AssemblyLoadContexts`:

```markdown
### UI is a single-page app with hash router

The frontend is pure Vanilla JS + Web Components (no framework, no build step). `index.html` is the SPA shell; views mount into `#app-root`. `fb.router.register("#/path", "tag-name", { label, icon })` wires a view; `fb.router.mount("#app-root")` starts the router. Views use light DOM (so `app.css` classes apply); components use Shadow DOM (for style isolation).

Login and setup stay server-rendered at `/login` and `/setup` — they're pre-auth and don't participate in the SPA routing.

System components are prefixed `fb-`; mods go in `fishbowl-mods/components/` with `usr_` prefix (CONCEPT.md rule). `IResourceProvider` already handles disk override of any `fb-*.js` file.

`fb.icons.register(name, pathString)` lets mods extend the icon dictionary at runtime without overriding `icons.js`.

Full spec: `docs/superpowers/specs/2026-04-19-ui-foundation-design.md`.
Manual test checklist: `docs/ui-manual-test-checklist.md`.
```

In "Conventions worth knowing", add:

```markdown
- **`api.js` for all backend calls.** Views call `fb.api.notes.list()` / `.create()` / `.update()` / `.delete()`. 401s redirect to `/login` automatically. Don't call `fetch` directly from views.
```

- [ ] **Step 3: Run all tests one last time**

Run: `dotnet test Fishbowl.sln -p:ContinuousIntegrationBuild=true`

Expected: every test passes (Core + Data + Host + Ui).

- [ ] **Step 4: Manual-verify per the checklist**

Walk the checklist at least once. Fix anything that doesn't pass; do not ship broken.

- [ ] **Step 5: Commit + push**

```bash
git add docs/ui-manual-test-checklist.md CLAUDE.md
git commit -m "docs: UI manual-test checklist and CLAUDE.md refresh

New doc: ui-manual-test-checklist.md — walk-through for before
releases and multi-component changes.

CLAUDE.md — new 'UI is a single-page app' section; api.js convention
added to 'Conventions worth knowing'.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"

git push origin master
```

Verify GitHub Actions green on both the `build-test` matrix and `ui-smoke` job.

---

# Done — UI foundation shipped

At this point:
- Hub at `#/` with 2 working-feature tiles.
- Full 12-component `<fb-*>` library available for future feature work.
- Login and setup look like part of the product.
- Notes + Todos fully functional via the new UI.
- Mod hook (`fb.icons.register`, disk override for `fb-*.js`) works.
- CI runs the smoke test on every push.

Next feature chapter (search, Discord bot, calendar sync, teams, apps) is its own brainstorm → spec → plan → execute cycle. Each new view registers itself with `fb.router.register("#/...", ...)`; `<fb-nav>` picks it up automatically.
