/**
 * <fb-log>
 *
 * Structured timestamped entries. Colors: info=green, warn=amber, error=red.
 *
 * Methods:
 *   add(text, level)   — level: "info" | "warn" | "error"
 *   clear()
 *   copy()             — copies all entries to clipboard
 */
class FbLog extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: "open" });
        this.entries = [];
    }

    connectedCallback() {
        this.render();
    }

    add(text, level = "info") {
        const ts = new Date().toLocaleTimeString();
        this.entries.push({ ts, text, level });
        this.renderBody();
    }

    clear() {
        this.entries = [];
        this.renderBody();
    }

    async copy() {
        const text = this.entries.map(e => `[${e.ts}] ${e.level.toUpperCase()}: ${e.text}`).join("\n");
        try {
            await navigator.clipboard.writeText(text);
            this.flashCopied();
        } catch {
            // ignore
        }
    }

    flashCopied() {
        const btn = this.shadowRoot.getElementById("copy-btn");
        if (!btn) return;
        const original = btn.textContent;
        btn.textContent = "Copied!";
        setTimeout(() => { btn.textContent = original; }, 1500);
    }

    render() {
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    display: flex;
                    flex-direction: column;
                    background: rgba(0, 0, 0, 0.3);
                    border-radius: 8px;
                    border: 1px solid var(--border, rgba(255,255,255,0.08));
                    font-size: 0.8rem;
                    font-family: 'Inter', sans-serif;
                    height: 100%;
                    min-height: 120px;
                }
                .hdr {
                    display: flex;
                    justify-content: space-between;
                    padding: 0.4rem 0.75rem;
                    border-bottom: 1px solid var(--border, rgba(255,255,255,0.08));
                    color: #64748b;
                    font-size: 0.7rem;
                    text-transform: uppercase;
                    letter-spacing: 0.05em;
                }
                .hdr button { font-size: 0.7rem; color: #cbd5e1; cursor: pointer; border: none; background: none; padding: 0.1rem 0.4rem; margin-left: 0.3rem; border-radius: 4px; }
                .hdr button:hover { color: #f8fafc; background: rgba(255,255,255,0.08); }
                .body {
                    flex: 1;
                    overflow-y: auto;
                    padding: 0.5rem 0.75rem;
                    color: #cbd5e1;
                }
                .entry {
                    padding: 0.2rem 0;
                    animation: slideIn 0.2s ease-out;
                }
                @keyframes slideIn {
                    from { transform: translateY(5px); opacity: 0; }
                    to   { transform: translateY(0);   opacity: 1; }
                }
                .entry .ts { color: #64748b; margin-right: 0.5rem; }
                .entry.info  { color: #22c55e; }
                .entry.warn  { color: #f59e0b; }
                .entry.error { color: var(--danger, #ef4444); }
            </style>
            <div class="hdr">
                <span>LOG</span>
                <div>
                    <button id="copy-btn">Copy</button>
                    <button id="clear-btn">Clear</button>
                </div>
            </div>
            <div class="body" id="body"></div>
        `;
        this.shadowRoot.getElementById("copy-btn").addEventListener("click", () => this.copy());
        this.shadowRoot.getElementById("clear-btn").addEventListener("click", () => this.clear());
        this.renderBody();
    }

    renderBody() {
        const body = this.shadowRoot.getElementById("body");
        if (!body) return;
        body.innerHTML = this.entries.map(e => `
            <div class="entry ${e.level}">
                <span class="ts">${e.ts}</span>
                <span>${escapeHtml(e.text)}</span>
            </div>
        `).join("");
        body.scrollTop = body.scrollHeight;
    }
}

function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[c]));
}

customElements.define("fb-log", FbLog);
