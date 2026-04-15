using Fishbowl.Core.Models;

namespace Fishbowl.Core.Repositories;

public interface ITodoRepository
{
    Task<TodoItem?> GetByIdAsync(string userId, string id);
    Task<IEnumerable<TodoItem>> GetAllAsync(string userId, bool includeCompleted = false);
    Task<string> CreateAsync(string userId, TodoItem item);
    Task<bool> UpdateAsync(string userId, TodoItem item);
    Task<bool> DeleteAsync(string userId, string id);
}
