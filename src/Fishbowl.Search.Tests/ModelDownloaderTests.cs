using Fishbowl.Search;
using Xunit;

namespace Fishbowl.Search.Tests;

// No-network tests for ModelDownloader's behaviour around the models dir.
// The actual download flow is exercised manually (too slow / too dependent
// on network for CI); these cover the readiness and path logic so a typo in
// EmbeddingService won't silently read from the wrong place.
public class ModelDownloaderTests : IDisposable
{
    private readonly string _tempRoot;

    public ModelDownloaderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "fishbowl_model_dl_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void IsReady_ReturnsFalse_WhenFilesAbsent()
    {
        var dl = new ModelDownloader(_tempRoot);
        Assert.False(dl.IsReady());
    }

    [Fact]
    public void IsReady_ReturnsFalse_WhenOnlyOneFilePresent()
    {
        var dl = new ModelDownloader(_tempRoot);
        var modelsDir = Path.GetDirectoryName(dl.ModelPath)!;
        Directory.CreateDirectory(modelsDir);

        // Only vocab.txt present → incomplete setup, not ready.
        File.WriteAllText(dl.VocabPath, "[PAD]\n[CLS]\n");
        Assert.False(dl.IsReady());
    }

    [Fact]
    public void IsReady_ReturnsTrue_WhenBothFilesPresent()
    {
        var dl = new ModelDownloader(_tempRoot);
        var modelsDir = Path.GetDirectoryName(dl.ModelPath)!;
        Directory.CreateDirectory(modelsDir);

        // Stand-in bytes — we only care about presence for IsReady, not shape.
        File.WriteAllBytes(dl.ModelPath, new byte[] { 0 });
        File.WriteAllText(dl.VocabPath, "[PAD]\n");
        Assert.True(dl.IsReady());
    }

    [Fact]
    public void ModelPath_AndVocabPath_AreInDataRootModelsSubfolder()
    {
        var dl = new ModelDownloader(_tempRoot);
        // Embedding callers and re-index endpoints rely on the fixed layout —
        // keep it surfaced as part of the contract.
        Assert.StartsWith(
            Path.Combine(_tempRoot, "models", "MiniLmL6V2"),
            dl.ModelPath);
        Assert.StartsWith(
            Path.Combine(_tempRoot, "models", "MiniLmL6V2"),
            dl.VocabPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, true); } catch { }
        }
    }
}
