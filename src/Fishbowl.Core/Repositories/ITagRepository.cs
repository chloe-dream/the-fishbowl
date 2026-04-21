using System.Data;
using Fishbowl.Core.Models;

namespace Fishbowl.Core.Repositories;

public interface ITagRepository
{
    Task<IEnumerable<Tag>> GetAllAsync(string userId, CancellationToken ct = default);
    Task<Tag> UpsertColorAsync(string userId, string name, string color, CancellationToken ct = default);
    Task<bool> RenameAsync(string userId, string oldName, string newName, CancellationToken ct = default);
    Task<bool> DeleteAsync(string userId, string name, CancellationToken ct = default);

    // In-transaction hook used by NoteRepository on save: registers any tag
    // names not yet known with a deterministic default color, and normalises
    // them in place. Returns the normalised list (deduped).
    Task<IReadOnlyList<string>> EnsureExistsAsync(
        IDbConnection db,
        IDbTransaction tx,
        IEnumerable<string> rawNames,
        CancellationToken ct = default);
}
