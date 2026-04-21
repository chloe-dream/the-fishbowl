using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using Fishbowl.Core;
using Fishbowl.Data;
using Fishbowl.Data.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fishbowl.Host.Tests;

// Integration tests for the MCP Streamable HTTP endpoint. Lives in
// Fishbowl.Host.Tests (not Fishbowl.Mcp.Tests) because it needs the full
// WebApplicationFactory<Program> wiring — pure-unit tests for the dispatcher
// can move into a dedicated Mcp.Tests project later if that pays off.
public class McpEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DatabaseFactory _dbFactory;
    private readonly ApiKeyRepository _keys;
    private readonly string _dataDir;
    private const string AliceId = "mcp_alice";

    public McpEndpointTests(WebApplicationFactory<Program> factory)
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "fishbowl_mcp_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_dataDir);

        _dbFactory = new DatabaseFactory(_dataDir);
        _keys = new ApiKeyRepository(_dbFactory);

        using (var db = _dbFactory.CreateSystemConnection())
        {
            db.Execute(
                "INSERT OR IGNORE INTO users(id, name, email, created_at) VALUES (@id, 'A', 'a@a', @now)",
                new { id = AliceId, now = DateTime.UtcNow.ToString("o") });
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

    private async Task<HttpClient> BearerClientAsync(params string[] scopes)
    {
        var issued = await _keys.IssueAsync(AliceId, ContextRef.User(AliceId),
            "mcp-test",
            scopes.Length == 0 ? new[] { "read:notes" } : scopes,
            TestContext.Current.CancellationToken);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", issued.RawToken);
        return client;
    }

    private static StringContent JsonRpc(object body)
        => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    [Fact]
    public async Task Mcp_NoAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/mcp",
            JsonRpc(new { jsonrpc = "2.0", id = 1, method = "initialize" }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Mcp_InvalidBearer_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "fb_live_bogus_token_xxx");
        var resp = await client.PostAsync("/mcp",
            JsonRpc(new { jsonrpc = "2.0", id = 1, method = "initialize" }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Mcp_Initialize_ReturnsServerInfo()
    {
        var client = await BearerClientAsync();
        var resp = await client.PostAsync("/mcp",
            JsonRpc(new { jsonrpc = "2.0", id = 1, method = "initialize" }),
            TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(
            await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = doc.RootElement;

        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(1, root.GetProperty("id").GetInt32());
        var result = root.GetProperty("result");
        Assert.Equal("2025-03-26", result.GetProperty("protocolVersion").GetString());
        Assert.Equal("fishbowl", result.GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.True(result.TryGetProperty("capabilities", out var caps));
        Assert.True(caps.TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task Mcp_ToolsList_ReturnsEmptyArray_ForNow()
    {
        // Task 4.3 populates this — the test proves the dispatcher wiring.
        var client = await BearerClientAsync();
        var resp = await client.PostAsync("/mcp",
            JsonRpc(new { jsonrpc = "2.0", id = 2, method = "tools/list" }),
            TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(
            await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var tools = doc.RootElement.GetProperty("result").GetProperty("tools");
        Assert.Equal(JsonValueKind.Array, tools.ValueKind);
        Assert.Equal(0, tools.GetArrayLength());
    }

    [Fact]
    public async Task Mcp_Notification_Returns202_WithEmptyBody()
    {
        // JSON-RPC notifications have no `id`. Server must not respond with
        // a body — Streamable HTTP recommends 202.
        var client = await BearerClientAsync();
        var resp = await client.PostAsync("/mcp",
            JsonRpc(new { jsonrpc = "2.0", method = "notifications/initialized" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(string.IsNullOrEmpty(body), $"expected empty body, got: {body}");
    }

    [Fact]
    public async Task Mcp_UnknownMethod_ReturnsMethodNotFound()
    {
        var client = await BearerClientAsync();
        var resp = await client.PostAsync("/mcp",
            JsonRpc(new { jsonrpc = "2.0", id = 3, method = "totally/unknown" }),
            TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(
            await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = doc.RootElement;
        Assert.False(root.TryGetProperty("result", out _));
        var err = root.GetProperty("error");
        Assert.Equal(-32601, err.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Mcp_MalformedJson_ReturnsParseError()
    {
        var client = await BearerClientAsync();
        var resp = await client.PostAsync("/mcp",
            new StringContent("{not valid json", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        // Even with a parse failure the server responds 200 with a JSON-RPC
        // error envelope — the HTTP layer is fine, the payload is not.
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(
            await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var err = doc.RootElement.GetProperty("error");
        Assert.Equal(-32700, err.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Mcp_StringId_IsEchoedBackExactly()
    {
        // MCP clients use string ids too; we must not mangle them.
        var client = await BearerClientAsync();
        var resp = await client.PostAsync("/mcp",
            JsonRpc(new { jsonrpc = "2.0", id = "abc-42", method = "initialize" }),
            TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(
            await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("abc-42", doc.RootElement.GetProperty("id").GetString());
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
}
