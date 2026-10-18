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

    [Fact]
    public void The_shared_gate_lets_four_start_at_once()
    {
        Assert.Equal(4, ProcessStarts.Bound);
        // Its places, not its free ones: the suite's other classes start git through the shared gate in parallel, so how many
        // are free at this moment is theirs to change (the merge's driver gate saw 3).
        Assert.Equal(ProcessStarts.Bound, ProcessStarts.Shared.Places);
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
            held.Wait(TimeSpan.FromSeconds(30));
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
        await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(30));

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
            held.Wait(TimeSpan.FromSeconds(30));
            return null;
        });

        var first = starts.StartAsync(Info(), CancellationToken.None);
        using var cancel = new CancellationTokenSource();
        var second = starts.StartAsync(Info(), cancel.Token);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        held.Set();
        Assert.Null(await first.WaitAsync(TimeSpan.FromSeconds(30)));
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
