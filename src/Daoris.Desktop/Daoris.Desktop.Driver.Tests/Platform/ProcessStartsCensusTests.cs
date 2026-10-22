using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every process the desktop starts passes the start gate (PROCSTART2, UIFREEZE1): .NET starts every process of the
/// application under one lock, and a start made on the window's thread, before its route's first wait, waited on that lock
/// there. A source scan, as <see cref="EveryChildIsHandedTheToolsTests"/> holds the tools' environment: the next start
/// written will not know to.
/// </summary>
/// <remarks>
/// <para><b>A direct start</b> is the runtime's <c>Process.Start</c>, called or handed as a method, a <c>Process</c> built
/// to be started, or the system's <c>CreateProcessW</c>, declared or called (TERMSTART1); comments are read out first. Each
/// that may stay is named below, by file and count, with why.</para>
/// <para><b>The file manager</b> is the window kit's launcher, which starts its own process: each module that opens a folder
/// calls it through <c>OpenFolders.OpenAsync</c>, which passes the gate's <c>RunAsync</c>.</para>
/// <para><b>The agents' route</b> (TERMTRUST1a part (i), D201 point 4): every agent, plugin process and piece is started through
/// <see cref="AgentStarts"/>, which makes its start the launcher's and passes the gate; every other start through the gate is named as
/// Daoris's own, with its reason, so an agent's start written outside the route is named here.</para>
/// </remarks>
public sealed class ProcessStartsCensusTests
{
    private static readonly string[] Projects =
    [
        "Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules", "Daoris.Desktop.App", "Daoris.Desktop.Launcher",
    ];

    /// <summary>The direct starts that stay, by file and how many, each with its reason.</summary>
    private static readonly Dictionary<string, (int Count, string Why)> Allowed = new(StringComparer.Ordinal)
    {
        ["Daoris.Desktop.Driver/Platform/ProcessStarts.cs"] = (1, "the gate itself: the one place the runtime's start is handed in"),
        ["Daoris.Desktop.Driver/Platform/GitDoors.cs"] = (3, "the git doors (GITAUTH1b), compiled into a program of their own that git starts, "
            + "with no window and no bridge: the repository's ssh setting asked of git, the ssh, and git's own transport"),
        ["Daoris.Desktop.Driver/Conversations/Partners/PieceLauncher.cs"] = (1, "the agents' launcher (COAGENT1b, TERMTRUST1a), compiled "
            + "into a program of its own that the driver starts for every agent, plugin process and piece, with no window and no bridge: "
            + "the harness, once the launcher is in the jobs it was named"),
        ["Daoris.Desktop.Launcher/Program.cs"] = (1,"the launcher is a program of its own, with no window and no bridge: it starts the application once and exits"),
        ["Daoris.Desktop.Driver/Agents/Consoles/PseudoConsole.cs"] = (2, "a terminal's shell under a pseudo-console, which .NET cannot start: "
            + "CreateProcessW, declared and called once; the terminal module runs that start through the gate's RunAsync"),
        ["Daoris.Desktop.Driver/Sessions/Heavy/HeavyStep.cs"] = (3, "a heavy run's gate (HEAVYRUN1a), in the verb's own process, "
            + "not the desktop's: CreateProcessW, declared and called once, so the gate starts suspended and joins the run's "
            + "kill-on-close job before it can start a child of its own; and /bin/sh off Windows, where no job holds it"),
    };

    [Fact]
    public void No_process_is_started_outside_the_gate_but_the_ones_named_with_their_reason()
    {
        var found = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (file, source) in Sources())
        {
            var direct = Regex.Matches(source, @"\bProcess\.Start\b|\bnew\s+(?:System\.Diagnostics\.)?Process\s*[({]|\bCreateProcessW\s*\(").Count;
            if (direct > 0) found[file] = direct;
        }

