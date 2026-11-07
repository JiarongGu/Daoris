namespace Daoris.Driver;

/// <summary>
/// The git read an observation may be handed where the records cannot establish an outcome alone: <c>push</c>'s remote-tracking ref.
/// Absent, the outcome is unknown, never guessed.
/// </summary>
/// <param name="RemoteTip">The commit git's remote-tracking ref holds for a landing's branch, or null where git could not say.</param>
public sealed record NodeGitReads(Func<LandedBranch, string?>? RemoteTip = null)
{
    /// <summary>
    /// What the line and the person's branches hold of a branch unit's work at a commit (the <c>content.holds</c> reading), by the unit's id and
    /// that commit; null where it is not read there.
    /// </summary>
    public Func<string, string?, HoldContent?>? Content { get; init; }
}
