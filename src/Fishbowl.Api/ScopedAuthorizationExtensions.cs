using Fishbowl.Core.Mcp;
using Microsoft.AspNetCore.Builder;

namespace Fishbowl.Api;

public static class ScopedAuthorizationExtensions
{
    // Cookie/OAuth principals have no scope claims and represent a human
    // with full access — the check is "unless it's a Bearer token, let it
    // through". Bearer principals must carry the exact scope claim.
    //
    // Fail-closed: an unrecognised authentication type is treated as Bearer,
    // so a future non-cookie scheme that forgets to emit scopes defaults to
    // denied rather than accidentally bypassing the check.
    public static RouteHandlerBuilder RequireScope(this RouteHandlerBuilder builder, string scope)
        => builder.RequireAuthorization(policy => policy.RequireAssertion(ctx =>
        {
            var authType = ctx.User.Identity?.AuthenticationType;
            // Only Bearer principals are scope-gated. Everything else passes.
            if (authType != McpContextClaims.BearerScheme) return true;
            return ctx.User.HasClaim(McpContextClaims.Scope, scope);
        }));
}
