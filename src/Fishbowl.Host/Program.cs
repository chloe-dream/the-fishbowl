using Fishbowl.Core;
using Fishbowl.Data;

var builder = WebApplication.CreateBuilder(args);

// Register Core Services
builder.Services.AddSingleton<IResourceProvider, ResourceProvider>(sp => 
    new ResourceProvider(modsPath: "fishbowl-mods", embeddedAssembly: typeof(ResourceProvider).Assembly));

builder.Services.AddSingleton<DatabaseFactory>(new DatabaseFactory("fishbowl-data/users"));

var app = builder.Build();

app.MapGet("/", () => "The Fishbowl is running.");

// Test endpoint for ResourceProvider
app.MapGet("/test/resource/{*path}", async (string path, IResourceProvider resources) =>
{
    var resource = await resources.GetAsync(path);
    if (resource == null) return Results.NotFound($"Resource '{path}' not found.");
    
    using var reader = new StreamReader(resource.Content);
    var content = await reader.ReadToEndAsync();
    
    return Results.Ok(new 
    { 
        Path = path, 
        Source = resource.Source.ToString(), 
        ContentSnippet = content.Length > 100 ? content[..100] + "..." : content 
    });
});

// Test endpoint for Database
app.MapGet("/test/db/{userId}", (string userId, DatabaseFactory dbFactory) =>
{
    try 
    {
        using var connection = dbFactory.CreateConnection(userId);
        return Results.Ok($"Successfully connected to and initialized database for user: {userId}");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to initialize database: {ex.Message}");
    }
});

app.Run();
