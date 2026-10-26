using System.Diagnostics;
using Daoris.Driver;
using StandInService = Daoris.Desktop.Driver.Tests.OrphanedSessionTests.StandInService;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSPROC1: a process's end takes away its own entry and its own marker, never the live one a second start of the same record
/// stood beside. The registry used to keep one entry per session, so the second start's tracking replaced the live one and its end
/// removed whatever the session had and deleted the marker: the window's stop found none, and another Daoris's orphan sweep read the
/// record as run by nothing and stopped it while it ran (the cowork view design §9, D173's note accepting COWORKVIEW1 row a).
/// </summary>
/// <remarks>
/// Stand-in processes (node holding a timer, never an agent), each test in a scratch home of its own under the repository's scratch
/// folder, and the sweep the real one over a stand-in service.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class SessionProcessesProcessTests : IDisposable
{
    private const string Session = "a1b2c3d4";

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "session-processes", Guid.NewGuid().ToString("N")[..8]);

    private readonly List<Process> _spawned = [];

    private string Markers => DaorisHome.Entry(_home, HomeEntry.Sessions);

    public SessionProcessesProcessTests() => Directory.CreateDirectory(Path.Combine(_home, "engine"));

    public void Dispose()
    {
        foreach (var process in _spawned)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            process.Dispose();
        }

        GitSeed.Discard(_home);
    }

    private Process Holding()
    {
        var process = Process.Start(new ProcessStartInfo("node", ["-e", "setTimeout(() => {}, 60000)"]) { UseShellExecute = false })!;
        _spawned.Add(process);
        return process;
    }

    private string MarkedProcess() => File.ReadAllText(Path.Combine(Markers, Session + ".pid")).Split(' ')[0];

    /// <summary>One track and its end, as before: running and marked while it lasts, gone and unmarked once it ends.</summary>
    [Fact]
    public void One_track_and_its_end_leave_the_session_unrun_and_unmarked()
    {
        var processes = new SessionProcesses(Markers);
        var process = Holding();

        var tracked = processes.Track(Session, process);
        Assert.Equal([Session], processes.Running);
        Assert.Equal(process.Id.ToString(), MarkedProcess());
        Assert.True(new SessionProcesses(Markers).AliveOnThisMachine(Session));

        tracked.Dispose();
        Assert.Empty(processes.Running);
        Assert.False(processes.RunsHere(Session));
        Assert.Empty(Directory.GetFiles(Markers));
        Assert.False(new SessionProcesses(Markers).AliveOnThisMachine(Session));
        Assert.False(processes.Stop(Session));
    }

    /// <summary>The second start's end, the first still running: still run here, stoppable from the window, marked as the first's.</summary>
    [Fact]
    public async Task A_second_start_that_ends_leaves_the_live_one_running_stoppable_and_marked()
    {
        var processes = new SessionProcesses(Markers);
        var first = Holding();
        var second = Holding();

        using var firstTracked = processes.Track(Session, first);
        var secondTracked = processes.Track(Session, second);
        secondTracked.Dispose();

        Assert.Equal([Session], processes.Running);
        Assert.True(processes.RunsHere(Session));
        Assert.Equal(first.Id.ToString(), MarkedProcess());
        Assert.True(new SessionProcesses(Markers).AliveOnThisMachine(Session));

        Assert.True(processes.Stop(Session));
        await first.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(first.HasExited);
        Assert.True(processes.WasStopRequested(Session));
    }

    /// <summary>Another Daoris's sweep, reading the home's markers, leaves the record the live process runs.</summary>
    [Fact]
    public async Task The_sweep_of_another_daoris_leaves_a_record_whose_live_run_a_second_start_stood_beside()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        service.Seed(Session, "working");
        var processes = new SessionProcesses(Markers);

        using var firstTracked = processes.Track(Session, Holding());
        processes.Track(Session, Holding()).Dispose();

        using var client = new ServiceClient(service.Url, null);
        var ended = await Orphans.EndAsync(client, new SessionProcesses(Markers));

        Assert.Empty(ended);
        Assert.Equal("working", service.State(Session));
    }

    /// <summary>The first's own end afterwards takes its entry and its marker, the second having ended before it.</summary>
    [Fact]
    public void The_first_ending_after_the_second_removes_its_entry_and_its_marker()
    {
        var processes = new SessionProcesses(Markers);
        var firstTracked = processes.Track(Session, Holding());
        processes.Track(Session, Holding()).Dispose();

        firstTracked.Dispose();

        Assert.Empty(processes.Running);
        Assert.Empty(Directory.GetFiles(Markers));
        Assert.False(new SessionProcesses(Markers).AliveOnThisMachine(Session));
    }

    /// <summary>The first ending while the second still runs leaves the second kept, marked as it, and answering the window's stop.</summary>
    [Fact]
    public async Task The_first_ending_while_the_second_runs_leaves_the_second_running_and_marked()
    {
        var processes = new SessionProcesses(Markers);
        var second = Holding();
        var firstTracked = processes.Track(Session, Holding());
        using var secondTracked = processes.Track(Session, second);

        firstTracked.Dispose();

        Assert.Equal([Session], processes.Running);
        Assert.Equal(second.Id.ToString(), MarkedProcess());
        Assert.True(new SessionProcesses(Markers).AliveOnThisMachine(Session));
        Assert.True(processes.Stop(Session));
        await second.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>Ending one's tracking twice takes nothing of the other's.</summary>
    [Fact]
    public void An_end_told_twice_takes_only_its_own_once()
    {
        var processes = new SessionProcesses(Markers);
        var first = Holding();
        using var firstTracked = processes.Track(Session, first);
        var secondTracked = processes.Track(Session, Holding());

        secondTracked.Dispose();
        secondTracked.Dispose();

        Assert.Equal([Session], processes.Running);
        Assert.Equal(first.Id.ToString(), MarkedProcess());
    }

    /// <summary>
    /// A run that exited and was replaced by its continuation is replaced as ever (a stop reads the continuation's flags), and its
    /// late end takes neither the continuation nor its marker.
    /// </summary>
    [Fact]
    public async Task A_run_that_exited_is_replaced_and_its_late_end_leaves_its_continuation()
    {
        var processes = new SessionProcesses(Markers);
        var run = Holding();
        var firstTracked = processes.Track(Session, run);
        run.Kill(entireProcessTree: true);
        await run.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var continuation = Holding();
        using var continued = processes.Track(Session, continuation);

        firstTracked.Dispose();

        Assert.Equal([Session], processes.Running);
        Assert.Equal(continuation.Id.ToString(), MarkedProcess());
        Assert.True(new SessionProcesses(Markers).AliveOnThisMachine(Session));
    }

    /// <summary>
    /// Both running, the newest answers, as the only one did before a second was kept: the window's stop ends it, its flags are what
    /// the conclusion reads, and the older one answers again once it is gone.
    /// </summary>
    [Fact]
    public async Task While_both_run_the_newest_answers_and_the_older_answers_once_it_is_gone()
    {
        var processes = new SessionProcesses(Markers);
        var first = Holding();
        var second = Holding();
        using var firstTracked = processes.Track(Session, first, refusesInput: "the first takes no line");
        var secondTracked = processes.Track(Session, second);

        Assert.Null(processes.RefusesInput(Session));
        Assert.True(processes.Stop(Session));
        await second.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(processes.WasStopRequested(Session));
        Assert.False(first.HasExited);

        // The newest has exited and is not yet untracked: the one still running answers, and the run stopped still reads its own stop.
        Assert.Equal("the first takes no line", processes.RefusesInput(Session));
        Assert.True(processes.WasStopRequested(Session));
        Assert.NotNull(processes.StopMade(Session));

        secondTracked.Dispose();
        Assert.Equal("the first takes no line", processes.RefusesInput(Session));
        Assert.False(processes.WasStopRequested(Session));
        Assert.Equal(first.Id.ToString(), MarkedProcess());
    }
}
