using System.Data;
using Dapper;
using Fishbowl.Core;
using Fishbowl.Core.Models;
using Fishbowl.Core.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fishbowl.Data.Repositories;

public class NoteRepository : INoteRepository
{
    private readonly DatabaseFactory _dbFactory;
    private readonly ITagRepository _tagRepository;
    private readonly ILogger<NoteRepository> _logger;

    public NoteRepository(
        DatabaseFactory dbFactory,
        ITagRepository tagRepository,
        ILogger<NoteRepository>? logger = null)
    {
        _dbFactory = dbFactory;
        _tagRepository = tagRepository;
        _logger = logger ?? NullLogger<NoteRepository>.Instance;
    }

    // ────────── ContextRef overloads (canonical implementation) ──────────

    public async Task<Note?> GetByIdAsync(ContextRef ctx, string id, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateContextConnection(ctx);
        return await db.QuerySingleOrDefaultAsync<Note>(
            new CommandDefinition("SELECT * FROM notes WHERE id = @id", new { id }, cancellationToken: ct));
    }

    public Task<IEnumerable<Note>> GetAllAsync(ContextRef ctx, CancellationToken ct = default)
        => GetAllAsync(ctx, tags: null, match: "any", ct);

    public async Task<IEnumerable<Note>> GetAllAsync(
        ContextRef ctx,
        IReadOnlyCollection<string>? tags,
        string match,
        CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateContextConnection(ctx);

        if (tags is null || tags.Count == 0)
        {
            return await db.QueryAsync<Note>(new CommandDefinition(
                "SELECT * FROM notes ORDER BY updated_at DESC", cancellationToken: ct));
        }

        var sql = match == "all"
            ? @"SELECT * FROM notes
                WHERE (SELECT COUNT(DISTINCT je.value) FROM json_each(notes.tags) je
                       WHERE je.value IN @tags) = @count
                ORDER BY updated_at DESC"
            : @"SELECT * FROM notes
                WHERE EXISTS (SELECT 1 FROM json_each(notes.tags) je WHERE je.value IN @tags)
                ORDER BY updated_at DESC";

        return await db.QueryAsync<Note>(new CommandDefinition(
            sql, new { tags, count = tags.Count }, cancellationToken: ct));
    }

    public Task<string> CreateAsync(ContextRef ctx, string actorUserId, Note note, CancellationToken ct = default)
        => CreateAsync(ctx, actorUserId, note, NoteSource.Human, ct);

