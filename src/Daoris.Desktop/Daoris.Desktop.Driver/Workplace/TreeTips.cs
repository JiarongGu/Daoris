namespace Daoris.Driver;

/// <summary>
/// A tree's tip read from git's own files, with no process: the <c>HEAD</c> of its git directory, and the branch it names
/// as a loose ref or a line of <c>packed-refs</c> in the common directory. Null wherever the files cannot say it (an unborn branch, the
/// reftable format, a file being replaced), so the caller asks git.
/// </summary>
public static class TreeTips
{
    /// <summary>The commit <paramref name="tree"/>'s <c>HEAD</c> stands at, lower case, or null.</summary>
    public static string? Read(string tree)
    {
        try
        {
            if (GitBranches.GitDirectory(tree) is not { } own) return null;
            var head = File.ReadAllText(Path.Combine(own, "HEAD")).Trim();
            if (!head.StartsWith("ref:", StringComparison.Ordinal)) return Commit(head);

            return Named(Common(own), head["ref:".Length..].Trim());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// The branch <paramref name="tree"/>'s <c>HEAD</c> names, without <c>refs/heads/</c>, or null where it is detached or the files
    /// cannot say: what a landing's hold names its tree by.
    /// </summary>
    public static string? Branch(string tree)
    {
        try
        {
            if (GitBranches.GitDirectory(tree) is not { } own) return null;
            var head = File.ReadAllText(Path.Combine(own, "HEAD")).Trim();
            const string Heads = "ref: refs/heads/";
            return head.StartsWith(Heads, StringComparison.Ordinal) && head.Length > Heads.Length ? head[Heads.Length..].Trim() : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// The commit <paramref name="name"/> (<c>refs/…</c>) stands at in the common directory <paramref name="common"/>, lower case: a loose
    /// ref, else a line of <c>packed-refs</c>. Null where the files cannot say it (FABRIC-END6 reads origin's line by it).
    /// </summary>
    public static string? Named(string common, string name)
    {
        try
        {
            if (!name.StartsWith("refs/", StringComparison.Ordinal) || name.Contains("..", StringComparison.Ordinal)
                || Directory.Exists(Path.Combine(common, "reftable")))
            {
                return null;
            }

            var loose = Path.Combine(common, name.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(loose)) return Commit(File.ReadAllText(loose).Trim());

            var packed = Path.Combine(common, "packed-refs");
            if (!File.Exists(packed)) return null;
            foreach (var line in File.ReadLines(packed))
            {
                var space = line.IndexOf(' ', StringComparison.Ordinal);
                if (space > 0 && string.Equals(line[(space + 1)..].TrimEnd(), name, StringComparison.Ordinal)) return Commit(line[..space]);
            }

            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? Commit(string text) => EvidenceCodes.IsObjectId(text) ? text.ToLowerInvariant() : null;

    /// <summary>
    /// The repository's common git directory for a checkout, read from git's own files with no process: the checkout's git directory
    /// (<see cref="GitBranches.GitDirectory"/>), and the one its <c>commondir</c> names where it is a linked worktree's. Null where the
    /// files cannot say it. What LANDRACE1's branch hold names a repository by, whichever checkout a door holds.
    /// </summary>
    public static string? CommonDirectory(string checkout)
    {
        try
        {
            return GitBranches.GitDirectory(checkout) is { } own ? Common(own) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>A linked tree's git directory names the repository's in <c>commondir</c>; any other is its own.</summary>
    private static string Common(string own)
    {
        var file = Path.Combine(own, "commondir");
        if (!File.Exists(file)) return own;
        var named = File.ReadAllText(file).Trim();
        return Path.GetFullPath(Path.IsPathRooted(named) ? named : Path.Combine(own, named));
    }
}
