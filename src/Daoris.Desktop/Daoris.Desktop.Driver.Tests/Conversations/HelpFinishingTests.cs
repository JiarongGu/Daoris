using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPNEWCHAT1 (the owner's report, 2026-10-24): *New conversation* finishes Ask Daoris's running conversation, which winds up
/// before its run ends, and a start in that time was handed it as running, carried on: the new conversation's first words went
/// back into the one finished. A finished conversation is marked until its run ends, so a start never carries it on, and a start
/// waits a bounded moment for it to end. A fake door, so nothing is spawned.
/// </summary>
public sealed class HelpFinishingTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-finishing-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpFinishingTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A running conversation whose finishing is counted, and never ends its input.</summary>
    private sealed class Chat : ChatRunner.ITurnedChat
    {
        public ChatTurns Turns { get; } = new(ready: () => Task.FromResult(true), take: (_, sent) =>
        {
            sent();
            return Task.CompletedTask;
        }, interrupt: () => Task.CompletedTask, changed: _ => { });

        public int Finished { get; private set; }

        public Task FinishAsync(Action endInput)
        {
            Finished++;
            return Task.CompletedTask;
        }
    }

    private ChatRunner Runner() => new(
        new ChatLedger().Client(), AdapterSet.Built(), _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), null,
        harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)));

    /// <summary>🔴 A conversation the person finished is finishing, and no other is: a start carries on only one they did not finish.</summary>
    [Fact]
    public void A_finished_conversation_is_finishing_and_one_not_finished_is_not()
    {
        var runner = Runner();
        var chat = new Chat();
        runner.Context.Turned["h1e1p000"] = chat;
        runner.Context.Turned["0the4000"] = new Chat();

        Assert.True(runner.Finish("h1e1p000"));

        Assert.Equal(1, chat.Finished);
        Assert.True(runner.Finishing("h1e1p000"));
        Assert.True(runner.Finishing("H1E1P000"));
        Assert.False(runner.Finishing("0the4000"));
    }

    /// <summary>One this runner does not hold is not finished, and is not marked.</summary>
    [Fact]
    public void A_conversation_not_held_here_is_not_finished_nor_marked()
    {
        var runner = Runner();

        Assert.False(runner.Finish("n0ne0000"));
        Assert.False(runner.Finishing("n0ne0000"));
    }

    /// <summary>
    /// A start waits for a finishing conversation's run to end, at most its bound: false while its watch still runs, true once it
    /// ended, and true at once for one with no run here.
    /// </summary>
    [Fact]
    public async Task A_start_waits_for_a_finishing_conversation_at_most_its_bound()
    {
        var runner = Runner();
        var watch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runner.Context.Watching["h1e1p000"] = watch.Task;

        Assert.False(await runner.WoundUpAsync("h1e1p000", TimeSpan.FromMilliseconds(50)));

        var waiting = runner.WoundUpAsync("h1e1p000", Timeout.InfiniteTimeSpan);
        watch.SetResult();
        Assert.True(await waiting.WaitAsync(Poll.Patience));
        Assert.True(await runner.WoundUpAsync("n0ne0000", Timeout.InfiniteTimeSpan));
        runner.Context.Watching.TryRemove("h1e1p000", out _);
    }
}
