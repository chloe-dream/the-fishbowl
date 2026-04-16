using Fishbowl.Core.Models;

namespace Fishbowl.Core.Repositories;

public interface INoteRepository
{
    Task<Note?> GetByIdAsync(string userId, string id, CancellationToken ct = default);
    Task<IEnumerable<Note>> GetAllAsync(string userId, CancellationToken ct = default);
    Task<string> CreateAsync(string userId, Note note, CancellationToken ct = default);
    Task<bool> UpdateAsync(string userId, Note note, CancellationToken ct = default);
    Task<bool> DeleteAsync(string userId, string id, CancellationToken ct = default);
}
