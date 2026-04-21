using Fishbowl.Core;
using Fishbowl.Core.Models;

namespace Fishbowl.Core.Repositories;

public interface INoteRepository
{
    // Context-aware overloads. `actorUserId` is the user writing/creating —
    // stored as created_by. For personal context it usually equals ctx.Id;
    // for team context it's the logged-in user, not the team id.

    Task<Note?> GetByIdAsync(ContextRef ctx, string id, CancellationToken ct = default);
    Task<IEnumerable<Note>> GetAllAsync(ContextRef ctx, CancellationToken ct = default);

    Task<IEnumerable<Note>> GetAllAsync(
        ContextRef ctx,
        IReadOnlyCollection<string>? tags,
        string match,
        CancellationToken ct = default);

    Task<string> CreateAsync(ContextRef ctx, string actorUserId, Note note, CancellationToken ct = default);
    Task<string> CreateAsync(ContextRef ctx, string actorUserId, Note note, NoteSource source, CancellationToken ct = default);
    Task<bool> UpdateAsync(ContextRef ctx, Note note, CancellationToken ct = default);
    Task<bool> UpdateAsync(ContextRef ctx, Note note, NoteSource source, CancellationToken ct = default);
    Task<bool> DeleteAsync(ContextRef ctx, string id, CancellationToken ct = default);

    // Legacy (personal-context) aliases. Kept so existing cookie-auth call
    // sites stay minimal-diff. Implementations delegate to the ContextRef
    // versions with ContextRef.User(userId).

    Task<Note?> GetByIdAsync(string userId, string id, CancellationToken ct = default);
    Task<IEnumerable<Note>> GetAllAsync(string userId, CancellationToken ct = default);

    Task<IEnumerable<Note>> GetAllAsync(
        string userId,
        IReadOnlyCollection<string>? tags,
        string match,
        CancellationToken ct = default);

    Task<string> CreateAsync(string userId, Note note, CancellationToken ct = default);
    Task<bool> UpdateAsync(string userId, Note note, CancellationToken ct = default);
    Task<bool> DeleteAsync(string userId, string id, CancellationToken ct = default);
}
