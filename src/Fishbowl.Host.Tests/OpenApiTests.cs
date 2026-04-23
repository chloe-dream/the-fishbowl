using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Fishbowl.Host.Tests;

public class OpenApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task OpenApi_DocumentAvailable_Test()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/openapi.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("/api/v1/notes", body);
        Assert.Contains("/api/v1/todos", body);
    }

    [Fact]
    public async Task OpenApi_IncludesContactsEndpoint_Test()
    {
        // Catches accidental un-registration: if MapContactsApi() is dropped
        // from Program.cs, the OpenAPI doc silently loses the path — this
        // assertion flags that before it ships.
        var client = _factory.CreateClient();
        var body = await client.GetStringAsync("/api/openapi.json",
            TestContext.Current.CancellationToken);

        Assert.Contains("/api/v1/contacts", body);
        Assert.Contains("\"ListContacts\"", body);
        Assert.Contains("\"CreateContact\"", body);
        Assert.Contains("\"DeleteContact\"", body);
        Assert.Contains("\"SearchContacts\"", body);
    }

    [Fact]
    public async Task OpenApi_IncludesExportEndpoint_Test()
    {
        var client = _factory.CreateClient();
        var body = await client.GetStringAsync("/api/openapi.json",
            TestContext.Current.CancellationToken);

        Assert.Contains("/api/v1/export/db", body);
        Assert.Contains("\"ExportUserDatabase\"", body);
    }

    [Fact]
    public async Task OpenApi_IncludesNotesSearchEndpoint_Test()
    {
        var client = _factory.CreateClient();
        var body = await client.GetStringAsync("/api/openapi.json",
            TestContext.Current.CancellationToken);

        Assert.Contains("\"SearchNotes\"", body);
    }

    [Fact]
    public async Task OpenApi_IncludesEventsEndpoint_Test()
    {
        var client = _factory.CreateClient();
        var body = await client.GetStringAsync("/api/openapi.json",
            TestContext.Current.CancellationToken);

        Assert.Contains("/api/v1/events", body);
        Assert.Contains("\"ListEvents\"", body);
        Assert.Contains("\"CreateEvent\"", body);
        Assert.Contains("\"DeleteEvent\"", body);
    }
}
