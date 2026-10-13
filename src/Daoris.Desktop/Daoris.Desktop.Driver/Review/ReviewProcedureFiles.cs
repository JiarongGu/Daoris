namespace Daoris.Driver;

/// <summary>
/// A procedure looked for in a checkout (REVIEWENV1a, design §1.1): reached
/// through <see cref="ReviewRules.Holds"/>.
/// </summary>
internal static class ReviewProcedureFiles
{
    /// <summary>
    /// Whether <paramref name="root"/> holds a regular file at the path <paramref name="parts"/> name, reached through folders, with
    /// no link on the way.
    /// </summary>
    public static bool Holds(string root, IReadOnlyList<string> parts)
    {
        var at = root;
        for (var index = 0; index < parts.Count; index++)
        {
            at = Path.Combine(at, parts[index]);
            FileSystemInfo found = index < parts.Count - 1 ? new DirectoryInfo(at) : new FileInfo(at);
            if (!found.Exists || found.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
        }

        return true;
    }
}
