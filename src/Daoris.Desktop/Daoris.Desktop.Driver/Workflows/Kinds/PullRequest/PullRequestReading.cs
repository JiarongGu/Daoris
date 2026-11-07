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

    /// <summary>Whether the landing's pull request merged, a squash merge included: the plugin answered it completed.</summary>
    public static bool IsMerged(LandedBranch landing) => landing.PullRequestState is { State: PullRequestStates.Completed };

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
        if (landing.PullRequest is not { } address) return new(null, $"no pull request was opened for `{landing.Branch}` yet");
        var settled = new NodeWatched(null, "") { Revision = landing.Tip, Record = LandedBranches.FileName, Artifact = landing.Branch };
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
