namespace Daoris.Driver;

/// <summary>
/// What an Accept lands is a repository's work of one key: the lines of one ask in one repository, each its own row in Sessions
/// (<see cref="SessionEntry.Line"/>), are one entry here, their parts together in the order the lines are.
/// </summary>
public static class LandingEntries
{
    /// <summary>The entries with those of one key made one, the first's place and its leading part kept; their order is the first's of each key.</summary>
    public static IReadOnlyList<SessionEntry> Together(IEnumerable<SessionEntry> entries) =>
        [.. entries.GroupBy(entry => entry.Key, StringComparer.Ordinal).Select(same =>
        {
            var first = same.First();
            if (same.Count() == 1) return first;
            return first with
            {
                Parts = [.. same.SelectMany(entry => entry.Parts)],
                Coworkers = [.. same.SelectMany(entry => entry.Coworkers)],
                Head = null,
                Line = "",
            };
        })];
}
