using Microsoft.AspNetCore.Authentication;

namespace Fishbowl.Host.Auth;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
}
