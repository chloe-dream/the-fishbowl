using System.Data;
using System.Text.Json;
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
        var row = await db.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition("SELECT * FROM notes WHERE id = @id", new { id }, cancellationToken: ct));

        if (row == null) return null;

        return MapRowToNote(row);
    }

    public async Task<IEnumerable<Note>> GetAllAsync(string userId, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateConnection(userId);
        var rows = await db.QueryAsync<dynamic>(
            new CommandDefinition("SELECT * FROM notes ORDER BY updated_at DESC", cancellationToken: ct));
        return rows.Select(MapRowToNote);
    }

    public async Task<string> CreateAsync(string userId, Note note, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(note.Id))
        {
            note.Id = Ulid.NewUlid().ToString();
        }
        
        note.CreatedAt = DateTime.UtcNow;
        note.UpdatedAt = note.CreatedAt;
        note.CreatedBy = userId;

        using var db = _dbFactory.CreateConnection(userId);
        await db.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO notes (id, title, content, content_secret, type, tags, created_by, created_at, updated_at, pinned, archived)
            VALUES (@Id, @Title, @Content, @ContentSecret, @Type, @TagsJson, @CreatedBy, @CreatedAt, @UpdatedAt, @Pinned, @Archived)",
            new {
                note.Id,
                note.Title,
                note.Content,
                note.ContentSecret,
                note.Type,
                TagsJson = JsonSerializer.Serialize(note.Tags),
                note.CreatedBy,
                CreatedAt = note.CreatedAt.ToString("o"),
                UpdatedAt = note.UpdatedAt.ToString("o"),
                Pinned = note.Pinned ? 1 : 0,
                Archived = note.Archived ? 1 : 0
            }, cancellationToken: ct));

        return note.Id;
    }

    public async Task<bool> UpdateAsync(string userId, Note note, CancellationToken ct = default)
    {
        note.UpdatedAt = DateTime.UtcNow;

        using var db = _dbFactory.CreateConnection(userId);
        var affected = await db.ExecuteAsync(new CommandDefinition(@"
            UPDATE notes 
            SET title = @Title, 
                content = @Content, 
                content_secret = @ContentSecret, 
                type = @Type, 
                tags = @TagsJson, 
                updated_at = @UpdatedAt, 
                pinned = @Pinned, 
                archived = @Archived
            WHERE id = @Id",
            new {
                note.Title,
                note.Content,
                note.ContentSecret,
                note.Type,
                TagsJson = JsonSerializer.Serialize(note.Tags),
                UpdatedAt = note.UpdatedAt.ToString("o"),
                Pinned = note.Pinned ? 1 : 0,
                Archived = note.Archived ? 1 : 0,
                note.Id
            }, cancellationToken: ct));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(string userId, string id, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateConnection(userId);
        var affected = await db.ExecuteAsync(new CommandDefinition("DELETE FROM notes WHERE id = @id", new { id }, cancellationToken: ct));
        return affected > 0;
    }

    private Note MapRowToNote(dynamic row)
    {
        return new Note
        {
            Id = row.id,
            Title = row.title,
            Content = row.content,
            ContentSecret = row.content_secret,
            Type = row.type,
            Tags = string.IsNullOrEmpty(row.tags) ? new List<string>() : JsonSerializer.Deserialize<List<string>>((string)row.tags) ?? new List<string>(),
            CreatedBy = row.created_by,
            CreatedAt = DateTime.Parse(row.created_at),
            UpdatedAt = DateTime.Parse(row.updated_at),
            Pinned = row.pinned == 1,
            Archived = row.archived == 1
        };
    }
}
