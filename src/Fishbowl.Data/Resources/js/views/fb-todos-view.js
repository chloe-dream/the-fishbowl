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
