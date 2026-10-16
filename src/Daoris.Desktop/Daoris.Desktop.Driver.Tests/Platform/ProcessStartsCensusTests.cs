using System.Text.RegularExpressions;

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
        ["Daoris.Desktop.App/Program.cs"] = (1, "Relaunch: the launcher, through the shell (no CreateProcess lock), once, as the application closes for an update; "
            + "the updater's look that asks it is synchronous"),
        ["Daoris.Desktop.Driver/Agents/Consoles/PseudoConsole.cs"] = (2, "a terminal's shell under a pseudo-console, which .NET cannot start: "
            + "CreateProcessW, declared and called once; the terminal module runs that start through the gate's RunAsync"),
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
