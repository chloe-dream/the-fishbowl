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
    Task<Resource?> GetAsync(string path);

    /// <summary>
    /// Checks if a resource exists.
    /// </summary>
    Task<bool> ExistsAsync(string path);
}
