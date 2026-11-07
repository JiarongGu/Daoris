using static Daoris.Driver.SetAsideDeleting;

namespace Daoris.Driver;

/// <summary>Why the purge keeps a folder moved aside: read and never written, the purge's own read and the inventory's.</summary>
internal sealed class SetAsideKeeps(TreeContext context)
{
    /// <summary>A file in <paramref name="folder"/> created or written after <paramref name="since"/>, by its path from the folder; folders are passed over, since a delete changes their times.</summary>
    internal static string? FileWrittenSince(string folder, DateTimeOffset since)
    {
        var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
        var pending = new Stack<string>();
        pending.Push(folder);
        while (pending.TryPop(out var at))
        {
            foreach (var entry in new DirectoryInfo(at).EnumerateFileSystemInfos("*", options))
            {
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                if (entry is DirectoryInfo directory)
                {
                    pending.Push(directory.FullName);
                    continue;
                }

                if (entry.CreationTimeUtc > since.UtcDateTime || entry.LastWriteTimeUtc > since.UtcDateTime)
                {
                    return Path.GetRelativePath(folder, entry.FullName).Replace('\\', '/');
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Why the purge keeps a folder moved aside at <paramref name="moved"/>, read and never written: reached through a link, a link in it
    /// outside a build folder, something in it written after the move, or a folder that cannot be read; null where the purge would delete
    /// it. The purge's own read, and the inventory's (after review), so a folder its rule can no longer finish is said to the
    /// person rather than retained for ever: the comparison is with the move, so a later write keeps it at every pass.
    /// <para>The manifest beside it, where it reads, says what the slim removed, which the read
    /// passes over (<see cref="WrittenSince"/>). One that does not read says nothing, so the folder is read whole: kept more, never less.</para>
    /// </summary>
    /// <param name="top">A finished tree's folder: its own top's time is read too, since a top-level
    /// entry deleted after the judgement's read leaves no later time on anything that stays. A folder from before, whose top was made when it
    /// was copied or moved, is read as before.</param>
    internal (string Code, string Sentence)? Kept(string folder, DateTimeOffset moved, bool top = false)
    {
        try
        {
            // A link on the way to it, the folder itself included, makes it somewhere else: never walked into.
            return context.Worktrees.LinkBelowTreesRoot(folder) is { } link
                ? (SetAsideKept.Linked, $"it is reached through a link, at {link}")
                : WrittenSince(folder, moved, SlimManifest.Read(SlimManifest.PathFor(folder)).Manifest, top);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (SetAsideKept.Unread, $"it could not be read or deleted ({error.Message.Trim()})");
        }
    }

    /// <summary>
    /// Why a folder moved aside is kept: something in it written after <paramref name="moved"/>, or a link in it outside a build
    /// folder; null where neither. A link under a build folder is a package manager's, which goes as a link: its own
    /// write is read, never what it points at, and it is never walked into.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Each entry's creation time too</b> (as <see cref="TreeStrays"/> reads one): a copy keeps its source's
    /// write time and takes a new creation time, so a file copied in after the move is a write.</para>
    /// <para>🔴 <b>What the slim removed</b>: removing a folder changes the time of the
    /// folder above it, which this read would take for a write after the move and keep for ever. So a path the manifest marks
    /// <c>removing</c> is passed over, never read into; the folder directly above a <c>removed</c> path has its own write time read
    /// against when that removal ended, and passed over while a path below it is still <c>removing</c>. Its creation time, and every
    /// other entry, are read against the move; a removed path there again is read as any entry, a write.</para>
    /// </remarks>
    internal static (string Code, string Sentence)? WrittenSince(string folder, DateTimeOffset moved, SlimManifest? manifest = null, bool top = false)
    {
        var same = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var removing = new HashSet<string>(same);
        var passedOver = new HashSet<string>(same);
        var unknown = new HashSet<string>(same);
        var ended = new Dictionary<string, DateTime>(same);
        foreach (var path in manifest?.Paths ?? [])
        {
            var above = path.Path.LastIndexOf('/') is var slash and > 0 ? path.Path[..slash] : "";
            if (path.State == SlimStates.Removing)
            {
                removing.Add(path.Path);
                passedOver.Add(above);
            }
            else if (path.State == SlimStates.Removed && path.Ended is { } stamped)
            {
                // The manifest keeps a moment to the millisecond, floored, and the folder's own time to 100 ns: a removal that ended within
                // the millisecond it is stamped with leaves the folder above later than its stamp, so the stamp is read as that millisecond's end
                // (seen once the read under `.going` met a folder's time the first read had not yet).
                var end = stamped.UtcDateTime.AddMilliseconds(1);
                ended[above] = ended.TryGetValue(above, out var was) && was > end ? was : end;
            }
            else if (path.State == SlimStates.Removed)
            {
                // A removal whose end is not known reads the folder above against the move: kept more, never less.
                unknown.Add(above);
            }
        }

        var after = moved.UtcDateTime;
        DateTime Since(string named) => !unknown.Contains(named) && ended.TryGetValue(named, out var end) && end > after ? end : after;
        var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
        var pending = new Stack<string>();
        pending.Push(folder);
        while (pending.TryPop(out var at))
        {
            foreach (var entry in new DirectoryInfo(at).EnumerateFileSystemInfos("*", options))
            {
                var named = Path.GetRelativePath(folder, entry.FullName).Replace('\\', '/');
                if (removing.Contains(named)) continue;
                var link = entry.Attributes.HasFlag(FileAttributes.ReparsePoint);
                if (link && !TreeScratch.UnderBuildFolder(named)) return (SetAsideKept.Linked, $"it holds a link, at {named}");
                var written = entry.CreationTimeUtc > after || (!passedOver.Contains(named) && entry.LastWriteTimeUtc > Since(named));
                if (written) return (SetAsideKept.Written, $"something in it was written after it was moved aside: {named}");
                if (entry is DirectoryInfo directory && !link) pending.Push(directory.FullName);
            }
        }

        // A finished tree's own top, read last: an entry deleted there leaves no later time on anything that stays.
        if (top && !passedOver.Contains("") && new DirectoryInfo(folder).LastWriteTimeUtc > Since(""))
        {
            return (SetAsideKept.Written, "something at its top was added or removed after it was judged");
        }

        return null;
    }
}
