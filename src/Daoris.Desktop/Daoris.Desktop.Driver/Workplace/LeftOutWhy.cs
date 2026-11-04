using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>Why a landing leaves a commit out (LANDSYNC1, D87's 2026-10-15 amendment points 1 and 2).</summary>
public static class LeftOutWhy
{
    /// <summary>The run from where the tree left the line whose work reads on the line by content (WSR6's walk): a squash of several.</summary>
    public const string Content = "content";

    /// <summary>A commit the line gained since has its patch id (what <c>git cherry</c> marks <c>-</c>): a one-commit squash or a cherry-pick.</summary>
    public const string Patch = "patch";

    /// <summary>Kept, and found empty by the replay: its changes are already on the line.</summary>
    public const string Empty = "empty";
}
