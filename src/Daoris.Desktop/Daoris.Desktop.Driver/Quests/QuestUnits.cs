namespace Daoris.Driver;

/// <summary>
/// The quest adapter (FABRIC-UNIT1a; the fabric events design §D2): each quest the service answered, read as a <see cref="WorkUnit"/>
/// of kind <see cref="WorkUnitKind.Quest"/>. Its canonical record is the service's quest store and its one writer the service, through
/// its quest exchange; this reads the answer and writes nothing, so a quest's every move stays the service's.
/// </summary>
/// <remarks>
/// <para><b>Its end is its status</b>, never judged here: a done is the end, unless the service holds it for the person's yes or its
/// evidence (D144), which is no end yet; a decline drops it; a status this reader does not know is unknown, never read as ended.</para>
/// <para><b>What it relates to is linked by id</b> (<see cref="WorkLink"/>), never copied onto it: the ask its sender names, the chain's
/// step before it, the question its taker waits on (D79). The branch units its sessions worked are linked by <see cref="WorkUnits.Linked"/>.</para>
/// </remarks>
public static class QuestUnits
{
    /// <summary>A quest unit's id.</summary>
    public static string IdOf(string quest) => $"quest:{quest}";

    /// <summary>Each quest as a unit, in the order given; its executors the sessions whose record serves it, oldest first.</summary>
    public static IReadOnlyList<WorkUnit> Read(IReadOnlyList<QuestView> quests, IReadOnlyList<SessionRecord> sessions) =>
        [.. quests.Select(quest => Unit(quest, sessions))];

    private static WorkUnit Unit(QuestView quest, IReadOnlyList<SessionRecord> sessions)
    {
        var id = IdOf(quest.Id);
        var links = new List<WorkLink>();
        if (AskWords.AskOf(quest.From) is { Length: > 0 } ask) links.Add(new WorkLink(WorkRelation.ProducedBy, AskUnits.IdOf(ask)));
        if (quest.Parent is { Length: > 0 } parent) links.Add(new WorkLink(WorkRelation.Follows, IdOf(parent)));
        if (quest.Awaits is { Length: > 0 } awaits) links.Add(new WorkLink(WorkRelation.Awaits, IdOf(awaits)));

        return new WorkUnit(
            id, WorkUnitKind.Quest, new WorkOwner(quest.To, quest.Workspace, WorkRecord.Quests, WorkRecord.ServiceWriter),
            new WorkSubject($"#{quest.Id}", quest.Updated is { } moved ? Moments.Write(moved) : null), End(quest),
            Evidence(id, InventoryKind.Quest, quest.Id, "its words, its done, its answers and its evidence", quest.Attachments))
        {
            Executors = [.. sessions.Where(session => string.Equals(session.Quest, quest.Id, StringComparison.Ordinal))
                .OrderBy(session => session.Created).Select(session => session.Id)],
            Links = links,
        };
    }

    private static MilestoneEnd End(QuestView quest) => quest.Status switch
    {
        "Done" when quest.Held => new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null,
            $"its done is held{(quest.Hold is { Length: > 0 } hold ? $" ({hold})" : "")}: it waits for the person's yes or its evidence"),
        "Done" => new MilestoneEnd(WorkEndState.Ended, "done", null, null, quest.Updated, "its done is in, and nothing holds it"),
        "Declined" => new MilestoneEnd(WorkEndState.Dropped, "declined", null, null, quest.Updated,
            quest.Note is { Length: > 0 } note ? $"it was declined: {note}" : "it was declined"),
        "Taken" => new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, quest.Awaits is { Length: > 0 } awaits
            ? $"`{quest.To}` took it, and waits on #{awaits}"
            : $"`{quest.To}` took it, and its done is not in yet"),
        "Open" => new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "it is open: nobody has taken it yet"),
        _ => new MilestoneEnd(WorkEndState.Unknown, null, null, null, null,
            $"the service says `{quest.Status}`, which this reader does not know, so its end is not said"),
    };

    /// <summary>
    /// A service record's evidence pointers: the record itself, and each file it carries by name. Kept: the end never removes history.
    /// A file's path is this machine's, and a pointer names none.
    /// </summary>
    internal static IReadOnlyList<InventoryEntry> Evidence(string unit, string kind, string id, string holds, IReadOnlyList<QuestFileView> files) =>
    [
        new InventoryEntry(unit, kind, $"#{id}", InventoryRole.Evidence, InventoryDisposition.Kept,
            $"the service keeps it, with {holds}; the end never removes it"),
        .. files.Select(file => new InventoryEntry(unit, InventoryKind.Attachment, file.Name, InventoryRole.Evidence, InventoryDisposition.Kept,
            $"the service keeps its bytes (sha256 {TextBounds.ShortCommit(file.Sha256, 12)})")),
    ];
}
