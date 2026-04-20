/**
 * <fb-notes-view>  (mounted at #/notes)
 *
 * iCloud-style two-pane notes UI:
 *   - List pane: search, "All Notes" header with filter + new-note actions,
 *     rich items (title + date + snippet + pin indicator).
 *   - Editor pane: centered timestamp, title + content inputs, delete and
 *     pin actions in the top-left of the header.
 *
 * Pinned notes always sort first; archived are hidden unless the archive
 * button is toggled. Search filters title + content.
 *
 * Light-DOM so app.css tokens apply; all component-specific CSS is scoped
 * with `fb-notes-view` to avoid leaking into other views.
 */
class FbNotesView extends HTMLElement {
    constructor() {
        super();
        this.notes = [];
        this.selectedId = null;
        this.showArchived = false;
        this.searchQuery = "";
        this._saveDebounce = null;
    }

    async connectedCallback() {
        this.render();
        await this.loadNotes();
    }

    disconnectedCallback() {
        // Router already clears on swap, but guard against any other unmount.
        // Fire-and-forget any pending autosave so a quick view-switch mid-typing
        // doesn't drop edits.
        this.flushSave();
        if (window.fb?.toolbar) fb.toolbar.clear();
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
            <style>
                fb-notes-view { display: block; height: 100%; }
                /* [hidden] must beat our scoped display rules */
                fb-notes-view [hidden] { display: none !important; }
                fb-notes-view .nv-layout {
                    display: flex;
                    height: calc(100vh - 50px);
                    background: var(--bg);
                }

                /* --- LIST PANE ------------------------------------------------- */
                fb-notes-view .nv-list-pane {
                    width: 340px;
                    background: var(--panel);
                    border-right: 1px solid var(--border);
                    display: flex;
                    flex-direction: column;
                    flex-shrink: 0;
                }
                fb-notes-view .nv-search {
                    position: relative;
                    padding: 12px 12px 0;
                }
                fb-notes-view .nv-search fb-icon {
                    position: absolute;
                    left: 22px;
                    top: 12px;
                    height: 32px;
                    display: flex;
                    align-items: center;
                    color: var(--text-muted);
                    --icon-size: 14px;
                    pointer-events: none;
                }
                fb-notes-view .nv-search input {
                    width: 100%;
                    background: rgba(0, 0, 0, 0.3);
                    border: 1px solid var(--border);
                    border-radius: 8px;
                    padding: 7px 10px 7px 32px;
                    color: var(--text);
                    font-family: inherit;
                    font-size: 13px;
                    outline: none;
                    transition: border-color 0.15s;
                }
                fb-notes-view .nv-search input::placeholder { color: var(--text-muted); }
                fb-notes-view .nv-search input:focus { border-color: var(--accent); }

                fb-notes-view .nv-list-header {
                    display: flex;
                    align-items: center;
                    padding: 12px 16px 6px;
                    gap: 2px;
                }
                fb-notes-view .nv-list-title {
                    flex: 1;
                    font-family: 'Outfit', sans-serif;
                    font-weight: 700;
                    font-size: 11px;
                    text-transform: uppercase;
                    letter-spacing: 0.1em;
                    color: var(--text-muted);
                }
                fb-notes-view .nv-icon-btn {
                    padding: 5px 7px;
                    border-radius: 6px;
                    background: transparent;
                    border: none;
                    color: var(--text-muted);
                    cursor: pointer;
                    display: inline-flex;
                    align-items: center;
                    transition: background 0.15s, color 0.15s;
                }
                fb-notes-view .nv-icon-btn:hover {
                    background: rgba(255, 255, 255, 0.06);
                    color: var(--text);
                }
                fb-notes-view .nv-icon-btn.active {
                    background: rgba(249, 115, 22, 0.15);
                    color: var(--accent-warm);
                }
                fb-notes-view .nv-icon-btn fb-icon { --icon-size: 16px; }

                fb-notes-view .nv-items {
                    flex: 1;
                    overflow-y: auto;
                    padding: 2px 8px 12px;
                }

                fb-notes-view .nv-item {
                    position: relative;
                    padding: 10px 12px;
                    border-radius: 10px;
                    cursor: pointer;
                    margin-bottom: 2px;
                    border: 1px solid transparent;
                    transition: background 0.12s, border-color 0.12s;
                }
                fb-notes-view .nv-item:hover { background: rgba(255, 255, 255, 0.04); }
                fb-notes-view .nv-item.selected {
                    background: rgba(59, 130, 246, 0.12);
                    border-color: rgba(59, 130, 246, 0.28);
                }
                fb-notes-view .nv-item-title-row {
                    display: flex;
                    align-items: center;
                    gap: 6px;
                    margin-bottom: 4px;
                    /* Reserve space so the absolute action row never collides with
                       the title. Pin indicator alone is ~22px; on hover the full
                       3-icon row needs more, but title already ellipsis-truncates. */
                    padding-right: 22px;
                }
                fb-notes-view .nv-item-title {
                    font-weight: 600;
                    font-size: 14px;
                    overflow: hidden;
                    text-overflow: ellipsis;
                    white-space: nowrap;
                    color: var(--text);
                    flex: 1;
                }

                /* Hover action row: pin (persistent when active), archive, delete.
                   Buttons are opacity:0 by default and fade in when the row is
                   hovered. The active pin overrides that so a pinned note always
                   shows its indicator. */
                fb-notes-view .nv-item-actions {
                    position: absolute;
                    top: 6px;
                    right: 6px;
                    display: flex;
                    gap: 1px;
                }
                fb-notes-view .nv-item-action {
                    padding: 4px;
                    background: transparent;
                    border: none;
                    border-radius: 5px;
                    color: var(--text-muted);
                    cursor: pointer;
                    display: inline-flex;
                    align-items: center;
                    opacity: 0;
                    transition: opacity 0.12s, background 0.12s, color 0.12s;
                }
                fb-notes-view .nv-item-action fb-icon { --icon-size: 13px; }
                fb-notes-view .nv-item:hover .nv-item-action { opacity: 1; }
                fb-notes-view .nv-item-action:hover {
                    background: rgba(255, 255, 255, 0.1);
                    color: var(--text);
                }
                fb-notes-view .nv-item-action.pin.active {
                    opacity: 1;
                    color: var(--accent-warm);
                }
                /* Archive indicator: same persistent-icon treatment as pin,
                   in muted grey so it reads as "backgrounded / shelved"
                   instead of "highlighted". */
                fb-notes-view .nv-item-action.archive.active {
                    opacity: 1;
                    color: var(--text-muted);
                }
                fb-notes-view .nv-item-action.delete:hover { color: var(--danger); }

                /* Archived rows (only shown when "show archived" is on) get
                   dimmed title + snippet so they're visually distinct from
                   active notes at a glance. Date and the persistent archive
                   icon are left at normal opacity to stay scannable. */
                fb-notes-view .nv-item.archived .nv-item-title,
                fb-notes-view .nv-item.archived .nv-item-snippet {
                    opacity: 0.55;
                }
                fb-notes-view .nv-item.archived .nv-item-title {
                    font-style: italic;
                }
                fb-notes-view .nv-item-preview {
                    display: flex;
                    gap: 6px;
                    font-size: 12px;
                    line-height: 1.4;
                    color: var(--text-muted);
                    align-items: baseline;
                }
                fb-notes-view .nv-item-date {
                    flex-shrink: 0;
                    font-variant-numeric: tabular-nums;
                }
                fb-notes-view .nv-item-snippet {
                    overflow: hidden;
                    text-overflow: ellipsis;
                    white-space: nowrap;
                    flex: 1;
                }
                fb-notes-view .nv-empty-list {
                    padding: 40px 16px;
                    text-align: center;
                    color: var(--text-muted);
                    font-size: 13px;
                }

                /* --- EDITOR PANE ---------------------------------------------- */
                fb-notes-view .nv-editor-pane {
                    flex: 1;
                    display: flex;
                    flex-direction: column;
                    min-width: 0;
                }
                fb-notes-view .nv-editor-footer {
                    display: flex;
                    align-items: center;
                    gap: 12px;
                    min-height: 32px;
                    padding: 8px 20px;
                    border-top: 1px solid var(--border);
                    background: var(--panel);
                    font-size: 11px;
                    color: var(--text-muted);
                    flex-shrink: 0;
                }
                fb-notes-view .nv-editor-footer-spacer { flex: 1; }
                fb-notes-view .nv-editor-footer #timestamp {
                    font-variant-numeric: tabular-nums;
                }
                fb-notes-view .nv-editor-tags {
                    display: flex;
                    gap: 6px;
                    /* Tag chips land here in a future change. */
                }
                fb-notes-view .nv-editor-body {
                    flex: 1;
                    overflow: auto;
                    padding: 36px 56px;
                    display: flex;
                    flex-direction: column;
                }
                fb-notes-view .nv-empty-state {
                    flex: 1;
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                    justify-content: center;
                    color: var(--text-muted);
                    gap: 12px;
                }
                fb-notes-view .nv-empty-state fb-icon {
                    --icon-size: 72px;
                    opacity: 0.2;
                }
                fb-notes-view .nv-empty-state p {
                    margin: 0;
                    font-size: 14px;
                }
                fb-notes-view .nv-title-input {
                    width: 100%;
                    font-family: 'Outfit', sans-serif;
                    font-weight: 800;
                    font-size: 1.75rem;
                    letter-spacing: -0.02em;
                    background: none;
                    border: none;
                    color: var(--text);
                    outline: none;
                    margin-bottom: 14px;
                    padding: 0;
                }
                fb-notes-view .nv-title-input::placeholder {
                    color: var(--text-muted);
                    opacity: 0.4;
                }
                fb-notes-view .nv-content-input {
                    /* Auto-grows to fit content via JS; overflow:hidden disables
                       the textarea's internal scrollbar so the outer .nv-editor-body
                       becomes the scroll container. That places the scrollbar at
                       the viewport's right edge, running full-height from nav
                       bottom to the footer, and lets the global scrollbar theme
                       apply (textarea internal scrollbars are rendered differently
                       on some browsers and can't be reliably themed). */
                    display: block;
                    width: 100%;
                    min-height: 60vh;
                    background: none;
                    border: none;
                    color: var(--text);
                    font-family: inherit;
                    font-size: 15px;
                    line-height: 1.6;
                    outline: none;
                    resize: none;
                    padding: 0;
                    overflow: hidden;
                }
                fb-notes-view .nv-content-input::placeholder {
                    color: var(--text-muted);
                    opacity: 0.4;
                }

