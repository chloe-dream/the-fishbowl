/**
 * <fb-tag-chip name="work" color="blue" [removable] [selected] [clickable]>
 *
 * A coloured pill. The colour is a palette slot ("blue", "orange", …) that
 * resolves to var(--tag-<slot>). Background is a tinted color-mix so the slot
 * works in both light and dark themes without a second token per slot.
 *
 * Attributes:
 *   name       — display label.
 *   color      — palette slot. Defaults to "gray" if unknown.
 *   removable  — shows an × button. Click emits 'tag-remove' (bubbles, composed).
 *   selected   — filter-strip "active" state; brighter ring + saturated bg.
 *   clickable  — adds pointer cursor + hover affordance (filter-strip mode).
 *
 * Events:
 *   tag-remove — e.detail = { name } (only when [removable] is set).
 */
class FbTagChip extends HTMLElement {
    static get observedAttributes() { return ["name", "color", "removable", "selected", "clickable"]; }

    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    connectedCallback() {
        if (!this.shadowRoot.firstChild) this._render();
        else this._refresh();
    }

    attributeChangedCallback() {
        if (this.shadowRoot.firstChild) this._refresh();
    }

    _render() {
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    --chip-color: var(--tag-gray, #64748b);
                    display: inline-flex;
                    align-items: center;
                    gap: 4px;
                    padding: 3px 9px;
                    border-radius: 999px;
                    font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
                    font-size: 11px;
                    font-weight: 600;
                    letter-spacing: 0.01em;
                    line-height: 1;
                    background: color-mix(in srgb, var(--chip-color) 18%, transparent);
                    color: var(--chip-color);
                    border: 1px solid color-mix(in srgb, var(--chip-color) 35%, transparent);
                    user-select: none;
                    transition: background 120ms, border-color 120ms, transform 80ms;
                }
                :host([clickable]) { cursor: pointer; }
                :host([clickable]:hover) {
                    background: color-mix(in srgb, var(--chip-color) 30%, transparent);
                    border-color: color-mix(in srgb, var(--chip-color) 55%, transparent);
                }
                :host([selected]) {
                    background: color-mix(in srgb, var(--chip-color) 40%, transparent);
                    border-color: var(--chip-color);
                    color: #fff;
                    box-shadow: 0 0 0 1px color-mix(in srgb, var(--chip-color) 60%, transparent);
                }
                .name { white-space: nowrap; }
                .x {
                    display: none;
                    align-items: center;
                    justify-content: center;
                    width: 14px;
                    height: 14px;
                    border-radius: 50%;
                    background: transparent;
                    border: none;
                    color: inherit;
                    cursor: pointer;
                    padding: 0;
                    margin-left: 2px;
                    margin-right: -3px;
                    opacity: 0.6;
                    transition: opacity 100ms, background 100ms;
                    font: inherit;
                }
                :host([removable]) .x { display: inline-flex; }
                .x:hover {
                    opacity: 1;
                    background: color-mix(in srgb, var(--chip-color) 40%, transparent);
                }
                .x svg { width: 10px; height: 10px; }
            </style>
            <span class="name"></span>
            <button type="button" class="x" title="Remove" aria-label="Remove tag">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round">
                    <line x1="18" y1="6" x2="6" y2="18"/>
                    <line x1="6" y1="6" x2="18" y2="18"/>
                </svg>
            </button>
        `;
        this.shadowRoot.querySelector(".x").addEventListener("click", (e) => {
            e.stopPropagation();
            this.dispatchEvent(new CustomEvent("tag-remove", {
                detail: { name: this.getAttribute("name") || "" },
                bubbles: true, composed: true,
            }));
        });
        this._refresh();
    }

    _refresh() {
        const name = this.getAttribute("name") || "";
        const color = this.getAttribute("color") || "gray";
        const nameEl = this.shadowRoot.querySelector(".name");
        if (nameEl) nameEl.textContent = name;
        this.style.setProperty("--chip-color", `var(--tag-${color}, var(--tag-gray, #64748b))`);
    }
}

customElements.define("fb-tag-chip", FbTagChip);
