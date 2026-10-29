namespace Daoris.Driver;

/// <summary>
/// An effect link of the loop's chain held while an update drains (DRAINHOLD1, D139 §2): the tidy of a ref move removes a branch and its
/// tree, which the drain does not wait on and the close would cut. While <paramref name="draining"/> answers true, a try is refused with no
/// git, so its units stay pending, read from the persisted facts, and the first wake or deadline after the drain tries them again. Its
/// pending units are read as they are, so nothing is lost.
/// </summary>
/// <param name="inner">The effect that does the work when nothing drains.</param>
/// <param name="draining">Whether an update drains now, asked at each try.</param>
internal sealed class DrainHeldEffect(IChainEffect inner, Func<bool> draining) : IChainEffect
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ChainPending>> PendingAsync(CancellationToken ct) => inner.PendingAsync(ct);

    /// <inheritdoc />
    public Task<ChainTry> TryAsync(string unit, CancellationToken ct) =>
        draining() ? Task.FromResult(ChainTry.Refused) : inner.TryAsync(unit, ct);

    /// <inheritdoc />
    public Task<ChainTry> TryAsync(string unit, bool due, CancellationToken ct) =>
        draining() ? Task.FromResult(ChainTry.Refused) : inner.TryAsync(unit, due, ct);
}
