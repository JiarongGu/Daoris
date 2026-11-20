namespace Daoris.Driver;

/// <summary>
/// What the tidy reads of a session tree on disk: when it last moved and how many bytes it holds. A tree moves by a commit or a switch of
/// branch, which its reflog records, or by a new top-level entry; an edit with no commit is uncommitted work, which keeps the tree at any
/// age (<see cref="TreeReleases"/>).
/// </summary>
internal static class TreeUse
{
    /// <summary>The later of the tree's top folder's write time and its HEAD's last move, git's reflog read; the folder's time alone where git says none.</summary>
    internal static async Task<DateTimeOffset> LastAtAsync(string tree, CancellationToken ct)
    {
        var latest = DateTimeOffset.MinValue;
        try
        {
            latest = new DateTimeOffset(Directory.GetLastWriteTimeUtc(tree), TimeSpan.Zero);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // No time of its own.
        }

        var (code, output, _) = await WorkingTree.GitAsync(tree, ["log", "-g", "-1", "--format=%ct", "HEAD"], ct).ConfigureAwait(false);
        if (code == 0 && long.TryParse(output.Trim(), out var seconds))
        {
            var moved = DateTimeOffset.FromUnixTimeSeconds(seconds);
            if (moved > latest) latest = moved;
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
