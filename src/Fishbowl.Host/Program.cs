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
using Microsoft.Extensions.Options;
using Fishbowl.Host;

var builder = WebApplication.CreateBuilder(args);

// Register Core Services
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IResourceProvider, ResourceProvider>(sp => 
    new ResourceProvider(
        cache: sp.GetRequiredService<IMemoryCache>(),
        modsPath: "fishbowl-mods", 
        embeddedAssembly: typeof(ResourceProvider).Assembly));

// Consistent data root from CLI or default
var dataPath = builder.Configuration["data"] ?? "fishbowl-data";
builder.Services.AddSingleton<DatabaseFactory>(new DatabaseFactory(dataPath));

// Register Repositories
builder.Services.AddScoped<ISystemRepository, SystemRepository>();
builder.Services.AddScoped<INoteRepository, NoteRepository>();
builder.Services.AddScoped<ITodoRepository, TodoRepository>();

// Authentication Configuration
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
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
            // If we are in "Setup Mode" (no valid Google Config), 
            // we should probably redirect to /setup instead of /login (which challenges Google)
            // For now, /login handles it.
            context.Response.Redirect(context.RedirectUri);
        }
        return Task.CompletedTask;
    };
})
.AddGoogle(options => 
{
    // These will be overridden by OpenOptions below
    options.ClientId = "placeholder";
    options.ClientSecret = "placeholder";
    options.SaveTokens = true;

    options.Events.OnTicketReceived = async context =>
    {
        var repo = context.HttpContext.RequestServices.GetRequiredService<ISystemRepository>();
        var provider = "google";
        var providerId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(providerId)) return;

        var internalUserId = await repo.GetUserIdByMappingAsync(provider, providerId);
        
        if (string.IsNullOrEmpty(internalUserId))
        {
            // CREATE NEW USER (GUID)
            internalUserId = Guid.NewGuid().ToString();
            var name = context.Principal?.FindFirstValue(ClaimTypes.Name);
            var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
            var avatar = context.Principal?.FindFirstValue("urn:google:image");

            await repo.CreateUserAsync(internalUserId, name, email, avatar);
            await repo.CreateUserMappingAsync(internalUserId, provider, providerId);
        }

        // Add internal ID as a claim - this is what our APIs will use
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        identity.AddClaim(new Claim("fishbowl_user_id", internalUserId));
    };
});

// Delay Google Configuration until ISystemRepository is available.
// Empty creds are valid — /login redirects to /setup when ClientId is unconfigured.
builder.Services.AddOptions<GoogleOptions>(GoogleDefaults.AuthenticationScheme)
    .Configure<ISystemRepository>((options, repo) =>
    {
        var clientId = repo.GetConfigAsync("Google:ClientId").GetAwaiter().GetResult();
        var clientSecret = repo.GetConfigAsync("Google:ClientSecret").GetAwaiter().GetResult();
        // "placeholder" keeps GoogleOptions validation happy (non-empty) without being
        // a usable credential. /login checks for "placeholder" explicitly and redirects
        // to /setup. See docs/superpowers/specs/2026-04-18-a-plus-hardening-design.md §1.2.
        options.ClientId = clientId ?? "placeholder";
        options.ClientSecret = clientSecret ?? "placeholder";
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
app.MapGet("/login", async (string? returnUrl, HttpContext context, ISystemRepository repo) => 
{
    var clientId = await repo.GetConfigAsync("Google:ClientId");
    if (string.IsNullOrEmpty(clientId) || clientId == "placeholder")
    {
        return Results.Redirect("/setup");
    }

    var resourceProvider = context.RequestServices.GetRequiredService<IResourceProvider>();
    var resource = await resourceProvider.GetAsync("login.html");
    if (resource == null) return Results.NotFound("Login page not found.");

    return Results.Bytes(resource.Data, "text/html");
});

app.MapGet("/login/challenge/{provider}", (string provider, string? returnUrl) => 
{
    var scheme = provider.ToLower() switch
    {
        "google" => GoogleDefaults.AuthenticationScheme,
        _ => null
    };

    if (scheme == null) return Results.BadRequest("Unsupported provider.");

    return Results.Challenge(
        properties: new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
        authenticationSchemes: new[] { scheme });
});

app.MapGet("/api/auth/providers", async (ISystemRepository repo) => 
{
    var providers = new List<object>();
    
    var googleClientId = await repo.GetConfigAsync("Google:ClientId");
    if (!string.IsNullOrEmpty(googleClientId) && googleClientId != "placeholder")
    {
        providers.Add(new { id = "google", name = "Google", icon = "fa-brands fa-google" });
    }

    return Results.Ok(providers);
});

app.MapGet("/setup", async (HttpContext context, ISystemRepository repo) => 
{
    // Only allow setup if not configured or from localhost
    var clientId = await repo.GetConfigAsync("Google:ClientId");
    if (!string.IsNullOrEmpty(clientId) && clientId != "placeholder")
    {
         return Results.Redirect("/");
    }

    var resourceProvider = context.RequestServices.GetRequiredService<IResourceProvider>();
    var resource = await resourceProvider.GetAsync("setup.html");
    if (resource == null) return Results.NotFound("Setup page not found.");

    return Results.Bytes(resource.Data, "text/html");
});

app.MapPost("/api/setup", async (SetupRequest request, ISystemRepository repo) => 
{
    await repo.SetConfigAsync("Google:ClientId", request.ClientId);
    await repo.SetConfigAsync("Google:ClientSecret", request.ClientSecret);
    return Results.Ok();
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
app.MapFallback("{*path}", async (HttpContext context, IResourceProvider resources) =>
{
    var path = context.Request.Path.Value?.TrimStart('/') ?? "index.html";
    if (string.IsNullOrEmpty(path)) path = "index.html";

    var resource = await resources.GetAsync(path);
    
    if (resource == null)
    {
        if (path.Contains('.')) return Results.NotFound();
        path = "index.html";
        resource = await resources.GetAsync(path);
        if (resource == null) return Results.NotFound();
    }

    var contentType = GetContentType(path);
    return Results.Bytes(resource.Data, contentType);
});

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

app.Run();

public record SetupRequest(string ClientId, string ClientSecret);
public partial class Program { }
