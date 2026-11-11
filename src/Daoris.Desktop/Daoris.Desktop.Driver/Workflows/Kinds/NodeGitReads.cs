namespace Daoris.Driver;

/// <summary>
/// The git read an observation may be handed where the records cannot establish an outcome alone: <c>push</c>'s remote-tracking ref.
/// Absent, the outcome is unknown, never guessed.
/// </summary>
/// <param name="RemoteTip">The commit git's remote-tracking ref holds for a landing's branch, or null where git could not say.</param>
public sealed record NodeGitReads(Func<LandedBranch, string?>? RemoteTip = null)
{
    /// <summary>
    /// What the line and the person's branches hold of a branch unit's work at a commit (the <c>content.holds</c> and <c>land</c> readings), by the unit's id and
    /// that commit; null where it is not read there.
    /// </summary>
    public Func<string, string?, HoldContent?>? Content { get; init; }

    /// <summary>The tip of a full ref in the run's repository, as the pass read it, for a step's target (<see cref="WireTargets"/>); null where none is read.</summary>
    public Func<string, string?>? Tip { get; init; }

    /// <summary>What a run's <c>rebase</c> attempt came to, by run, node and attempt, as its door wrote it (<see cref="RebaseRecords"/>); null where none is read.</summary>
    public Func<string, string, int, RebaseAttempt?>? Rebased { get; init; }

    /// <summary>What a run's <c>resolve</c> came to and waits on, by run, node and attempt, as its door wrote it (<see cref="ResolveRecords"/>); null where none is read.</summary>
    public Func<string, string, int, (ResolveAttempt? Made, string? Waits)>? Resolved { get; init; }

    /// <summary>A run's work with no branch as its review reads it this pass (<see cref="ReviewWork"/>); null where it is not read.</summary>
    public ReviewWork? Reviewed { get; init; }
}
