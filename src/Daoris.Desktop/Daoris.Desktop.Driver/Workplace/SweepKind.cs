using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What a session branch holds, for the clean-up's list. Only the empty, the landed and the carried go.</summary>
public static class SweepKind
{
    /// <summary>Nothing beyond the line.</summary>
    public const string Empty = "empty";

    /// <summary>
    /// Every commit on a branch of the person's; or, where a squash merge or a cherry-pick left them on none, its work there by
    /// content (<see cref="SweepItem.HeldBy"/>).
    /// </summary>
    public const string Landed = "landed";

    /// <summary>Commits only Daoris's branches hold — kept, and named.</summary>
    public const string Unlanded = "unlanded";

    /// <summary>
    /// Commits no branch of the person's holds, which a landed branch's completed pull request carried, as its plugin answered and
    /// git confirms: what a squash leaves once the platform deleted the source branch.
    /// </summary>
    public const string Carried = "carried";

    /// <summary>Its tree holds uncommitted work — kept.</summary>
    public const string Dirty = "dirty";

    /// <summary>A session still running or waiting names its tree — kept.</summary>
    public const string InUse = "in-use";
}
