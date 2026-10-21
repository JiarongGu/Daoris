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

    /// <summary>
    /// A refusal's command names a workspace with a space in double quotes, and an account a shell cannot take by a placeholder
    /// (<see cref="ShellWord"/>): the CLI's <c>shellword.test.ts</c> held these through its TypeScript verbs until PUSH1f2e handed them here.
    /// </summary>
    [Fact]
    public async Task A_refusals_command_names_a_workspace_with_a_space_in_double_quotes_and_an_account_it_cannot_spell_by_a_placeholder()
    {
        foreach (var name in new[] { "account-1", "account-2", "R&D" }) Account(Home, name);
        Assert.Equal(0, await AgentsCommand.RunAsync(
            ["profile", "order", "claude-code", "account-1", "--workspace", "my team"], World(), CancellationToken.None));

        Assert.Contains("`daoris agent profile order claude-code account-1 account-2 --workspace \"my team\"` adds it",
            await Refused("profile", "default", "claude-code", "account-2", "--workspace", "my team"));
        Assert.Contains("`daoris agent profile order claude-code <account> account-1` sets it",
            await Refused("profile", "order", "claude-code", "R&D", "account-1", "--clear"));
        Assert.Contains("`daoris agent profile ready claude-code <account>` is that account",
            await Refused("profile", "ready", "claude-code", "R&D", "--own"));
        Assert.Contains("`daoris agent profile rename claude-code <account> work`; naming it `R&D` gives it none",
            await Refused("profile", "rename", "claude-code", "R&D"));
        Assert.Contains("`daoris agent profile join claude-code <account> work`", await Refused("profile", "join", "claude-code", "R&D"));

        async Task<string> Refused(params string[] words)
        {
            _out.GetStringBuilder().Clear();
            _error.GetStringBuilder().Clear();
            Assert.NotEqual(0, await AgentsCommand.RunAsync(words, World(), CancellationToken.None));
            return _out.ToString() + _error;
        }
    }

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
    [InlineData("unpin claude-code")]
    [InlineData("unpin claude-code --workspace work")]
    [InlineData("profile remove claude-code work")]
    [InlineData("profile default claude-code work")]
    [InlineData("profile order claude-code work")]
    [InlineData("profile ready claude-code --own")]
    [InlineData("profile rename claude-code work seat")]
    [InlineData("profile join claude-code work --machine")]
    public async Task A_marked_terminal_s_change_acting_alone_is_refused_and_nothing_is_written(string words)
    {
        Propose(Home, "p0000007");
        Account(Home);
        var written = Snapshot();

        var exit = await AgentsCommand.RunAsync(words.Replace("<folder>", Folder).Split(' '), World(Marker), CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Contains(DriverChoicesCommand.SessionsTerminal, _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(written, Snapshot());
        Assert.True(Directory.Exists(HarnessSettings.ProfileHome(Home, "claude-code", "work")));
    }

    /// <summary>A read is no press: the marker refuses nothing it says.</summary>
    [Theory]
    [InlineData("rules")]
    [InlineData("rules proposals")]
    [InlineData("settings claude-code --account work")]
    [InlineData("trust claude-code <folder> --profile work --dry-run")]
    [InlineData("profile")]
    [InlineData("profile list")]
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

    /// <summary>
    /// An account's change beside the running Daoris is the Accounts place's own press (PUSH1f2e2): <c>profile order</c> sends
    /// <c>ACCOUNT_USE</c>'s order, confirmed in its window, and this process writes nothing; a workspace's unpin, which no route takes, is
    /// written here and nothing is sent.
    /// </summary>
    [Fact]
    public async Task Beside_the_running_Daoris_an_order_is_its_route_and_a_workspace_s_unpin_is_written_here()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var running = new InstanceTestHome();
        var standIn = running.Serve();
        standIn.Confirming = true;
        Account(running.Home);
        Account(running.Home, "seat");
        var wiring = DaorisHome.Entry(running.Home, HomeEntry.Harnesses);

        var exit = await AgentsCommand.RunAsync(["profile", "order", "claude-code-acp", "seat", "work", "--workspace", "aurora"], Beside(running),
            CancellationToken.None);

        Assert.Equal(0, exit);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("ACCOUNT_USE", press.GetProperty("type").GetString());
        var payload = press.GetProperty("payload");
        Assert.Equal("order", payload.GetProperty("action").GetString());
        Assert.Equal("claude-code", payload.GetProperty("harness").GetString());
        Assert.Equal(["seat", "work"], payload.GetProperty("accounts").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal("aurora", payload.GetProperty("workspace").GetString());
        Assert.Contains("daoris: in `aurora`, `claude-code`'s rotation is seat, then work.", running.Out.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(wiring));

        File.WriteAllText(wiring, """{"workspaceVersions":{"aurora":{"claude-code":"2.1.0"}},"versions":{"claude-code":"2.1.0"}}""");
        exit = await AgentsCommand.RunAsync(["unpin", "claude-code", "--workspace", "aurora"], Beside(running), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Single(standIn.Presses);
        var settings = HarnessSettings.Load(wiring);
        Assert.Empty(settings.WorkspaceVersions);
        Assert.Equal("2.1.0", settings.Versions["claude-code"]);
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

    /// <summary>
    /// <c>which --json</c> (PUSH1f2e, the alone-path design §4): the program a start resolves, as one object the real-run tools read: the
    /// binary on <c>PATH</c> with nothing pinned; a pin's executable, the workspace's own pin first; a pin with nothing installed
    /// <c>missing</c>, exit 0; and the command <c>driver.json</c> names, before any pin. Said from a session's terminal too: a read.
    /// </summary>
    [Fact]
    public async Task Which_answers_the_program_a_start_resolves_as_one_object()
    {
        async Task<System.Text.Json.JsonElement> Which(params string[] words)
        {
            _out.GetStringBuilder().Clear();
            Assert.Equal(0, await AgentsCommand.RunAsync(["which", .. words, "--json"], World(Marker), CancellationToken.None));
            Assert.Equal("", _error.ToString());
            return System.Text.Json.JsonDocument.Parse(_out.ToString()).RootElement;
        }

        string[] Run(System.Text.Json.JsonElement answer) => [.. answer.GetProperty("run").EnumerateArray().Select(each => each.GetString()!)];
        var binary = AdapterSet.Built().Resolve("claude-code-acp").Toolchain!.Binary;

        var path = await Which("claude-code-acp");
        Assert.Equal("claude-code-acp", path.GetProperty("agent").GetString());
        Assert.Equal(binary, Run(path));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, path.GetProperty("managed").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, path.GetProperty("pinned").ValueKind);
        Assert.False(path.GetProperty("missing").GetBoolean());

        var wiring = DaorisHome.Entry(Home, HomeEntry.Harnesses);
        Directory.CreateDirectory(Path.GetDirectoryName(wiring)!);
        File.WriteAllText(wiring, """{ "versions": { "claude-code-acp": "1.0.0" }, "workspaceVersions": { "work": { "claude-code-acp": "2.0.0" } } }""");
        var installed = Path.Combine(HarnessSettings.ManagedHome(Home, "claude-code-acp", "2.0.0"), "bin",
            OperatingSystem.IsWindows() ? binary[0] + ".exe" : binary[0]);
        Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
        File.WriteAllText(installed, "");

        var missing = await Which("claude-code-acp");
        Assert.True(missing.GetProperty("missing").GetBoolean());
        Assert.Equal("1.0.0", missing.GetProperty("pinned").GetString());
        Assert.Empty(Run(missing));

        var pinned = await Which("claude-code-acp", "--workspace", "work");
        Assert.False(pinned.GetProperty("missing").GetBoolean());
        Assert.Equal("2.0.0", pinned.GetProperty("pinned").GetString());
        Assert.Equal(installed, pinned.GetProperty("managed").GetString());
        Assert.Equal(installed, Run(pinned)[0]);

        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.DriverConfig), """{ "commands": { "claude-code-acp": ["my-acp", "--fast"] } }""");
        var commanded = await Which("claude-code-acp", "--workspace", "work");
        Assert.Equal(["my-acp", "--fast"], Run(commanded));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, commanded.GetProperty("pinned").ValueKind);
    }

    [Fact]
    public async Task Which_refuses_an_agent_no_wiring_declares_and_a_wiring_file_that_does_not_read()
    {
        Assert.Equal(2, await AgentsCommand.RunAsync(["which", "frobnicate", "--json"], World(), CancellationToken.None));
        Assert.StartsWith("daoris: unknown agent 'frobnicate'", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal("", _out.ToString());

        _error.GetStringBuilder().Clear();
        var wiring = DaorisHome.Entry(Home, HomeEntry.Harnesses);
        Directory.CreateDirectory(Path.GetDirectoryName(wiring)!);
        File.WriteAllText(wiring, "{ not json");
        Assert.Equal(2, await AgentsCommand.RunAsync(["which", "claude-code", "--json"], World(), CancellationToken.None));
        Assert.StartsWith("daoris: harnesses.json could not be read", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal("", _out.ToString());
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
        Assert.Equal(AgentsCommand.Verbs.Except(AgentsCommand.Reads).Order(StringComparer.Ordinal),
            AgentRoutes.Verbs.Select(row => row.Verb.Split(' ')[1]).Distinct().Order(StringComparer.Ordinal));
        Assert.All(AgentsCommand.Reads, verb => Assert.False(AgentsCommand.Isolated([verb], Named)));
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
