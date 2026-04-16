using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using Fishbowl.Core.Models;
using Fishbowl.Core.Repositories;

namespace Fishbowl.Api.Endpoints;

public static class NotesApi
{
    public static RouteGroupBuilder MapNotesApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/notes");

        group.MapGet("/", async (ClaimsPrincipal user, INoteRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
            return Results.Ok(await repo.GetAllAsync(userId, ct));
        });

        group.MapGet("/{id}", async (string id, ClaimsPrincipal user, INoteRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
            
            var note = await repo.GetByIdAsync(userId, id, ct);
            return note is not null ? Results.Ok(note) : Results.NotFound();
        });

        group.MapPost("/", async (Note note, ClaimsPrincipal user, INoteRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var id = await repo.CreateAsync(userId, note, ct);
            return Results.Created($"/api/notes/{id}", note);
        });

        group.MapPut("/{id}", async (string id, Note note, ClaimsPrincipal user, INoteRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            note.Id = id;
            var updated = await repo.UpdateAsync(userId, note, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id}", async (string id, ClaimsPrincipal user, INoteRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var success = await repo.DeleteAsync(userId, id, ct);
            return success ? Results.NoContent() : Results.NotFound();
        });

        return group.RequireAuthorization();
    }
}
