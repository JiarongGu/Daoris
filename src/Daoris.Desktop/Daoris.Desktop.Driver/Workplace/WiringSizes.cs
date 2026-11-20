using System.Collections.Concurrent;

namespace Daoris.Driver;
/// <summary>One measure of a Daoris folder: each entry's bytes, each name its table does not hold, and each top folder walked whole.</summary>
public sealed class SizeReading
{
    public required DateTimeOffset Measured { get; init; }

    public required bool Partial { get; init; }

    public string? Why { get; init; }

    public required int Counted { get; init; }

    /// <summary>Each entry of the table, by name, and its bytes where it is found (its new place or its old one).</summary>
    public required IReadOnlyDictionary<string, long> Entries { get; init; }

    /// <summary>Each name at the home's top that no entry holds, and its bytes.</summary>
    public required IReadOnlyDictionary<string, long> LeftOver { get; init; }

    /// <summary>Each top folder of the Daoris folder walked whole, by its id (<c>app</c>, <c>update</c>, a category beside the home).</summary>
    public required IReadOnlyDictionary<string, long> Folders { get; init; }

    /// <summary>The entries, names and folders a part of which could not be read: their bytes are as far as the walk got.</summary>
    public required IReadOnlySet<string> Unreadable { get; init; }

    /// <summary>Each project folder Daoris left in the person's tool home, by name, measured with the home; null where none is asked.</summary>
    public IReadOnlyDictionary<string, ToolMeasure>? Tool { get; init; }
}

/// <summary>
/// The sizes This Daoris shows: measured, never
/// guessed; never walked on a page's open, only when asked, in the background, one walk per folder at a time; kept for the
/// process, so every window reads the last measure; cancellable; bounded by a budget, answering what it counted past it.
/// </summary>
/// <remarks>
/// <para><b>Each file is counted once, for the entry that holds it.</b> The places nest (a kept file under a session's
/// folder is <c>work</c>'s, the session's record <c>data</c>'s), so a file is given to the entry whose place matches it most
/// closely, a named part outranking a <c>*</c>; a file no entry holds is its top name's, a name the table does not hold.</para>
///
/// <para><b>No link or junction is followed</b>: a folder that is one is not walked, so a walk never leaves the folder or
/// counts a tree twice.</para>
/// </remarks>
public sealed class WiringSizes
{
    /// <summary>This process's: what every window of the desktop reads.</summary>
    public static WiringSizes Shared { get; } = new();

    private readonly ConcurrentDictionary<string, SizeReading> _readings = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (Task Walk, CancellationTokenSource Stop)> _walking = new(StringComparer.Ordinal);

    /// <summary>The names an earlier build of Daoris wrote at the home's top, which no entry holds now.</summary>
    public static IReadOnlySet<string> EarlierBuild { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // The window's engine of an earlier build.
        "webview2",
    };

    /// <summary>The top folders of the Daoris folder beside the home, walked whole where they are there.</summary>
    public static IReadOnlyList<string> TopFolders { get; } = ["app", "update", "data", "profiles", "work", "cache", "run", "logs"];

    /// <summary>The last measure of <paramref name="home"/>, or null where none has ended.</summary>
    public SizeReading? Of(string home) => _readings.TryGetValue(Key(home), out var reading) ? reading : null;

    /// <summary>Whether a walk of <paramref name="home"/> is under way.</summary>
    public bool Measuring(string home) => _walking.ContainsKey(Key(home));

    /// <summary>
    /// Start a walk in the background, or nothing where one is under way: true where one started. Its reading replaces the last
    /// when it ends.
    /// </summary>
    /// <param name="root">The Daoris folder the home is in, whose other top folders are walked whole; null walks the home alone.</param>
    /// <param name="tool">The project folders in the person's tool home to measure with it; none measures none.</param>
    public bool Start(string home, string? root, WiringBudget budget, IReadOnlyList<ToolFolder>? tool = null)
    {
        var key = Key(home);
        var stop = new CancellationTokenSource();
        var started = new TaskCompletionSource();
        if (!_walking.TryAdd(key, (started.Task, stop)))
        {
            stop.Dispose();
            return false;
        }

        _ = Task.Run(() =>
        {
            try
            {
                MeasureNow(home, root, budget, stop.Token, tool);
            }
            finally
            {
                _walking.TryRemove(key, out _);
                stop.Dispose();
                started.TrySetResult();
            }
        });
        return true;
    }

