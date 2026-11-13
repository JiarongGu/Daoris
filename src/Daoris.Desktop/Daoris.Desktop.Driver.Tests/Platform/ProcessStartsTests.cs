using System.ComponentModel;
using System.Diagnostics;
using Daoris.Driver;
using Daoris.Ports.Processes;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Process starts, a few at a time and never on the caller's thread: .NET starts every process under one lock, a
/// start an antivirus hook holds keeps it, and a sweep's git fan-out once parked 51 threads on it, the window's among them.
/// </summary>
/// <remarks>The fast half: no process starts. A start is a function handed in, held on a gate the test opens, as a slow
/// <c>CreateProcess</c> holds the lock.</remarks>
public sealed class ProcessStartsTests
{
    // True on the thread that is making the calls, while it makes them: a start that ran there ran on a caller's thread.
    [ThreadStatic]
    private static bool t_calling;

    private static ProcessStartInfo Info() => new("probe") { UseShellExecute = false };

    // The clock a stage's span reads, in milliseconds, moved by hand so a wait's length is the test's to say.
    private long _now;

    private LookStages Stages() => new(clock: () => Volatile.Read(ref _now));

    private void Pass(long ms) => Interlocked.Add(ref _now, ms);

    /// <summary>A gate of one place, taken by a start held until <paramref name="held"/> is set: the place a stage's git waits for.</summary>
    private static ProcessStarts HeldFull(ManualResetEventSlim held, out Task<Process?> holder)
    {
        var starts = new ProcessStarts(new ScriptedProcesses(1), info =>
        {
            if (info.FileName == "holder") held.Wait(Poll.Patience);
            return null;
        });
        holder = starts.StartAsync(new ProcessStartInfo("holder") { UseShellExecute = false, CreateNoWindow = true }, ProcessPurpose.Agent, CancellationToken.None);
        return starts;
    }

    /// <summary>
    /// 🔴 A start a stage's work makes waits for its place, and the stage's line says that wait as its
    /// queue, apart from its hold, so a stage slow for want of a place does not read as slow work.
    /// </summary>
    [Fact]
    public async Task A_start_that_waits_for_a_place_in_a_stage_s_work_is_that_stage_s_queue_apart_from_its_hold()
    {
        using var held = new ManualResetEventSlim();
        var starts = HeldFull(held, out var holder);
        var stages = Stages();

        using (var start = stages.Begin("start", "#q1"))
        {
            var git = start.RunAsync(() => starts.StartAsync(Info(), ProcessPurpose.Git, StartClass.Background,CancellationToken.None));
            Pass(2500);
            held.Set();
            await git.WaitAsync(Poll.Patience);
            Pass(300);
        }

        await holder.WaitAsync(Poll.Patience);
        Assert.Equal("start=300@#q1 start.queue=2500", stages.Take());
    }

    [Fact]
    public async Task A_start_that_finds_its_place_says_no_queue()
    {
        var starts = new ProcessStarts(new ScriptedProcesses(1), _ => null);
        var stages = Stages();

        using (var start = stages.Begin("start", "#q1"))
        {
            await start.RunAsync(() => starts.StartAsync(Info(), ProcessPurpose.Git, StartClass.Background,CancellationToken.None));
            Pass(400);
        }

        Assert.Equal("start=400@#q1", stages.Take());
    }

    /// <summary>
    /// A wait is a stage's only inside the work the stage runs: a span held in a variable, as a look holds each start's, takes none
    /// of its flow's waits, and work that has returned leaves its caller's flow said to nobody. Said nowhere, never to the wrong one.
    /// </summary>
    [Fact]
    public async Task A_wait_in_a_flow_with_no_stage_in_flight_is_said_nowhere()
    {
        using var held = new ManualResetEventSlim();
        var starts = HeldFull(held, out var holder);
        var stages = Stages();

        using (var start = stages.Begin("start", "#q1"))
        {
            await start.RunAsync(() => Task.CompletedTask);
            var git = starts.StartAsync(Info(), ProcessPurpose.Git, StartClass.Background,CancellationToken.None);
            Pass(2500);
            held.Set();
            await git.WaitAsync(Poll.Patience);
        }

        await holder.WaitAsync(Poll.Patience);
        Assert.Equal("start=2500@#q1", stages.Take());
    }

    /// <summary>A stage within another's work (a plugin's say within a start) takes the waits of its own work, the innermost's alone.</summary>
    [Fact]
    public async Task A_wait_in_a_stage_within_another_is_the_inner_stage_s_alone()
    {
        using var held = new ManualResetEventSlim();
        var starts = HeldFull(held, out var holder);
        var stages = Stages();

        using (var start = stages.Begin("start", "#q1"))
        {
            await start.RunAsync(async () =>
            {
                Pass(100);
                await stages.TimeAsync("consider", async () =>
                {
                    var plugin = starts.StartAsync(Info(), ProcessPurpose.Git, StartClass.Background,CancellationToken.None);
                    Pass(2000);
                    held.Set();
                    await plugin.WaitAsync(Poll.Patience);
                }, "#q1");
                Pass(300);
            });
        }

        await holder.WaitAsync(Poll.Patience);
        Assert.Equal("start=2400@#q1 consider.queue=2000", stages.Take());
    }

