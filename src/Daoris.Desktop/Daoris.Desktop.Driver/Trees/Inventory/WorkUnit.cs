namespace Daoris.Driver;

/// <summary>The kinds of work a <see cref="WorkUnit"/> projects (the fabric events design §D2's table), each read by its own adapter.</summary>
public static class WorkUnitKind
{
    /// <summary>A session branch's work: <see cref="WorkInventory.Units"/>, over the driver's own records.</summary>
    public const string Branch = "branch";

    /// <summary>A quest: <see cref="QuestUnits"/>, over the service's quest records.</summary>
    public const string Quest = "quest";

    /// <summary>An ask, the quests' space: <see cref="AskUnits"/>, over the service's ask records.</summary>
    public const string Ask = "ask";
}

/// <summary>Whether a unit is still owed anything (§D3): <see cref="Retired"/> only once its end is over and every entry is terminal.</summary>
public static class WorkUnitState
{
    public const string Open = "open";

    public const string Retired = "retired";
}

/// <summary>Where a unit's end stands, the same words for every kind (<see cref="MilestoneEnd.State"/>).</summary>
public static class WorkEndState
{
    /// <summary>Reached, on its record's evidence: the line holds the branch's work, the quest's or the ask's done.</summary>
    public const string Ended = "ended";

    /// <summary>A branch's work a landing carries, which the line does not hold yet.</summary>
    public const string Handed = "handed";

    /// <summary>Not reached: what waits is in <see cref="MilestoneEnd.Said"/>.</summary>
    public const string NotEnded = "not-ended";

    /// <summary>Over without its result (§D3, "or the person drops it"): a quest declined, an ask the person closed.</summary>
    public const string Dropped = "dropped";

    /// <summary>Not said: no record names its owner, or its record says what this reader does not know. Never read as ended.</summary>
    public const string Unknown = "unknown";

    /// <summary>Whether nothing more comes of it: ended or dropped.</summary>
    public static bool Over(string state) => state is Ended or Dropped;
}

/// <summary>How one unit names another (<see cref="WorkLink"/>): by id, so a reader follows the link rather than reading a copy.</summary>
public static class WorkRelation
{
    /// <summary>The unit whose work this one was: a branch's quest, a quest's ask.</summary>
    public const string ProducedBy = "produced-by";

    /// <summary>The inverse: a quest's branch units, an ask's quests.</summary>
    public const string Produced = "produced";

    /// <summary>The question its taker waits on (D79): another quest.</summary>
    public const string Awaits = "awaits";

    /// <summary>The chain's step before it (D65 §4): the quest whose close published this one.</summary>
    public const string Follows = "follows";

    /// <summary>The relation read from the other end, or null where none is kept both ways.</summary>
    public static string? Inverse(string relation) => relation switch
    {
        ProducedBy => Produced,
        Produced => ProducedBy,
        _ => null,
    };
}

/// <summary>The canonical records the adapters read, and each one's one writer (§D2's table).</summary>
public static class WorkRecord
{
    /// <summary>The service's quest store, written through its quest exchange.</summary>
    public const string Quests = "quests";

    /// <summary>The service's ask store.</summary>
    public const string Asks = "asks";

    public const string Driver = "driver";

    public const string Service = "service";
}

/// <summary>Who answers for a unit (§D2's <c>owner</c>): the repository and workspace, and the record that names it with that record's writer.</summary>
/// <param name="Repository">The repository whose work it is; null for an ask, which is a workspace's.</param>
/// <param name="Record">
/// The canonical record that names the unit (<see cref="SessionBranches.FileName"/>, <see cref="LandedBranches.FileName"/>,
/// <see cref="AutoLandings.FileName"/>, <see cref="WorkRecord.Quests"/>, <see cref="WorkRecord.Asks"/>); null where no record names
/// it, an unknown owner (canary 27), which blocks and is never guessed.
/// </param>
/// <param name="Writer">That record's one writer: <see cref="WorkRecord.Driver"/> or <see cref="WorkRecord.Service"/>.</param>
public sealed record WorkOwner(string? Repository, string Workspace, string? Record, string Writer);

/// <summary>
/// What the work is on (§D2's <c>subject</c>), what a later node's acceptance binds (§E2): the artifact, and the revision its record
/// last bound. An adapter reads records, never git, so a revision no record holds is null and the live one is git's to say.
/// </summary>
/// <param name="Artifact">A branch's name; a quest's or an ask's <c>#id</c>.</param>
/// <param name="Revision">A branch's tip the kept end fact judged; when a quest last moved; none for an ask.</param>
public sealed record WorkSubject(string? Artifact, string? Revision);

/// <summary>One unit naming another by its id (<see cref="WorkRelation"/>): a link, never a copy of the other's entries.</summary>
public sealed record WorkLink(string Relation, string Unit);

/// <summary>
/// A unit of work (FABRIC-UNIT1a; the fabric events design §D2): responsibility toward a result, apart from whatever executes it, read
/// the same way whatever its kind so a later node treats them alike (§E2). A projection, never a store: each kind's adapter reads it
/// from its canonical record, and nothing here is written anywhere.
/// </summary>
/// <param name="Id">Stable: a branch unit's id as its tree's opening wrote it (or the name its records give it), <c>quest:&lt;id&gt;</c>, <c>ask:&lt;id&gt;</c>.</param>
/// <param name="Kind">One of <see cref="WorkUnitKind"/>.</param>
/// <param name="End">Where its end stands (<see cref="WorkEndState"/>), on what evidence and since when.</param>
/// <param name="Entries">What its work made and keeps (§D3's inventory): each with its role, the evidence pointers among them.</param>
public sealed record WorkUnit(string Id, string Kind, WorkOwner Owner, WorkSubject Subject, MilestoneEnd End, IReadOnlyList<InventoryEntry> Entries)
{
    /// <summary>The sessions that executed it, oldest first: executors, never owners (§D2), so a hand-over keeps the unit.</summary>
    public IReadOnlyList<string> Executors { get; init; } = [];

    /// <summary>The units it names, by id: what produced it, what it produced, what it waits on, the step it follows.</summary>
    public IReadOnlyList<WorkLink> Links { get; init; } = [];

    /// <summary>Whether a record names its owner; an unknown one blocks its cleanup and it never retires (canary 27).</summary>
    public bool OwnerKnown => Owner.Record is not null;

    /// <summary>What stays (§D3's <c>result</c> role).</summary>
    public IReadOnlyList<InventoryEntry> Results => [.. Entries.Where(entry => entry.Role == InventoryRole.Result)];

    /// <summary>The evidence pointers (§D3's <c>evidence</c> role): each names where the evidence is kept, never a copy of it.</summary>
    public IReadOnlyList<InventoryEntry> Evidence => [.. Entries.Where(entry => entry.Role == InventoryRole.Evidence)];

    /// <summary>
    /// <see cref="WorkUnitState.Retired"/> once its end is over, its owner known, and every entry terminal: kept, retained or removed
    /// (§D3). Anything pending or blocked keeps it open, and an open unit is never pruned.
    /// </summary>
    public string State =>
        WorkEndState.Over(End.State) && OwnerKnown
        && Entries.All(entry => entry.Disposition is InventoryDisposition.Kept or InventoryDisposition.Retained or InventoryDisposition.Removed)
            ? WorkUnitState.Retired
            : WorkUnitState.Open;
}
