using Fishbowl.Core.Models;

namespace Fishbowl.Core.Repositories;

public interface ITodoRepository
{
    Task<TodoItem?> GetByIdAsync(string userId, string id, CancellationToken ct = default);
    Task<IEnumerable<TodoItem>> GetAllAsync(string userId, bool includeCompleted = false, CancellationToken ct = default);
    Task<string> CreateAsync(string userId, TodoItem item, CancellationToken ct = default);
    Task<bool> UpdateAsync(string userId, TodoItem item, CancellationToken ct = default);
    Task<bool> DeleteAsync(string userId, string id, CancellationToken ct = default);
}