                /* Archived-note editor is read-only. Inputs get the readonly
                   attr (caret suppressed, value unchangeable) and we dim them
                   slightly so the state is visible. not-allowed cursor on
                   hover reinforces it. */
                fb-notes-view .nv-editor.readonly .nv-title-input,
                fb-notes-view .nv-editor.readonly .nv-content-input {
                    opacity: 0.7;
                    cursor: not-allowed;
                }

                /* Small pill in the editor footer when viewing an archived
                   note, so the read-only state is explained, not just felt. */
                fb-notes-view .nv-archived-pill {
                    display: inline-flex;
                    align-items: center;
                    gap: 4px;
                    padding: 2px 8px;
                    border-radius: 999px;
                    background: rgba(255, 255, 255, 0.06);
                    border: 1px solid var(--border);
                    color: var(--text-muted);
                    font-size: 10px;
                    font-weight: 600;
                    text-transform: uppercase;
                    letter-spacing: 0.08em;
                }
                fb-notes-view .nv-archived-pill fb-icon { --icon-size: 10px; }

                fb-notes-view .nv-editor {
                    /* No flex:1/column here — we want content to stack normally
                       and push the outer .nv-editor-body to scroll when it
                       overflows, rather than forcing the textarea to scroll
                       internally. */
                    display: block;
                }
            </style>

