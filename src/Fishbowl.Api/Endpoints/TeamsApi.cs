using System.Security.Claims;
using Fishbowl.Core;
using Fishbowl.Core.Mcp;
using Fishbowl.Core.Models;
using Fishbowl.Core.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Fishbowl.Api.Endpoints;

public static class TeamsApi
{
    public record CreateTeamRequest(string Name);

    public static RouteGroupBuilder MapTeamsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/teams");

        // ────────── Team CRUD ──────────

        group.MapGet("/", async (ClaimsPrincipal user, ITeamRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var memberships = await repo.ListByMemberAsync(userId, ct);
            return Results.Ok(memberships.Select(m => new
            {
                id = m.Team.Id,
                slug = m.Team.Slug,
                name = m.Team.Name,
                role = m.Role.ToDbValue(),
                createdAt = m.Team.CreatedAt,
            }));
        })
        .WithName("ListTeams")
        .WithSummary("Lists teams the authenticated user belongs to.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/", async (
            CreateTeamRequest body, ClaimsPrincipal user, ITeamRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            try
            {
                var team = await repo.CreateAsync(userId, body.Name, ct);
                return Results.Created($"/api/v1/teams/{team.Slug}", team);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateTeam")
        .WithSummary("Creates a team owned by the authenticated user.")
        .Produces<Team>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{slug}", async (string slug, ClaimsPrincipal user, ITeamRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var team = await repo.GetBySlugAsync(slug, ct);
            if (team is null) return Results.NotFound();

            var role = await repo.GetMembershipAsync(team.Id, userId, ct);
            if (role is null) return Results.Forbid();

            return Results.Ok(new
            {
                id = team.Id,
                slug = team.Slug,
                name = team.Name,
                role = role.Value.ToDbValue(),
                createdAt = team.CreatedAt,
            });
        })
        .WithName("GetTeam")
        .WithSummary("Gets a single team by slug. Requires membership.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{slug}", async (string slug, ClaimsPrincipal user, ITeamRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var team = await repo.GetBySlugAsync(slug, ct);
            if (team is null) return Results.NotFound();

            var ok = await repo.DeleteAsync(team.Id, userId, ct);
            return ok ? Results.NoContent() : Results.Forbid();
        })
        .WithName("DeleteTeam")
        .WithSummary("Deletes a team. Owner only. Leaves the .db file in place.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // ────────── Nested: team notes ──────────
        // Routes to ContextRef.Team(team.Id). Membership checked per request;
        // readonly members blocked from writes.

        group.MapGet("/{slug}/notes", async (
            string slug, string[]? tag, string? match,
            ClaimsPrincipal user, ITeamRepository teams, INoteRepository notes, CancellationToken ct) =>
        {
            var resolved = await ResolveTeamAsync(slug, user, teams, ct);
            if (resolved.Error is not null) return resolved.Error;
            var (team, _) = (resolved.Team!, resolved.Role!.Value);

            var tags = tag is { Length: > 0 } ? tag : null;
            var matchMode = match == "all" ? "all" : "any";
            return Results.Ok(await notes.GetAllAsync(ContextRef.Team(team.Id), tags, matchMode, ct));
        })
        .WithName("ListTeamNotes")
        .RequireScope("read:notes");

        group.MapGet("/{slug}/notes/{id}", async (
            string slug, string id,
            ClaimsPrincipal user, ITeamRepository teams, INoteRepository notes, CancellationToken ct) =>
        {
            var resolved = await ResolveTeamAsync(slug, user, teams, ct);
            if (resolved.Error is not null) return resolved.Error;
            var team = resolved.Team!;

            var note = await notes.GetByIdAsync(ContextRef.Team(team.Id), id, ct);
            return note is not null ? Results.Ok(note) : Results.NotFound();
        })
        .WithName("GetTeamNote")
        .RequireScope("read:notes");

        group.MapPost("/{slug}/notes", async (
            string slug, Note note,
            ClaimsPrincipal user, ITeamRepository teams, INoteRepository notes, CancellationToken ct) =>
        {
            var resolved = await ResolveTeamAsync(slug, user, teams, ct);
            if (resolved.Error is not null) return resolved.Error;
            var (team, role) = (resolved.Team!, resolved.Role!.Value);
            if (!role.CanWrite()) return Results.Forbid();

            var userId = user.FindFirst("fishbowl_user_id")!.Value;
            var created = await notes.CreateAsync(ContextRef.Team(team.Id), userId, note, ct);
            return Results.Created($"/api/v1/teams/{slug}/notes/{created}", note);
        })
        .WithName("CreateTeamNote")
        .RequireScope("write:notes");

        group.MapPut("/{slug}/notes/{id}", async (
            string slug, string id, Note note,
            ClaimsPrincipal user, ITeamRepository teams, INoteRepository notes, CancellationToken ct) =>
        {
            var resolved = await ResolveTeamAsync(slug, user, teams, ct);
            if (resolved.Error is not null) return resolved.Error;
            var (team, role) = (resolved.Team!, resolved.Role!.Value);
            if (!role.CanWrite()) return Results.Forbid();

            note.Id = id;
            var updated = await notes.UpdateAsync(ContextRef.Team(team.Id), note, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        })
        .WithName("UpdateTeamNote")
        .RequireScope("write:notes");

        group.MapDelete("/{slug}/notes/{id}", async (
            string slug, string id,
            ClaimsPrincipal user, ITeamRepository teams, INoteRepository notes, CancellationToken ct) =>
        {
            var resolved = await ResolveTeamAsync(slug, user, teams, ct);
            if (resolved.Error is not null) return resolved.Error;
            var (team, role) = (resolved.Team!, resolved.Role!.Value);
            if (!role.CanWrite()) return Results.Forbid();

            var ok = await notes.DeleteAsync(ContextRef.Team(team.Id), id, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteTeamNote")
        .RequireScope("write:notes");

        return group.RequireAuthorization();
    }

    private record TeamResolution(Team? Team, TeamRole? Role, IResult? Error);

    // Resolves {slug} → Team + the caller's TeamRole. Returns an Error result
    // (401/404/403) when the caller isn't authenticated, the team doesn't
    // exist, or the caller isn't a member. Keeps the endpoint handlers above
    // declarative.
    private static async Task<TeamResolution> ResolveTeamAsync(
        string slug, ClaimsPrincipal user, ITeamRepository teams, CancellationToken ct)
    {
        var userId = user.FindFirst(McpContextClaims.UserId)?.Value;
        if (string.IsNullOrEmpty(userId))
            return new TeamResolution(null, null, Results.Unauthorized());

        var team = await teams.GetBySlugAsync(slug, ct);
        if (team is null)
            return new TeamResolution(null, null, Results.NotFound());

        // Bearer-context match: if the principal is a token, it must be bound
        // to THIS team. A personal token on a team URL is rejected even when
        // the underlying user is a team member — the token's own context is
        // the authoritative scope, not the human behind it.
        if (user.Identity?.AuthenticationType == McpContextClaims.BearerScheme)
        {
            var ctxType = user.FindFirst(McpContextClaims.ContextType)?.Value;
            var ctxId = user.FindFirst(McpContextClaims.ContextId)?.Value;
            if (ctxType != "team" || ctxId != team.Slug)
                return new TeamResolution(team, null, Results.Forbid());
        }

        var role = await teams.GetMembershipAsync(team.Id, userId, ct);
        if (role is null)
            return new TeamResolution(team, null, Results.Forbid());

        return new TeamResolution(team, role, null);
    }
}
