/**
 * Fishbowl — global namespace.
 * Loaded first. Populated incrementally by other lib scripts.
 *
 * Mods use window.fb to interact with the system without rebuilding
 * a component (e.g. fb.icons.register, fb.router.navigate).
 */
(function () {
    if (window.fb) return; // idempotent
    window.fb = {
        version: null,                  // populated by /api/v1/version fetch
        api:     null,                  // populated by api.js
        router:  null,                  // populated by router.js
        icons:   null,                  // populated by icons.js
        /**
         * Nav ribbon toolbar — views call fb.toolbar.set([...]) to project
         * action icons into the fixed <fb-nav> ribbon. <fb-nav> registers
         * itself as the renderer on connect; router clears between view swaps.
         *
         * Item shape: { icon, title, onClick, active? }
         */
        toolbar: {
            _items: [],
            _nav:   null,
            set(items) {
                this._items = Array.isArray(items) ? items : [];
                this._nav?.renderToolbar();
            },
            clear() { this.set([]); }
        }
    };
})();
