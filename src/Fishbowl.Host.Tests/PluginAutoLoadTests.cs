using Fishbowl.Core;
using Fishbowl.Core.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fishbowl.Host.Tests;

public class PluginAutoLoadTests : IDisposable
{
    private readonly string _tempPluginDir;

    public PluginAutoLoadTests()
    {
        _tempPluginDir = Path.Combine(Path.GetTempPath(), "fishbowl_plugin_autoload_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempPluginDir);
    }

    [Fact]
    public void PluginLoader_LoadsPluginsFromDirectory_RegistersServices_Test()
    {
        // Arrange — compile a plugin that registers a fake IBotClient
        var pluginSource = @"
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading;
            using System.Threading.Tasks;
            using Fishbowl.Core;
            using Fishbowl.Core.Plugins;
            using Microsoft.Extensions.DependencyInjection;

            namespace TestPlugin;

            public class MyBot : IBotClient {
                public string Name => ""my-bot"";
                public Task SendAsync(string userId, string message, CancellationToken ct) => Task.CompletedTask;
                public async IAsyncEnumerable<IncomingMessage> ReceiveAsync([EnumeratorCancellation] CancellationToken ct) {
                    await Task.CompletedTask;
                    yield break;
                }
            }

            public class MyPlugin : IFishbowlPlugin {
                public string Name => ""My Plugin"";
                public string Version => ""1.0.0"";
                public void Register(IServiceCollection services, IFishbowlApi api) {
                    api.AddBotClient(new MyBot());
                }
            }";

        var pluginPath = CompilePluginToFile(pluginSource, _tempPluginDir, "MyPlugin.dll");
        Assert.True(File.Exists(pluginPath));

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Plugins:Path", _tempPluginDir);
        });

        // Act — booting the factory runs the plugin loader
        using var scope = factory.Services.CreateScope();
        var bots = scope.ServiceProvider.GetServices<IBotClient>().ToList();

        // Assert
        Assert.Contains(bots, b => b.Name == "my-bot");
    }

    private static string CompilePluginToFile(string source, string outputDir, string filename)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken);
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IFishbowlPlugin).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "netstandard.dll")),
        };

        var compilation = CSharpCompilation.Create(
            Path.GetFileNameWithoutExtension(filename),
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms, cancellationToken: TestContext.Current.CancellationToken);

        if (!result.Success)
        {
            var failures = string.Join("\n", result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            throw new InvalidOperationException($"Compilation failed:\n{failures}");
        }

        var path = Path.Combine(outputDir, filename);
        File.WriteAllBytes(path, ms.ToArray());
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPluginDir))
        {
            try { Directory.Delete(_tempPluginDir, true); } catch { }
        }
    }
}
