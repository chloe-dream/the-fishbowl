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
/// Core API provided to plugins during registration.
/// </summary>
public interface IFishbowlApi
{
    void AddBotClient(object client); // TODO: Define IBotClient
    void AddSyncProvider(object provider); // TODO: Define ISyncProvider
    void AddScheduledJob(object job); // TODO: Define IScheduledJob
}
