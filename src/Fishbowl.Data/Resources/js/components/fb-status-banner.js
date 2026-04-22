/**
 * <fb-status-banner>  —  Inline, non-modal status message.
 *
 * Sits where you place it in the DOM (typically at the top of a form panel),
 * stays hidden until something goes wrong, shows a tinted strip with a short
 * message. No modal overlay, no auto-dismiss — callers decide when to clear.
 *
 * Usage:
 *   <fb-status-banner id="form-status"></fb-status-banner>
 *   ...
 *   formStatus.show("Name is required.", "error");
 *   formStatus.show("Key saved.", "success");
 *   formStatus.hide();
 *
 * Attributes (declarative equivalent of the JS API):
 *   kind     — "error" (default) | "info" | "warn" | "success"
 *   message  — the text to display
 *   open     — presence makes the banner visible
 *
 * Shadow DOM so the styling can't be accidentally overridden by a parent
 * view's CSS, and so modders can drop `<fb-status-banner>` into their own
 * views without importing the tokens themselves.
 */
class FbStatusBanner extends HTMLElement {
    static get observedAttributes() { return ["kind", "message", "open"]; }

    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    connectedCallback() {
        if (!this.shadowRoot.firstChild) this._render();
        this._sync();
    }

    attributeChangedCallback() {
        if (this.shadowRoot.firstChild) this._sync();
    }

    // Imperative API. Most callers will use these rather than fiddling with
    // attributes — it reads more naturally at the call site.
    show(message, kind = "error") {
        if (message != null) this.setAttribute("message", String(message));
        this.setAttribute("kind", kind);
        this.setAttribute("open", "");
    }

    hide() {
        this.removeAttribute("open");
    }

    _render() {
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    display: none;
                    margin: 0 0 14px;
                    font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
                }
                :host([open]) { display: block; }

                .banner {
                    padding: 10px 12px;
                    border-radius: 8px;
                    font-size: 13px;
                    line-height: 1.4;
                    border: 1px solid transparent;
                }

                /* Error — red. Uses the --danger token shared across the app. */
                :host([kind="error"]) .banner,
                :host(:not([kind])) .banner {
                    background: rgba(239, 68, 68, 0.1);
                    border-color: rgba(239, 68, 68, 0.4);
                    color: #fca5a5;
                }

                /* Info — accent blue. */
                :host([kind="info"]) .banner {
                    background: rgba(59, 130, 246, 0.1);
                    border-color: rgba(59, 130, 246, 0.4);
                    color: var(--accent, #3b82f6);
                }

                /* Warn — amber. */
                :host([kind="warn"]) .banner {
                    background: rgba(245, 158, 11, 0.1);
                    border-color: rgba(245, 158, 11, 0.4);
                    color: var(--accent-warm, #f59e0b);
                }

                /* Success — green. Hard-coded; the app has no --success token. */
                :host([kind="success"]) .banner {
                    background: rgba(34, 197, 94, 0.1);
                    border-color: rgba(34, 197, 94, 0.4);
                    color: #86efac;
                }
            </style>
            <div class="banner" role="alert" id="msg"></div>
        `;
    }

    _sync() {
        const el = this.shadowRoot.getElementById("msg");
        if (el) el.textContent = this.getAttribute("message") || "";
    }
}

customElements.define("fb-status-banner", FbStatusBanner);
