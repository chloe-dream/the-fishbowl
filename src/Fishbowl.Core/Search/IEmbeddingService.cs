namespace Fishbowl.Core.Search;

// Produces a fixed-dimensional vector from arbitrary text. Callers (note
// writes, hybrid search queries) assume the vector is L2-normalised so cosine
// similarity reduces to a dot product.
//
// Why in Fishbowl.Core and not Fishbowl.Search: NoteRepository (Fishbowl.Data)
// calls this on every write, but Data must not depend on Search — the
// dependency direction in CLAUDE.md is `Data/Search → Core`. Putting the
// contract here keeps both sides clean.
public interface IEmbeddingService
{
    // 384 for all-MiniLM-L6-v2. Exposed as a property so callers sizing
    // buffers don't have to hard-code it.
    int Dimensions { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}

// Thrown when the embedding model hasn't finished downloading yet. Callers
// on the hot path (NoteRepository writes, HybridSearchService queries) catch
// this and gracefully degrade — the row gets indexed on the next re-index,
// the query falls back to FTS-only.
public class EmbeddingUnavailableException : Exception
{
    public EmbeddingUnavailableException(string message) : base(message) { }
    public EmbeddingUnavailableException(string message, Exception inner) : base(message, inner) { }
}
