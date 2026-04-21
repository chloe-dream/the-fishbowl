using Dapper;
using Fishbowl.Core.Models;
using Fishbowl.Core.Repositories;
using Fishbowl.Core.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fishbowl.Data.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly DatabaseFactory _dbFactory;
    private readonly ILogger<TeamRepository> _logger;

    public TeamRepository(DatabaseFactory dbFactory, ILogger<TeamRepository>? logger = null)
    {
        _dbFactory = dbFactory;
        _logger = logger ?? NullLogger<TeamRepository>.Instance;
    }

    public async Task<Team> CreateAsync(string ownerUserId, string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Team name is required.", nameof(name));

        using var db = _dbFactory.CreateSystemConnection();
        using var tx = db.BeginTransaction();

        var baseSlug = SlugGenerator.FromName(name);
        var slug = SlugGenerator.DedupeAgainst(baseSlug, candidate =>
            db.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM teams WHERE slug = @candidate",
                new { candidate }, transaction: tx) > 0);

        var team = new Team
        {
            Id = Ulid.NewUlid().ToString(),
            Slug = slug,
            Name = name.Trim(),
            CreatedBy = ownerUserId,
            CreatedAt = DateTime.UtcNow,
        };

        await db.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO teams(id, slug, name, created_by, created_at)
            VALUES (@Id, @Slug, @Name, @CreatedBy, @CreatedAt)",
            new
            {
                team.Id,
                team.Slug,
                team.Name,
                team.CreatedBy,
                CreatedAt = team.CreatedAt.ToString("o"),
            }, transaction: tx, cancellationToken: ct));

        await db.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO team_members(team_id, user_id, role, joined_at)
            VALUES (@TeamId, @UserId, @Role, @JoinedAt)",
            new
            {
                TeamId = team.Id,
                UserId = ownerUserId,
                Role = TeamRole.Owner.ToDbValue(),
                JoinedAt = team.CreatedAt.ToString("o"),
            }, transaction: tx, cancellationToken: ct));

        tx.Commit();
        _logger.LogInformation("Created team {TeamId} slug={Slug}", team.Id, team.Slug);
        return team;
    }

    public async Task<IReadOnlyList<TeamMembership>> ListByMemberAsync(string userId, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateSystemConnection();
        var rows = await db.QueryAsync<(string Id, string Slug, string Name, string CreatedBy, string CreatedAt, string Role)>(
            new CommandDefinition(@"
                SELECT t.id AS Id, t.slug AS Slug, t.name AS Name,
                       t.created_by AS CreatedBy, t.created_at AS CreatedAt,
                       m.role AS Role
                FROM teams t
                JOIN team_members m ON m.team_id = t.id
                WHERE m.user_id = @userId
                ORDER BY t.name",
                new { userId }, cancellationToken: ct));

        return rows.Select(r => new TeamMembership(
            new Team
            {
                Id = r.Id,
                Slug = r.Slug,
                Name = r.Name,
                CreatedBy = r.CreatedBy,
                CreatedAt = DateTime.Parse(r.CreatedAt, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind),
            },
            TeamRoleExtensions.FromDbValue(r.Role))).ToList();
    }

    public async Task<Team?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateSystemConnection();
        return await db.QuerySingleOrDefaultAsync<Team>(new CommandDefinition(
            "SELECT * FROM teams WHERE slug = @slug",
            new { slug }, cancellationToken: ct));
    }

    public async Task<Team?> GetByIdAsync(string teamId, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateSystemConnection();
        return await db.QuerySingleOrDefaultAsync<Team>(new CommandDefinition(
            "SELECT * FROM teams WHERE id = @teamId",
            new { teamId }, cancellationToken: ct));
    }

    public async Task<TeamRole?> GetMembershipAsync(string teamId, string userId, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateSystemConnection();
        var role = await db.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT role FROM team_members WHERE team_id = @teamId AND user_id = @userId",
            new { teamId, userId }, cancellationToken: ct));
        return role is null ? null : TeamRoleExtensions.FromDbValue(role);
    }

    public async Task<bool> DeleteAsync(string teamId, string actingUserId, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateSystemConnection();
        using var tx = db.BeginTransaction();

        var role = await db.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT role FROM team_members WHERE team_id = @teamId AND user_id = @actingUserId",
            new { teamId, actingUserId }, transaction: tx, cancellationToken: ct));

        if (role != TeamRole.Owner.ToDbValue()) return false;

        await db.ExecuteAsync(new CommandDefinition(
            "DELETE FROM team_members WHERE team_id = @teamId",
            new { teamId }, transaction: tx, cancellationToken: ct));

        var affected = await db.ExecuteAsync(new CommandDefinition(
            "DELETE FROM teams WHERE id = @teamId",
            new { teamId }, transaction: tx, cancellationToken: ct));

        tx.Commit();
        _logger.LogInformation("Deleted team {TeamId} by owner {UserId}", teamId, actingUserId);
        return affected > 0;
    }
}
