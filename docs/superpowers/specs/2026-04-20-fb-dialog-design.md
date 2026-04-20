# fb-dialog — Custom Confirm Dialog

**Status:** approved 2026-04-20
**Scope:** new UI component + first call-site (delete-note).

## Motivation

The frontend uses native `window.confirm()` for destructive confirms. It's abrupt, breaks the app's visual language, has no keyboard safeguards, and only offers a binary yes/no. For notes we want a third alternative — "archive instead" — and a cooling-off period before the destructive action becomes the default.

## What we're building

Two deliverables shipped together:

1. **`<fb-dialog>`** — a general-purpose modal confirm dialog component.
2. **`fb.dialog.confirm({...})`** — a thin promise-based wrapper for the common case.

Plus: migrate `fb-notes-view.js` `deleteById` to use the wrapper. Offers "Cancel / Archive / Delete" on unarchived notes, "Cancel / Delete" on already-archived notes.

Out of scope for this change: `fb-todos-view.js` migration (tracked separately); non-confirm dialog variants (prompt, info); mobile/narrow-viewport layout.

## Files

- `src/Fishbowl.Data/Resources/js/components/fb-dialog.js` — web component (Shadow DOM, glass styling, focus trap, backdrop, arming timer).
- `src/Fishbowl.Data/Resources/js/lib/dialog.js` — `fb.dialog` namespace + `fb.dialog.confirm()` wrapper.
- `src/Fishbowl.Data/Resources/index.html` — add both `<script defer>` tags (dialog.js in the lib block, fb-dialog.js in the component block).
- `src/Fishbowl.Data/Resources/js/lib/globals.js` — add `dialog: null` placeholder in `window.fb`.
- `src/Fishbowl.Data/Resources/js/views/fb-notes-view.js` — `deleteById` rewritten to call `fb.dialog.confirm(...)`.

Embedded-resources glob in `Fishbowl.Data.csproj` picks the new files up automatically (`Resources\**\*`).

## Component API — `<fb-dialog>`

Shadow DOM. Looks for `open` attribute to render; absent = `display: none`.

**Attributes:**
- `open` — reflects state. Toggle via `open()` / `close()`, not directly.
- `title` — optional heading text.

**Properties:**
- `buttons` — array of `{ action: string, label: string, kind: "default" | "primary" | "destructive", armAfterMs?: number }`. Set before `open()`.

**Methods:**
- `open()` — sets the attribute, dispatches `fb-dialog:open`, starts arming timers for any `armAfterMs` buttons.
- `close(action = null)` — clears attribute, dispatches `fb-dialog:action` with `detail: { action }`, cancels any pending arming timer. Safe to call multiple times (idempotent).

**Events:**
- `fb-dialog:open` — fired after `open()`.
- `fb-dialog:action` — fired on any resolution (button click, Escape, backdrop click, programmatic close). `detail.action` is the button's `action` string, or `null` for non-button dismissals.

**Slotted body:** `<slot>` between title and button row. Callers can pass arbitrary markup; the wrapper uses a plain `<p>`.

## Wrapper API — `fb.dialog.confirm()`

```js
/**
 * Open a modal confirm dialog and resolve with the chosen action.
 * @param {object}   opts
 * @param {string}   opts.title
 * @param {string}  [opts.message]    plain-text body; rendered into a <p>.
 * @param {Array}    opts.buttons     [{ action, label, kind, armAfterMs? }]
 * @returns {Promise<string|null>}    the action of the clicked button,
 *                                    or null if dismissed.
 */
fb.dialog.confirm(opts): Promise<string | null>
```

Implementation:
- Create an `<fb-dialog>` element, set `title`, set `.buttons`, append `<p>` with `message` (textContent — never innerHTML, avoid XSS from upstream data).
- Append to `document.body`.
- Listen once for `fb-dialog:action`; resolve with `detail.action`.
- On resolution: wait for the fade-out transition (~150ms) then remove the node.
- `null` comes back for Escape, backdrop click, or programmatic dismiss — callers treat the same as "cancel."

## Interaction model

