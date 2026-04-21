using Dapper;
using Fishbowl.Core.Models;
using Fishbowl.Core.Repositories;
using Fishbowl.Data;
using Fishbowl.Data.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Fishbowl.Data.Tests.Repositories;

public class TeamRepositoryTests : IDisposable
{
    private readonly string _dataDir;
    private readonly DatabaseFactory _factory;
    private readonly TeamRepository _repo;
    private const string OwnerId = "u_owner";
    private const string OtherId = "u_other";

    public TeamRepositoryTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "fishbowl_teamrepo_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_dataDir);
        _factory = new DatabaseFactory(_dataDir);
        _repo = new TeamRepository(_factory);

        using var db = _factory.CreateSystemConnection();
        var now = DateTime.UtcNow.ToString("o");
        db.Execute(
            "INSERT OR IGNORE INTO users(id, name, email, created_at) VALUES (@id, @n, @e, @now)",
            new[]
            {
                new { id = OwnerId, n = "Owner", e = "o@o", now },
                new { id = OtherId, n = "Other", e = "x@x", now },
            });
    }

    [Fact]
    public async Task CreateAsync_GeneratesSlugAndAddsOwnerMembership()
    {
        var team = await _repo.CreateAsync(OwnerId, "Fishbowl Dev",
            TestContext.Current.CancellationToken);

        Assert.Equal("fishbowl-dev", team.Slug);
        Assert.Equal("Fishbowl Dev", team.Name);
        Assert.Equal(OwnerId, team.CreatedBy);
        Assert.NotEqual(string.Empty, team.Id);

        var role = await _repo.GetMembershipAsync(team.Id, OwnerId,
            TestContext.Current.CancellationToken);
        Assert.Equal(TeamRole.Owner, role);
    }

    [Fact]
    public async Task CreateAsync_SlugCollision_AppendsSuffix()
    {
        await _repo.CreateAsync(OwnerId, "Backlog", TestContext.Current.CancellationToken);
        var t2 = await _repo.CreateAsync(OwnerId, "Backlog",
            TestContext.Current.CancellationToken);
        Assert.Equal("backlog-2", t2.Slug);
    }

    [Fact]
    public async Task CreateAsync_EmptyName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _repo.CreateAsync(OwnerId, "   ", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListByMemberAsync_ReturnsOnlyMyTeams()
    {
        var mine = await _repo.CreateAsync(OwnerId, "Mine",
            TestContext.Current.CancellationToken);
        _ = await _repo.CreateAsync(OtherId, "Theirs",
            TestContext.Current.CancellationToken);

        var teams = await _repo.ListByMemberAsync(OwnerId,
            TestContext.Current.CancellationToken);
        Assert.Single(teams);
        Assert.Equal(mine.Id, teams[0].Team.Id);
        Assert.Equal(TeamRole.Owner, teams[0].Role);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNullForUnknown()
    {
        var t = await _repo.GetBySlugAsync("does-not-exist",
            TestContext.Current.CancellationToken);
        Assert.Null(t);
    }

    [Fact]
    public async Task GetMembershipAsync_NonMember_ReturnsNull()
    {
        var team = await _repo.CreateAsync(OwnerId, "Private",
            TestContext.Current.CancellationToken);
        var role = await _repo.GetMembershipAsync(team.Id, OtherId,
            TestContext.Current.CancellationToken);
        Assert.Null(role);
    }

    [Fact]
    public async Task DeleteAsync_Owner_Succeeds()
    {
        var team = await _repo.CreateAsync(OwnerId, "Doomed",
            TestContext.Current.CancellationToken);
        var ok = await _repo.DeleteAsync(team.Id, OwnerId,
            TestContext.Current.CancellationToken);
        Assert.True(ok);

        var refetched = await _repo.GetByIdAsync(team.Id,
            TestContext.Current.CancellationToken);
        Assert.Null(refetched);
    }

    [Fact]
    public async Task DeleteAsync_NonOwner_ReturnsFalse()
    {
        var team = await _repo.CreateAsync(OwnerId, "Locked",
            TestContext.Current.CancellationToken);
        var ok = await _repo.DeleteAsync(team.Id, OtherId,
            TestContext.Current.CancellationToken);
        Assert.False(ok);

        var refetched = await _repo.GetByIdAsync(team.Id,
            TestContext.Current.CancellationToken);
        Assert.NotNull(refetched);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dataDir))
        {
            try { Directory.Delete(_dataDir, true); } catch { }
        }
    }
}
