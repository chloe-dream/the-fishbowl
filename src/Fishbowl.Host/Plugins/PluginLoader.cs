using Fishbowl.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fishbowl.Host.Plugins;

public static class PluginLoader
{
    public static void LoadPlugins(IServiceCollection services, string pluginsPath, ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;

        if (!Directory.Exists(pluginsPath))
        {
            logger.LogDebug("Plugins path {Path} does not exist; skipping plugin load", pluginsPath);
            return;
        }

        var api = new FishbowlApi(services);

        foreach (var dllPath in Directory.EnumerateFiles(pluginsPath, "*.dll"))
        {
            try
            {
                var alc = new PluginLoadContext(dllPath);
                var assembly = alc.LoadFromAssemblyPath(dllPath);

                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IFishbowlPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

                foreach (var type in pluginTypes)
                {
                    var plugin = (IFishbowlPlugin)Activator.CreateInstance(type)!;
                    plugin.Register(services, api);
                    logger.LogInformation("Loaded plugin {Name} v{Version} from {File}", plugin.Name, plugin.Version, Path.GetFileName(dllPath));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load plugin {File}", Path.GetFileName(dllPath));
            }
        }
    }
}
