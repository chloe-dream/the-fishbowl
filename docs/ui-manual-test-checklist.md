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

## Windows

- [ ] Creating a `<fb-window>` from the console shows a draggable window.
- [ ] Drag moves it; resize handle resizes; minimum 200×150 enforced.
- [ ] Close button dismisses.
- [ ] Opening a second window stacks above (click its title to bring to front).
- [ ] Scrolling inside a window does NOT scroll the page behind it.

## Library smoke

- [ ] `customElements.get("fb-icon")` etc. returns a class for all 12 components (`fb-icon`, `fb-footer`, `fb-nav`, `fb-section`, `fb-toggle`, `fb-segmented-control`, `fb-window`, `fb-slider`, `fb-hud`, `fb-log`, `fb-terminal`, `fb-loader`).
- [ ] `<fb-icon name="note">` renders.
- [ ] `fb.icons.register("test", "<rect x='0' y='0' width='24' height='24'/>"); <fb-icon name="test">` renders the test shape.
- [ ] `<fb-log>` (or `<fb-terminal>`): instantiate in console, call `.add(...)` / `.append(...)`, entries appear with correct colors and Copy/Clear work.
- [ ] `<fb-loader>`: instantiate and call `.show("Working…", "hold on")`; overlay appears, `.hide()` fades it out.
