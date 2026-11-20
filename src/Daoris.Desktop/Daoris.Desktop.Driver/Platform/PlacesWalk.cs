using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>How far a walk may go before it answers what it counted: the most files, the longest time.</summary>
public sealed record WiringBudget(int Files, TimeSpan Time)
{
    public static WiringBudget Default { get; } = new(400_000, TimeSpan.FromSeconds(60));
}

/// <summary>One kind's places walked (<see cref="PlacesWalk.Measure"/>): bytes, files, why it stopped short (null where whole), each group's bytes.</summary>
public sealed record PlacesMeasure(long Bytes, int Files, string? Partial, IReadOnlyDictionary<string, long> Groups);

/// <summary>The bounded walk of places that every measure of bytes shares, owned at the platform so a home's own reader can size what it finds.</summary>
public static class PlacesWalk
{
    /// <summary>A path as a key: full, without a trailing separator, upper-cased where the file system ignores case.</summary>
    public static string Key(string home)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
        return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
    }

    /// <summary>
    /// One kind's places walked together under one budget: each a folder or a file, none followed
    /// through a link, a place under <paramref name="skip"/> passed over (another kind's), so one large kind never starves the rest.
    /// Where <paramref name="groupDepth"/> is more than none, each file's bytes are kept too under its first parts below its place,
    /// joined by <c>/</c>: the set-aside kind's folders, which its <i>next</i> reads.
    /// </summary>
    public static PlacesMeasure Measure(
        IReadOnlyList<string> places, IReadOnlyCollection<string> skip, WiringBudget budget, CancellationToken cancellationToken, int groupDepth = 0)
    {
        var skipped = new HashSet<string>(skip.Select(Key), StringComparer.Ordinal);
        var walk = new FolderWalk(budget, cancellationToken) { Skip = path => skipped.Contains(Key(path)) };
        var bytes = 0L;
        var groups = new Dictionary<string, long>(StringComparer.Ordinal);
        var failed = false;
        foreach (var place in places)
        {
            if (walk.Stopped is not null) break;
            if (skipped.Contains(Key(place))) continue;
            try
            {
                var file = new FileInfo(place);
                if (file.Exists)
                {
                    // A link is never followed, a file one included.
                    if (file.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    walk.Count();
                    bytes += file.Length;
                    continue;
                }

                if (!Directory.Exists(place) || new DirectoryInfo(place).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                failed = true;
                continue;
            }

            walk.Files(place, (parts, size) =>
            {
                bytes += size;
                if (groupDepth > 0 && parts.Length > groupDepth)
                {
                    var group = string.Join('/', parts.Take(groupDepth));
                    groups[group] = groups.GetValueOrDefault(group) + size;
                }
            }, _ => failed = true);
        }

        return new(bytes, walk.Counted, walk.Stopped ?? (failed ? "unreadable" : null), groups);
    }
}

/// <summary>One walk's budget, its count and why it stopped short.</summary>
internal sealed class FolderWalk(WiringBudget budget, CancellationToken cancellationToken)
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public int Counted { get; private set; }

    public string? Stopped { get; private set; }

    /// <summary>Told each file counted, for a walk that wants more of it than its size (the newest file, say).</summary>
    public Action<FileInfo>? Seen { get; init; }

    /// <summary>A path the walk passes over, folder or file, by its full path (another kind's place).</summary>
    public Func<string, bool>? Skip { get; init; }

    /// <summary>A file counted outside <see cref="Files"/>, a place that is itself a file.</summary>
    public void Count() => Counted++;

    /// <summary>Each file under <paramref name="folder"/>, by its parts under it; a folder that cannot be read, by its parts.</summary>
    public void Files(string folder, Action<string[], long> file, Action<string[]> unreadable)
    {
        var pending = new Stack<(string Path, string[] Parts)>();
        pending.Push((folder, []));
        while (pending.Count > 0 && Stopped is null)
        {
            var (path, parts) = pending.Pop();
            IEnumerable<FileSystemInfo> children;
            try
            {
                children = new DirectoryInfo(path).EnumerateFileSystemInfos().ToArray();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                unreadable(parts);
                continue;
            }

            foreach (var child in children)
            {
                if (cancellationToken.IsCancellationRequested) Stopped = "cancelled";
                else if (Counted >= budget.Files || _clock.Elapsed > budget.Time) Stopped = "budget";
                if (Stopped is not null) return;

                string[] under = [.. parts, child.Name];
                // 🔴 A link or a junction is never followed: it may lead outside the folder, or back into it.
                if (child.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                if (Skip is { } skip && skip(child.FullName)) continue;
                if (child is DirectoryInfo)
                {
                    pending.Push((child.FullName, under));
                    continue;
                }

                long bytes;
                try
                {
                    bytes = ((FileInfo)child).Length;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    unreadable(under);
                    continue;
                }

                Counted++;
                Seen?.Invoke((FileInfo)child);
                file(under, bytes);
            }
        }
    }
}
