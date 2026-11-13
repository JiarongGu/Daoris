namespace Daoris.Driver;

/// <summary>
/// The session a run's <c>land</c> judges: the live part of its line, the newest of its branch unit's executors with a tree, by when each was made
/// and whatever order the executors are read in. An earlier part a later one went on from in the same tree never stands for the run: judged in
/// its place it closes as superseded and lands nothing, while the work it went on to waits.
/// </summary>
public static class LandSessions
{
    /// <summary>The branch unit's newest executor with a tree and a quest; else the newest session on the run's own quest with a tree.</summary>
    public static SessionRecord? Of(LandStart start, IReadOnlyList<SessionRecord> records) =>
        Newest(start, records, each => each is { Tree.Length: > 0, Quest.Length: > 0 })
        ?? records.Where(each => !each.Teammate && each is { Tree.Length: > 0 } && string.Equals(each.Quest, start.Run, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(each => each.Created).FirstOrDefault();

    /// <summary>A conversation's run, which has no quest: the branch unit's newest executor with a tree.</summary>
    public static SessionRecord? OfChat(LandStart start, IReadOnlyList<SessionRecord> records) =>
        Newest(start, records, each => each is { Tree.Length: > 0 });

    /// <summary>The newest executor <paramref name="stands"/> holds of; of two made at once, the later executor.</summary>
    private static SessionRecord? Newest(LandStart start, IReadOnlyList<SessionRecord> records, Func<SessionRecord, bool> stands) =>
        start.Executors
            .Select((id, at) => (Record: records.FirstOrDefault(record => string.Equals(record.Id, id, StringComparison.OrdinalIgnoreCase)), At: at))
            .Where(each => each.Record is not null && stands(each.Record))
            .OrderByDescending(each => each.Record!.Created).ThenByDescending(each => each.At)
            .Select(each => each.Record).FirstOrDefault();
}
