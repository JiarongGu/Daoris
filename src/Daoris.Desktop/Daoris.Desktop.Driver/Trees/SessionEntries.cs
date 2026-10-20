namespace Daoris.Driver;

/// <summary>How a part of an entry began (D167, the co-working design §1.3), the words <see cref="EntryPart.Began"/> carries.</summary>
public static class PartBegan
{
    /// <summary>A first start of its quest, or a conversation's own.</summary>
    public const string Opened = "opened";

    /// <summary>Another start of a quest whose earlier session failed.</summary>
    public const string Retried = "retried";

    /// <summary>The quest's session went on once what it waited on closed (D79), or on its own words' record.</summary>
    public const string Resumed = "resumed";

    /// <summary>A start after a cut-off (D80), or a chain's next step (D145).</summary>
    public const string CarriedOn = "carried-on";

    /// <summary>A record that continues another by D163's <c>from</c>: another agent or account, or a conversation started from words.</summary>
    public const string HandedOver = "handed-over";

    /// <summary>
    /// A record that went on from one whose own conversation was proven lost (SESSCTX1b, <see cref="HandOverWhy.Resume"/>; the
    /// session-context design §7): a move of its own, never a hand-over.
    /// </summary>
    public const string Moved = "moved";

    /// <summary>A quest another session published, to this repository: a sibling's request.</summary>
    public const string Asked = "asked";
}

/// <summary>One record of an entry (D167): a part, with where it is listed and how it began.</summary>
/// <param name="Session">The record's id.</param>
/// <param name="Quest">The quest it serves, or null.</param>
/// <param name="Shown">The word its row shows (<see cref="SessionGrouping.Shown"/>).</param>
/// <param name="Group">The group its own row is in, one of <see cref="SessionGroup"/>.</param>
/// <param name="Began">One of <see cref="PartBegan"/>.</param>
public sealed record EntryPart(string Session, string? Quest, string Shown, string Group, string Began)
{
    /// <summary>The earlier part it follows: what it retried, resumed or carried on, or the session a hand-over continues.</summary>
    public string? After { get; init; }

    /// <summary>For <see cref="PartBegan.Asked"/>: the repository that asked.</summary>
    public string? AskedBy { get; init; }
}

/// <summary>
/// One line of work in one repository (D167, the design §1.1): the unit the person counts, read from the records that share a
/// key.
/// </summary>
/// <param name="Key">The service's key, <c>&lt;origin&gt;/&lt;repository&gt;</c>, read as an opaque string.</param>
/// <param name="Origin">The text before the key's first <c>/</c>: <c>ask:&lt;id&gt;</c>, <c>quest:&lt;id&gt;</c>, <c>session:&lt;id&gt;</c>.</param>
/// <param name="Repository">The text after it: where the line ran, or <c>intake</c> for an ask's intake.</param>
/// <param name="Group">The first of D126's groups any part qualifies for.</param>
/// <param name="Leading">The part whose state put it there: the newest of that group's.</param>
/// <param name="Parts">Its parts, oldest first, so the newest is last.</param>
public sealed record SessionEntry(string Key, string Origin, string Repository, string Group, string Leading, IReadOnlyList<EntryPart> Parts);

