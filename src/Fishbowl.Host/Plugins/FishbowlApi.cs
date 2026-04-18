using Fishbowl.Core;
using Fishbowl.Core.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Fishbowl.Host.Plugins;

/// <summary>
/// Host-side implementation of IFishbowlApi. Registers plugin-contributed
/// clients, providers, and jobs into the DI container.
/// </summary>
public class FishbowlApi : IFishbowlApi
{
    private readonly IServiceCollection _services;

    public FishbowlApi(IServiceCollection services)
    {
        _services = services;
    }

    public void AddBotClient(IBotClient client) =>
        _services.AddSingleton(client);

    public void AddSyncProvider(ISyncProvider provider) =>
        _services.AddSingleton(provider);

    public void AddScheduledJob(IScheduledJob job) =>
        _services.AddSingleton(job);
}
