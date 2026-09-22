using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Adopting a host that is already answering is the supervisor's rule, and it says nothing about
/// WHAT that host serves — the first deployment's 4d: the shell adopted the machine's host, the
/// window was new, the page was old, and no surface said so. The second deployment fixed which host
/// is <i>spawned</i>; this is the other door. What the shell can honestly say is what the page names
/// about itself: its own bundle.
/// </summary>
public sealed class HostSupervisorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-supervisor-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void The_bundle_a_page_names_is_read_from_its_script_tag()
    {
        Assert.Equal("index-CFwEAMAB.js", HostSupervisor.BundleNamed(
            "<script type=\"module\" crossorigin src=\"/assets/index-CFwEAMAB.js\"></script>"));
        Assert.Null(HostSupervisor.BundleNamed("<html><body>not the platform</body></html>"));
    }

    /// <summary>
    /// The case itself: a host somebody else started, serving the page it was installed with, and an
    /// install that carries a different one. The notice names both, so the person can tell which
    /// page they are looking at without listing a folder.
    /// </summary>
    [Fact]
    public async Task Adopting_a_host_that_serves_another_page_than_this_install_carries_says_so()
    {
        using var running = await StubHost.StartAsync(Page("index-CeYefnb-.js"));
        var supervisor = new HostSupervisor(running.Url, () => Carrying("index-CFwEAMAB.js"));

        Assert.True(await supervisor.EnsureAsync());

        Assert.NotNull(supervisor.Notice);
        Assert.Contains("index-CeYefnb-.js", supervisor.Notice);
        Assert.Contains("index-CFwEAMAB.js", supervisor.Notice);
        Assert.Null(supervisor.Trouble);
    }

    [Fact]
    public async Task Adopting_a_host_that_serves_this_install_s_own_page_is_silent()
    {
        using var running = await StubHost.StartAsync(Page("index-CFwEAMAB.js"));
        var supervisor = new HostSupervisor(running.Url, () => Carrying("index-CFwEAMAB.js"));

        Assert.True(await supervisor.EnsureAsync());

        Assert.Null(supervisor.Notice);
    }

    /// <summary>
    /// Nothing to compare against is not a difference: a shell with no host of its own — nothing
    /// installed, no workspace — adopts whatever answers and says nothing, as it always did.
    /// </summary>
    [Fact]
    public async Task A_shell_carrying_no_host_of_its_own_adopts_in_silence()
    {
        using var running = await StubHost.StartAsync(Page("index-CeYefnb-.js"));
        var supervisor = new HostSupervisor(running.Url, () => null);

        Assert.True(await supervisor.EnsureAsync());

        Assert.Null(supervisor.Notice);
    }

    private static string Page(string bundle) =>
        $"<!doctype html><html><head><script type=\"module\" crossorigin src=\"/assets/{bundle}\"></script></head></html>";

    /// <summary>An install's host location, with the page it carries beside it.</summary>
    private HostLocation Carrying(string bundle)
    {
        var home = Path.Combine(_root, "app", "daoris-knowledge-http");
        Directory.CreateDirectory(Path.Combine(home, "wwwroot"));
        File.WriteAllText(Path.Combine(home, "wwwroot", "index.html"), Page(bundle));
        return new HostLocation(Path.Combine(home, ServiceHostLocator.ExecutableName), home);
    }

    /// <summary>
    /// A host that answers its status probe and serves one page — the two things the supervisor
    /// asks of a host it did not start.
    /// </summary>
    private sealed class StubHost : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _stopping = new();

        public string Url { get; }

        private StubHost(HttpListener listener, string url)
        {
            _listener = listener;
            Url = url;
        }

        public static async Task<StubHost> StartAsync(string page)
        {
            var listener = new HttpListener();
            var url = $"http://127.0.0.1:{FreePort()}/";
            listener.Prefixes.Add(url);
            listener.Start();
            var host = new StubHost(listener, url.TrimEnd('/'));
            _ = host.ServeAsync(page);
            await Task.Yield();
            return host;
        }

        private async Task ServeAsync(string page)
        {
            while (!_stopping.IsCancellationRequested)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception error) when (error is HttpListenerException or ObjectDisposedException) { return; }

                var body = context.Request.Url?.AbsolutePath == "/api/status" ? "{}" : page;
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = 200;
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        private static int FreePort()
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        public void Dispose()
        {
            _stopping.Cancel();
            _listener.Stop();
            _listener.Close();
        }
    }
}
