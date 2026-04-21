using Fishbowl.Core.Models;

namespace Fishbowl.Core.Search;

// Hybrid (semantic + FTS) note search. Implementations live in
// Fishbowl.Search; callers (MCP SearchMemoryTool, future UI search) only
// need this contract. Secrets are stripped by the implementation before
// the result reaches the caller.
public interface ISearchService
{
    Task<HybridSearchResult> HybridSearchAsync(
        ContextRef ctx,
        string query,
        int limit,
        bool includePending,
        CancellationToken ct = default);
}

// A single ranked hit. `Score` is the merged vec+FTS rank in [0, 1];
// useful for the UI to show relevance indicators but callers should treat
// the order of the list as authoritative.
public record MemorySearchResult(Note Note, double Score);

// Response envelope. `Degraded` is true when the embedding model wasn't
// ready and the ranking is FTS-only — UI can surface a "semantic search
// warming up" hint.
public record HybridSearchResult(IReadOnlyList<MemorySearchResult> Hits, bool Degraded);
