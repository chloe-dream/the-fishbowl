using Dapper;
using Microsoft.Data.Sqlite;

namespace Fishbowl.Host.Configuration;

/// <summary>
/// Synchronous peek at a handful of system.db keys needed before DI is
/// built. The regular ConfigurationCache + ConfigurationInitializer flow
/// runs as an IHostedService, which fires *after* Kestrel + LettuceEncrypt
/// are registered — too late for anything that has to bind a port or
/// declare a domain list at startup.
///
/// Only used for boot-time decisions (TLS, bindings). Runtime config still
/// flows through ConfigurationCache so /api/setup updates are observable
/// without a restart.
/// </summary>
public static class BootConfig
{
    public record AcmeSettings(IReadOnlyList<string> Domains, string Email, bool AcceptTos)
    {
        public bool IsValid => AcceptTos && Domains.Count > 0 && !string.IsNullOrWhiteSpace(Email);
    }

    /// <summary>
    /// Returns ACME config if system.db exists and all three keys are set.
    /// Returns null on fresh install or when any field is missing — caller
    /// skips LettuceEncrypt registration and runs HTTP-only.
    /// </summary>
    public static AcmeSettings? LoadAcme(string dataRoot)
    {
        var dbPath = Path.Combine(dataRoot, "system.db");
        if (!File.Exists(dbPath)) return null;

        try
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            conn.Open();

            var domainsStr = conn.QuerySingleOrDefault<string?>(
                "SELECT value FROM system_config WHERE key = 'Acme:Domains'");
            var email = conn.QuerySingleOrDefault<string?>(
                "SELECT value FROM system_config WHERE key = 'Acme:Email'");
            var tos = conn.QuerySingleOrDefault<string?>(
                "SELECT value FROM system_config WHERE key = 'Acme:AcceptTos'");

            if (string.IsNullOrWhiteSpace(domainsStr) || string.IsNullOrWhiteSpace(email)) return null;
            if (!string.Equals(tos, "true", StringComparison.OrdinalIgnoreCase)) return null;

            var domains = domainsStr
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();
            if (domains.Length == 0) return null;

            return new AcmeSettings(domains, email, AcceptTos: true);
        }
        catch
        {
            // system_config table missing (pre-v1 schema), file locked, or any
            // other read failure — treat as "no ACME config" and boot HTTP-only.
            return null;
        }
    }
}
