using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SUPERVISOR1a-ii (D208 point 2), amended by STEWARDROLE1a-i (D209 §2.2, §6.1 and §10 row 1a-i's <c>HolderTerminalTests</c>): a role
/// holder's terminal is a tab of Daoris's own terminal panel, never a console window of the system's, whose process is Daoris's own
/// pinned harness with <see cref="HolderTerminal.SessionArguments"/> as its argument vector, stated whole; in the holder's own folder by
/// its id, never moved or emptied while it lives, two holders of one role in two folders; on the holders' configuration directory, its
/// owned files written and the harness's own state left in place; its first lines say what it is and none holds the key; and a role
/// added to a running holder changes its title and its tab's words, not its folder.
/// </summary>
/// <remarks>
/// 🔴 A stand-in terminal factory throughout: no case starts a shell, a harness, a console or a window (the owner's direction, after a
/// Process case opened a console window that took the keyboard). The panel's real pseudo-console is <c>TerminalModuleTests</c>' own; the
/// harness is a file nobody runs (<see cref="HolderHarnessTests"/> holds how it is found and checked).
/// </remarks>
public sealed class HolderTerminalTests : Bridge
{
    private const string Sentinel = "5e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a1";

    internal sealed class Started(int id) : ITerminal
    {
        public int ProcessId { get; } = id;

        public DateTime Created { get; } = DateTime.UnixEpoch.AddSeconds(id);

        public void Write(string data)
        {
        }

        public void Resize(int columns, int rows)
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>The panel's factory as a stand-in: it records each launch and starts nothing; its exits are the test's to call.</summary>
    internal sealed class Terminals(params TerminalShell[] shells) : ITerminalFactory
    {
        public List<TerminalLaunch> Launched { get; } = [];

        public List<Action<int>> Exits { get; } = [];

        /// <summary>Called inside the start, as a harness that exits at once would end it while its appointment still opens it.</summary>
        public bool ExitAtOnce { get; set; }

        public IReadOnlyList<TerminalShell> Shells() => shells;

        public ITerminal Start(TerminalLaunch launch, Action<string> output, Action<int> exited)
        {
            lock (Launched)
            {
                Launched.Add(launch);
                Exits.Add(exited);
            }

            if (ExitAtOnce) exited(1);
            return new Started(4000 + Launched.Count);
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions Relaxed = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private string Binary => Path.Combine(Home, "toolchain-stand-in", "claude.exe");

    /// <summary>The module with a harness that is found and checked: a file nobody runs.</summary>
    private TerminalModule Module(Terminals terminals)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Binary)!);
        File.WriteAllText(Binary, "MZ-not-really-a-binary");
        return new TerminalModule(Bus, terminals) { Harness = _ => (Binary, null), App = Path.Combine(Home, "app") };
    }

    private static HolderOpen Open(string holder, IReadOnlyList<string>? living = null, params string[] roles) =>
        new(holder, Sentinel, roles.Length == 0 ? [Roles.Supervisor] : roles, DateTimeOffset.Parse("2026-10-24T14:02:00Z"), living ?? []);

    [Fact]
    public void The_argument_vector_is_stated_whole_its_settings_arriving_with_their_quotes_and_the_initial_prompt_last()
    {
        Assert.Equal(
            [
                "--setting-sources", "user", "--settings", "{\"disableAllHooks\":true}", "--strict-mcp-config", "--mcp-config", "{\"mcpServers\":{}}",
                "--allowedTools", "Read", "Grep", "Glob", "Bash(daoris-driver sessions supervise:*)", "Bash(daoris-driver steward:*)",
                "Bash(daoris-driver roles brief)", "Bash(daoris-driver sessions --json:*)", "Bash(daoris-driver notices:*)",
                "Bash(daoris-driver history:*)",
                "You hold a role Daoris appointed. Run `daoris-driver roles brief` and do as it says.",
            ],
            HolderTerminal.SessionArguments);
        Assert.Equal(HolderTerminal.InitialPrompt, HolderTerminal.SessionArguments[^1]);
        Assert.DoesNotContain(HolderTerminal.SessionArguments, each => each.StartsWith("Bash(daoris-driver sessions:", StringComparison.Ordinal));
        // The prompt names no key, no appointment and no particular role.
        Assert.DoesNotContain(HolderKey.Variable, HolderTerminal.InitialPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain(Roles.Supervisor, HolderTerminal.InitialPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain(Roles.Steward, HolderTerminal.InitialPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_appointment_opens_a_tab_of_the_panel_running_the_harness_in_the_holders_folder_and_the_key_in_its_environment()
    {
        var said = new List<EventMessage>();
        using var output = Bus.Subscribe("DAORIS", "TERMINAL_OUTPUT", message => { lock (said) said.Add(message); return Task.CompletedTask; });
        using var opened = Bus.Subscribe("DAORIS", "TERMINAL_OPENED", message => { lock (said) said.Add(message); return Task.CompletedTask; });
        var terminals = new Terminals();
        using var module = Module(terminals);

        var tab = await module.OpenHolderAsync(Home, Open("h1"));

        Assert.NotNull(tab.Tab);
        var launch = Assert.Single(terminals.Launched);
        Assert.Equal((Binary, HolderTerminal.SessionArguments), (launch.Shell.Path, launch.Shell.Arguments));
        Assert.True(launch.Whole);
        Assert.Equal(HolderTerminal.Folder(Home, "h1"), launch.Directory);
        Assert.StartsWith(DaorisHome.Entry(Home, HomeEntry.RoleRooms, "holders"), launch.Directory, StringComparison.Ordinal);
        Assert.Equal(Sentinel, launch.Environment[HolderKey.Variable]);
        Assert.Equal(HolderTerminal.Config(Home), launch.Environment[HolderTerminal.ConfigVariable]);
        Assert.Equal((4001, Binary, HolderTerminal.Config(Home)), (tab.Process!.Id, tab.Process.Image, tab.Process.Config));

        string Told()
        {
            lock (said) return string.Join('\n', said.Select(each => System.Text.Json.JsonSerializer.Serialize(each.Payload, Relaxed)));
        }

        await UntilAsync(() => Told().Contains("Daoris started its own pinned Claude Code here", StringComparison.Ordinal)
                               && Told().Contains("Daoris: the supervisor's terminal", StringComparison.Ordinal));
        var told = Told();
        Assert.DoesNotContain(Sentinel, told, StringComparison.Ordinal);
        Assert.Contains("never print the", told, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_holders_of_one_role_open_in_two_folders_and_neither_empties_the_other()
    {
        var terminals = new Terminals();
        using var module = Module(terminals);
        await module.OpenHolderAsync(Home, Open("h1"));
        var first = HolderTerminal.Folder(Home, "h1");
        File.WriteAllText(Path.Combine(first, "notes.txt"), "the harness's own");

        await module.OpenHolderAsync(Home, Open("h2", living: ["h1"]));

        Assert.Equal([first, HolderTerminal.Folder(Home, "h2")], terminals.Launched.Select(each => each.Directory));
        Assert.True(File.Exists(Path.Combine(first, "notes.txt")));

        // A holder no longer living is removed at the next holder's start; a living one never is.
        await module.OpenHolderAsync(Home, Open("h3", living: ["h2"]));
        Assert.False(Directory.Exists(first));
        Assert.True(Directory.Exists(HolderTerminal.Folder(Home, "h2")));
    }

    [Fact]
    public async Task The_configuration_directory_s_owned_files_are_written_and_the_harness_s_own_state_left_in_place()
    {
        var config = HolderTerminal.Config(Home);
        Directory.CreateDirectory(Path.Combine(config, "plugins"));
        Directory.CreateDirectory(Path.Combine(config, "projects"));
        File.WriteAllText(Path.Combine(config, "settings.json"), """{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"planted"}]}]}}""");
        File.WriteAllText(Path.Combine(config, "CLAUDE.md"), "planted");
        File.WriteAllText(Path.Combine(config, ".credentials.json"), "the owner's sign-in");
        File.WriteAllText(Path.Combine(config, "projects", "history.jsonl"), "the harness's own");
        var terminals = new Terminals();
        using var module = Module(terminals);

        await module.OpenHolderAsync(Home, Open("h1"));

        Assert.Equal(HolderTerminal.Settings, File.ReadAllText(Path.Combine(config, "settings.json")));
        Assert.Equal("{\"disableAllHooks\":true}", File.ReadAllText(Path.Combine(config, "settings.json")));
        Assert.False(File.Exists(Path.Combine(config, "CLAUDE.md")));
        Assert.False(Directory.Exists(Path.Combine(config, "plugins")));
        Assert.Equal("the owner's sign-in", File.ReadAllText(Path.Combine(config, ".credentials.json")));
        Assert.Equal("the harness's own", File.ReadAllText(Path.Combine(config, "projects", "history.jsonl")));
        Assert.Null(HolderTerminal.Owned(config));
        File.WriteAllText(Path.Combine(config, "commands"), "x");
        Assert.Equal("commands", HolderTerminal.Owned(config));
    }

    [Fact]
    public async Task A_role_added_to_a_running_holder_leaves_its_folder_in_place_and_changes_its_title_and_words()
    {
        var said = new List<EventMessage>();
        using var output = Bus.Subscribe("DAORIS", "TERMINAL_OUTPUT", message => { lock (said) said.Add(message); return Task.CompletedTask; });
        using var opened = Bus.Subscribe("DAORIS", "TERMINAL_OPENED", message => { lock (said) said.Add(message); return Task.CompletedTask; });
        var terminals = new Terminals();
        using var module = Module(terminals);
        var tab = (await module.OpenHolderAsync(Home, Open("h1"))).Tab!;
        var folder = HolderTerminal.Folder(Home, "h1");
        File.WriteAllText(Path.Combine(folder, "session.txt"), "kept");

        string[] roles = [Roles.Steward, Roles.Supervisor];
        Assert.True(module.TellHolder(tab, HolderTerminal.Added(Roles.Steward, DateTimeOffset.Parse("2026-10-24T14:05:00Z"), roles), HolderTerminal.Title(roles)));

        Assert.Single(terminals.Launched);
        Assert.True(File.Exists(Path.Combine(folder, "session.txt")));
        Assert.Equal("Daoris: the steward's and supervisor's terminal", HolderTerminal.Title(roles));
        string Told()
        {
            lock (said) return string.Join('\n', said.Select(each => System.Text.Json.JsonSerializer.Serialize(each.Payload, Relaxed)));
        }

        await UntilAsync(() => Told().Contains("The steward was added to this terminal at", StringComparison.Ordinal)
                               && Told().Contains("Daoris: the steward's and supervisor's terminal", StringComparison.Ordinal));
        Assert.False(module.TellHolder("t-gone", "x"));
    }

    /// <summary>D209 §2.2: the tab's end reaches the judge by one callback on every path, once each: the page's close, the harness's exit, the module's disposal, and an exit while it opens.</summary>
    [Fact]
    public async Task Every_path_a_holder_s_tab_ends_by_is_told_once()
    {
        var ended = new List<string>();
        var terminals = new Terminals();
        var module = Module(terminals);
        module.HolderEnded = tab => { lock (ended) ended.Add(tab); };

        var closed = (await module.OpenHolderAsync(Home, Open("h1"))).Tab!;
        Assert.True((await AnswerAsync(module, "CLOSE", new { id = closed })).GetProperty("closed").GetBoolean());
        var exited = (await module.OpenHolderAsync(Home, Open("h2"))).Tab!;
        terminals.Exits[1](0);
        terminals.Exits[1](0);
        var disposed = (await module.OpenHolderAsync(Home, Open("h3"))).Tab!;
        terminals.ExitAtOnce = true;
        var opening = (await module.OpenHolderAsync(Home, Open("h4"))).Tab!;
        module.Dispose();

        Assert.Equal([closed, exited, opening, disposed], ended);
    }

    /// <summary>The panel's environment block never inherits the key: only the holder's own launch adds it, and a whole block inherits nothing.</summary>
    [Fact]
    public void A_tab_of_the_panel_never_inherits_the_key()
    {
        var inherited = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [HolderKey.Variable] = Sentinel, ["PATH"] = @"C:\x" };
        Assert.DoesNotContain(Sentinel, Block(new Dictionary<string, string>(), inherited), StringComparison.Ordinal);
        Assert.Contains($"{HolderKey.Variable}={Sentinel}",
            Block(new Dictionary<string, string> { [HolderKey.Variable] = Sentinel }, new Dictionary<string, string>()), StringComparison.Ordinal);
        var whole = (string)typeof(PseudoConsoleTerminals).Assembly.GetType("Daoris.Driver.PseudoConsole")!
            .GetMethod("WholeBlock", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, [new Dictionary<string, string> { ["B"] = "2", ["A"] = "1" }])!;
        Assert.Equal("A=1\0B=2\0\0", whole);
    }

    private static string Block(IReadOnlyDictionary<string, string> added, IReadOnlyDictionary<string, string> inherited) =>
        (string)typeof(PseudoConsoleTerminals).Assembly.GetType("Daoris.Driver.PseudoConsole")!
            .GetMethod("EnvironmentBlock", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, [added, inherited])!;

    private static async Task UntilAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow + Poll.Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, "the panel told nothing");
            await Task.Delay(10);
        }
    }
}
