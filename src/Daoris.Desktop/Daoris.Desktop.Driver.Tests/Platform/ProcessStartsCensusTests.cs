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
        ["Daoris.Desktop.Launcher/Program.cs"] = (1, "the launcher is a program of its own, with no window and no bridge: it starts the application once and exits"),
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

    /// <summary>The starts the row named, and the file manager's three doors: each passes the gate.</summary>
    [Theory]
    [InlineData("Daoris.Desktop.Driver/Loop/Driver.cs", "ProcessStarts.Shared.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Loop/DriverContinuations.cs", "ProcessStarts.Shared.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Conversations/Chat/ChatRunner.cs", "ProcessStarts.Shared.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Plugins/HookProcess.cs", "ProcessStarts.Shared.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Accounts/Harnesses/HarnessActions.cs", "ProcessStarts.Shared.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Accounts/Usage/CodexUsage.cs", "ProcessStarts.Shared.StartAsync(")]
    [InlineData("Daoris.Desktop.Driver/Plugins/PluginTrialProcess.cs", "ProcessStarts.Shared.StartAsync(")]
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
            if (!Regex.IsMatch(source, @"\bProcessStarts\.Shared\.(?:StartAsync|RunAsync)\(")) continue;
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
