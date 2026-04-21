using Fishbowl.Data;
using Fishbowl.Data.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Fishbowl.Data.Tests.Repositories;

public class TagSystemFlagTests : IDisposable
{
    private readonly string _dataDir;
    private readonly DatabaseFactory _factory;
    private readonly TagRepository _repo;
    private const string UserId = "guard-user";

    public TagSystemFlagTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "fishbowl_tag_guards_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_dataDir);
        _factory = new DatabaseFactory(_dataDir);
        _repo = new TagRepository(_factory);
    }

    [Fact]
    public async Task RenameAsync_OnSystemTag_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _repo.RenameAsync(UserId, "review:pending", "review:needs-human",
                TestContext.Current.CancellationToken));
        Assert.Contains("system tag", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_OnSystemTag_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _repo.DeleteAsync(UserId, "source:mcp", TestContext.Current.CancellationToken));
        Assert.Contains("system tag", ex.Message);
    }

    [Fact]
    public async Task UpsertColorAsync_OnSystemTag_Succeeds()
    {
        // Colour is presentation — a user may want to re-skin system tags in
        // their palette. Guard only fires on rename/delete.
        var updated = await _repo.UpsertColorAsync(UserId, "review:pending", "red",
            TestContext.Current.CancellationToken);
        Assert.Equal("red", updated.Color);
        Assert.True(updated.IsSystem);
    }

    [Fact]
    public async Task RenameAsync_OnUserTag_StillWorks()
    {
        await _repo.UpsertColorAsync(UserId, "ephemeral", "blue",
            TestContext.Current.CancellationToken);
        var renamed = await _repo.RenameAsync(UserId, "ephemeral", "permanent",
            TestContext.Current.CancellationToken);
        Assert.True(renamed);
    }

    [Fact]
    public async Task DeleteAsync_OnUserTag_StillWorks()
    {
        await _repo.UpsertColorAsync(UserId, "throwaway", "gray",
            TestContext.Current.CancellationToken);
        var deleted = await _repo.DeleteAsync(UserId, "throwaway",
            TestContext.Current.CancellationToken);
        Assert.True(deleted);
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
