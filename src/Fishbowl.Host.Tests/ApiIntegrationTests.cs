using System.Net.Http.Json;
using Fishbowl.Core.Models;
using Fishbowl.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fishbowl.Host.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string UserA = "user_a";
    private const string UserB = "user_b";

    private readonly string _testDataDir;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), "fishbowl_api_integration_tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_testDataDir);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the production DatabaseFactory
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DatabaseFactory));
                if (descriptor != null) services.Remove(descriptor);

                // Add a test-specific one
                services.AddSingleton<DatabaseFactory>(new DatabaseFactory(_testDataDir));
            });
        });
    }

    [Fact]
    public async Task PostAndGet_IsolatesDataByUser_Test()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        var noteA = new Note { Title = "User A Note" };
        var noteB = new Note { Title = "User B Note" };

        // Act 1: Post as User A
        var requestA = new HttpRequestMessage(HttpMethod.Post, "/api/notes");
        requestA.Headers.Add("X-Fishbowl-User-Id", UserA);
        requestA.Content = JsonContent.Create(noteA);
        var responseA = await client.SendAsync(requestA);
        Assert.True(responseA.IsSuccessStatusCode);

        // Act 2: Post as User B
        var requestB = new HttpRequestMessage(HttpMethod.Post, "/api/notes");
        requestB.Headers.Add("X-Fishbowl-User-Id", UserB);
        requestB.Content = JsonContent.Create(noteB);
        var responseB = await client.SendAsync(requestB);
        Assert.True(responseB.IsSuccessStatusCode);

        // Act 3: Get notes for User A
        var getRequestA = new HttpRequestMessage(HttpMethod.Get, "/api/notes");
        getRequestA.Headers.Add("X-Fishbowl-User-Id", UserA);
        var getResponseA = await client.SendAsync(getRequestA);
        var notesA = await getResponseA.Content.ReadFromJsonAsync<IEnumerable<Note>>();

        // Act 4: Get notes for User B
        var getRequestB = new HttpRequestMessage(HttpMethod.Get, "/api/notes");
        getRequestB.Headers.Add("X-Fishbowl-User-Id", UserB);
        var getResponseB = await client.SendAsync(getRequestB);
        var notesB = await getResponseB.Content.ReadFromJsonAsync<IEnumerable<Note>>();

        // Assert
        Assert.Single(notesA!);
        Assert.Equal("User A Note", notesA!.First().Title);
        
        Assert.Single(notesB!);
        Assert.Equal("User B Note", notesB!.First().Title);
    }

    [Fact]
    public async Task Get_ReturnsBadRequest_IfHeaderMissing_Test()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/notes");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_testDataDir))
        {
            try { Directory.Delete(_testDataDir, true); } catch { }
        }
    }
}
