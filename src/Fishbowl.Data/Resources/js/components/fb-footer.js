/**
 * <fb-footer>
 *
 * Minimal footer: version string + optional GitHub link.
 * The GitHub link only appears when window.fb.system?.githubUrl is set
 * (not yet wired — honors the "no dead links" rule).
 *
 * Auto-fetches the version from /api/v1/version on connect.
 */
class FbFooter extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    async connectedCallback() {
        this.render("loading...");
        try {
            const v = await fb.api.version();
            fb.version = v?.version ?? "unknown";
            this.render(fb.version);
        } catch {
            this.render("offline");
        }
    }

    render(versionText) {
        const githubUrl = window.fb?.system?.githubUrl ?? null;
        this.shadowRoot.innerHTML = `
            <style>
                :host {
                    display: block;
                    text-align: center;
                    padding: 1.5rem 1rem;
                    color: var(--text-muted, #64748b);
                    font-size: 0.75rem;
                    letter-spacing: 0.05em;
                    text-transform: uppercase;
                    border-top: 1px solid var(--border, rgba(255,255,255,0.08));
                    margin-top: 4rem;
                }
                a {
                    color: inherit;
                    text-decoration: none;
                    margin-left: 0.5rem;
                }
                a:hover { color: var(--accent, #3b82f6); }
                .version { opacity: 0.7; }
            </style>
            <span>THE FISHBOWL</span>
            <span class="version"> · v${versionText}</span>
            ${githubUrl ? `<a href="${githubUrl}" target="_blank" rel="noopener">GITHUB</a>` : ``}
        `;
    }
}

customElements.define("fb-footer", FbFooter);