    public async Task<string> CreateAsync(ContextRef ctx, string actorUserId, Note note, NoteSource source, CancellationToken ct = default)
    {
        ApplySourceTags(note, source);

        if (string.IsNullOrEmpty(note.Id))
            note.Id = Ulid.NewUlid().ToString();

        _logger.LogDebug("Creating note {Id} in context {CtxType}:{CtxId}", note.Id, ctx.Type, ctx.Id);

        note.CreatedAt = DateTime.UtcNow;
        note.UpdatedAt = note.CreatedAt;
        note.CreatedBy = actorUserId;

        await _dbFactory.WithContextTransactionAsync(ctx, async (db, tx, token) =>
        {
            note.Tags = (await _tagRepository.EnsureExistsAsync(db, tx, note.Tags, token)).ToList();

            await db.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO notes (id, title, content, content_secret, type, tags, created_by, created_at, updated_at, pinned, archived)
                VALUES (@Id, @Title, @Content, @ContentSecret, @Type, @Tags, @CreatedBy, @CreatedAt, @UpdatedAt, @Pinned, @Archived)",
                new
                {
                    note.Id,
                    note.Title,
                    note.Content,
                    note.ContentSecret,
                    note.Type,
                    note.Tags,
                    note.CreatedBy,
                    CreatedAt = note.CreatedAt.ToString("o"),
                    UpdatedAt = note.UpdatedAt.ToString("o"),
                    Pinned = note.Pinned ? 1 : 0,
                    Archived = note.Archived ? 1 : 0
                }, transaction: tx, cancellationToken: token));

            await db.ExecuteAsync(new CommandDefinition(
                "INSERT INTO notes_fts (rowid, title, content, tags) VALUES ((SELECT rowid FROM notes WHERE id = @Id), @Title, @Content, @TagsFlat)",
                new
                {
                    note.Id,
                    note.Title,
                    note.Content,
                    TagsFlat = string.Join(' ', note.Tags)
                }, transaction: tx, cancellationToken: token));
        }, ct);

        return note.Id;
    }

    public Task<bool> UpdateAsync(ContextRef ctx, Note note, CancellationToken ct = default)
        => UpdateAsync(ctx, note, NoteSource.Human, ct);

    public async Task<bool> UpdateAsync(ContextRef ctx, Note note, NoteSource source, CancellationToken ct = default)
    {
        ApplySourceTags(note, source);
        note.UpdatedAt = DateTime.UtcNow;

        return await _dbFactory.WithContextTransactionAsync<bool>(ctx, async (db, tx, token) =>
        {
            note.Tags = (await _tagRepository.EnsureExistsAsync(db, tx, note.Tags, token)).ToList();

            var affected = await db.ExecuteAsync(new CommandDefinition(@"
                UPDATE notes
                SET title = @Title, content = @Content, content_secret = @ContentSecret,
                    type = @Type, tags = @Tags, updated_at = @UpdatedAt,
                    pinned = @Pinned, archived = @Archived
                WHERE id = @Id",
                new
                {
                    note.Title,
                    note.Content,
                    note.ContentSecret,
                    note.Type,
                    note.Tags,
                    UpdatedAt = note.UpdatedAt.ToString("o"),
                    Pinned = note.Pinned ? 1 : 0,
                    Archived = note.Archived ? 1 : 0,
                    note.Id
                }, transaction: tx, cancellationToken: token));

            if (affected > 0)
            {
                await db.ExecuteAsync(new CommandDefinition(
                    "UPDATE notes_fts SET title = @Title, content = @Content, tags = @TagsFlat WHERE rowid = (SELECT rowid FROM notes WHERE id = @Id)",
                    new
                    {
                        note.Id,
                        note.Title,
                        note.Content,
                        TagsFlat = string.Join(' ', note.Tags)
                    }, transaction: tx, cancellationToken: token));
            }
            else
            {
                _logger.LogWarning("Update of note {Id} in {CtxType}:{CtxId} matched no rows",
                    note.Id, ctx.Type, ctx.Id);
            }

            return affected > 0;
        }, ct);
    }

    public async Task<bool> DeleteAsync(ContextRef ctx, string id, CancellationToken ct = default)
    {
        return await _dbFactory.WithContextTransactionAsync<bool>(ctx, async (db, tx, token) =>
        {
            await db.ExecuteAsync(new CommandDefinition(
                "DELETE FROM notes_fts WHERE rowid = (SELECT rowid FROM notes WHERE id = @id)",
                new { id }, transaction: tx, cancellationToken: token));

            var affected = await db.ExecuteAsync(new CommandDefinition(
                "DELETE FROM notes WHERE id = @id",
                new { id }, transaction: tx, cancellationToken: token));

            return affected > 0;
        }, ct);
    }

    // ────────── Legacy (personal-context) aliases ──────────

    public Task<Note?> GetByIdAsync(string userId, string id, CancellationToken ct = default)
        => GetByIdAsync(ContextRef.User(userId), id, ct);

    public Task<IEnumerable<Note>> GetAllAsync(string userId, CancellationToken ct = default)
        => GetAllAsync(ContextRef.User(userId), ct);

    public Task<IEnumerable<Note>> GetAllAsync(
        string userId,
        IReadOnlyCollection<string>? tags,
        string match,
        CancellationToken ct = default)
        => GetAllAsync(ContextRef.User(userId), tags, match, ct);

    public Task<string> CreateAsync(string userId, Note note, CancellationToken ct = default)
        => CreateAsync(ContextRef.User(userId), userId, note, ct);

    public Task<bool> UpdateAsync(string userId, Note note, CancellationToken ct = default)
        => UpdateAsync(ContextRef.User(userId), note, ct);

    public Task<bool> DeleteAsync(string userId, string id, CancellationToken ct = default)
        => DeleteAsync(ContextRef.User(userId), id, ct);

    // ────────── Source-tag massage ──────────
    // Runs inside CreateAsync/UpdateAsync before the tag-repo's EnsureExists.
    // Mcp writes gain `source:mcp` + `review:pending` so the human notices
    // them in the review inbox. Human writes strip `review:pending` —
    // editing a pending note is implicit approval.
    private static void ApplySourceTags(Note note, NoteSource source)
    {
        note.Tags ??= new List<string>();
        var tags = new HashSet<string>(note.Tags, StringComparer.Ordinal);

        if (source == NoteSource.Mcp)
        {
            tags.Add("source:mcp");
            tags.Add("review:pending");
        }
        else
        {
            tags.Remove("review:pending");
        }

        note.Tags = tags.ToList();
    }
}
