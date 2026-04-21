using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
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
