using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A held quest's queued check stands aside while the checks behind it go on, says why on its waiting row, and is tried again at its first
/// arrival once the hold is lifted. On a checkout whose git folder is bare files, so no process starts and an attempt it takes is answered
/// <i>unknown</i>.
/// </summary>
/// <remarks>The Process half's, for its wall-clock polls and the checkout folders it makes.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class HeavyExecutorHoldProcessTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "heavy-hold", Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset At = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private HeavyExecutor? _executor;
    private string Home => Path.Combine(_folder, "home");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(Home);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_executor is not null) await _executor.DisposeAsync();
        GitSeed.Discard(_folder);
    }

    private VerifySend Send(string run, string invocation)
    {
        var checkout = Path.Combine(_folder, run);
        Directory.CreateDirectory(Path.Combine(checkout, ".git"));
        return new VerifySend(run, invocation, "engine", checkout, "c-" + run, null) { Bound = TimeSpan.FromDays(60) };
    }

    private async Task SettledAsync(string run, string invocation)
    {
        var path = VerifyFiles.ResultOf(Home, run, invocation);
        await Poll.Until(() => File.Exists(path), () => $"no result at {path}");
    }

    [Fact]
    public async Task A_held_runs_check_stands_aside_for_the_ones_behind_it_and_runs_once_lifted()
    {
        var executor = _executor = new HeavyExecutor(Home, new VerifyWorld { Poll = TimeSpan.FromMilliseconds(50) });
        Assert.Null(HoldBook.Set(Home, new HoldEvent(At, PriorityBook.Quest, "r1", "games", true, "make room", "the person")));

        await executor.SendAsync(Send("r1", "i1"));
        await executor.SendAsync(Send("r2", "i2"));
        await SettledAsync("r2", "i2");

        Assert.False(File.Exists(VerifyFiles.ResultOf(Home, "r1", "i1")));
        Assert.True(executor.Holds("i1"));
        var row = Assert.Single(VerifySlot.Look(Home).Waiters, waiter => waiter.Run == "r1");
        Assert.Contains("held by the person: make room", row.For);

        Assert.Null(HoldBook.Set(Home, new HoldEvent(At, PriorityBook.Quest, "r1", "games", false, "lifted", "the person")));
        await SettledAsync("r1", "i1");
        Assert.False(executor.Holds("i1"));
    }
}
