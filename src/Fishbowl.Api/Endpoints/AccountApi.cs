using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using Fishbowl.Core.Mcp;
using Fishbowl.Core.Models;
using Fishbowl.Core.Repositories;

namespace Fishbowl.Api.Endpoints;

public static class AccountApi
{
    public static IEndpointRouteBuilder MapAccountApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1");

        group.MapGet("/me", async (ClaimsPrincipal user, ISystemRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var profile = await repo.GetUserAsync(userId, ct);
            if (profile is null) return Results.NotFound();

            return Results.Ok(profile);
        })
        .WithName("GetMe")
        .WithSummary("Returns the current authenticated user's profile.")
        .Produces<User>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization();

        // Diagnostic: what does the server think about this request's auth?
        // Used by MCP clients to confirm a Bearer token is valid and to
        // discover the context + scopes it's bound to. Cookie users see
        // their personal context and no scopes (cookies have full access).
        group.MapGet("/auth/whoami", (ClaimsPrincipal user) =>
        {
            var userId = user.FindFirst(McpContextClaims.UserId)?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var ctxType = user.FindFirst(McpContextClaims.ContextType)?.Value ?? "user";
            var ctxId = user.FindFirst(McpContextClaims.ContextId)?.Value ?? userId;
            var scopes = user.FindAll(McpContextClaims.Scope).Select(c => c.Value).ToList();

            return Results.Ok(new
            {
                userId,
                context = new { type = ctxType, id = ctxId },
                scopes,
                authType = user.Identity?.AuthenticationType,
            });
        })
        .WithName("WhoAmI")
        .WithSummary("Returns the resolved context and scopes for the current request.")
        .RequireAuthorization();

        // POST is correct for a state-changing operation (sign-out clears the
        // cookie). Returns 204 — frontend redirects to /login itself.
        group.MapPost("/auth/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        })
        .WithName("Logout")
        .WithSummary("Signs the current user out. Clears the auth cookie.")
        .Produces(StatusCodes.Status204NoContent);

        return routes;
    }
}
