using Fishbowl.Core.Models;

namespace Fishbowl.Core.Plugins;

/// <summary>
/// Bidirectional sync with an external calendar/data source.
/// Fishbowl remains the source of truth; on conflict, Fishbowl wins.
/// </summary>
public interface ISyncProvider
{
    string Name { get; }
    Task<SyncResult> PullAsync(string userId, SyncSource source, CancellationToken ct);
    Task PushAsync(string userId, SyncTarget target, IEnumerable<Event> events, CancellationToken ct);
}
