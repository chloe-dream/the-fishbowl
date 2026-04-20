/**
 * Fishbowl — hash-based SPA router.
 * Views are custom elements registered via fb.router.register("#/hash", "tag-name", { label, icon }).
 * On hashchange, the root mount point's innerHTML is swapped to the matching tag.
 */
(function () {
    const routes = new Map();    // "#/notes" → { tag, label, icon }
    let rootElement = null;

    function currentHash() {
        return window.location.hash || "#/";
    }

    function render() {
        if (!rootElement) return;
        const hash = currentHash();
        const entry = routes.get(hash) || routes.get("#/");
        if (!entry) { rootElement.innerHTML = ""; return; }
        rootElement.innerHTML = `<${entry.tag}></${entry.tag}>`;
    }

    fb.router = {
        register(hash, tagName, options = {}) {
            routes.set(hash, {
                tag: tagName,
                label: options.label || tagName,
                icon:  options.icon  || null
            });
            // Notify listeners (e.g. <fb-nav>) that the route table changed.
            // Needed because nav components are instantiated before view
            // scripts register, so their first render sees an empty map.
            window.dispatchEvent(new CustomEvent("fb:route-registered", {
                detail: { hash, tag: tagName }
            }));
            if (rootElement && currentHash() === hash) render();
        },
        routes() {
            return Array.from(routes.entries()).map(([hash, info]) => ({ hash, ...info }));
        },
        current() {
            return currentHash();
        },
        navigate(hash) {
            window.location.hash = hash;
        },
        mount(selector) {
            rootElement = document.querySelector(selector);
            if (!rootElement) {
                console.error(`[fb.router] mount: no element matches ${selector}`);
                return;
            }
            window.addEventListener("hashchange", render);
            render();
        }
    };
})();
