namespace Daoris.Driver;

/// <summary>
/// A pull request's state as a monitor watches it: the answer the plugin that opened it gave through
/// <c>work/state</c>, kept on the landing. Completed is <see cref="Merged"/>, a squash merge included and said so;
/// abandoned is <see cref="Closed"/>; open or unanswered is no state yet. Pure: nothing is asked or written here.
/// </summary>
public static class PullRequestReading
{
    /// <summary>The request merged, a squash merge included.</summary>
    public const string Merged = "merged";

    /// <summary>The request closed without merging.</summary>
    public const string Closed = "closed";

    /// <summary>The states a pull request reports, in order.</summary>
    public static readonly IReadOnlyList<string> States = [Merged, Closed];

    /// <summary>
    /// Whether the landing's pull request merged, a squash merge included: the plugin answered it completed, or, with no answer, the line
    /// holds the landed branch (<see cref="OnTheLine"/>).
    /// </summary>
    public static bool IsMerged(LandedBranch landing) =>
        landing.PullRequestState is { State: PullRequestStates.Completed } ? !MergedElsewhere(landing) : landing.PullRequestState is null && OnTheLine(landing) is not null;

    /// <summary>
    /// The keeper's proof that the line the work grew from holds the landed branch (by ancestry or by content), which reads as its pull
    /// request merged where no plugin's answer says otherwise: a request completed on the platform that no plugin answered for; else null.
    /// </summary>
    public static HeldFact? OnTheLine(LandedBranch landing) =>
        landing.Held is { } held && (landing.Line is null || string.Equals(held.Line, landing.Line, StringComparison.Ordinal)) ? held : null;

    /// <summary>Whether the request completed into a branch other than the line the work grew from, which proves nothing of the line (<see cref="PullRequestProof"/>'s <c>other-target</c>).</summary>
    private static bool MergedElsewhere(LandedBranch landing) =>
        landing.PullRequestState?.Target is { } target && landing.Line is { } line && !string.Equals(target, line, StringComparison.Ordinal);

    /// <summary>The state of the pull request of the branch unit the reading is on, read from its landing.</summary>
    public static NodeWatched Read(NodeReading reading)
    {
        if (reading.Unit.Kind != WorkUnitKind.Branch)
        {
            return new(null, $"it reads a {WorkUnitKind.Branch} unit's pull request, and this is a {reading.Unit.Kind}") { Refused = NodeRefusal.NotThisUnit };
        }

        return reading.Unit.OwnerKnown ? Of(NodeObserving.LandingOf(reading.Unit)) : new(null, "no record names its unit, so nothing of it is said") { Unknown = true };
    }

    /// <summary>The state of <paramref name="landing"/>'s pull request.</summary>
    public static NodeWatched Of(LandedBranch? landing)
    {
        if (landing is null) return new(null, "nothing was landed yet");
        var settled = new NodeWatched(null, "") { Revision = landing.Tip, Record = LandedBranches.FileName, Artifact = landing.Branch };
        if (landing.PullRequestState is null && OnTheLine(landing) is { } held)
        {
            return settled with { State = Merged, Said = $"`{landing.Branch}` is on `{held.On}` ({WorkInventory.ProofSaid(held.By, held.On)}), so its pull request was merged" };
        }

        if (landing.PullRequest is not { } address) return new(null, $"no pull request was opened for `{landing.Branch}` yet");
        if (landing.PullRequestState is { State: PullRequestStates.Completed } && MergedElsewhere(landing))
        {
            return new(null, $"{address} completed into `{landing.PullRequestState.Target}`, not the line, so nothing of it is on the line yet");
        }

        return landing.PullRequestState?.State switch
        {
            PullRequestStates.Completed => settled with
            {
                State = Merged,
                Said = landing.PullRequestState.How == MergeHow.Squash
                    ? $"{address} was merged as one squashed commit, so the work it carries is on the line by its content"
                    : $"{address} was merged",
            },
            PullRequestStates.Abandoned => settled with { State = Closed, Said = $"{address} was closed without merging" },
            PullRequestStates.Open => new(null, $"{address} is open"),
            _ => new(null, $"{address} was not answered yet"),
        };
    }
}
