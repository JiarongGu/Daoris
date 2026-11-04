namespace Daoris.Driver;

/// <summary>One part of the line a brief is composed from (SESSCTX1b): its record, the agent it ran on, and its events as kept.</summary>
/// <param name="Session">The record's id.</param>
/// <param name="Agent">The adapter it ran on, as its record names it; null where it names none.</param>
/// <param name="Events">Its record's events, oldest first, as <see cref="SessionEvents"/> keeps them.</param>
public sealed record BriefPart(string Session, string? Agent, IReadOnlyList<SessionEvent> Events)
{
    /// <summary>Why it continued the part before it on the line, one of <see cref="HandOverWhy"/>; null for the line's first.</summary>
    public string? Why { get; init; }

    /// <summary>Where its kept attachments lie (<see cref="ChatFiles.Folder"/>), named beside each file the person attached.</summary>
    public string? Kept { get; init; }
}

/// <summary>The tree as read when a brief is composed (the session-context design §3.1 item 5), since a part's end commit is not recorded.</summary>
/// <param name="Head">The tree's head, its short id and subject; null where the tree could not be read.</param>
/// <param name="Uncommitted">What it holds uncommitted, git's short lines, relative to the tree.</param>
/// <param name="Commits">The commits on it since the line's first part began, newest first, short id and subject.</param>
public sealed record BriefTree(string? Head, IReadOnlyList<string> Uncommitted, IReadOnlyList<string> Commits);

/// <summary>
/// One thing a line waits on, as plain values (COMMS1f): the request's id, the session that owns it, whom it asked, its delivery and
/// outcome as the exchanges' read gives them, and the words of the ask where it has some.
/// </summary>
public sealed record BriefWait(string Id, string Owner, string To, string Delivery, string Outcome, string? Detail = null);

/// <summary>What a brief is composed from: the line a new part continues, oldest part first, and what was read beside it.</summary>
/// <param name="Why">Why the session moves on now, one of <see cref="HandOverWhy"/>.</param>
/// <param name="Parts">The line, one chain back by each part's <c>from</c>: the part moved from last, never a part beside it.</param>
public sealed record BriefLine(string Why, IReadOnlyList<BriefPart> Parts)
{
    /// <summary>The line's counts as Daoris read them (CONTEXTDRIVE1b), each with what Daoris does on it; none read, none said.</summary>
    public IReadOnlyList<CountFact> Counts { get; init; } = [];

    /// <summary>A conversation's line: its request is its first word, which item 3 does not tell again. A driven line's is its quest.</summary>
    public bool Conversation { get; init; }

    /// <summary>The tree the line works in, so a file a call touched is named relative to it; never said itself.</summary>
    public string? Tree { get; init; }

    /// <summary>The tree as read now; null where nothing was read, and the state says the record's alone.</summary>
    public BriefTree? State { get; init; }

    /// <summary>The gates the repository declares, and this machine's heavy-gate mapping for it: what makes a call a check.</summary>
    public IReadOnlyList<HeavyGate> Gates { get; init; } = [];

    /// <summary>
    /// The outgoing agent's own note (§3.1 item 6), as its claim: MIXAGENT1c's <c>handover_propose</c> once it exists, or the closing
    /// note SESSCTX1c asks for (§2.2). Null where none was written, and the brief says nothing of one.
    /// </summary>
    public string? Note { get; init; }

    /// <summary>
    /// Whether the outgoing agent was asked for its closing note (SESSCTX1c part B, §2.2): where it was and wrote none, the note's section
    /// says it is absent (<see cref="OutgoingNote.Absent"/>), since the move never waited more than that one turn.
    /// </summary>
    public bool NoteAsked { get; init; }

    /// <summary>The entry's other parts, beside the line, one line each (§3.1 item 7).</summary>
    public IReadOnlyList<string> Others { get; init; } = [];

    /// <summary>What the line waits on: each request its parts sent that nothing has answered, or whose answer is kept and not yet handed.</summary>
    public IReadOnlyList<BriefWait> Open { get; init; } = [];

    /// <summary>
    /// The memory the line read, newest first, each entry in one line as the service rechecked it when the brief was composed, never as it
    /// was read (KNOWCENTRAL2f). The brief hands it on (<see cref="ComposedBrief.Memory"/>), and the standing block says it (CONTEXTONE1c).
    /// </summary>
    public IReadOnlyList<string> Memory { get; init; } = [];
}
