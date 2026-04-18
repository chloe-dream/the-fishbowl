using System.Data;
using Dapper;
using Fishbowl.Core.Models;
using Fishbowl.Core.Repositories;

namespace Fishbowl.Data.Repositories;

public class NoteRepository : INoteRepository
{
    private readonly DatabaseFactory _dbFactory;

    public NoteRepository(DatabaseFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<Note?> GetByIdAsync(string userId, string id, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateConnection(userId);
        return await db.QuerySingleOrDefaultAsync<Note>(
            new CommandDefinition("SELECT * FROM notes WHERE id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<IEnumerable<Note>> GetAllAsync(string userId, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateConnection(userId);
        return await db.QueryAsync<Note>(
            new CommandDefinition("SELECT * FROM notes ORDER BY updated_at DESC", cancellationToken: ct));
    }

    public async Task<string> CreateAsync(string userId, Note note, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(note.Id))
            note.Id = Ulid.NewUlid().ToString();

        note.CreatedAt = DateTime.UtcNow;
        note.UpdatedAt = note.CreatedAt;
        note.CreatedBy = userId;

        await _dbFactory.WithUserTransactionAsync(userId, async (db, tx, token) =>
        {
            await db.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO notes (id, title, content, content_secret, type, tags, created_by, created_at, updated_at, pinned, archived)
                VALUES (@Id, @Title, @Content, @ContentSecret, @Type, @Tags, @CreatedBy, @CreatedAt, @UpdatedAt, @Pinned, @Archived)",
                new {
                    note.Id, note.Title, note.Content, note.ContentSecret, note.Type,
                    note.Tags, note.CreatedBy,
                    CreatedAt = note.CreatedAt.ToString("o"),
                    UpdatedAt = note.UpdatedAt.ToString("o"),
                    Pinned = note.Pinned ? 1 : 0,
                    Archived = note.Archived ? 1 : 0
                }, transaction: tx, cancellationToken: token));

            await db.ExecuteAsync(new CommandDefinition(
                "INSERT INTO notes_fts (rowid, title, content, tags) VALUES ((SELECT rowid FROM notes WHERE id = @Id), @Title, @Content, @TagsFlat)",
                new {
                    note.Id, note.Title, note.Content,
                    TagsFlat = string.Join(' ', note.Tags)
                }, transaction: tx, cancellationToken: token));
        }, ct);

        return note.Id;
    }

    public async Task<bool> UpdateAsync(string userId, Note note, CancellationToken ct = default)
    {
        note.UpdatedAt = DateTime.UtcNow;

        return await _dbFactory.WithUserTransactionAsync<bool>(userId, async (db, tx, token) =>
        {
            var affected = await db.ExecuteAsync(new CommandDefinition(@"
                UPDATE notes
                SET title = @Title, content = @Content, content_secret = @ContentSecret,
                    type = @Type, tags = @Tags, updated_at = @UpdatedAt,
                    pinned = @Pinned, archived = @Archived
                WHERE id = @Id",
                new {
                    note.Title, note.Content, note.ContentSecret, note.Type,
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
                    new {
                        note.Id, note.Title, note.Content,
                        TagsFlat = string.Join(' ', note.Tags)
                    }, transaction: tx, cancellationToken: token));
            }

            return affected > 0;
        }, ct);
    }

    public async Task<bool> DeleteAsync(string userId, string id, CancellationToken ct = default)
    {
        return await _dbFactory.WithUserTransactionAsync<bool>(userId, async (db, tx, token) =>
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

}
