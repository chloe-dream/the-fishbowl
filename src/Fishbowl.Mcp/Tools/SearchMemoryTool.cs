using System.Security.Claims;
using System.Text.Json;
using Fishbowl.Core;
using Fishbowl.Core.Mcp;
using Fishbowl.Core.Repositories;
using Fishbowl.Core.Util;

namespace Fishbowl.Mcp.Tools;

// Keyword search over notes in the resolved context. FTS-only for v1 —
// Phase 5's HybridSearchService replaces this with a 70/30 semantic+FTS
// merge. Until then the implementation does a client-side substring
// filter, which is fine for personal-scale data volume.
public class SearchMemoryTool : IMcpTool
{
    private readonly INoteRepository _notes;

    public SearchMemoryTool(INoteRepository notes) { _notes = notes; }

    public string Name => "search_memory";
    public string Description =>
        "Full-text search over notes in the current context. Returns matching notes with secrets stripped.";
    public string RequiredScope => "read:notes";
    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            query = new { type = "string", description = "Search term" },
            limit = new { type = "integer", @default = 10, description = "Max hits to return" },
            include_pending = new { type = "boolean", @default = true, description = "Include review:pending notes" },
        },
        required = new[] { "query" },
    };

    public async Task<object> InvokeAsync(
        ContextRef ctx, string actor, JsonElement arguments, ClaimsPrincipal principal, CancellationToken ct)
    {
        var query = arguments.GetProperty("query").GetString() ?? string.Empty;
        var limit = arguments.TryGetProperty("limit", out var l) && l.ValueKind == JsonValueKind.Number
            ? Math.Clamp(l.GetInt32(), 1, 100) : 10;
        var includePending = !arguments.TryGetProperty("include_pending", out var p)
            || p.ValueKind == JsonValueKind.Null
            || p.GetBoolean();

        var q = query.Trim();
        if (string.IsNullOrEmpty(q)) return new { notes = Array.Empty<object>() };

        var all = await _notes.GetAllAsync(ctx, ct);
        var ql = q.ToLowerInvariant();

        var hits = all
            .Where(n => !n.Archived)
            .Where(n => includePending || !n.Tags.Contains("review:pending"))
            .Where(n =>
                (n.Title ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (n.Content ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                n.Tags.Any(t => t.Contains(ql, StringComparison.OrdinalIgnoreCase)))
            .Take(limit)
            .Select(SecretStripper.StripNote)
            .ToList();

        return new { notes = hits };
    }
}
