namespace Daoris.Driver;

/// <summary>
/// The answers-alone bound (COWORK1e, D188 point 4; the message-urgency design §2.3, trap 11): a line of work goes on by solicited
/// answers alone, a helper's answer or a second opinion's findings with no word of the person's or Daoris's beside them, at most
/// <see cref="Bound"/> times in a row, and the next such answer waits for the person (§8.3). Pure: counted from the line's events,
/// oldest first, as its parts keep them, so it survives a restart and needs no counter of its own.
/// </summary>
/// <remarks>
/// <para><b>A go-on is a turn whose prompt took only solicited answers.</b> A turn's prompt is the words a record took before its agent
/// acted on them, between one turn's end and the next agent's step; an answer taken at a step of a running turn is no go-on and counts
/// nothing, since no run went on past its own turn (§2.3). A prompt the agent never acted on (a resume it would not take) is no turn.</para>
///
/// <para><b>Only the person's or Daoris's word resets it</b>, wherever on the line it was taken, and the person's *Go on with this
/// answer* is their word (the steward's choice 1). A hand-over's brief, a retry's instruction and a claim the person passed on neither
/// count nor reset, so the count runs across the line's parts. A word shown while it waited is not yet taken, and is read where it is.</para>
/// </remarks>
internal static class AnswersAlone
{
    /// <summary>How many go-ons by answers alone a line takes in a row: a first value the owner may move (§2.3).</summary>
    public const int Bound = 2;

    /// <summary>
    /// Whether a word is an answer the line goes on by with nobody between: a helper's, a second opinion's findings, or the supervisor's
    /// (SUPERVISOR1a-i, the supervisor design §4.3: one count, D188's, so a mixed prompt evades neither). The look hands only an
    /// attributed supervisor's word, so one reaching here is one.
    /// </summary>
    public static bool Solicited(SaidWordView word) => word.Helper is not null || word.By is not null || word.Supervisors;

    /// <summary>
    /// Whether going on with <paramref name="words"/> now would take the line on by answers alone once past the bound: every word an
    /// answer, and the line already gone on so <see cref="Bound"/> times in a row.
    /// </summary>
    public static bool Holds(IReadOnlyList<SaidWordView> words, IEnumerable<SessionEvent> line) =>
        words.Count > 0 && words.All(Solicited) && Count(line) >= Bound;

    /// <summary>The go-ons by answers alone at the line's end, in a row, back to the last word of the person's or Daoris's.</summary>
    public static int Count(IEnumerable<SessionEvent> line)
    {
        var count = 0;
        // Whether the events read now are a turn's prompt, before its agent acted; and what that prompt took so far.
        var prompting = true;
        var said = false;
        var answered = false;

        foreach (var e in line)
        {
            if (e.Kind == SessionEventKind.Turn)
            {
                Close(acted: true);
                prompting = true;
                continue;
            }

            if (Acting(e.Kind))
            {
                if (prompting) Close(acted: true);
                prompting = false;
                continue;
            }

            if (Resets(e))
            {
                if (prompting) said = true;
                else count = 0;
                continue;
            }

            if (prompting && Answers(e)) answered = true;
        }

        // A prompt nothing acted on yet: the person's word in it still resets, an answer in it is no go-on.
        Close(acted: false);
        return count;

        void Close(bool acted)
        {
            if (said) count = 0;
            else if (answered && acted) count++;
            said = false;
            answered = false;
        }
    }

    /// <summary>The agent acting on its prompt: what ends a turn's prompt.</summary>
    private static bool Acting(string kind) =>
        kind is SessionEventKind.Message or SessionEventKind.Thought or SessionEventKind.Tool or SessionEventKind.Plan;

    /// <summary>
    /// A word of the person's (a word with no origin, from a record kept before it was written, is theirs) or of Daoris's, taken; or the
    /// person's press that let an answer go on (<see cref="NoteCodes.AnswersPassed"/>).
    /// </summary>
    private static bool Resets(SessionEvent e) =>
        e.Kind == SessionEventKind.User && e.Reaches is null && e.From != SupervisorHanded.From
            && (e.Origin is null or "person" || e.From == DaorisWords.From)
        || e.Kind == SessionEventKind.Note && e.Parts?.Any(part => part.Code == NoteCodes.AnswersPassed.Code) == true;

    /// <summary>
    /// A solicited answer, taken: a helper's answer by its mark, the turn a second opinion's findings were handed in, or the supervisor's
    /// answer by its event's <c>from</c> (SUPERVISOR1a-i). The log is a home file (D156 point 7), so a forged supervisor's event only adds
    /// to the count, which holds the line sooner: the conservative side, read across a restart as every event is.
    /// </summary>
    private static bool Answers(SessionEvent e) =>
        e.Kind == SessionEventKind.User && e.Reaches is null
        && (e.Helper is not null || e.Opinion is not null || e.From == SupervisorHanded.From);
}

/// <summary>
/// The notes the answers-alone bound keeps in a session's conversation (COWORK1e), each by its code: the hold, the person's press that
/// lets the answer go on, which the count reads as their word, and their *Not now*. Each names the words it speaks of by their ids.
/// </summary>
internal static class AnswersAloneNotes
{
    /// <summary>The answers wait for the person: going on with them would be the third time in a row with nobody between.</summary>
    public static SessionEvent Held(string agent, IReadOnlyList<string> words) => Note(Noted.Of(
        NoteCodes.AnswersHeld,
        $"{agent}'s answer waits for you: it would take this work on a third time with nobody between.",
        ("agent", agent)), words);

    /// <summary>The person let the answer go on (<em>Go on with this answer</em>): their word on the line.</summary>
    public static SessionEvent Passed(IReadOnlyList<string> words) =>
        Note(Noted.Of(NoteCodes.AnswersPassed, "You let this answer go on."), words);

    /// <summary>The person kept the answer (<em>Not now</em>): it goes on only beside words of theirs.</summary>
    public static SessionEvent Kept(IReadOnlyList<string> words) =>
        Note(Noted.Of(NoteCodes.AnswersKept, "You kept this answer: it goes on only with your words."), words);

    private static SessionEvent Note(Noted noted, IReadOnlyList<string> words) => new()
    {
        Kind = SessionEventKind.Note,
        Text = noted.Note,
        Parts = noted.Parts,
        Words = words.Count > 0 ? words : null,
    };
}
