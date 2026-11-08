namespace Daoris.Driver;

/// <summary>A path shared into a tree: a link at <see cref="Path"/> to <see cref="Place"/> in the checkout, <c>read</c> or <c>write</c>.</summary>
/// <param name="Path">From the tree's top in git's spelling, a folder ending in <c>/</c>.</param>
/// <param name="Place">The checkout's full path the link leads to.</param>
internal sealed record TreeShare(string Path, string Place, string Reach);

/// <summary>What stands at a share's path, read off the entry and never through it.</summary>
internal enum TreeShareStanding
{
    /// <summary>A link leading to the recorded place: the share.</summary>
    Holds,

    Absent,

    /// <summary>A link leading anywhere else: a foreign link.</summary>
    Elsewhere,

    /// <summary>An ordinary file or folder, which a save by rename or a folder made again leaves: the line's own.</summary>
    Ordinary,
}

/// <summary>
/// The links Daoris makes to shared places (the tree-pool design §35.5, §37.4). The link is Daoris's and the place behind it never is,
/// so a share counts, and is unlinked, only while its entry reads as a link to its recorded place; anything else standing there is
/// the line's own, its record dropped and kept in <see cref="TreeFilesState.Lost"/> until the files stage says so.
/// </summary>
internal static class TreeShares
{
    private static StringComparison PathCase => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    internal static string Full(string tree, string path) => System.IO.Path.Combine(tree, path.TrimEnd('/').Replace('/', System.IO.Path.DirectorySeparatorChar));

    /// <summary>What stands at <paramref name="share"/>'s path in <paramref name="tree"/>, read without following a link.</summary>
    internal static TreeShareStanding Standing(string tree, TreeShare share)
    {
        var entry = new FileInfo(Full(tree, share.Path));
        if ((int)entry.Attributes == -1) return TreeShareStanding.Absent;
        if (!entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) return TreeShareStanding.Ordinary;
        var folder = entry.Attributes.HasFlag(FileAttributes.Directory);
        return folder == share.Path.EndsWith('/') && DependencyLinks.Leads(entry) is { } target
            && string.Equals(System.IO.Path.TrimEndingDirectorySeparator(target), System.IO.Path.TrimEndingDirectorySeparator(share.Place), PathCase)
            ? TreeShareStanding.Holds
            : TreeShareStanding.Elsewhere;
    }

    /// <summary>The record's shares that still hold in <paramref name="tree"/>; every other is dropped from the record and kept as lost.</summary>
    internal static IReadOnlyList<TreeShare> Holding(string home, string tree)
    {
        if (TreeFilesRecord.Read(home, tree) is not { Shares.Count: > 0 } record) return [];
        var lost = record.Shares.Where(share => Standing(tree, share) != TreeShareStanding.Holds).Select(share => share.Path).ToList();
        if (lost.Count > 0) TreeFilesRecord.Change(home, tree, state => Drop(state, lost, lost));
        return [.. record.Shares.Where(share => !lost.Contains(share.Path))];
    }

    /// <summary>The links among <paramref name="links"/> (from the tree's top, <c>/</c>-separated) that are no share still holding.</summary>
    internal static IReadOnlyList<string> Unshared(string home, string tree, IReadOnlyList<string> links)
    {
        if (links.Count == 0) return links;
        var shares = Holding(home, tree).Select(share => share.Path.TrimEnd('/')).ToList();
        return [.. links.Where(link => !shares.Any(share => string.Equals(share, link.TrimEnd('/'), PathCase)))];
    }

    /// <summary>A share's link made at its path: a junction for a folder on Windows, a symbolic link elsewhere and for a file.</summary>
    internal static void Make(string tree, TreeShare share)
    {
        var full = Full(tree, share.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        DependencyLinks.Make(full, full, share.Place, directory: share.Path.EndsWith('/'));
    }

    /// <summary>
    /// Every recorded share's link taken off as a link, each read again just before: one that holds is unlinked and its record
    /// dropped, one already absent is dropped, and anything else standing is the line's, dropped as lost and left as it stands.
    /// Null once none is left, else why a link stays.
    /// </summary>
    internal static string? Unlink(string home, string tree)
    {
        foreach (var share in TreeFilesRecord.Read(home, tree)?.Shares ?? [])
        {
            var standing = Standing(tree, share);
            if (standing == TreeShareStanding.Holds)
            {
                var full = Full(tree, share.Path);
                try
                {
                    if (share.Path.EndsWith('/') && OperatingSystem.IsWindows()) Directory.Delete(full, recursive: false);
                    else File.Delete(full);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    return $"its shared link at {share.Path} could not be taken off as a link ({error.Message.Trim()})";
                }
            }

            IReadOnlyList<string> lost = standing is TreeShareStanding.Holds or TreeShareStanding.Absent ? [] : [share.Path];
            TreeFilesRecord.Change(home, tree, state => Drop(state, [share.Path], lost));
        }

        return null;
    }

    /// <summary>The record with the shares at <paramref name="paths"/> dropped and <paramref name="lost"/> kept to be said.</summary>
    internal static TreeFilesState Drop(TreeFilesState state, IReadOnlyList<string> paths, IReadOnlyList<string> lost) => state with
    {
        Shares = [.. state.Shares.Where(share => !paths.Contains(share.Path))],
        Lost = [.. state.Lost.Concat(lost).Distinct(StringComparer.Ordinal)],
    };
}
