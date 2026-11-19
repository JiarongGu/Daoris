using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>A path under the Daoris folder no <see cref="HomeEntry"/> names: <c>/</c>-separated from the folder, with the bytes under it.</summary>
public sealed record HomeStray(string Path, long Bytes);

/// <summary>What <see cref="HomeStrays.Read"/> found: each stray, and why the sizes are short where a walk stopped early.</summary>
public sealed record HomeStrayReading(IReadOnlyList<HomeStray> Paths, string? Partial)
{
    public long Bytes => Paths.Sum(path => path.Bytes);
}

/// <summary>
/// The home's files that no entry of the table names (HOMEORPHAN1): a place a build dropped from the table while an install still holds
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
    public static HomeStrayReading Cached(string home)
    {
        var key = Path.GetFullPath(home);
        lock (Kept)
        {
            if (Kept.TryGetValue(key, out var held) && DateTimeOffset.UtcNow - held.At < TimeSpan.FromMinutes(5)) return held.Reading;
        }

        var reading = Read(home);
        lock (Kept) Kept[key] = (DateTimeOffset.UtcNow, reading);
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

    public static HomeStrayReading Read(string home, WiringBudget budget)
    {
        var folder = DaorisHome.FolderOf(home);
        var strays = new List<string>();
        if (Directory.Exists(folder))
        {
            var patterns = HomeEntry.Table.Select(entry => entry.New.Split('/', StringSplitOptions.RemoveEmptyEntries)).ToList();
            Walk(folder, 0, patterns, strays, string.Empty);
        }

        var sizes = new List<HomeStray>();
        string? partial = null;
        var left = budget;
        foreach (var relative in strays.Order(StringComparer.Ordinal))
        {
            var place = System.IO.Path.Combine([folder, .. relative.Split('/')]);
            var measure = WiringSizes.MeasurePlaces([place], [], left, CancellationToken.None);
            sizes.Add(new HomeStray(relative, measure.Bytes));
            partial ??= measure.Partial;
            left = left with { Files = Math.Max(1, left.Files - measure.Files) };
        }

        return new(sizes, partial);
    }

    private static void Walk(string folder, int depth, List<string[]> patterns, List<string> strays, string at)
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
            var matching = patterns.Where(parts => parts.Length > depth && Matches(parts[depth], name)).ToList();
            if (matching.Count == 0)
            {
                strays.Add(relative);
            }
            else if (matching.All(parts => parts.Length > depth + 1) && Directory.Exists(child)
                && !new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                Walk(child, depth + 1, matching, strays, relative);
            }
        }
    }

    private static bool IsInstalls(string name) =>
        string.Equals(name, DaorisHome.InstallApp, PathCase) || string.Equals(name, DaorisHome.InstallMarker, PathCase)
        || string.Equals(name, Purged, PathCase);

    private static StringComparison PathCase =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool Matches(string part, string name) =>
        part.Contains('*')
            ? Regex.IsMatch(name, "^" + Regex.Escape(part).Replace("\\*", ".*", StringComparison.Ordinal) + "$",
                OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase : RegexOptions.None)
            : string.Equals(part, name, PathCase);
}
