using System.Data;
using Dapper;
using Fishbowl.Core.Models;
using Fishbowl.Core.Repositories;

namespace Fishbowl.Data.Repositories;

public class TodoRepository : ITodoRepository
{
    private readonly DatabaseFactory _dbFactory;

    public TodoRepository(DatabaseFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<TodoItem?> GetByIdAsync(string userId, string id)
    {
        using var db = _dbFactory.CreateConnection(userId);
        var row = await db.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT * FROM todos WHERE id = @id", new { id });

        if (row == null) return null;

        return MapRowToTodo(row);
    }

    public async Task<IEnumerable<TodoItem>> GetAllAsync(string userId, bool includeCompleted = false)
    {
        using var db = _dbFactory.CreateConnection(userId);
        string sql = "SELECT * FROM todos";
        if (!includeCompleted)
        {
            sql += " WHERE completed_at IS NULL";
        }
        sql += " ORDER BY created_at DESC";

        var rows = await db.QueryAsync<dynamic>(sql);
        return rows.Select(MapRowToTodo);
    }

    public async Task<string> CreateAsync(string userId, TodoItem item)
    {
        if (string.IsNullOrEmpty(item.Id))
        {
            item.Id = Ulid.NewUlid().ToString();
        }
        
        item.CreatedAt = DateTime.UtcNow;
        item.UpdatedAt = item.CreatedAt;
        item.CreatedBy = userId;

        using var db = _dbFactory.CreateConnection(userId);
        await db.ExecuteAsync(@"
            INSERT INTO todos (id, title, description, due_at, reminder_at, source, created_by, created_at, updated_at, completed_at)
            VALUES (@Id, @Title, @Description, @DueAt, @ReminderAt, @Source, @CreatedBy, @CreatedAt, @UpdatedAt, @CompletedAt)",
            new {
                item.Id,
                item.Title,
                item.Description,
                DueAt = item.DueAt?.ToString("o"),
                ReminderAt = item.ReminderAt?.ToString("o"),
                item.Source,
                item.CreatedBy,
                CreatedAt = item.CreatedAt.ToString("o"),
                UpdatedAt = item.UpdatedAt.ToString("o"),
                CompletedAt = item.CompletedAt?.ToString("o")
            });

        return item.Id;
    }

    public async Task<bool> UpdateAsync(string userId, TodoItem item)
    {
        item.UpdatedAt = DateTime.UtcNow;

        using var db = _dbFactory.CreateConnection(userId);
        var affected = await db.ExecuteAsync(@"
            UPDATE todos 
            SET title = @Title, 
                description = @Description, 
                due_at = @DueAt, 
                reminder_at = @ReminderAt, 
                source = @Source, 
                updated_at = @UpdatedAt, 
                completed_at = @CompletedAt
            WHERE id = @Id",
            new {
                item.Title,
                item.Description,
                DueAt = item.DueAt?.ToString("o"),
                ReminderAt = item.ReminderAt?.ToString("o"),
                item.Source,
                UpdatedAt = item.UpdatedAt.ToString("o"),
                CompletedAt = item.CompletedAt?.ToString("o"),
                item.Id
            });

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(string userId, string id)
    {
        using var db = _dbFactory.CreateConnection(userId);
        var affected = await db.ExecuteAsync("DELETE FROM todos WHERE id = @id", new { id });
        return affected > 0;
    }

    private TodoItem MapRowToTodo(dynamic row)
    {
        return new TodoItem
        {
            Id = row.id,
            Title = row.title,
            Description = row.description,
            DueAt = row.due_at != null ? DateTime.Parse(row.due_at) : null,
            ReminderAt = row.reminder_at != null ? DateTime.Parse(row.reminder_at) : null,
            Source = row.source,
            CreatedBy = row.created_by,
            CreatedAt = DateTime.Parse(row.created_at),
            UpdatedAt = DateTime.Parse(row.updated_at),
            CompletedAt = row.completed_at != null ? DateTime.Parse(row.completed_at) : null
        };
    }
}
