using System.ComponentModel;
using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Process starts, a few at a time and never on the caller's thread (UIFREEZE1): .NET starts every process under one lock, a
/// start an antivirus hook holds keeps it, and a sweep's git fan-out once parked 51 threads on it, the window's among them.
/// </summary>
/// <remarks>
/// The fast half: no process starts. A start is a function handed in, held on a gate the test opens, as a slow
/// <c>CreateProcess</c> holds the lock.
/// </remarks>
public sealed class ProcessStartsTests
{
    // True on the thread that is making the calls, while it makes them: a start that ran there ran on a caller's thread.
    [ThreadStatic]
    private static bool t_calling;

    private static ProcessStartInfo Info() => new("git") { UseShellExecute = false };

    // LOOKWAIT1b: the clock a stage's span reads, in milliseconds, moved by hand so a wait's length is the test's to say.
    private long _now;

    private LookStages Stages() => new(clock: () => Volatile.Read(ref _now));

    private void Pass(long ms) => Interlocked.Add(ref _now, ms);

    /// <summary>A gate of one place, taken by a start held until <paramref name="held"/> is set: the place a stage's git waits for.</summary>
    private static ProcessStarts HeldFull(ManualResetEventSlim held, out Task<Process?> holder)
    {
        var starts = new ProcessStarts(1, info =>
        {
            if (info.FileName == "holder") held.Wait(Poll.Patience);
            return null;
        });
        holder = starts.StartAsync(new ProcessStartInfo("holder") { UseShellExecute = false }, CancellationToken.None);
        return starts;
    }

    /// <summary>
    /// 🔴 LOOKWAIT1b (D181 point 6): a start a stage's work makes waits for its place, and the stage's line says that wait as its
    /// queue, apart from its hold, so a stage slow for want of a place no longer reads as slow work.
    /// </summary>
    [Fact]
    public async Task A_start_that_waits_for_a_place_in_a_stage_s_work_is_that_stage_s_queue_apart_from_its_hold()
    {
        using var held = new ManualResetEventSlim();
        var starts = HeldFull(held, out var holder);
        var stages = Stages();

        using (var start = stages.Begin("start", "#q1"))
        {
            var git = start.RunAsync(() => starts.StartAsync(Info(), StartClass.Background, CancellationToken.None));
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
        var starts = new ProcessStarts(1, _ => null);
        var stages = Stages();

        using (var start = stages.Begin("start", "#q1"))
        {
            await start.RunAsync(() => starts.StartAsync(Info(), StartClass.Background, CancellationToken.None));
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
            var git = starts.StartAsync(Info(), StartClass.Background, CancellationToken.None);
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
                    var plugin = starts.StartAsync(Info(), StartClass.Background, CancellationToken.None);
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
                var beside = ProcessStarts.QueuedToAsync(null, () => starts.StartAsync(Info(), StartClass.Background, CancellationToken.None));
                Pass(2000);
                held.Set();
                await beside.WaitAsync(Poll.Patience);
            });
        }

