using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>A path under the Daoris folder no <see cref="HomeEntry"/> names: <c>/</c>-separated from the folder, with the bytes under it.</summary>
public sealed record HomeStray(string Path, long Bytes);

/// <summary>What <see cref="HomeStrays.Read"/> found: each stray, and why the sizes are short where a walk stopped early.</summary>
public sealed record HomeStrayReading(IReadOnlyList<HomeStray> Paths, string? Partial, IReadOnlyList<HomeWalked> Walked)
{
    public long Bytes => Paths.Sum(path => path.Bytes);
}

/// <summary>A path the walk met: where it is, its bytes, and the entry it falls under (null: none, a stray).</summary>
public sealed record HomeWalked(string Path, long Bytes, string? Entry);

/// <summary>
/// The home's files that no entry of the table names: a place a build dropped from the table while an install still holds
/// files there, which no reader finds. The walk goes down only the folders an entry's place passes through; a path no entry's pattern
/// matches is a stray, named at its first such part and sized whole, and an entry's own children are never named (its pattern covers
/// them). A junction is not followed: it is named, and counts nothing. The install's own <c>app/</c>, its marker and its <c>purged/</c>
/// are the install's, not the home's. The walk is bounded by <see cref="WiringBudget"/>.
/// </summary>
public static class HomeStrays
{
    /// <summary>The folder an install's purge moves what it removes into, which is the install's.</summary>
    private const string Purged = "purged";

    /// <summary>The paths under <paramref name="home"/>'s Daoris folder that no entry names, in path order.</summary>
    public static HomeStrayReading Read(string home) => Read(home, new WiringBudget(100_000, TimeSpan.FromSeconds(10)));

    /// <summary>
    /// The reading for the window, kept for five minutes per home so a page that asks often walks once; a stray is a rare file and
    /// its size is for the person's eye, not a rule.
    /// </summary>
    public static HomeStrayReading Cached(string home, DateTimeOffset now)
    {
        var key = Path.GetFullPath(home);
        lock (Kept)
        {
            if (Kept.TryGetValue(key, out var held) && now - held.At < TimeSpan.FromMinutes(5) && now >= held.At) return held.Reading;
        }

        var reading = Read(home);
        lock (Kept) Kept[key] = (now, reading);
        return reading;
    }

    /// <summary>Says each stray in the machine log at a start: one warning per path, with its bytes; nothing where there is none. Never throws.</summary>
    public static void Say(string home, MachineLog log)
    {
        try
        {
            foreach (var stray in Read(home).Paths)
            {
                log.Warn("home.stray", ("path", stray.Path), ("bytes", stray.Bytes));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            log.Warn("home.stray.unread", ("error", error.GetType().Name));
        }
    }

    private static readonly Dictionary<string, (DateTimeOffset At, HomeStrayReading Reading)> Kept = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The walk once: every place an entry covers and every stray, each with the entry it falls under. With <paramref name="sizeEntries"/>
    /// the covered places are sized too (<see cref="HomeStrayReading.Walked"/>), so a reader of sizes per entry does not walk again.
    /// </summary>
    public static HomeStrayReading Read(string home, WiringBudget budget, bool sizeEntries = false)
    {
        var folder = DaorisHome.FolderOf(home);
        var met = new List<(string Path, string? Entry)>();
        if (Directory.Exists(folder))
        {
            var patterns = HomeEntry.Table.Select(entry => (Entry: entry.Name, Parts: entry.New.Split('/', StringSplitOptions.RemoveEmptyEntries))).ToList();
            Walk(folder, 0, patterns, met, string.Empty);
        }

        var sizes = new List<HomeStray>();
        var walked = new List<HomeWalked>();
        string? partial = null;
        var left = budget;
        foreach (var (relative, entry) in met.OrderBy(each => each.Path, StringComparer.Ordinal))
        {
            if (entry is not null && !sizeEntries) continue;
            var place = System.IO.Path.Combine([folder, .. relative.Split('/')]);
            var measure = PlacesWalk.Measure([place], [], left, CancellationToken.None);
            if (entry is null) sizes.Add(new HomeStray(relative, measure.Bytes));
            walked.Add(new HomeWalked(relative, measure.Bytes, entry));
            partial ??= measure.Partial;
            left = left with { Files = Math.Max(1, left.Files - measure.Files) };
        }

        return new(sizes, partial, walked);
    }

    private static void Walk(string folder, int depth, List<(string Entry, string[] Parts)> patterns, List<(string Path, string? Entry)> strays, string at)
    {
        IEnumerable<string> children;
        try
        {
            children = Directory.EnumerateFileSystemEntries(folder).ToList();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var child in children)
        {
            var name = System.IO.Path.GetFileName(child);
            var relative = at.Length == 0 ? name : at + "/" + name;
            if (depth == 0 && IsInstalls(name)) continue;
            var matching = patterns.Where(row => row.Parts.Length > depth && Matches(row.Parts[depth], name)).ToList();
            if (matching.Count == 0)
            {
                strays.Add((relative, null));
            }
            else if (matching.All(row => row.Parts.Length > depth + 1))
            {
                if (Directory.Exists(child) && !new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    Walk(child, depth + 1, matching, strays, relative);
                }
            }
            else
            {
                strays.Add((relative, matching.First(row => row.Parts.Length == depth + 1).Entry));
            }
        }
    }

    private static bool IsInstalls(string name) =>
        string.Equals(name, DaorisHome.InstallApp, PathCase) || string.Equals(name, DaorisHome.InstallMarker, PathCase)
        || string.Equals(name, Purged, PathCase) || string.Equals(name, DaorisHome.InstallUpdate, PathCase)
        || string.Equals(name, DaorisHome.InstallLauncher, PathCase);

    /// <summary>What a database keeps beside its file while open: part of the entry, as <c>WiringPlaces</c> counts it.</summary>
    private static readonly string[] Beside = ["-wal", "-shm", "-journal", ".wake"];

    private static StringComparison PathCase =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool Matches(string part, string name) =>
        part.Contains('*')
            ? Regex.IsMatch(name, "^" + Regex.Escape(part).Replace("\\*", ".*", StringComparison.Ordinal) + "$",
                OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase : RegexOptions.None)
            : string.Equals(part, name, PathCase) || Beside.Any(suffix => string.Equals(part + suffix, name, PathCase));
}
