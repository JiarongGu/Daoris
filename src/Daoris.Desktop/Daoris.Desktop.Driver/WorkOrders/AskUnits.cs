namespace Daoris.Driver;

/// <summary>
/// The ask adapter (FABRIC-UNIT1a; the fabric events design §D2, "read-only, as the quest's space"): each ask the service answered
/// (D65 §1a), read as a <see cref="WorkUnit"/> of kind <see cref="WorkUnitKind.Ask"/>. Its canonical record is the service's ask store
/// and its one writer the service; this reads the answer and writes nothing.
/// </summary>
/// <remarks>
/// Its end is its state as the service derives it: done once every quest it became is done, dropped where the person closed it. The
/// quests it became are linked by id, never copied; its executor is the intake session that served it.
/// </remarks>
public static class AskUnits
{
    /// <summary>An ask unit's id.</summary>
    public static string IdOf(string ask) => $"ask:{ask}";

    /// <summary>Each ask as a unit, in the order given.</summary>
    public static IReadOnlyList<WorkUnit> Read(IReadOnlyList<AskView> asks) => [.. asks.Select(Unit)];

    private static WorkUnit Unit(AskView ask)
    {
        var id = IdOf(ask.Id);
        return new WorkUnit(
            id, WorkUnitKind.Ask, new WorkOwner(null, ask.Workspace, WorkRecord.Asks, WorkRecord.ServiceWriter), new WorkSubject($"#{ask.Id}", null),
            End(ask), QuestUnits.Evidence(id, InventoryKind.Ask, ask.Id, "the person's words and the quests it became", ask.Attachments))
        {
            Executors = ask.Intake is { Length: > 0 } intake ? [intake] : [],
            Links = [.. ask.Quests.Distinct(StringComparer.Ordinal).Select(quest => new WorkLink(WorkRelation.Produced, QuestUnits.IdOf(quest)))],
            // FABRIC-CUTOVER1c: the ask as answered, whose go-aheads a run's go-ahead node observes.
            Records = WorkUnitRecords.None with { Ask = ask },
        };
    }

    private static MilestoneEnd End(AskView ask) => ask.State switch
    {
        "Done" => new MilestoneEnd(WorkEndState.Ended, "done", null, null, null, "every quest it became is done"),
        "Closed" => new MilestoneEnd(WorkEndState.Dropped, "closed", null, null, null,
            ask.Note is { Length: > 0 } note ? $"the person closed it: {note}" : "the person closed it"),
        "Published" => new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "a quest it became is not done yet"),
        "Proposed" => new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "a tier proposed where it belongs, and nothing is published yet"),
        "Open" => new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "nothing has answered it yet"),
        _ => new MilestoneEnd(WorkEndState.Unknown, null, null, null, null,
            $"the service says `{ask.State}`, which this reader does not know, so its end is not said"),
    };
}
