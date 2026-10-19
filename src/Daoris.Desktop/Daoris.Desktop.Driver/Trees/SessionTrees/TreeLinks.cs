namespace Daoris.Driver;

/// <summary>
/// The links in a tree, taken off as links before git removes the tree (TREELINK1, D51's note). Git for Windows'
/// <c>worktree remove</c> follows a junction: TREEDEPS1's probe on git 2.53.0.windows.3 deleted the file a junction in a tree
/// pointed at, outside it, forced or not. So every path that has git delete a tree's folder (<see cref="TreeRemovals"/>, a
/// landing's tidy in <see cref="TreeTidies"/>, the press of <see cref="TreeSweeps"/>, <see cref="TreeOpenings.UndoAsync"/>) first
/// takes each link in it off as a link, its target never touched, and keeps the tree where it cannot. The look's own tidy moves a
/// tree's folder aside whole and its purge never follows a link (TIDIEDLINK1), so it needs none of this.
/// </summary>
internal static class TreeLinks
{
    /// <summary>
    /// The most entries looked at in one tree before its removal: past it the tree stays, since what was not looked through may
    /// hold a link. Each entry is read once, its kind from the folder's own listing, which costs far less than git's delete of it
    /// after; a tree's installed dependencies run to tens of thousands.
    /// </summary>
    internal const int LinkWalkLimit = 1_000_000;

    /// <summary>
    /// Every link in <paramref name="tree"/> taken off as a link, never followed: null where none is left, or the tree is gone; else
    /// why the tree stays, a clause beginning <c>its tree</c> or naming it. The walk never enters a link, skips the tree's own
    /// <c>.git</c>, and looks at no more than <paramref name="limit"/> entries; it takes nothing off unless it read the whole tree.
    /// </summary>
    /// <remarks>
    /// A junction or a folder's link is taken off with the folder call on Windows, which removes the link and never reaches into it,
    /// and unlinked elsewhere, as <see cref="TreeSetAside"/>'s purge does. A link that will not go stops the walk: the links already
    /// taken off stay off, which loses nothing they pointed at, and the tree stays for git to never reach through it.
    /// </remarks>
    internal static string? Unlinked(string tree, int limit = LinkWalkLimit)
    {
        var links = new List<FileSystemInfo>();
        try
        {
            var top = new DirectoryInfo(tree);
            if (!top.Exists) return null;
            if (top.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return "its tree's folder is itself a link, which git's removal would reach through";
            }

            var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
            var pending = new Stack<DirectoryInfo>();
            pending.Push(top);
            var seen = 0;
            while (pending.TryPop(out var at))
            {
                foreach (var entry in at.EnumerateFileSystemInfos("*", options))
                {
                    if (at == top && string.Equals(entry.Name, ".git", StringComparison.OrdinalIgnoreCase)) continue;
                    if (++seen > limit)
                    {
                        return $"its tree holds more than {limit} entries, too many to look through for the links git's removal would follow";
                    }

                    if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) links.Add(entry);
                    else if (entry is DirectoryInfo folder) pending.Push(folder);
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return $"Daoris could not look through its tree for the links git's removal would follow ({error.Message.Trim()})";
        }

        foreach (var link in links)
        {
            try
            {
                if (link is DirectoryInfo && OperatingSystem.IsWindows()) Directory.Delete(link.FullName, recursive: false);
                else File.Delete(link.FullName);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                var named = Path.GetRelativePath(tree, link.FullName).Replace('\\', '/');
                return $"its tree holds a link, at {named}, that could not be taken off as a link ({error.Message.Trim()}), and git's removal would follow it";
            }
        }

        return null;
    }
}