            <div class="nv-layout">
                <aside class="nv-list-pane">
                    <div class="nv-search">
                        <fb-icon name="search"></fb-icon>
                        <input type="search" id="search-input" placeholder="Search all notes"/>
                    </div>
                    <div class="nv-list-header">
                        <span class="nv-list-title" id="list-title">All Notes</span>
                        <button class="nv-icon-btn" id="toggle-archived-btn" title="Show archived">
                            <fb-icon name="archive"></fb-icon>
                        </button>
                        <button class="nv-icon-btn" id="new-btn" title="New note">
                            <fb-icon name="plus"></fb-icon>
                        </button>
                    </div>
                    <div class="nv-items" id="note-list"></div>
                </aside>

                <main class="nv-editor-pane">
                    <div class="nv-editor-body">
                        <div class="nv-empty-state" id="editor-empty">
                            <fb-icon name="note"></fb-icon>
                            <p>Select a note to start writing</p>
                        </div>
                        <div id="editor" class="nv-editor" hidden>
                            <input id="title" class="nv-title-input" placeholder="Untitled"/>
                            <textarea id="content" class="nv-content-input" placeholder="Start writing..."></textarea>
                        </div>
                    </div>
                    <footer class="nv-editor-footer" id="editor-footer" hidden>
                        <span class="nv-editor-footer-meta">
                            Updated <span id="timestamp"></span>
                        </span>
                        <span class="nv-archived-pill" id="archived-pill" hidden>
                            <fb-icon name="archive"></fb-icon> Archived · Read-only
                        </span>
                        <div class="nv-editor-footer-spacer"></div>
                        <div class="nv-editor-tags" id="tags">
                            <!-- Tag chips land here in a future change. -->
                        </div>
                    </footer>
                </main>
            </div>
        `;
        this.attachHandlers();
    }

    attachHandlers() {
        this.querySelector("#new-btn").addEventListener("click", () => this.createNote());
        this.querySelector("#toggle-archived-btn").addEventListener("click", () => {
            this.showArchived = !this.showArchived;
            this.querySelector("#toggle-archived-btn").classList.toggle("active", this.showArchived);
            this.querySelector("#list-title").textContent = this.showArchived ? "All + Archived" : "All Notes";
            this.renderList();
        });
        this.querySelector("#search-input").addEventListener("input", (e) => {
            this.searchQuery = e.target.value.trim().toLowerCase();
            this.renderList();
        });
        const titleEl = this.querySelector("#title");
        titleEl.addEventListener("blur",  () => this.flushSave());
        titleEl.addEventListener("input", () => this.scheduleAutoSave());
        const contentEl = this.querySelector("#content");
        contentEl.addEventListener("blur",  () => this.flushSave());
        contentEl.addEventListener("input", () => {
            this.autosizeContent();
            this.scheduleAutoSave();
        });
        // Pin + trash also live in fb-nav's toolbar (see updateToolbar) and on
        // each list row (see renderList's action buttons).
    }

    /** Schedule an autosave after the user stops typing. */
    scheduleAutoSave() {
        clearTimeout(this._saveDebounce);
        this._saveDebounce = setTimeout(() => this.saveSelected(), 700);
    }

    /** Cancel any pending autosave and save immediately. */
    async flushSave() {
        clearTimeout(this._saveDebounce);
        this._saveDebounce = null;
        await this.saveSelected();
    }

    /** Resize the textarea to fit its content so the outer editor body scrolls. */
    autosizeContent() {
        const el = this.querySelector("#content");
        if (!el) return;
        el.style.height = "auto";
        el.style.height = el.scrollHeight + "px";
    }

    /**
     * Publish per-note actions to the global fb-nav toolbar. Called whenever
     * a note becomes the active selection, or when the active note's pinned
     * state changes.
     */
    updateToolbar(note) {
        fb.toolbar.set([
            {
                icon:    "pin",
                title:   note.pinned ? "Unpin" : "Pin to top",
                active:  !!note.pinned,
                onClick: () => this.togglePinned()
            },
            {
                icon:    "trash",
                title:   "Delete note",
                onClick: () => this.deleteSelected()
            }
        ]);
    }

    renderList() {
        const filtered = this.notes.filter(n => {
            if (!this.showArchived && n.archived) return false;
            if (this.searchQuery) {
                const haystack = ((n.title || "") + " " + (n.content || "")).toLowerCase();
                if (!haystack.includes(this.searchQuery)) return false;
            }
            return true;
        });

        // Sort: pinned first, then most-recently-updated.
        filtered.sort((a, b) => {
            if (a.pinned && !b.pinned) return -1;
            if (!a.pinned && b.pinned) return 1;
            return new Date(b.updatedAt || 0) - new Date(a.updatedAt || 0);
        });

        const list = this.querySelector("#note-list");
        if (filtered.length === 0) {
            list.innerHTML = `<div class="nv-empty-list">No notes. Click + to create one.</div>`;
            return;
        }

        list.innerHTML = filtered.map(n => {
            const snippet = (n.content || "").replace(/\s+/g, " ").slice(0, 80);
            const date = this.formatDate(n.updatedAt);
            const isSelected = n.id === this.selectedId;
            const archiveTitle = n.archived ? "Unarchive" : "Archive";
            const pinTitle = n.pinned ? "Unpin" : "Pin to top";
            const rowClasses = [
                "nv-item",
                isSelected  ? "selected" : "",
                n.archived  ? "archived" : "",
            ].filter(Boolean).join(" ");
            return `
                <div class="${rowClasses}" data-id="${n.id}">
                    <div class="nv-item-title-row">
                        <span class="nv-item-title">${escapeHtml(n.title || "Untitled")}</span>
                    </div>
                    <div class="nv-item-preview">
                        <span class="nv-item-date">${date}</span>
                        <span class="nv-item-snippet">${escapeHtml(snippet || "No additional text")}</span>
                    </div>
                    <div class="nv-item-actions">
                        <button class="nv-item-action pin ${n.pinned ? "active" : ""}" data-action="pin" title="${pinTitle}"><fb-icon name="pin"></fb-icon></button>
                        <button class="nv-item-action archive ${n.archived ? "active" : ""}" data-action="archive" title="${archiveTitle}"><fb-icon name="archive"></fb-icon></button>
                        <button class="nv-item-action delete" data-action="delete" title="Delete"><fb-icon name="trash"></fb-icon></button>
                    </div>
                </div>
            `;
        }).join("");

        list.querySelectorAll(".nv-item").forEach(el => {
            el.addEventListener("click", (e) => {
                if (e.target.closest(".nv-item-action")) return;
                this.select(el.dataset.id);
            });
            el.querySelectorAll(".nv-item-action").forEach(btn => {
                btn.addEventListener("click", (e) => {
                    e.stopPropagation();
                    const id = el.dataset.id;
                    switch (btn.dataset.action) {
                        case "pin":     this.togglePinnedById(id);    break;
                        case "archive": this.toggleArchivedById(id);  break;
                        case "delete":  this.deleteById(id);          break;
                    }
                });
            });
        });
    }

    async select(id) {
        if (this.selectedId === id) return;
        // Persist any pending edits on the outgoing note BEFORE we swap the
        // editor's DOM values — otherwise the pending debounced save would fire
        // against the incoming note's values.
        await this.flushSave();
        this.selectedId = id;
        const note = this.notes.find(n => n.id === id);
        if (!note) return;
        this.querySelector("#editor-empty").hidden  = true;
        this.querySelector("#editor").hidden        = false;
        this.querySelector("#editor-footer").hidden = false;
        this.querySelector("#title").value   = note.title   || "";
        this.querySelector("#content").value = note.content || "";
        this.querySelector("#timestamp").textContent = this.formatFullTimestamp(note.updatedAt);
        this._applyReadOnly(note);
        this.updateToolbar(note);
        // Resize the textarea to its content after the value is set. Defer to
        // the next frame so layout has caught up with the new value.
        requestAnimationFrame(() => this.autosizeContent());
        this.renderList();
    }

    /** Toggle editor inputs + footer pill based on whether the note is archived. */
    _applyReadOnly(note) {
        const ro = !!note.archived;
        const editor = this.querySelector("#editor");
        editor.classList.toggle("readonly", ro);
        const title   = this.querySelector("#title");
        const content = this.querySelector("#content");
        title.toggleAttribute("readonly", ro);
        content.toggleAttribute("readonly", ro);
        this.querySelector("#archived-pill").hidden = !ro;
    }

    clearSelection() {
        this.selectedId = null;
        this.querySelector("#editor-empty").hidden  = false;
        this.querySelector("#editor").hidden        = true;
        this.querySelector("#editor-footer").hidden = true;
        this.querySelector("#timestamp").textContent = "";
        fb.toolbar.clear();
    }

    async saveSelected() {
        if (!this.selectedId) return;
        // Any debounced save firing is equivalent to an explicit flush.
        clearTimeout(this._saveDebounce);
        this._saveDebounce = null;
        const note = this.notes.find(n => n.id === this.selectedId);
        if (!note) return;
        const newTitle   = this.querySelector("#title").value;
        const newContent = this.querySelector("#content").value;
        if (newTitle === note.title && newContent === note.content) return;
        note.title   = newTitle;
        note.content = newContent;
        try {
            await fb.api.notes.update(note.id, note);
            note.updatedAt = new Date().toISOString();
            this.querySelector("#timestamp").textContent = this.formatFullTimestamp(note.updatedAt);
            // Update just this row in place — a full renderList() here would
            // wipe the list DOM mid-click and cause the user's click on
            // another row to be dropped (mousedown/mouseup land on different
            // elements so the browser doesn't fire the click). Re-sort on
            // next explicit render (select, delete, archive, pin).
            this._updateRowInPlace(note);
        } catch (err) {
            console.error("[fb-notes-view] update failed:", err);
        }
    }

    /** Refresh a single row's title/date/snippet without wiping the list DOM. */
    _updateRowInPlace(note) {
        const row = this.querySelector(`.nv-item[data-id="${note.id}"]`);
        if (!row) return;
        const titleEl   = row.querySelector(".nv-item-title");
        const dateEl    = row.querySelector(".nv-item-date");
        const snippetEl = row.querySelector(".nv-item-snippet");
        if (titleEl) titleEl.textContent = note.title || "Untitled";
        if (dateEl)  dateEl.textContent  = this.formatDate(note.updatedAt);
        if (snippetEl) {
            const snippet = (note.content || "").replace(/\s+/g, " ").slice(0, 80);
            snippetEl.textContent = snippet || "No additional text";
        }
    }

    /** Toolbar-triggered delegates that just dispatch to the id-based methods. */
    togglePinned()    { if (this.selectedId) this.togglePinnedById(this.selectedId); }
    deleteSelected()  { if (this.selectedId) this.deleteById(this.selectedId); }

    async togglePinnedById(id) {
        // If the target is the active editor, flush text edits first so they
        // ride in the same PUT as the pinned flip (one round-trip, no clobber).
        if (id === this.selectedId) await this.flushSave();
        const note = this.notes.find(n => n.id === id);
        if (!note) return;
        note.pinned = !note.pinned;
        try {
            await fb.api.notes.update(note.id, note);
            if (id === this.selectedId) this.updateToolbar(note);
            this.renderList();
        } catch (err) {
            console.error("[fb-notes-view] pin toggle failed:", err);
            note.pinned = !note.pinned;
        }
    }

    async toggleArchivedById(id) {
        if (id === this.selectedId) await this.flushSave();
        const note = this.notes.find(n => n.id === id);
        if (!note) return;
        note.archived = !note.archived;
        try {
            await fb.api.notes.update(note.id, note);
            // If we archived the currently-open note and archive view is off,
            // it just disappeared from the list — clear the editor too.
            if (id === this.selectedId && note.archived && !this.showArchived) {
                this.clearSelection();
            } else if (id === this.selectedId) {
                // Still visible (either unarchived, or archived with
                // showArchived on) — refresh the editor's read-only state.
                this._applyReadOnly(note);
            }
            this.renderList();
        } catch (err) {
            console.error("[fb-notes-view] archive toggle failed:", err);
            note.archived = !note.archived;
        }
    }

    async deleteById(id) {
        const note = this.notes.find(n => n.id === id);
        if (!note) return;

        // Offer Archive as the safer alternative, unless the note is already
        // archived (then it'd be a no-op and clutters the dialog).
        const buttons = [{ action: "cancel", label: "Cancel", kind: "default" }];
        if (!note.archived) {
            buttons.push({ action: "archive", label: "Archive", kind: "primary" });
        }
        // armAfterMs gives the user 2s to read before Delete becomes the
        // Enter default — see spec 2026-04-20-fb-dialog-design.md.
        buttons.push({ action: "delete", label: "Delete", kind: "destructive", armAfterMs: 2000 });

        const message = note.archived
            ? "This note will be permanently deleted."
            : "This note will be permanently deleted. Archive it instead to keep it hidden but recoverable.";

        const result = await fb.dialog.confirm({ title: "Delete this note?", message, buttons });

        if (result === "archive") return this.toggleArchivedById(id);
        if (result !== "delete") return;

        // Cancel any pending autosave for this note so it doesn't re-create it
        // after the DELETE round-trip.
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

    async createNote() {
        try {
            const created = await fb.api.notes.create({ title: "", content: "" });
            this.notes.unshift(created);
            // Let select() set selectedId so its dedupe guard doesn't short-circuit.
            await this.select(created.id);
            this.querySelector("#title").focus();
        } catch (err) {
            console.error("[fb-notes-view] create failed:", err);
        }
    }

    formatDate(iso) {
        if (!iso) return "";
        const d = new Date(iso);
        const now = new Date();
        const sameDay = d.toDateString() === now.toDateString();
        const yesterday = new Date(now); yesterday.setDate(now.getDate() - 1);
        const isYesterday = d.toDateString() === yesterday.toDateString();
        const sameYear = d.getFullYear() === now.getFullYear();
        if (sameDay)     return d.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
        if (isYesterday) return "Yesterday";
        if (sameYear)    return d.toLocaleDateString(undefined, { month: "short", day: "numeric" });
        return d.toLocaleDateString(undefined, { year: "2-digit", month: "numeric", day: "numeric" });
    }

    formatFullTimestamp(iso) {
        if (!iso) return "";
        const d = new Date(iso);
        return d.toLocaleDateString(undefined, { year: "numeric", month: "long", day: "numeric" })
             + " at "
             + d.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
    }
}

function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[c]));
}

customElements.define("fb-notes-view", FbNotesView);
fb.router.register("#/notes", "fb-notes-view", { label: "Notes", icon: "note" });
