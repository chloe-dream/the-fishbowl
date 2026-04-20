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
