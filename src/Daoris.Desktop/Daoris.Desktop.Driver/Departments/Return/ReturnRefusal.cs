namespace Daoris.Driver;

/// <summary>
/// What a return refuses to bring back rather than guess at, by what it found at a path: the codes the return's wait carries beside
/// each path, which the person reads in their own words. A manifest skips links and lists files alone, so a link, a file turned folder
/// and a rename onto the same name read as removals and additions; each is found here on the disk before anything is written.
/// </summary>
public static class ReturnRefusal
{
    /// <summary>The path leads outside the folder.</summary>
    public const string Escapes = "escapes";

    /// <summary>A link or junction at the path, or on its way, in the session's copy.</summary>
    public const string LinkInCopy = "link-copy";

    /// <summary>A link or junction at the path, or on its way, in the folder.</summary>
    public const string LinkInFolder = "link-folder";

    /// <summary>The session's copy replaced a file with a folder of the same name, or a folder with a file.</summary>
    public const string TypeInCopy = "type-copy";

    /// <summary>The folder holds a folder where the session's file goes, or a file where its folder goes.</summary>
    public const string TypeInFolder = "type-folder";

    /// <summary>Two names that are one file to the disk: a rename that changes only case, or a name the folder already holds otherwise.</summary>
    public const string Collision = "collision";

    public static IReadOnlyList<string> All { get; } = [Escapes, LinkInCopy, LinkInFolder, TypeInCopy, TypeInFolder, Collision];

    /// <summary>Names differing only in case are one file on Windows and macOS.</summary>
    private static StringComparer Folding => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>The paths of <paramref name="inCopy"/> refused, by what was found at each.</summary>
    /// <param name="based">The folder as the copy was made from it.</param>
    /// <param name="copied">The copy now.</param>
    /// <param name="place">The folder now.</param>
    public static IReadOnlyDictionary<string, string> Of(
        FolderCopy copy, RunManifest based, RunManifest copied, RunManifest place, IReadOnlyList<string> inCopy)
    {
        var folded = inCopy.GroupBy(path => path, Folding).Where(group => group.Count() > 1).SelectMany(group => group).ToHashSet(StringComparer.Ordinal);
        var held = place.Entries.Select(entry => entry.Path).ToLookup(path => path, Folding);
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in inCopy)
        {
            var kept = copied.Listed(path) is not null;
            var code = Escaping(copy.Root, path)
                ?? (Linked(copy.Folder, path) ? LinkInCopy : null)
                ?? (Linked(copy.Root, path) ? LinkInFolder : null)
                ?? (Replaced(copy.Folder, based, path, kept) ? TypeInCopy : null)
                ?? (kept && Blocked(copy.Root, path) ? TypeInFolder : null)
                ?? (folded.Contains(path) || held[path].Any(other => !string.Equals(other, path, StringComparison.Ordinal)) ? Collision : null);
            if (code is not null) found[path] = code;
        }

        return found;
    }

    /// <summary><see cref="Escapes"/> where <paramref name="path"/> leads outside <paramref name="root"/>, else null.</summary>
    public static string? Escaping(string root, string path)
    {
        if (path.Length == 0 || path.Contains(':') || Path.IsPathRooted(path) || path.Split('/', '\\').Any(part => part is "" or "." or "..")) return Escapes;
        var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(Path.Combine(root, path)).StartsWith(top, StringComparison.OrdinalIgnoreCase) ? null : Escapes;
    }

    /// <summary>Whether a link or junction stands at <paramref name="path"/> under <paramref name="root"/>, or at a folder on its way.</summary>
    private static bool Linked(string root, string path)
    {
        var at = root;
        foreach (var part in path.Split('/'))
        {
            at = Path.Combine(at, part);
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(at);
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0) return true;
        }

        return false;
    }

    /// <summary>Whether the copy turned a file at <paramref name="path"/> into a folder, or a folder on its way into a file.</summary>
    private static bool Replaced(string folder, RunManifest based, string path, bool kept)
    {
        if (based.Listed(path) is null) return Above(path).Any(above => based.Listed(above) is not null);
        return !kept && (Directory.Exists(Path.Combine(folder, path)) || Above(path).Any(above => File.Exists(Path.Combine(folder, above))));
    }

    /// <summary>Whether the folder holds a folder at <paramref name="path"/>, or a file where a folder on its way goes.</summary>
    private static bool Blocked(string root, string path) =>
        Directory.Exists(Path.Combine(root, path)) || Above(path).Any(above => File.Exists(Path.Combine(root, above)));

    private static IEnumerable<string> Above(string path)
    {
        for (var slash = path.IndexOf('/'); slash > 0; slash = path.IndexOf('/', slash + 1)) yield return path[..slash];
    }
}
