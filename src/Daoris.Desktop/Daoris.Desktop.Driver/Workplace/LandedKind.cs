using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>What a landed branch holds, for the clean-up's list (WSR5). Only the first three go.</summary>
public static class LandedKind
{
    /// <summary>Every file it changed reads on the line as it left it — a squash merge's pull request reached the line.</summary>
    public const string OnLine = "on-line";

    /// <summary>Every commit of it is on the line.</summary>
    public const string Merged = "merged";

    /// <summary>Its history is inside another landed branch whose work is on the line.</summary>
    public const string Inside = "inside";

    /// <summary>
    /// Its pull request completed, as the plugin that pushed it answered, and git confirms it here (PLUGHOOK1a, D148 point 4): the
    /// merge commit is on the line and the branch stands at or under the commit it merged. What a squash leaves once the line
    /// has moved on.
    /// </summary>
    public const string PullRequest = "pull-request";

    /// <summary>Some file it changed reads otherwise on the line — kept, and the files named.</summary>
    public const string Differs = "differs";

    /// <summary>A working tree has it checked out, the repository's own checkout included — kept.</summary>
    public const string CheckedOut = "checked-out";

    /// <summary>It holds commits its remote-tracking branch does not: pushed, then moved — kept.</summary>
    public const string AheadOfRemote = "ahead-of-remote";

    /// <summary>A session branch that stays holds commits only this branch holds, and D88's proof for it leans on this one — kept.</summary>
    public const string LeanedOn = "leaned-on";

    /// <summary>Git could not say — kept.</summary>
    public const string Unknown = "unknown";
}