1. On `open()`: the dialog host element receives focus (`tabindex="-1"`) so keyboard events route to it. **No button is focused.**
2. For any button with `armAfterMs`, start a timer. When it fires:
   - Focus the button (activates native Enter-to-click).
   - The button gets a visual focus ring (colour matches the button's `kind` — red for destructive, accent otherwise).
3. **Arming cancellation:** a `mouseenter` on *any other button* before the arm fires cancels the timer. Delete will not auto-focus; user can still Tab to it and Enter.
4. **Pre-arm Enter:** does nothing — no button is focused.
5. **Escape:** always closes with `null` (treated as cancel). Wired on the dialog element's `keydown`.
6. **Backdrop click:** closes with `null`. Click on the panel itself is absorbed (does not bubble to the backdrop).
7. **Tab:** focus trap — cycles only between the dialog's buttons. Shift-Tab cycles backwards.
8. On close: element fades out over ~120ms, then the wrapper removes it from the DOM.

## Visual treatment

All scoped to the shadow root.

- **Overlay:** `position: fixed; inset: 0; z-index: 20000;` (above `fb-window`'s 10000+). Background `rgba(0,0,0,0.5)` + `backdrop-filter: blur(8px)`. Fade in 150ms.
- **Panel:** centered (flex). Width 420px, max-width `calc(100vw - 32px)`. `background: rgba(30, 41, 59, 0.75); backdrop-filter: blur(20px) saturate(180%); border: 1px solid rgba(255,255,255,0.1); border-radius: 16px; box-shadow: 0 20px 50px rgba(0,0,0,0.5), inset 0 0 0 1px rgba(255,255,255,0.05);` Matches `fb-window`'s glass. Scale 0.96 → 1 on open (120ms ease-out).
- **Title:** Outfit 700, 1.05rem, `var(--text, #f8fafc)`, 20px top+sides padding.
- **Body `<slot>`:** 0.9rem, `var(--text-muted, #94a3b8)`, 1.5 line-height, 8px top, 20px sides, 20px bottom padding.
- **Button row:** flex, `justify-content: flex-end`, gap 8px, 20px sides + bottom padding, 12px top.
- **Buttons:** `font: 600 0.85rem 'Inter', sans-serif; padding: 8px 14px; border-radius: 8px; cursor: pointer; border: 1px solid transparent; transition: background 120ms, color 120ms, border-color 120ms;`
  - `default` — transparent bg, `color: var(--text-muted)`, hover `background: rgba(255,255,255,0.06); color: var(--text)`.
  - `primary` — `background: var(--accent, #3b82f6); color: #fff;` hover brightens 10%.
  - `destructive` — transparent bg, `color: var(--danger, #ef4444); border-color: var(--danger, #ef4444);` hover `background: var(--danger); color: #fff`.
  - Focus: `outline: 2px solid var(--accent);` — for destructive buttons use `var(--danger)` so the focus ring matches intent. `outline-offset: 2px;`

Note: **`var(--text-muted, #94a3b8)` fallback.** Shadow DOM inherits CSS custom properties from `:root`, but `app.css` uses `#64748b` — darker than ideal for a muted body on a dark glass panel. The fallback `#94a3b8` keeps the dialog readable if a theme omits the token; production path uses the token.

## Call-site migration — `fb-notes-view.js`

Replaces `if (!confirm("Delete this note?")) return;` at `deleteById`:

```js
async deleteById(id) {
    const note = this.notes.find(n => n.id === id);
    if (!note) return;

    const buttons = [{ action: "cancel", label: "Cancel", kind: "default" }];
    if (!note.archived) {
        buttons.push({ action: "archive", label: "Archive", kind: "primary" });
    }
    buttons.push({ action: "delete", label: "Delete", kind: "destructive", armAfterMs: 2000 });

    const message = note.archived
        ? "This note will be permanently deleted."
        : "This note will be permanently deleted. Archive it instead to keep it hidden but recoverable.";

    const result = await fb.dialog.confirm({ title: "Delete this note?", message, buttons });

    if (result === "archive") return this.toggleArchivedById(id);
    if (result !== "delete") return;

    if (id === this.selectedId) {
        clearTimeout(this._saveDebounce);
        this._saveDebounce = null;
    }
    try {
        await fb.api.notes.delete(id);
        this.notes = this.notes.filter(n => n.id !== id);
        if (id === this.selectedId) this.clearSelection();
        this.renderList();
    } catch (err) {
        console.error("[fb-notes-view] delete failed:", err);
    }
}
```

The existing delete path below the confirm is preserved; only the branch on `result === "archive"` is new.

## Testing

- No new backend code, so no new C# tests.
- The existing Playwright smoke test (`Fishbowl.Ui.Tests`) loads `index.html` and verifies component definitions. It will cover that `fb-dialog` is defined and `fb.dialog.confirm` exists.
- Manual verification checklist added to `docs/ui-manual-test-checklist.md` (follow-up PR). Scope for this PR:
  1. Click delete on an unarchived note → 3 buttons, no default focus, Delete gets focus ring after 2s, Enter then deletes.
  2. Click delete on an already-archived note → 2 buttons only (no Archive).
  3. Press Escape during the 2s window → dialog closes, note still present.
  4. Hover Cancel before the 2s window ends → Delete never gets focus.
  5. Click Archive → note archived, removed from default list, dialog closes.
  6. Click backdrop → dialog dismisses, note still present.

## Non-goals

- No `prompt` / `info` dialog variants — add when first real consumer appears.
- No todo-view migration — tracked separately (trivial follow-up once `fb.dialog.confirm` exists).
- No responsive breakpoint — panel stays 420px; `max-width: calc(100vw - 32px)` keeps it sane on narrow viewports but we don't try to re-layout buttons.
