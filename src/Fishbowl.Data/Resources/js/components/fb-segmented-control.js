/**
 * <fb-segmented-control value="all">
 *   <button data-value="today">Today</button>
 *   <button data-value="week">Week</button>
 *   <button data-value="all">All</button>
 * </fb-segmented-control>
 *
 * Button group; one active at a time. Active state tracked via `value` attribute.
 *
 * Attributes:
 *   value — currently-active data-value.
 *
 * Events:
 *   change — e.detail = new value (string).
 */
class FbSegmentedControl extends HTMLElement {
    static get observedAttributes() { return ["value"]; }

    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    connectedCallback()      { this.render(); this.attach(); }
    attributeChangedCallback() { if (this.shadowRoot.firstChild) this.applyActive(); }

    get value() { return this.getAttribute("value") || ""; }
    set value(v) {
        this.setAttribute("value", v);
        this.dispatchEvent(new CustomEvent("change", { detail: v, bubbles: true, composed: true }));
    }

    render() {
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    display: inline-flex;
                    background: rgba(0, 0, 0, 0.3);
                    border-radius: 8px;
                    padding: 2px;
                    gap: 2px;
                }
                ::slotted(button) {
                    padding: 0.35rem 0.75rem;
                    border: none;
                    background: transparent;
                    color: #cbd5e1;
                    border-radius: 6px;
                    cursor: pointer;
                    font-size: 0.85rem;
                    font-family: inherit;
                    transition: all 0.15s;
                }
                ::slotted(button:hover) { color: #f8fafc; }
                ::slotted(button.active) {
                    background: var(--accent, #3b82f6);
                    color: white;
                }
            </style>
            <slot></slot>
        `;
        this.applyActive();
    }

    applyActive() {
        const value = this.value;
        for (const btn of this.querySelectorAll("button[data-value]")) {
            btn.classList.toggle("active", btn.dataset.value === value);
        }
    }

    attach() {
        this.addEventListener("click", (e) => {
            const btn = e.target.closest("button[data-value]");
            if (!btn) return;
            this.value = btn.dataset.value;
        });
    }
}

customElements.define("fb-segmented-control", FbSegmentedControl);
