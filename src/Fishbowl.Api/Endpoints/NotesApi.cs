using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Fishbowl.Core.Models;
using Fishbowl.Core.Repositories;

namespace Fishbowl.Api.Endpoints;

public static class NotesApi
{
    public static RouteGroupBuilder MapNotesApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/notes");

        group.MapGet("/", async (INoteRepository repo, HttpContext context) =>
        {
            var userId = context.Request.Headers["X-Fishbowl-User-Id"].ToString();
            if (string.IsNullOrEmpty(userId)) return Results.BadRequest("User ID missing");
            
            return Results.Ok(await repo.GetAllAsync(userId));
        });

        group.MapGet("/{id}", async (string id, INoteRepository repo, HttpContext context) =>
        {
            var userId = context.Request.Headers["X-Fishbowl-User-Id"].ToString();
            if (string.IsNullOrEmpty(userId)) return Results.BadRequest("User ID missing");

            var note = await repo.GetByIdAsync(userId, id);
            return note != null ? Results.Ok(note) : Results.NotFound();
        });

        group.MapPost("/", async (Note note, INoteRepository repo, HttpContext context) =>
        {
            var userId = context.Request.Headers["X-Fishbowl-User-Id"].ToString();
            if (string.IsNullOrEmpty(userId)) return Results.BadRequest("User ID missing");

            var id = await repo.CreateAsync(userId, note);
            return Results.Created($"/api/notes/{id}", note);
        });

        group.MapPut("/{id}", async (string id, Note note, INoteRepository repo, HttpContext context) =>
        {
            var userId = context.Request.Headers["X-Fishbowl-User-Id"].ToString();
            if (string.IsNullOrEmpty(userId)) return Results.BadRequest("User ID missing");

            note.Id = id;
            var updated = await repo.UpdateAsync(userId, note);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id}", async (string id, INoteRepository repo, HttpContext context) =>
        {
            var userId = context.Request.Headers["X-Fishbowl-User-Id"].ToString();
            if (string.IsNullOrEmpty(userId)) return Results.BadRequest("User ID missing");

            var deleted = await repo.DeleteAsync(userId, id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return group;
    }
}
