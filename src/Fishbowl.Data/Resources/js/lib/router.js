/**
 * Fishbowl — hash-based SPA router.
 * Views are custom elements registered via fb.router.register("#/hash", "tag-name").
 * On hashchange, the root mount point's innerHTML is swapped to the matching tag.
 */
(function () {
    const routes = new Map();    // "#/notes" → "fb-notes-view"
    let rootElement = null;

    function currentHash() {
        return window.location.hash || "#/";
    }

    function render() {
        if (!rootElement) return;
        const hash = currentHash();
        const tag = routes.get(hash) || routes.get("#/");
        if (!tag) {
            rootElement.innerHTML = "";
            return;
        }
        // Clear + remount. Views are self-destructing: the browser disconnects
        // the removed element and connects the new one.
        rootElement.innerHTML = `<${tag}></${tag}>`;
    }

    fb.router = {
        register(hash, tagName) {
            routes.set(hash, tagName);
            // If mount() already happened and this registration matches the
            // current hash, render immediately (handles late-loading views).
            if (rootElement && currentHash() === hash) render();
        },
        routes() {
            return Array.from(routes.entries()); // [[hash, tag], ...]
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
