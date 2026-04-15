using System.Reflection;
using Fishbowl.Core;
using Microsoft.Extensions.Caching.Memory;

namespace Fishbowl.Data;

public class ResourceProvider : IResourceProvider
{
    private readonly string _modsPath;
    private readonly Assembly _embeddedAssembly;
    private readonly IMemoryCache _cache;

    public ResourceProvider(IMemoryCache cache, string modsPath = "fishbowl-mods", Assembly? embeddedAssembly = null)
    {
        _cache = cache;
        _modsPath = modsPath;
        _embeddedAssembly = embeddedAssembly ?? Assembly.GetExecutingAssembly();
    }

    public async Task<Resource?> GetAsync(string path)
    {
        if (_cache.TryGetValue(path, out Resource? cached))
        {
            return cached;
        }

        Resource? resource = null;

        // 1. Check Disk (Mods)
        var diskPath = Path.Combine(_modsPath, path);
        if (File.Exists(diskPath))
        {
            var data = await File.ReadAllBytesAsync(diskPath);
            resource = new Resource(data, path, ResourceSource.Disk);
        }

        // 2. Check Database (TODO: Implement when DB service is ready)
        if (resource == null)
        {
            // var dbResource = await _db.GetResourceAsync(path);
            // if (dbResource != null) resource = dbResource;
        }

        // 3. Check Embedded
        if (resource == null)
        {
            var embeddedPath = path.Replace('/', '.').Replace('\\', '.');
            var resourceName = $"{_embeddedAssembly.GetName().Name}.Resources.{embeddedPath}";
            using var stream = _embeddedAssembly.GetManifestResourceStream(resourceName);
            
            if (stream != null)
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                resource = new Resource(ms.ToArray(), path, ResourceSource.Embedded);
            }
        }

        if (resource != null)
        {
            _cache.Set(path, resource);
        }

        return resource;
    }

    public async Task<bool> ExistsAsync(string path)
    {
        if (_cache.TryGetValue(path, out _)) return true;

        var diskPath = Path.Combine(_modsPath, path);
        if (File.Exists(diskPath)) return true;

        var embeddedPath = path.Replace('/', '.').Replace('\\', '.');
        var resourceName = $"{_embeddedAssembly.GetName().Name}.Resources.{embeddedPath}";
        var manifestNames = _embeddedAssembly.GetManifestResourceNames();
        
        foreach (var name in manifestNames)
        {
            if (name.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
