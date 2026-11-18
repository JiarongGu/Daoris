using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// The trees <c>daoris session list</c> walks with no Daoris running: only those of the rows needing the person, whose lines say what
/// their trees hold (a run's Accept its commits to review, a hold its landing). Every other row is placed from what is recorded, the
/// records, the quests, the marks and the waits, as the window's list places it before the facts' parts judge its tree, so the list of
/// a machine's sessions waits on no git walk of a tree no row of it needs: eight walks held it 50 s on a fixture of an installed
/// machine's shape, against under a second for the rest.
/// </summary>
/// <remarks>
/// The trees left unread are counted, their folders still here, so the listing says it read none of them and which door reads one: a
/// running Daoris's own list, which the verb asks first and whose facts judge every tree apart from it, or a session's landing plan.
/// </remarks>
internal static class ListedTrees
{
    /// <summary>A listing's rows with the trees it walked, and how many trees its rows would offer were they walked, left unread.</summary>
    internal sealed record Listed(SessionLook Look, IReadOnlyList<SessionGrouping> Rows, int Unread);

    /// <summary>The sessions whose trees the list walks: the rows needing the person.</summary>
    public static IReadOnlySet<string> Needing(IEnumerable<SessionGrouping> rows) =>
        rows.Where(row => row.Group == SessionGroup.NeedsYou).Select(row => row.Session).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// <paramref name="look"/>, its <paramref name="rows"/> placed from what is recorded, with the trees of the rows needing the person
    /// walked through <paramref name="walk"/>, as <c>walks</c> in <paramref name="times"/>, and the rows read again after a walk.
    /// </summary>
    /// <param name="held">Whether a path is a tree this home opened (<see cref="SessionTrees.Holds"/>).</param>
    /// <param name="walk"><see cref="SessionTrees.WorkAsync"/>, git's walk of one tree; a test hands its own.</param>
    public static async Task<Listed> JudgeAsync(
        SessionLook look, IReadOnlyList<SessionGrouping> rows, Func<string, bool> held, Func<string, CancellationToken, Task<TreeWork?>> walk,
        LookTimes times, CancellationToken ct)
    {
        var needing = Needing(rows);
        var asked = SessionGroups.TreesToJudge(look, held, needing).Select(SessionGroups.Normal).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unread = SessionGroups.TreesToJudge(look, held).Count(tree => !asked.Contains(SessionGroups.Normal(tree)) && TreeFolders.Walkable(tree));
        times.Trees = asked.Count;
        if (asked.Count == 0) return new(look, rows, unread);

        var judged = await times.TimeAsync("walks", () => SessionGroups.JudgeAsync(look, held, walk, needing, ct)).ConfigureAwait(false);
        return new(judged, times.Time("walkedRows", () => SessionGroups.Read(judged)), unread);
    }

    /// <summary>The listing's last line where it left <paramref name="count"/> trees unread.</summary>
    public static string Unread(int count) =>
        $"sessions: {count.ToString(CultureInfo.InvariantCulture)} tree{(count == 1 ? "" : "s")} of sessions not needing you left unread; "
        + "a running Daoris's list reads their work, and `daoris tree land <session> --plan` one tree's.";
}
