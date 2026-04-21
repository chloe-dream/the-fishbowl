using System.Security.Claims;
using System.Text.Json;
using Fishbowl.Core;
using Fishbowl.Core.Mcp;
using Fishbowl.Core.Models;
using Fishbowl.Core.Repositories;
using Fishbowl.Core.Util;

namespace Fishbowl.Mcp.Tools;

// Writes a new note. Passes NoteSource.Mcp so the repository auto-tags
// `source:mcp` + `review:pending` — the human catches it in the review
// inbox before it counts as approved memory.
public class RememberTool : IMcpTool
{
    private readonly INoteRepository _notes;

    public RememberTool(INoteRepository notes) { _notes = notes; }

    public string Name => "remember";
    public string Description =>
        "Create a new memory note in the current context. Returns the stored note.";
    public string RequiredScope => "write:notes";
    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            title = new { type = "string" },
            content = new { type = "string" },
            tags = new { type = "array", items = new { type = "string" } },
        },
        required = new[] { "title" },
    };

    public async Task<object> InvokeAsync(
        ContextRef ctx, string actor, JsonElement arguments, ClaimsPrincipal principal, CancellationToken ct)
    {
        var title = arguments.TryGetProperty("title", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("title is required", nameof(arguments));

        var content = arguments.TryGetProperty("content", out var c) ? c.GetString() : null;
        var tags = arguments.TryGetProperty("tags", out var g) && g.ValueKind == JsonValueKind.Array
            ? g.EnumerateArray().Select(e => e.GetString() ?? "")
               .Where(s => !string.IsNullOrEmpty(s)).ToList()
            : new List<string>();

        var note = new Note
        {
            Title = title!,
            Content = content,
            Tags = tags,
        };

        var id = await _notes.CreateAsync(ctx, actor, note, NoteSource.Mcp, ct);
        note.Id = id;

        return new { id, note = SecretStripper.StripNote(note) };
    }
}
