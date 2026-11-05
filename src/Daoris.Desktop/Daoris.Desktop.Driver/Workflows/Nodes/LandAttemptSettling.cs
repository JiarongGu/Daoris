using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// A unit's <c>land</c> attempts a lapse left unreconciled, settled before anything new starts on it (<see cref="LandAttempts"/>). Each delivery
/// bound at an attempt's commit that stands unresolved is read by phase first, as a new delivery's start reads it: one whose phases are all
/// certain, a refused landing that made nothing among them, holds no new attempt back, since nothing lands it at a commit its subject moved
/// past and the unit's next delivery settles it. One held, unknown, waiting in its line's queue or left to its plugin still holds the attempt.
/// </summary>
internal static class LandAttemptSettling
{
    /// <summary>What a reading by phase leaves known: delivered, settled before, or going on from a phase every entry before it recorded.</summary>
    private static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        DeliveryStandings.Delivered, DeliveryStandings.Settled, DeliveryStandings.GoesOn,
    };

    /// <summary><paramref name="record"/> with each attempt settled that no delivery at its commit still holds, as kept; the record itself where none is held.</summary>
    public static async Task<NodeRecord> SettleAsync(NodeRecordFiles records, GovernedUnit governed, NodeRecord record, CancellationToken ct)
    {
        if (!LandAttempts.Holds(governed.Graph, record)) return record;
        var home = records.Home;
        var repository = governed.Watched.Repository;
        var root = governed.Scope?.Root ?? governed.Watched.Root;
        var store = new DeliveryStore(home);
        var reconciler = new DeliveryReconciler(store, home, each =>
            string.Equals(each.Intent.Repository, repository, StringComparison.OrdinalIgnoreCase) ? root : DeliveryReconciler.Checkouts(home)(each));
        var holding = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tip in LandAttempts.Tips(governed.Graph, record))
        {
            foreach (var delivery in store.At(repository, tip).Where(each => each.Unresolved))
            {
                if (!Known.Contains(await StandingAsync(reconciler, delivery, ct).ConfigureAwait(false))) holding.Add(tip);
            }
        }

        records.Edit(governed.Unit.Id, now => (LandAttempts.Settled(governed.Graph, now, holding.Contains), 0));
        return records.Read(governed.Unit.Id) ?? record;
    }

    private static async Task<string> StandingAsync(DeliveryReconciler reconciler, DeliveryRecord delivery, CancellationToken ct)
    {
        try
        {
            return (await reconciler.OneAsync(delivery, ct).ConfigureAwait(false)).Standing;
        }
        catch (Exception error) when (error is SqliteException or DesktopStoreException or IOException)
        {
            return DeliveryStandings.Held;
        }
    }
}
