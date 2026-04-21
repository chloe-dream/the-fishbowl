/**
 * <fb-tag-manage-dialog>
 *
 * Inline tag administration. Lists every tag with an inline rename input, a
 * 10-swatch picker, and a delete button. Uses fb-window as the shell so the
 * red close button + bottom-right resize handle match every other floating
 * panel in the app (Profile, etc.) — no reinvented chrome.
 *
 * Usage:
 *   const dlg = document.createElement("fb-tag-manage-dialog");
 *   document.body.appendChild(dlg);
 *   dlg.addEventListener("tags-changed", () => view.refresh());
 *   dlg.open();
 *
 * Events:
 *   tags-changed — fired once on close if any tag was renamed/recoloured/deleted.
 */
class FbTagManageDialog extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: "open" });
        this._dirty = false;
    }

    connectedCallback() {
        if (!this.shadowRoot.firstChild) this._render();
    }

    async open() {
        this._dirty = false;
        await this._refreshList();
        this._window.open();
    }

    _render() {
        this.shadowRoot.innerHTML = `
            <style>
                :host { display: contents; }
                /* fb-window's .content already scrolls + pads — .list just
                   lays out the rows. No nested scrollbar, no inner padding. */
                .list { }
                .list:empty::after {
                    content: "no tags yet";
                    display: block;
                    text-align: center;
                    color: var(--text-muted, #64748b);
                    font-size: 12px;
                    padding: 24px 0;
                }
                .row {
                    display: flex;
                    align-items: center;
                    gap: 10px;
                    padding: 8px 4px;
                    border-radius: 8px;
                    flex-wrap: nowrap;
                }
                .row .name-input { min-width: 160px; }
                .row + .row { border-top: 1px solid rgba(255,255,255,0.04); }
                .name-input {
                    flex: 1;
                    background: rgba(0, 0, 0, 0.3);
                    color: var(--text, #f8fafc);
                    border: 1px solid var(--border, rgba(255, 255, 255, 0.08));
                    border-radius: 6px;
                    padding: 4px 8px;
                    font: inherit;
                    font-size: 12px;
                    outline: none;
                }
                .name-input:focus { border-color: var(--accent, #3b82f6); }
                .name-input.invalid { border-color: var(--danger, #ef4444); }
                .swatches {
                    display: flex;
                    gap: 3px;
                }
                .swatch {
                    width: 16px; height: 16px;
                    border-radius: 50%;
                    border: 2px solid transparent;
                    cursor: pointer;
                    transition: transform 80ms;
                }
                .swatch:hover { transform: scale(1.18); }
                .swatch.active { border-color: rgba(255,255,255,0.85); }
                .count {
                    font-size: 10px;
                    color: var(--text-muted, #64748b);
                    width: 28px;
                    text-align: right;
                    flex-shrink: 0;
                }
                .del {
                    background: transparent;
                    border: none;
                    color: var(--text-muted, #64748b);
                    cursor: pointer;
                    padding: 4px 6px;
                    border-radius: 6px;
                    transition: color 100ms, background 100ms;
                }
                .del:hover { color: var(--danger, #ef4444); background: rgba(239, 68, 68, 0.12); }
                .del svg { width: 14px; height: 14px; display: block; }
                /* System tags: name is locked (load-bearing for workflows);
                   delete is blocked in backend. Make the UI match so users
                   don't try. Colour stays editable — that's presentation. */
                .name-input.system {
                    background: transparent;
                    border-color: transparent;
                    color: var(--text-muted, #64748b);
                    cursor: default;
                    font-style: italic;
                }
                .sys-badge {
                    font-size: 10px;
                    color: var(--text-muted, #64748b);
                    text-transform: uppercase;
                    letter-spacing: 0.04em;
                    padding: 2px 6px;
                    border: 1px solid var(--border, rgba(255,255,255,0.08));
                    border-radius: 999px;
                    margin-right: 4px;
                    flex-shrink: 0;
                }
            </style>
            <fb-window
                id="win"
                title="Manage tags"
                width="640px"
                height="480px"
                top="80px"
                left="calc(50vw - 320px)">
                <div class="list" id="list"></div>
            </fb-window>
        `;
        this._window = this.shadowRoot.getElementById("win");
        this._listEl = this.shadowRoot.getElementById("list");

        // fb-window fires 'close' when its red close button is clicked. Emit
        // the tags-changed signal here so views can refresh in one shot
        // rather than after every per-row mutation.
        this._window.addEventListener("close", () => {
            if (this._dirty) {
                fb.tags.invalidate();
                this.dispatchEvent(new CustomEvent("tags-changed", { bubbles: true, composed: true }));
            }
        });
    }

    async _refreshList() {
        let tags = [];
        try {
            tags = await fb.api.tags.list();
        } catch {
            tags = [];
        }
        this._listEl.innerHTML = "";
        for (const tag of tags) {
            this._listEl.appendChild(this._renderRow(tag));
        }
    }

    _renderRow(tag) {
        const row = document.createElement("div");
        row.className = "row";
        const isSystem = tag.isSystem === true;
        const nameInputHtml = isSystem
            ? `<input class="name-input system" type="text" value="${tag.name}" readonly title="System tag — name is protected"/>`
            : `<input class="name-input" type="text" value="${tag.name}"/>`;
        const delBtnHtml = isSystem
            ? ``
            : `<button type="button" class="del" title="Delete tag" aria-label="Delete">
                   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                        stroke-linecap="round" stroke-linejoin="round">
                       <polyline points="3 6 5 6 21 6"/>
                       <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
                   </svg>
               </button>`;
        const sysBadge = isSystem ? `<span class="sys-badge" title="System tag">system</span>` : ``;

        row.innerHTML = `
            ${sysBadge}
            ${nameInputHtml}
            <div class="swatches">
                ${fb.tags.SLOTS.map(c =>
                    `<button type="button" class="swatch ${c === tag.color ? "active" : ""}"
                             data-color="${c}" title="${c}"
                             style="background:var(--tag-${c})"></button>`
                ).join("")}
            </div>
            <span class="count">${tag.usageCount || 0}</span>
            ${delBtnHtml}
        `;

        const nameInput = row.querySelector(".name-input");
        const swatches = row.querySelectorAll(".swatch");
        const delBtn = row.querySelector(".del");

        let currentName = tag.name;

        // System tag names are load-bearing (backend rejects rename). Skip the
        // rename listener entirely — colour swatches below still bind.
        if (!isSystem) {
            nameInput.addEventListener("blur", async () => {
                const newName = nameInput.value.trim().toLowerCase();
                if (!newName || newName === currentName) {
                    nameInput.value = currentName;
                    nameInput.classList.remove("invalid");
                    return;
                }
                if (!/^[a-z0-9_:-]{1,50}$/.test(newName)) {
                    nameInput.classList.add("invalid");
                    return;
                }
                try {
                    await fb.api.tags.rename(currentName, newName);
                    currentName = newName;
                    this._dirty = true;
                    nameInput.classList.remove("invalid");
                } catch (err) {
                    console.warn("rename failed", err);
                    nameInput.classList.add("invalid");
                }
            });
        }

        swatches.forEach(btn => {
            btn.addEventListener("click", async () => {
                const color = btn.dataset.color;
                try {
                    await fb.api.tags.upsertColor(currentName, color);
                    swatches.forEach(b => b.classList.toggle("active", b === btn));
                    this._dirty = true;
                } catch (err) {
                    console.warn("recolor failed", err);
                }
            });
        });

        // delBtn is absent for system tags (rendered conditionally above).
        delBtn?.addEventListener("click", async () => {
            // Tags in use: confirm first because deleting strips the tag from
            // every referencing note. Unused tags delete silently — there's
            // nothing to lose.
            if ((tag.usageCount || 0) > 0) {
                const noun = tag.usageCount === 1 ? "note" : "notes";
                const result = await fb.dialog.confirm({
                    title: `Delete tag "${currentName}"?`,
                    message: `Used by ${tag.usageCount} ${noun}. Deleting will remove this tag from all of them. The notes themselves stay.`,
                    buttons: [
                        { action: "cancel", label: "Cancel", kind: "default" },
                        { action: "delete", label: "Delete", kind: "destructive", armAfterMs: 1500 }
                    ]
                });
                if (result !== "delete") return;
            }
            try {
                await fb.api.tags.delete(currentName);
                row.remove();
                this._dirty = true;
            } catch (err) {
                console.warn("delete failed", err);
            }
        });

        return row;
    }
}

customElements.define("fb-tag-manage-dialog", FbTagManageDialog);
