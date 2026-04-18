using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Fishbowl.Host.Tests;

public class AuthBehaviorTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthBehaviorTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
        });
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
        var response = await client.GetAsync("/api/notes", TestContext.Current.CancellationToken);

        // Assert
        // This is the CRITICAL fix for the CORS issue: 
        // Unauthenticated API calls must return 401, not a 302 redirect to Google.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Location"), "API should not redirect to login page.");
    }

    [Fact]
    public async Task GetLogin_ReturnsOk_Page_Test()
    {
        // Arrange
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
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/login/challenge/google", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.Contains("accounts.google.com", location);
        Assert.Contains("client_id=1049281787342", location); // Check for start of our seeded dev client id
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
