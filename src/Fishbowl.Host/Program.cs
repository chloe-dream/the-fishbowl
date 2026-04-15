using Fishbowl.Core;
using Fishbowl.Data;
using Fishbowl.Core.Repositories;
using Fishbowl.Data.Repositories;
using Fishbowl.Api.Endpoints;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// Register Core Services
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IResourceProvider, ResourceProvider>(sp => 
    new ResourceProvider(
        cache: sp.GetRequiredService<IMemoryCache>(),
        modsPath: "fishbowl-mods", 
        embeddedAssembly: typeof(ResourceProvider).Assembly));

builder.Services.AddSingleton<DatabaseFactory>(new DatabaseFactory("fishbowl-data/users"));

// Register Repositories
builder.Services.AddScoped<INoteRepository, NoteRepository>();
builder.Services.AddScoped<ITodoRepository, TodoRepository>();

var app = builder.Build();

app.MapGet("/", () => "The Fishbowl is running.");

// Register API Endpoints
app.MapNotesApi();
app.MapTodoApi();

// Test endpoint for ResourceProvider
app.MapGet("/test/resource/{*path}", async (string path, IResourceProvider resources) =>
{
    var resource = await resources.GetAsync(path);
    if (resource == null) return Results.NotFound($"Resource '{path}' not found.");
    
    // We can directly decode byte[] to string for simple text resources (like templates/scripts)
    var content = System.Text.Encoding.UTF8.GetString(resource.Data);
    
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

public partial class Program { }
