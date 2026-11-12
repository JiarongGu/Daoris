namespace Daoris.Driver;

/// <summary>A conversation's id (each record's <c>line</c>, else the part itself) and its parts held here, oldest first.</summary>
/// <param name="Cut"><see cref="StepCut"/> where its first part continues one held here on another conversation: its counts read at least.</param>
public sealed record LineKey(string Key, IReadOnlyList<string> Parts, string? Cut = null)
{
    public const string StepCut = "cut";
}

/// <summary>One part of a conversation: its id, why it continued the part before it (its <c>fromWhy</c>), and its record.</summary>
public sealed record LinePart(string Session, string? Why, PriorSession Record);

/// <summary>
/// A conversation's membership, the one read every reader of a line uses: the records held here whose <c>line</c> names it, in the
/// order they were created. Nothing walks back by <c>from</c> for it; a teammate's record is on no line here.
/// </summary>
public static class LineKeys
{
    public static LineKey Of(string session, IReadOnlyDictionary<string, PriorSession> records)
    {
        records.TryGetValue(session, out var named);
        if (!SessionEventText.IsLocalId(session) || named?.Teammate == true) return new LineKey(session, []);

        var key = named?.Line ?? session;
        var members = Held(key, records);
        var first = members.FirstOrDefault();
        var cut = first?.From is { Length: > 0 } from && records.TryGetValue(from, out var before) && !before.Teammate
            && (before.Line ?? before.Session) != key ? LineKey.StepCut : null;
        return new LineKey(key, members.Count > 0 ? [.. members.Select(member => member.Session)] : [session], cut);
    }

    public static IReadOnlyList<PriorSession> Members(LineKey key, IReadOnlyDictionary<string, PriorSession> records) => Held(key.Key, records);

    /// <summary>The parts of <paramref name="session"/>'s conversation up to it, oldest first.</summary>
    public static IReadOnlyList<LinePart> Upto(string session, IReadOnlyDictionary<string, PriorSession> records)
    {
        var members = Members(Of(session, records), records);
        var at = members.Select(member => member.Session).ToList().IndexOf(session);
        return [.. (at < 0 ? [] : members.Take(at + 1)).Select(member => new LinePart(member.Session, member.From is { Length: > 0 } ? member.FromWhy : null, member))];
    }

    private static List<PriorSession> Held(string key, IReadOnlyDictionary<string, PriorSession> records) =>
        [.. records.Values.Where(record => !record.Teammate && (record.Line ?? record.Session) == key)
            .OrderBy(record => record.Created ?? DateTimeOffset.MinValue).ThenBy(record => record.Session, StringComparer.Ordinal)];
}
