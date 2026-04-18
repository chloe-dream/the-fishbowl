using Dapper;

namespace Fishbowl.Data.Dapper;

public static class DapperConventions
{
    private static bool _installed;
    private static readonly object _lock = new();

    /// <summary>
    /// Enables snake_case ↔ PascalCase column mapping and registers custom type handlers.
    /// Safe to call multiple times; installs exactly once per process.
    /// </summary>
    public static void Install()
    {
        lock (_lock)
        {
            if (_installed) return;

            DefaultTypeMap.MatchNamesWithUnderscores = true;
            SqlMapper.AddTypeHandler(new JsonTagsHandler());

            _installed = true;
        }
    }
}
