using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The places of the home's table as the folder a walk reads sees them: each entry's new place is under the Daoris folder and its
/// old one under the old home (<see cref="DaorisHome.FolderOf"/>), so a walk of the home (an install's <c>data/</c>, or a plain
/// home) matches each place by its parts below that home. A database's journal, write-ahead log, shared memory and wake file are
/// the database's.
/// </summary>
public static class WiringPlaces
{
    /// <summary>One place of an entry below the walked home, its parts each a match for one name, to be compared by how closely each names it.</summary>
    public sealed record Pattern(string Entry, Regex[] Parts, int[] Ranks);

    private static readonly ConcurrentDictionary<string, IReadOnlyList<Pattern>> Cache = new(StringComparer.Ordinal);

    /// <summary>Whether a name at the top of <paramref name="home"/> is held by an entry of the table: a name that is not is left over.</summary>
    public static bool Held(string home, string name) => Of(home).Any(pattern => pattern.Parts[0].IsMatch(name));

    /// <summary>Every place of every entry of the table that lies in <paramref name="home"/>, new and old.</summary>
    public static IReadOnlyList<Pattern> Of(string home) => Cache.GetOrAdd(Path.GetFullPath(home), full => [.. Build(full)]);

    private static IEnumerable<Pattern> Build(string home)
    {
        var (folder, oldHome) = DaorisHome.FolderOf(home);
        foreach (var entry in HomeEntry.Table)
        {
            // An entry whose old place repeats its new has none older: the place under the old home is one nothing writes.
            var places = entry.Old == entry.New
                ? new[] { (entry.New, folder) }
                : new[] { (entry.New, folder), (entry.Old, oldHome) };
            foreach (var (place, under) in places)
            {
                var names = place.Length == 0 ? entry.Holds : [place];
                foreach (var each in names)
                {
                    foreach (var parts in Below(home, under, each))
                    {
                        yield return Of(entry.Name, parts);
                        if (parts.Length > 0 && parts[^1].EndsWith(".db", StringComparison.Ordinal))
                        {
                            foreach (var beside in new[] { "-wal", "-shm", "-journal", ".wake" })
                            {
                                yield return Of(entry.Name, [.. parts[..^1], parts[^1] + beside]);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>The parts of a place below the walked home: the place's own under its base, the base's way down to the home taken off, or put on.</summary>
    private static IEnumerable<string[]> Below(string home, string under, string place)
    {
        var parts = place.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(under));
        var walked = Path.TrimEndingDirectorySeparator(home);
        var same = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(full, walked, same))
        {
            yield return parts;
            yield break;
        }

        var down = Path.GetRelativePath(walked, full).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (down.Length > 0 && down[0] != "..")
        {
            // The base is inside the walked home: the place's parts come after the way down to it.
            yield return [.. down, .. parts];
            yield break;
        }

        var up = Path.GetRelativePath(full, walked).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (up.Length == 0 || up[0] == ".." || parts.Length <= up.Length) yield break;
        // The walked home is inside the base: only a place that passes through it is in the home, by what lies below.
        for (var at = 0; at < up.Length; at++)
        {
            if (!string.Equals(parts[at], up[at], same)) yield break;
        }

        yield return parts[up.Length..];
    }

    private static Pattern Of(string entry, string[] parts)
    {
        var options = OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase | RegexOptions.CultureInvariant : RegexOptions.CultureInvariant;
        return new Pattern(
            entry,
            [.. parts.Select(part => new Regex("^" + Regex.Escape(part).Replace(@"\*", ".*", StringComparison.Ordinal) + "$", options))],
            [.. parts.Select(part => part == "*" ? 1 : part.Contains('*') ? 2 : 3)]);
    }

    /// <summary>The entry whose place matches the most closely: compared part by part, a named part above a partial one above a <c>*</c>, any above none.</summary>
    public static string? Holder(IReadOnlyList<Pattern> patterns, string[] parts)
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
}
