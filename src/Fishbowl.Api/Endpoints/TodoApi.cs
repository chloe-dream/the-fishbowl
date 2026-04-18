using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using Fishbowl.Core.Models;
using Fishbowl.Core.Repositories;

namespace Fishbowl.Api.Endpoints;

public static class TodoApi
{
    public static RouteGroupBuilder MapTodoApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/todos");

        group.MapGet("/", async (ClaimsPrincipal user, ITodoRepository repo, bool includeCompleted = false, CancellationToken ct = default) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
            return Results.Ok(await repo.GetAllAsync(userId, includeCompleted, ct));
        });

        group.MapGet("/{id}", async (string id, ClaimsPrincipal user, ITodoRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var item = await repo.GetByIdAsync(userId, id, ct);
            return item != null ? Results.Ok(item) : Results.NotFound();
        });

        group.MapPost("/", async (TodoItem item, ClaimsPrincipal user, ITodoRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var id = await repo.CreateAsync(userId, item, ct);
            return Results.Created($"/api/v1/todos/{id}", item);
        });

        group.MapPut("/{id}", async (string id, TodoItem item, ClaimsPrincipal user, ITodoRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            item.Id = id;
            var updated = await repo.UpdateAsync(userId, item, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id}", async (string id, ClaimsPrincipal user, ITodoRepository repo, CancellationToken ct) =>
        {
            var userId = user.FindFirst("fishbowl_user_id")?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var deleted = await repo.DeleteAsync(userId, id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return group.RequireAuthorization();
    }
}
