using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Fishbowl.Core;
using Fishbowl.Data;
using Fishbowl.Core.Repositories;
using Fishbowl.Data.Repositories;
using Fishbowl.Api.Endpoints;
using Microsoft.Extensions.Caching.Memory;
using Fishbowl.Host;

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

// Authentication Configuration
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    // We set challenge to Cookies so that unauthenticated requests hit our OnRedirectToLogin logic first.
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options => 
{
    options.LoginPath = "/login";
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }
        return Task.CompletedTask;
    };
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "placeholder-client-id";
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "placeholder-client-secret";
    options.SaveTokens = true;
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Enforce SSL
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Branding Output
if (!app.Environment.IsEnvironment("Testing"))
{
    StartupBranding.PrintBanner();
}

// Register Auth Endpoints
app.MapGet("/login", (string? returnUrl, HttpContext context) => 
{
    return Results.Challenge(
        properties: new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
        authenticationSchemes: new[] { GoogleDefaults.AuthenticationScheme });
});

app.MapGet("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

// Register API Endpoints
app.MapNotesApi();
app.MapTodoApi();

// Fallback to serve Web UI from ResourceProvider
// We use a catch-all pattern '{*path}' to ensure assets with extensions (css, js) are captured.
app.MapFallback("{*path}", async (HttpContext context, IResourceProvider resources) =>
{
    var path = context.Request.Path.Value?.TrimStart('/') ?? "index.html";
    if (string.IsNullOrEmpty(path)) path = "index.html";

    // Try to find the resource
    var resource = await resources.GetAsync(path);
    
    // Fallback logic for client-side routing and directories
    if (resource == null)
    {
        // If it's a request for a file that isn't found, return 404
        if (path.Contains('.')) return Results.NotFound();

        // Otherwise, it might be a client-side route, serves index.html
        path = "index.html";
        resource = await resources.GetAsync(path);
        if (resource == null) return Results.NotFound();
    }

    var contentType = GetContentType(path);
    return Results.Bytes(resource.Data, contentType);
});

// Helper for MIME types
static string GetContentType(string path)
{
    var ext = Path.GetExtension(path).ToLowerInvariant();
    return ext switch
    {
        ".html" => "text/html",
        ".css"  => "text/css",
        ".js"   => "application/javascript",
        ".json" => "application/json",
        ".png"  => "image/png",
        ".jpg"  => "image/jpeg",
        ".ico"  => "image/x-icon",
        ".svg"  => "image/svg+xml",
        ".woff" => "font/woff",
        ".woff2"=> "font/woff2",
        _       => "application/octet-stream"
    };
}

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
