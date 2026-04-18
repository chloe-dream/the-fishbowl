using System.Data;
using Dapper;
using Fishbowl.Core.Repositories;

namespace Fishbowl.Data.Repositories;

public class SystemRepository : ISystemRepository
{
    private readonly DatabaseFactory _dbFactory;

    public SystemRepository(DatabaseFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<string?> GetUserIdByMappingAsync(string provider, string providerId, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateSystemConnection();
        return await db.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT user_id FROM user_mappings WHERE provider = @provider AND provider_id = @providerId",
            new { provider, providerId }, cancellationToken: ct));
    }

    public async Task<bool> CreateUserMappingAsync(string userId, string provider, string providerId, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateSystemConnection();
        var affected = await db.ExecuteAsync(
            new CommandDefinition("INSERT INTO user_mappings (provider, provider_id, user_id) VALUES (@provider, @providerId, @userId)",
            new { provider, providerId, userId }, cancellationToken: ct));
        return affected > 0;
    }

    public async Task<bool> CreateUserAsync(string userId, string? name, string? email, string? avatarUrl, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateSystemConnection();
        var affected = await db.ExecuteAsync(
            new CommandDefinition("INSERT INTO users (id, name, email, avatar_url, created_at) VALUES (@userId, @name, @email, @avatarUrl, @createdAt)",
            new { userId, name, email, avatarUrl, createdAt = DateTime.UtcNow.ToString("o") }, cancellationToken: ct));
        return affected > 0;
    }

    public async Task<dynamic?> GetUserAsync(string userId, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateSystemConnection();
        return await db.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition("SELECT * FROM users WHERE id = @userId", new { userId }, cancellationToken: ct));
    }

    public async Task<string?> GetConfigAsync(string key, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateSystemConnection();
        return await db.ExecuteScalarAsync<string>(
            new CommandDefinition("SELECT value FROM system_config WHERE key = @key", new { key }, cancellationToken: ct));
    }

    public async Task<bool> SetConfigAsync(string key, string value, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateSystemConnection();
        var affected = await db.ExecuteAsync(
            new CommandDefinition(@"
                INSERT INTO system_config (key, value, updated_at) 
                VALUES (@key, @value, @updatedAt)
                ON CONFLICT(key) DO UPDATE SET value = @value, updated_at = @updatedAt",
            new { key, value, updatedAt = DateTime.UtcNow.ToString("o") }, cancellationToken: ct));
        return affected > 0;
    }
}
