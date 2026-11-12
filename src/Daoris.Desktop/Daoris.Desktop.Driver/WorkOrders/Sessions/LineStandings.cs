namespace Daoris.Driver;

/// <summary>
/// A line's state, read through its membership (<see cref="LineKeys"/>) and its co-workers, so every part of a line and every
/// co-worker serving one shows the one state: a line whose newest part works while an earlier part ended reads working on each.
/// </summary>
/// <remarks>
/// <para><b>The first that holds</b>: needs you, working and pending, each from the newest part in it, needs you and working also from
/// a co-worker in it; else as its newest part ended. A co-worker's pending or ending says nothing of the line.</para>
///
/// <para><b>A teammate's record is on no line here</b>, read by its own row; so is a co-worker of a session this machine does not hold.</para>
/// </remarks>
internal static class LineStandings
{
    private static readonly string[] Ongoing = [SessionGroup.NeedsYou, SessionGroup.Working, SessionGroup.Pending];

    /// <summary>Each row with its line's standing; a row whose record the look does not hold keeps none.</summary>
    public static IReadOnlyList<SessionGrouping> With(IReadOnlyList<SessionGrouping> rows, IReadOnlyList<SessionRecord> records)
    {
        var rowOf = rows.GroupBy(row => row.Session, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var held = records.DistinctBy(record => record.Id, StringComparer.Ordinal).Where(record => rowOf.ContainsKey(record.Id)).ToList();
        var priors = AtWorkExchangeReading.Priors(held);
        var coworkers = held.Where(record => Served(record, priors) is not null).OrderBy(record => record.Created)
            .ToLookup(record => LineKeys.Of(record.Serves!, priors).Key, StringComparer.Ordinal);

        var standings = new Dictionary<string, LineStanding>(StringComparer.Ordinal);
        foreach (var record in held.Where(record => !standings.ContainsKey(record.Id)))
        {
            var key = LineKeys.Of(Served(record, priors) ?? record.Id, priors);
            var parts = LineKeys.Members(key, priors).Select(member => member.Session).Where(rowOf.ContainsKey).ToList();
            if (parts.Count == 0) parts = [record.Id];
            var serving = coworkers[key.Key].ToList();
            var standing = Read(parts, serving, rowOf);
            foreach (var each in parts.Concat(serving.Select(coworker => coworker.Id))) standings[each] = standing;
        }

        return [.. rows.Select(row => standings.TryGetValue(row.Session, out var standing) ? row with { Line = standing } : row)];
    }

    /// <summary>The part a co-worker serves where this machine holds it, or null for every other record.</summary>
    internal static string? Served(SessionRecord record, IReadOnlyDictionary<string, PriorSession> priors) =>
        !record.Teammate && LineRole.Of(record) is not null && priors.TryGetValue(record.Serves!, out var served) && !served.Teammate
            ? record.Serves
            : null;

    private static LineStanding Read(IReadOnlyList<string> parts, IReadOnlyList<SessionRecord> coworkers, IReadOnlyDictionary<string, SessionGrouping> rowOf)
    {
        foreach (var state in Ongoing)
        {
            if (parts.LastOrDefault(part => rowOf[part].State == state) is { } part) return Of(rowOf[part], LineRole.Part);
            if (state != SessionGroup.Pending && coworkers.LastOrDefault(coworker => rowOf[coworker.Id].State == state) is { } by)
            {
                return Of(rowOf[by.Id], LineRole.Of(by)!);
            }
        }

        return Of(rowOf[parts[^1]], LineRole.Part);
    }

    private static LineStanding Of(SessionGrouping row, string role) => new(row.State, row.Group, row.Reason, row.Session, role);
}
