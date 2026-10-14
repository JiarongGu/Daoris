namespace Daoris.Driver;

/// <summary>Where a landed session's branch stands now, for its review (REVIEW2, D113).</summary>
public static class LandedState
{
    /// <summary>The branch stands and still holds the commit the landing made it at: its changes are read from it.</summary>
    public const string Standing = "standing";

    /// <summary>No branch of the landing's is here any more: the clean-up removed it, or someone deleted it.</summary>
    public const string Gone = "gone";

    /// <summary>A branch of that name stands and does not hold the landing's commit: rebased or replaced, so not read as this session's work.</summary>
    public const string NotOurs = "not-ours";

    /// <summary>The repository has no checkout on this machine, or its checkout is not a repository of its own.</summary>
    public const string NoCheckout = "no-checkout";
}

/// <summary>
/// Whether a gone branch's work reads on the line (REVIEW2), by WSR5's proof by content on the commit the landing made
/// it at, while git still holds that commit.
/// </summary>
/// <param name="Kind">
/// <see cref="LandedKind.OnLine"/>, <see cref="LandedKind.Merged"/>, <see cref="LandedKind.Differs"/> or
/// <see cref="LandedKind.Unknown"/> as the proof found it, or <see cref="CommitsGone"/>.
/// </param>
/// <param name="Where">On the line or merged: the form of the line it was proven on. Differs: the form compared with.</param>
/// <param name="Files">Differs: the files it changed that read otherwise on the line.</param>
/// <param name="Detail">Unknown: git's own words.</param>
public sealed record LandedReads(string Kind, string? Where, IReadOnlyList<string> Files, string? Detail)
{
    /// <summary>Git no longer holds the commit the landing made the branch at (its objects were pruned), so the proof cannot be made.</summary>
    public const string CommitsGone = "commits-gone";
}

/// <summary>A landed session's review (REVIEW2, D113): its landing, where that branch stands, and what it holds.</summary>
/// <param name="Entry">The landing record, standing or a trace.</param>
/// <param name="State">One of <see cref="LandedState"/>.</param>
/// <param name="Changes">Standing, where asked: the changes from where its work grew from up to the branch; null where git could not read them.</param>
/// <param name="Reads">Gone: whether its work reads on the line.</param>
/// <param name="Detail">Standing: why its changes could not be read, where they could not.</param>
public sealed record LandedReview(LandedBranch Entry, string State, WorkingTree.TreeDiff? Changes = null, LandedReads? Reads = null, string? Detail = null)
{
    /// <summary>
    /// Whether the session's review reads as landed: its landed branch stands, or its tree is gone. Then accepting is
    /// no door — a second landing is refused while the branch stands (D87), and there is no tree to land once it is
    /// gone — and neither is sending it back; only the hand-off is, where one applies (WSR5b).
    /// </summary>
    /// <remarks>
    /// A tree still here after its landed branch went is a session that may have carried on after its landing
    /// (WSR6's replay keeps its own commits), so its review is the tree's again, as any other.
    /// </remarks>
    public bool ReadsAsLanded(bool treeGone) => treeGone || State == LandedState.Standing;
}
