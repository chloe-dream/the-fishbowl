using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Fishbowl.Mcp.Endpoints;

// Placeholder for the MCP Streamable HTTP endpoint. Task 4.2 replaces this
// with a real JSON-RPC dispatcher over `POST /mcp` and wires Bearer auth.
// For now: a hello-world that proves the project is referenced and the
// route is mapped.
public static class McpEndpoint
{
    public static IEndpointRouteBuilder MapMcpEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/mcp", () => Results.Ok(new
        {
            jsonrpc = "2.0",
            result = new
            {
                serverInfo = new { name = "fishbowl", version = "0.1" },
            },
        }))
        .WithName("McpStub")
        .WithSummary("Placeholder for the MCP Streamable HTTP endpoint.");

        return routes;
    }
}
