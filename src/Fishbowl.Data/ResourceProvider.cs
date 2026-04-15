using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Fishbowl.Core;

namespace Fishbowl.Data;

public class ResourceProvider : IResourceProvider
{
    private readonly string _modsPath;
    private readonly Assembly _embeddedAssembly;

    public ResourceProvider(string modsPath = "fishbowl-mods", Assembly? embeddedAssembly = null)
    {
        _modsPath = modsPath;
        _embeddedAssembly = embeddedAssembly ?? Assembly.GetExecutingAssembly();
    }

    public async Task<Resource?> GetAsync(string path)
    {
        // 1. Check Disk (Mods)
        var diskPath = Path.Combine(_modsPath, path);
        if (File.Exists(diskPath))
        {
            var stream = File.OpenRead(diskPath);
            return new Resource(stream, path, ResourceSource.Disk);
        }

        // 2. Check Database (TODO: Implement when DB service is ready)
        // var dbResource = await _db.GetResourceAsync(path);
        // if (dbResource != null) return dbResource;

        // 3. Check Embedded
        var embeddedPath = path.Replace('/', '.').Replace('\\', '.');
        // Assembly resources are usually prefixed with the default namespace
        var resourceName = $"{_embeddedAssembly.GetName().Name}.Resources.{embeddedPath}";
        var streamEmbedded = _embeddedAssembly.GetManifestResourceStream(resourceName);
        
        if (streamEmbedded != null)
        {
            return new Resource(streamEmbedded, path, ResourceSource.Embedded);
        }

        return null;
    }

    public Task<bool> ExistsAsync(string path)
    {
        var diskPath = Path.Combine(_modsPath, path);
        if (File.Exists(diskPath)) return Task.FromResult(true);

        var embeddedPath = path.Replace('/', '.').Replace('\\', '.');
        var resourceName = $"{_embeddedAssembly.GetName().Name}.Resources.{embeddedPath}";
        var manifestNames = _embeddedAssembly.GetManifestResourceNames();
        
        foreach (var name in manifestNames)
        {
            if (name.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
