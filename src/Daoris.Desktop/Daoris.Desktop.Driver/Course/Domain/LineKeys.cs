namespace Daoris.Driver;

/// <summary>A line's key and the parts the walk back reached, oldest first.</summary>
/// <param name="Key">The quest's id where a part is a driven run of a quest, else the line's first part's id; the part itself where the walk is cut.</param>
/// <param name="Parts">The ids the walk reached, oldest first; none for a record not this machine's.</param>
/// <param name="Cut">Why the walk is cut (<see cref="CycleCut"/>, <see cref="BoundCut"/>), or null: its line counts read at least.</param>
public sealed record LineKey(string Key, IReadOnlyList<string> Parts, string? Cut = null)
{
    /// <summary>The walk meets a part it already passed.</summary>
    public const string CycleCut = "cycle";

    /// <summary>The walk went past <see cref="LineKeys.Bound"/> hops.</summary>
    public const string BoundCut = "bound";
}

/// <summary>
/// The one key a line is counted by: pure over the service's records every executor
/// already reads. It walks back from a part by <c>from</c>; a part that is a driven run of a quest keys the line by that quest, since every
/// run of the quest here is on the line (the service's walk joins them by its quest hop), so a resume with no <c>from</c> carries its
/// count. A teammate's record, or a part not this machine's, ends the walk before it.
/// </summary>
public static class LineKeys
{
    /// <summary>The most hops a walk follows: the service walk's own bound (<c>Lines.Bound</c>), held to it by <c>fixtures/line-counts.json</c>.</summary>
    public const int Bound = 256;

    /// <summary>The key of the line <paramref name="session"/> is on.</summary>
    public static LineKey Of(string session, IReadOnlyDictionary<string, PriorSession> records)
    {
        if (!SessionEventText.IsLocalId(session) || (records.TryGetValue(session, out var named) && named.Teammate)) return new LineKey(session, []);

        var parts = new List<string> { session };
        var seen = new HashSet<string>(StringComparer.Ordinal) { session };
        string? cut = null;
        var at = session;
        for (var hops = 0; records.TryGetValue(at, out var record) && record.From is { Length: > 0 } from; hops++)
        {
            if (!SessionEventText.IsLocalId(from) || (records.TryGetValue(from, out var before) && before.Teammate)) break;
            if (!seen.Add(from)) { cut = LineKey.CycleCut; break; }
            if (hops == Bound) { cut = LineKey.BoundCut; break; }
            parts.Add(from);
            at = from;
        }

        parts.Reverse();
        if (cut is not null) return new LineKey(session, parts, cut);
        var quest = parts.Select(part => records.TryGetValue(part, out var each) && each.Kind == "driven" ? each.Quest : null)
            .FirstOrDefault(each => each is not null);
        return new LineKey(quest ?? parts[0], parts);
    }

    /// <summary>
    /// The records on the line <paramref name="key"/> names, oldest first: each record that keys to it and each part its walk reached, so a
    /// conversation a driven part continued, and a run of the quest with no <c>from</c>, are on their quest's line.
    /// </summary>
    public static IReadOnlyList<PriorSession> Members(LineKey key, IReadOnlyDictionary<string, PriorSession> records)
    {
        var ids = new HashSet<string>(key.Parts, StringComparer.Ordinal);
        foreach (var record in records.Values.Where(each => !each.Teammate))
        {
            if (ids.Contains(record.Session)) continue;
            var other = Of(record.Session, records);
            if (other.Key == key.Key) ids.UnionWith(other.Parts);
        }

        return [.. ids.Select(id => records.GetValueOrDefault(id)).OfType<PriorSession>().OrderBy(record => record.Created ?? DateTimeOffset.MinValue)];
    }
}
