using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The review a run's Accept is pressed on, as a page draws it: the run, its review step, and the attempt and revision of the record that
/// review stands on, so the window's Accept (<c>WORKFLOW_NODE</c>'s <c>accept</c>) names what is drawn and a moved record refuses it
/// <c>moved</c>. A review not begun yet is named with neither, and its Accept is kept early on the record standing.
/// </summary>
public sealed record ReviewDrawn(string Run, string Node, int? Attempt, string? Revision)
{
    /// <summary>The review <paramref name="run"/>'s graph waits on, its first not settled; null where the run holds no review or no graph.</summary>
    public static ReviewDrawn? Of(string home, string? run)
    {
        if (string.IsNullOrWhiteSpace(run)) return null;
        var records = new NodeRecordFiles(home);
        if (new GraphRuns(records).Find(run) is not { Version.Graph: { } graph } bound) return null;
        var statuses = records.Read(bound.Unit)?.Statuses;
        var reviews = graph.Nodes.Where(node => node.Kind == NodeKinds.Review).ToList();
        foreach (var review in reviews)
        {
            if (statuses?.GetValueOrDefault(review.Id) is not { State: NodeStates.Active } status) continue;
            return new ReviewDrawn(run, review.Id, status.Attempt, ReviewRecords.Read(home, run, review.Id, status.Attempt)?.Revision);
        }

        return reviews.FirstOrDefault(review => statuses?.GetValueOrDefault(review.Id) is not { State: NodeStates.Reached })
            is { } waiting ? new ReviewDrawn(run, waiting.Id, null, null) : null;
    }

    /// <summary>The revision the run's review stands on, which an entry's Accept names for its part; null where none is drawn.</summary>
    public static string? RevisionOf(string home, string? run) => Of(home, run)?.Revision;
}
