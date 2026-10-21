using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris-driver agent</c> (PUSH1f2d, D194, the alone-path design §1.2, §2): a person's press acting alone is refused from a session's
/// terminal, nothing written, a read is not; the rules and their proposals keep the door on <c>DAORIS_HOME</c> whatever
/// <c>DAORIS_HARNESS_CONFIG</c> names, so <c>rules accept</c> is never alone beside a running Daoris; an account's settings and a trust on
/// a named wiring file's home are written alone, nothing sent; and the verb table says which home each verb writes. Today's words are
/// <see cref="AgentsTextTests"/>'.
/// </summary>
public sealed class AgentsCommandTests : IDisposable
{
    private const string Marker = "s1a2b3c4";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agents-command", Guid.NewGuid().ToString("N")[..8]);

    private readonly StringWriter _out = new();

    private readonly StringWriter _error = new();

    public AgentsCommandTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Folder);
    }

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "home");

    private string Folder => Path.Combine(_root, "work", "engine");

    private string Elsewhere => Path.Combine(_root, "other", "data", "harnesses.json");

    /// <summary>A proposal exactly as the connector writes it, waiting for the person.</summary>
    private static string Propose(string home, string id)
    {
        var path = Path.Combine(DaorisHome.Entry(home, HomeEntry.RuleProposals), $"{id}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""
            {
              "id": "{{id}}",
              "proposed": "2026-09-24T10:00:00.0000000+00:00",
              "by": { "session": "s1a2b3c4", "ask": null, "folder": "C:/somewhere/engine" },
              "change": { "action": "add", "scope": "machine", "name": null, "list": "allow", "rule": "WebFetch", "default": null, "on": null },
              "why": "The tests need it.",
              "state": "waiting"
            }
            """);
        return path;
    }

    private static void Account(string home, string name = "work") =>
        Directory.CreateDirectory(HarnessSettings.ProfileHome(home, "claude-code", name));

    private AgentsWorld World(string? marker = null, string? wiring = null) => new()
    {
        Instance = new InstanceWorld
        {
            Home = Home,
            Environment = variable => variable switch
            {
                DriverChoicesCommand.SessionVariable => marker,
                DaorisHome.Variable => Home,
                HarnessSettings.PathVariable => wiring,
                _ => null,
            },
            Out = _out,
            Error = _error,
            Verb = "daoris",
        },
        Folder = _root,
        UserHome = Path.Combine(_root, "user"),
        Adapters = _ => AdapterSet.Built(),
    };

    /// <summary>
    /// A person's press acting alone from a session's terminal is refused (D194 point 5, the alone-path design §2): <c>rules accept</c>
    /// among them, the one way a widening an agent proposed ever applies, so the proposal waits and no rule is written.
    /// </summary>
    [Fact]
    public async Task A_marked_terminal_s_rules_accept_is_refused_and_the_proposal_waits()
    {
        var proposal = Propose(Home, "p0000007");
        var before = File.ReadAllText(proposal);

        var exit = await AgentsCommand.RunAsync(["rules", "accept", "p0000007"], World(Marker), CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Equal($"daoris: {DriverChoicesCommand.SessionsTerminal}", _error.ToString().Trim());
        Assert.Equal(before, File.ReadAllText(proposal));
        Assert.False(File.Exists(PermissionRules.PathOf(Home)));
    }

    [Theory]
    [InlineData("rules allow WebFetch")]
    [InlineData("rules default no-push off")]
    [InlineData("rules decline p0000007")]
    [InlineData("settings claude-code --account work model opus")]
    [InlineData("trust claude-code <folder> --yes")]
    public async Task A_marked_terminal_s_change_acting_alone_is_refused_and_nothing_is_written(string words)
    {
        Propose(Home, "p0000007");
        Account(Home);
        var written = Snapshot();

        var exit = await AgentsCommand.RunAsync(words.Replace("<folder>", Folder).Split(' '), World(Marker), CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Contains(DriverChoicesCommand.SessionsTerminal, _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(written, Snapshot());
    }

    /// <summary>A read is no press: the marker refuses nothing it says.</summary>
    [Theory]
    [InlineData("rules")]
    [InlineData("rules proposals")]
    [InlineData("settings claude-code --account work")]
    [InlineData("trust claude-code <folder> --profile work --dry-run")]
    public async Task A_read_is_said_from_a_marked_terminal(string words)
    {
        Propose(Home, "p0000007");
        Account(Home);

        var exit = await AgentsCommand.RunAsync(words.Replace("<folder>", Folder).Split(' '), World(Marker), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal("", _error.ToString());
        Assert.NotEqual("", _out.ToString());
    }

    /// <summary>
    /// <c>rules accept</c> with <c>DAORIS_HARNESS_CONFIG</c> set takes the door on <c>DAORIS_HOME</c> (the alone-path design §1.2): the
    /// proposals are that home's whatever the variable names, so beside the running Daoris the press is its route, confirmed in its
    /// window, and this process writes nothing. Naming a wiring file no longer makes it alone.
    /// </summary>
    [Fact]
    public async Task Rules_accept_with_a_named_wiring_file_takes_the_door_on_the_home_and_never_acts_alone_beside_an_instance()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var running = new InstanceTestHome();
        var standIn = running.Serve();
        standIn.Confirming = true;
        var proposal = Propose(running.Home, "p0000007");
        var before = File.ReadAllText(proposal);
        var world = Beside(running, wiring: Elsewhere);

        var exit = await AgentsCommand.RunAsync(["rules", "accept", "p0000007"], world, CancellationToken.None);

        Assert.Equal(0, exit);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("RULE_PROPOSAL", press.GetProperty("type").GetString());
        Assert.Equal("p0000007", press.GetProperty("payload").GetProperty("id").GetString());
        Assert.True(press.GetProperty("payload").GetProperty("accept").GetBoolean());
        Assert.StartsWith("daoris: accepted #p0000007 — allow `WebFetch` for every session on this machine is now in the rules.",
            running.Out.ToString(), StringComparison.Ordinal);
        Assert.Contains("daoris: waiting for you to confirm this in Daoris's window…", running.Error.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(proposal));
        Assert.False(File.Exists(PermissionRules.PathOf(running.Home)));
    }

    /// <summary>
    /// A wiring file <c>DAORIS_HARNESS_CONFIG</c> names is no instance's (D194 point 2): an account's settings and a trust on its home are
    /// written alone beside the running Daoris, and nothing is sent.
    /// </summary>
    [Theory]
    [InlineData("settings claude-code --account work model opus")]
    [InlineData("trust claude-code <folder> --profile work --yes")]
    public async Task A_setting_or_a_trust_on_a_named_wiring_file_is_written_alone_and_nothing_is_sent(string words)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var running = new InstanceTestHome();
        var standIn = running.Serve();
        var other = Path.GetDirectoryName(Path.GetDirectoryName(Elsewhere))!;
        Account(other);

        var exit = await AgentsCommand.RunAsync(words.Replace("<folder>", Folder).Split(' '), Beside(running, wiring: Elsewhere), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Empty(standIn.Presses);
        Assert.Equal(0, standIn.Connections);
        var account = HarnessSettings.ProfileHome(other, "claude-code", "work");
        Assert.True(File.Exists(Path.Combine(account, words.StartsWith("settings", StringComparison.Ordinal) ? AgentSettings.FileName : ClaudeTrust.FileName)),
            running.Error.ToString());
    }

    /// <summary>A trust no route takes is written here beside the running Daoris too (D171's PUSH1f note, kept by D194 point 5), nothing sent.</summary>
    [Fact]
    public async Task Beside_the_running_Daoris_a_trust_is_written_here_and_nothing_is_sent()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var running = new InstanceTestHome();
        var standIn = running.Serve();
        Account(running.Home);

        var exit = await AgentsCommand.RunAsync(["trust", "claude-code", Folder, "--profile", "work", "--yes"], Beside(running), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Empty(standIn.Presses);
        Assert.True(ClaudeTrust.Accepted(Path.Combine(HarnessSettings.ProfileHome(running.Home, "claude-code", "work"), ClaudeTrust.FileName), Folder));
    }

    [Fact]
    public async Task Asked_for_its_usage_it_says_it_and_a_verb_it_lacks_is_refused_with_it()
    {
        Assert.Equal(0, await AgentsCommand.RunAsync(["--help"], World(), CancellationToken.None));
        Assert.Contains(AgentsCommand.Usage, _out.ToString(), StringComparison.Ordinal);

        Assert.Equal(2, await AgentsCommand.RunAsync(["login", "claude-code"], World(), CancellationToken.None));
        Assert.StartsWith("daoris-driver agent: `login` is not a verb of this program's.", _error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The family's verb table says which home each verb writes (the alone-path design §1.2), and the command and the lease read it: a
    /// named wiring file isolates exactly the rows that are the wiring's.
    /// </summary>
    [Fact]
    public void The_verb_table_says_which_home_each_verb_writes_and_a_named_wiring_file_isolates_those()
    {
        static string? Named(string name) => name == HarnessSettings.PathVariable ? "X:/elsewhere/harnesses.json" : null;
        Assert.Equal(AgentsCommand.Verbs.Order(StringComparer.Ordinal),
            AgentRoutes.Verbs.Select(row => row.Verb.Split(' ')[1]).Distinct().Order(StringComparer.Ordinal));
        foreach (var (verb, _, _, home) in AgentRoutes.Verbs)
        {
            string[] words = [verb.Split(' ')[1]];
            Assert.Equal(home == AgentHome.Wiring, AgentsCommand.Isolated(words, Named));
            Assert.False(AgentsCommand.Isolated(words, _ => null));
        }

        Assert.Equal(AgentHome.Daoris, AgentRoutes.HomeOf("rules"));
        Assert.Equal(AgentHome.Wiring, AgentRoutes.HomeOf("settings"));
        Assert.Equal(AgentHome.Wiring, AgentRoutes.HomeOf("trust"));
    }

    private AgentsWorld Beside(InstanceTestHome running, string? wiring = null)
    {
        var instance = running.World("daoris");
        return new AgentsWorld
        {
            Instance = new InstanceWorld
            {
                Home = running.Home,
                Environment = variable => variable == HarnessSettings.PathVariable ? wiring : variable == DaorisHome.Variable ? running.Home : null,
                BaseDirectory = instance.BaseDirectory,
                Status = instance.Status,
                Out = running.Out,
                Error = running.Error,
                Verb = "daoris",
                Answer = instance.Answer,
                Wait = instance.Wait,
            },
            Folder = _root,
            UserHome = Path.Combine(_root, "user"),
            Adapters = _ => AdapterSet.Built(),
        };
    }

    /// <summary>Every file under the root and what it holds.</summary>
    private Dictionary<string, string> Snapshot() =>
        Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllText, StringComparer.Ordinal);
}
