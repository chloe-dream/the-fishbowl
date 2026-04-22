/**
 * Fishbowl — fetch wrapper for /api/v1/*.
 * 401 responses redirect to /login. Non-OK responses throw ApiError.
 *
 * Context-aware: every CRUD wrapper routes through `fb.context.endpoint(path)`
 * so a request for "/notes" becomes "/api/v1/notes" when personal is active
 * or "/api/v1/teams/SLUG/notes" when a team is active. Context-agnostic
 * endpoints (teams CRUD, API keys, auth, /me) stay on the personal path.
 */
(function () {
    const base = "/api/v1";

    class ApiError extends Error {
        constructor(status, body) {
            super(`API error ${status}: ${body}`);
            this.status = status;
            this.body = body;
        }
    }

    async function request(path, options = {}) {
        const res = await fetch(base + path, {
            headers: { "Content-Type": "application/json", ...(options.headers || {}) },
            ...options
        });
        if (res.status === 401) {
            window.location.href = "/login";
            // Throw so callers' await chains don't continue as if success.
            throw new ApiError(401, "Unauthenticated");
        }
        if (!res.ok) {
            const body = await res.text().catch(() => "");
            throw new ApiError(res.status, body);
        }
        if (res.status === 204) return undefined;
        const contentType = res.headers.get("content-type") || "";
        if (contentType.includes("application/json")) return res.json();
        return res.text();
    }

    // `ctx(path)` prefixes `path` with the current team slug when a team
    // context is active. Called lazily (per request) so switching context
    // doesn't require rebuilding the fb.api object.
    function ctx(path) {
        return window.fb?.context?.endpoint ? fb.context.endpoint(path) : path;
    }

    const crud = (resource) => ({
        list:   ()        => request(ctx(`/${resource}`)),
        get:    (id)      => request(ctx(`/${resource}/${encodeURIComponent(id)}`)),
        create: (body)    => request(ctx(`/${resource}`),                      { method: "POST",   body: JSON.stringify(body) }),
        update: (id, body) => request(ctx(`/${resource}/${encodeURIComponent(id)}`), { method: "PUT",    body: JSON.stringify(body) }),
        delete: (id)      => request(ctx(`/${resource}/${encodeURIComponent(id)}`), { method: "DELETE" })
    });

    // Notes list accepts an optional filter: { tags?: string[], match?: 'any'|'all' }.
    // Repeated `tag` query params follow the server's IReadOnlyCollection<string>
    // binding; absent params leave the server defaults (no filter, match=any).
    function listNotes(opts) {
        if (!opts || !opts.tags || opts.tags.length === 0) return request(ctx("/notes"));
        const qs = new URLSearchParams();
        for (const t of opts.tags) qs.append("tag", t);
        if (opts.match === "all") qs.set("match", "all");
        return request(`${ctx("/notes")}?${qs.toString()}`);
    }

    const notes = crud("notes");
    notes.list = listNotes;

    fb.api = {
        notes,
        todos: crud("todos"),
        tags: {
            list:        ()                  => request(ctx("/tags")),
            upsertColor: (name, color)       => request(ctx(`/tags/${encodeURIComponent(name)}`),
                                                        { method: "PUT", body: JSON.stringify({ color }) }),
            rename:      (name, newName)     => request(ctx(`/tags/${encodeURIComponent(name)}/rename`),
                                                        { method: "POST", body: JSON.stringify({ newName }) }),
            delete:      (name)              => request(ctx(`/tags/${encodeURIComponent(name)}`),
                                                        { method: "DELETE" })
        },
        // Search admin — reindex runs against the active context. Cookie-only
        // server-side (Bearer 403s), consistent with SearchApi / TeamsApi.
        search: {
            reindex: () => request(ctx("/search/reindex"), { method: "POST" })
        },
        teams: {
            list:   ()       => request("/teams"),
            get:    (slug)   => request(`/teams/${encodeURIComponent(slug)}`),
            create: ({ name }) => request("/teams", { method: "POST", body: JSON.stringify({ name }) }),
            delete: (slug)   => request(`/teams/${encodeURIComponent(slug)}`, { method: "DELETE" }),
        },
        // API keys — the create() response is the ONLY moment the raw token
        // exists on the client. Store nothing; surface it to the user with a
        // one-time-view dialog and let them copy it themselves.
        keys: {
            list:   ()       => request("/keys"),
            create: ({ name, contextType, contextId, scopes }) =>
                request("/keys", {
                    method: "POST",
                    body: JSON.stringify({ name, contextType, contextId, scopes }),
                }),
            delete: (id)     => request(`/keys/${encodeURIComponent(id)}`, { method: "DELETE" }),
        },
        version: () => request("/version"),
        providers: () => fetch("/api/auth/providers").then(r => r.json()),
        me: {
            get: () => request("/me")
        },
        auth: {
            logout: () => request("/auth/logout", { method: "POST" })
        }
    };
    fb.ApiError = ApiError;
})();
