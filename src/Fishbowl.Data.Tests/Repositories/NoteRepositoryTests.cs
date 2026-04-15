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
        var id = await _repo.CreateAsync(TestUserId, note);

        // Assert
        Assert.NotNull(id);
        Assert.Equal(id, note.Id);
        Assert.Equal(TestUserId, note.CreatedBy);
        
        var retrieved = await _repo.GetByIdAsync(TestUserId, id);
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
        var id = await _repo.CreateAsync(TestUserId, note);

        // Act
        note.Title = "Updated";
        note.Tags.Add("new-tag");
        var success = await _repo.UpdateAsync(TestUserId, note);

        // Assert
        Assert.True(success);
        var retrieved = await _repo.GetByIdAsync(TestUserId, id);
        Assert.Equal("Updated", retrieved!.Title);
        Assert.Contains("new-tag", retrieved.Tags);
    }

    [Fact]
    public async Task Delete_RemovesFromDb_Test()
    {
        // Arrange
        var note = new Note { Title = "To Delete" };
        var id = await _repo.CreateAsync(TestUserId, note);

        // Act
        var success = await _repo.DeleteAsync(TestUserId, id);

        // Assert
        Assert.True(success);
        var retrieved = await _repo.GetByIdAsync(TestUserId, id);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetAll_ReturnsAllNotesForUser_Test()
    {
        // Arrange
        await _repo.CreateAsync(TestUserId, new Note { Title = "Note 1" });
        await _repo.CreateAsync(TestUserId, new Note { Title = "Note 2" });
        await _repo.CreateAsync("other_user", new Note { Title = "Other Note" });

        // Act
        var notes = await _repo.GetAllAsync(TestUserId);

        // Assert
        Assert.Equal(2, notes.Count());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDbDir))
        {
            Directory.Delete(_tempDbDir, true);
        }
    }
}
