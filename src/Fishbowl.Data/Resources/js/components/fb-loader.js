/**
 * <fb-loader>
 *
 * Full-screen blocking overlay with two concentric spinning rings
 * (blue + orange, matching the logo mark). Used for heavy processing.
 *
 * Methods:
 *   show(title, subtitle)  — displays overlay
 *   hide()                 — fades out over 300ms
 */
class FbLoader extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    connectedCallback() { this.render(); }

    show(title = "Loading...", subtitle = "") {
        this.render(title, subtitle);
        const overlay = this.shadowRoot.querySelector(".overlay");
        if (overlay) {
            overlay.style.display = "flex";
            requestAnimationFrame(() => overlay.style.opacity = "1");
        }
    }

    hide() {
        const overlay = this.shadowRoot.querySelector(".overlay");
        if (!overlay) return;
        overlay.style.opacity = "0";
        setTimeout(() => { overlay.style.display = "none"; }, 300);
    }

    render(title = "", subtitle = "") {
        this.shadowRoot.innerHTML = `
            <style>
                .overlay {
                    position: fixed;
                    inset: 0;
                    background: rgba(10, 10, 10, 0.85);
                    backdrop-filter: blur(8px);
                    -webkit-backdrop-filter: blur(8px);
                    display: none;
                    flex-direction: column;
                    align-items: center;
                    justify-content: center;
                    z-index: 10000;
                    opacity: 0;
                    transition: opacity 0.3s;
                }
                .spinner {
                    position: relative;
                    width: 64px;
                    height: 64px;
                    margin-bottom: 1.5rem;
                }
                .ring {
                    position: absolute;
                    inset: 0;
                    border: 3px solid transparent;
                    border-radius: 50%;
                }
                .ring.outer {
                    border-top-color: var(--accent, #3b82f6);
                    animation: spin 1.2s linear infinite;
                }
                .ring.inner {
                    inset: 8px;
                    border-top-color: var(--accent-warm, #f97316);
                    animation: spin 0.9s linear infinite reverse;
                }
                @keyframes spin {
                    to { transform: rotate(360deg); }
                }
                .title {
                    font-family: 'Outfit', sans-serif;
                    font-weight: 700;
                    color: #f8fafc;
                    font-size: 1.1rem;
                }
                .sub {
                    margin-top: 0.3rem;
                    color: #64748b;
                    font-size: 0.85rem;
                }
            </style>
            <div class="overlay">
                <div class="spinner">
                    <div class="ring outer"></div>
                    <div class="ring inner"></div>
                </div>
                <div class="title">${escapeHtml(title)}</div>
                ${subtitle ? `<div class="sub">${escapeHtml(subtitle)}</div>` : ""}
            </div>
        `;
    }
}

function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[c]));
}

customElements.define("fb-loader", FbLoader);
