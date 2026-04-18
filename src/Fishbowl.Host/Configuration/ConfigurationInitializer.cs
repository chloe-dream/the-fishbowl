using Fishbowl.Core.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fishbowl.Host.Configuration;

/// <summary>
/// Runs once at application startup, before the HTTP server listens.
/// Reads system_config into ConfigurationCache so options binders can
/// read synchronously.
/// </summary>
public class ConfigurationInitializer : IHostedService
{
    private static readonly string[] TrackedKeys =
    {
        "Google:ClientId",
        "Google:ClientSecret",
    };

    private readonly ISystemRepository _repo;
    private readonly ConfigurationCache _cache;
    private readonly ILogger<ConfigurationInitializer> _logger;

    public ConfigurationInitializer(
        ISystemRepository repo,
        ConfigurationCache cache,
        ILogger<ConfigurationInitializer> logger)
    {
        _repo = repo;
        _cache = cache;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        foreach (var key in TrackedKeys)
        {
            var value = await _repo.GetConfigAsync(key, ct);
            _cache.Set(key, value);
        }
        _logger.LogInformation("Configuration cache populated with {Count} keys", TrackedKeys.Length);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
