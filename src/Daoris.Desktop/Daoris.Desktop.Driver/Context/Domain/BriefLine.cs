namespace Daoris.Driver;

/// <summary>One part of the line a brief is composed from: its record, the agent it ran on, and its events as kept.</summary>
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

/// <summary>The tree as read when a brief is composed, since a part's end commit is not recorded.</summary>
/// <param name="Head">The tree's head, its short id and subject; null where the tree could not be read.</param>
/// <param name="Uncommitted">What it holds uncommitted, git's short lines, relative to the tree.</param>
/// <param name="Commits">The commits on it since the line's first part began, newest first, short id and subject.</param>
public sealed record BriefTree(string? Head, IReadOnlyList<string> Uncommitted, IReadOnlyList<string> Commits);

/// <summary>
/// One thing a line waits on, as plain values: the request's id, the session that owns it, whom it asked, its delivery and
/// outcome as the exchanges' read gives them, and the words of the ask where it has some.
/// </summary>
public sealed record BriefWait(string Id, string Owner, string To, string Delivery, string Outcome, string? Detail = null);

/// <summary>What a brief is composed from: the line a new part continues, oldest part first, and what is read beside it.</summary>
/// <param name="Why">Why the session moves on now, one of <see cref="HandOverWhy"/>.</param>
/// <param name="Parts">The line, one chain back by each part's <c>from</c>: the part moved from last, never a part beside it.</param>
public sealed record BriefLine(string Why, IReadOnlyList<BriefPart> Parts)
{
    /// <summary>The line's counts as Daoris read them, each with what Daoris does on it; none read, none said.</summary>
    public IReadOnlyList<CountFact> Counts { get; init; } = [];

    /// <summary>A conversation's line: its request is its first word, which the words do not tell again. A driven line's is its quest.</summary>
    public bool Conversation { get; init; }

    /// <summary>The tree the line works in, so a file a call touched is named relative to it; never said itself.</summary>
    public string? Tree { get; init; }

    /// <summary>The tree as read now; null where nothing is read, and the state says the record's alone.</summary>
    public BriefTree? State { get; init; }

    /// <summary>The gates the repository declares, and this machine's heavy-gate mapping for it: what makes a call a check.</summary>
    public IReadOnlyList<HeavyGate> Gates { get; init; } = [];

    /// <summary>
    /// The outgoing agent's own note, as its claim: <c>handover_propose</c>'s, or the closing
    /// note the brief asks for. Null where none is written, and the brief says nothing of one.
    /// </summary>
    public string? Note { get; init; }

    /// <summary>
    /// Whether the outgoing agent is asked for its closing note: where it is and wrote none, the note's section
    /// says it is absent (<see cref="OutgoingNote.Absent"/>), since the move never waited more than that one turn.
    /// </summary>
    public bool NoteAsked { get; init; }

    /// <summary>The entry's other parts, beside the line, one line each.</summary>
    public IReadOnlyList<string> Others { get; init; } = [];

    /// <summary>What the line waits on: each request its parts sent that nothing has answered, or whose answer is kept and not yet handed.</summary>
    public IReadOnlyList<BriefWait> Open { get; init; } = [];

    /// <summary>
    /// The memory the line read, newest first, each entry in one line as the service rechecked it when the brief is composed, never as it
    /// is read. The brief hands it on (<see cref="ComposedBrief.Memory"/>), and the standing block says it.
    /// </summary>
    public IReadOnlyList<string> Memory { get; init; } = [];
}
