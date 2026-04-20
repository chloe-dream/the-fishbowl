/**
 * <fb-nav app-name="NOTES">
 *   <button slot="toolbar">...</button>
 * </fb-nav>
 *
 * Fixed 50px ribbon with glassmorphic background + slide-out 300px panel.
 * Panel nav list is computed from fb.router.routes().
 *
 * Attributes:
 *   app-name — uppercase text after "THE FISHBOWL ·" in the brand area.
 *
 * Slots:
 *   toolbar — right-aligned content inside the ribbon.
 */
class FbNav extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: "open" });
        this.isOpen = false;
    }

    connectedCallback() {
        this.render();
        this.attachHandlers();
    }

    render() {
        const appName = this.getAttribute("app-name") || "";
        const routes = (fb.router?.routes() || []).filter(r => r.hash !== "#/");
        const current = fb.router?.current() || "#/";

        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    position: fixed;
                    top: 0; left: 0; right: 0;
                    z-index: 9999;
                    --accent: #3b82f6;
                    --accent-warm: #f97316;
                }
                .ribbon {
                    height: 50px;
                    display: flex;
                    align-items: center;
                    padding: 0 1rem;
                    background: rgba(15, 23, 42, 0.85);
                    backdrop-filter: blur(12px);
                    -webkit-backdrop-filter: blur(12px);
                    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
                }
                .menu-btn {
                    background: none;
                    border: none;
                    color: #f8fafc;
                    cursor: pointer;
                    padding: 0.25rem 0.5rem;
                    margin-right: 0.5rem;
                    font-size: 1.2rem;
                    border-radius: 6px;
                }
                .menu-btn:hover { background: rgba(255,255,255,0.08); }
                .logo-mark {
                    display: flex; flex-direction: column; gap: 2px;
                    width: 12px; height: 18px; margin-right: 0.75rem;
                }
                .logo-mark .top    { flex: 1; background: var(--accent);      border-radius: 8px 8px 0 0; }
                .logo-mark .bottom { flex: 1; background: var(--accent-warm); border-radius: 0 0 8px 8px; }
                .brand {
                    font-family: 'Outfit', sans-serif;
                    font-weight: 800;
                    font-size: 0.95rem;
                    letter-spacing: 0.08em;
                    color: #f8fafc;
                    display: flex; align-items: center; gap: 0.5rem;
                }
                .brand .sep { color: rgba(255,255,255,0.3); }
                .brand .app-name { color: var(--accent); }
                .spacer { flex: 1; }
                .toolbar { display: flex; gap: 0.5rem; }

                .backdrop {
                    position: fixed;
                    top: 50px; left: 0; right: 0; bottom: 0;
                    background: rgba(0,0,0,0.5);
                    opacity: 0;
                    visibility: hidden;
                    transition: opacity 0.3s, visibility 0.3s;
                    z-index: 1;
                }
                .backdrop.open { opacity: 1; visibility: visible; }

                .panel {
                    position: fixed;
                    top: 50px; left: 0; bottom: 0;
                    width: 300px;
                    background: rgba(15, 23, 42, 0.95);
                    backdrop-filter: blur(20px);
                    -webkit-backdrop-filter: blur(20px);
                    border-right: 1px solid rgba(255,255,255,0.1);
                    transform: translateX(-110%);
                    visibility: hidden;
                    transition: transform 0.5s cubic-bezier(0.77, 0, 0.175, 1), visibility 0.5s;
                    z-index: 2;
                    padding: 1.5rem 0;
                    overflow-y: auto;
                }
                .panel.open {
                    transform: translateX(0);
                    visibility: visible;
                }
                .nav-list { list-style: none; padding: 0; margin: 0; }
                .nav-item {
                    display: flex;
                    align-items: center;
                    gap: 0.75rem;
                    padding: 0.75rem 1.5rem;
                    color: #cbd5e1;
                    text-decoration: none;
                    font-size: 0.95rem;
                    cursor: pointer;
                    transition: background 0.2s;
                }
                .nav-item:hover { background: rgba(255,255,255,0.05); color: #f8fafc; }
                .nav-item.active {
                    background: rgba(59, 130, 246, 0.15);
                    border-left: 3px solid var(--accent);
                    color: var(--accent);
                    padding-left: calc(1.5rem - 3px);
                }
                .nav-item fb-icon { --icon-size: 18px; }
            </style>
            <nav class="ribbon">
                <button class="menu-btn" aria-label="Menu">
                    <fb-icon name="menu"></fb-icon>
                </button>
                <a class="brand" href="#/">
                    <span class="logo-mark"><span class="top"></span><span class="bottom"></span></span>
                    <span>THE FISHBOWL</span>
                    ${appName ? `<span class="sep">·</span><span class="app-name">${appName}</span>` : ``}
                </a>
                <div class="spacer"></div>
                <div class="toolbar"><slot name="toolbar"></slot></div>
            </nav>
            <div class="backdrop"></div>
            <aside class="panel">
                <ul class="nav-list">
                    ${routes.map(r => `
                        <li>
                            <a class="nav-item ${r.hash === current ? "active" : ""}" href="${r.hash}">
                                ${r.icon ? `<fb-icon name="${r.icon}"></fb-icon>` : ""}
                                <span>${r.label}</span>
                            </a>
                        </li>
                    `).join("")}
                </ul>
            </aside>
        `;
    }

    attachHandlers() {
        const menuBtn = this.shadowRoot.querySelector(".menu-btn");
        const backdrop = this.shadowRoot.querySelector(".backdrop");
        const panel = this.shadowRoot.querySelector(".panel");

        const toggle = () => {
            this.isOpen = !this.isOpen;
            backdrop.classList.toggle("open", this.isOpen);
            panel.classList.toggle("open", this.isOpen);
        };
        menuBtn.addEventListener("click", toggle);
        backdrop.addEventListener("click", toggle);

        this._escHandler = (e) => { if (e.key === "Escape" && this.isOpen) toggle(); };
        document.addEventListener("keydown", this._escHandler);

        // Re-render on route change so the active highlight updates.
        this._hashHandler = () => this.render();
        window.addEventListener("hashchange", this._hashHandler);
    }

    disconnectedCallback() {
        if (this._escHandler)  document.removeEventListener("keydown",    this._escHandler);
        if (this._hashHandler) window.removeEventListener("hashchange",   this._hashHandler);
    }
}

customElements.define("fb-nav", FbNav);
