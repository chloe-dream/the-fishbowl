using System;
using System.IO;
using System.Threading.Tasks;

namespace Fishbowl.Core;

/// <summary>
/// Source of a resource.
/// </summary>
public enum ResourceSource
{
    Disk,
    Database,
    Embedded
}

/// <summary>
/// Represents a system or mod resource.
/// </summary>
public record Resource(byte[] Data, string Path, ResourceSource Source, string? Hash = null);

/// <summary>
/// Provides access to system and mod resources with a priority system:
/// Disk > Database > Embedded.
/// </summary>
public interface IResourceProvider
{
    /// <summary>
    /// Retrieves a resource by its path.
    /// </summary>
    /// <param name="bypassCache">
    /// When true, skips the in-memory cache (both read and write) and
    /// re-reads the resource from disk/embedded on every call. Intended
    /// for the dev loop — the HTTP layer sets this when the browser sends
    /// <c>Cache-Control: no-cache</c> (i.e. devtools open with "Disable
    /// cache" checked).
    /// </param>
    Task<Resource?> GetAsync(string path, CancellationToken ct = default, bool bypassCache = false);

    /// <summary>
    /// Checks if a resource exists.
    /// </summary>
    Task<bool> ExistsAsync(string path, CancellationToken ct = default);
}
