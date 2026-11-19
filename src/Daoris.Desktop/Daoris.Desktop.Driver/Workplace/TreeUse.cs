namespace Daoris.Driver;

/// <summary>
/// What the tidy reads of a session tree on disk: when it was last used and how many bytes it holds. A tree's use is a commit, a switch of
/// branch or a new top-level entry, which git writes in the tree's own git folder and the tree's top; an edit with no commit is
/// uncommitted work, which keeps the tree whatever its age (<see cref="TreeReleases"/>).
/// </summary>
internal static class TreeUse
{
    /// <summary>The newest of the tree's top folder, its <c>.git</c> link and its git folder's <c>HEAD</c> and <c>logs/HEAD</c>.</summary>
    internal static DateTimeOffset LastAt(string tree)
    {
        var latest = DateTimeOffset.MinValue;
        void Note(string path)
        {
            try
            {
                if (!Path.Exists(path)) return;
                var at = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
                if (at > latest) latest = at;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A time that cannot be read is no use seen.
            }
        }

        Note(tree);
        var link = Path.Combine(tree, ".git");
        Note(link);
        if (File.Exists(link))
        {
            try
            {
                var line = File.ReadLines(link).FirstOrDefault(each => each.StartsWith("gitdir:", StringComparison.Ordinal));
                if (line is not null)
                {
                    var folder = Path.GetFullPath(line["gitdir:".Length..].Trim(), tree);
                    Note(Path.Combine(folder, "HEAD"));
                    Note(Path.Combine(folder, "logs", "HEAD"));
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Read as the top folder's time alone.
            }
        }

        return latest;
    }

    /// <summary>The bytes of the files under <paramref name="tree"/>, a link never followed; what could not be read counts nothing.</summary>
    internal static long Bytes(string tree)
    {
        long bytes = 0;
        var pending = new Stack<string>();
        pending.Push(tree);
        while (pending.Count > 0)
        {
            var folder = pending.Pop();
            try
            {
                foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos())
                {
                    if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    if (entry is FileInfo file) bytes += file.Length;
                    else pending.Push(entry.FullName);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Counted as far as it read.
            }
        }

        return bytes;
    }
}
