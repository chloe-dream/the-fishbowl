using System.Security.Claims;
using System.Text.Json;

namespace Fishbowl.Core.Mcp;

// A single MCP tool — thin adapter between a JSON-RPC `tools/call` request
// and a repository/search method. Implementations live in Fishbowl.Mcp.Tools.
public interface IMcpTool
{
    // Machine name used in `tools/call`. e.g. "search_memory".
    string Name { get; }

    // Human-readable description surfaced in `tools/list`.
    string Description { get; }

    // JSON-schema-like object describing the `arguments` shape. The dispatcher
    // does not enforce this — it's metadata for the client. Implementations
    // should return a plain anonymous object that serialises cleanly.
    object InputSchema { get; }

    // Bearer scope required to invoke this tool. Cookie principals bypass
    // this check (they have full access by design). Dispatcher enforces.
    string RequiredScope { get; }

    // Invoke the tool. `ctx` is the resolved ContextRef (user or team).
    // `actor` is the user id from `fishbowl_user_id` — used as `created_by`
    // on write paths. Returns a plain object that the dispatcher serialises
    // into the `content` array of the `tools/call` response.
    Task<object> InvokeAsync(
        ContextRef ctx,
        string actor,
        JsonElement arguments,
        ClaimsPrincipal principal,
        CancellationToken ct);
}
