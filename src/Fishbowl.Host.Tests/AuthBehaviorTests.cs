using System.Net;
using Fishbowl.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fishbowl.Host.Tests;

public class AuthBehaviorTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _testDataDir;

    public AuthBehaviorTests(WebApplicationFactory<Program> factory)
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), "fishbowl_auth_tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_testDataDir);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Replace DatabaseFactory with test instance
                var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DatabaseFactory));
                if (dbDescriptor != null) services.Remove(dbDescriptor);
                services.AddSingleton<DatabaseFactory>(new DatabaseFactory(_testDataDir));
            });
        });
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_testDataDir))
        {
            try { Directory.Delete(_testDataDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GetApiNotes_Returns401_NotRedirect_Test()
    {
        // Arrange
        // We MUST disable auto-redirect to see the 401/302 status
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/api/v1/notes", TestContext.Current.CancellationToken);

        // Assert
        // This is the CRITICAL fix for the CORS issue: 
        // Unauthenticated API calls must return 401, not a 302 redirect to Google.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Location"), "API should not redirect to login page.");
    }

    [Fact]
    public async Task GetLogin_RedirectsToSetup_WhenUnconfigured_Test()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/login", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/setup", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task GetLogin_ReturnsOk_Page_Test()
    {
        // Arrange — seed Google config so /login serves the login page
        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<Fishbowl.Core.Repositories.ISystemRepository>();
            await repo.SetConfigAsync("Google:ClientId", "test-client-id.apps.googleusercontent.com", TestContext.Current.CancellationToken);
            await repo.SetConfigAsync("Google:ClientSecret", "test-secret-value", TestContext.Current.CancellationToken);
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/login", TestContext.Current.CancellationToken);

        // Assert
        // The landing page should return 200 OK as it is now an HTML page
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("The Fishbowl", content);
    }

    [Fact]
    public async Task GetLoginChallenge_RedirectsToGoogle_Test()
    {
        // Arrange — seed Google config into system.db via the test host
        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<Fishbowl.Core.Repositories.ISystemRepository>();
            await repo.SetConfigAsync("Google:ClientId", "seeded-test.apps.googleusercontent.com", TestContext.Current.CancellationToken);
            await repo.SetConfigAsync("Google:ClientSecret", "seeded-test-secret-value-long-enough", TestContext.Current.CancellationToken);
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/login/challenge/google", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.Contains("accounts.google.com", location);
        Assert.Contains("client_id=seeded-test", location);
    }

    [Fact]
    public async Task GetLogout_RedirectsHome_Test()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/logout", TestContext.Current.CancellationToken);

        // Assert
        // Proves that /logout clears the session and returns user to root.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.ToString());
    }
}
