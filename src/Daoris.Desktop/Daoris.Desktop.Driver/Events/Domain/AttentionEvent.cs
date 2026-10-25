namespace Daoris.Driver;

/// <summary>Why a person is being interrupted.</summary>
/// <remarks>
/// Read by name where it is delivered, so a new kind has two counterparts: the shell's <c>SessionNotifier</c> (its balloon's
/// line where the event has no detail, and its icon) and the page's <c>ShellSignals</c> (its toast's tone).
/// </remarks>
public enum AttentionKind
{
    /// <summary>A session stopped and can only be cleared by the person (D46's `awaiting-person`).</summary>
    Parked,

    /// <summary>A session ended, and the person did not ask for that.</summary>
    Ended,

    /// <summary>
    /// Starts wait because every account they may use is cooling (TOOL4d, D125 §4). Nothing about the work is wrong, and
    /// it starts by itself at the reset; it is said because the person may sign in to another account, or end the
    /// cool-off early, sooner. <see cref="AttentionEvent.Session"/> is empty: no session is concerned.
    /// </summary>
    Waiting,

    /// <summary>
    /// A quest parked on its failed sessions here (SESSUX1i, D126 §4.7): only the person's *Try again* starts it again.
    /// <see cref="AttentionEvent.Session"/> is its last session, which a notification opens, and
    /// <see cref="AttentionEvent.Quest"/> the quest.
    /// </summary>
    QuestParked,

    /// <summary>
    /// A conversation answered the person (NOTIFY1a): a turn their own words started has ended, and nothing more of theirs
    /// waits. <see cref="AttentionEvent.Session"/> is the conversation, which a notification opens.
    /// </summary>
    Answered,
}

/// <param name="State">The state it ended in, for <see cref="AttentionKind.Ended"/>; null for a park.</param>
/// <param name="Note">
/// What the session said about it — the analysis on a park, the conclusion's sentence on an end.
/// Absent is a state, not a gap: a session may park without explaining itself, and a surface that
/// required a note would have nothing to show for the one that did.
/// </param>
public sealed record AttentionEvent(
    AttentionKind Kind, string Session, string Repository, string? State = null, string? Note = null)
{
    /// <summary>For <see cref="AttentionKind.QuestParked"/>: the quest that parked. Null for every other kind.</summary>
    public string? Quest { get; init; }

    /// <summary>For <see cref="AttentionKind.QuestParked"/>: how many sessions failed, as the planner counted to park it; null where unsaid.</summary>
    public int? Strikes { get; init; }

    /// <summary>
    /// For <see cref="AttentionKind.Ended"/>: the workspace the session was filed under at the last look that saw it active, which its
    /// notice keeps (NOTIFY2f, the notification design §2.5); null where no look saw it, and the notice is every window's.
    /// </summary>
    public string? Workspace { get; init; }

    /// <summary>
    /// The one line this is worth saying — a toast's title, and a headless console's line.
    /// </summary>
    /// <remarks>
    /// <b>Composed here so both doors say the same thing.</b> It is the driver's own sentence, which
    /// this platform has always rendered verbatim rather than translated (D24, and the reason
    /// `errors.DRIVER_REFUSED` is `{{message}}`): re-authoring these in two languages would mean two
    /// that drift, and a native toast has no catalogue to read from anyway.
    /// </remarks>
    public string Headline => Kind switch
    {
        AttentionKind.Parked => $"{Repository} — a session needs you",
        AttentionKind.Waiting => $"{Repository} — waits for an account",
        AttentionKind.QuestParked => $"{Repository} — `#{Quest}` parked after " + Strikes switch
        {
            null => "its failed sessions",
            1 => "1 failed session",
            var count => $"{count} failed sessions",
        },
        // Ask Daoris's room is no repository the person knows by name (NOTIFY1a): its answer is said by the room's.
        AttentionKind.Answered when Repository == HelpRoom.Repository => "Ask Daoris answered you",
        AttentionKind.Answered => $"{Repository} — a conversation answered you",
        _ => $"{Repository} — a session {State}",
    };

    /// <summary>The note's lines by code, for a park: its notice is the agent's part alone (NOTICEPLAIN1, D184). Null where unsaid.</summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    /// <summary>
    /// What it said about that, where it said anything. Null is a state, not a gap. A park's is the session's own question,
    /// without Daoris's lead, so a short balloon cannot cut the question off (NOTICEPLAIN1); a park with no agent part, and
    /// every other kind, says its note whole.
    /// </summary>
    public string? Detail
    {
        get
        {
            if (Kind == AttentionKind.Parked
                && string.Concat(NoteParts?.Where(part => part.By == NoteBy.Agent).Select(part => part.Words) ?? []).Trim() is { Length: > 0 } asked)
            {
                return asked;
            }

            return string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
        }
    }

    /// <summary>Both halves on one line, for a door that has only lines.</summary>
    public string Line => Detail is null ? Headline : $"{Headline}: {Detail}";
}
