namespace Daoris.Driver;

/// <summary>
/// The kinds of the work cycle an outside session hears that the records say (EVENTHOOK1d, D244; the event channel design §3), named as
/// the event hook design §4.2 names them, <c>&lt;subject&gt;.&lt;what&gt;</c>: each says what moved, never what it is now.
/// </summary>
public static class WorkEventKinds
{
    /// <summary>The replay began a quest here: a publish, made here or integrated by sync.</summary>
    public const string QuestPublished = "quest.published";

    /// <summary>A quest's status moved into <c>Done</c> or <c>Declined</c>.</summary>
    public const string QuestClosed = "quest.closed";

    /// <summary>A quest told closed is open again: a rebase found this machine's close lost.</summary>
    public const string QuestCloseLost = "quest.close-lost";

    /// <summary>Read the truth again: what moved before this teller's place, or between a lost cursor and the journal's floor, is not told.</summary>
    public const string Missed = "events.missed";
}

/// <summary>
/// One event a source of the lease's process tells (EVENTHOOK1d, D244): its kind, when it moved, and the ids and closed codes its frame
/// carries, never a name, a word or a path. The shapes are checked where the frame is built (the event hook design §4.3).
/// </summary>
/// <param name="At">When the record moved; for <see cref="WorkEventKinds.Missed"/>, when the gap was found.</param>
public sealed record WorkEvent(string Kind, DateTimeOffset At)
{
    /// <summary>The quest's id.</summary>
    public string? Quest { get; init; }

    /// <summary>The status a quest moved into, as a closed lower-case code (<c>done</c>, <c>declined</c>, <c>open</c>, <c>taken</c>).</summary>
    public string? State { get; init; }
}
