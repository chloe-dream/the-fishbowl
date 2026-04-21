using Fishbowl.Core.Models;

namespace Fishbowl.Core.Repositories;

public interface ISystemRepository
{
    // User Mapping
    Task<string?> GetUserIdByMappingAsync(string provider, string providerId, CancellationToken ct = default);
    Task<bool> CreateUserMappingAsync(string userId, string provider, string providerId, CancellationToken ct = default);

    // User Profile
    Task<bool> CreateUserAsync(string userId, string? name, string? email, string? avatarUrl, CancellationToken ct = default);

    /// <summary>
    /// Insert-or-update the user's profile snapshot. Called on every successful
    /// login so name/email/avatar stay in sync with what the provider sends.
    /// Returns true on insert or when at least one field changed.
    /// </summary>
    Task<bool> UpsertUserAsync(string userId, string? name, string? email, string? avatarUrl, CancellationToken ct = default);

    Task<User?> GetUserAsync(string userId, CancellationToken ct = default);

    // Configuration
    Task<string?> GetConfigAsync(string key, CancellationToken ct = default);
    Task<bool> SetConfigAsync(string key, string value, CancellationToken ct = default);
}
