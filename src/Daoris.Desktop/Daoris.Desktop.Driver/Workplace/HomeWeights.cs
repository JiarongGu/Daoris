using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>A place of the home over its entry's budget: its path under the Daoris folder, its entry, its bytes and the budget.</summary>
public sealed record HomeOver(string Path, string Entry, long Bytes, long Budget);

/// <summary>One of the largest files of the home.</summary>
public sealed record HomeBigFile(string Path, long Bytes);

/// <summary>Identical files held more than once: each copy's bytes, how many copies, and the first paths holding one (a name of a record already listed is not another).</summary>
public sealed record HomeDuplicates(long Bytes, int Copies, IReadOnlyList<string> Paths)
{
    /// <summary>What the copies beyond the first cost.</summary>
    public long Wasted => Bytes * (Copies - 1);
}

/// <summary>What <see cref="HomeWeights.Read"/> found, and why it is short where its walk stopped early.</summary>
public sealed record HomeWeightReading(
    IReadOnlyList<HomeOver> Over, IReadOnlyList<HomeBigFile> Largest, IReadOnlyList<HomeDuplicates> Duplicates, string? Partial);

/// <summary>The size each place of an entry may reach before the home says it is over; one number per entry, whatever is declared for none is its category's.</summary>
public static class HomeBudgets
{
    private const long Mb = 1L << 20;
    private const long Gb = 1L << 30;

    private static readonly Dictionary<string, long> Named = new(StringComparer.Ordinal)
    {
        // A session tree, with its build outputs; the dependency store's own limit holds the rest of work/.
        ["trees"] = 4 * Gb,
        ["deps"] = 3 * Gb,
        ["knowledge"] = 4 * Gb,
        ["desktop-db"] = 2 * Gb,
        ["memory"] = 2 * Gb,
        ["browser-engine"] = 4 * Gb,
        ["browser-edge"] = 4 * Gb,
        ["agent-homes"] = 4 * Gb,
        ["window"] = 2 * Gb,
        ["toolchain"] = 4 * Gb,
        ["tool-versions"] = 4 * Gb,
        ["bin"] = 2 * Gb,
        ["logs"] = 2 * Gb,
        ["sessions"] = 2 * Gb,
        ["usage-history"] = Gb,
    };

    /// <summary>The budget of <paramref name="entry"/>'s place: its own where one is named, else its category's.</summary>
    public static long Of(string entry) =>
        Named.TryGetValue(entry, out var named) ? named
        : HomeEntry.Named(entry)?.Category switch
        {
            HomeCategory.Work or HomeCategory.Cache or HomeCategory.Profiles => Gb,
            HomeCategory.Logs => 2 * Gb,
            _ => 256 * Mb,
        };
}

/// <summary>
/// What the home weighs, read from <see cref="HomeStrays"/>' one walk: each place over its entry's budget (<see cref="HomeBudgets"/>),
/// the largest files, and the files over the threshold that identical bytes hold in more than one place, hashed only where their
/// sizes match. Names of one file record (a hard link, as a tree's dependencies are) are one copy. A walk that stops at its budget says
/// so and answers what it counted. Said in the machine log at a start and in Disk space.
/// </summary>
public static class HomeWeights
{
    /// <summary>A file under this is neither listed nor weighed for a copy.</summary>
    public const long Threshold = 1L << 20;

    /// <summary>How many largest files, duplicate groups and paths of a group are kept.</summary>
    public const int Top = 20;

    private static readonly Dictionary<string, (DateTimeOffset At, HomeWeightReading Reading)> Kept = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Counting = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The reading with the machine's bounds: a million files, two minutes.</summary>
    public static HomeWeightReading Read(string home) =>
        Read(home, new WiringBudget(1_000_000, TimeSpan.FromMinutes(2)), HomeBudgets.Of, Threshold, Top);

