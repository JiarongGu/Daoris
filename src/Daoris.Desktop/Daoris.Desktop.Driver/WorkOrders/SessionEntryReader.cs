namespace Daoris.Driver;

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
    /// that continues another began by its link's why (SESSCTX1b: a hand-over's five, carried on, retried or moved, <see cref="PartBegan.ByLink"/>);
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
            entries.Add((new SessionEntry(key, read.Origin, read.Repository, group, lead.Record.Id, parts) { Head = HeadOf(read.Origin, parts) }, position[lead.Record.Id]));
        }

        return [.. entries.OrderBy(each => each.At).Select(each => each.Entry)];
    }

    /// <summary>The entries asked about, from one look whose git walk is only theirs, and their parts' rows.</summary>
    /// <param name="lastLook">The loop's last verdicts, which a surface with a loop hands so the look need not ask them again.</param>
    /// <param name="waits">What the loop's look holds waiting on an account, where the surface has a loop.</param>
    public static async Task<(IReadOnlyList<SessionEntry> Entries, IReadOnlyList<SessionGrouping> Rows)> LookAsync(
        ServiceClient service, DriverConfig config, SessionWire door, IReadOnlyList<Consideration>? lastLook,
        IReadOnlyList<AccountWait>? waits, string home, Func<SessionEntry, bool> which, CancellationToken cancellationToken)
    {
        // No tree is walked to find the entries (they are the records'); then only their parts' trees are.
        var look = await SessionGroups.LookAsync(service, config, door, lastLook, home, [], cancellationToken).ConfigureAwait(false);
        if (waits is not null) look = look with { Waits = waits };
        var entries = SessionGroups.ReadEntries(look).Where(which).ToList();
        var ids = entries.SelectMany(entry => entry.Parts.Select(part => part.Session)).ToList();
        var holds = new SessionTrees(home);
        look = await SessionGroups.JudgeAsync(look, holds.Holds, holds.WorkAsync, ids, cancellationToken).ConfigureAwait(false);
        return (entries, SessionGroups.Read(look, ids));
    }

    /// <summary>An ask's intake (D65 §1b): the conversation that answers it.</summary>
    private static bool Intake(SessionRecord record) =>
        (record.Ask is not null && record.Quest is null) || record.Entry?.EndsWith("/intake", StringComparison.Ordinal) == true;

    /// <summary>
    /// Where the first part's line ends. An ask's entry whose parts (those no part published) end on more than one line is a set of
    /// sibling quests: none continues another, so it has no head.
    /// </summary>
    private static string? HeadOf(string origin, IReadOnlyList<EntryPart> parts)
    {
        if (origin.StartsWith("ask:", StringComparison.Ordinal)
            && parts.Where(part => part.Began != PartBegan.Asked && part.Head is not null).Select(part => part.Head).Distinct(StringComparer.Ordinal).Count() > 1)
        {
            return null;
        }

        return parts[0].Head;
    }

    private static List<EntryPart> Began(
        IReadOnlyList<SessionRecord> records, IReadOnlyList<SessionGrouping> rows, IReadOnlyDictionary<string, QuestView> quests)
    {
        var parts = new List<EntryPart>();
        for (var at = 0; at < records.Count; at++)
        {
            var record = records[at];
            var prior = record.Quest is null ? null : records.Take(at).LastOrDefault(earlier => earlier.Quest == record.Quest);
            quests.TryGetValue(record.Quest ?? "", out var quest);
            var part = new EntryPart(record.Id, record.Quest, rows[at].Shown, rows[at].Group, PartBegan.Opened) { Head = record.Head };
            parts.Add(record switch
            {
                // SESSCTX1b: every continuation names its link, so the link's why says how the part began; a why this build has no
                // word for, and a record from before with none, read as a hand-over, as every link once did.
                { From: { } from } => part with { Began = PartBegan.ByLink(record.FromWhy), After = from },
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
}
