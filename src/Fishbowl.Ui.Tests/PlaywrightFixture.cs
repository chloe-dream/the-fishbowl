using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;

namespace Fishbowl.Ui.Tests;

/// <summary>
/// Launches Fishbowl.Host as a subprocess on a free port and a headless
/// Chromium so the Playwright smoke test can hit real HTTP. Installs
/// Chromium on first use.
/// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    private Process? _hostProcess;
    public string BaseUrl { get; private set; } = string.Empty;
    public IPlaywright? Playwright { get; private set; }
    public IBrowser? Browser { get; private set; }

    public async ValueTask InitializeAsync()
    {
        // Install chromium (no-op if already cached)
        var installExit = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        if (installExit != 0)
            throw new InvalidOperationException($"Playwright chromium install failed (exit {installExit})");

        // Pick a free loopback port
        var port = FindFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        // Launch Fishbowl.Host directly via the compiled dll, bypassing launchSettings.json profiles.
        // The path resolves from bin/Debug/net10.0 up to src, then down into Fishbowl.Host.
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var hostDll = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Fishbowl.Host", "bin", configuration, "net10.0", "Fishbowl.Host.dll"));

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{hostDll}\" --urls {BaseUrl}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        // This is the magic marker that enables auto-auth in the Host's middleware
        psi.Environment["FISHBOWL_PLAYWRIGHT_TEST"] = "true";

        _hostProcess = new Process { StartInfo = psi };
        _hostProcess.Start();

        // Wait up to 60s for the host to respond
        await WaitForHttpReady(BaseUrl + "/api/v1/version", TimeSpan.FromSeconds(60));

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser != null) await Browser.CloseAsync();
        if (Playwright != null) Playwright.Dispose();
        if (_hostProcess != null && !_hostProcess.HasExited)
        {
            _hostProcess.Kill(entireProcessTree: true);
            await _hostProcess.WaitForExitAsync();
            _hostProcess.Dispose();
        }
    }

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForHttpReady(string url, TimeSpan timeout)
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow + timeout;
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var res = await http.GetAsync(url);
                if (res.IsSuccessStatusCode) return;
            }
            catch (Exception ex) { last = ex; }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Server at {url} did not become ready within {timeout}. Last error: {last?.Message}");
    }
}
