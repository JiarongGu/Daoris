using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// The service's journal rows of answers kept to a second opinion's findings, teed into the bus's log as <c>opinion.kept</c> as they are
/// given (BUS2b2): the service's trigger journals each keep, the change stream wakes the lease holder's journal feed, and this teller is
/// one of its tellers. An observation of a turn in progress: the gate judges the turn's end (<c>opinion.answered</c>), never a partial answer.
/// </summary>
public static class OpinionKeptNews
{
    /// <summary>The journal's family of the opinions' rows, and the change an answers keep is.</summary>
    public const string Family = "opinions", Change = "answers-kept";

    /// <summary>The fact an answers-kept row is, or null for any other row. A count the row does not say stays unknown.</summary>
    public static BusDraft? Of(JournalEntry row)
    {
        if (row.Family != Family || row.Change != Change || row.Record.Length == 0) return null;
        return new BusDraft(BusTypes.OpinionKept, row.Record)
        {
            After = ["opinion-given:" + row.Record, "opinion:" + row.Record],
            Payload =
            [
                ("session", row.FromSession), ("fixed", Count(row, "fixed")), ("rejected", Count(row, "rejected")), ("unresolved", Count(row, "unresolved")),
            ],
        };
    }

    /// <summary>
    /// The teller for the lease holder's journal feed: each answers-kept row of a batch teed over <paramref name="bus"/> as it is handed, from
    /// the cursor <paramref name="load"/> and <paramref name="keep"/> keep (none: from the journal's newest, as a feed begins), a gap
    /// telling nothing, since the turn's end reads whatever answers stand.
    /// </summary>
    public static JournalTeller Teller(BusLog bus, Func<JournalPosition?>? load, Action<JournalPosition>? keep) =>
        new(batch =>
        {
            foreach (var row in batch.Rows) bus.Tee(() => Of(row));
            return Task.CompletedTask;
        }, load, keep);

    private static long? Count(JournalEntry row, string name)
    {
        var prefix = name + ":";
        var mark = row.Marks.FirstOrDefault(each => each.StartsWith(prefix, StringComparison.Ordinal));
        return mark is not null && long.TryParse(mark.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : null;
    }
}
