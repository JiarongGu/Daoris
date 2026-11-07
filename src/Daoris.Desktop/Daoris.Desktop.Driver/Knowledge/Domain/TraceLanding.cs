namespace Daoris.Driver;

/// <summary>Where a session's work landed: each branch a landing made of it, or why none is named.</summary>
public sealed record TraceLanding
{
    /// <summary>Its store: <see cref="TraceStores.Landings"/>.</summary>
    public string Source => TraceStores.Landings;

    /// <summary>
    /// Where no branch is named: <see cref="TraceLandingGaps.Unread"/>, <see cref="TraceLandingGaps.None"/>, <see cref="TraceLandingGaps.Unknown"/> or
    /// <see cref="TraceLandingGaps.MergeAccepted"/>.
    /// </summary>
    public string? Missing { get; init; }

    public IReadOnlyList<TraceBranch> Branches { get; init; } = [];
}

/// <summary>A branch a landing made, standing or gone.</summary>
public sealed record TraceBranch(string Branch, string Tip, DateTimeOffset At)
{
    /// <summary>The line it landed from, or null where git named none.</summary>
    public string? Line { get; init; }

    /// <summary>The commit its work grew from.</summary>
    public string? From { get; init; }

    public string? Plugin { get; init; }

    public bool Pushed { get; init; }

    public string? PullRequest { get; init; }

    /// <summary>What its plugin last answered about its pull request, with when; null where none is kept.</summary>
    public PullRequestState? PullRequestState { get; init; }

    /// <summary>The latest ask about it that failed since that answer; null where none did.</summary>
    public PullRequestAskFailed? PullRequestAskFailed { get; init; }

    public DateTimeOffset? Gone { get; init; }

    /// <summary>What the clean-up proved when it removed it (<see cref="LandedKind"/>).</summary>
    public string? RemovedAs { get; init; }

    public string? RemovedOn { get; init; }

    /// <summary>
    /// Who accepted it, one of <see cref="Daoris.Driver.AcceptedBy"/>; null for a landing recorded
    /// before it is kept, which both doors say is not kept.
    /// </summary>
    public string? AcceptedBy { get; init; }

    /// <summary>The landing rule it is made under, as it stood then; null for one recorded before it is kept.</summary>
    public TraceRule? Rule { get; init; }

    /// <summary>
    /// The review that let it land: the person's reviewed, with its environment and the set-up's commit,
    /// or their skip; null where no review is asked, and for a landing recorded before it is kept.
    /// </summary>
    public LandingReview? Review { get; init; }

    /// <summary>
    /// The second opinion that let it land: settled, or the person's answer, or why it
    /// landed with none; null where none is asked, and for a landing recorded before it is kept.
    /// </summary>
    public LandingOpinion? Opinion { get; init; }

    /// <summary>
    /// The named workflow it landed by: its id, the version its run kept and the level that
    /// chose it; null under Current, and for a landing recorded before it is kept.
    /// </summary>
    public LandingWorkflow? Workflow { get; init; }

    /// <summary>
    /// The session it is made for: a landing that carries several sessions' work is read from each, so the one that made
    /// it is named beside those whose done moved it on (<see cref="Advances"/>). Null only from a driver before the field.
    /// </summary>
    public string? Session { get; init; }

    /// <summary>
    /// Each later done that moved it on, oldest first, each with its session: one landing carries several units'
    /// work, so the flowchart is the branch's and never one session's. Empty where none moved it.
    /// </summary>
    public IReadOnlyList<TraceAdvance> Advances { get; init; } = [];

    /// <summary>The branch work units whose work it carries, its own session's first; empty where its record keeps none.</summary>
    public IReadOnlyList<string> Units { get; init; } = [];
}

/// <summary>
/// One later done that moved a landed branch on: from the commit it stood at to the one it moved to, when, the
/// session whose done moved it, who accepted that done and what let it go, and the quest and unit its record keeps for that session.
/// </summary>
public sealed record TraceAdvance(string From, string To, DateTimeOffset At, string Session)
{
    /// <summary>One of <see cref="Daoris.Driver.AcceptedBy"/>, or null in a record that did not keep it.</summary>
    public string? AcceptedBy { get; init; }

    public LandingReview? Review { get; init; }

    public LandingOpinion? Opinion { get; init; }

    public LandingWorkflow? Workflow { get; init; }

    /// <summary>The quest its session served; null where it served none, and in a record from before.</summary>
    public string? Quest { get; init; }

    /// <summary>Its session's branch work unit; null where its tree had none, and in a record from before.</summary>
    public string? Unit { get; init; }
}

/// <summary>The landing rule a branch is made under, as it stood then.</summary>
/// <param name="Source">Where the rule came from, one of <see cref="LandingSource"/>.</param>
public sealed record TraceRule(string? Plugin, bool AutoAccept, string Source)
{
    /// <summary>
    /// Where the access setup that turned the rule's switch on is set (<see cref="LandedRule.Access"/>), one of
    /// <see cref="AccessRules.FromRepository"/> and the rest; null where the rule's own switch stood, and in a record from before.
    /// </summary>
    public string? Access { get; init; }
}
