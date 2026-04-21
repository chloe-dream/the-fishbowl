using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Fishbowl.Mcp.Endpoints;

// MCP Streamable HTTP endpoint. One POST /mcp with JSON-RPC 2.0 payloads.
// Requests with an `id` get a 200 + JSON response envelope; notifications
// (no `id`) get 202 Accepted with an empty body. No SSE in v1 — every
// method returns synchronously because Claude Code's current client accepts
// that. The real tool surface (search_memory, remember, etc.) lands in
// Task 4.3; for now `initialize` and `tools/list` are the only methods.
public static class McpEndpoint
{
    public static IEndpointRouteBuilder MapMcpEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/mcp", HandleAsync)
            .WithName("Mcp")
            .WithSummary("MCP Streamable HTTP endpoint (JSON-RPC 2.0).")
            .RequireAuthorization();

        return routes;
    }

    private static async Task<IResult> HandleAsync(HttpContext ctx, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Mcp");

        McpRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<McpRequest>(
                ctx.Request.Body, JsonOpts, ctx.RequestAborted);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "MCP JSON parse failed");
            return ErrorResponse(id: null, McpErrorCodes.ParseError, "Parse error");
        }

        if (request is null || string.IsNullOrEmpty(request.Method))
            return ErrorResponse(id: null, McpErrorCodes.InvalidRequest, "Invalid Request");

        var isNotification = request.Id is null;

        object? result;
        McpError? error = null;
        try
        {
            result = await DispatchAsync(request, ctx);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP dispatch failed for {Method}", request.Method);
            result = null;
            error = new McpError(McpErrorCodes.InternalError, ex.Message);
        }

        // Notifications: spec says the server MUST NOT return a response
        // body. Streamable HTTP recommends 202 Accepted.
        if (isNotification) return Results.Accepted();

        // Unknown method: error response, not exception.
        if (result is null && error is null)
        {
            error = new McpError(McpErrorCodes.MethodNotFound, $"Method not found: {request.Method}");
        }

        var response = new McpResponse("2.0", request.Id, error is null ? result : null, error);
        return Results.Json(response, JsonOpts);
    }

    // Returns a result object, or null if the method is unknown (caller
    // converts that to a MethodNotFound error envelope).
    private static Task<object?> DispatchAsync(McpRequest request, HttpContext ctx)
    {
        return request.Method switch
        {
            "initialize" => Task.FromResult<object?>(new
            {
                protocolVersion = McpProtocol.ProtocolVersion,
                capabilities = new { tools = new { } },
                serverInfo = new { name = McpProtocol.ServerName, version = McpProtocol.ServerVersion },
            }),

            // Placeholder — Task 4.3 swaps this for a real tool registry.
            // Listing is free (no scope check); calling is scope-gated.
            "tools/list" => Task.FromResult<object?>(new { tools = Array.Empty<object>() }),

            _ => Task.FromResult<object?>(null),
        };
    }

    private static IResult ErrorResponse(JsonElement? id, int code, string message)
    {
        var response = new McpResponse("2.0", id, null, new McpError(code, message));
        return Results.Json(response, JsonOpts);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
