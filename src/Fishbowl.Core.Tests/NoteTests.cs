using Xunit;
using Fishbowl.Core.Models;

namespace Fishbowl.Core.Tests;

public class NoteTests
{
    [Fact]
    public void Note_Initialization_SetsDefaultValues_Test()
    {
        // Arrange & Act
        var note = new Note
        {
            Title = "Test Note",
            CreatedBy = "user1"
        };

        // Assert
        Assert.Equal("Test Note", note.Title);
        Assert.Equal("user1", note.CreatedBy);
        Assert.False(note.Pinned);
        Assert.False(note.Archived);
        Assert.Equal("note", note.Type);
    }
}
