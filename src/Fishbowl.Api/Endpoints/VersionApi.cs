using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Fishbowl.Api.Endpoints;

public static class VersionApi
{
    public static RouteGroupBuilder MapVersionApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1");

        group.MapGet("/version", () =>
            Results.Ok(new { version = "0.1.0-alpha" }))
        .WithName("GetVersion")
        .WithSummary("Returns the running server version.")
        .Produces<object>();

        return group;
    }
}
