using Fishbowl.Core.Models;

namespace Fishbowl.Core.Repositories;

public interface INoteRepository
{
    Task<Note?> GetByIdAsync(string userId, string id);
    Task<IEnumerable<Note>> GetAllAsync(string userId);
    Task<string> CreateAsync(string userId, Note note);
    Task<bool> UpdateAsync(string userId, Note note);
    Task<bool> DeleteAsync(string userId, string id);
}
