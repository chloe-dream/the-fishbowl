using Dapper;
using Fishbowl.Core;
using Fishbowl.Data;
using Fishbowl.Data.Repositories;

// Dev utility: mints a personal-scope API key for local testing and prints
// the raw token to stdout. Intended to be run once, piped into `.mcp.json`
// or copy-pasted into Claude Code's MCP config — production keys still get
// minted through the UI (Settings → API Keys).
//
// Usage:
//   dotnet run --project tools/mint-dev-key -- [--data <path>] [--user <id>] [--name <label>]
//
// Defaults:
//   --data fishbowl-data            (matches Fishbowl.Host's default)
//   --user <first user in system.db>
//   --name claude-code-local

var args_ = args;
var dataPath = GetArg("--data") ?? "fishbowl-data";
var userIdArg = GetArg("--user");
var name = GetArg("--name") ?? "claude-code-local";

if (!Directory.Exists(dataPath) || !File.Exists(Path.Combine(dataPath, "system.db")))
{
    Console.Error.WriteLine($"error: no system.db found at {Path.GetFullPath(dataPath)}/system.db — start the host at least once first.");
    return 2;
}

var factory = new DatabaseFactory(dataPath);

string userId;
if (!string.IsNullOrEmpty(userIdArg))
{
    userId = userIdArg;
}
else
{
    using var sys = factory.CreateSystemConnection();
    var first = sys.QueryFirstOrDefault<string>(
        "SELECT id FROM users ORDER BY created_at LIMIT 1");
    if (string.IsNullOrEmpty(first))
    {
        Console.Error.WriteLine("error: no users in system.db — log in via the web UI first, then retry.");
        return 3;
    }
    userId = first;
}

var keys = new ApiKeyRepository(factory);
var issued = await keys.IssueAsync(
    userId,
    ContextRef.User(userId),
    name,
    new[] { "read:notes", "write:notes" });

Console.WriteLine(issued.RawToken);
Console.Error.WriteLine($"# minted key id={issued.Record.Id} user={userId} scopes=read:notes,write:notes");
return 0;

string? GetArg(string flag)
{
    var i = Array.IndexOf(args_, flag);
    if (i < 0 || i + 1 >= args_.Length) return null;
    return args_[i + 1];
}
