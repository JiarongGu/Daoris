namespace Daoris.Driver;

/// <summary>
/// A branch unit's executors in the order <see cref="WorkUnit.Executors"/> promises, oldest first, whatever named each: a landing names the
/// session that made it, a tree names every session that recorded it, and a session that went on in a tree lands after the one it went on
/// from, so the order they are found in is not their age. The last is the newest, which a unit's readers take for its live part.
/// </summary>
internal static class UnitExecutors
{
    /// <summary>
    /// <paramref name="found"/> in the order this machine's records were made (<paramref name="recorded"/>, oldest first); one no record
    /// names keeps its place among the others found before any recorded one, since its record went first.
    /// </summary>
    public static IReadOnlyList<string> OldestFirst(IEnumerable<string> found, IReadOnlyList<RecordedTree> recorded)
    {
        var made = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var at = 0; at < recorded.Count; at++) made.TryAdd(recorded[at].Session, at);
        return [.. found.Select((session, at) => (Session: session, Found: at))
            .OrderBy(each => made.TryGetValue(each.Session, out var rank) ? rank : -1)
            .ThenBy(each => each.Found)
            .Select(each => each.Session)];
    }
}
