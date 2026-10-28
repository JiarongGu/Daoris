namespace Daoris.Driver;

/// <summary>
/// The session a new quest's start continues (SESSONE1d, D167 point 4 as amended; the co-working design §1.5): its entry's key and
/// the parts it already has on this machine, and where the start stands to the entry's tree. A new quest of an entry goes into the
/// tree its newest part here held when nothing live holds it (D51), and otherwise runs alongside in a tree of its own, as a further
/// part of the same entry; nothing waits.
/// </summary>
/// <param name="Key">The entry's key, <c>&lt;origin&gt;/&lt;repository&gt;</c>, as the records answer it.</param>
/// <param name="Parts">The entry's earlier parts here: the last run on this machine of each of its other quests, newest first.</param>
public sealed record EntryParts(string Key, IReadOnlyList<PriorSession> Parts)
{
    /// <summary>The part whose tree the start goes into, free: the newest part's. Null where it runs alongside.</summary>
    public PriorSession? Into { get; init; }

    /// <summary>The live session holding the entry's tree, where the start runs alongside instead; null where none does.</summary>
    public string? HeldBy { get; init; }

    /// <summary>
    /// The key a quest's sessions are opened under, as the records here answer it; null where none names it. The rule is the
    /// service's alone (SESSONE1a, <c>SessionEntries</c>), so it is read and never re-derived: the quest's own last record here,
    /// a start that failed before it, holds it; else the record of the session that published the quest holds its origin, which
    /// the quest's sessions share (the rule's publisher clause, design §1.2 rule 4). The publisher of an ask's quest is the ask's
    /// intake, whose origin is the ask, so an ask's quest reads the ask's line through it too. A quest no record here names, a
    /// person's publish or a chain's step, has no key here, and starts as a first start does.
    /// </summary>
    public static string? KeyOf(QuestView quest, Snapshot snapshot)
    {
        if (snapshot.LastRun.TryGetValue(quest.Id, out var own) && own.Entry is { Length: > 0 } stored) return stored;
        return quest.PublishedBy is { Length: > 0 } by && OriginOf(PublisherEntry(snapshot.Entries, by)) is { } origin
            ? $"{origin}/{quest.To}"
            : null;
    }

    /// <summary>The entry's earlier parts here: the last run of each of its other quests on this machine, newest first.</summary>
    public static IReadOnlyList<PriorSession> Earlier(string key, QuestView quest, Snapshot snapshot) =>
    [
        .. snapshot.LastRun
            .Where(run => !string.Equals(run.Key, quest.Id, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(run.Value.Entry, key, StringComparison.Ordinal)
                          && !run.Value.Teammate)
            .Select(run => run.Value)
            .OrderByDescending(part => part.Created ?? DateTimeOffset.MinValue)
            .ThenBy(part => part.Session, StringComparer.Ordinal),
    ];

    /// <summary>
    /// The publisher's entry: the record this machine knew it by, else a teammate's mirror of it keyed <c>origin/id</c>, as the
    /// service finds a publisher (SESSONE1a, <c>FindOrMirrorAsync</c>).
    /// </summary>
    private static string? PublisherEntry(IReadOnlyDictionary<string, string> entries, string publisher) =>
        entries.TryGetValue(publisher, out var own)
            ? own
            : entries.FirstOrDefault(pair => pair.Key.EndsWith($"/{publisher}", StringComparison.Ordinal)).Value;

    /// <summary>The origin a key holds, what comes before its first slash, as the service reads a stored key; null for none.</summary>
    private static string? OriginOf(string? key) => EntryKey.Parse(key) is { HasSlash: true } parsed ? parsed.Origin : null;
}