    /// <summary>The walk once over the home, then the files of its places weighed.</summary>
    public static HomeWeightReading Read(string home, WiringBudget budget, Func<string, long> budgetOf, long threshold, int top)
    {
        var clock = Stopwatch.StartNew();
        var walked = HomeStrays.Read(home, budget, sizeEntries: true);
        var folder = DaorisHome.FolderOf(home);
        var over = walked.Walked
            .Where(place => place.Entry is not null && place.Bytes > budgetOf(place.Entry))
            .Select(place => new HomeOver(place.Path, place.Entry!, place.Bytes, budgetOf(place.Entry!)))
            .OrderByDescending(each => each.Bytes)
            .ToList();

        var files = new List<(string Path, string Full, long Bytes)>();
        var walk = new FolderWalk(budget, CancellationToken.None);
        foreach (var place in walked.Walked)
        {
            if (walk.Stopped is not null) break;
            var at = Path.Combine([folder, .. place.Path.Split('/')]);
            try
            {
                var file = new FileInfo(at);
                if (file.Exists)
                {
                    walk.Count();
                    if (!file.Attributes.HasFlag(FileAttributes.ReparsePoint) && file.Length >= threshold) files.Add((place.Path, at, file.Length));
                    continue;
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            walk.Files(at, (parts, size) =>
            {
                if (size >= threshold) files.Add((place.Path + "/" + string.Join('/', parts), Path.Combine([at, .. parts]), size));
            }, _ => { });
        }

        var records = new HashSet<string>(StringComparer.Ordinal);
        var largest = new List<HomeBigFile>();
        foreach (var each in files.OrderByDescending(file => file.Bytes).ThenBy(file => file.Path, StringComparer.Ordinal))
        {
            if (largest.Count >= top) break;
            // One record under many names is listed once, under its first.
            if (FileLinks.IdentityOf(each.Full) is { } record && !records.Add(record)) continue;
            largest.Add(new HomeBigFile(each.Path, each.Bytes));
        }

        var stopped = walk.Stopped;
        var duplicates = Duplicates(files, top, budget.Time - clock.Elapsed, ref stopped);
        return new HomeWeightReading(over, largest, duplicates, walked.Partial ?? stopped);
    }

    private static List<HomeDuplicates> Duplicates(List<(string Path, string Full, long Bytes)> files, int top, TimeSpan left, ref string? stopped)
    {
        var clock = Stopwatch.StartNew();
        var groups = new List<HomeDuplicates>();
        foreach (var same in files.GroupBy(each => each.Bytes).Where(group => group.Count() > 1))
        {
            if (clock.Elapsed > left)
            {
                stopped ??= "budget";
                break;
            }

            // One copy per file record: the names of a link are listed once, as the cost is.
            var records = new Dictionary<string, (string Path, string Full)>(StringComparer.Ordinal);
            foreach (var each in same.OrderBy(file => file.Path, StringComparer.Ordinal))
            {
                records.TryAdd(FileLinks.IdentityOf(each.Full) ?? "path:" + each.Full, (each.Path, each.Full));
            }

            if (records.Count < 2) continue;
            var byHash = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var (path, full) in records.Values)
            {
                try
                {
                    var hash = DependencyManifest.HashOf(full);
                    if (!byHash.TryGetValue(hash, out var paths)) byHash[hash] = paths = [];
                    paths.Add(path);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // A file that cannot be read is not weighed.
                }
            }

            groups.AddRange(byHash.Values.Where(paths => paths.Count > 1).Select(paths => new HomeDuplicates(same.Key, paths.Count, paths)));
        }

        return [.. groups.OrderByDescending(each => each.Wasted).ThenBy(each => each.Paths[0], StringComparer.Ordinal).Take(top)
            .Select(each => each with { Paths = [.. each.Paths.Take(top)] })];
    }

    /// <summary>
    /// The reading kept for an hour, counted in the background: what is kept now (null before the first count ends, which this
    /// starts), so the page that asks never waits for a walk of the home.
    /// </summary>
    public static HomeWeightReading? Cached(string home, DateTimeOffset now)
    {
        var key = Path.GetFullPath(home);
        lock (Kept)
        {
            var held = Kept.TryGetValue(key, out var kept) ? kept : default;
            var fresh = held.Reading is not null && now - held.At < TimeSpan.FromHours(1) && now >= held.At;
            if (!fresh && Counting.Add(key))
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        var reading = Read(home);
                        lock (Kept) Kept[key] = (now, reading);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        // The next ask counts again.
                    }
                    finally
                    {
                        lock (Kept) Counting.Remove(key);
                    }
                });
            }

            return held.Reading;
        }
    }

    /// <summary>
    /// Counts the home once at a start, keeps the reading for <see cref="Cached"/> (so the first Disk space view walks no more), and says
    /// in the machine log what it found: a warning for each place over budget, one for the identical files (how many groups, what they cost
    /// and the first group's paths) and one for the five largest files; nothing where none is. Never throws.
    /// </summary>
    public static void Say(string home, MachineLog log, DateTimeOffset now)
    {
        try
        {
            var reading = Read(home);
            lock (Kept) Kept[Path.GetFullPath(home)] = (now, reading);
            foreach (var each in reading.Over)
            {
                log.Warn("home.over", ("path", each.Path), ("entry", each.Entry), ("bytes", each.Bytes), ("budget", each.Budget));
            }

            if (reading.Duplicates.Count > 0)
            {
                var first = reading.Duplicates[0];
                log.Warn("home.duplicates", ("groups", reading.Duplicates.Count), ("wasted", reading.Duplicates.Sum(each => each.Wasted)),
                    ("firstBytes", first.Bytes), ("firstCopies", first.Copies), ("firstPaths", string.Join(" | ", first.Paths.Take(5))));
            }

            foreach (var each in reading.Largest.Take(5))
            {
                log.Info("home.largest", ("path", each.Path), ("bytes", each.Bytes));
            }

            if (reading.Partial is not null) log.Warn("home.weight.partial", ("why", reading.Partial));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            log.Warn("home.weight.unread", ("error", error.GetType().Name));
        }
    }
}
