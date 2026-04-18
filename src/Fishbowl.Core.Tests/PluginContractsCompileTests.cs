using Fishbowl.Core;
using Fishbowl.Core.Models;
using Fishbowl.Core.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fishbowl.Core.Tests;

// Compile-only tests: they verify that a plugin author can implement
// each contract. If these classes compile, the interfaces are usable.

public class PluginContractsCompileTests
{
    [Fact]
    public void Contracts_AreImplementable_Test()
    {
        IBotClient bot = new FakeBotClient();
        ISyncProvider sync = new FakeSyncProvider();
        IScheduledJob job = new FakeScheduledJob();
        IFishbowlPlugin plugin = new FakePlugin();

        Assert.Equal("fake", bot.Name);
        Assert.Equal("fake-sync", sync.Name);
        Assert.Equal("fake-job", job.Name);
        Assert.Equal("FakePlugin", plugin.Name);
    }

    private sealed class FakeBotClient : IBotClient
    {
        public string Name => "fake";
        public Task SendAsync(string userId, string message, CancellationToken ct) => Task.CompletedTask;
        public async IAsyncEnumerable<IncomingMessage> ReceiveAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class FakeSyncProvider : ISyncProvider
    {
        public string Name => "fake-sync";
        public Task<SyncResult> PullAsync(string userId, SyncSource source, CancellationToken ct) =>
            Task.FromResult(new SyncResult(0, 0, 0));
        public Task PushAsync(string userId, SyncTarget target, IEnumerable<Event> events, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class FakeScheduledJob : IScheduledJob
    {
        public string Name => "fake-job";
        public string CronExpression => "*/5 * * * *";
        public Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakePlugin : IFishbowlPlugin
    {
        public string Name => "FakePlugin";
        public string Version => "0.0.1";
        public void Register(IServiceCollection services, IFishbowlApi api)
        {
            api.AddBotClient(new FakeBotClient());
            api.AddSyncProvider(new FakeSyncProvider());
            api.AddScheduledJob(new FakeScheduledJob());
        }
    }
}
