using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// Why a landing's branch went where Daoris did not remove it (<see cref="LandedBranch.GoneAs"/>), each on git's own word
/// (the knowledge note <c>git-ref-absence</c>).
/// </summary>
public static class LandedGone
{
    /// <summary>
    /// git says its branch is missing (<c>show-ref --exists</c> exit 2), and no branch of its name in another case stands: the person
    /// removed it, which is the person's end of it.
    /// </summary>
    public const string Missing = "missing";

    /// <summary>A branch of its name stands that is not the landing's (another commit, or the name in another case): kept, the person's to say.</summary>
    public const string Stands = "stands";

    /// <summary>
    /// git says its branch is missing, and another local branch holds the landing's recorded tip by ancestry (after review): the
    /// work moved there, a rename or a branch made from it, so it is kept, never read as the person's end (<see cref="LandedBranch.MovedTo"/>).
    /// Read only where the end judgement kept no end first (<see cref="EndKeeping.KeepGoneAsync"/>): a line that holds it ends it.
    /// </summary>
    public const string Moved = "moved";

    /// <summary>
    /// The person, or the steward, withdrew it (<see cref="LandedWithdrawals"/>): the line will never hold its work, so Daoris
    /// took the branch away on their word, kept its commits on a recovery ref (<see cref="LandedBranch.KeptOn"/>) and the reason beside
    /// them (<see cref="LandedBranch.GoneWhy"/>). Daoris's own act, never the person's removal outside it, and never an end the line proved.
    /// </summary>
    public const string Withdrawn = "withdrawn";
}
