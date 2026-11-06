namespace Daoris.Driver;

/// <summary>What a <c>monitor</c> or a <c>tidy</c> reads on its landing: reached at the landing's tip, or nothing yet, in a clause.</summary>
internal sealed record CompletionSeen(bool Reached, string Said)
{
    /// <summary>The landing whose record reached it.</summary>
    public LandedBranch? Landing { get; init; }
}

/// <summary>
/// The steps past a pull request (D245): <c>monitor</c> and <c>tidy</c>, each read from the run's landing record. The monitor reads the answer
/// the plugin that opened the request gave through <c>work/state</c>, which the moments ask (D148's minute); the tidy reads what its door ended
/// there (<see cref="ChainTidied"/>). Pure: nothing is asked or written here.
/// </summary>
public static class CompletionNodes
{
    /// <summary>
    /// The prebuilt set a workflow composes to complete work through a pull request (D245 point 4), in its order: the plugin's step is
    /// <c>&lt;plugin&gt;/pull-request</c>, named here by its id.
    /// </summary>
    public static readonly IReadOnlyList<string> Set = [NodeKinds.Land, NodeKinds.Push, PullRequestSteps.Id, NodeKinds.Monitor, NodeKinds.Tidy];

    /// <summary>Whether the landing's pull request was merged, a squash merge included: the plugin answered it completed.</summary>
    public static bool Merged(LandedBranch landing) => landing.PullRequestState is { State: PullRequestStates.Completed };

    /// <summary>A <c>monitor</c>: reached once the request is merged or closed; pending while it is open or unanswered.</summary>
    internal static CompletionSeen Monitor(LandedBranch? landing)
    {
        if (landing is null) return new(false, "nothing was landed yet");
        if (landing.PullRequest is not { } address) return new(false, $"no pull request was opened for `{landing.Branch}` yet");
        return landing.PullRequestState?.State switch
        {
            PullRequestStates.Completed => new(true, landing.PullRequestState.How == MergeHow.Squash
                ? $"{address} was merged as one squashed commit, so the work it carries is on the line by its content"
                : $"{address} was merged") { Landing = landing },
            PullRequestStates.Abandoned => new(true, $"{address} was closed without merging") { Landing = landing },
            PullRequestStates.Open => new(false, $"{address} is open"),
            _ => new(false, $"{address} was not answered yet"),
        };
    }

    /// <summary>A <c>tidy</c>: reached once the landing record holds what its door ended at the landing's tip.</summary>
    internal static CompletionSeen Tidy(LandedBranch? landing)
    {
        if (landing?.Tidied is not { } tidied || !string.Equals(tidied.Tip, landing.Tip, StringComparison.OrdinalIgnoreCase))
        {
            return new(false, "its chain's parts were not tidied yet");
        }

        var kept = tidied.Parts.Count(part => part.Kept is not null);
        return new(true, $"{tidied.Parts.Count} part(s) of its chain ended on the line" + (kept == 0 ? "" : $", {kept} of their branches kept"))
        {
            Landing = landing,
        };
    }
}
