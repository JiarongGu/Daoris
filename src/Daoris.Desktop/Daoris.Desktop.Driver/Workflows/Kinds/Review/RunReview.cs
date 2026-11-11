namespace Daoris.Driver;

/// <summary>
/// A run's review as the person reads it, opened by run: the record of the attempt read, where the review stands now, the presses that
/// attempt still takes, a newer record where one is ready, and the steps after its Accept.
/// </summary>
/// <param name="Record">The attempt's record; null where the review has kept none yet.</param>
public sealed record RunReview(string Run, string Node, ReviewRecord? Record)
{
    /// <summary>The review's state now (<see cref="NodeStates"/>), and the attempt it stands on.</summary>
    public string State { get; init; } = NodeStates.Blocked;

    public int Standing { get; init; }

    public DateTimeOffset? Since { get; init; }

    /// <summary>Whose word settled it (<see cref="NodeSource"/>), with the words kept, where it is settled.</summary>
    public string? By { get; init; }

    public string? Words { get; init; }

    /// <summary>The values the review's sentence of its kind is worded from.</summary>
    public RunReviewSays Says { get; init; } = RunReviewSays.Of(null);

    /// <summary>The steps before the review, its holds, the look and the second opinion among them, each as it stands.</summary>
    public IReadOnlyList<RunReviewStep> Before { get; init; } = [];

    /// <summary>The acts the read attempt takes: none on an attempt the review has left, or once it is settled.</summary>
    public IReadOnlyList<string> Presses { get; init; } = [];

    public RunReviewNewer? Newer { get; init; }

    /// <summary>The steps an Accept may start, in the graph's order, each as it stands.</summary>
    public IReadOnlyList<RunReviewStep> Follows { get; init; } = [];

    /// <summary>How the run ended (<see cref="RunOutcomes"/>); null while it goes on.</summary>
    public string? Ended { get; init; }
}

/// <summary>A record newer than the one read: its attempt, its revision and the paths whose content differs from the one read.</summary>
public sealed record RunReviewNewer(int Attempt, string Revision, IReadOnlyList<string> Changed);

/// <summary>A step before or after the review: its kind, its state, the way that leads to it, its outcome and door, and its recovery.</summary>
public sealed record RunReviewStep(string Node, string Kind, string State)
{
    public int Attempt { get; init; }

    public string? Code { get; init; }

    public string? Words { get; init; }

    public DateTimeOffset? Since { get; init; }

    /// <summary>The kind of the wire that leads to it (<see cref="WireKinds"/>), and the outcome or the failure it is taken on, where it names one.</summary>
    public string? Via { get; init; }

    public string? On { get; init; }

    /// <summary>The outcome reached, and what it reached: a pull request's address, a landed branch, a release's evidence.</summary>
    public string? Outcome { get; init; }

    public string? Door { get; init; }

    /// <summary>The person's words its attempt takes now: <c>again</c> on a failure kept for them, a held attempt's resolutions.</summary>
    public IReadOnlyList<string> Offers { get; init; } = [];
}
