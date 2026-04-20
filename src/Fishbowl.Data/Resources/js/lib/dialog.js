/**
 * fb.dialog — promise-based confirm dialog helper.
 *
 * Thin wrapper around <fb-dialog>. Constructs the element, waits for the
 * user's response, removes the element, resolves with the chosen action.
 *
 *   const answer = await fb.dialog.confirm({
 *       title:   "Delete this note?",
 *       message: "This note will be permanently deleted.",
 *       buttons: [
 *           { action: "cancel", label: "Cancel", kind: "default" },
 *           { action: "delete", label: "Delete", kind: "destructive", armAfterMs: 2000 },
 *       ],
 *   });
 *
 * Resolves with the clicked button's `action` string, or `null` if the dialog
 * was dismissed via Escape / backdrop / programmatic close. Callers treat
 * `null` the same as "cancel."
 */
(function () {
    if (!window.fb) return;

    window.fb.dialog = {
        confirm({ title, message, buttons }) {
            return new Promise((resolve) => {
                const dlg = document.createElement("fb-dialog");
                if (title) dlg.setAttribute("title", title);
                dlg.buttons = Array.isArray(buttons) ? buttons : [];

                if (message) {
                    // textContent — never innerHTML. Callers may pass
                    // user-sourced strings (note titles, etc.).
                    const p = document.createElement("p");
                    p.textContent = message;
                    dlg.appendChild(p);
                }

                dlg.addEventListener("fb-dialog:action", (e) => {
                    // Let the fade-out transition run before removal.
                    setTimeout(() => {
                        dlg.remove();
                        resolve(e.detail.action);
                    }, 120);
                }, { once: true });

                document.body.appendChild(dlg);
                // Give the browser one frame to register the element so the
                // open animation actually runs (otherwise it starts mid-anim).
                requestAnimationFrame(() => dlg.open());
            });
        },
    };
})();
