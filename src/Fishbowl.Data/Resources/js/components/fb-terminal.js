/**
 * <fb-terminal>
 *
 * Terminal-style output. Darker background, monospace, colored line levels.
 *
 * Methods:
 *   append(text, level)  — level: "normal" | "warn" | "error" | "success"
 *   clear()
 *   copy()
 */
class FbTerminal extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: "open" });
        this.lines = [];
    }

    connectedCallback() { this.render(); }

    append(text, level = "normal") {
        this.lines.push({ text, level });
        this.renderBody();
    }
    clear() { this.lines = []; this.renderBody(); }

    async copy() {
        const text = this.lines.map(l => l.text).join("\n");
        try {
            await navigator.clipboard.writeText(text);
            this.flashCopied();
        } catch { /* ignore */ }
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
                    background: #0f172a;
                    border-radius: 8px;
                    border: 1px solid var(--border, rgba(255,255,255,0.08));
                    font-family: 'Courier New', 'Lucida Console', monospace;
                    font-size: 0.85rem;
                    height: 100%;
                    min-height: 140px;
                }
                .hdr {
                    display: flex;
                    justify-content: space-between;
                    padding: 0.4rem 0.75rem;
                    border-bottom: 1px solid rgba(255,255,255,0.08);
                    color: #64748b;
                    font-family: 'Inter', sans-serif;
                    font-size: 0.7rem;
                    text-transform: uppercase;
                    letter-spacing: 0.05em;
                }
                .hdr button { font-family: 'Inter', sans-serif; font-size: 0.7rem; color: #cbd5e1; cursor: pointer; border: none; background: none; padding: 0.1rem 0.4rem; margin-left: 0.3rem; border-radius: 4px; }
                .hdr button:hover { color: #f8fafc; background: rgba(255,255,255,0.08); }
                .body { flex: 1; overflow-y: auto; padding: 0.5rem 0.75rem; color: #22c55e; }
                .line.normal  { color: #22c55e; }
                .line.warn    { color: #eab308; }
                .line.error   { color: var(--danger, #ef4444); }
                .line.success { color: #4ade80; font-weight: 600; }
            </style>
            <div class="hdr">
                <span>TERMINAL</span>
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
        body.innerHTML = this.lines.map(l => `
            <div class="line ${l.level}">${escapeHtml(l.text)}</div>
        `).join("");
        body.scrollTop = body.scrollHeight;
    }
}

function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[c]));
}

customElements.define("fb-terminal", FbTerminal);
