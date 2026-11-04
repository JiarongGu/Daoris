using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <param name="Commits">Unlanded and carried: how many only Daoris holds. Landed: how many it carried.</param>
/// <param name="Where">Landed: the first branch of the person's that holds it, or by content what holds its work. Empty: the line. Carried: the landed branch whose pull request carried it.</param>
/// <param name="Detail">Git's own lines where they say more: the unlanded commits, the uncommitted count.</param>
public sealed record SweepItem(
    string Repository, string Workspace, string Branch, string? Tree, string Kind, int Commits, string? Where, string? Detail)
{
    public bool Removable => Kind is SweepKind.Empty or SweepKind.Landed or SweepKind.Carried;

    /// <summary>Carried: the landing entry whose completed pull request carried it (PLUGHOOK1a), on which its removal is kept.</summary>
    public LandedBranch? CarriedBy { get; init; }

    /// <summary>
    /// Landed by content (SQUASHTIDY1): where its work is, though no branch of the person's holds its commits, and how it was
    /// found there. Null for a branch landed by ancestry, and for every other kind.
    /// </summary>
    public ContentHold? HeldBy { get; init; }

    /// <summary>
    /// git could not answer one of the guards (AUTOTIDY1): what its tree holds, or what its commits are. The kind is the safe
    /// side's (dirty, unlanded), and the look's own tidy says why it stays, once.
    /// </summary>
    public bool Unread { get; init; }

    /// <summary>
    /// How many ignored paths its tree holds that the checkout holds too (AUTOTIDY1): a build's output, which goes with the tree
    /// at the press (SQUASHTIDY1c), and with its folder moved aside at the look's own tidy (AUTOTIDY2a). Zero for a branch with no
    /// tree here.
    /// </summary>
    public int IgnoredShared { get; init; }

    /// <summary>
    /// It made commits since it grew (AUTOTIDY1; on every kind once its tip is read, AUTOTIDY2a): its tip is not the commit it
    /// started at, by the record of where it grew from (WSR6); false where the record has no entry for it. A branch that never
    /// moved is a conversation's place, which words to it go on in (D137), so the look's own tidy leaves its tree for the press.
    /// </summary>
    public bool Worked { get; init; }

    /// <summary>
    /// The package folders of its tree the look may drop once the tree is set aside (KEEP1a, <see cref="TreeSlims.JudgeAsync"/>), judged
    /// before the repository's hold for the look's own tidy; none on every row the press's list judged, and where git could not say.
    /// </summary>
    public IReadOnlyList<string> Droppable { get; init; } = [];

    /// <summary>
    /// Landed, judged for the look's own tidy (AUTOTIDY2a): its work is on the line itself, every commit an ancestor of the line's
    /// own branch or origin's copy, or held there by content; or a squashing landing's branch the line holds. False where it is
    /// only on another branch of the person's, whose session the person may still ask to change it: the look waits for the line,
    /// and the press may take it before. False on every row the press's list judged.
    /// </summary>
    public bool OnLine { get; init; }

    /// <summary>
    /// The branch the session's tree moved onto (TREEFOLLOW1): its agent checked out a branch of its own there, as its repository's
    /// rules ask, so no tree has this session branch out any more and the work is on that branch. Null where the tree is on this
    /// one, or gone. The tree is that branch's: it is never taken with this one.
    /// </summary>
    public string? TreeOn { get; init; }

    /// <summary>
    /// Which hold the judgement found keeps its commits (KEEPSIZE1b, the keep-size design §1.1, §3.3): <c>line</c>, <c>landing</c>,
    /// <c>squash</c>, <c>content</c>, <c>carried</c>, <c>empty</c>, or <c>branch</c> for a branch of the person's; null where none was found.
    /// </summary>
    public string? Hold { get; init; }

    /// <summary>
    /// Held by a landing (<see cref="Hold"/> <c>landing</c>): that landing's own branch, by which its record's landing is found (the last fixes'
    /// check, finding 6), since <see cref="SweepItem.Where"/> names the form of the line it was proven on. Null for every other hold.
    /// </summary>
    public string? LandingBranch { get; init; }
}
