using System.Text.Json;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>A tree a session holds as the ledger says (<see cref="BranchTidying.HoldersAsync"/>), with the session that holds it.</summary>
public sealed record TreeHolder(string Tree, string Session);

/// <summary>
/// Whether a writer of a checkout waits for a session working there (GITLOCK1, GITAPP2's §3 <i>Beside a session</i>): the line's pull and a
/// promotion move a checkout only where the ledger holds no session in it, running, waiting or held to go on, read inside the writer's hold;
/// a ledger that cannot be read holds it too. Each wait is said, naming the session and where the person finds it, and the write goes on at
/// the next look once the checkout is free.
/// </summary>
internal static class CheckoutBeside
{
    /// <summary>
    /// Why <paramref name="checkout"/> is not written now, or null where no session holds it. <paramref name="beside"/> null is a door that
    /// reads no ledger, which writes as before. <paramref name="what"/> is what waits, as the sentence names it.
    /// </summary>
    internal static async Task<string?> WaitsAsync(
        Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside, string checkout, string repository, string what, CancellationToken ct)
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

        return holders.FirstOrDefault(each => SamePath(each.Tree, checkout)) is { } holder
            ? $"session `{holder.Session}` works in `{repository}`'s checkout (Repositories → the repository's page), so {what} waits for it "
              + "and goes on at the next look once it ends"
            : null;
    }
}