    /// <summary>Completes when the walk under way, if any, has ended.</summary>
    public Task Finished(string home) => _walking.TryGetValue(Key(home), out var walk) ? walk.Walk : Task.CompletedTask;

    /// <summary>Stop the walk under way, if any: it ends with what it counted, said as cancelled.</summary>
    public void Cancel(string home)
    {
        if (_walking.TryGetValue(Key(home), out var walk))
        {
            try
            {
                walk.Stop.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // It stopped when asked.
            }
        }
    }

    /// <summary>Walk now, on this thread, and keep the reading: the terminal's <c>--measure</c>, and what a background walk runs.</summary>
    public SizeReading MeasureNow(
        string home, string? root, WiringBudget budget, CancellationToken cancellationToken, IReadOnlyList<ToolFolder>? tool = null)
    {
        var walk = new FolderWalk(budget, cancellationToken);
        var patterns = WiringPlaces.Of(home);
        var entries = new Dictionary<string, long>(StringComparer.Ordinal);
        var leftOver = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var folders = new Dictionary<string, long>(StringComparer.Ordinal);
        var unreadable = new HashSet<string>(StringComparer.Ordinal);

        // The home, each file given to the entry that holds it, or to its top name.
        walk.Files(home, (parts, bytes) =>
        {
            if (WiringPlaces.Holder(patterns, parts) is { } entry) entries[entry] = entries.GetValueOrDefault(entry) + bytes;
            else leftOver[parts[0]] = leftOver.GetValueOrDefault(parts[0]) + bytes;
        }, parts =>
        {
            unreadable.Add(parts.Length == 0 ? "home" : WiringPlaces.Holder(patterns, parts) ?? $"leftOver:{parts[0]}");
        });

        // The Daoris folder's other top folders, each whole.
        if (root is not null && !SamePlace(root, home))
        {
            foreach (var id in TopFolders)
            {
                var folder = Path.Combine(root, id);
                if (SamePlace(folder, home) || !Directory.Exists(folder)) continue;
                folders[id] = 0;
                walk.Files(folder, (_, bytes) => folders[id] += bytes, _ => unreadable.Add($"folder:{id}"));
            }
        }

        var reading = new SizeReading
        {
            Measured = DateTimeOffset.UtcNow,
            Partial = walk.Stopped is not null,
            Why = walk.Stopped,
            Counted = walk.Counted,
            Entries = entries,
            LeftOver = leftOver,
            Folders = folders,
            Unreadable = unreadable,
            Tool = tool is null ? null : ToolHomeLeftovers.Measure(tool, budget, cancellationToken),
        };
        _readings[Key(home)] = reading;
        return reading;
    }

    /// <summary>
    /// One folder's bytes and its newest file's time, walked as the home is (no link followed, the same budget), nothing read
    /// inside a file. <c>Partial</c> says any read failed: the walk stopped short, a folder or a file's size or time
    /// could not be read, so the bytes are as far as it got and the newest time may be older than the truth.
    /// </summary>
    public static (long Bytes, DateTimeOffset? Newest, bool Partial) MeasureFolder(string folder, WiringBudget budget, CancellationToken cancellationToken)
    {
        DateTime? newest = null;
        var bytes = 0L;
        var failed = false;
        var walk = new FolderWalk(budget, cancellationToken)
        {
            Seen = file =>
            {
                try
                {
                    if (newest is null || file.LastWriteTimeUtc > newest) newest = file.LastWriteTimeUtc;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // Activity it cannot see is activity unknown: the folder counts as not fully read.
                    failed = true;
                }
            },
        };
        walk.Files(folder, (_, size) => bytes += size, _ => failed = true);
        return (bytes, newest is { } at ? new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc)) : null, failed || walk.Stopped is not null);
    }

    private static string Key(string home) => PlacesWalk.Key(home);

    private static bool SamePlace(string a, string b) => Key(a) == Key(b);
}
