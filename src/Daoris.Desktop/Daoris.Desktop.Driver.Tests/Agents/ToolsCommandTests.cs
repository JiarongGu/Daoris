using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris-driver tool</c> (PUSH1f2c, D194, the alone-path design §1.2, §1.4, §2): a person's press acting alone is refused from a
/// session's terminal, an open look is not; a download holds nothing while it fetches and publishes under the home's lease, waited for
/// and never refused, said once its hold has lasted thirty seconds, taken the moment it is let go, run beside an instance that started
/// meanwhile, and stopped by the person's Ctrl+C with its staging gone. Today's words are <see cref="ToolsTextTests"/>'.
/// </summary>
public sealed class ToolsCommandTests : IDisposable
{
    private const string Location = "https://lists.example/resources.json";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tools-command", Guid.NewGuid().ToString("N")[..8]);

    private readonly StringWriter _out = new();

    private readonly StringWriter _error = new();

    public ToolsCommandTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Application);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Home => Path.Combine(_root, "home");

    private string Application => Path.Combine(_root, "application");

    private string ToolsFile => DaorisHome.Entry(Home, HomeEntry.Tools);

    private static readonly byte[] Bytes = ToolInstallTests.Archive.Build("zip", "bin/gh.exe=gh", "");

    /// <summary>A host that answers the archive, once the test lets it where it holds.</summary>
    private sealed class Host : HttpMessageHandler
    {
        public TaskCompletionSource Asked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Answer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Host(bool hold = false)
        {
            if (!hold) Answer.SetResult();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.TrySetResult();
            await Answer.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(Bytes) };
        }
    }

    /// <summary>A home whose one location's copy offers GitHub CLI 2.62.0 for win-x64, at the archive the host answers.</summary>
    private void Offered()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ToolsFile)!);
        File.WriteAllText(ToolsFile, $$"""{"locations":["{{Location}}"]}""");
        var copy = ToolResources.LocationCopy(Home, Location);
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.WriteAllText(copy, new JsonObject
        {
            ["schema"] = 1,
            ["tools"] = new JsonObject
            {
                ["gh"] = new JsonObject
                {
                    ["versions"] = new JsonObject
                    {
                        ["2.62.0"] = new JsonObject
                        {
                            ["files"] = new JsonObject
                            {
                                ["win-x64"] = new JsonObject
                                {
                                    ["url"] = "https://maker.example/gh-2.62.0.zip",
                                    ["sha256"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Bytes)),
                                    ["size"] = Bytes.Length,
                                    ["archive"] = "zip",
                                    ["exe"] = "bin/gh.exe",
                                },
                            },
                        },
                    },
                },
            },
        }.ToJsonString());
    }

    private ToolsWorld World(HttpMessageHandler? host = null, string? marker = null, Action? waiting = null, Func<DateTimeOffset>? now = null) => new()
    {
        Instance = new InstanceWorld
        {
            Home = Home,
            Environment = variable => variable == DriverChoicesCommand.SessionVariable ? marker : variable == DaorisHome.Variable ? Home : null,
            BaseDirectory = Application,
            Out = _out,
            Error = _error,
            Verb = "daoris",
        },
        WorkingDirectory = Home,
        SearchPath = "",
        Platform = "win-x64",
        Transport = host ?? new Host(),
        AskVersion = (_, _, _) => Task.FromResult<(string?, string?)>(("2.51.0", null)),
        Waiting = waiting,
        Now = now ?? (() => DateTimeOffset.UtcNow),
    };

    private IReadOnlyList<string> Stagings()
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.ToolVersions, "gh");
        return Directory.Exists(folder) ? [.. Directory.EnumerateDirectories(folder, "*.part*").Select(Path.GetFileName).OfType<string>()] : [];
    }

    [Theory]
    [InlineData("use git system")]
    [InlineData("locations add https://lists.example/resources.json")]
    [InlineData("delete gh 2.62.0")]
    [InlineData("use gh managed")]
    [InlineData("download gh 2.62.0")]
    [InlineData("update gh")]
    public async Task A_person_s_press_acting_alone_is_refused_from_a_session_s_terminal(string words)
    {
        Offered();
        var before = File.ReadAllText(ToolsFile);

        var exit = await ToolsCommand.RunAsync(words.Split(' '), World(marker: "s-1"), CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Equal($"daoris: {DriverChoicesCommand.SessionsTerminal}", _error.ToString().Trim());
        Assert.Equal(before, File.ReadAllText(ToolsFile));
        Assert.Empty(ToolInstall.Downloaded(Home, "gh"));
    }

    [Fact]
    public async Task An_open_look_and_a_read_are_not_refused_from_a_session_s_terminal()
    {
        Offered();
        Assert.Equal(1, await ToolsCommand.RunAsync(["look"], World(marker: "s-1"), CancellationToken.None));
        Assert.StartsWith("daoris: looked at 1 location:", _out.ToString(), StringComparison.Ordinal);
        Assert.Equal("", _error.ToString());
        Assert.Equal(0, await ToolsCommand.RunAsync(["list"], World(marker: "s-1"), CancellationToken.None));
        Assert.Equal("", _error.ToString());
    }

    [Fact]
    public async Task A_publication_waits_on_a_held_lease_says_so_once_and_takes_it_when_let_go()
    {
        Offered();
        var held = HomeLease.TryAcquire(Home, LeaseKind.Verb, out _, "daoris-driver trees land");
        Assert.NotNull(held);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // The clock moves past the thirty seconds once the wait began, so the hold is said; it is said once however long it lasts.
        var started = DateTimeOffset.UtcNow;
        var looks = 0;
        var run = ToolsCommand.RunAsync(["use", "gh", "managed"], World(waiting: waiting.SetResult, now: () => started.AddSeconds(31 * looks++)),
            CancellationToken.None);

        await waiting.Task.WaitAsync(TimeSpan.FromMinutes(2));
        Assert.False(run.IsCompleted);
        Assert.Empty(ToolInstall.Downloaded(Home, "gh"));
        Assert.Equal([$"2.62.0.part-{Environment.ProcessId}"], Stagings());
        Assert.DoesNotContain("managed", File.ReadAllText(ToolsFile), StringComparison.Ordinal);

        held.Dispose();
        Assert.Equal(0, await run.WaitAsync(TimeSpan.FromMinutes(2)));
        Assert.Equal(["2.62.0"], ToolInstall.Downloaded(Home, "gh"));
        Assert.Empty(Stagings());
        Assert.Equal("2.62.0", Tools.Read(Home).Entries["gh"].Version);
        var told = _error.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["daoris: waiting for a terminal's `daoris-driver trees land`"], told.Select(line => line[..line.IndexOf(" (pid", StringComparison.Ordinal)]));
        Assert.False(File.Exists(HomeLease.PathIn(Home)), "the publication let the home go");
    }

    [Fact]
    public async Task A_download_holds_nothing_while_it_fetches()
    {
        Offered();
        var host = new Host(hold: true);
        var run = ToolsCommand.RunAsync(["download", "gh", "2.62.0"], World(host), CancellationToken.None);

        await host.Asked.Task.WaitAsync(TimeSpan.FromMinutes(2));
        Assert.Null(HomeLease.HeldBy(Home));
        host.Answer.SetResult();

        Assert.Equal(0, await run.WaitAsync(TimeSpan.FromMinutes(2)));
        Assert.Contains("GitHub CLI 2.62.0 is downloaded and verified — nothing switches.", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_publication_beside_an_instance_that_started_meanwhile_is_made_holding_nothing()
    {
        Offered();
        using var desktop = HomeLease.TryAcquire(Home, LeaseKind.Desktop, out _);
        Assert.NotNull(desktop);
        var lease = File.ReadAllText(HomeLease.PathIn(Home));

        Assert.Equal(0, await ToolsCommand.RunAsync(["use", "gh", "managed"], World(), CancellationToken.None));

        Assert.Equal(["2.62.0"], ToolInstall.Downloaded(Home, "gh"));
        Assert.Equal("2.62.0", Tools.Read(Home).Entries["gh"].Version);
        Assert.Equal(lease, File.ReadAllText(HomeLease.PathIn(Home)));
        Assert.Equal("", _error.ToString());
    }

    [Fact]
    public async Task A_Ctrl_C_while_the_publication_waits_takes_the_staging_and_switches_nothing()
    {
        Offered();
        using var held = HomeLease.TryAcquire(Home, LeaseKind.Verb, out _, "daoris-driver trees land");
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource();
        var run = ToolsCommand.RunAsync(["use", "gh", "managed"], World(waiting: waiting.SetResult), stop.Token);

        await waiting.Task.WaitAsync(TimeSpan.FromMinutes(2));
        await stop.CancelAsync();

        Assert.Equal(2, await run.WaitAsync(TimeSpan.FromMinutes(2)));
        Assert.Empty(Stagings());
        Assert.Empty(ToolInstall.Downloaded(Home, "gh"));
        Assert.Null(Tools.Read(Home).Entries["gh"].Version);
        Assert.Equal("daoris: stopped at your Ctrl+C — nothing was kept, and nothing switched.", _error.ToString().Trim());
    }

    [Fact]
    public async Task A_Ctrl_C_while_it_downloads_takes_the_staging()
    {
        Offered();
        var host = new Host(hold: true);
        using var stop = new CancellationTokenSource();
        var run = ToolsCommand.RunAsync(["download", "gh", "2.62.0"], World(host), stop.Token);

        await host.Asked.Task.WaitAsync(TimeSpan.FromMinutes(2));
        Assert.Equal([$"2.62.0.part-{Environment.ProcessId}"], Stagings());
        await stop.CancelAsync();

        Assert.Equal(2, await run.WaitAsync(TimeSpan.FromMinutes(2)));
        Assert.Empty(Stagings());
        Assert.Empty(ToolInstall.Downloaded(Home, "gh"));
    }

    /// <summary>The short changes hold the home from the top; a read, the usage and the long verbs hold none (D194 point 2).</summary>
    [Theory]
    [InlineData("use git system", true)]
    [InlineData("use git file C:/bin/git.exe", true)]
    [InlineData("delete gh 2.62.0", true)]
    [InlineData("locations add https://lists.example/r.json", true)]
    [InlineData("locations remove https://lists.example/r.json", true)]
    [InlineData("look", true)]
    [InlineData("list", false)]
    [InlineData("path git", false)]
    [InlineData("locations", false)]
    [InlineData("use gh managed", false)]
    [InlineData("download gh 2.62.0", false)]
    [InlineData("update gh", false)]
    [InlineData("use git system --help", false)]
    [InlineData("frobnicate", false)]
    public void A_short_change_is_read_off_the_words(string words, bool changes) =>
        Assert.Equal(changes, ToolsCommand.Changes(words.Split(' ')));
}
