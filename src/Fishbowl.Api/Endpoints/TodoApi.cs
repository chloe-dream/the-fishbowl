using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Fishbowl.Core.Models;
using Fishbowl.Core.Repositories;

namespace Fishbowl.Api.Endpoints;

public static class TodoApi
{
    public static RouteGroupBuilder MapTodoApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/todos");

        group.MapGet("/", async (ITodoRepository repo, HttpContext context, bool includeCompleted = false) =>
        {
            var userId = context.Request.Headers["X-Fishbowl-User-Id"].ToString();
            if (string.IsNullOrEmpty(userId)) return Results.BadRequest("User ID missing");
            
            return Results.Ok(await repo.GetAllAsync(userId, includeCompleted));
        });

        group.MapGet("/{id}", async (string id, ITodoRepository repo, HttpContext context) =>
        {
            var userId = context.Request.Headers["X-Fishbowl-User-Id"].ToString();
            if (string.IsNullOrEmpty(userId)) return Results.BadRequest("User ID missing");

            var item = await repo.GetByIdAsync(userId, id);
            return item != null ? Results.Ok(item) : Results.NotFound();
        });

        group.MapPost("/", async (TodoItem item, ITodoRepository repo, HttpContext context) =>
        {
            var userId = context.Request.Headers["X-Fishbowl-User-Id"].ToString();
            if (string.IsNullOrEmpty(userId)) return Results.BadRequest("User ID missing");

            var id = await repo.CreateAsync(userId, item);
            return Results.Created($"/api/todos/{id}", item);
        });

        group.MapPut("/{id}", async (string id, TodoItem item, ITodoRepository repo, HttpContext context) =>
        {
            var userId = context.Request.Headers["X-Fishbowl-User-Id"].ToString();
            if (string.IsNullOrEmpty(userId)) return Results.BadRequest("User ID missing");

            item.Id = id;
            var updated = await repo.UpdateAsync(userId, item);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id}", async (string id, ITodoRepository repo, HttpContext context) =>
        {
            var userId = context.Request.Headers["X-Fishbowl-User-Id"].ToString();
            if (string.IsNullOrEmpty(userId)) return Results.BadRequest("User ID missing");

            var deleted = await repo.DeleteAsync(userId, id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return group;
    }
}
