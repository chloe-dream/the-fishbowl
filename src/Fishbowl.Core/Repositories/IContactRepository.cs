using Fishbowl.Core;
using Fishbowl.Core.Models;

namespace Fishbowl.Core.Repositories;

public interface IContactRepository
{
    // ContextRef overloads — canonical shape. Personal and team contacts
    // share identical schema (per CONCEPT.md § Teams "identical to user
    // DBs"); the context picks the file.

    Task<Contact?> GetByIdAsync(ContextRef ctx, string id, CancellationToken ct = default);
    Task<IEnumerable<Contact>> GetAllAsync(ContextRef ctx, bool includeArchived = false, CancellationToken ct = default);
    Task<string> CreateAsync(ContextRef ctx, string actorUserId, Contact contact, CancellationToken ct = default);
    Task<bool> UpdateAsync(ContextRef ctx, Contact contact, CancellationToken ct = default);
    Task<bool> DeleteAsync(ContextRef ctx, string id, CancellationToken ct = default);

    // Legacy personal-context aliases. Cookie-auth callers that only hold
    // a userId keep a one-line signature.
    Task<Contact?> GetByIdAsync(string userId, string id, CancellationToken ct = default);
    Task<IEnumerable<Contact>> GetAllAsync(string userId, bool includeArchived = false, CancellationToken ct = default);
    Task<string> CreateAsync(string userId, Contact contact, CancellationToken ct = default);
    Task<bool> UpdateAsync(string userId, Contact contact, CancellationToken ct = default);
    Task<bool> DeleteAsync(string userId, string id, CancellationToken ct = default);
}
