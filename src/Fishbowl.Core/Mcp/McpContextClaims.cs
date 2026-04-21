using System.Security.Claims;

namespace Fishbowl.Core.Mcp;

// Claim names shared by the Bearer auth handler and downstream endpoints.
// Cookie-authenticated requests only carry `UserId`; Bearer-authenticated
// requests additionally carry `ContextType` + `ContextId` (the binding the
// API key was issued against) and one `Scope` claim per permitted action.
public static class McpContextClaims
{
    public const string UserId = "fishbowl_user_id";
    public const string ContextType = "fishbowl_context_type";
    public const string ContextId = "fishbowl_context_id";
    public const string Scope = "scope";

    // Name of the authentication scheme used for Bearer tokens. Duplicated
    // here (vs ApiKeyAuthenticationOptions.DefaultScheme in Fishbowl.Host)
    // because Fishbowl.Api sits below Host in the dep graph and must not
    // reference host-level auth types.
    public const string BearerScheme = "ApiKey";

    // Collapses the principal to a single `ContextRef`. Bearer principals with
    // an explicit team context resolve to ContextRef.Team; everything else
    // (cookie users and Bearer keys bound to the personal space) resolves to
    // ContextRef.User(fishbowl_user_id). Throws when the principal has no
    // usable identity — callers should have already gated with [Authorize].
    public static ContextRef Resolve(ClaimsPrincipal user)
    {
        var type = user.FindFirst(ContextType)?.Value;
        var id = user.FindFirst(ContextId)?.Value;
        if (string.Equals(type, "team", StringComparison.Ordinal) && !string.IsNullOrEmpty(id))
            return ContextRef.Team(id);

        var uid = user.FindFirst(UserId)?.Value;
        if (!string.IsNullOrEmpty(uid)) return ContextRef.User(uid);

        throw new InvalidOperationException("Principal has no resolvable context.");
    }
}
