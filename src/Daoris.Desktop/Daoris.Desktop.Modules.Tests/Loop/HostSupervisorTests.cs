using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// CONNECTOR1c: a shell that finds no host answering and none to start is told the two commands that lay one down, the
/// install's first, in the order <see cref="ServiceHostLocator"/> ranks them after the person's own path, and in the words
/// the driver's no-connector sentences use. Each is a script the workspace has. It starts nothing, so it is in the fast
/// half (MOD8), where <see cref="HostSupervisorTests"/> is not.
/// </summary>
public sealed class HostSupervisorNoHostTests
{
    [Fact]
    public void A_shell_with_no_host_is_told_the_install_and_the_home_commands_that_lay_one_down()
    {
        var said = HostSupervisor.NoHost("http://localhost:5177");

        Assert.Contains("no service host is running at http://localhost:5177", said);
        Assert.Contains($"no {ServiceHostLocator.ExecutableName} was found", said);
        var install = said.IndexOf(
            "`npm run publish:desktop -- --to <install> --service` lays one beside the application", StringComparison.Ordinal);
        var home = said.IndexOf("`npm run publish:service -- --install` lands one in the home's `bin/`", StringComparison.Ordinal);
        Assert.True(install >= 0, said);
        Assert.True(home > install, said);
        Assert.Contains(ServiceHostLocator.PathVariable, said);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "daoris.json"))) root = root.Parent;
        Assert.NotNull(root);
        using var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "package.json")));
        var scripts = package.RootElement.GetProperty("scripts");
        Assert.True(scripts.TryGetProperty("publish:desktop", out _), "no publish:desktop script");
        Assert.True(scripts.TryGetProperty("publish:service", out _), "no publish:service script");
    }
}

