using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The trust hold, said in a shape a screen can act on (D73) — not only in a sentence.
/// </summary>
/// <remarks>
/// <para>The hold was a sentence, and a sentence is for a person: a page that classified it by
/// matching the words would turn every rewording into a silent change of behaviour. So a tick reports
/// each trust hold as a fact — the folder the harness has not trusted, the file the driver read that
/// from, and the quest (or ask) it held — which is what the screen's *trust this folder…* grants,
/// and nothing else.</para>
///
/// <para>Driven through a REAL tick, on a stand-in for Claude Code — node answering the toolchain's own
/// questions — under a NAMED profile, so the file the hold reads is a fixture in this test's home and
/// never the machine's real one. No model anywhere, and no account.</para>
/// </remarks>
public sealed class TrustHoldTickTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-trust-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;
    private readonly string _trustFile;

    public TrustHoldTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "trust@example.com");
        Git("config", "user.name", "D73");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");

        // The account a session here runs as: a named profile, whose configuration home Daoris owns —
        // and whose own record has seen another folder, never this one.
        var profile = Path.Combine(_home, "harnesses", "claude-code", "work");
        Directory.CreateDirectory(profile);
        _trustFile = Path.Combine(profile, ClaudeTrust.FileName);
        File.WriteAllText(_trustFile, """{"projects":{"D:/somewhere/else":{"hasTrustDialogAccepted":true}}}""");
        File.WriteAllText(Path.Combine(_home, "harnesses.json"), """{"defaults":{"claude-code":"work"}}""");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// 🔴 <b>Measured (2026-09-24): the rules Daoris hands over at spawn reach an untrusted session, on
    /// both doors</b> (`docs/2026-09-24-deploy1-acp-trust-evidence.md`). With PERM1's `connector`
    /// default carried, the session can take and close its quest in a folder nobody trusted, so there
    /// is nothing to hold for: trust then decides only whether the repository's OWN allow-list counts.
    /// </summary>
    [Fact]
    public async Task A_session_handed_the_connector_is_not_held_for_trust()
    {
        await using var service = StandInService.Start(_repository);

        var report = await Driver(service).TickAsync();

        Assert.Empty(report.Untrusted);
        Assert.DoesNotContain(report.Events, line => line.Contains("never been trusted", StringComparison.Ordinal));
        Assert.Contains(report.Events, line => line.StartsWith("refused", StringComparison.Ordinal));
    }

    /// <summary>
    /// The hold is back whenever the allowance would not reach the session: the person denied the tool
    /// the session takes its quest with, so only the repository's own list could allow it, and that
    /// list counts only in a trusted folder.
    /// </summary>
    [Fact]
    public async Task A_session_denied_its_connector_is_held_for_trust_again()
    {
        File.WriteAllText(
            Path.Combine(_home, PermissionRules.FileName),
            """{"machine":{"deny":["mcp__daoris-knowledge__quest_respond"]}}""");
        await using var service = StandInService.Start(_repository);

        var report = await Driver(service).TickAsync();

        Assert.Single(report.Untrusted);
    }

    [Fact]
    public async Task A_trust_hold_names_the_folder_the_file_and_the_quest_and_a_grant_lifts_it()
    {
        // The connector default switched off: the one case left in which trust decides whether the
        // session can take its quest at all.
        File.WriteAllText(Path.Combine(_home, PermissionRules.FileName), """{"defaultsOff":["connector"]}""");
        await using var service = StandInService.Start(_repository);
        var driver = Driver(service);

        var held = await driver.TickAsync();

        var hold = Assert.Single(held.Untrusted);
        Assert.Equal(_repository, hold.Folder);
        Assert.Equal(_trustFile, hold.TrustFile);
        Assert.Equal("q1", hold.Quest);
        Assert.Null(hold.Ask);
        // The sentence still says it, for a person reading the console — and the command it names
        // grants the account the hold read, the named profile, not the machine's default.
        Assert.Contains(held.Events, line => line.Contains("never been trusted", StringComparison.Ordinal)
            && line.Contains("--profile work", StringComparison.Ordinal));

        ClaudeTrust.Grant(hold.TrustFile, hold.Folder);
        var after = await driver.TickAsync();

        Assert.Empty(after.Untrusted);
        Assert.DoesNotContain(after.Events, line => line.Contains("never been trusted", StringComparison.Ordinal));
        // Past the hold, to the ledger — which this stand-in refuses, so nothing runs.
        Assert.Contains(after.Events, line => line.StartsWith("refused", StringComparison.Ordinal));
    }

    private Daoris.Driver.Driver Driver(StandInService service)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Adapter = "claude-code",
            TimeoutMinutes = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = ["node", Harness()] },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>A stand-in for Claude Code: it answers its version and its login question, and nothing else.</summary>
    private string Harness()
    {
        var script = Path.Combine(_home, "claude.mjs");
        File.WriteAllText(script, """
            const argv = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.0 (Claude Code)'); process.exit(0); }
            if (argv[0] === 'auth' && argv[1] === 'status') { console.log('{"loggedIn": true}'); process.exit(0); }
            process.exit(3);
            """);
        return script;
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    /// <summary>
    /// The service's reads one tick makes — one open quest to `engine`, adopted with a root here — and a
    /// ledger that refuses every session it is asked to open, so a tick past the hold starts nothing.
    /// </summary>
    private sealed class StandInService : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly string _root;

        public string Url { get; }

        private StandInService(HttpListener listener, string url, string root)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _root = root;
            _serving = ServeAsync();
        }

        public static StandInService Start(string root)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new StandInService(listener, url, root);
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }

                var (status, body) = Answer(context.Request);
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        private (int, string) Answer(HttpListenerRequest request) =>
            (request.HttpMethod, request.Url!.AbsolutePath) switch
            {
                ("GET", "/api/quests") when request.QueryString["includeClosed"] != "true" => (200, new JsonArray(new JsonObject
                {
                    ["id"] = "q1", ["from"] = "game", ["to"] = "engine", ["title"] = "Expose a budget",
                    ["body"] = "The game needs one.", ["status"] = "Open",
                }).ToJsonString()),
                ("GET", "/api/registry") => (200, new JsonArray(new JsonObject
                {
                    ["repository"] = "engine", ["adopted"] = true, ["registered"] = true,
                    ["root"] = _root, ["workspace"] = "default",
                }).ToJsonString()),
                ("POST", "/api/sessions") => (409, """{"error":"refused by the stand-in"}"""),
                ("GET", _) => (200, "[]"),
                _ => (404, """{"error":"not here"}"""),
            };

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            try { await _serving; } catch (ObjectDisposedException) { }
        }
    }
}
