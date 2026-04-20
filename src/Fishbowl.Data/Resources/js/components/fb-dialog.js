/**
 * <fb-dialog>  —  Modal confirm dialog.
 *
 * Shadow-DOM, glass-styled, centered overlay. General primitive; the common
 * "ask a question, get an answer" case is wrapped by `fb.dialog.confirm()`.
 *
 * Usage:
 *   const dlg = document.createElement("fb-dialog");
 *   dlg.setAttribute("title", "Delete?");
 *   dlg.buttons = [
 *     { action: "cancel", label: "Cancel", kind: "default" },
 *     { action: "delete", label: "Delete", kind: "destructive", armAfterMs: 2000 },
 *   ];
 *   dlg.append(Object.assign(document.createElement("p"), { textContent: "…" }));
 *   document.body.appendChild(dlg);
 *   dlg.addEventListener("fb-dialog:action", e => console.log(e.detail.action));
 *   dlg.open();
 *
 * Keyboard:
 *   - Escape            → close with action=null.
 *   - Enter             → activates focused button (native).
 *   - Tab / Shift-Tab   → cycles between buttons (focus trap).
 *
 * Arming:
 *   - A button with armAfterMs waits N ms, then receives focus so Enter acts.
 *   - If the user's pointer enters any other button before the arm fires, the
 *     timer is cancelled — we don't steal focus from an actively-interacting
 *     user.
 */
