using Fishbowl.Core.Models;

namespace Fishbowl.Core.Repositories;

public record TeamMembership(Team Team, TeamRole Role);

public interface ITeamRepository
{
    // Creates a team owned by the given user. Slug is derived from `name` and
    // disambiguated against existing teams. The creator is inserted as the
    // sole owner member.
    Task<Team> CreateAsync(string ownerUserId, string name, CancellationToken ct = default);

    // Teams the user belongs to, with their role in each. Ordered by team name.
    Task<IReadOnlyList<TeamMembership>> ListByMemberAsync(string userId, CancellationToken ct = default);

    Task<Team?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Team?> GetByIdAsync(string teamId, CancellationToken ct = default);

    // Null if the user isn't a member of the given team.
    Task<TeamRole?> GetMembershipAsync(string teamId, string userId, CancellationToken ct = default);

    // Owner-only. Returns true on success, false if the user isn't the owner
    // or the team doesn't exist. Leaves the .db file in place — callers can
    // keep the data for recovery or delete it themselves.
    Task<bool> DeleteAsync(string teamId, string actingUserId, CancellationToken ct = default);
}
