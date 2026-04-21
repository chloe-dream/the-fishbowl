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
        var group = routes.MapGroup("/api/v1/notes");

        group.MapGet("/", async (
            string[]? tag,
            string? match,
            ClaimsPrincipal user,
            INoteRepository repo,
            CancellationToken ct) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var tags = tag is { Length: > 0 } ? tag : null;
            var matchMode = match == "all" ? "all" : "any";
            return Results.Ok(await repo.GetAllAsync(userId, tags, matchMode, ct));
        })
        .WithName("ListNotes")
        .WithSummary("Lists notes for the authenticated user. Optional ?tag=foo&tag=bar&match=any|all filter.")
        .Produces<IEnumerable<Note>>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id}", async (string id, ClaimsPrincipal user, INoteRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var note = await repo.GetByIdAsync(userId, id, ct);
            return note is not null ? Results.Ok(note) : Results.NotFound();
        })
        .WithName("GetNote")
        .WithSummary("Gets a single note by id.")
        .Produces<Note>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/", async (Note note, ClaimsPrincipal user, INoteRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var id = await repo.CreateAsync(userId, note, ct);
            return Results.Created($"/api/v1/notes/{id}", note);
        })
        .WithName("CreateNote")
        .WithSummary("Creates a new note.")
        .Produces<Note>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPut("/{id}", async (string id, Note note, ClaimsPrincipal user, INoteRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            note.Id = id;
            var updated = await repo.UpdateAsync(userId, note, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        })
        .WithName("UpdateNote")
        .WithSummary("Updates an existing note.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapDelete("/{id}", async (string id, ClaimsPrincipal user, INoteRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var success = await repo.DeleteAsync(userId, id, ct);
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteNote")
        .WithSummary("Deletes a note.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        return group.RequireAuthorization();
    }
}