class FbDialog extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: "open" });
        this.buttons = [];
        this._armTimer = null;
        this._resolved = false;
        this._onKeydown = this._onKeydown.bind(this);
    }

    static get observedAttributes() { return ["title"]; }

    connectedCallback() {
        if (!this.shadowRoot.firstChild) this._render();
    }

    attributeChangedCallback() {
        if (this.shadowRoot.firstChild) this._renderHeader();
    }

    open() {
        this._resolved = false;
        this._render();
        this.setAttribute("open", "");
        // Host focus so keydown routes here even before any button is focused.
        this.focus();
        document.addEventListener("keydown", this._onKeydown, true);
        this._startArmTimer();
        this.dispatchEvent(new CustomEvent("fb-dialog:open"));
    }

    close(action = null) {
        if (this._resolved) return;
        this._resolved = true;
        this._cancelArmTimer();
        document.removeEventListener("keydown", this._onKeydown, true);
        this.removeAttribute("open");
        this.dispatchEvent(new CustomEvent("fb-dialog:action", { detail: { action } }));
    }

    _onKeydown(e) {
        if (e.key === "Escape") {
            e.stopPropagation();
            e.preventDefault();
            this.close(null);
            return;
        }
        if (e.key === "Tab") {
            // Focus trap across the dialog's own buttons.
            const btns = Array.from(this.shadowRoot.querySelectorAll(".btn"));
            if (btns.length === 0) return;
            const active = this.shadowRoot.activeElement;
            const idx = btns.indexOf(active);
            let next;
            if (e.shiftKey) next = btns[(idx <= 0 ? btns.length : idx) - 1];
            else            next = btns[(idx + 1) % btns.length];
            e.preventDefault();
            next.focus();
        }
    }

    _startArmTimer() {
        const armed = this.buttons.findIndex(b => b.armAfterMs > 0);
        if (armed < 0) return;
        const ms = this.buttons[armed].armAfterMs;
        this._armTimer = setTimeout(() => {
            this._armTimer = null;
            const btn = this.shadowRoot.querySelectorAll(".btn")[armed];
            if (!btn) return;
            // .armed class gives the explicit visual cue — :focus-visible
            // won't fire reliably for programmatic focus (Chrome's heuristic
            // hides the ring when focus is set by script without a prior
            // keyboard event).
            btn.classList.add("armed");
            btn.focus();
        }, ms);
    }

    _cancelArmTimer() {
        if (this._armTimer != null) {
            clearTimeout(this._armTimer);
            this._armTimer = null;
        }
    }

    _render() {
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    position: fixed;
                    inset: 0;
                    z-index: 20000;
                    display: none;
                    align-items: center;
                    justify-content: center;
                    font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
                    outline: none;
                }
                :host([open]) { display: flex; }

                .backdrop {
                    position: absolute;
                    inset: 0;
                    background: rgba(0, 0, 0, 0.5);
                    backdrop-filter: blur(8px);
                    -webkit-backdrop-filter: blur(8px);
                    opacity: 0;
                    animation: fade-in 150ms ease-out forwards;
                }

                .panel {
                    position: relative;
                    width: 420px;
                    max-width: calc(100vw - 32px);
                    background: rgba(30, 41, 59, 0.75);
                    backdrop-filter: blur(20px) saturate(180%);
                    -webkit-backdrop-filter: blur(20px) saturate(180%);
                    border: 1px solid rgba(255, 255, 255, 0.1);
                    border-radius: 16px;
                    box-shadow:
                        0 20px 50px rgba(0, 0, 0, 0.5),
                        inset 0 0 0 1px rgba(255, 255, 255, 0.05);
                    opacity: 0;
                    transform: scale(0.96);
                    animation: pop-in 150ms ease-out forwards;
                    color: var(--text, #f8fafc);
                }

                .title {
                    font-family: 'Outfit', sans-serif;
                    font-weight: 700;
                    font-size: 1.05rem;
                    letter-spacing: -0.01em;
                    padding: 20px 20px 0;
                    color: var(--text, #f8fafc);
                }

                .body {
                    padding: 8px 20px 20px;
                    font-size: 0.9rem;
                    line-height: 1.5;
                    color: var(--text-muted, #94a3b8);
                }
                .body ::slotted(p) { margin: 0; }
                .body ::slotted(p + p) { margin-top: 8px; }

                .actions {
                    display: flex;
                    justify-content: flex-end;
                    gap: 8px;
                    padding: 12px 20px 20px;
                }

                /* Shared language for all three kinds: subtle tinted bg,
                   colored text, matching-color border on hover. The only
                   difference between kinds is the accent hue. */
                .btn {
                    font: 600 0.85rem 'Inter', sans-serif;
                    padding: 8px 16px;
                    border-radius: 8px;
                    cursor: pointer;
                    border: 1px solid transparent;
                    background: rgba(255, 255, 255, 0.06);
                    color: var(--text, #f8fafc);
                    transition: background 120ms, color 120ms, border-color 120ms, box-shadow 120ms;
                    outline: none;
                }
                .btn:hover {
                    background: rgba(255, 255, 255, 0.14);
                    border-color: rgba(255, 255, 255, 0.18);
                }
                .btn:focus-visible {
                    outline: 2px solid var(--accent, #3b82f6);
                    outline-offset: 2px;
                }

                .btn.primary {
                    background: rgba(59, 130, 246, 0.18);
                    color: var(--accent, #3b82f6);
                    border-color: rgba(59, 130, 246, 0.35);
                }
                .btn.primary:hover {
                    background: rgba(59, 130, 246, 0.32);
                    color: #fff;
                    border-color: var(--accent, #3b82f6);
                }

                .btn.destructive {
                    background: rgba(239, 68, 68, 0.12);
                    color: var(--danger, #ef4444);
                    border-color: rgba(239, 68, 68, 0.3);
                }
                .btn.destructive:hover {
                    background: rgba(239, 68, 68, 0.26);
                    color: #fff;
                    border-color: var(--danger, #ef4444);
                }
                .btn.destructive:focus-visible {
                    outline-color: var(--danger, #ef4444);
                }

                /* .armed is added by the arming timer so the destructive
                   button has an unmistakable cue independent of
                   :focus-visible (which JS focus can't reliably trigger). */
                .btn.armed {
                    background: rgba(239, 68, 68, 0.26);
                    color: #fff;
                    border-color: var(--danger, #ef4444);
                    box-shadow: 0 0 0 3px rgba(239, 68, 68, 0.28);
                }

                @keyframes fade-in {
                    to { opacity: 1; }
                }
                @keyframes pop-in {
                    to { opacity: 1; transform: scale(1); }
                }
            </style>
            <div class="backdrop" part="backdrop"></div>
            <div class="panel" part="panel" role="dialog" aria-modal="true">
                <div class="title" id="dlg-title"></div>
                <div class="body"><slot></slot></div>
                <div class="actions"></div>
            </div>
        `;
        this._renderHeader();
        this._renderButtons();
        this.shadowRoot.querySelector(".backdrop").addEventListener("click", () => this.close(null));
    }

    _renderHeader() {
        const el = this.shadowRoot.getElementById("dlg-title");
        if (el) el.textContent = this.getAttribute("title") || "";
    }

    _renderButtons() {
        const row = this.shadowRoot.querySelector(".actions");
        row.innerHTML = "";
        this.buttons.forEach((spec) => {
            const btn = document.createElement("button");
            btn.className = "btn" + (spec.kind && spec.kind !== "default" ? " " + spec.kind : "");
            btn.type = "button";
            btn.textContent = spec.label;
            btn.addEventListener("click", () => this.close(spec.action));
            btn.addEventListener("mouseenter", () => {
                // Any pointer interaction with a *different* button cancels the
                // arm timer so we don't yank focus out from under the user.
                if (this._armTimer != null) {
                    const armedIdx = this.buttons.findIndex(b => b.armAfterMs > 0);
                    const myIdx = this.buttons.indexOf(spec);
                    if (myIdx !== armedIdx) this._cancelArmTimer();
                }
            });
            row.appendChild(btn);
        });
    }
}

customElements.define("fb-dialog", FbDialog);
