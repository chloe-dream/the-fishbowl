using Fishbowl.Core.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Fishbowl.Core;

/// <summary>
/// Base interface for all Fishbowl plugins.
/// </summary>
public interface IFishbowlPlugin
{
    string Name { get; }
    string Version { get; }
    void Register(IServiceCollection services, IFishbowlApi api);
}

/// <summary>
/// Capability registration surface provided to plugins during Register().
/// </summary>
public interface IFishbowlApi
{
    void AddBotClient(IBotClient client);
    void AddSyncProvider(ISyncProvider provider);
    void AddScheduledJob(IScheduledJob job);
}
