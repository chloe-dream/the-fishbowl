/**
 * <fb-toggle label="Pinned" checked>
 *
 * Custom switch. Fires `change` events with `e.detail` = boolean.
 *
 * Attributes:
 *   label   — text to the left of the switch.
 *   checked — presence = on.
 */
class FbToggle extends HTMLElement {
    static get observedAttributes() { return ["label", "checked"]; }

    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    connectedCallback()      { this.render(); this.attach(); }
    attributeChangedCallback() { if (this.shadowRoot.firstChild) this.render(); }

    get checked() { return this.hasAttribute("checked"); }
    set checked(v) {
        if (v) this.setAttribute("checked", "");
        else   this.removeAttribute("checked");
    }

    render() {
        const label = this.getAttribute("label") || "";
        const on = this.checked;
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    display: flex;
                    align-items: center;
                    justify-content: space-between;
                    padding: 0.25rem 0;
                    cursor: pointer;
                    color: #f8fafc;
                    font-size: 0.875rem;
                }
                .label { user-select: none; }
                .switch {
                    position: relative;
                    width: 28px;
                    height: 14px;
                    background: rgba(255, 255, 255, 0.15);
                    border-radius: 7px;
                    transition: background 0.2s;
                }
                .knob {
                    position: absolute;
                    top: 1px; left: 1px;
                    width: 12px; height: 12px;
                    background: #f8fafc;
                    border-radius: 50%;
                    transition: transform 0.2s, background 0.2s;
                }
                :host([checked]) .switch { background: var(--accent, #3b82f6); }
                :host([checked]) .knob   { transform: translateX(14px); }
            </style>
            <span class="label">${label}</span>
            <div class="switch"><div class="knob"></div></div>
        `;
    }

    attach() {
        this.addEventListener("click", () => {
            this.checked = !this.checked;
            this.dispatchEvent(new CustomEvent("change", {
                detail: this.checked,
                bubbles: true,
                composed: true
            }));
        });
    }
}

customElements.define("fb-toggle", FbToggle);
