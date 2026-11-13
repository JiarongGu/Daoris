using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every process the desktop starts passes the start gate: .NET starts every process of the
/// application under one lock, and a start made on the window's thread, before its route's first wait, waited on that lock
/// there. A source scan, as <see cref="EveryChildIsHandedTheToolsTests"/> holds the tools' environment: the next start
/// written will not know to.
/// </summary>
/// <remarks>
/// <para><b>A direct start</b> is the runtime's <c>Process.Start</c>, called or handed as a method, a <c>Process</c> built
/// to be started, or the system's <c>CreateProcessW</c>, declared or called; comments are read out first. Each
/// file that may stay is named below, with why; how many starts a listed file makes is not pinned.</para>
/// <para><b>The file manager</b> is the window kit's launcher, which starts its own process: each module that opens a folder
/// calls it through <c>OpenFolders.OpenAsync</c>, which passes the gate's <c>RunAsync</c>.</para>
/// <para><b>The agents' route</b>: every agent, plugin process and piece is started through
/// <see cref="AgentStarts"/>, which makes its start the launcher's and passes the gate; every other start through the gate is named as
/// Daoris's own, with its reason, so an agent's start written outside the route is named here.</para>
/// </remarks>
[Trait(Category.Name, Category.Census)]
public sealed class ProcessStartsCensusTests
{
    private static readonly string[] Projects =
    [
        "Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules", "Daoris.Desktop.App", "Daoris.Desktop.Launcher",
    ];

    /// <summary>The files whose direct starts stay, each with its reason; how many a listed file makes is not pinned.</summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.Ordinal)
    {
        ["Daoris.Desktop.Driver/Platform/ProcessStarts.cs"] = "the gate itself: the one place the runtime's start is handed in",
        ["Daoris.Desktop.Driver/Workplace/GitDoors.cs"] = "the git doors (GITAUTH1b), compiled into a program of their own that git starts, "
            + "with no window and no bridge: the repository's ssh setting asked of git, the ssh, and git's own transport",
        ["Daoris.Desktop.Driver/Platform/CommandLauncher.cs"] = "the install's command, compiled into a program of its own that a terminal runs, "
            + "with no window and no bridge: the desktop's program, with the arguments the command was given",
        ["Daoris.Desktop.Driver/Platform/PieceLauncher.cs"] = "the agents' launcher (COAGENT1b, TERMTRUST1a), compiled "
            + "into a program of its own that the driver starts for every agent, plugin process and piece, with no window and no bridge: "
            + "the harness, once the launcher is in the jobs it was named",
        ["Daoris.Desktop.Launcher/Program.cs"] = "the launcher is a program of its own, with no window and no bridge: it starts the application once and exits",
        ["Daoris.Desktop.Driver/Platform/PseudoConsole.cs"] = "a terminal's shell under a pseudo-console, which .NET cannot start: "
            + "CreateProcessW, declared and called once; the terminal module runs that start through the gate's RunAsync",
        ["Daoris.Desktop.Driver/Review/Heavy/HeavyStep.cs"] = "a heavy run's gate (HEAVYRUN1a), in the running application for the terminal "
            + "that asked it, else in the verb's own process: CreateProcessW, declared and called once, so the gate starts suspended and joins the run's "
            + "kill-on-close job before it can start a child of its own; and /bin/sh off Windows, where no job holds it",
        ["Daoris.Desktop.Driver/Platform/HeldChild.cs"] = "a git act's effect step (GITAPP2a): CreateProcessW, declared and called once, so its git starts "
            + "suspended, joins the act's named kill-on-close job, and runs only once its id is journaled; none off Windows",
    };

    /// <summary>The files, with how many direct starts each makes, of the sources that make one.</summary>
    private static Dictionary<string, int> DirectStarts(IEnumerable<(string File, string Source)> sources)
    {
        var found = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (file, source) in sources)
        {
            var direct = Regex.Matches(source, @"\bProcess\.Start\b|\bnew\s+(?:System\.Diagnostics\.)?Process\s*[({]|\bCreateProcessW\s*\(").Count;
            if (direct > 0) found[file] = direct;
        }

