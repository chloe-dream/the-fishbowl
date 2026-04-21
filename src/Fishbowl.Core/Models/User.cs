using System;

namespace Fishbowl.Core.Models;

/// <summary>
/// A Fishbowl user — internal identity (GUID) plus the most recent profile
/// snapshot from the auth provider. Refreshed on every successful login via
/// <c>ISystemRepository.UpsertUserAsync</c>.
/// </summary>
public class User
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
