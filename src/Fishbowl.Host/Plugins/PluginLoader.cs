using Fishbowl.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Fishbowl.Host.Plugins;

public static class PluginLoader
{
    public static void LoadPlugins(IServiceCollection services, string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return;

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
                    Console.WriteLine($"[Plugin] Loaded {plugin.Name} v{plugin.Version} from {Path.GetFileName(dllPath)}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Plugin] Failed to load {Path.GetFileName(dllPath)}: {ex.Message}");
            }
        }
    }
}
