using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Fishbowl.Core.Models;
using Fishbowl.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fishbowl.Host.Tests;

public class TagsApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _testDataDir;
    private const string UserA = "tags_user_a";
    private const string UserB = "tags_user_b";

    public TagsApiTests(WebApplicationFactory<Program> factory)
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), "fishbowl_tags_api_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_testDataDir);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DatabaseFactory));
                if (dbDescriptor != null) services.Remove(dbDescriptor);
                services.AddSingleton<DatabaseFactory>(new DatabaseFactory(_testDataDir));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultScheme = TestAuthHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.AuthenticationScheme, _ => { });
            });
        });
    }

    private HttpRequestMessage Req(HttpMethod method, string path, string user, object? body = null)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add(TestAuthHandler.UserIdHeader, user);
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    private async Task CreateNoteAsync(HttpClient client, string user, string title, params string[] tags)
    {
        var note = new Note { Title = title, Tags = tags.ToList() };
        var resp = await client.SendAsync(Req(HttpMethod.Post, "/api/v1/notes", user, note),
            TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CreatingNote_AutoRegistersUnknownTags_WithDeterministicColors()
    {
        var client = _factory.CreateClient();
        await CreateNoteAsync(client, UserA, "n1", "work", "Urgent");

        var listResp = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/tags", UserA),
            TestContext.Current.CancellationToken);
        listResp.EnsureSuccessStatusCode();
        var allTags = await listResp.Content.ReadFromJsonAsync<List<Tag>>(TestContext.Current.CancellationToken);

        Assert.NotNull(allTags);
        // Filter out system-seeded tags; this test is about user-tag auto-registration.
        var userTags = allTags!.Where(t => !t.IsSystem).ToList();
        Assert.Equal(2, userTags.Count);
        // names should be normalized
        Assert.Contains(userTags, t => t.Name == "work");
        Assert.Contains(userTags, t => t.Name == "urgent");
        Assert.All(userTags, t => Assert.Equal(Fishbowl.Core.Util.TagPalette.DefaultFor(t.Name), t.Color));
        Assert.All(userTags, t => Assert.Equal(1, t.UsageCount));
    }

    [Fact]
    public async Task UpsertColor_CreatesThenUpdates()
    {
        var client = _factory.CreateClient();

        var resp1 = await client.SendAsync(
            Req(HttpMethod.Put, "/api/v1/tags/manual", UserA, new { color = "blue" }),
            TestContext.Current.CancellationToken);
        resp1.EnsureSuccessStatusCode();
        var t1 = await resp1.Content.ReadFromJsonAsync<Tag>(TestContext.Current.CancellationToken);
        Assert.Equal("manual", t1!.Name);
        Assert.Equal("blue", t1.Color);

        var resp2 = await client.SendAsync(
            Req(HttpMethod.Put, "/api/v1/tags/manual", UserA, new { color = "green" }),
            TestContext.Current.CancellationToken);
        resp2.EnsureSuccessStatusCode();
        var t2 = await resp2.Content.ReadFromJsonAsync<Tag>(TestContext.Current.CancellationToken);
        Assert.Equal("green", t2!.Color);
    }

    [Fact]
    public async Task UpsertColor_RejectsUnknownPaletteSlot()
    {
        var client = _factory.CreateClient();
        var resp = await client.SendAsync(
            Req(HttpMethod.Put, "/api/v1/tags/whatever", UserA, new { color = "chartreuse" }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task FilterNotes_AnyMatch_ReturnsUnion()
    {
        var client = _factory.CreateClient();
        await CreateNoteAsync(client, UserA, "alpha", "work");
        await CreateNoteAsync(client, UserA, "beta", "personal");
        await CreateNoteAsync(client, UserA, "gamma", "work", "personal");

        var resp = await client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/notes?tag=work&tag=personal", UserA),
            TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
        var notes = await resp.Content.ReadFromJsonAsync<List<Note>>(TestContext.Current.CancellationToken);
        Assert.Equal(3, notes!.Count);
    }

    [Fact]
    public async Task FilterNotes_AllMatch_ReturnsIntersection()
    {
        var client = _factory.CreateClient();
        await CreateNoteAsync(client, UserA, "alpha", "work");
        await CreateNoteAsync(client, UserA, "beta", "personal");
        await CreateNoteAsync(client, UserA, "gamma", "work", "personal");

        var resp = await client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/notes?tag=work&tag=personal&match=all", UserA),
            TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
        var notes = await resp.Content.ReadFromJsonAsync<List<Note>>(TestContext.Current.CancellationToken);
        Assert.Single(notes!);
        Assert.Equal("gamma", notes![0].Title);
    }

    [Fact]
    public async Task DeleteTag_StripsItFromEveryNote()
    {
        var client = _factory.CreateClient();
        await CreateNoteAsync(client, UserA, "n1", "keep", "drop");
        await CreateNoteAsync(client, UserA, "n2", "drop");

        var del = await client.SendAsync(Req(HttpMethod.Delete, "/api/v1/tags/drop", UserA),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var notesResp = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/notes", UserA),
            TestContext.Current.CancellationToken);
        var notes = await notesResp.Content.ReadFromJsonAsync<List<Note>>(TestContext.Current.CancellationToken);
        Assert.All(notes!, n => Assert.DoesNotContain("drop", n.Tags));

        // FTS should also no longer have the deleted tag — proxy: a filter on
        // the dropped name now returns nothing.
        var afterFilter = await client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/notes?tag=drop", UserA),
            TestContext.Current.CancellationToken);
        var filtered = await afterFilter.Content.ReadFromJsonAsync<List<Note>>(TestContext.Current.CancellationToken);
        Assert.Empty(filtered!);
    }

    [Fact]
    public async Task RenameTag_RewritesEveryNote()
    {
        var client = _factory.CreateClient();
        await CreateNoteAsync(client, UserA, "n1", "old", "shared");
        await CreateNoteAsync(client, UserA, "n2", "old");

        var rename = await client.SendAsync(
            Req(HttpMethod.Post, "/api/v1/tags/old/rename", UserA, new { newName = "new" }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, rename.StatusCode);

        var notesResp = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/notes", UserA),
            TestContext.Current.CancellationToken);
        var notes = await notesResp.Content.ReadFromJsonAsync<List<Note>>(TestContext.Current.CancellationToken);
        Assert.All(notes!, n => Assert.DoesNotContain("old", n.Tags));
        Assert.Equal(2, notes!.Count(n => n.Tags.Contains("new")));

        var listResp = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/tags", UserA),
            TestContext.Current.CancellationToken);
        var tags = await listResp.Content.ReadFromJsonAsync<List<Tag>>(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(tags!, t => t.Name == "old");
        Assert.Contains(tags!, t => t.Name == "new");
    }

    [Fact]
    public async Task TagIsolation_BetweenUsers()
    {
        var client = _factory.CreateClient();
        await CreateNoteAsync(client, UserA, "a-note", "alpha");
        await CreateNoteAsync(client, UserB, "b-note", "beta");

        var aResp = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/tags", UserA),
            TestContext.Current.CancellationToken);
        var aAll = await aResp.Content.ReadFromJsonAsync<List<Tag>>(TestContext.Current.CancellationToken);
        var aUserTags = aAll!.Where(t => !t.IsSystem).ToList();
        Assert.Single(aUserTags);
        Assert.Equal("alpha", aUserTags[0].Name);

        var bResp = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/tags", UserB),
            TestContext.Current.CancellationToken);
        var bAll = await bResp.Content.ReadFromJsonAsync<List<Tag>>(TestContext.Current.CancellationToken);
        var bUserTags = bAll!.Where(t => !t.IsSystem).ToList();
        Assert.Single(bUserTags);
        Assert.Equal("beta", bUserTags[0].Name);
    }

    [Fact]
    public async Task TagsApi_RequiresAuth()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/tags", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
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
