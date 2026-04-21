/**
 * <fb-tag-input>
 *
 * Combobox for editing a note's tag list. Renders the current chips followed
 * by a text input; focus/typing opens a dropdown of matching tags from
 * `fb.tags.all()`. Tab / Enter / comma commit the highlighted suggestion (or
 * the top match when none highlighted). When no existing tag matches, the top
 * option is "Create tag 'foo'" — choosing it opens a 10-swatch picker, calls
 * `fb.api.tags.upsertColor`, then commits the new chip.
 *
 * Keyboard:
 *   - Tab / Enter / ,    commit highlighted/top
 *   - Esc                close dropdown (no commit)
 *   - ArrowDown / Up     move highlight
 *   - Backspace empty    remove last chip
 *
 * Properties:
 *   value   — string[]; current normalised tag names. Reading returns a copy.
 *
 * Events:
 *   change  — e.detail = string[] (new tag list, normalised, deduped).
 */
class FbTagInput extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: "open" });
        this._value = [];
        this._suggestions = [];      // Tag[]
        this._open = false;
        this._highlight = 0;
        this._creating = null;       // pending name awaiting color choice
        this._onDocPointer = this._onDocPointer.bind(this);
    }

    connectedCallback() {
        if (!this.shadowRoot.firstChild) this._render();
        this._loadSuggestions();
        document.addEventListener("pointerdown", this._onDocPointer, true);
        window.addEventListener("fb-tags-invalidated", this._onInvalidated = () => this._loadSuggestions());
        this._onReposition = () => this._positionDropdown();
        window.addEventListener("scroll", this._onReposition, true);
        window.addEventListener("resize", this._onReposition);
    }

    disconnectedCallback() {
        document.removeEventListener("pointerdown", this._onDocPointer, true);
        if (this._onInvalidated) window.removeEventListener("fb-tags-invalidated", this._onInvalidated);
        if (this._onReposition) {
            window.removeEventListener("scroll", this._onReposition, true);
            window.removeEventListener("resize", this._onReposition);
        }
    }

    get value() { return [...this._value]; }
    set value(v) {
        this._value = Array.isArray(v) ? [...new Set(v.filter(Boolean).map(this._normalize).filter(Boolean))] : [];
        this._renderChips();
        this._refreshAddButton();
    }

    async _loadSuggestions() {
        try {
            this._suggestions = await fb.tags.all();
        } catch {
            this._suggestions = [];
        }
        if (this._open) this._renderDropdown();
    }

    _normalize(raw) {
        if (raw == null) return "";
        const trimmed = String(raw).trim().toLowerCase();
        return /^[a-z0-9_:-]{1,50}$/.test(trimmed) ? trimmed : "";
    }

    _render() {
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    display: inline-block;
                    position: relative;
                    font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
                    font-size: 12px;
                    color: var(--text, #f8fafc);
                }
                .row {
                    display: inline-flex;
                    flex-wrap: wrap;
                    align-items: center;
                    gap: 4px;
                }
                input.entry {
                    flex: 1 1 80px;
                    min-width: 80px;
                    background: transparent;
                    color: inherit;
                    border: none;
                    outline: none;
                    font: inherit;
                    padding: 3px 4px;
                }
                input.entry[hidden] { display: none !important; }

                /* Empty-state affordance: a chip-shaped dashed ghost button so
                   it's obvious you can add tags. Replaces the passive
                   placeholder which was easy to miss. */
                button.add-btn {
                    display: inline-flex;
                    align-items: center;
                    gap: 4px;
                    padding: 3px 9px;
                    border-radius: 999px;
                    border: 1px dashed var(--text-muted, #64748b);
                    background: transparent;
                    color: var(--text-muted, #64748b);
                    font: inherit;
                    font-size: 11px;
                    font-weight: 600;
                    cursor: pointer;
                    transition: color 100ms, border-color 100ms, background 100ms;
                }
                button.add-btn:hover {
                    color: var(--text, #f8fafc);
                    border-color: var(--text, #f8fafc);
                    background: rgba(255, 255, 255, 0.04);
                }
                button.add-btn[hidden] { display: none !important; }
                button.add-btn .plus { font-weight: 700; }
                .dropdown {
                    /* position:fixed + JS-positioned coordinates so the
                       dropdown lives outside layout flow (no page scrollbar)
                       and we can flip up/down based on viewport space. */
                    position: fixed;
                    min-width: 220px;
                    max-width: 360px;
                    max-height: 240px;
                    overflow-y: auto;
                    background: rgba(20, 26, 40, 0.96);
                    backdrop-filter: blur(14px) saturate(160%);
                    -webkit-backdrop-filter: blur(14px) saturate(160%);
                    border: 1px solid var(--border, rgba(255, 255, 255, 0.1));
                    border-radius: 10px;
                    padding: 4px;
                    box-shadow: 0 10px 30px rgba(0, 0, 0, 0.4);
                    z-index: 9999;
                }
                :host(:not([open])) .dropdown { display: none; }
                .opt {
                    display: flex;
                    align-items: center;
                    gap: 8px;
                    padding: 6px 8px;
                    border-radius: 6px;
                    cursor: pointer;
                    color: var(--text, #f8fafc);
                }
                .opt:hover, .opt.hl {
                    background: rgba(255, 255, 255, 0.08);
                }
                .opt .swatch {
                    width: 10px;
                    height: 10px;
                    border-radius: 50%;
                    flex-shrink: 0;
                }
                .opt .count {
                    margin-left: auto;
                    font-size: 10px;
                    color: var(--text-muted, #64748b);
                }
                .opt.create {
                    color: var(--accent, #3b82f6);
                    font-weight: 600;
                }
                .opt.create::before {
                    content: "+";
                    width: 14px; text-align: center;
                    color: var(--accent, #3b82f6);
                }
                .picker {
                    display: grid;
                    grid-template-columns: repeat(5, 1fr);
                    gap: 6px;
                    padding: 8px;
                }
                .swatch-btn {
                    width: 28px; height: 28px;
                    border-radius: 50%;
                    border: 2px solid transparent;
                    background: var(--tag-gray);
                    cursor: pointer;
                    transition: transform 100ms, border-color 100ms;
                }
                .swatch-btn:hover { transform: scale(1.1); border-color: rgba(255, 255, 255, 0.5); }
                .picker-header {
                    padding: 8px 10px 4px;
                    font-size: 11px;
                    color: var(--text-muted, #64748b);
                    text-transform: uppercase;
                    letter-spacing: 0.04em;
                }
            </style>
            <div class="row" id="row">
                <button type="button" class="add-btn" id="add-btn">
                    <span class="plus">+</span> Add tag
                </button>
                <input class="entry" id="entry" autocomplete="off" hidden/>
            </div>
            <div class="dropdown" id="dropdown" role="listbox"></div>
        `;
        this._chipsRoot = this.shadowRoot.getElementById("row");
        this._entry = this.shadowRoot.getElementById("entry");
        this._addBtn = this.shadowRoot.getElementById("add-btn");
        this._dropdown = this.shadowRoot.getElementById("dropdown");

        this._addBtn.addEventListener("click", () => {
            // Reveal the input first so focus() lands on a non-hidden element.
            this._addBtn.hidden = true;
            this._entry.hidden = false;
            this._entry.focus();
        });

        this._entry.addEventListener("focus", () => this._openDropdown());
        this._entry.addEventListener("input", () => { this._highlight = 0; this._renderDropdown(); });
        this._entry.addEventListener("keydown", (e) => this._onKey(e));
        this._entry.addEventListener("blur", () => {
            // After the dropdown closes via outside-click, snap back to the
            // ghost button when the input ends up empty + unfocused.
            queueMicrotask(() => this._refreshAddButton());
        });

        this._renderChips();
        this._refreshAddButton();
    }

    /** Show the "+ Add tag" button only when there are no chips, the input is
     *  not focused, and the dropdown is closed. Keeps the affordance visible
     *  for new users without crowding the UI mid-edit. */
    _refreshAddButton() {
        if (!this._addBtn || !this._entry) return;
        const empty = this._value.length === 0;
        const inputFocused = this.shadowRoot.activeElement === this._entry;
        const showButton = empty && !inputFocused && !this._open && this._entry.value === "";
        this._addBtn.hidden = !showButton;
        this._entry.hidden = showButton;
    }

    _renderChips() {
        // Remove old chips only — keep both the input and the add button.
        for (const node of [...this._chipsRoot.children]) {
            if (node === this._entry || node === this._addBtn) continue;
            node.remove();
        }
        for (const name of this._value) {
            const chip = document.createElement("fb-tag-chip");
            chip.setAttribute("name", name);
            chip.setAttribute("color", fb.tags.colorFor(name));
            // Locked-on tags (e.g. source:mcp) keep their × off so the user
            // can't strip provenance. userRemovable defaults to true for any
            // tag not yet in the registry — only explicitly-false flags hide.
            const meta = fb.tags.byName(name);
            if (meta?.userRemovable !== false) {
                chip.setAttribute("removable", "");
                chip.addEventListener("tag-remove", (e) => this._removeChip(e.detail.name));
            }
            this._chipsRoot.insertBefore(chip, this._addBtn);
        }
    }

    _openDropdown() {
        this._open = true;
        this.setAttribute("open", "");
        this._refreshAddButton();
        this._renderDropdown();
        this._positionDropdown();
    }

    _closeDropdown() {
        this._open = false;
        this.removeAttribute("open");
        this._creating = null;
        this._entry.value = "";
        this._refreshAddButton();
    }

    /** Anchor the fixed-position dropdown to the input's current viewport
     *  rect. Opens downward when there's room, otherwise flips up. Avoids
     *  layout-flow tricks that could trigger page scroll on narrow editors. */
    _positionDropdown() {
        if (!this._open || !this._dropdown || !this._entry) return;
        const rect = this._entry.getBoundingClientRect();
        const dropdownMax = 240; // matches max-height
        const margin = 4;
        const spaceBelow = window.innerHeight - rect.bottom;
        const spaceAbove = rect.top;
        const openUp = spaceBelow < dropdownMax + margin && spaceAbove > spaceBelow;
        const top = openUp
            ? Math.max(8, rect.top - Math.min(dropdownMax, spaceAbove - margin) - margin)
            : rect.bottom + margin;
        this._dropdown.style.top = `${top}px`;
        this._dropdown.style.left = `${rect.left}px`;
        this._dropdown.style.maxHeight = `${Math.min(dropdownMax, openUp ? spaceAbove - margin : spaceBelow - margin)}px`;
    }

    _onDocPointer(e) {
        if (!this._open) return;
        if (e.composedPath().includes(this)) return;
        this._closeDropdown();
    }

    _renderDropdown() {
        if (!this._open) return;

        if (this._creating) {
            this._renderColorPicker();
            return;
        }

        const q = this._entry.value.trim().toLowerCase();
        const matches = this._suggestions.filter(t =>
            !this._value.includes(t.name) &&
            t.userAssignable !== false &&
            (q === "" || t.name.includes(q))
        );

        const exact = q && matches.find(t => t.name === q);
        const showCreate = q && !exact && this._normalize(q);

        const opts = [];
        for (const t of matches) {
            opts.push({ kind: "existing", tag: t });
        }
        if (showCreate) {
            opts.push({ kind: "create", name: this._normalize(q) });
        }

        if (opts.length === 0) {
            this._dropdown.innerHTML = `<div class="opt" style="color:var(--text-muted);cursor:default;">no matches</div>`;
            return;
        }

        if (this._highlight >= opts.length) this._highlight = 0;
        this._currentOpts = opts;

        const html = opts.map((o, i) => {
            if (o.kind === "create") {
                return `<div class="opt create ${i === this._highlight ? "hl" : ""}" data-idx="${i}">Create tag "${o.name}"</div>`;
            }
            const color = `var(--tag-${o.tag.color}, var(--tag-gray))`;
            const count = o.tag.usageCount > 0 ? `<span class="count">${o.tag.usageCount}</span>` : "";
            return `<div class="opt ${i === this._highlight ? "hl" : ""}" data-idx="${i}">
                        <span class="swatch" style="background:${color}"></span>
                        <span>${o.tag.name}</span>${count}
                    </div>`;
        }).join("");

        this._dropdown.innerHTML = html;
        this._dropdown.querySelectorAll(".opt[data-idx]").forEach(el => {
            el.addEventListener("mouseenter", () => {
                this._highlight = Number(el.dataset.idx);
                this._dropdown.querySelectorAll(".opt").forEach(o => o.classList.remove("hl"));
                el.classList.add("hl");
            });
            el.addEventListener("mousedown", (e) => {
                // mousedown so the entry's blur doesn't close us first.
                e.preventDefault();
                this._highlight = Number(el.dataset.idx);
                this._commit();
            });
        });
    }

    _renderColorPicker() {
        const slots = fb.tags.SLOTS;
        const hint = `Pick color for "${this._creating}"`;
        const swatches = slots.map(c =>
            `<button type="button" class="swatch-btn" data-color="${c}" style="background:var(--tag-${c})" title="${c}"></button>`
        ).join("");
        this._dropdown.innerHTML = `
            <div class="picker-header">${hint}</div>
            <div class="picker">${swatches}</div>
        `;
        this._dropdown.querySelectorAll(".swatch-btn").forEach(btn => {
            btn.addEventListener("mousedown", async (e) => {
                e.preventDefault();
                const color = btn.dataset.color;
                const name = this._creating;
                this._creating = null;
                try {
                    await fb.api.tags.upsertColor(name, color);
                    fb.tags.invalidate();
                    await this._loadSuggestions();
                } catch (err) {
                    console.warn("Tag create failed", err);
                }
                this._addChip(name);
                this._entry.value = "";
                this._renderDropdown();
                this._entry.focus();
            });
        });
    }

    _onKey(e) {
        if (this._creating) {
            if (e.key === "Escape") { this._creating = null; this._renderDropdown(); e.preventDefault(); }
            return;
        }

        if (e.key === "Backspace" && this._entry.value === "" && this._value.length > 0) {
            this._removeChip(this._value[this._value.length - 1]);
            e.preventDefault();
            return;
        }

        if (!this._open) {
            if (e.key === "ArrowDown") { this._openDropdown(); e.preventDefault(); }
            return;
        }

        if (e.key === "Escape") { this._closeDropdown(); e.preventDefault(); return; }

        const opts = this._currentOpts || [];
        if (e.key === "ArrowDown") {
            this._highlight = Math.min(this._highlight + 1, opts.length - 1);
            this._renderDropdown(); e.preventDefault(); return;
        }
        if (e.key === "ArrowUp") {
            this._highlight = Math.max(this._highlight - 1, 0);
            this._renderDropdown(); e.preventDefault(); return;
        }
        if (e.key === "Enter" || e.key === "Tab" || e.key === ",") {
            if (opts.length === 0) {
                // No suggestions; if input is normalisable, treat as create.
                const n = this._normalize(this._entry.value);
                if (n) {
                    this._creating = n;
                    this._renderDropdown();
                    e.preventDefault();
                }
                return;
            }
            this._commit();
            e.preventDefault();
        }
    }

    _commit() {
        const opt = this._currentOpts?.[this._highlight];
        if (!opt) return;
        if (opt.kind === "create") {
            this._creating = opt.name;
            this._renderDropdown();
            return;
        }
        this._addChip(opt.tag.name);
        this._entry.value = "";
        this._highlight = 0;
        this._renderDropdown();
    }

    _addChip(name) {
        const n = this._normalize(name);
        if (!n || this._value.includes(n)) return;
        this._value = [...this._value, n];
        this._renderChips();
        this._refreshAddButton();
        this._emit();
    }

    _removeChip(name) {
        const next = this._value.filter(t => t !== name);
        if (next.length === this._value.length) return;
        this._value = next;
        this._renderChips();
        this._renderDropdown();
        this._refreshAddButton();
        this._emit();
    }

    _emit() {
        this.dispatchEvent(new CustomEvent("change", {
            detail: [...this._value],
            bubbles: true, composed: true,
        }));
    }
}

customElements.define("fb-tag-input", FbTagInput);
