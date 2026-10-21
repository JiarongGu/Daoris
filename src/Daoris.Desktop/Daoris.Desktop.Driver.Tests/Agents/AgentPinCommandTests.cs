using System.Net;
using System.Security.Cryptography;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris-driver agent pin|update</c> (PUSH1f2e, the alone-path design §1.4): a maker's release downloaded holding nothing, staged
/// under the run's own name, and published under the home's lease, the move and the pin's write, waited for and stopped by Ctrl+C; a
/// pinned update moving the pin as a pin. Codex's channel serves, its bytes from a transport the test holds: no test here touches the
/// network, and none starts a real agent's install or update. Today's words for the refusals are <c>AgentsTextTests</c>' rows.
/// </summary>
public sealed class AgentPinCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agent-pin", Guid.NewGuid().ToString("N")[..8]);

    private readonly StringWriter _out = new();

    private readonly StringWriter _error = new();

    public AgentPinCommandTests() => Directory.CreateDirectory(Home);

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "home");

    private string Wiring => DaorisHome.Entry(Home, HomeEntry.Harnesses);

    private static string Exe => OperatingSystem.IsWindows() ? ".exe" : "";

    private AgentsWorld World(
        HttpMessageHandler? transport = null, Action? waiting = null, string? marker = null, IReadOnlyList<string>? npm = null) => new()
    {
        Instance = new InstanceWorld
        {
            Home = Home,
            Environment = variable => variable switch
            {
                DriverChoicesCommand.SessionVariable => marker,
                DaorisHome.Variable => Home,
                _ => null,
            },
            Out = _out,
            Error = _error,
            Verb = "daoris",
        },
        Folder = _root,
        UserHome = Path.Combine(_root, "user"),
        Adapters = _ => AdapterSet.Built(),
        Waiting = waiting,
        Transport = transport,
        Npm = npm ?? ["npm"],
    };

    private string Where(string version) => HarnessSettings.ManagedHome(Home, "codex", version);

    /// <summary>A version laid where Daoris keeps it, as a verified install leaves it.</summary>
    private void Installed(string version)
    {
        Directory.CreateDirectory(Path.Combine(Where(version), "bin"));
        File.WriteAllText(Path.Combine(Where(version), "bin", $"codex{Exe}"), "");
    }

    [Fact]
    public async Task A_pin_from_the_channel_is_downloaded_published_and_written_and_the_home_let_go()
    {
        var transport = new Served(CodexRelease("0.156.1"));

        var exit = await AgentsCommand.RunAsync(["pin", "codex", "0.156.1"], World(transport), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal("", _error.ToString());
        Assert.Equal("0.156.1", HarnessSettings.Load(Wiring).Versions["codex"]);
        Assert.Equal(Path.Combine(Where("0.156.1"), "bin", $"codex{Exe}"), HarnessSettings.ManagedBinary(Home, "codex", "0.156.1", ["codex"]));
        Assert.Equal([Where("0.156.1")], Directory.EnumerateDirectories(Path.GetDirectoryName(Where("0.156.1"))!));
        var said = Lines(_out);
        Assert.Equal("daoris: installing Codex 0.156.1 from OpenAI's own release channel, into a directory Daoris owns.", said[0]);
        Assert.Contains("  fetching Codex 0.156.1's release metadata", said);
        Assert.Equal("daoris: `codex` runs at 0.156.1 on this machine.", said[^3]);
        Assert.Null(HomeLease.HeldBy(Home));
    }

    [Fact]
    public async Task A_pin_s_publication_waits_on_a_held_lease_and_takes_it_when_let_go()
    {
        var held = HomeLease.TryAcquire(Home, LeaseKind.Verb, out _, "daoris-driver trees land");
        Assert.NotNull(held);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = AgentsCommand.RunAsync(["pin", "codex", "0.156.1", "--workspace", "Aurora"],
            World(new Served(CodexRelease("0.156.1")), waiting.SetResult), CancellationToken.None);

        await waiting.Task.WaitAsync(Poll.Patience);
        Assert.False(run.IsCompleted);
        // Downloaded and verified, and not yet the version: nothing resolves it, and no pin names it.
        Assert.False(Directory.Exists(Where("0.156.1")));
        Assert.False(File.Exists(Wiring));

        held.Dispose();
        Assert.Equal(0, await run.WaitAsync(Poll.Patience));
        Assert.Equal("0.156.1", HarnessSettings.Load(Wiring).WorkspaceVersions["aurora"]["codex"]);
        Assert.NotNull(HarnessSettings.ManagedBinary(Home, "codex", "0.156.1", ["codex"]));
        Assert.Contains("daoris: `codex` runs at 0.156.1 for the `Aurora` workspace on this machine.", Lines(_out));
    }

    [Fact]
    public async Task A_Ctrl_C_while_the_pin_waits_removes_the_download_and_pins_nothing()
    {
        using var held = HomeLease.TryAcquire(Home, LeaseKind.Verb, out _, "daoris-driver trees land");
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource();
        var run = AgentsCommand.RunAsync(["pin", "codex", "0.156.1"], World(new Served(CodexRelease("0.156.1")), waiting.SetResult), stop.Token);

        await waiting.Task.WaitAsync(Poll.Patience);
        await stop.CancelAsync();

        Assert.Equal(2, await run.WaitAsync(Poll.Patience));
        Assert.EndsWith("daoris: stopped at your Ctrl+C — the verified download was removed, and nothing was pinned.", _error.ToString().Trim());
        Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(Where("0.156.1"))!));
        Assert.False(File.Exists(Wiring));
    }

    [Fact]
    public async Task A_download_the_channel_refuses_pins_nothing_and_leaves_nothing()
    {
        var files = CodexRelease("0.156.1");
        files[Codex("0.156.1", CodexReleases.Package(CodexReleaseDownloads.Current()))] = Encoding.UTF8.GetBytes("not the package");
        Installed("0.156.0");
        HarnessSettings.Pin(Wiring, Home, "codex", "0.156.0");

        var exit = await AgentsCommand.RunAsync(["pin", "codex", "0.156.1"], World(new Served(files)), CancellationToken.None);

        Assert.Equal(2, exit);
        var refused = Lines(_error);
        Assert.StartsWith("daoris: the package downloaded from ", refused[0]);
        Assert.Equal(
            "  Nothing was pinned: a pin naming a version that is not there would run a different tool than the one you asked for.", refused[1]);
        Assert.Equal("0.156.0", HarnessSettings.Load(Wiring).Versions["codex"]);
        Assert.Equal([Where("0.156.0")], Directory.EnumerateDirectories(Path.GetDirectoryName(Where("0.156.1"))!));
    }

    [Fact]
    public async Task A_pinned_update_moves_the_pin_to_the_channel_s_newest_release()
    {
        Installed("0.156.0");
        HarnessSettings.Pin(Wiring, Home, "codex", "0.156.0");
        var files = CodexRelease("0.156.1");
        files[CodexReleases.Latest] = Encoding.UTF8.GetBytes("""{"tag_name":"rust-v0.156.1"}""");

        var exit = await AgentsCommand.RunAsync(["update", "codex"], World(new Served(files)), CancellationToken.None);

        Assert.Equal(0, exit);
        var said = Lines(_out);
        Assert.Equal("daoris: `codex` is pinned at 0.156.0. Asking OpenAI's own release channel which release is newest.", said[0]);
        Assert.Equal("daoris: `codex` 0.156.0 → 0.156.1", said[1]);
        Assert.Equal("0.156.1", HarnessSettings.Load(Wiring).Versions["codex"]);
        Assert.NotNull(HarnessSettings.ManagedBinary(Home, "codex", "0.156.1", ["codex"]));
    }

    [Theory]
    [InlineData("0.156.1", "daoris: `codex` 0.156.1 is already the newest release — nothing was fetched, and the pin stays.")]
    [InlineData("0.157.0", "daoris: `codex` is pinned at 0.157.0, newer than the newest release (0.156.1) — nothing was")]
    public async Task A_pinned_update_that_would_not_move_forward_fetches_nothing(string pinned, string line)
    {
        Installed(pinned);
        HarnessSettings.Pin(Wiring, Home, "codex", pinned, "aurora");
        var transport = new Served(new Dictionary<string, byte[]>
        {
            [CodexReleases.Latest] = Encoding.UTF8.GetBytes("""{"tag_name":"rust-v0.156.1"}"""),
        });

        var exit = await AgentsCommand.RunAsync(["update", "codex", "--workspace", "aurora"], World(transport), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal([CodexReleases.Latest], transport.Asked);
        Assert.Equal(line, Lines(_out)[1]);
        Assert.Equal(pinned, HarnessSettings.Load(Wiring).WorkspaceVersions["aurora"]["codex"]);
        Assert.StartsWith($"daoris: `codex` is pinned at {pinned} for the `aurora` workspace.", Lines(_out)[0]);
    }

    [Fact]
    public async Task A_channel_that_names_no_newest_release_is_refused_and_the_pin_stays()
    {
        Installed("0.156.0");
        HarnessSettings.Pin(Wiring, Home, "codex", "0.156.0");

        var exit = await AgentsCommand.RunAsync(["update", "codex"], World(new Served([])), CancellationToken.None);

        Assert.Equal(2, exit);
        Assert.Equal(
            $"daoris: nothing answered at {CodexReleases.Latest}, where the channel names its newest release — nothing was fetched or pinned. "
            + "The pin stays at 0.156.0.", _error.ToString().Trim());
        Assert.Equal("0.156.0", HarnessSettings.Load(Wiring).Versions["codex"]);
    }

    [Theory]
    [InlineData("pin codex 0.156.1")]
    [InlineData("update codex")]
    public async Task A_marked_terminal_s_pin_or_update_is_refused_before_anything_is_fetched(string words)
    {
        var transport = new Served(CodexRelease("0.156.1"));

        var exit = await AgentsCommand.RunAsync(words.Split(' '), World(transport, marker: "s1a2b3c4"), CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Equal($"daoris: {DriverChoicesCommand.SessionsTerminal}", _error.ToString().Trim());
        Assert.Empty(transport.Asked);
        Assert.False(File.Exists(Wiring));
    }

    /// <summary>
    /// A package pin's npm, a stand-in that fails: started at this terminal as the agent's own command (§1.1's second hop), its failure
    /// pins nothing. A real process, so the suite's Process half runs it.
    /// </summary>
    [Trait(Category.Name, Category.Process)]
    public sealed class Npm : IDisposable
    {
        private readonly AgentPinCommandTests _held = new();

        public void Dispose() => _held.Dispose();

        [Fact]
        public async Task A_package_pin_whose_npm_fails_pins_nothing()
        {
            string[] npm = OperatingSystem.IsWindows()
                ? [Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/d", "/c", "exit 3", "&&"]
                : ["/bin/sh", "-c", "exit 3", "npm"];

            var exit = await AgentsCommand.RunAsync(["pin", "dsh", "1.2.3"], _held.World(npm: npm), CancellationToken.None);

            Assert.Equal(2, exit);
            var said = Lines(_held._out);
            Assert.Equal("daoris: installing `@deepseek-ai/dsh@1.2.3` into a directory Daoris owns.", said[0]);
            Assert.Equal(
                "  Nothing was pinned: a pin naming a version that is not there would run a different tool than the one you asked for.", said[^1]);
            Assert.False(File.Exists(_held.Wiring));
        }
    }

    private static string[] Lines(StringWriter writer) =>
        writer.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');

    private static string Codex(string version, string file) => $"{CodexReleases.Base}/{version}/{file}";

    /// <summary>A Codex release for this machine, in the vendor's metadata shape, over a package built here.</summary>
    private static Dictionary<string, byte[]> CodexRelease(string version)
    {
        var target = CodexReleaseDownloads.Current();
        var package = ToolInstallTests.Archive.Build("tar.gz", $"codex-package.json={{}}; bin/codex{Exe}=codex", "");
        var name = CodexReleases.Package(target);
        var sums = Encoding.UTF8.GetBytes($"{Sha(package)}  {name}\n");
        var metadata = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        {
            tag_name = $"rust-v{version}",
            assets = new object[]
            {
                new { name, digest = $"sha256:{Sha(package)}", browser_download_url = Codex(version, name) },
                new { name = CodexReleases.Sums, digest = $"sha256:{Sha(sums)}", browser_download_url = Codex(version, CodexReleases.Sums) },
            },
        });
        return new Dictionary<string, byte[]>
        {
            [Codex(version, "release.json")] = metadata,
            [Codex(version, name)] = package,
            [Codex(version, CodexReleases.Sums)] = sums,
        };
    }

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>A transport over a table of URLs, and the URLs it was asked for, in order.</summary>
    private sealed class Served(Dictionary<string, byte[]> files) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            lock (Asked) Asked.Add(url);
            return Task.FromResult(files.TryGetValue(url, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