public static class SessionEntries
{
    /// <summary>
    /// The entries, in the groups' order and, within a group, in the order its leading parts' rows are (D126 §2.1): each the
    /// first of the groups any part qualifies for (SESSONE1b, D167, the design §1.6).
    /// </summary>
    /// <remarks>
    /// <para><b>The key is opaque.</b> Records are grouped on the string the service answers, which a record from before it is
    /// answered derived there (<see cref="SessionRecord.EntryKey"/> covers a host that sends none); nothing here decides what
    /// joins an entry, so no twin of that rule exists.</para>
    ///
    /// <para><b>An ended intake whose ask became quests leaves the list</b>: it is its ask's, and its ask's page names it. Its ask
    /// became quests where a quest of this look was asked by it, or a session of its ask remains. A running intake, or one that
    /// ended with nothing published, stays, since only the person moves its ask on. The rows (<see cref="SessionGroups.Read"/>) keep it.</para>
    ///
    /// <para><b>How a part began</b> is read from the record's own <c>from</c> and its quest's chain, in this order: a record
    /// that continues another began by its link's why (SESSCTX1b: a hand-over's five, carried on, retried or moved, <see cref="ByLink"/>);
    /// a quest an earlier part served is retried (its last part failed), carried on
    /// (it was cut off) or resumed; a quest another session published is asked of this repository; a chain's step is carried
    /// on; otherwise the part opened.</para>
    /// </remarks>
    /// <param name="only">The sessions asked about: each entry holding one is answered whole, since its other parts decide its group.</param>
    public static IReadOnlyList<SessionEntry> Read(SessionLook look, IReadOnlyCollection<string>? only = null)
    {
        // Every record's place, in the order a list shows them: a part's group is its own row's, and an entry's order is its
        // leading part's row's, so the entries and the rows cannot disagree.
        var rows = SessionGroups.Read(look);
        var rowOf = rows.GroupBy(row => row.Session, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var position = rows.Select((row, at) => (row.Session, at)).GroupBy(each => each.Session, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().at, StringComparer.Ordinal);
        var quests = look.Quests
            .GroupBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var askedOf = look.Records.Where(record => !Intake(record) && record.EntryKey.StartsWith("ask:", StringComparison.Ordinal))
            .Select(record => EntryKey.Parse(record.EntryKey).Origin)
            .Concat(look.Quests.Where(quest => quest.From.StartsWith("ask #", StringComparison.OrdinalIgnoreCase)).Select(quest => "ask:" + quest.From[5..]))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var lines = look.Records
            .Where(record => !(Intake(record) && !record.Live && record.Ask is { } ask && askedOf.Contains("ask:" + ask)))
            .GroupBy(record => record.EntryKey, StringComparer.Ordinal)
            .Where(line => only is null || line.Any(record => only.Contains(record.Id, StringComparer.Ordinal)));

        var entries = new List<(SessionEntry Entry, int At)>();
        foreach (var line in lines)
        {
            var placed = line
                .OrderBy(record => record.Created).ThenBy(record => record.Id, StringComparer.Ordinal)
                .Select(record => (Record: record, Row: rowOf[record.Id]))
                .ToList();
            var group = SessionGroup.Order.First(each => placed.Any(part => part.Row.Group == each));
            var lead = placed.Last(part => part.Row.Group == group);
            var parts = Began(placed.Select(part => part.Record).ToList(), placed.Select(part => part.Row).ToList(), quests);
            var key = line.Key;
            var read = EntryKey.Parse(key);
            entries.Add((new SessionEntry(key, read.Origin, read.Repository, group, lead.Record.Id, parts), position[lead.Record.Id]));
        }

        return [.. entries.OrderBy(each => each.At).Select(each => each.Entry)];
    }

    /// <summary>An ask's intake (D65 §1b): the conversation that answers it.</summary>
    private static bool Intake(SessionRecord record) =>
        (record.Ask is not null && record.Quest is null) || record.Entry?.EndsWith("/intake", StringComparison.Ordinal) == true;

    private static List<EntryPart> Began(
        IReadOnlyList<SessionRecord> records, IReadOnlyList<SessionGrouping> rows, IReadOnlyDictionary<string, QuestView> quests)
    {
        var parts = new List<EntryPart>();
        for (var at = 0; at < records.Count; at++)
        {
            var record = records[at];
            var prior = record.Quest is null ? null : records.Take(at).LastOrDefault(earlier => earlier.Quest == record.Quest);
            quests.TryGetValue(record.Quest ?? "", out var quest);
            var part = new EntryPart(record.Id, record.Quest, rows[at].Shown, rows[at].Group, PartBegan.Opened);
            parts.Add(record switch
            {
                // SESSCTX1b: every continuation names its link, so the link's why says how the part began; a why this build has no
                // word for, and a record from before with none, read as a hand-over, as every link once did.
                { From: { } from } => part with { Began = ByLink(record.FromWhy), After = from },
                _ when prior is not null => part with
                {
                    Began = prior.State switch
                    {
                        "failed" => PartBegan.Retried,
                        "stopped" or "stood-down" => PartBegan.CarriedOn,
                        _ => PartBegan.Resumed,
                    },
                    After = prior.Id,
                },
                _ when quest?.PublishedBy is { Length: > 0 } => part with { Began = PartBegan.Asked, AskedBy = quest.From },
                _ when quest?.Parent is { Length: > 0 } => part with { Began = PartBegan.CarriedOn },
                _ => part,
            });
        }

        return parts;
    }

    /// <summary>How a part with a <c>from</c> link began, by the link's why (SESSCTX1b, the session-context design §7).</summary>
    internal static string ByLink(string? why) => why switch
    {
        HandOverWhy.CarryOn => PartBegan.CarriedOn,
        HandOverWhy.Retry => PartBegan.Retried,
        HandOverWhy.Resume => PartBegan.Moved,
        _ => PartBegan.HandedOver,
    };
}