    /// <summary>Work begun beside a stage is not the stage's: its waits are said to none until a stage of its own takes them.</summary>
    [Fact]
    public async Task Work_begun_beside_a_stage_says_its_waits_to_none_but_a_stage_of_its_own()
    {
        using var held = new ManualResetEventSlim();
        var starts = HeldFull(held, out var holder);
        var stages = Stages();

        using (var landing = stages.Begin("landing"))
        {
            await landing.RunAsync(async () =>
            {
                var beside = ProcessStarts.QueuedToAsync(null, () => starts.StartAsync(Info(), ProcessPurpose.Git, StartClass.Background,CancellationToken.None));
                Pass(2000);
                held.Set();
                await beside.WaitAsync(Poll.Patience);
            });
        }

        await holder.WaitAsync(Poll.Patience);
        Assert.Equal("landing=2000", stages.Take());
    }

    /// <summary>A wait given up when its caller closes is still a wait: the stage's queue says it.</summary>
    [Fact]
    public async Task A_wait_given_up_is_still_the_queue_it_waited()
    {
        using var held = new ManualResetEventSlim();
        var starts = HeldFull(held, out var holder);
        var stages = Stages();
        using var closing = new CancellationTokenSource();

        using (var start = stages.Begin("start", "#q1"))
        {
            var git = start.RunAsync(() => starts.StartAsync(Info(), ProcessPurpose.Git, StartClass.Background,closing.Token));
            Pass(700);
            await closing.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => git);
            Pass(50);
        }

        held.Set();
        await holder.WaitAsync(Poll.Patience);
        Assert.Equal("start=50@#q1 start.queue=700", stages.Take());
    }

    [Fact]
    public async Task A_start_is_placed_under_its_class()
    {
        var places = new ScriptedProcesses(4);
        var starts = new ProcessStarts(places, _ => null);

        await starts.StartAsync(Info(), ProcessPurpose.Agent, StartClass.Person, CancellationToken.None);
        await starts.StartAsync(Info(), ProcessPurpose.Tool, StartClass.Background, CancellationToken.None);

        Assert.Equal([AdmissionClasses.Person, AdmissionClasses.Background], places.Asked);
        Assert.Equal(4, places.Free);
    }

    [Fact]
    public async Task Many_starts_at_once_hold_at_most_the_bound_and_no_caller_thread_runs_one()
    {
        using var held = new ManualResetEventSlim();
        var inside = 0;
        var most = 0;
        var onCaller = 0;
        var places = new ScriptedProcesses(4);
        var starts = new ProcessStarts(places, _ =>
        {
            if (t_calling) Interlocked.Increment(ref onCaller);
            var now = Interlocked.Increment(ref inside);
            for (var seen = Volatile.Read(ref most); now > seen; seen = Volatile.Read(ref most))
            {
                if (Interlocked.CompareExchange(ref most, now, seen) == seen) break;
            }

            // A CreateProcess an antivirus hook holds, as long as the test says.
            held.Wait(Poll.Patience);
            Interlocked.Decrement(ref inside);
            return null;
        });

        List<Task<Process?>> calls;
        t_calling = true;
        try
        {
            calls = [.. Enumerable.Range(0, 40).Select(_ => starts.StartAsync(Info(), ProcessPurpose.Agent, CancellationToken.None))];
        }
        finally
        {
            t_calling = false;
        }

        // Every call comes back to its caller while every start it made stays held.
        Assert.All(calls, call => Assert.False(call.IsCompleted));
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref inside) == 4, Poll.Patience), "four starts never ran");
        // The rest wait on the gate, not on a thread: given the time, none more goes in.
        await Task.Delay(200);
        Assert.Equal(4, Volatile.Read(ref inside));

        held.Set();
        await Task.WhenAll(calls).WaitAsync(Poll.Patience);

        Assert.Equal(4, most);
        Assert.Equal(0, onCaller);
        Assert.Equal(4, places.Free);
    }

    [Fact]
    public async Task A_call_cancelled_while_it_waits_for_the_gate_throws_and_takes_no_place()
    {
        using var held = new ManualResetEventSlim();
        var ran = 0;
        var places = new ScriptedProcesses(1);
        var starts = new ProcessStarts(places, _ =>
        {
            Interlocked.Increment(ref ran);
            held.Wait(Poll.Patience);
            return null;
        });

        var first = starts.StartAsync(Info(), ProcessPurpose.Agent, CancellationToken.None);
        using var cancel = new CancellationTokenSource();
        var second = starts.StartAsync(Info(), ProcessPurpose.Agent, cancel.Token);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        held.Set();
        Assert.Null(await first.WaitAsync(Poll.Patience));
        Assert.Equal(1, ran);
        Assert.Equal(1, places.Free);
    }

    [Fact]
    public async Task A_start_the_runtime_refuses_is_the_runners_not_started_with_its_error()
    {
        var refused = new Win32Exception(2, "The system cannot find the file specified.");
        var places = new ScriptedProcesses(2);
        var starts = new ProcessStarts(places, _ => throw refused);

        var run = await ChildProcess.RunAsync(Info(), TimeSpan.FromSeconds(5), CancellationToken.None, closeInput: false, starts);

        Assert.False(run.Started);
        Assert.Same(refused, run.Error);
        Assert.Equal(2, places.Free);
    }

    [Fact]
    public async Task A_start_that_answers_no_process_is_the_runners_not_started()
    {
        var starts = new ProcessStarts(new ScriptedProcesses(2), _ => null);

        var run = await ChildProcess.RunAsync(Info(), TimeSpan.FromSeconds(5), CancellationToken.None, closeInput: false, starts);

        Assert.False(run.Started);
        Assert.Null(run.Error);
    }
}
