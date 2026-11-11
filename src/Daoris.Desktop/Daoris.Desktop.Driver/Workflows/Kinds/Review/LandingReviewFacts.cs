using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What a landing supervisor reads of a review it may decide, from the review's record alone: the kind of work, the revision its decision is
/// bound to, the rows in summary with their marks, what could not be read, how far back the rows reach, and every part's handback with its
/// checks. The same facts on every kind, so no kind is decided on less than another.
/// </summary>
public static class LandingReviewFacts
{
    /// <summary>The most rows named; the rest are counted.</summary>
    public const int Rows = 40;

    public static string Of(ReviewRecord record)
    {
        var text = new StringBuilder();
        text.Append("work: ").Append(Kind(record)).Append('\n');
        text.Append("revision: ").Append(record.Revision).Append(" (attempt ").Append(record.Attempt).Append(")\n");
        if (record.Rows.Count > 0)
        {
            text.Append("files: ").Append(record.Rows.Count).Append('\n');
            foreach (var row in record.Rows.Take(Rows))
            {
                text.Append("  ").Append(row.Change).Append(' ').Append(row.Path);
                if (row.Marks.Count > 0) text.Append(" (").Append(string.Join(", ", row.Marks)).Append(')');
                text.Append('\n');
            }

            if (record.Rows.Count > Rows) text.Append("  and ").Append(record.Rows.Count - Rows).Append(" more\n");
        }

        foreach (var gap in record.Incomplete) text.Append("not read: ").Append(gap.Path is { } path ? $"{path}: " : "").Append(gap.Said).Append('\n');
        text.Append("reaches back: ").Append(record.Coverage).Append('\n');
        foreach (var part in record.Parts)
        {
            text.Append("handback ").Append(part.Quest).Append(' ').Append(part.Title).Append(": ").Append(part.Note ?? "no note");
            if (part.Unchecked) text.Append(" (nothing checked its done)");
            text.Append('\n');
            foreach (var requirement in part.Requirements) text.Append("  requirement: ").Append(requirement.Quote).Append(" — checked by ").Append(requirement.Check).Append('\n');
        }

        return text.ToString().TrimEnd('\n');
    }

    private static string Kind(ReviewRecord record) => record.Kind switch
    {
        ReviewKinds.Branch => $"a branch, {record.Reference.Branch} at {record.Reference.Tip}, onto {record.Reference.Line}",
        ReviewKinds.Checkout => $"a checkout's turns, {record.Reference.Turns.Count} of them, worked in place",
        ReviewKinds.Folder => "a folder's files, worked in place",
        ReviewKinds.Copy => $"a folder's files, worked in the copy {record.Reference.Copy}",
        ReviewKinds.Plan => $"plan {record.Reference.Plan} for the system {record.Reference.System}",
        _ => "an answer that changed nothing",
    };
}
