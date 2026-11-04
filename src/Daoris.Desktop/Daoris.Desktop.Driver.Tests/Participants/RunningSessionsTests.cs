using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The sessions a driver keeps between looks (DEV3): what a look is answered about a start, and what waits
/// for a later look's report.
/// </summary>
public sealed class RunningSessionsTests
{
    /// <summary>A hold or a refusal is the look's own to say, and nothing of it is kept for a later look.</summary>
    [Fact]
    public async Task A_start_that_comes_to_nothing_before_it_opens_is_answered_to_its_look_and_not_kept()
    {
        var running = new RunningSessions();

        var came = await running.StartAsync(_ => Task.FromResult(new StartRun("held  #q1 → engine: dirty", false, Held: "dirty")));

        Assert.Equal("dirty", came?.Held);
        Assert.True(running.Idle);
        Assert.Empty(running.Drain());
    }

    /// <summary>An error before the record opened fails the look, as it always did.</summary>
    [Fact]
    public async Task An_error_before_the_record_opens_fails_the_look()
    {
        var running = new RunningSessions();

        await Assert.ThrowsAsync<HttpRequestException>(() => running.StartAsync(async _ =>
        {
            await Task.Yield();
            throw new HttpRequestException("the service is not answering");
        }));
        Assert.True(running.Idle);
    }

    /// <summary>
    /// Once its record is open, a run is answered at once and kept: its ending waits, once, for the next report.
    /// </summary>
    [Fact]
    public async Task An_opened_run_is_kept_until_it_ends_and_its_ending_is_handed_on_once()
    {
        var running = new RunningSessions();
        var end = new TaskCompletionSource();

        var came = await running.StartAsync(async opened =>
        {
            opened();
            await end.Task;
            return new StartRun("completed  session s1 (#q1 → engine): done.", true);
        }).WaitAsync(Poll.Patience);

        Assert.Null(came);
        Assert.Equal(1, running.Running);
        Assert.False(running.Idle);
        Assert.Empty(running.Drain());

        end.SetResult();
        await running.EndedAsync().WaitAsync(Poll.Patience);

        Assert.Equal("completed  session s1 (#q1 → engine): done.", Assert.Single(running.Drain()).Line);
        Assert.Empty(running.Drain());
        Assert.True(running.Idle);
    }

    /// <summary>An error past the record's opening is said in the next report, never lost with the look that started it.</summary>
    [Fact]
    public async Task An_error_after_the_record_opens_is_said_in_the_next_report()
    {
        var running = new RunningSessions();

        var came = await running.StartAsync(async opened =>
        {
            opened();
            await Task.Yield();
            throw new InvalidOperationException("the conclusion could not be written");
        }).WaitAsync(Poll.Patience);
        await running.EndedAsync().WaitAsync(Poll.Patience);

        Assert.Null(came);
        var said = Assert.Single(running.Drain());
        Assert.StartsWith("failed  ", said.Line, StringComparison.Ordinal);
        Assert.Contains("the conclusion could not be written", said.Line, StringComparison.Ordinal);
    }

    /// <summary>What a closing driver waits on: every run concluded, however many looks started them.</summary>
    [Fact]
    public async Task Settled_waits_for_every_run_still_going()
    {
        var running = new RunningSessions();
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();
        foreach (var end in new[] { first, second })
        {
            await running.StartAsync(async opened =>
            {
                opened();
                await end.Task;
                return new StartRun("completed", true);
            }).WaitAsync(Poll.Patience);
        }

        var settled = running.SettledAsync();
        first.SetResult();
        await running.EndedAsync().WaitAsync(Poll.Patience);
        Assert.False(settled.IsCompleted);

        second.SetResult();
        await settled.WaitAsync(Poll.Patience);
        Assert.Equal(0, running.Running);
        Assert.Equal(2, running.Drain().Count);
    }

    /// <summary>The wait between looks ends on an ending, or on the pace, or on the close — and never throws.</summary>
    [Fact]
    public async Task The_wait_between_looks_ends_on_an_ending_before_its_pace()
    {
        var running = new RunningSessions();
        var end = new TaskCompletionSource();
        await running.StartAsync(async opened =>
        {
            opened();
            await end.Task;
            return new StartRun("completed", true);
        }).WaitAsync(Poll.Patience);

        var waiting = running.NextAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.False(waiting.IsCompleted);
        end.SetResult();
        await waiting.WaitAsync(Poll.Patience);

        using var closing = new CancellationTokenSource();
        var closed = running.NextAsync(TimeSpan.FromMinutes(5), closing.Token, endings: false);
        await closing.CancelAsync();
        await closed.WaitAsync(Poll.Patience);
    }

    /// <summary>
    /// LOOKWAIT1b (D181 point 6): work kept beside the looks is never the stage that began it, which does not wait on it. Its git
    /// waiting for a place while that stage still runs is not the stage's queue: said nowhere, or to a span of its own.
    /// </summary>
    [Theory]
    [InlineData("judge")]
    [InlineData("sync")]
    [InlineData("pass")]
    public async Task Work_begun_beside_a_stage_waits_for_its_places_apart_from_that_stage(string beside)
    {
        long now = 0;
        var stages = new LookStages(clock: () => Volatile.Read(ref now));
        using var held = new ManualResetEventSlim();
        var starts = new ProcessStarts(1, info =>
        {
            if (info.FileName == "holder") held.Wait(Poll.Patience);
            return null;
        });
        var holder = starts.StartAsync(new ProcessStartInfo("holder") { UseShellExecute = false, CreateNoWindow = true }, CancellationToken.None);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = new RunningSessions();
        Func<CancellationToken, Task<IReadOnlyList<string>>> work = async _ =>
        {
            var git = starts.StartAsync(new ProcessStartInfo("git") { UseShellExecute = false, CreateNoWindow = true }, StartClass.Background, CancellationToken.None);
            waiting.SetResult();
            await git;
            waited.SetResult();
            return [];
        };

        using (var stage = stages.Begin("landing"))
        {
            await stage.RunAsync(async () =>
            {
                Assert.True(beside switch
                {
                    "judge" => running.Judge("opinion", "q1", 1, work, CancellationToken.None),
                    "sync" => running.SyncBeside(work, CancellationToken.None),
                    _ => running.PassBeside(work, CancellationToken.None),
                });
                await waiting.Task.WaitAsync(Poll.Patience);
                Interlocked.Add(ref now, 2000);
                held.Set();
                await waited.Task.WaitAsync(Poll.Patience);
            });
        }

        await holder.WaitAsync(Poll.Patience);
        Assert.Equal("landing=2000", stages.Take());
    }
}
