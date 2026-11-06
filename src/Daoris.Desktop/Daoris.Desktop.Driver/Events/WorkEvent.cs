namespace Daoris.Driver;

/// <summary>
/// The kinds of the work cycle an outside session hears at <c>work/event</c> (EVENTHOOK1b, EVENTHOOK1d, D244; the event hook design §4.2, the
/// event channel design §3), named <c>&lt;subject&gt;.&lt;what&gt;</c>: each says what moved, never what it is now.
/// </summary>
public static class WorkEventKinds
{
    /// <summary>A session newly waits on the person: the attention watch's park.</summary>
    public const string SessionParked = "session.parked";

    /// <summary>A quest parked on its failed sessions here.</summary>
    public const string QuestParked = "quest.parked";

    /// <summary>A session ended in a state the person did not ask for.</summary>
    public const string SessionEnded = "session.ended";

    /// <summary>The install landed a session's work as a branch the line does not hold yet, or moved a chain's branch on.</summary>
    public const string WorkLanded = "work.landed";

    /// <summary>The replay began a quest here: a publish, made here or integrated by sync.</summary>
    public const string QuestPublished = "quest.published";

    /// <summary>A quest's status moved into <c>Done</c> or <c>Declined</c>.</summary>
    public const string QuestClosed = "quest.closed";

    /// <summary>A quest told closed is open again: a rebase found this machine's close lost.</summary>
    public const string QuestCloseLost = "quest.close-lost";

    /// <summary>A press no terminal waited on its line for settled, after its record said how (D244's note on the confirmation's kind).</summary>
    public const string ConfirmationEnded = "confirmation.ended";

    /// <summary>The line holds a session's work: the end keeping wrote its entry ended (D244's note on <c>work.ended</c>'s teller).</summary>
    public const string WorkEnded = "work.ended";

    /// <summary>
    /// Read the truth again: what moved before a teller's place, in a watch's baseline, or in an event whose required field failed its
    /// shape, is not told.
    /// </summary>
    public const string Missed = "events.missed";
}

/// <summary>
/// One event a source of the lease's process tells (EVENTHOOK1b, EVENTHOOK1d): its kind, when it moved, and the ids and closed codes its
/// frame carries, never a name, a word or a path. The shapes are checked where the frame is built (<see cref="HookFrames.Event"/>).
/// </summary>
/// <param name="At">When the record moved; for <see cref="WorkEventKinds.Missed"/>, when the gap was found.</param>
public sealed record WorkEvent(string Kind, DateTimeOffset At)
{
    /// <summary>The session's id.</summary>
    public string? Session { get; init; }

    /// <summary>The quest's id.</summary>
    public string? Quest { get; init; }

    /// <summary>The ask's id, where the watch's repository is <c>ask #&lt;id&gt;</c>.</summary>
    public string? Ask { get; init; }

    /// <summary>
    /// The state moved into, as a closed lower-case code: a quest's (<c>done</c>, <c>declined</c>, <c>open</c>, <c>taken</c>) or a session's
    /// ending (<see cref="SessionLog.Closed"/>).
    /// </summary>
    public string? State { get; init; }

    /// <summary>The adapter an ending names, one of this build's.</summary>
    public string? Adapter { get; init; }

    /// <summary>A quest park's count of failed sessions.</summary>
    public int? Strikes { get; init; }

    /// <summary>A landing's code: <c>landed</c> or <c>advanced</c>.</summary>
    public string? Code { get; init; }

    /// <summary>A press's own id on the pipe, 32 hex, never the terminal's request id.</summary>
    public string? Confirmation { get; init; }

    /// <summary>How a press ended: <c>confirmed</c>, <c>refused</c>, <c>changed</c>, <c>withdrawn</c> or <c>lost</c>.</summary>
    public string? End { get; init; }
}
