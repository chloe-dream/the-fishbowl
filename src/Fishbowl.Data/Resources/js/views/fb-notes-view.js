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
    }

    async connectedCallback() {
        this.render();
        await this.loadNotes();
    }

    disconnectedCallback() {
        // Router already clears on swap, but guard against any other unmount.
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
                fb-notes-view .nv-item-title-row fb-icon {
                    --icon-size: 12px;
                    color: var(--accent-warm);
                    flex-shrink: 0;
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
                    flex: 1;
                    width: 100%;
                    min-height: 300px;
                    background: none;
                    border: none;
                    color: var(--text);
                    font-family: inherit;
                    font-size: 15px;
                    line-height: 1.6;
                    outline: none;
                    resize: none;
                    padding: 0;
                }
                fb-notes-view .nv-content-input::placeholder {
                    color: var(--text-muted);
                    opacity: 0.4;
                }
                fb-notes-view .nv-editor {
                    flex: 1;
                    display: flex;
                    flex-direction: column;
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
        this.querySelector("#title").addEventListener("blur",   () => this.saveSelected());
        this.querySelector("#content").addEventListener("blur", () => this.saveSelected());
        // Pin + trash actions live in fb-nav's toolbar now; see updateToolbar().
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
            return `
                <div class="nv-item ${isSelected ? "selected" : ""}" data-id="${n.id}">
                    <div class="nv-item-title-row">
                        <span class="nv-item-title">${escapeHtml(n.title || "Untitled")}</span>
                        ${n.pinned ? `<fb-icon name="pin"></fb-icon>` : ""}
                    </div>
                    <div class="nv-item-preview">
                        <span class="nv-item-date">${date}</span>
                        <span class="nv-item-snippet">${escapeHtml(snippet || "No additional text")}</span>
                    </div>
                </div>
            `;
        }).join("");

        list.querySelectorAll(".nv-item").forEach(el => {
            el.addEventListener("click", () => this.select(el.dataset.id));
        });
    }

    select(id) {
        this.selectedId = id;
        const note = this.notes.find(n => n.id === id);
        if (!note) return;
        this.querySelector("#editor-empty").hidden  = true;
        this.querySelector("#editor").hidden        = false;
        this.querySelector("#editor-footer").hidden = false;
        this.querySelector("#title").value   = note.title   || "";
        this.querySelector("#content").value = note.content || "";
        this.querySelector("#timestamp").textContent = this.formatFullTimestamp(note.updatedAt);
        this.updateToolbar(note);
        this.renderList();
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
            this.renderList();
        } catch (err) {
            console.error("[fb-notes-view] update failed:", err);
        }
    }

    async togglePinned() {
        if (!this.selectedId) return;
        const note = this.notes.find(n => n.id === this.selectedId);
        if (!note) return;
        note.pinned = !note.pinned;
        try {
            await fb.api.notes.update(note.id, note);
            this.updateToolbar(note);  // reflect new pinned state in the nav button
            this.renderList();
        } catch (err) {
            console.error("[fb-notes-view] pin toggle failed:", err);
            note.pinned = !note.pinned; // revert optimistic change
        }
    }

    async createNote() {
        try {
            const created = await fb.api.notes.create({ title: "", content: "" });
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
            this.clearSelection();
            this.renderList();
        } catch (err) {
            console.error("[fb-notes-view] delete failed:", err);
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
