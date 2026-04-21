/**
 * Fishbowl — fetch wrapper for /api/v1/*.
 * 401 responses redirect to /login. Non-OK responses throw ApiError.
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

    const crud = (resource) => ({
        list:   ()        => request(`/${resource}`),
        get:    (id)      => request(`/${resource}/${encodeURIComponent(id)}`),
        create: (body)    => request(`/${resource}`,                      { method: "POST",   body: JSON.stringify(body) }),
        update: (id, body) => request(`/${resource}/${encodeURIComponent(id)}`, { method: "PUT",    body: JSON.stringify(body) }),
        delete: (id)      => request(`/${resource}/${encodeURIComponent(id)}`, { method: "DELETE" })
    });

    fb.api = {
        notes: crud("notes"),
        todos: crud("todos"),
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
