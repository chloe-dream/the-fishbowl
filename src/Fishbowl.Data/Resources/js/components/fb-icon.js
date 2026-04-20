/**
 * <fb-icon name="...">
 *
 * Inline SVG icon. Resolves `name` against fb.icons registry (icons.js).
 * Size via CSS custom property --icon-size (default 24px).
 * Color via currentColor (inherits from parent).
 *
 * Attributes:
 *   name — lookup key in fb.icons registry.
 *
 * Example:
 *   <fb-icon name="note"></fb-icon>
 *   <fb-icon name="cube" style="--icon-size: 48px;"></fb-icon>
 */
class FbIcon extends HTMLElement {
    static get observedAttributes() { return ["name"]; }

    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    connectedCallback() { this.render(); }
    attributeChangedCallback() { this.render(); }

    render() {
        const name = this.getAttribute("name");
        const path = (window.fb && fb.icons) ? fb.icons.get(name) : null;

        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    display: inline-flex;
                    width: var(--icon-size, 24px);
                    height: var(--icon-size, 24px);
                    vertical-align: middle;
                }
                svg {
                    width: 100%;
                    height: 100%;
                    display: block;
                }
            </style>
            ${path
                ? `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor"
                        stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                     ${path}
                   </svg>`
                : ``}
        `;
    }
}

customElements.define("fb-icon", FbIcon);
