using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using Fishbowl.Core;
using Fishbowl.Core.Mcp;
using Fishbowl.Core.Repositories;

namespace Fishbowl.Api.Endpoints;

// Search-side administrative endpoints. Kept separate from NotesApi so
// re-index doesn't clutter the /api/v1/notes surface — different verb,
// different consumer (human hitting the settings UI, not MCP clients).
public static class SearchApi
{
    public static RouteGroupBuilder MapSearchApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/search");

        // Bulk re-embed every note in the resolved context. Cookie-only —
        // Bearer callers shouldn't be able to trigger long-running
        // maintenance against a user's DB from outside the UI.
        //
        // The operation is synchronous: response comes back only after
        // every note is processed. At personal-memory scale (hundreds-to-
        // low-thousands) that's seconds, not minutes. Revisit with an
        // async/progress-polled design if vault sizes grow.
        group.MapPost("/reindex", async (
            ClaimsPrincipal user,
            INoteRepository repo,
            CancellationToken ct) =>
        {
            if (user.Identity?.AuthenticationType == McpContextClaims.BearerScheme)
                return Results.Forbid();

            ContextRef ctx;
            try { ctx = McpContextClaims.Resolve(user); }
            catch (InvalidOperationException) { return Results.Unauthorized(); }

            var result = await repo.ReEmbedAllAsync(ctx, ct);
            return Results.Ok(new
            {
                processed = result.Processed,
                failed = result.Failed,
            });
        })
        .WithName("ReindexSearch")
        .WithSummary("Re-embeds every note in the current context. Cookie-auth only.");

        return group;
    }
}
