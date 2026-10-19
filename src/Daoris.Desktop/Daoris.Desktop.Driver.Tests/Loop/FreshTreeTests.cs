using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// QUEUEDTREE1 (D51, GOONTREE1; D181's PARALLEL1f note): the tree a start opened for a session whose record never opened goes by the
/// clean path, on no token, on each way out that starts nothing and on the driver's close before the record's open is asked. On the
/// loop's token the close makes the removal throw at its first git, and the tree is left for the sweep.
/// </summary>
/// <remarks>
/// The fast half: no process starts. The removal is handed in, so each row sees what was let go; the start's own path through the
/// driver, its tree opened over a stand-in git, is <see cref="StartTreeCloseTests"/>' (QUEUEDTREE1b), and over real git GOONTREE1's
/// row in the Process half's <c>SessionMessagesTickTests</c>.
/// </remarks>
public sealed class FreshTreeTests
{
    private const string Tree = "X:/daoris/trees/engine/default/s-1";

    private readonly List<string> _removed = [];

    private FreshTree Fresh() => new(path =>
    {
        lock (_removed) _removed.Add(path);
        return Task.CompletedTask;
    });

    /// <summary>🔴 The row's case: the driver closes after the tree opened and before the record's open is asked.</summary>
    [Fact]
    public async Task A_close_before_the_records_open_is_asked_lets_the_fresh_tree_go_and_still_closes()
    {
        var fresh = Fresh();
        using var closing = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fresh.UntilOpenAsync<StartRun>(async () =>
        {
            fresh.Opened(Tree);
            await closing.CancelAsync();
            closing.Token.ThrowIfCancellationRequested();
            return new StartRun("never", false);
        }, closing.Token));

        Assert.Equal([Tree], _removed);
    }

    /// <summary>Once the open is asked the ledger may hold the tree (LEFT2): a close leaves it to the sweep, which reads the ledger.</summary>
    [Fact]
    public async Task A_close_once_the_records_open_is_asked_leaves_the_tree_to_the_ledger()
    {
        var fresh = Fresh();
        using var closing = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fresh.UntilOpenAsync<StartRun>(async () =>
        {
            fresh.Opened(Tree);
            fresh.Asked();
            await closing.CancelAsync();
            closing.Token.ThrowIfCancellationRequested();
            return new StartRun("never", false);
        }, closing.Token));

        Assert.Empty(_removed);
    }

    /// <summary>A return that starts nothing lets the tree go itself, and a refused open after the ask still does; never twice.</summary>
    [Fact]
    public async Task A_tree_let_go_on_a_return_goes_once_whatever_follows()
    {
        var fresh = Fresh();
        using var closing = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fresh.UntilOpenAsync<StartRun>(async () =>
        {
            fresh.Opened(Tree);
            fresh.Asked();
            await fresh.LetGoAsync();
            await fresh.LetGoAsync();
            await closing.CancelAsync();
            closing.Token.ThrowIfCancellationRequested();
            return new StartRun("never", false);
        }, closing.Token));

        Assert.Equal([Tree], _removed);
    }

    [Fact]
    public async Task A_start_that_opened_no_tree_lets_nothing_go()
    {
        var fresh = Fresh();
        using var closing = new CancellationTokenSource();
        await closing.CancelAsync();

        await fresh.LetGoAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fresh.UntilOpenAsync<StartRun>(() => Task.FromCanceled<StartRun>(closing.Token), closing.Token));

        Assert.Empty(_removed);
    }

    /// <summary>
    /// Only the close: a start that failed otherwise, or a timeout's cancellation while the loop runs on, fails its look as it always
    /// did, its tree the sweep's (D51).
    /// </summary>
    [Fact]
    public async Task A_failure_that_is_not_the_close_leaves_the_tree_as_it_did()
    {
        var fresh = Fresh();
        using var running = new CancellationTokenSource();

        await Assert.ThrowsAsync<DriverException>(() => fresh.UntilOpenAsync<StartRun>(() =>
        {
            fresh.Opened(Tree);
            throw new DriverException("the service did not answer");
        }, running.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fresh.UntilOpenAsync<StartRun>(() =>
        {
            fresh.Opened(Tree);
            throw new TaskCanceledException("a request timed out");
        }, running.Token));

        Assert.Empty(_removed);
    }

    /// <summary>A start that comes to something returns it, and nothing is let go that the start did not let go itself.</summary>
    [Fact]
    public async Task A_start_that_ends_on_its_own_returns_what_it_came_to()
    {
        var fresh = Fresh();
        var held = new StartRun("held  #q1 → engine: the harness is untrusted", false);

        var came = await fresh.UntilOpenAsync(() =>
        {
            fresh.Opened(Tree);
            return Task.FromResult(held);
        }, CancellationToken.None);

        Assert.Same(held, came);
        Assert.Empty(_removed);
    }
}
