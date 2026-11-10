using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The files a folder's manifest passes by, by name: an editor's owner and lock files, the shell's thumbnails and folder settings, and
/// temporary files. A change to one is no one's work.
/// </summary>
public static partial class FolderExcludes
{
    /// <summary>The names passed by, as globs.</summary>
    public static IReadOnlyList<string> Default { get; } = ["~$*", ".~lock.*#", "Thumbs.db", "desktop.ini", "*.tmp"];

    /// <summary>Whether a file named <paramref name="name"/> is passed by.</summary>
    public static bool Passes(string name) => Pattern().IsMatch(name);

    [GeneratedRegex(@"^(~\$.*|\.~lock\..*#|thumbs\.db|desktop\.ini|.*\.tmp)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
