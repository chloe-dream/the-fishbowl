using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Fishbowl.Core;
using Fishbowl.Host.Plugins;
using Xunit;

namespace Fishbowl.Host.Tests;

public class PluginIsolationTests
{
    [Fact]
    public void LoadPlugin_IsolatedAssembly_CanInstantiateAndCall()
    {
        // 1. Define a "Magical" Plugin in a string
        string pluginSource = @"
            using Fishbowl.Core;
            using Microsoft.Extensions.DependencyInjection;

            namespace TestPlugin;

            public class MyMagicPlugin : IFishbowlPlugin
            {
                public string Name => ""Magic Plugin"";
                public string Version => ""1.0.0"";
                
                public void Register(IServiceCollection services, IFishbowlApi api) 
                {
                    // Do nothing
                }
            }
        ";

        // 2. Compile it on-the-fly using Roslyn
        var syntaxTree = CSharpSyntaxTree.ParseText(pluginSource, cancellationToken: TestContext.Current.CancellationToken);

        // We need to reference the assemblies for IFishbowlPlugin and IServiceCollection
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IFishbowlPlugin).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "netstandard.dll")),
        };

        var compilation = CSharpCompilation.Create(
            "MagicPluginAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms, cancellationToken: TestContext.Current.CancellationToken);

        if (!result.Success)
        {
            var failures = string.Join("\n", result.Diagnostics.Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
            Assert.Fail($"Compilation failed:\n{failures}");
        }

        ms.Seek(0, SeekOrigin.Begin);

        // 3. Load it via our PluginLoadContext
        // For this test, we'll write it to a temp file because PluginLoadContext uses AssemblyDependencyResolver which expects a path
        var tempDir = Path.Combine(Path.GetTempPath(), "fishbowl_plugin_test_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var pluginPath = Path.Combine(tempDir, "MagicPlugin.dll");
        File.WriteAllBytes(pluginPath, ms.ToArray());

        try
        {
            var alc = new PluginLoadContext(pluginPath);
            var assembly = alc.LoadFromAssemblyPath(pluginPath);

            // 4. Verify we can find and instantiate the plugin
            var pluginType = assembly.GetTypes().FirstOrDefault(t => typeof(IFishbowlPlugin).IsAssignableFrom(t));
            Assert.NotNull(pluginType);

            var pluginInstance = Activator.CreateInstance(pluginType!) as IFishbowlPlugin;
            Assert.NotNull(pluginInstance);
            Assert.Equal("Magic Plugin", pluginInstance!.Name);
            Assert.Equal("1.0.0", pluginInstance.Version);

            // 5. Cleanup
            alc.Unload();
        }
        catch (Exception ex)
        {
            Assert.Fail($"Test failed with exception: {ex.Message}\nStack Trace: {ex.StackTrace}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                // Note: Unloading might not be instant, deletion might fail if file is locked
                // For tests, we'll try our best
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
