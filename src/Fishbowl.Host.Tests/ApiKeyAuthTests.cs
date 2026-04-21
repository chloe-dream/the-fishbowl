using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dapper;
using Fishbowl.Core;
using Fishbowl.Core.Repositories;
using Fishbowl.Data;
using Fishbowl.Data.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fishbowl.Host.Tests;

public class ApiKeyAuthTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DatabaseFactory _dbFactory;
    private readonly ApiKeyRepository _keys;
    private readonly string _dataDir;
    private const string AliceId = "apikey_alice";
    private const string BobId = "apikey_bob";

    public ApiKeyAuthTests(WebApplicationFactory<Program> factory)
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "fishbowl_apikey_auth_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_dataDir);

        _dbFactory = new DatabaseFactory(_dataDir);
        _keys = new ApiKeyRepository(_dbFactory);

        // FK: api_keys.user_id references users(id). TestAuthHandler isn't in
        // play here — seed the user row directly. Also seed the profile so
        // /api/v1/me works for endpoints that consume the user record.
        using (var db = _dbFactory.CreateSystemConnection())
        {
            var now = DateTime.UtcNow.ToString("o");
            db.Execute(
                "INSERT OR IGNORE INTO users(id, name, email, created_at) VALUES (@id, @n, @e, @now)",
                new[]
                {
                    new { id = AliceId, n = "Alice", e = "a@a", now },
                    new { id = BobId,   n = "Bob",   e = "b@b", now },
                });
        }

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var existing = services.SingleOrDefault(d => d.ServiceType == typeof(DatabaseFactory));
                if (existing != null) services.Remove(existing);
                services.AddSingleton<DatabaseFactory>(_dbFactory);
            });
        });
    }

    private HttpClient ClientWithToken(string? rawToken)
    {
        var client = _factory.CreateClient();
        if (rawToken is not null)
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", rawToken);
        return client;
    }

    [Fact]
    public async Task NoAuthorizationHeader_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/auth/whoami",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task InvalidBearerToken_Returns401()
    {
        var client = ClientWithToken("fb_live_this_is_not_a_real_token_xyz");
        var resp = await client.GetAsync("/api/v1/auth/whoami",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task NonFishbowlBearerToken_Returns401()
    {
        // Doesn't match our prefix — handler returns NoResult, cookie has nothing,
        // so the request falls through to 401.
        var client = ClientWithToken("not-ours-at-all");
        var resp = await client.GetAsync("/api/v1/auth/whoami",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task RevokedBearerToken_Returns401()
    {
        var issued = await _keys.IssueAsync(AliceId, ContextRef.User(AliceId), "to-be-revoked",
            new[] { "read:notes" }, TestContext.Current.CancellationToken);
        await _keys.RevokeAsync(issued.Record.Id, AliceId, TestContext.Current.CancellationToken);

        var client = ClientWithToken(issued.RawToken);
        var resp = await client.GetAsync("/api/v1/auth/whoami",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ValidTokenUserContext_PrincipalHasUserClaims()
    {
        var issued = await _keys.IssueAsync(AliceId, ContextRef.User(AliceId), "personal-key",
            new[] { "read:notes", "write:notes" }, TestContext.Current.CancellationToken);

        var client = ClientWithToken(issued.RawToken);
        var resp = await client.GetAsync("/api/v1/auth/whoami",
            TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<WhoAmIDto>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(payload);
        Assert.Equal(AliceId, payload!.UserId);
        Assert.Equal("user", payload.Context.Type);
        Assert.Equal(AliceId, payload.Context.Id);
        Assert.Contains("read:notes", payload.Scopes);
        Assert.Contains("write:notes", payload.Scopes);
        Assert.Equal("ApiKey", payload.AuthType);
    }

    [Fact]
    public async Task ValidTokenTeamContext_PrincipalHasTeamClaims()
    {
        var issued = await _keys.IssueAsync(AliceId, ContextRef.Team("fishbowl-dev"),
            "team-key", new[] { "read:notes" }, TestContext.Current.CancellationToken);

        var client = ClientWithToken(issued.RawToken);
        var resp = await client.GetAsync("/api/v1/auth/whoami",
            TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<WhoAmIDto>(
            TestContext.Current.CancellationToken);
        Assert.Equal("team", payload!.Context.Type);
        Assert.Equal("fishbowl-dev", payload.Context.Id);
        Assert.Equal(AliceId, payload.UserId);
        Assert.Single(payload.Scopes, "read:notes");
    }

    [Fact]
    public async Task ValidToken_UpdatesLastUsedAt()
    {
        var issued = await _keys.IssueAsync(BobId, ContextRef.User(BobId), "touched",
            new[] { "read:notes" }, TestContext.Current.CancellationToken);

        var client = ClientWithToken(issued.RawToken);
        var resp = await client.GetAsync("/api/v1/auth/whoami",
            TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();

        // Fire-and-forget — give it a tick to land before querying.
        await Task.Delay(200, TestContext.Current.CancellationToken);

        var after = (await _keys.ListByUserAsync(BobId, TestContext.Current.CancellationToken))
            .Single(k => k.Id == issued.Record.Id);
        Assert.NotNull(after.LastUsedAt);
    }

    public void Dispose()
    {
        _factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dataDir))
        {
            try { Directory.Delete(_dataDir, true); } catch { }
        }
    }

    private record ContextDto(string Type, string Id);
    private record WhoAmIDto(string UserId, ContextDto Context, List<string> Scopes, string? AuthType);
}