/// <summary>
/// Adopting a host that is already answering is the supervisor's rule, and it says nothing about
/// WHAT that host serves — the first deployment's 4d: the shell adopted the machine's host, the
/// window was new, the page was old, and no surface said so. The second deployment fixed which host
/// is <i>spawned</i>; this is the other door. What the shell can honestly say is what the page names
/// about itself: its own bundle.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class HostSupervisorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-supervisor-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly List<StandInHost> _standIns = [];

    public void Dispose()
    {
        // A stand-in a failed test left running would hold its folder, and a port, until the run ended.
        // Only a node by that id: one that has gone may have handed its id to somebody else's process.
        foreach (var host in _standIns)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(host.Pid());
                if (!string.Equals(process.ProcessName, "node", StringComparison.OrdinalIgnoreCase)) continue;
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5_000);
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or FormatException)
            {
                // Gone already, or it never wrote its id.
            }
        }

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

    /// <summary>
    /// HOSTID1: something answering on the service port is adopted only when it answers as a Daoris
    /// host does, with the status the service gives (its search tier). Anything else is not this
    /// machine's host, and the shell says what answered rather than feeding its page from it.
    /// </summary>
    [Fact]
    public async Task Something_else_answering_on_the_port_is_not_adopted_and_the_shell_says_so()
    {
        using var squatter = await StubHost.StartAsync("<html>another program</html>", status: "{\"ok\":true}");
        var supervisor = new HostSupervisor(squatter.Url, () => Carrying("index-CFwEAMAB.js"));

        Assert.False(await supervisor.EnsureAsync());

        Assert.NotNull(supervisor.Trouble);
        Assert.Contains(squatter.Url, supervisor.Trouble);
        Assert.Contains("not a Daoris host", supervisor.Trouble);
        Assert.Null(supervisor.Notice);
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

    /// <summary>
    /// A host that answers is up, whatever the notice beside it could read (REV3 modules F8). The
    /// install's own page, held open by something, threw out of the notice, and the loop counted an
    /// answering host as down and never started.
    /// </summary>
    [Fact]
    public async Task A_notice_that_cannot_read_its_own_page_does_not_make_an_answering_host_down()
    {
        using var running = await StubHost.StartAsync(Page("index-CeYefnb-.js"));
        var location = Carrying("index-CFwEAMAB.js");
        var supervisor = new HostSupervisor(running.Url, () => location);

        using (File.Open(Path.Combine(location.WorkingDirectory, "wwwroot", "index.html"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.True(await supervisor.EnsureAsync());
        }

        Assert.Null(supervisor.Notice);
        Assert.Null(supervisor.Trouble);
    }

    /// <summary>
    /// LOG2a: the host this shell starts is started to be stopped cleanly — its input redirected, and
    /// asked to stop when that input ends. The variable is a twin of the host's
    /// <c>InputEndStop.Variable</c>, whose tests hold the same spelling and take only <c>1</c>.
    /// </summary>
    [Fact]
    public void The_host_it_starts_is_asked_to_stop_when_its_input_ends()
    {
        var location = new HostLocation(Path.Combine(_root, "app", "daoris-knowledge-http", "host"), Path.Combine(_root, "app"));

        var start = HostSupervisor.StartInfo(location);

        Assert.Equal("DAORIS_STOP_ON_INPUT_END", HostSupervisor.StopOnInputEnd);
        Assert.Equal("1", start.Environment[HostSupervisor.StopOnInputEnd]);
        Assert.True(start.RedirectStandardInput);
        Assert.False(start.RedirectStandardOutput);
        Assert.False(start.UseShellExecute);
        Assert.True(start.CreateNoWindow);
        Assert.Equal(location.Executable, start.FileName);
        Assert.Equal(location.WorkingDirectory, start.WorkingDirectory);
    }

    /// <summary>
    /// The first real log's finding: the shell killed the host it started, so the host never wrote its
    /// stop. A host that goes when its input closes is let go, and never killed.
    /// </summary>
    [Fact]
    public async Task A_host_that_stops_when_its_input_ends_is_let_go_and_not_killed()
    {
        var host = StandIn(honoursInputEnd: true);
        var supervisor = new HostSupervisor(host.Url, () => host.Location) { StopWithin = TimeSpan.FromSeconds(20) };
        Assert.True(await supervisor.EnsureAsync(), supervisor.Trouble);

        var stopped = supervisor.Stop();

        Assert.Equal(HostStop.Exited, stopped);
        Assert.True(await GoneAsync(host.Pid()), "the stand-in host is still running");
    }

    /// <summary>A host that does not go when asked is killed, after the bound and not before.</summary>
    [Fact]
    public async Task A_host_that_ignores_its_input_ending_is_killed_after_the_bound()
    {
        var host = StandIn(honoursInputEnd: false);
        var bound = TimeSpan.FromSeconds(1);
        var supervisor = new HostSupervisor(host.Url, () => host.Location) { StopWithin = bound };
        Assert.True(await supervisor.EnsureAsync(), supervisor.Trouble);
        var pid = host.Pid();

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var stopped = supervisor.Stop();

        Assert.Equal(HostStop.Killed, stopped);
        Assert.True(clock.Elapsed >= bound - TimeSpan.FromMilliseconds(50), $"killed after {clock.Elapsed}, before the bound");
        Assert.True(await GoneAsync(pid), "the stand-in host is still running");
    }

    /// <summary>
    /// HOSTSTART1: a host that dies at start says why on its standard error, and the window showed only that it exited.
    /// The trouble carries what it printed, an em dash and 中文 whole, since the stream is read as UTF-8.
    /// </summary>
    [Fact]
    public async Task A_host_that_exits_at_start_is_said_with_what_it_printed_on_standard_error()
    {
        // Written synchronously: an exit drops what a pipe's asynchronous write still holds.
        var host = StandIn("""
            import { writeSync } from 'node:fs';
            writeSync(2, 'the knowledge index is newer than this build — 更新 the install.\n');
            process.exit(2);
            """);
        var supervisor = new HostSupervisor(host.Url, () => host.Location);

        Assert.False(await supervisor.EnsureAsync());

        Assert.NotNull(supervisor.Trouble);
        Assert.Contains($"the service host at {host.Location.Executable} exited before it answered.", supervisor.Trouble);
        Assert.EndsWith(
            "Its last lines on standard error:\nthe knowledge index is newer than this build — 更新 the install.",
            supervisor.Trouble);
    }

    /// <summary>A host that printed nothing is said as before.</summary>
    [Fact]
    public async Task A_host_that_exits_at_start_having_printed_nothing_is_said_as_before()
    {
        var host = StandIn("process.exit(3);");
        var supervisor = new HostSupervisor(host.Url, () => host.Location);

        Assert.False(await supervisor.EnsureAsync());

        Assert.Equal($"the service host at {host.Location.Executable} exited before it answered.", supervisor.Trouble);
    }

    /// <summary>
    /// The stream is read from the start, never held: a host that prints a megabyte before it listens, more than any pipe
    /// holds, is not left waiting on a full one, and answers. Written synchronously, so an unread pipe would stop it.
    /// </summary>
    [Fact]
    public async Task A_host_that_prints_a_megabyte_on_standard_error_while_starting_is_not_stalled()
    {
        var host = StandIn(
            """
            import { writeSync } from 'node:fs';
            for (let line = 0; line < 1024; line++) writeSync(2, 'x'.repeat(1023) + '\n');
            """,
            honoursInputEnd: true);
        var supervisor = new HostSupervisor(host.Url, () => host.Location) { StopWithin = TimeSpan.FromSeconds(20) };

        var clock = System.Diagnostics.Stopwatch.StartNew();
        Assert.True(await supervisor.EnsureAsync(), supervisor.Trouble);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), $"answered after {clock.Elapsed}");

        Assert.Equal(HostStop.Exited, supervisor.Stop());
    }

    /// <summary>HOSTID1's other half: a host the shell adopted is somebody else's, and its stop is theirs.</summary>
    [Fact]
    public async Task A_host_it_adopted_is_not_stopped()
    {
        using var running = await StubHost.StartAsync(Page("index-CFwEAMAB.js"));
        var supervisor = new HostSupervisor(running.Url, () => Carrying("index-CFwEAMAB.js"));
        Assert.True(await supervisor.EnsureAsync());

        Assert.Equal(HostStop.NotOwned, supervisor.Stop());

        using var probe = new HttpClient();
        Assert.Contains("tier", await probe.GetStringAsync($"{running.Url}/api/status"));
    }

    /// <summary>
    /// A stand-in for the HTTP host, as the supervisor starts it: a program at a location that answers
    /// the status probe as a Daoris host does. One that honours its input stops when that input ends,
    /// and only when asked by the variable, as the host does; one that ignores it serves on.
    /// </summary>
    private StandInHost StandIn(bool honoursInputEnd) => StandIn(string.Empty, honoursInputEnd);

    /// <summary>
    /// A stand-in that runs <paramref name="first"/> before it serves (HOSTSTART1): what it prints, and whether it exits
    /// before it ever listens. Its imports are hoisted, as a module's are.
    /// </summary>
    private StandInHost StandIn(string first, bool honoursInputEnd = false)
    {
        var folder = Path.Combine(_root, "stand-in-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(folder);
        var port = StubHost.FreePort();
        var pidFile = Path.Combine(folder, "pid");
        File.WriteAllText(Path.Combine(folder, "host.mjs"), $$"""
            import { createServer } from 'node:http';
            import { writeFileSync } from 'node:fs';
            writeFileSync({{JsonSerializer.Serialize(pidFile)}}, String(process.pid));
            {{first}}
            const server = createServer((request, response) => {
              response.writeHead(200, { 'content-type': 'application/json' });
              response.end(request.url === '/api/status' ? {{JsonSerializer.Serialize(StubHost.DaorisStatus)}} : '<html></html>');
            });
            server.listen({{port}}, '127.0.0.1');
            if ({{(honoursInputEnd ? "true" : "false")}} && process.env.{{HostSupervisor.StopOnInputEnd}} === '1') {
              process.stdin.on('data', () => {});
              process.stdin.on('end', () => { server.close(); process.exit(0); });
            }
            """);

        // The supervisor starts a location's executable by itself, with no arguments, as it starts the
        // real host: so the stand-in is a script that runs node on the program beside it.
        string executable;
        if (OperatingSystem.IsWindows())
        {
            executable = Path.Combine(folder, "host.cmd");
            File.WriteAllText(executable, "@echo off\r\nnode \"%~dp0host.mjs\"\r\n");
        }
        else
        {
            executable = Path.Combine(folder, "host");
            File.WriteAllText(executable, "#!/bin/sh\nexec node \"$(dirname \"$0\")/host.mjs\"\n");
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var host = new StandInHost($"http://127.0.0.1:{port}", new HostLocation(executable, folder), pidFile);
        _standIns.Add(host);
        return host;
    }

    private sealed record StandInHost(string Url, HostLocation Location, string PidFile)
    {
        /// <summary>The stand-in program's own process: under a script on Windows, the script itself elsewhere.</summary>
        public int Pid() => int.Parse(File.ReadAllText(PidFile), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Whether a process has gone, within a few seconds: a kill lands a moment after it is asked.</summary>
    private static async Task<bool> GoneAsync(int pid)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(pid);
                if (process.HasExited) return true;
            }
            catch (ArgumentException)
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
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

        /// <summary>What a Daoris host's status says: its search tier (and more, which is not asked).</summary>
        public const string DaorisStatus = "{\"semantic\":false,\"tier\":\"lexical only\",\"note\":null}";

        public static async Task<StubHost> StartAsync(string page, string status = DaorisStatus)
        {
            var listener = new HttpListener();
            var url = $"http://127.0.0.1:{FreePort()}/";
            listener.Prefixes.Add(url);
            listener.Start();
            var host = new StubHost(listener, url.TrimEnd('/'));
            _ = host.ServeAsync(page, status);
            await Task.Yield();
            return host;
        }

        private async Task ServeAsync(string page, string status)
        {
            while (!_stopping.IsCancellationRequested)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception error) when (error is HttpListenerException or ObjectDisposedException) { return; }

                var body = context.Request.Url?.AbsolutePath == "/api/status" ? status : page;
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = 200;
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        public static int FreePort()
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
