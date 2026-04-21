using Fishbowl.Core.Search;
using Fishbowl.Search;
using Xunit;

namespace Fishbowl.Search.Tests;

// Two flavours:
//   * Always-on contract tests — no model needed.
//   * Model-dependent semantic tests — skip when the MiniLM-L6-v2 files
//     aren't on disk at the shared cache path below. The cache survives
//     across runs so a developer (or CI job) runs `EnsureModelAsync` once
//     and subsequent runs exercise the semantic properties.
public class EmbeddingServiceTests : IDisposable
{
    private readonly string _tempRoot;
    // Stable cache path under the system temp dir so the downloaded model
    // persists across test runs. Tests pointing here never mutate the files;
    // they only read.
    private static readonly string SharedModelCache =
        Path.Combine(Path.GetTempPath(), "fishbowl-test-models");

    public EmbeddingServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "fishbowl_emb_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task EmbedAsync_ThrowsEmbeddingUnavailable_WhenModelNotReady()
    {
        var downloader = new ModelDownloader(_tempRoot);
        Assert.False(downloader.IsReady());

        using var service = new EmbeddingService(downloader);
        await Assert.ThrowsAsync<EmbeddingUnavailableException>(
            async () => await service.EmbedAsync("hello", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Dimensions_Is384()
    {
        var downloader = new ModelDownloader(_tempRoot);
        using var service = new EmbeddingService(downloader);
        // The contract with sqlite-vec's `vec_notes` FLOAT[384] column —
        // hard-pinned so a future swap to a different model doesn't silently
        // mismatch the schema.
        Assert.Equal(384, service.Dimensions);
    }

    [Fact]
    public async Task EmbedAsync_ReturnsNormalisedVector_OfDimension384()
    {
        SkipIfModelMissing();
        using var service = BuildService();

        var vec = await service.EmbedAsync(
            "The quick brown fox jumps over the lazy dog",
            TestContext.Current.CancellationToken);

        Assert.Equal(384, vec.Length);

        // L2 norm ≈ 1 — the pipeline normalises so downstream can treat
        // cosine similarity as a plain dot product.
        var normSquared = 0.0;
        foreach (var x in vec) normSquared += x * x;
        Assert.InRange(Math.Sqrt(normSquared), 0.99, 1.01);
    }

    [Fact]
    public async Task EmbedAsync_IsDeterministic_ForSameInput()
    {
        SkipIfModelMissing();
        using var service = BuildService();

        var a = await service.EmbedAsync("hello world", TestContext.Current.CancellationToken);
        var b = await service.EmbedAsync("hello world", TestContext.Current.CancellationToken);

        Assert.Equal(a.Length, b.Length);
        for (var i = 0; i < a.Length; i++)
        {
            // FP rounding is irrelevant across a single process but we keep
            // the window wide enough for future CPU/ORT tweaks.
            Assert.InRange(Math.Abs(a[i] - b[i]), 0.0f, 1e-5f);
        }
    }

    [Fact]
    public async Task EmbedAsync_SemanticallySimilarInputs_ScoreCloserThanUnrelated()
    {
        SkipIfModelMissing();
        using var service = BuildService();
        var ct = TestContext.Current.CancellationToken;

        var cat = await service.EmbedAsync("cat", ct);
        var kitten = await service.EmbedAsync("kitten", ct);
        var motorcycle = await service.EmbedAsync("motorcycle", ct);

        // Cosine = dot product for L2-normalised vectors.
        var closeSim = Dot(cat, kitten);
        var farSim = Dot(cat, motorcycle);

        // The expected gap with MiniLM-L6-v2 on this triple is comfortably
        // positive (~0.3+). A tight inequality is enough to catch a model
        // swap or a broken pipeline, without being fragile to minor model
        // updates.
        Assert.True(closeSim > farSim + 0.05,
            $"Expected cat↔kitten similarity > cat↔motorcycle + 0.05, got {closeSim} vs {farSim}");
    }

    private EmbeddingService BuildService()
        => new(new ModelDownloader(SharedModelCache));

    private static void SkipIfModelMissing()
    {
        var downloader = new ModelDownloader(SharedModelCache);
        if (!downloader.IsReady())
        {
            Assert.Skip(
                $"MiniLM-L6-v2 model not present at {Path.GetDirectoryName(downloader.ModelPath)}. " +
                "Run `dotnet run --project src/Fishbowl.Host` once to populate, " +
                "or copy an existing download into the shared cache.");
        }
    }

    private static float Dot(float[] a, float[] b)
    {
        var s = 0.0;
        for (var i = 0; i < a.Length; i++) s += a[i] * b[i];
        return (float)s;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, true); } catch { }
        }
    }
}
