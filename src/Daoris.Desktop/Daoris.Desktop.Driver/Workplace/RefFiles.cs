namespace Daoris.Driver;

/// <summary>
/// Whether a ref is certainly not in a repository, read from its files with no git started: a review asked git's counts on origin's copy
/// of the line even where the repository has none, and each such ask would be a start that only failed. A ref that may be there is never
/// called absent: a reftable repository, a loose file of the name (in any case a file system reads alike) and a line of
/// <c>packed-refs</c> naming it each make the answer false.
/// </summary>
internal static class RefFiles
{
    /// <summary>
    /// The git directory a folder's repository keeps its refs in, read from the folder's <c>.git</c> (a directory, or a file naming a
    /// linked tree's own directory and, beside it, <c>commondir</c>): two folders of one repository name the same one. Null where it
    /// cannot be read; nothing is started.
    /// </summary>
    internal static string? CommonDirOf(string folder)
    {
        if (GitDirOf(folder) is not { } own) return null;
        try
        {
            var link = Path.Combine(own, "commondir");
            return Path.TrimEndingDirectorySeparator(File.Exists(link) ? Path.GetFullPath(File.ReadAllText(link).Trim(), own) : own);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>A folder's own git directory, where its index lives: its <c>.git</c> directory, or the one a linked tree's <c>.git</c>
    /// file names. Null where it cannot be read; nothing is started.</summary>
    internal static string? GitDirOf(string folder)
    {
        try
        {
            var dot = Path.Combine(folder, ".git");
            if (Directory.Exists(dot)) return Path.TrimEndingDirectorySeparator(Path.GetFullPath(dot));
            if (!File.Exists(dot) || File.ReadLines(dot).FirstOrDefault() is not { } first || !first.StartsWith("gitdir:", StringComparison.Ordinal)) return null;
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(first["gitdir:".Length..].Trim(), Path.GetFullPath(folder)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Whether two git directories are the one, as the file system spells them.</summary>
    internal static bool SameDir(string? one, string? other) =>
        one is not null && other is not null
        && string.Equals(one, other, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>True only where <paramref name="reference"/> (a full name) is neither loose nor packed in the repository whose git directory is <paramref name="commonDir"/>.</summary>
    internal static bool Absent(string commonDir, string reference)
    {
        try
        {
            var directory = commonDir.Trim();
            if (directory.Length == 0 || !Directory.Exists(directory) || Directory.Exists(Path.Combine(directory, "reftable"))) return false;
            if (File.Exists(Path.Combine(directory, reference)) || Directory.Exists(Path.Combine(directory, reference))) return false;
            var packed = Path.Combine(directory, "packed-refs");
            if (!File.Exists(packed)) return true;
            var suffix = " " + reference;
            return !File.ReadLines(packed).Any(line => line.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
