using Fishbowl.Core.Mcp;

namespace Fishbowl.Mcp;

// Dictionary-backed lookup over the DI-registered IMcpTool instances.
// Registered as a singleton in Program.cs; tools themselves are also
// singletons so they can be cached without rebuilding every request.
public class ToolRegistry
{
    private readonly Dictionary<string, IMcpTool> _byName;
    public IReadOnlyList<IMcpTool> All { get; }

    public ToolRegistry(IEnumerable<IMcpTool> tools)
    {
        All = tools.ToList();
        _byName = All.ToDictionary(t => t.Name, StringComparer.Ordinal);
    }

    public IMcpTool? Get(string name) => _byName.TryGetValue(name, out var t) ? t : null;
}
