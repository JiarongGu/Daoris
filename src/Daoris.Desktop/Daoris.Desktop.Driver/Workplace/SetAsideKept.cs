using System.Globalization;
using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>Why a folder moved aside outlives its wait: the machine log's <c>tidied.kept</c> codes.</summary>
public static class SetAsideKept
{
    /// <summary>Something in it was written after it was moved aside: a late write it was moved to keep.</summary>
    public const string Written = "written";

    /// <summary>
    /// It holds a link outside a build folder, or is reached through one, which a delete is never asked to follow; a link under a
    /// build folder goes with it as a link.
    /// </summary>
    public const string Linked = "linked";

    /// <summary>It could not be read, or deleted.</summary>
    public const string Unread = "unread";

    /// <summary>A recorded folder past its hour whose holding check did not pass right before its rename, or could not run.</summary>
    public const string Unheld = "unheld";

    /// <summary>A folder a stopped removal left in <c>.going</c>, put back where it stood, saying why.</summary>
    public const string Returned = "returned";
}
