using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>How far a walk may go before it answers what it counted (the second opinion's point 3).</summary>
/// <param name="Files">The most files it counts.</param>
/// <param name="Time">The longest it walks.</param>
public sealed record WiringBudget(int Files, TimeSpan Time)
{
    /// <summary>Enough for an owner's folder of many trees, and bounded where one holds more.</summary>
    public static WiringBudget Default { get; } = new(400_000, TimeSpan.FromSeconds(60));
}

/// <summary>One measure of a Daoris folder: each entry's bytes, each name its table does not hold, and each top folder walked whole.</summary>
public sealed class SizeReading
{
    /// <summary>When the walk ended.</summary>
    public required DateTimeOffset Measured { get; init; }

    /// <summary>Whether the walk stopped short: what it counted is still answered, and <see cref="Why"/> says why.</summary>
    public required bool Partial { get; init; }

    /// <summary><c>budget</c> or <c>cancelled</c> for a partial walk; null for a whole one.</summary>
    public string? Why { get; init; }

    /// <summary>How many files were counted.</summary>
    public required int Counted { get; init; }

    /// <summary>Each entry of D172's table, by name, and its bytes where it is found (its new place or its old one).</summary>
    public required IReadOnlyDictionary<string, long> Entries { get; init; }

    /// <summary>Each name at the home's top that no entry holds, and its bytes.</summary>
    public required IReadOnlyDictionary<string, long> LeftOver { get; init; }

    /// <summary>Each top folder of the Daoris folder walked whole, by its id (<c>app</c>, <c>update</c>, a category beside the home).</summary>
    public required IReadOnlyDictionary<string, long> Folders { get; init; }

    /// <summary>The entries, names and folders a part of which could not be read: their bytes are as far as the walk got.</summary>
    public required IReadOnlySet<string> Unreadable { get; init; }
}

/// <summary>
/// The sizes This Daoris shows (WIRING1b, the this-Daoris design §3.1 and its second opinion's point 3): measured, never
/// guessed; never walked on a page's open, only when asked, in the background, one walk per folder at a time; kept for the
/// process, so every window reads the last measure; cancellable; bounded by a budget, answering what it counted past it.
/// </summary>
/// <remarks>
/// <para><b>Each file is counted once, for the entry that holds it.</b> D172's places nest (a kept file under a session's
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
        // The window's engine before D92.
        "webview2",
    };

    /// <summary>The top folders of the Daoris folder beside the home, walked whole where they are there (D172 point 1).</summary>
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
    public bool Start(string home, string? root, WiringBudget budget)
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
                MeasureNow(home, root, budget, stop.Token);
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
                // It ended as it was asked to stop.
            }
        }
    }

    /// <summary>Walk now, on this thread, and keep the reading: the terminal's <c>--measure</c>, and what a background walk runs.</summary>
    public SizeReading MeasureNow(string home, string? root, WiringBudget budget, CancellationToken cancellationToken)
    {
        var walk = new Walk(budget, cancellationToken);
        var patterns = Patterns();
        var entries = new Dictionary<string, long>(StringComparer.Ordinal);
        var leftOver = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var folders = new Dictionary<string, long>(StringComparer.Ordinal);
        var unreadable = new HashSet<string>(StringComparer.Ordinal);

        // The home, each file given to the entry that holds it, or to its top name.
        walk.Files(home, (parts, bytes) =>
        {
            if (Holder(patterns, parts) is { } entry) entries[entry] = entries.GetValueOrDefault(entry) + bytes;
            else leftOver[parts[0]] = leftOver.GetValueOrDefault(parts[0]) + bytes;
        }, parts =>
        {
            unreadable.Add(parts.Length == 0 ? "home" : Holder(patterns, parts) ?? $"leftOver:{parts[0]}");
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
        };
        _readings[Key(home)] = reading;
        return reading;
    }

    /// <summary>Whether a name at the home's top is held by an entry of the table: a name that is not is left over.</summary>
    public static bool Held(string name) => Patterns().Any(pattern => pattern.Parts[0].IsMatch(name));

    /// <summary>One place of an entry, its parts each a match for one name, to be compared by how closely each names it.</summary>
    private sealed record Pattern(string Entry, Regex[] Parts, int[] Ranks);

    private static IReadOnlyList<Pattern>? _patterns;

    /// <summary>Every place of every entry of D172's table, new and old; an entry known by the names it holds, each of them.</summary>
    private static IReadOnlyList<Pattern> Patterns() => _patterns ??= [.. Build()];

    private static IEnumerable<Pattern> Build()
    {
        foreach (var entry in HomeEntry.Table)
        {
            foreach (var place in new[] { entry.New, entry.Old }.Distinct(StringComparer.Ordinal))
            {
                var places = place.Length == 0 ? entry.Holds : [place];
                foreach (var each in places)
                {
                    yield return Of(entry.Name, each);
                    // A database's journal and its write-ahead log are the database's (design §3.8).
                    if (each.EndsWith(".db", StringComparison.Ordinal))
                    {
                        foreach (var beside in new[] { "-wal", "-shm", "-journal" }) yield return Of(entry.Name, each + beside);
                    }
                }
            }
        }
    }

    private static Pattern Of(string entry, string place)
    {
        var parts = place.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var options = OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase | RegexOptions.CultureInvariant : RegexOptions.CultureInvariant;
        return new Pattern(
            entry,
            [.. parts.Select(part => new Regex("^" + Regex.Escape(part).Replace(@"\*", ".*", StringComparison.Ordinal) + "$", options))],
            [.. parts.Select(part => part == "*" ? 1 : part.Contains('*') ? 2 : 3)]);
    }

    /// <summary>The entry whose place matches the most closely: compared part by part, a named part above a partial one above a <c>*</c>, any above none.</summary>
    private static string? Holder(IReadOnlyList<Pattern> patterns, string[] parts)
    {
        Pattern? best = null;
        foreach (var pattern in patterns)
        {
            if (pattern.Parts.Length > parts.Length) continue;
            var matches = true;
            for (var at = 0; at < pattern.Parts.Length && matches; at++) matches = pattern.Parts[at].IsMatch(parts[at]);
            if (matches && (best is null || Closer(pattern, best))) best = pattern;
        }

        return best?.Entry;
    }

    private static bool Closer(Pattern pattern, Pattern than)
    {
        for (var at = 0; at < Math.Max(pattern.Ranks.Length, than.Ranks.Length); at++)
        {
            var a = at < pattern.Ranks.Length ? pattern.Ranks[at] : 0;
            var b = at < than.Ranks.Length ? than.Ranks[at] : 0;
            if (a != b) return a > b;
        }

        return false;
    }

    private static string Key(string home)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
        return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
    }

    private static bool SamePlace(string a, string b) => Key(a) == Key(b);

    /// <summary>One walk's budget, its count and why it stopped short.</summary>
    private sealed class Walk(WiringBudget budget, CancellationToken cancellationToken)
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        public int Counted { get; private set; }

        public string? Stopped { get; private set; }

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
                    file(under, bytes);
                }
            }
        }
    }
}
