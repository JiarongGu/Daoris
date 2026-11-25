using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An agent process of Daoris's toolchain left running when its session has ended is named in the machine log with its
/// id, pin and age and ended by the look's reaper after a grace (at once where its pin has moved on); a process of another program
/// running the same file, which Daoris never recorded, is never touched. The stand-in agent is <c>node</c> running a script laid out
/// as an installed pin's, never an agent.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class StrayAgentsProcessTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-stray-agents-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<Process> _started = [];
    private DateTimeOffset _now = Start;

    public StrayAgentsProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var process in _started)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Already gone.
            }

            process.Dispose();
        }

        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Markers => Path.Combine(_root, "run", "sessions");

    private string Toolchain => Path.Combine(_root, "cache", "toolchain");

    private string ScriptOf(string pin)
    {
        var script = Path.Combine(Toolchain, "claude-code-acp", pin, "node_modules", "agent", "index.js");
        Directory.CreateDirectory(Path.GetDirectoryName(script)!);
        File.WriteAllText(script, "setInterval(() => {}, 1000);");
        return script;
    }

    private Process Run(string script)
    {
        var info = new ProcessStartInfo("node")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, WorkingDirectory = _root,
        };
        info.ArgumentList.Add(script);
        var process = Process.Start(info)!;
        _started.Add(process);
        return process;
    }

    private StrayAgents Reaper(params string[] pins) =>
        new(Markers, Toolchain, _ => new HashSet<string>(pins, StringComparer.Ordinal), () => _now);

    private static bool Alive(Process process)
    {
        process.Refresh();
        return !process.HasExited;
    }

    private static async Task Ended(Process process)
    {
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(bound.Token);
    }

    private static readonly IReadOnlySet<string> None = new HashSet<string>(StringComparer.Ordinal);

    [Fact]
    public async Task An_agent_left_from_an_ended_session_is_said_then_ended_after_its_grace_and_another_program_at_its_path_is_never_touched()
    {
        var script = ScriptOf("0.109.1");
        var left = Run(script);
        var other = Run(script);
        StartRecords.Write(Markers, "s-ended", left);
        var reaper = Reaper("0.109.1");

        var first = reaper.Look(None, _ => false);
        var line = Assert.Single(first);
        Assert.Contains($"Agent process {left.Id} (claude-code-acp 0.109.1", line);
        Assert.Contains("session s-ended", line);
        Assert.True(Alive(left));

        _now += StrayAgents.Grace - TimeSpan.FromSeconds(1);
        Assert.Empty(reaper.Look(None, _ => false));
        Assert.True(Alive(left));

        _now += TimeSpan.FromSeconds(2);
        var ended = Assert.Single(reaper.Look(None, _ => false));
        Assert.Contains("ended", ended);
        await Ended(left);
        Assert.True(Alive(other));
        Assert.Empty(StartRecords.Read(Markers));
    }

    [Fact]
    public async Task An_agent_at_a_pin_that_has_moved_on_is_ended_at_the_look_that_finds_it()
    {
        var left = Run(ScriptOf("0.106.0"));
        StartRecords.Write(Markers, "s-ended", left);

        var said = Reaper("0.109.1").Look(None, _ => false);

        Assert.Equal(2, said.Count);
        Assert.Contains("0.106.0", said[0]);
        Assert.Contains("pin that has moved on", said[0]);
        await Ended(left);
    }

    [Fact]
    public void A_process_of_a_session_the_service_lists_or_this_registry_runs_is_left_alone_and_its_time_cleared()
    {
        var left = Run(ScriptOf("0.109.1"));
        StartRecords.Write(Markers, "s-working", left);
        var reaper = Reaper("0.109.1");

        Assert.Single(reaper.Look(None, _ => false));
        Assert.NotNull(Assert.Single(StartRecords.Read(Markers)).Start.Seen);

        _now += StrayAgents.Grace * 2;
        Assert.Empty(reaper.Look(new HashSet<string>(["s-working"], StringComparer.Ordinal), _ => false));
        Assert.Null(Assert.Single(StartRecords.Read(Markers)).Start.Seen);
        Assert.Empty(reaper.Look(None, session => session == "s-working"));
        Assert.True(Alive(left));
    }

    [Fact]
    public async Task A_record_of_a_process_that_has_gone_is_removed_and_a_program_outside_the_toolchain_is_not_reaped()
    {
        var gone = Run(ScriptOf("0.109.1"));
        StartRecords.Write(Markers, "s-gone", gone);
        gone.Kill(entireProcessTree: true);
        await Ended(gone);

        var elsewhere = Path.Combine(_root, "elsewhere.js");
        File.WriteAllText(elsewhere, "setInterval(() => {}, 1000);");
        var outside = Run(elsewhere);
        StartRecords.Write(Markers, "s-outside", outside);

        var reaper = Reaper("0.109.1");
        _now += StrayAgents.Grace * 2;
        Assert.Empty(reaper.Look(None, _ => false));
        Assert.Empty(reaper.Look(None, _ => false));

        Assert.True(Alive(outside));
        Assert.Equal("s-outside", Assert.Single(StartRecords.Read(Markers)).Start.Session);
    }

    [Fact]
    public async Task A_tracked_process_leaves_its_record_and_takes_it_when_it_ends()
    {
        var script = ScriptOf("0.109.1");
        var processes = new SessionProcesses(Markers);
        var info = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true };
        info.ArgumentList.Add(script);
        var process = Process.Start(info)!;
        _started.Add(process);

        var tracking = processes.Track("s-tracked", process);
        var record = Assert.Single(StartRecords.Read(Markers));
        Assert.Equal(("claude-code-acp", "0.109.1"), record.Start.Toolchain(Toolchain));
        Assert.Equal(process.Id, record.Start.Pid);

        tracking.Dispose();
        await Ended(process);
        // The tracking's end took the process with its job; a process still exiting leaves its record to the next look, which removes it.
        Reaper("0.109.1").Look(None, _ => false);
        Assert.Empty(StartRecords.Read(Markers));
    }
}
