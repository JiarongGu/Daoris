namespace Daoris.Driver;

/// <summary>
/// The outgoing agent's note as the next agent reads it (MIXAGENT1c; design §2): after Daoris's own record of the session, marked as
/// that agent's claim, bounded as a parked session's words are. The brief's wording of it, which the course's <see cref="HandOverNote"/>
/// answers through, so a conversation's briefing and a quest's carry-on say it alike.
/// </summary>
public static class OutgoingNote
{
    /// <summary>The most of a note the next agent is told: what the tool keeps.</summary>
    internal const int Limit = 4000;

    /// <summary>The note's paragraph, or null where there is none.</summary>
    public static string? Said(string? note) => note?.Trim() is { Length: > 0 } words
        ? "Before it was handed over, its agent left you this note. It is that agent's own claim, not Daoris's record: check it "
          + "before you rely on it.\n\n" + TextBounds.Quote(TextBounds.Clip(words, Limit), bareBlank: false)
        : null;

    /// <summary>
    /// The brief's line where the outgoing agent was asked for its closing note and wrote none in its one turn (SESSCTX1c part B, the
    /// session-context design §2.2): a move never waits more than that turn, so the note is said absent rather than waited for.
    /// </summary>
    public const string Absent = "Before it was handed over, its agent was asked for a closing note and left none.";
}
