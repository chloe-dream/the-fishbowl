using System.Collections.Concurrent;

namespace Fishbowl.Host.Configuration;

/// <summary>
/// Thread-safe in-memory snapshot of values from system_config.
/// Populated once at startup by ConfigurationInitializer; updated
/// in-place by /api/setup when config changes.
/// </summary>
public class ConfigurationCache
{
    private readonly ConcurrentDictionary<string, string?> _values = new();

    public string? Get(string key) => _values.TryGetValue(key, out var v) ? v : null;

    public void Set(string key, string? value) => _values[key] = value;
}
