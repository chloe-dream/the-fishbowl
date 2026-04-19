# UI Foundation — Design

**Date:** 2026-04-19
**Status:** Approved design; implementation plan to follow
**Scope:** Replace the current ad-hoc UI with a full component library and view system adapted from Dream Tools. Purely UI — no new backend features.

## Why

The current Fishbowl UI is a ~900-line single-page scaffold that barely matches CONCEPT.md's "hub + tool pages" vision. The Dream Tools project (sibling project) ships a well-documented UI system — vanilla Web Components + Shadow DOM + design tokens + hub/tool-page patterns — that fits Fishbowl's self-hosted, no-build-step philosophy exactly. Adopting it as the base (not a 1:1 copy — adapted for Fishbowl's domain) sets the rails for every subsequent feature (Notes, Todos, Calendar, Contacts, Documents, Secrets, Search, Settings).

## Guiding principles

- **No dead links or buttons.** Every tile, button, or nav entry must correspond to a working feature. No "Coming soon" placeholders. This is a durable user principle, captured in memory.
- **Full library, not 1:1 usage.** Every component Dream Tools defines is ported, but Fishbowl decides how to *use* them (e.g. windows are for settings dialogs, not tool canvases).
- **CONCEPT.md wins on architecture.** SPA with hash-based client router is prescribed by CONCEPT.md. Dream Tools' multi-page hosting constraint doesn't apply here.
- **Modding works via `IResourceProvider` (unchanged).** System components use the `fb-` prefix; user mods use `usr_`. Disk → DB → embedded fallback already handles overrides and additions.
- **Vertical-slice delivery.** Master stays deployable after every phase. Feature work can interrupt at any phase boundary.

## Acceptance criteria

The UI foundation is "done" when:

1. **Hub at `#/`** renders with exactly the tiles for working features (v1: Notes + Todos — two tiles) using `<fb-*>` components.
2. **Each hub tile routes** to a working tool view (`#/notes`, `#/todos`) mounting the corresponding view component.
3. **Login and setup pages** use the design tokens (fonts, palette, glassmorphism, logo mark, orb background) but no `<fb-nav>`.
4. **All twelve components** listed in §3 exist, are registered, and have a short comment header describing purpose + attributes + events.
5. **Icon extension hook** `fb.registerIcon(name, pathString)` works end-to-end: a mod file drops into `fishbowl-mods/components/` and can register new icons that render via `<fb-icon name="...">`.
6. **CI green** including a new Playwright smoke test for "app loads + hub renders + tile navigation works".
7. **No feature regressions** — existing Notes CRUD and Todos CRUD paths still work end-to-end through the new UI.

## Total estimate

~8–10 working days across five phases.

---

## 1. Architecture

### Single-page app with hash router

Single `index.html` serves the hub and all tool views. Client router (`/js/lib/router.js`) watches `hashchange`, unmounts the current view from `#app-root`, mounts the new one by setting `innerHTML` to the matching custom element tag. No history API, no client-side redirects on non-hash changes, no transitions. Matches CONCEPT.md's "minimal hash-based client router" directive.

Login and setup stay server-rendered at `/login` and `/setup` respectively — they're pre-auth pages and don't participate in the SPA routing. The backend is unchanged.

### File layout

All UI files live under `src/Fishbowl.Data/Resources/` (existing; served via `IResourceProvider` with disk → DB → embedded fallback). Post-refactor:

```
src/Fishbowl.Data/Resources/
  index.html                    ← SPA shell (only #app-root, <fb-nav>, <fb-footer>)
  login.html                    ← pre-auth; tokens-only; no <fb-nav>
  setup.html                    ← pre-auth; tokens-only; no <fb-nav>
  css/
    app.css                     ← :root tokens, base element styles, orb, .tile
  js/
    lib/
      router.js                 ← hash router (~40 LOC)
      api.js                    ← fetch wrapper for /api/v1/*
      icons.js                  ← default icon-path dictionary + registerIcon
      globals.js                ← window.fb namespace setup
    components/
      fb-nav.js
      fb-icon.js
      fb-footer.js
      fb-window.js
      fb-section.js
      fb-toggle.js
      fb-slider.js
      fb-segmented-control.js
      fb-hud.js
      fb-log.js
      fb-terminal.js
      fb-loader.js
    views/
      fb-hub-view.js
      fb-notes-view.js
      fb-todos-view.js
```

### Shadow DOM everywhere

Every `<fb-*>` component uses Shadow DOM for style isolation. External theming via CSS custom properties declared at `:root` (they pierce shadow boundaries). Events bubble via `bubbles: true, composed: true`.

### Fonts

Google Fonts `<link>` in every HTML head:

```html
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&family=Outfit:wght@600;700;800&display=swap" rel="stylesheet">
```

Inter for UI body text; Outfit for headings, titles, branding (uppercase, tight tracking).

### Script loading

Load `icons.js` + `globals.js` first (synchronous defers), then components, then views. `fb-nav` uses `<fb-icon>` internally so icon must be registered first. Simplest approach: one script tag per file with `defer`, in the right order, at the end of `<body>`.

### Global namespace

`window.fb` exposes:

```js
window.fb = {
    api:   { notes: {...}, todos: {...}, providers: () => ... },
    icons: { register, get },
    router: { navigate, current },
    version: '...'   // populated from /api/v1/version
};
```

Mods use `fb.*` to interact with the system without rebuilding a component.

---

## 2. Design tokens

Dream Tools palette with one swap — **red accent becomes warm orange** to match Fishbowl's goldfish branding and reserve red for destructive actions only.

```css
:root {
    --bg:           #0a0a0a;                    /* deep midnight body background */
    --panel:        #1e1e1e;                    /* sidebar / card surface */
    --accent:       #3b82f6;                    /* blue — primary, focus, active */
    --accent-edit:  #a855f7;                    /* purple — edit modes */
    --accent-warm:  #f97316;                    /* orange — goldfish mark, warm highlights */
    --danger:       #ef4444;                    /* red — destructive only (delete, errors) */
    --border:       rgba(255, 255, 255, 0.08);
    --text:         #f8fafc;
    --text-muted:   #64748b;
    --glass:        rgba(15, 23, 42, 0.7);
    --bg-dark:      #000000;
}
```

Glassmorphism recipe (nav, windows, context menus, tiles) — unchanged from Dream Tools:

```css
background: rgba(15, 23, 42, 0.85);
backdrop-filter: blur(12px);
-webkit-backdrop-filter: blur(12px);
border: 1px solid rgba(255, 255, 255, 0.1);
```

### Logo mark

Pure-CSS two-tone block adapted to Fishbowl's colors:

```html
<div class="fb-logo-mark">
    <div class="top"></div>       <!-- blue: var(--accent) -->
    <div class="bottom"></div>    <!-- warm orange: var(--accent-warm) -->
</div>
```

```css
.fb-logo-mark { display: flex; flex-direction: column; gap: 2px; width: 14px; height: 20px; }
.fb-logo-mark .top    { flex: 1; background: var(--accent);       border-radius: 10px 10px 0 0; }
.fb-logo-mark .bottom { flex: 1; background: var(--accent-warm);  border-radius: 0 0 10px 10px; }
```

Used left of the brand name in `<fb-nav>`, on login + setup pages, and on the hub header.

### Orb background

Decorative radial-gradient blob, used on hub, login, and setup:

```css
.orb {
    position: fixed;
    width: 50vw; height: 50vw;
    background: radial-gradient(circle, rgba(59,130,246,0.05) 0%, transparent 70%);
    border-radius: 50%;
    top: -10vw; right: -10vw;
    z-index: -1; pointer-events: none;
}
```

---

## 3. Component library

Twelve components ported and adapted. Each ships with a one-line purpose comment + attributes + events header.

### `<fb-nav>`

50px fixed ribbon + slide-out 300px panel (`transform: translateX(-110%) → translateX(0)`, `0.5s cubic-bezier(0.77, 0, 0.175, 1)`). Logo mark + brand "THE FISHBOWL | <app-name>" on the left, named slot `toolbar` on the right. Panel lists routed views (computed from `fb.router.routes`) — each nav item is a link with `<fb-icon>` + label. Active route highlighted via `rgba(59,130,246,0.15)` background + 1px accent border.

**Attributes:** `app-name` (displayed after the brand, uppercase). **Slots:** `toolbar` (right-aligned buttons).

### `<fb-icon>`

Inline SVG lookup. Reads `name` attribute, resolves via `fb.icons.get(name)` against the dictionary, renders the SVG with `currentColor` stroke/fill. Size via `--icon-size` custom property (default `24px`).

**Default dictionary** (ships in `/js/lib/icons.js`): `fish`, `bowl`, `note`, `pencil`, `check`, `plus`, `trash`, `search`, `tag`, `pin`, `archive`, `lock`, `unlock`, `calendar`, `contact`, `document`, `attachment`, `key`, `settings`, `backup`, `sync`, `cloud`, `download`, `upload`, `menu`, `close`, `chevron-right`, `chevron-left`, `chevron-down`, `chevron-up`, `home`, `star`, `eye`, `eye-off`, `copy`, `link`, `external-link`, `info`, `warn`, `error`, `success`, `github`. (~40 icons.)

**Mod extension:** `fb.icons.register(name, pathString)`. Later calls with the same name override.

### `<fb-footer>`

Small centered strip below scrollable pages. Shows version string (from `fb.version`, populated by `fb.api.version()`), link to repository (if `system_config.GitHub:RepoUrl` is set — otherwise the link is omitted entirely, per the no-dead-links rule). No social icons.

### `<fb-window>`

Glassmorphic draggable/resizable floating panel. Used for settings + data-editing overlays. Non-modal (can stack); `bringToFront()` manages z-index. `border-radius: 16px 16px 2px 16px` (cut corner at resize handle). macOS-style 15px red close button. Drag tracked on `document` (not the element) for capture stability. `wheel` events `stopPropagation()` to avoid bubbling to the page. Minimum size 200×150.

**Attributes:** `title`, `width`, `height`, `top`, `left`, `open` (presence = visible). **Methods:** `open()`, `close()`, `toggle()`, `bringToFront()`.

### `<fb-section>`

Sidebar group separator with uppercase title + thin bottom border. `<slot>` for nested controls.

**Attributes:** `title`.

### `<fb-toggle>`

Switch (28×14px) with optional label. Fires `change` events (`e.detail = true|false`).

**Attributes:** `label`, `checked`.

### `<fb-slider>`

Range slider with live value display. Optional `labels` attribute replaces numeric readout with comma-separated text labels (`"Low,Medium,High"`). Fires `input` (live) and `change` (commit) events, `e.detail` = string value.

**Attributes:** `label`, `min`, `max`, `step`, `value`, `suffix`, `labels`.

### `<fb-segmented-control>`

Button group; one active at a time. Buttons slotted with `data-value` attributes. Active value tracked via `value` attribute. Color by `data-value`:
- `view` (or any "navigation" value) → blue (`--accent`).
- `edit`, `face`, `edge`, `vertex` (or explicit `data-edit`) → purple (`--accent-edit`).
- Others → slate.

**Events:** `change` with `e.detail = value`.

### `<fb-hud>`

Absolute info overlay positioned within a relative container. Auto-hides when empty (MutationObserver).

**Attributes:** `position` — `top-left` | `top-right` | `bottom-left` | `bottom-right`.

### `<fb-log>`

Structured timestamped entries. Colors: `info` = green, `warn` = amber, `error` = red. Entries animate in with slide-up fade. Header has Copy + Clear buttons.

**Methods:** `add(text, level)`, `clear()`, `copy()`.

### `<fb-terminal>`

Terminal-style output. Darker background (`#0f172a`), monospace (`Courier New`). Levels: `normal`, `warn`, `error`, `success`.

**Methods:** `append(text, level)`, `clear()`, `copy()`.

### `<fb-loader>`

Full-screen blocking overlay. Two concentric rings (blue + orange, matching logo mark) spinning in opposite directions. Fades in/out over 300ms.

**Methods:** `show(title, subtitle)`, `hide()`.

### Skipped

`<dream-scene-graph>` and `<dream-scene-item>` — built for 3D scene hierarchies. No natural Fishbowl use case. Trivially addable later (e.g. for Apps schema builder) if a feature needs it.

---

## 4. Views

### `<fb-hub-view>` (at `#/`)

Gradient title "THE FISHBOWL" (white → `--accent`, `font-family: Outfit`, `font-weight: 800`, `letter-spacing: -0.03em`, `clamp(2.5rem, 8vw, 4rem)`). Subtitle: "Your memory lives here." Tile grid below (CSS Grid, `auto-fill minmax(280px, 1fr)`, `grid-auto-rows: 280px`, `gap: 1.5rem`).

**v1 tiles (exactly two):**

- **Notes** — `<fb-icon name="note">`, title "Notes", body "Write freely. Find anything.", links to `#/notes`.
- **Todos** — `<fb-icon name="check">`, title "Todos", body "Fast to-dos, always at hand.", links to `#/todos`.

Tile hover lift (`translateY(-8px) scale(1.02)`, accent border, soft blue glow). Icon rotates slightly on hover (`scale(1.1) rotate(5deg)`).

No `<fb-nav>` slide-out from the hub — the tiles *are* the navigation.

### `<fb-notes-view>` (at `#/notes`)

Structure:

```
┌─ <fb-nav app-name="NOTES">  [New Note button in toolbar slot] ─┐
├─────────────────────────────────────────────────────────────────┤
│ sidebar (260px)      │  list-pane (360px)  │  editor-pane       │
│  <fb-section title    │  ┌─ scrollable ─┐  │  ┌────────────────┐│
│   "Filters">          │  │ note item     │  │  │ title input    ││
│  <fb-toggle "Pinned"> │  │ note item     │  │  │ content textarea││
│  <fb-toggle "Archived>│  │  ...          │  │  │ tag chips      ││
│  tag chips            │  └───────────────┘  │  │ delete btn     ││
│                       │                     │  └────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

Data calls: `fb.api.notes.list()` on mount, `.create()` from toolbar button, `.update()` on blur, `.delete()` from editor.

### `<fb-todos-view>` (at `#/todos`)

Same layout as Notes. Sidebar: `<fb-toggle label="Hide completed">`, `<fb-segmented-control>` for date filter (`Today | Week | All`). List shows checkbox + title; editor shows title + due-date + completion toggle.

### Login (`/login`)

Server-rendered HTML. Centered glass card (560px max-width, `rgba(30,41,59,0.7)` + `backdrop-filter: blur(12px)`, rounded 24px). Content: logo mark + "THE FISHBOWL" (Outfit 800, gradient), tagline, list of configured OAuth providers (calls `/api/auth/providers` at page load; renders one button per provider). Orb background. No `<fb-nav>`, no footer.

If no providers are configured, the page redirects client-side to `/setup` (since a no-provider state means unconfigured).

### Setup (`/setup`)

Same centered glass card pattern. Form with two inputs: Google ClientId (placeholder hint "ending in .apps.googleusercontent.com"), Google ClientSecret. Inline validation matches server-side rules from Task 4.2 of the A+ hardening pass. On submit, POST `/api/setup` and redirect to `/`. Orb background. No `<fb-nav>`, no footer.

---

## 5. Router

`/js/lib/router.js` — minimal hash-based router. ~40 lines. **Non-module script** (matches the rest of the codebase: every file loaded via `<script defer>`, no ES modules, no build step).

```js
// fb.router is set up by globals.js; this file populates it.
(function () {
    const routes = new Map();    // "#/notes" → "fb-notes-view"

    fb.router = {
        register(hash, tagName)  { routes.set(hash, tagName); },
        current()                { return location.hash || "#/"; },
        navigate(hash)           { location.hash = hash; },
        mount(rootSelector)      {
            const root = document.querySelector(rootSelector);
            const render = () => {
                const tag = routes.get(this.current()) || routes.get("#/");
                root.innerHTML = `<${tag}></${tag}>`;
            };
            window.addEventListener("hashchange", render);
            render();
        }
    };
})();
```

Registration happens in each view's definition file:

```js
// fb-notes-view.js
customElements.define("fb-notes-view", FbNotesView);
fb.router.register("#/notes", "fb-notes-view");
```

`index.html` ends with:

```html
<script defer>
    window.addEventListener("DOMContentLoaded", () => fb.router.mount("#app-root"));
</script>
```

Views programmatically navigate via `fb.router.navigate("#/notes")`. Since router.js runs before view files in the script ordering, `fb.router.register(...)` at the bottom of each view file finds the populated `fb.router`.

---

## 6. Data flow

`/js/lib/api.js` — thin `fetch` wrapper.

```js
const base = "/api/v1";

async function request(path, options = {}) {
    const res = await fetch(base + path, {
        headers: { "Content-Type": "application/json", ...(options.headers ?? {}) },
        ...options,
    });
    if (res.status === 401) {
        window.location.href = "/login";
        return;
    }
    if (!res.ok) throw new ApiError(res.status, await res.text());
    if (res.status === 204) return undefined;
    return res.json();
}

fb.api = {
    notes: {
        list:   ()          => request("/notes"),
        get:    (id)        => request(`/notes/${id}`),
        create: (n)         => request("/notes", { method: "POST", body: JSON.stringify(n) }),
        update: (id, n)     => request(`/notes/${id}`, { method: "PUT", body: JSON.stringify(n) }),
        delete: (id)        => request(`/notes/${id}`, { method: "DELETE" }),
    },
    todos: { /* same shape */ },
    providers: ()           => fetch("/api/auth/providers").then(r => r.json()),
    version:   ()           => request("/version"),   // stub; returns { version: "0.1.0-alpha" }
};
```

Views own their own state. No global store, no observable, no reducers. If a feature later needs shared state (e.g. the `<fb-nav>` needs to know whether a note is dirty to warn on navigation), we'll introduce a minimal event-bus pattern — not in scope for this pass.

A stub `GET /api/v1/version` endpoint is added to the backend returning `{ version: "0.1.0-alpha" }` so `<fb-footer>` has something real to show.

---

## 7. Modding integration

Unchanged from current design. `IResourceProvider` handles everything:

- **Overriding a system component**: drop `fishbowl-mods/components/fb-note-editor.js`. Disk tier wins over embedded.
- **Adding a new component**: drop `fishbowl-mods/components/usr_my-thing.js`. Loads additively via the script loader (see §8 for loading-order considerations).
- **Extending icons at runtime**: a mod file calls `fb.icons.register("my-custom-icon", "<path d='...'/>")`. Subsequent `<fb-icon name="my-custom-icon">` renders it.

Loading order for mods: `index.html` ends with a server-rendered loop that lists every file in `fishbowl-mods/components/` and emits a `<script src="...">` tag per file. Load order is alphabetical (deterministic). The listing endpoint (`GET /api/v1/mods/components`) returns the filenames; `index.html` fetches this list at load time and injects scripts.

The mod files are loaded *after* the system components (so they can override registered elements or extend icons). A mod that tries to `customElements.define` an already-registered tag throws a `NotSupportedError` — this is a deliberate guard rail. To override a system component, the mod must either (a) replace it entirely via the disk override (IResourceProvider serves the mod file *instead of* the embedded one, so only one `define` happens), or (b) define a new tag under `usr_` and replace consumer references.

---

## 8. Testing

### Automated

- **New test project: `Fishbowl.Ui.Tests`** using `Microsoft.Playwright` (the .NET binding) on top of xUnit v3 to match the rest of the test suite. Browser lifecycle handled by a per-test `IAsyncLifetime` fixture. One smoke test:
  - App loads at `https://localhost:7180/`.
  - Hub renders within 3 seconds.
  - Exactly two tiles are visible with text "Notes" and "Todos".
  - Clicking the Notes tile changes `window.location.hash` to `#/notes`.
  - `<fb-notes-view>` element appears in the DOM.
- Smoke test runs in CI on `ubuntu-latest` only (Playwright requires browser binaries; single-OS keeps CI fast).
- Test setup: Playwright installs chromium on first run. Playwright's browser cache is keyed in CI.

### Manual

Everything else. A short `docs/ui-manual-test-checklist.md` lists the manual steps: load hub, navigate via tiles, create a note, edit it, delete it, navigate via slide-out panel, open a window, verify scroll isolation in windows, verify login redirects when creds missing, verify setup validates input.

### Explicit non-goals

- **No per-component unit tests.** Web Components + Shadow DOM are painful to unit-test; the smoke test + integration with real API gives better signal for the cost.
- **No visual regression tests.** Overkill for v1. Add later if a feature (e.g. Apps template renderer) needs it.

---

## 9. Phases (vertical slices)

Five phases. Master stays green and deployable after each. Feature work can interrupt at any phase boundary.

### Phase 1 — Foundation (~1 day)

Deliverables:
- `css/app.css` with `:root` tokens, fonts link import, base styles, `.orb`, `.glass`, `.tile` classes.
- `/js/lib/globals.js` sets up `window.fb`.
- `/js/lib/icons.js` with the ~40-icon dictionary + `register/get`.
- `/js/lib/router.js`.
- `/js/lib/api.js`.
- `<fb-icon>` and `<fb-footer>` components.
- Backend: add `GET /api/v1/version` endpoint (trivial — returns a hardcoded version string for now).

No visible changes to the running app yet, but the foundation compiles and the new files are loaded.

### Phase 2 — Login + setup polish (~1 day)

Deliverables:
- `login.html` rebuilt with tokens, centered glass card, logo mark, orb background. Calls `/api/auth/providers` to render the provider list. Redirects to `/setup` if empty.
- `setup.html` rebuilt identically styled. Inline client-side validation (ClientId suffix, ClientSecret length) matches server validation.
- No `<fb-nav>` on either page.

First visible user-facing change. Pre-auth pages now feel like part of the product.

### Phase 3 — Hub + nav (~2 days)

Deliverables:
- `<fb-nav>` with ribbon + slide-out panel. Panel lists routed views (currently Notes + Todos).
- `<fb-hub-view>` at `#/` with the two tiles.
- `index.html` becomes the SPA shell (only `#app-root`, `<fb-nav>`, `<fb-footer>`).
- Router mounted.
- Tile clicks route via `location.hash`. `<fb-nav>` panel links use the same router.

At this point the old single-pane layout is gone; the user lands on the hub.

### Phase 4 — Notes + Todos tool pages (~2–3 days)

Deliverables:
- `<fb-notes-view>` at `#/notes` — sidebar + list + editor. Reuses current notes UX logic, rebuilt as Web Components.
- `<fb-todos-view>` at `#/todos` — sidebar + list + editor.
- `<fb-section>`, `<fb-toggle>`, `<fb-segmented-control>` components (needed by the todos-view date filter and notes sidebar).
- Playwright smoke test lands here (relies on the hub + at least one routed view).
- CI extension: `Fishbowl.Ui.Tests` project added to `ci.yml` on ubuntu-latest.

All existing note and todo functionality reachable through the new UI.

### Phase 5 — Remaining library + settings window (~2 days)

Deliverables:
- `<fb-window>`, `<fb-slider>`, `<fb-hud>`, `<fb-log>`, `<fb-terminal>`, `<fb-loader>`.
- A minimal settings `<fb-window>` opened from a toolbar button in `<fb-nav>`. Content stub-only: shows an `<fb-section title="Backup">` with a disabled-looking empty body and an `<fb-section title="Notifications">` with nothing inside. **Does not violate no-dead-links** because the window exists to prove the full library works end-to-end; any control that *does* appear in it must correspond to a working feature. For phase 5 we either ship the window with only one real control (e.g. a "Delete my account" button wired to a real endpoint) OR we defer the settings-window mount-point until a real settings feature exists. **Pick at implementation time based on what's easiest**; either is spec-compliant.

After this phase, the full component library is available for every subsequent feature chapter.

---

## 10. Out of scope

- Feature work (search, Discord bot, calendar sync, reminders, teams, apps, triggers). The goal is a UI foundation the features will sit on.
- Dark-mode / theme switching. Single dark theme for now.
- Accessibility audit. Components follow basic a11y (focusable buttons, keyboard toggles), but no formal WCAG audit or screen-reader testing in this pass.
- Internationalisation. English-only; strings inline in components.
- Mobile-responsive layouts beyond what CSS Grid gives for free. The tile grid and tool pages will resize; detailed phone-sized layouts are feature-work.
- `<dream-scene-graph>` / `<dream-scene-item>` — not needed for v1; trivially addable later.
- Apps (user-defined tables) and Teams UI. Both are their own brainstorm → spec → plan cycles.
- Visual regression testing, per-component unit testing.
- Animation polish beyond what's copied from Dream Tools.

## Open questions

None remaining. All were resolved during brainstorming:

- **Adoption scope** — full lib, adapted usage (not 1:1).
- **Component prefix** — `fb-` (per CONCEPT.md).
- **Icon system** — `<fb-icon>` with bundled dictionary + `fb.icons.register()`.
- **Hub content** — working features only (Notes + Todos v1). No dead links.
- **Login/setup treatment** — tokens only; no `<fb-nav>`.
- **SPA vs multi-page** — SPA with hash router (per CONCEPT.md).
- **Mods integration** — unchanged; `IResourceProvider` handles it. System `fb-`, mods `usr_`.
- **Logo mark colors** — blue top (`--accent`) + orange bottom (`--accent-warm`), matching goldfish and reserving red for destructive actions only.
- **Testing** — Playwright smoke test in new `Fishbowl.Ui.Tests` project; manual for everything else; no unit tests.
