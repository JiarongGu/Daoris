using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A start's hold carried as a value (<see cref="StartHold"/>): the bind and the service's opens it governs ask its live handle, so a write
/// asked after its dispose, or an open in a tree with no hold, throws and writes nothing.
/// </summary>
public sealed class StartHoldTests : TestHome
{
    private static readonly QuestView Quest = new("q1", "daoris", "engine", "Fix the parser", "", "Open");

    private string Checkout => Directory.CreateDirectory(Path.Combine(Home, "engine")).FullName;

    [Fact]
    public void A_start_s_hold_carried_as_a_value_holds_its_repository_until_it_is_let_go()
    {
        var other = Directory.CreateDirectory(Path.Combine(Home, "shop")).FullName;
        var held = StartHold.TryTake(Home, Checkout, StartIn.Fresh)!;

        Assert.True(TreeLock.InFlow(held.Lock, Checkout));
        Assert.False(TreeLock.InFlow(held.Lock, other));
        Assert.False(TreeLock.InFlow(null, Checkout));

        held.Lock.Dispose();
        Assert.False(TreeLock.InFlow(held.Lock, Checkout));
        Assert.Throws<InvalidOperationException>(() => held.Assert("a run's bind"));
    }

    [Fact]
    public async Task A_run_s_bind_after_the_hold_s_dispose_is_refused_and_writes_nothing()
    {
        using var ledger = new StandInLedger();
        var held = StartHold.TryTake(Home, Checkout, StartIn.Fresh)!;
        held.Lock.Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowRunStart.AtStartAsync(
            new WorkflowStarted(Quest, "default"), new StartWorld(ledger.Client()), Home, DriverConfig.Empty, [], () => DateTimeOffset.UtcNow, held,
            CancellationToken.None));

        Assert.False(WorkflowRunBindings.Bound(Home, "q1"));
    }

    [Fact]
    public async Task A_run_s_bind_under_its_live_hold_is_written()
    {
        using var ledger = new StandInLedger();
        var held = StartHold.TryTake(Home, Checkout, StartIn.Fresh)!;
        using (held.Lock)
        {
            var bound = await WorkflowRunStart.AtStartAsync(
                new WorkflowStarted(Quest, "default"), new StartWorld(ledger.Client()), Home, DriverConfig.Empty, [], () => DateTimeOffset.UtcNow, held,
                CancellationToken.None);

            Assert.Equal(WorkflowBindCame.Bound, bound.Came);
        }

        Assert.True(WorkflowRunBindings.Bound(Home, "q1"));
    }

    [Fact]
    public async Task An_open_in_a_tree_with_no_start_s_hold_or_after_its_dispose_throws()
    {
        using var ledger = new StandInLedger();
        ledger.Publish("q1", "engine");
        var client = ledger.Client();
        var held = StartHold.TryTake(Home, Checkout, StartIn.Fresh)!;
        held.Lock.Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.OpenSessionAsync("q1", "stub", tree: Checkout));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.OpenSessionAsync("q1", "stub", tree: Checkout, starting: held));
        Assert.Empty(ledger.SessionsFor("q1"));
    }
}
