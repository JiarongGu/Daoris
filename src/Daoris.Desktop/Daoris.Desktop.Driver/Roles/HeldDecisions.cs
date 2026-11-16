using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>One held decision as <c>held_decisions</c> keeps it: its file's text whole, as the keeping wrote it, and when it goes on.</summary>
public sealed record HeldDecisionRow(
    string Id, string Session, string Kind, string Repository, string Generation, DateTimeOffset GoesOnAt, string Kept);

/// <summary>
/// A role's held decisions and their overrule windows, in the desktop store's <c>held_decisions</c>: a row is written when a decision is
/// kept held and goes at its first move off held (released, overruled, superseded, stale), so a restart reads back exactly the decisions
/// still inside or past their window (<see cref="SupervisorSweeps.SweepAtStart"/>). The row keeps the decision's file text, and the restart
/// takes it back only where the file still says it and its last line is <c>held</c>: a file an older build closed, or one changed since, is
/// closed as before. A store before the step, or one that cannot be opened, keeps nothing, and the window lives in memory as it did.
/// </summary>
internal sealed class HeldDecisions(string home, Func<StoreSettled>? settled = null)
{
    /// <summary>The family a write moves.</summary>
    public const string Family = "held_decisions";

    private string Location => DesktopStore.LocationIn(home);

    /// <summary>Keep <paramref name="row"/>; false where the store would not take it.</summary>
    public bool Hold(HeldDecisionRow row) => Write(write => write.Execute(
        """
        INSERT INTO held_decisions (id, session, kind, repository, generation, goes_on_at, kept)
        VALUES ($id, $session, $kind, $repository, $generation, $at, $kept)
        ON CONFLICT (id) DO UPDATE SET goes_on_at = excluded.goes_on_at
        """,
        ("$id", row.Id), ("$session", row.Session), ("$kind", row.Kind), ("$repository", row.Repository), ("$generation", row.Generation),
        ("$at", Moments.Write(row.GoesOnAt)), ("$kept", row.Kept)));

    /// <summary>Let <paramref name="id"/>'s row go: the decision moved off held.</summary>
    public void Drop(string id) => Write(write => write.Execute("DELETE FROM held_decisions WHERE id = $id", ("$id", id)));

    /// <summary>Every row, oldest window first; none before the step or with no store.</summary>
    public IReadOnlyList<HeldDecisionRow> All()
    {
        try
        {
            using var store = DesktopStoreReader.Open(Location);
            if (store is null || store.Version < DesktopStore.HeldDecisionsVersion) return [];
            return store.Query(
                "SELECT id, session, kind, repository, generation, goes_on_at, kept FROM held_decisions ORDER BY goes_on_at, id",
                row => new HeldDecisionRow(
                    row.GetString(0), row.GetString(1), row.GetString(2), row.GetString(3), row.GetString(4),
                    DateTimeOffset.Parse(row.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), row.GetString(6)));
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException or FormatException)
        {
            return [];
        }
    }

    private bool Write(Action<DesktopWrite> work)
    {
        try
        {
            using var store = DesktopStore.Open(Location, StoreProcess.Options(settled ?? StartConfirmation.Read));
            if (store.Stamp.Version < DesktopStore.HeldDecisionsVersion) return false;
            store.Write(Family, work);
            return true;
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            // The window is memory's: a row the store would not take only means a restart closes the decision as before.
            return false;
        }
    }
}