        var outside = found
            .Where(each => !Allowed.TryGetValue(each.Key, out var allowed) || each.Value > allowed.Count)
            .Select(each => $"{each.Key}: {each.Value} direct start(s)")
            .ToList();
        Assert.True(outside.Count == 0, "a start outside ProcessStarts waits on the runtime's start lock on its caller's thread, "
            + "the window's among them:\n" + string.Join('\n', outside));
        // An allowance nothing needs any more is taken back, so the list stays the reasons that are true.
        Assert.All(Allowed, allowed => Assert.True(found.ContainsKey(allowed.Key), $"{allowed.Key} starts nothing directly now: take it off the list"));
    }

    /// <summary>The starts the row named, and the file manager's three doors: each passes the gate (the agents' route passes it too).</summary>
    [Theory]
    [InlineData("Daoris.Desktop.Driver/Loop/Driver.cs", "AgentStarts.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Loop/DriverContinuations.cs", "AgentStarts.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Conversations/Chat/ChatRunner.cs", "AgentStarts.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Plugins/HookProcess.cs", "AgentStarts.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Platform/AgentStarts.cs", "ProcessStarts.Shared.StartAsync(info, whose, ct)")]
    [InlineData("Daoris.Desktop.Driver/Accounts/Harnesses/HarnessActions.cs", "ProcessStarts.Shared.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Accounts/Usage/CodexUsage.cs", "ProcessStarts.Shared.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Plugins/PluginTrialProcess.cs", "AgentStarts.StartAsync(")]
    [InlineData("Daoris.Desktop.Modules/Loop/HostSupervisor.cs", "ProcessStarts.Shared.StartAsync(")]
    [InlineData("Daoris.Desktop.App/EngineBrowserHost.cs", "ProcessStarts.Shared.StartAsync(")]
    [InlineData("Daoris.Desktop.Modules/Update/UpdateLauncher.cs", "ProcessStarts.Shared.StartAsync(")]
    [InlineData("Daoris.Desktop.Modules/Bridge/Routes/PluginsRoutes.cs", "_openFolder.OpenAsync(folder")]
    [InlineData("Daoris.Desktop.Modules/Bridge/Routes/SessionsRoutes.cs", "_openFolder.OpenAsync(folder")]
    [InlineData("Daoris.Desktop.Modules/Platform/LogModule.cs", "openFolder.OpenAsync(folder")]
    [InlineData("Daoris.Desktop.Modules/Windows/OpenFolder.cs", "ProcessStarts.Shared.RunAsync(() => open(folder)")]
    [InlineData("Daoris.Desktop.Modules/Windows/TerminalModule.cs", "ProcessStarts.Shared.RunAsync(() => started = _terminals.Start(")]
    public void Each_named_start_passes_the_gate(string file, string gate)
    {
        var source = Sources().Single(each => each.File == file).Source;

        Assert.Contains(gate, source);
    }

    /// <summary>
    /// Whose each start through the shared gate is (PARALLEL1d, D181 point 5), by file: a background start names
    /// <c>StartClass.Background</c>, so the person's place is never its; a person's names none, and takes any place free.
    /// </summary>
    private static readonly Dictionary<string, StartClass> Whose = new(StringComparer.Ordinal)
    {
        // git's own streams, the loop's agent starts (a run going on with the person's words is a person's), a plugin's
        // hook program, and the usage probe.
        ["Daoris.Desktop.Driver/Trees/WorkingTree.cs"] = StartClass.Background,
        ["Daoris.Desktop.Driver/Loop/Driver.cs"] = StartClass.Background,
        ["Daoris.Desktop.Driver/Plugins/HookProcess.cs"] = StartClass.Background,
        ["Daoris.Desktop.Driver/Accounts/Usage/CodexUsage.cs"] = StartClass.Background,
        // A git identity's key made, or its host's keys read and its test run, at the person's own verb (GITAUTH1c).
        ["Daoris.Desktop.Driver/Platform/GitIdentityActs.cs"] = StartClass.Person,
        // A conversation's agent, a session going on with the person's words, a press, a terminal, and the host the window
        // waits on.
        ["Daoris.Desktop.Driver/Conversations/Chat/ChatRunner.cs"] = StartClass.Person,
        ["Daoris.Desktop.Driver/Loop/DriverContinuations.cs"] = StartClass.Person,
        ["Daoris.Desktop.Driver/Accounts/Harnesses/HarnessActions.cs"] = StartClass.Person,
        ["Daoris.Desktop.Driver/Plugins/PluginTrialProcess.cs"] = StartClass.Person,
        ["Daoris.Desktop.Modules/Loop/HostSupervisor.cs"] = StartClass.Person,
        ["Daoris.Desktop.App/EngineBrowserHost.cs"] = StartClass.Person,
        ["Daoris.Desktop.Modules/Update/UpdateLauncher.cs"] = StartClass.Person,
        ["Daoris.Desktop.Modules/Windows/OpenFolder.cs"] = StartClass.Person,
        ["Daoris.Desktop.Modules/Windows/TerminalModule.cs"] = StartClass.Person,
    };

    [Fact]
    public void Every_start_through_the_shared_gate_says_whose_it_is()
    {
        var unnamed = new List<string>();
        var wrong = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (file, source) in Sources())
        {
            // The agents' route passes the gate as its caller says (TERMTRUST1a): its sites say whose, and the route hands it on.
            if (file == AgentRoute || !Regex.IsMatch(source, @"\bProcessStarts\.Shared\.(?:StartAsync|RunAsync)\(|\bAgentStarts\.StartAsync\(")) continue;
            seen.Add(file);
            if (!Whose.TryGetValue(file, out var whose))
            {
                unnamed.Add(file);
                continue;
            }

            var background = source.Contains("StartClass.Background", StringComparison.Ordinal);
            if (background != (whose == StartClass.Background)) wrong.Add($"{file}: expected {whose}");
        }

        Assert.True(unnamed.Count == 0, "a start nobody said is whose: name it in Whose, a person's or the background's:\n"
            + string.Join('\n', unnamed));
        Assert.True(wrong.Count == 0, "a start that is not what Whose says:\n" + string.Join('\n', wrong));
        Assert.All(Whose.Keys, file => Assert.True(seen.Contains(file), $"{file} starts nothing through the gate now: take it off Whose"));
    }

    /// <summary>The agents' route (TERMTRUST1a): it passes the gate itself, with whose its caller said.</summary>
    private const string AgentRoute = "Daoris.Desktop.Driver/Platform/AgentStarts.cs";

    /// <summary>
    /// Every start through the agents' route (TERMTRUST1a part (i), D201 point 4; the terminal-trust design §0.6 and §8 case 2), by file:
    /// how many, and what each starts. A new one is named here with its kind.
    /// </summary>
    private static readonly Dictionary<string, (int Count, string Kind)> Routed = new(StringComparer.Ordinal)
    {
        // The driver's one run: driven sessions, intakes, helpers, reviewers, brainstorm passes, workflow nodes, carry-ons and partners.
        ["Daoris.Desktop.Driver/Loop/Driver.cs"] = (1, "piece is null ? ContainedKind.Agent : ContainedKind.Piece"),
        // A native run going on with the person's words (MSG1b), and its one retry (SESSCTX1c).
        ["Daoris.Desktop.Driver/Loop/DriverContinuations.cs"] = (2, "ContainedKind.Agent"),
        // A conversation, Ask Daoris's among them.
        ["Daoris.Desktop.Driver/Conversations/Chat/ChatRunner.cs"] = (1, "ContainedKind.Agent"),
        // A plugin's hook program, and the kit's trial of it.
        ["Daoris.Desktop.Driver/Plugins/HookProcess.cs"] = (1, "ContainedKind.Plugin"),
        ["Daoris.Desktop.Driver/Plugins/PluginTrialProcess.cs"] = (1, "ContainedKind.Plugin"),
    };

    /// <summary>
    /// Every other start through the gate, Daoris's own and no agent's (the terminal-trust design §3.4), by file, each with its reason: an
    /// agent's start written beside them is not one of these, and the census names it.
    /// </summary>
    private static readonly Dictionary<string, string> DaorisOwn = new(StringComparer.Ordinal)
    {
        [AgentRoute] = "the agents' route itself: the launcher's start, or a start it could not contain, through the gate",
        ["Daoris.Desktop.Driver/Trees/WorkingTree.cs"] = "git's own runs in the trees, Daoris's acts: routed, each run in the look would cost a launcher's start (§3.4)",
        ["Daoris.Desktop.Driver/Platform/ChildProcess.cs"] = "the one runner of Daoris's own questions read to their end: git, and the harness probes' --version",
        ["Daoris.Desktop.Driver/Platform/GitIdentityActs.cs"] = "a git identity's key made, or its host's keys read, at the person's own verb: ssh-keygen and ssh",
        ["Daoris.Desktop.Driver/Accounts/Harnesses/HarnessActions.cs"] = "an agent's install or sign-in at the person's press: Daoris's start of the agent's own tool, never a session (§3.4)",
        ["Daoris.Desktop.Driver/Accounts/Usage/CodexUsage.cs"] = "a usage read: Daoris's own probe of an account (§3.4)",
        ["Daoris.Desktop.Modules/Loop/HostSupervisor.cs"] = "the HTTP host the window waits on",
        ["Daoris.Desktop.App/EngineBrowserHost.cs"] = "Daoris's own browser",
        ["Daoris.Desktop.Modules/Update/UpdateLauncher.cs"] = "the update's launcher, which outlives this process by design",
        ["Daoris.Desktop.Modules/Windows/OpenFolder.cs"] = "the file manager, at the person's press",
        ["Daoris.Desktop.Modules/Windows/TerminalModule.cs"] = "the terminal panel's shell, the person's own, in a job of its own that is no agent's (§3.3)",
    };

    /// <summary>
    /// 🔴 TERMTRUST1a: every agent start is through the launcher's route, and every other start through the gate is named as Daoris's own
    /// with its reason. A start written outside the route is not contained, and part (ii)'s standing confirmation would read its agent as the
    /// person.
    /// </summary>
    [Fact]
    public void Every_agent_start_goes_through_the_launchers_route_and_every_other_is_named_as_daoriss_own()
    {
        var (unrouted, routed) = Census(Sources());

        Assert.True(unrouted.Count == 0, "a start through the gate that is neither the agents' route nor named as Daoris's own: "
            + "an agent's goes through AgentStarts.StartAsync, and Daoris's own is named in DaorisOwn with why:\n" + string.Join('\n', unrouted));
        Assert.All(DaorisOwn.Keys, file => Assert.True(Sources().Any(each => each.File == file && GateCalls(each.Source) > 0),
            $"{file} starts nothing through the gate now: take it off DaorisOwn"));
        Assert.Equal(
            Routed.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => $"{each.Key}: {each.Value.Count}"),
            routed.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => $"{each.Key}: {each.Value}"));
    }

    /// <summary>Each routed start says what it starts, as the log's <c>kind</c> will.</summary>
    [Fact]
    public void Each_routed_start_says_its_kind()
    {
        foreach (var (file, (_, kind)) in Routed)
        {
            var source = Sources().Single(each => each.File == file).Source;
            Assert.True(Regex.IsMatch(source, @"AgentStarts\.StartAsync\(\s*info,\s*" + Regex.Escape(kind)), $"{file}: its start says {kind}");
        }
    }

    /// <summary>The census itself, red on a start planted outside the route: what the fact above would say of it.</summary>
    [Fact]
    public void A_start_planted_beside_the_route_is_named_by_the_census()
    {
        var planted = Sources().Append(("Daoris.Desktop.Driver/Loop/Planted.cs",
            "var process = await ProcessStarts.Shared.StartAsync(info, StartClass.Background, ct);"));

        var (unrouted, _) = Census(planted);

        Assert.Equal(["Daoris.Desktop.Driver/Loop/Planted.cs: 1 start(s) through the gate"], unrouted);
    }

    /// <summary>The starts through the gate outside the route and Daoris's own, and the route's calls by file.</summary>
    private static (List<string> Unrouted, Dictionary<string, int> Routed) Census(IEnumerable<(string File, string Source)> sources)
    {
        var unrouted = new List<string>();
        var routed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (file, source) in sources)
        {
            var gate = GateCalls(source);
            if (gate > 0 && !DaorisOwn.ContainsKey(file)) unrouted.Add($"{file}: {gate} start(s) through the gate");
            var route = Regex.Matches(source, @"\bAgentStarts\.StartAsync\(").Count;
            if (route > 0) routed[file] = route;
        }

        return (unrouted, routed);
    }

    private static int GateCalls(string source) =>
        Regex.Matches(source, @"\bProcessStarts\.Shared\.(?:StartAsync|RunAsync)\(|\bstarts\.StartAsync\(").Count;

    /// <summary>The one runner's runs are questions read to their end: the background's (PARALLEL1d).</summary>
    [Fact]
    public void The_child_runner_starts_as_the_background()
    {
        var source = Sources().Single(each => each.File == "Daoris.Desktop.Driver/Platform/ChildProcess.cs").Source;

        Assert.Contains("starts.StartAsync(info, StartClass.Background, ct)", source);
    }

    /// <summary>The launcher is called by <c>OpenFolders.OpenAsync</c> alone, which passes the gate: no module calls it itself.</summary>
    [Fact]
    public void The_file_manager_is_opened_through_the_gate_wherever_a_module_opens_it()
    {
        var opens = 0;
        var bare = new List<string>();
        foreach (var (file, source) in Sources())
        {
            opens += Regex.Matches(source, @"\.OpenAsync\(folder\b").Count;
            bare.AddRange(Regex.Matches(source, @"\b_?openFolder!?\s*\(").Select(call => $"{file} at offset {call.Index}"));
        }

        // The plugins' folders, a session's folder, and the log's.
        Assert.True(opens >= 3, $"expected the modules' three folder doors, found {opens}");
        Assert.True(bare.Count == 0, "the file manager opened on the caller's thread:\n" + string.Join('\n', bare));
    }

    /// <summary>Every production source of the desktop's projects, by its path under the desktop folder, comments read out.</summary>
    private static IEnumerable<(string File, string Source)> Sources()
    {
        var root = EveryChildIsHandedTheToolsTests.SourceRoot();
        foreach (var project in Projects)
        {
            var folder = Path.Combine(root, project);
            foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                // Line comments out, as the tools' scan reads them: a sentence naming a start is not one.
                var source = Regex.Replace(File.ReadAllText(file).Replace("\r\n", "\n"), @"//[^\n]*", "");
                yield return (Path.GetRelativePath(root, file).Replace('\\', '/'), source);
            }
        }
    }
}
