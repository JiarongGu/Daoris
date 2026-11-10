using System.Text.Json;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>A tree a session holds as the ledger says (<see cref="BranchTidying.HoldersAsync"/>), with the session that holds it.</summary>
public sealed record TreeHolder(string Tree, string Session)
{
    /// <summary>The step whose hand-off reserves the tree, as its words name it (run <c>r-12</c>'s <c>resolve</c>); null for a session's hold.</summary>
    public string? Step { get; init; }

    /// <summary>That the session holds the writer's lease where its tree is a place itself (<see cref="PlaceHolders.Leases"/>).</summary>
    public bool Leases { get; init; }
}

/// <summary>
/// Whether a writer of a checkout waits for a session working there: the line's pull and a
/// promotion move a checkout only where the ledger holds no session in it, read inside the writer's hold; a ledger that cannot be read
/// holds it too. In a session's own tree that is any session running, waiting or held to go on there; in the repository's own checkout
/// it is the writer's lease's holder alone, a turn running there or a line parked with uncommitted paths of its own. Each wait is said,
/// naming the session and where the person finds it, and the write goes on at the next look once the checkout is free.
/// </summary>
internal static class CheckoutBeside
{
    /// <summary>
    /// Why <paramref name="checkout"/> is not written now, or null where no session holds it. <paramref name="beside"/> null is a door that
    /// reads no ledger, which writes as before. <paramref name="what"/> is what waits, as the sentence names it. <paramref name="place"/>
    /// says the checkout is the repository's own, where only the lease's holder and a step's reservation hold it.
    /// </summary>
    internal static async Task<string?> WaitsAsync(
        Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside, string checkout, string repository, string what, CancellationToken ct,
        bool place = false)
    {
        if (beside is null) return null;
        IReadOnlyList<TreeHolder> holders;
        try
        {
            holders = await beside(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or IOException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return $"the ledger could not be read ({error.Message}), so whether a session works in `{repository}`'s checkout is not known "
                   + $"and {what} waits for the next look";
        }

        return holders.FirstOrDefault(each => SamePath(each.Tree, checkout) && (!place || each.Leases || each.Step is not null)) is { } holder
            ? $"session `{holder.Session}` works in `{repository}`'s checkout (Repositories → the repository's page), so {what} waits for it "
              + "and goes on at the next look once it ends"
            : null;
    }

    /// <summary>
    /// The same read for a press: why <paramref name="what"/> is refused now, or null where no session holds the checkout. A ledger not given or not
    /// readable refuses, since the person's press is not retried at a look.
    /// </summary>
    internal static async Task<string?> RefusesAsync(
        Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside, string checkout, string repository, string what, string then, CancellationToken ct)
    {
        if (beside is null) return $"the ledger of sessions was not read, so whether a session works in `{repository}`'s checkout is not known and {what} is refused";
        IReadOnlyList<TreeHolder> holders;
        try
        {
            holders = await beside(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or IOException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return $"the ledger could not be read ({error.Message}), so whether a session works in `{repository}`'s checkout is not known and {what} is refused";
        }

        return holders.FirstOrDefault(each => SamePath(each.Tree, checkout)) is { } holder
            ? $"session `{holder.Session}` works in `{repository}`'s checkout (Repositories → the repository's page), so {what} {then}"
            : null;
    }
}
