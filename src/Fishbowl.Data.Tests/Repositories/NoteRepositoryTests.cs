using Xunit;
using Fishbowl.Data;
using Fishbowl.Core.Models;
using Fishbowl.Data.Repositories;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Threading.Tasks;

namespace Fishbowl.Tests.Repositories;

public class NoteRepositoryTests : IDisposable
{
    private readonly string _tempDbDir;
    private readonly DatabaseFactory _dbFactory;
    private readonly NoteRepository _repo;
    private const string TestUserId = "user_repo_test";

    public NoteRepositoryTests()
    {
        _tempDbDir = Path.Combine(Path.GetTempPath(), "fishbowl_repo_tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDbDir);
        _dbFactory = new DatabaseFactory(_tempDbDir);
        _repo = new NoteRepository(_dbFactory);
    }

    [Fact]
    public async Task Create_SetsMetadataAndStores_Test()
    {
        // Arrange
        var note = new Note
        {
            Title = "Test Note",
            Content = "Hello World",
            Tags = new List<string> { "tag1", "tag2" }
        };

        // Act
        var id = await _repo.CreateAsync(TestUserId, note, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(id);
        Assert.Equal(id, note.Id);
        Assert.Equal(TestUserId, note.CreatedBy);
        
        var retrieved = await _repo.GetByIdAsync(TestUserId, id, TestContext.Current.CancellationToken);
        Assert.NotNull(retrieved);
        Assert.Equal("Test Note", retrieved.Title);
        Assert.Equal(2, retrieved.Tags.Count);
        Assert.Contains("tag1", retrieved.Tags);
    }

    [Fact]
    public async Task Update_ModifiesData_Test()
    {
        // Arrange
        var note = new Note { Title = "Original", Content = "Old" };
        var id = await _repo.CreateAsync(TestUserId, note, TestContext.Current.CancellationToken);

        // Act
        note.Title = "Updated";
        note.Tags.Add("new-tag");
        var success = await _repo.UpdateAsync(TestUserId, note, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(success);
        var retrieved = await _repo.GetByIdAsync(TestUserId, id, TestContext.Current.CancellationToken);
        Assert.Equal("Updated", retrieved!.Title);
        Assert.Contains("new-tag", retrieved.Tags);
    }

    [Fact]
    public async Task Delete_RemovesFromDb_Test()
    {
        // Arrange
        var note = new Note { Title = "To Delete" };
        var id = await _repo.CreateAsync(TestUserId, note, TestContext.Current.CancellationToken);

        // Act
        var success = await _repo.DeleteAsync(TestUserId, id, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(success);
        var retrieved = await _repo.GetByIdAsync(TestUserId, id, TestContext.Current.CancellationToken);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetAll_ReturnsAllNotesForUser_Test()
    {
        // Arrange
        await _repo.CreateAsync(TestUserId, new Note { Title = "Note 1" }, TestContext.Current.CancellationToken);
        await _repo.CreateAsync(TestUserId, new Note { Title = "Note 2" }, TestContext.Current.CancellationToken);
        await _repo.CreateAsync("other_user", new Note { Title = "Other Note" }, TestContext.Current.CancellationToken);

        // Act
        var notes = await _repo.GetAllAsync(TestUserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, notes.Count());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDbDir))
        {
            try { Directory.Delete(_tempDbDir, true); } catch { }
        }
    }
}
