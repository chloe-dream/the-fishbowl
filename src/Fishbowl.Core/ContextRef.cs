namespace Fishbowl.Core;

// Discriminated identifier for a data-holding SQLite file. Personal notes
// live in `users/{userId}.db`, team/project notes in `teams/{teamId}.db`. The
// schema is identical — only ownership differs. DatabaseFactory resolves a
// ContextRef to the right file; repositories take ContextRef so a single
// implementation serves both cookie-auth (personal) and Bearer-auth (team)
// callers without duplicate code paths.
public readonly record struct ContextRef(ContextType Type, string Id)
{
    public static ContextRef User(string id) => new(ContextType.User, id);
    public static ContextRef Team(string id) => new(ContextType.Team, id);
}

public enum ContextType
{
    User,
    Team,
}
