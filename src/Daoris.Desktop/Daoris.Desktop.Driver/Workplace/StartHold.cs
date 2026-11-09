namespace Daoris.Driver;

/// <summary>
/// A start's shared hold on its repository (<see cref="TreeLock.TryStarting"/>), carried as a value from the stage that takes it to the bind and
/// the opens it governs. Registered in the flow it would be gone by the bind, since the stage that took it has returned, and kept in the loop's
/// flow it would outlive the hold's dispose; so each governed write asks the live handle.
/// </summary>
/// <param name="Lock">The hold, as the run's state keeps it.</param>
/// <param name="Checkout">The checkout it holds.</param>
public sealed record StartHold(TreeLock Lock, string Checkout) : IStartHold
{
    /// <inheritdoc/>
    public void Assert(string what, string? tree = null)
    {
        if (!TreeLock.InFlow(Lock, tree ?? Checkout)) throw new InvalidOperationException($"{what} was asked outside the start's hold on `{tree ?? Checkout}`.");
    }

    /// <summary>The start's hold on <paramref name="checkout"/>, or null while a replay or a writer holds it alone; its lock is the caller's to let go.</summary>
    public static StartHold? TryTake(string home, string checkout) => TreeLock.TryStarting(home, checkout) is { } held ? new StartHold(held, checkout) : null;
}
