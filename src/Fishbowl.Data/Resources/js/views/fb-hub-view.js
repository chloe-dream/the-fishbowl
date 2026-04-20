/**
 * <fb-hub-view>  (mounted at #/)
 *
 * Landing view. Gradient title + subtitle + 2-tile grid.
 * Tile content is static here for v1 (Notes, Todos). Feature work adds tiles
 * as features become real — never show a tile for something that doesn't work.
 *
 * No <fb-nav> slide-out from the hub — the tiles are the navigation.
 */
class FbHubView extends HTMLElement {
    connectedCallback() {
        this.innerHTML = `
            <div class="orb"></div>
            <div class="hub-container">
                <header>
                    <h1>THE FISHBOWL</h1>
                    <p class="intro-text">Your memory lives here. You don't.</p>
                </header>
                <main class="grid">
                    <a class="tile" href="#/notes">
                        <fb-icon name="note"></fb-icon>
                        <div>
                            <h2>Notes</h2>
                            <p>Write freely. Find anything.</p>
                        </div>
                    </a>
                    <a class="tile" href="#/todos">
                        <fb-icon name="check"></fb-icon>
                        <div>
                            <h2>Todos</h2>
                            <p>Fast to-dos, always at hand.</p>
                        </div>
                    </a>
                </main>
            </div>
            <fb-footer></fb-footer>
        `;
    }
}

customElements.define("fb-hub-view", FbHubView);
fb.router.register("#/", "fb-hub-view", { label: "Home", icon: "home" });