        await holder.WaitAsync(Poll.Patience);
        Assert.Equal("landing=2000", stages.Take());
    }

    /// <summary>A wait given up when its caller closes was still waited: the stage's queue says it.</summary>
    [Fact]
    public async Task A_wait_given_up_is_still_the_queue_it_waited()
    {
        using var held = new ManualResetEventSlim();
        var starts = HeldFull(held, out var holder);
        var stages = Stages();
        using var closing = new CancellationTokenSource();

        using (var start = stages.Begin("start", "#q1"))
        {
            var git = start.RunAsync(() => starts.StartAsync(Info(), StartClass.Background, closing.Token));
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
    public void The_shared_gate_lets_four_start_at_once()
    {
        Assert.Equal(4, ProcessStarts.Bound);
        // Its places, not its free ones: the suite's other classes start git through the shared gate in parallel, so how many
        // are free at this moment is theirs to change (the merge's driver gate saw 3).
        Assert.Equal(ProcessStarts.Bound, ProcessStarts.Shared.Places);
    }

    /// <summary>PARALLEL1d (D181 point 5): the background takes three of the four, so one is always a person's.</summary>
    [Fact]
    public void The_shared_gate_leaves_one_of_its_four_places_to_a_person()
    {
        Assert.Equal(ProcessStarts.Bound - 1, ProcessStarts.BackgroundBound);
        Assert.Equal(ProcessStarts.BackgroundBound, ProcessStarts.Shared.BackgroundPlaces);
    }

    /// <summary>
    /// The row's proof (PARALLEL1d): four slow background starts held, as a git fan-out's are in an antivirus scan, and a
    /// person's agent start still starts, at once, while they are held.
    /// </summary>
    [Fact]
    public async Task Four_background_starts_held_leave_a_persons_start_its_place()
    {
        using var held = new ManualResetEventSlim();
        var inside = 0;
        var starts = new ProcessStarts(4, 3, info =>
        {
            if (info.FileName == "agent") return null;
            Interlocked.Increment(ref inside);
            held.Wait(Poll.Patience);
            Interlocked.Decrement(ref inside);
            return null;
        });

        var git = Enumerable.Range(0, 4).Select(_ => starts.StartAsync(Info(), StartClass.Background, CancellationToken.None)).ToList();
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref inside) == 3, Poll.Patience), "three background starts never ran");

        var agent = starts.StartAsync(new ProcessStartInfo("agent") { UseShellExecute = false }, CancellationToken.None);

        Assert.Null(await agent.WaitAsync(Poll.Patience));
        // Every git is still held or waiting, and the fourth never went in: the person's place is never the background's.
        Assert.All(git, call => Assert.False(call.IsCompleted));
        Assert.Equal(3, Volatile.Read(ref inside));

        held.Set();
        await Task.WhenAll(git).WaitAsync(Poll.Patience);
        Assert.Equal(4, starts.Free);
    }

    /// <summary>The background bound holds: a fan-out of forty never has more than its three under way.</summary>
    [Fact]
    public async Task Background_starts_never_hold_more_than_their_places()
    {
        using var held = new ManualResetEventSlim();
        var inside = 0;
        var most = 0;
        var starts = new ProcessStarts(4, 3, _ =>
        {
            var now = Interlocked.Increment(ref inside);
            for (var seen = Volatile.Read(ref most); now > seen; seen = Volatile.Read(ref most))
            {
                if (Interlocked.CompareExchange(ref most, now, seen) == seen) break;
            }

            held.Wait(Poll.Patience);
            Interlocked.Decrement(ref inside);
            return null;
        });

        var calls = Enumerable.Range(0, 40).Select(_ => starts.StartAsync(Info(), StartClass.Background, CancellationToken.None)).ToList();
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref inside) == 3, Poll.Patience), "three starts never ran");
        await Task.Delay(200);
        Assert.Equal(3, Volatile.Read(ref inside));
        Assert.Equal(1, starts.Free);

        held.Set();
        await Task.WhenAll(calls).WaitAsync(Poll.Patience);
        Assert.Equal(3, most);
        Assert.Equal(4, starts.Free);
    }

    /// <summary>A place that frees goes to a person waiting before the background waiting, whichever asked first.</summary>
    [Fact]
    public async Task A_freed_place_goes_to_the_person_waiting_before_the_background()
    {
        var holds = new Dictionary<string, ManualResetEventSlim>
        {
            ["person-1"] = new(), ["git-1"] = new(), ["git-2"] = new(), ["person-2"] = new(),
        };
        var ran = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var starts = new ProcessStarts(2, 1, info =>
        {
            ran.Enqueue(info.FileName);
            holds[info.FileName].Wait(Poll.Patience);
            return null;
        });
        Task<Process?> Start(string name, StartClass whose) =>
            starts.StartAsync(new ProcessStartInfo(name) { UseShellExecute = false }, whose, CancellationToken.None);

        try
        {
            var person1 = Start("person-1", StartClass.Person);
            var git1 = Start("git-1", StartClass.Background);
            Assert.True(SpinWait.SpinUntil(() => ran.Count == 2, Poll.Patience), "the first two never ran");
            // The gate is full: the background asks first, then a person.
            var git2 = Start("git-2", StartClass.Background);
            var person2 = Start("person-2", StartClass.Person);
            await Task.Delay(100);
            Assert.Equal(2, ran.Count);

            // The background's place frees: both waiting may take it, and the person does.
            holds["git-1"].Set();
            await git1.WaitAsync(Poll.Patience);
            Assert.True(SpinWait.SpinUntil(() => ran.Count == 3, Poll.Patience), "nothing took the freed place");
            Assert.Equal("person-2", ran.Last());
            Assert.False(git2.IsCompleted);

            foreach (var hold in holds.Values) hold.Set();
            await Task.WhenAll(person1, git2, person2).WaitAsync(Poll.Patience);
            Assert.Equal(2, starts.Free);
        }
        finally
        {
            foreach (var hold in holds.Values)
            {
                hold.Set();
                hold.Dispose();
            }
        }
    }

    [Fact]
    public async Task A_background_call_cancelled_while_it_waits_takes_no_place_and_the_next_goes_on()
    {
        using var held = new ManualResetEventSlim();
        var ran = 0;
        var starts = new ProcessStarts(2, 1, _ =>
        {
            Interlocked.Increment(ref ran);
            held.Wait(Poll.Patience);
            return null;
        });

        var first = starts.StartAsync(Info(), StartClass.Background, CancellationToken.None);
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref ran) == 1, Poll.Patience), "the first never ran");
        using var cancel = new CancellationTokenSource();
        var second = starts.StartAsync(Info(), StartClass.Background, cancel.Token);
        var third = starts.StartAsync(Info(), StartClass.Background, CancellationToken.None);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        held.Set();
        await Task.WhenAll(first, third).WaitAsync(Poll.Patience);
        Assert.Equal(2, Volatile.Read(ref ran));
        Assert.Equal(2, starts.Free);
    }

    [Fact]
    public async Task Many_starts_at_once_hold_at_most_the_bound_and_no_caller_thread_runs_one()
    {
        using var held = new ManualResetEventSlim();
        var inside = 0;
        var most = 0;
        var onCaller = 0;
        var starts = new ProcessStarts(4, _ =>
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
            calls = [.. Enumerable.Range(0, 40).Select(_ => starts.StartAsync(Info(), CancellationToken.None))];
        }
        finally
        {
            t_calling = false;
        }

        // Every call came back to its caller while every start it made was still held.
        Assert.All(calls, call => Assert.False(call.IsCompleted));
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref inside) == 4, Poll.Patience), "four starts never ran");
        // The rest wait on the gate, not on a thread: given the time, none more goes in.
        await Task.Delay(200);
        Assert.Equal(4, Volatile.Read(ref inside));

        held.Set();
        await Task.WhenAll(calls).WaitAsync(Poll.Patience);

        Assert.Equal(4, most);
        Assert.Equal(0, onCaller);
        Assert.Equal(4, starts.Free);
    }

    [Fact]
    public async Task A_call_cancelled_while_it_waits_for_the_gate_throws_and_takes_no_place()
    {
        using var held = new ManualResetEventSlim();
        var ran = 0;
        var starts = new ProcessStarts(1, _ =>
        {
            Interlocked.Increment(ref ran);
            held.Wait(Poll.Patience);
            return null;
        });

        var first = starts.StartAsync(Info(), CancellationToken.None);
        using var cancel = new CancellationTokenSource();
        var second = starts.StartAsync(Info(), cancel.Token);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        held.Set();
        Assert.Null(await first.WaitAsync(Poll.Patience));
        Assert.Equal(1, ran);
        Assert.Equal(1, starts.Free);
    }

    [Fact]
    public async Task A_start_the_runtime_refuses_is_the_runners_not_started_with_its_error()
    {
        var refused = new Win32Exception(2, "The system cannot find the file specified.");
        var starts = new ProcessStarts(2, _ => throw refused);

        var run = await ChildProcess.RunAsync(Info(), TimeSpan.FromSeconds(5), CancellationToken.None, closeInput: false, starts);

        Assert.False(run.Started);
        Assert.Same(refused, run.Error);
        Assert.Equal(2, starts.Free);
    }

    [Fact]
    public async Task A_start_that_answers_no_process_is_the_runners_not_started()
    {
        var starts = new ProcessStarts(2, _ => null);

        var run = await ChildProcess.RunAsync(Info(), TimeSpan.FromSeconds(5), CancellationToken.None, closeInput: false, starts);

        Assert.False(run.Started);
        Assert.Null(run.Error);
    }
}