        return found;
    }

    /// <summary>The problems of a scan against the list: a file that starts directly and is not listed, and a listed file that no longer does.</summary>
    private static List<string> DirectProblems(Dictionary<string, int> found) =>
    [
        .. found.Where(each => !Allowed.ContainsKey(each.Key)).Select(each => $"{each.Key}: {each.Value} direct start(s) and not listed"),
        .. Allowed.Keys.Where(file => !found.ContainsKey(file)).Select(file => $"{file} starts nothing directly now: take it off the list"),
    ];

    [Fact]
    public void No_process_is_started_outside_the_gate_but_in_the_files_named_with_their_reason()
    {
        var problems = DirectProblems(DirectStarts(Sources()));

        Assert.True(problems.Count == 0, "a start outside ProcessStarts waits on the runtime's start lock on its caller's thread, "
            + "the window's among them:\n" + string.Join('\n', problems));
    }

    /// <summary>A new file with a direct start fails; one more direct start in a listed file passes.</summary>
    [Fact]
    public void A_direct_start_in_a_new_file_fails_the_census_and_one_more_in_a_listed_file_passes()
    {
        var listed = Allowed.Keys.Select(file => (File: file, Source: "var p = Process.Start(info);")).ToList();

        Assert.Empty(DirectProblems(DirectStarts(listed)));
        Assert.Empty(DirectProblems(DirectStarts(listed.Select(each => each.File == listed[0].File
            ? (each.File, each.Source + "\nvar q = Process.Start(info);\nvar r = new Process();") : each))));
        Assert.Equal(["Daoris.Desktop.Driver/Loop/Planted.cs: 1 direct start(s) and not listed"],
            DirectProblems(DirectStarts(listed.Append(("Daoris.Desktop.Driver/Loop/Planted.cs", "var p = Process.Start(info);")))));
        Assert.Equal([$"{listed[0].File} starts nothing directly now: take it off the list"],
            DirectProblems(DirectStarts(listed.Select(each => each.File == listed[0].File ? (each.File, "var p = 1;") : each))));
    }

    /// <summary>The starts the row named, and the file manager's three doors: each passes the gate (the agents' route passes it too).</summary>
    [Theory]
    [InlineData("Daoris.Desktop.Driver/Agents/TransportRun.cs", "AgentStarts.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Plugins/HookProcess.cs", "AgentStarts.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Platform/AgentStarts.cs", "ProcessStarts.Shared.StartAsync(info, PurposeOf(start), whose, ct)")]
    [InlineData("Daoris.Desktop.Driver/Agents/HarnessActions.cs", "ProcessStarts.Shared.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Agents/Providers/Codex/CodexUsage.cs", "ProcessStarts.Shared.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Plugins/PluginToolSetUp.cs", "ProcessStarts.Shared.StartAsync(")]
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
    /// Whose each start through the shared gate is, by file: a background start names
    /// <c>StartClass.Background</c>, so the person's place is never its; a person's names none, and takes any place free.
    /// </summary>
    private static readonly Dictionary<string, StartClass> Whose = new(StringComparer.Ordinal)
    {
        // A doctrine command at the person's own words (ONEWAY1ai-i).
        ["Daoris.Desktop.Driver.Host/DoctrineConsole.cs"] = StartClass.Person,
        // git's own streams, a plugin's hook program, and the usage probe.
        ["Daoris.Desktop.Driver/Workplace/GitProcessRuns.cs"] = StartClass.Background,
        ["Daoris.Desktop.Driver/Plugins/HookProcess.cs"] = StartClass.Background,
        ["Daoris.Desktop.Driver/Agents/Providers/Codex/CodexUsage.cs"] = StartClass.Background,
        // npm installing a session's server's package, once, before the session's start.
        ["Daoris.Desktop.Driver/Agents/ServerPackages.cs"] = StartClass.Background,
        // A git identity's key made, or its host's keys read and its test run, at the person's own verb.
        ["Daoris.Desktop.Driver/Workplace/GitIdentityActs.cs"] = StartClass.Person,
        // The run over today's transports, every executor's start: its first start's class is its executor's, handed in
        // (the loop's, or a person's for a run going on with the person's words, and a conversation's); its go-on and its retry
        // are a person's. A press, a terminal, and the host the window waits on.
        ["Daoris.Desktop.Driver/Agents/TransportRun.cs"] = StartClass.Person,
        // The layer's runner: its class is its start's, handed in its seat as the run over today's transports hands its.
        ["Daoris.Desktop.Driver/Agents/Layer/LauncherRunner.cs"] = StartClass.Person,
        ["Daoris.Desktop.Driver/Agents/HarnessActions.cs"] = StartClass.Person,
        ["Daoris.Desktop.Driver/Plugins/PluginTrialProcess.cs"] = StartClass.Person,
        ["Daoris.Desktop.Driver/Plugins/PluginToolSetUp.cs"] = StartClass.Person,
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
            // The agents' route passes the gate as its caller says: its sites say whose, and the route hands it on.
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

    /// <summary>The agents' route: it passes the gate itself, with whose its caller said.</summary>
    private const string AgentRoute = "Daoris.Desktop.Driver/Platform/AgentStarts.cs";

    /// <summary>
    /// Every file that starts through the agents' route,
    /// and what it starts. A new file is named here with its kind; how many starts a listed file makes is not pinned.
    /// </summary>
    private static readonly Dictionary<string, string> Routed = new(StringComparer.Ordinal)
    {
        // The run over today's transports, every executor's start: driven sessions, answers' resumes, intakes, helpers,
        // reviewers, brainstorm passes, workflow nodes, carry-ons and partners, a native run's go-on with the person's words
        // and its resume's one retry, and a conversation, Ask Daoris's among them.
        ["Daoris.Desktop.Driver/Agents/TransportRun.cs"] = "piece is null ? ContainedKind.Agent : ContainedKind.Piece",
        // The layer's runner: a layer door's turn or read, its kind its seat's.
        ["Daoris.Desktop.Driver/Agents/Layer/LauncherRunner.cs"] = "seat.Kind",
        // A plugin's hook program, and the kit's trial of it.
        ["Daoris.Desktop.Driver/Plugins/HookProcess.cs"] = "ContainedKind.Plugin",
        ["Daoris.Desktop.Driver/Plugins/PluginTrialProcess.cs"] = "ContainedKind.Plugin",
    };

    /// <summary>
    /// Every other start through the gate, Daoris's own and no agent's (the terminal-trust design §3.4), by file, each with its reason: an
    /// agent's start written beside them is not one of these, and the census names it.
    /// </summary>
    private static readonly Dictionary<string, string> DaorisOwn = new(StringComparer.Ordinal)
    {
        [AgentRoute] = "the agents' route itself: the launcher's start, or a start it could not contain, through the gate",
        ["Daoris.Desktop.Driver/Workplace/GitProcessRuns.cs"] = "git's own runs in the trees, Daoris's acts: routed, each run in the look would cost a launcher's start (§3.4)",
        ["Daoris.Desktop.Driver.Host/DoctrineConsole.cs"] = "a doctrine command handed to the CLI the install carries, on node, at the person's own words (ONEWAY1ai-i)",
        ["Daoris.Desktop.Driver/Platform/ChildProcess.cs"] = "the one runner of Daoris's own questions read to their end: git, and the harness probes' --version",
        ["Daoris.Desktop.Driver/Workplace/GitIdentityActs.cs"] = "a git identity's key made, or its host's keys read, at the person's own verb: ssh-keygen and ssh",
        ["Daoris.Desktop.Driver/Agents/HarnessActions.cs"] = "an agent's install or sign-in at the person's press: Daoris's start of the agent's own tool, never a session (§3.4)",
        ["Daoris.Desktop.Driver/Agents/Providers/Codex/CodexUsage.cs"] = "a usage read: Daoris's own probe of an account (§3.4)",
        ["Daoris.Desktop.Driver/Agents/ServerPackages.cs"] =
            "npm installing a session's server's package into the toolchain, once, before any session starts it: never a session (LAUNCHCHAIN1)",
        ["Daoris.Desktop.Driver/Plugins/PluginToolSetUp.cs"] =
            "a plugin tool's set-up or sign-in at the person's press, in a console they see: its own tool's command, never a session (ADOSIGNIN1)",
        ["Daoris.Desktop.Modules/Loop/HostSupervisor.cs"] = "the HTTP host the window waits on",
        ["Daoris.Desktop.App/EngineBrowserHost.cs"] = "Daoris's own browser",
        ["Daoris.Desktop.Modules/Update/UpdateLauncher.cs"] = "the update's launcher, which outlives this process by design",
        ["Daoris.Desktop.Modules/Windows/OpenFolder.cs"] = "the file manager, at the person's press",
        ["Daoris.Desktop.Modules/Windows/TerminalModule.cs"] = "the terminal panel's shell, the person's own, in a job of its own that is no agent's (§3.3)",
    };

    /// <summary>
    /// 🔴 Every agent start is through the launcher's route, and every other start through the gate is named as Daoris's own
    /// with its reason. A start written outside the route is not contained, and the standing confirmation would read its agent as the
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
        Assert.Equal(Routed.Keys.Order(StringComparer.Ordinal), routed.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>Each routed start says what it starts, as the log's <c>kind</c> will.</summary>
    [Fact]
    public void Each_routed_start_says_its_kind()
    {
        foreach (var (file, kind) in Routed)
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

        // One more start in a file already named as Daoris's own passes.
        var own = DaorisOwn.Keys.First(file => file != AgentRoute);
        var more = Sources().Append((own, "ProcessStarts.Shared.StartAsync(a, b, c); ProcessStarts.Shared.StartAsync(a, b, c);"));
        Assert.Empty(Census(more).Unrouted);
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

    /// <summary>
    /// The downloads an agent's start is handed are kept by its run's spawn (<see cref="TransportRun"/> through the
    /// driver's port, or the conversation's; a layer run's through its hold), and no executor keeps them
    /// beside it.
    /// </summary>
    [Fact]
    public void A_starts_downloads_are_kept_by_its_runs_spawn_and_by_no_executor_beside_it()
    {
        var keeping = Sources()
            .Where(each => each.Source.Contains("KeepHandedAsync(", StringComparison.Ordinal))
            .Select(each => each.File)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Daoris.Desktop.Driver/Agents/Layer/LayerHold.cs", "Daoris.Desktop.Driver/Agents/TransportRun.cs", "Daoris.Desktop.Driver/Agents/TransportStart.cs",
                "Daoris.Desktop.Driver/Loop/Chat/ChatTransportHost.cs", "Daoris.Desktop.Driver/Loop/DriverTransportHost.cs", "Daoris.Desktop.Driver/Workplace/DownloadKeeping.cs",
            ],
            keeping);
    }

    /// <summary>The one runner's runs are questions read to their end: the background's.</summary>
    [Fact]
    public void The_child_runner_starts_as_the_background()
    {
        var source = Sources().Single(each => each.File == "Daoris.Desktop.Driver/Platform/ChildProcess.cs").Source;

        Assert.Contains("starts.StartAsync(info, ProcessPurpose.Read, StartClass.Background, ct)", source);
    }

    /// <summary>
    /// What each start through the gate is for, by file: the gate hands every start to the one management
    /// (<see cref="OwnedProcesses"/>) under the purpose it names, a required word, so a start cannot pass it unmanaged; this holds which
    /// purpose each file names. How many starts a listed file makes is not pinned.
    /// </summary>
    private static readonly Dictionary<string, string> Purposes = new(StringComparer.Ordinal)
    {
        // The agents' job where the launcher took the start, else its kind's job here.
        [AgentRoute] = "PurposeOf(start)",
        ["Daoris.Desktop.Driver/Workplace/GitProcessRuns.cs"] = "ProcessPurpose.Git",
        ["Daoris.Desktop.Driver/Platform/ChildProcess.cs"] = "ProcessPurpose.Read",
        ["Daoris.Desktop.Driver/Agents/Providers/Codex/CodexUsage.cs"] = "ProcessPurpose.Read",
        ["Daoris.Desktop.Driver/Agents/HarnessActions.cs"] = "ProcessPurpose.Tool",
        ["Daoris.Desktop.Driver/Agents/ServerPackages.cs"] = "ProcessPurpose.Tool",
        ["Daoris.Desktop.Driver/Workplace/GitIdentityActs.cs"] = "ProcessPurpose.Tool",
        ["Daoris.Desktop.Driver/Plugins/PluginToolSetUp.cs"] = "ProcessPurpose.Tool",
        ["Daoris.Desktop.Driver.Host/DoctrineConsole.cs"] = "ProcessPurpose.Tool",
        ["Daoris.Desktop.Modules/Loop/HostSupervisor.cs"] = "ProcessPurpose.EndsItself",
        ["Daoris.Desktop.App/EngineBrowserHost.cs"] = "ProcessPurpose.Outlives",
        ["Daoris.Desktop.Modules/Update/UpdateLauncher.cs"] = "ProcessPurpose.Outlives",
    };

    /// <summary>
    /// The starts through the gate that no job of the one management holds, each with why: a purpose held by none, and a start the gate
    /// runs without a process to hand on. The direct starts (<see cref="Allowed"/>) are each a program of its own or held in a job of their
    /// own from before their first instruction, as their reasons say.
    /// </summary>
    private static readonly Dictionary<string, string> HeldByNone = new(StringComparer.Ordinal)
    {
        ["Daoris.Desktop.Modules/Loop/HostSupervisor.cs"] =
            "the HTTP host stops itself when its input ends with this process (LOG2a), and a job's kill would cut that stop short",
        ["Daoris.Desktop.App/EngineBrowserHost.cs"] = "the Edge the settings choose, on Daoris's profile, recorded and reused by the next shell",
        ["Daoris.Desktop.Modules/Update/UpdateLauncher.cs"] = "the update's launcher, which replaces this process and outlives it by design",
        ["Daoris.Desktop.Modules/Windows/OpenFolder.cs"] = "the file manager, the system's own, handed the folder by the window kit's launcher",
        ["Daoris.Desktop.Modules/Windows/TerminalModule.cs"] =
            "the terminal's shell, started suspended and held in a job of its own before its first instruction (CONSOLE4a)",
    };

    /// <summary>🔴 Every start through the gate names its purpose as listed, and each start no job holds is named with why.</summary>
    [Fact]
    public void Every_start_through_the_gate_names_its_purpose_and_each_held_by_no_job_says_why()
    {
        var problems = PurposeProblems(Sources());

        Assert.True(problems.Count == 0, "the one management (D249): name each start's purpose in Purposes, and why one no job holds in "
            + "HeldByNone:\n" + string.Join('\n', problems));
    }

    /// <summary>The purposes' census, red on a start planted with no purpose listed and on one held by no job unnamed.</summary>
    [Fact]
    public void A_start_planted_with_an_unlisted_purpose_or_held_by_no_job_is_named_by_the_census()
    {
        var planted = Sources().Append(("Daoris.Desktop.Driver/Loop/Planted.cs",
            "var p = await ProcessStarts.Shared.StartAsync(info, ProcessPurpose.Outlives, StartClass.Background, ct);"));

        Assert.Equal(
            [
                "Daoris.Desktop.Driver/Loop/Planted.cs: a start whose purpose is not listed in Purposes",
                "Daoris.Desktop.Driver/Loop/Planted.cs: held by no job, and not named in HeldByNone with why",
            ],
            PurposeProblems(planted));
    }

    /// <summary>The runtime's kills that stay outside the one management's end (<see cref="OwnedProcesses.End"/>), each with why.</summary>
    private static readonly Dictionary<string, string> OwnKills = new(StringComparer.Ordinal)
    {
        ["Daoris.Desktop.Driver/Platform/OwnedProcesses.cs"] = "the management's own end",
        ["Daoris.Desktop.Driver/Workplace/GitDoors.cs"] =
            "the git doors' program of its own, which git starts and which compiles without the management: the door's ssh ended with it",
        ["Daoris.Desktop.Driver/Workplace/ActReconciler.cs"] =
            "a journaled act step's git an earlier Daoris left, found by its pid, whose refused kill the reconciler reads as not ended",
    };

    [Fact]
    public void Every_process_is_ended_through_the_one_management_but_in_the_files_named_with_their_reason()
    {
        // The runtime's tree kill; a plugin trial's own Kill() ends through the management.
        var killing = Sources().Where(each => Regex.IsMatch(each.Source, @"\.Kill\(\s*(?:entireProcessTree|true|false)\b")).Select(each => each.File).ToHashSet();

        Assert.True(killing.All(OwnKills.ContainsKey), "a process ended outside OwnedProcesses.End: end it there, or name the file in "
            + "OwnKills with why:\n" + string.Join('\n', killing.Where(file => !OwnKills.ContainsKey(file))));
        Assert.All(OwnKills.Keys, file => Assert.True(killing.Contains(file), $"{file} kills nothing itself now: take it off OwnKills"));
    }

    private static List<string> PurposeProblems(IEnumerable<(string File, string Source)> sources)
    {
        var problems = new List<string>();
        var starting = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (file, source) in sources)
        {
            // The management itself names the purposes it joins to none.
            if (file == "Daoris.Desktop.Driver/Platform/OwnedProcesses.cs") continue;
            var starts = Regex.IsMatch(source, @"\bProcessStarts\.Shared\.StartAsync\(|\bstarts\.StartAsync\(");
            var runs = Regex.IsMatch(source, @"\bProcessStarts\.Shared\.RunAsync\(");
            if (starts || runs) starting.Add(file);
            if (starts && !(Purposes.TryGetValue(file, out var purpose) && source.Contains(purpose, StringComparison.Ordinal)))
            {
                problems.Add($"{file}: a start whose purpose is not listed in Purposes");
            }

            var none = runs || Regex.IsMatch(source, @"\bProcessPurpose\.(?:Outlives|EndsItself)\b");
            if (none && !HeldByNone.ContainsKey(file)) problems.Add($"{file}: held by no job, and not named in HeldByNone with why");
        }

        problems.AddRange(Purposes.Keys.Concat(HeldByNone.Keys).Distinct().Where(file => !starting.Contains(file))
            .Select(file => $"{file} starts nothing through the gate now: take it off the list"));
        return problems;
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

    /// <summary>
    /// Every console program the driver, its tests, the devkit and the service start hides its window: a parent with
    /// no console of its own (the merge queue's detached runner) gives each one a visible window on the owner's screen otherwise.
    /// </summary>
    [Fact]
    public void Every_console_start_hides_its_window_but_the_ones_named_with_their_reason()
    {
        var sources = NoConsoleStarts.Read(NoConsoleStarts.Src(), "Daoris.Desktop/Daoris.Desktop.Driver", "Daoris.Desktop/Daoris.Desktop.Driver.Host",
            "Daoris.Desktop/Daoris.Desktop.Driver.Tests", "Daoris.Desktop/Daoris.Desktop.Driver.Rehearsals", "Daoris.Devkit", "Daoris.Service").ToList();
        var (problems, starts) = NoConsoleStarts.Scan(sources);

        Assert.True(starts >= 20, $"expected to find the projects' starts, found {starts}");
        Assert.True(problems.Count == 0, string.Join('\n', problems));
        Assert.Empty(NoConsoleStarts.Stale(sources));
    }

    /// <summary>Every direct start a driver test or rehearsal makes is git past the ceiling or named with its purpose in <see cref="TestProcessStarts"/>.</summary>
    [Fact]
    public void Every_direct_start_a_driver_test_makes_is_named_with_its_purpose()
    {
        var problems = new[] { "Daoris.Desktop.Driver.Tests", "Daoris.Desktop.Driver.Rehearsals" }
            .SelectMany(project => TestProcessStarts.Problems(TestProcessStarts.Sources(project), project)).ToList();

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    /// <summary>A start planted in an unlisted test file is named, and a listed file that starts nothing is taken off.</summary>
    [Fact]
    public void A_test_start_planted_unlisted_is_named_by_the_census()
    {
        var planted = TestProcessStarts.Sources("Daoris.Desktop.Driver.Tests").Append(("Daoris.Desktop.Driver.Tests/Planted.cs", "var p = Process.Start(info);"));

        Assert.Contains("Daoris.Desktop.Driver.Tests/Planted.cs: a direct start of a process, not named",
            TestProcessStarts.Problems(planted, "Daoris.Desktop.Driver.Tests"));
        Assert.Empty(TestProcessStarts.Problems([("Daoris.Desktop.Driver.Tests/Planted.cs", "var p = TestGit.Past(info); Process.Start(info);")], "Other"));
    }

    /// <summary>A start with no window hidden fails; each way of hiding it, and each named exemption, passes.</summary>
    [Fact]
    public void A_console_start_without_CreateNoWindow_fails_the_census()
    {
        var start = "var i = new ProcessStartInfo(\"git\") { UseShellExecute = false";
        Assert.Single(NoConsoleStarts.Scan([("Daoris.Desktop/X.cs", start + " };")]).Problems);
        Assert.Empty(NoConsoleStarts.Scan([("Daoris.Desktop/X.cs", start + ", CreateNoWindow = true };")]).Problems);
        Assert.Empty(NoConsoleStarts.Scan([("Daoris.Desktop/X.cs", start + " };\ni.CreateNoWindow = true;")]).Problems);
        Assert.Empty(NoConsoleStarts.Scan([("Daoris.Desktop/X.cs", start + " }; TestGit.Past(i);")]).Problems);
        Assert.Empty(NoConsoleStarts.Scan([("Daoris.Desktop/X.cs", "var i = new ProcessStartInfo(\"x\");")]).Problems);
        Assert.Single(NoConsoleStarts.Scan([("Daoris.Desktop/X.cs", "var i = new ProcessStartInfo(\"x\") { UseShellExecute = true };")]).Problems);
        Assert.Empty(NoConsoleStarts.Scan([(NoConsoleStarts.Shell.Keys.First(), "var i = new ProcessStartInfo(\"x\") { UseShellExecute = true };")]).Problems);
        var kept = NoConsoleStarts.Windows.Keys.First();
        var marked = "// console-window: why\n" + start + ", CreateNoWindow = false };";
        Assert.Empty(NoConsoleStarts.Scan([(kept, marked)]).Problems);
        Assert.Single(NoConsoleStarts.Scan([("Daoris.Desktop/X.cs", marked)]).Problems);
    }

    private static readonly Lazy<IReadOnlyList<(string File, string Source)>> Read = new(() => [.. ReadSources()]);

    /// <summary>Every production source of the desktop's projects, by its path under the desktop folder, comments read out: read once a run.</summary>
    private static IEnumerable<(string File, string Source)> Sources() => Read.Value;

    private static IEnumerable<(string File, string Source)> ReadSources()
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
