namespace Daoris.Driver;

/// <summary>
/// The answers-alone bound (COWORK1e, D188 point 4; the message-urgency design §2.3, trap 11): a line of work goes on by solicited
/// answers alone, a helper's answer or a second opinion's findings with no word of the person's or Daoris's beside them, at most
/// <see cref="Bound"/> times in a row, and the next such answer waits for the person (§8.3). Pure: what the line's events say to the
/// count, oldest first, as its parts keep them; the count itself is kept in the line's admission (COMMS1g, <c>ContinuationAdmissions</c>),
/// so a trimmed or lost event resets nothing.
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
    public const int Bound = SupervisorRuleBook.AnswersAloneBound;

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

    /// <summary>
    /// Whether the line is at the bound already (SUPERVISOR1b, the supervisor design §4.3): one more go-on by answers alone, the
    /// supervisor's word alone among them, would be held. The supervisor's route refuses <c>supervisor-bound</c> here, at its keeping
    /// and again at its release, so a decision is never kept only to be held behind the bound.
    /// </summary>
    public static bool AtBound(IEnumerable<SessionEvent> line) => Count(line) >= Bound;

    /// <summary>
    /// The go-ons by answers alone at the line's end, in a row, back to the last word of the person's or Daoris's: the events read whole,
    /// which a line with no admission yet is seeded from.
    /// </summary>
    public static int Count(IEnumerable<SessionEvent> line) =>
        Read(AloneScan.Start, line.Select(e => (Part: "", Event: e))).Steps
            .Aggregate(0, (count, step) => step.Kind switch { AloneStep.GoOn => count + 1, AloneStep.Reset => 0, _ => count });

    /// <summary>
    /// What the line's events say to its count, read on from where a reading stopped (COMMS1g): each go-on by answers alone at the event
    /// its agent acted on, each word of the person's or Daoris's at that word, and each prompt whose answers went beside such a word. A
    /// turn's prompt runs from a turn's end to the agent's first act; a reset in it takes its answers with it, and one taken while the
    /// agent acts resets at once. A prompt not yet acted on is no go-on: it is carried in the scan to the next reading.
    /// </summary>
    public static (AloneScan Scan, IReadOnlyList<AloneStep> Steps) Read(AloneScan from, IEnumerable<(string Part, SessionEvent Event)> line)
    {
        var steps = new List<AloneStep>();
        var prompting = from.Prompting;
        var said = from.Said;
        var answers = from.Answers;
        var answered = new List<string>(from.Answered);

        foreach (var (part, e) in line)
        {
            if (e.Kind == SessionEventKind.Turn)
            {
                Close(part, e, acted: true);
                prompting = true;
                continue;
            }

            if (Acting(e.Kind))
            {
                if (prompting) Close(part, e, acted: true);
                prompting = false;
                continue;
            }

            if (Resets(e))
            {
                steps.Add(new AloneStep(AloneStep.Reset, part, e.Seq, e.At, []));
                said |= prompting;
                continue;
            }

            if (prompting && Answers(e))
            {
                answers = true;
                if (e.Id is { Length: > 0 } id) answered.Add(id);
            }
        }

        return (new AloneScan(prompting, said, answers, [.. answered]), steps);

        void Close(string part, SessionEvent e, bool acted)
        {
            if (said && answers) steps.Add(new AloneStep(AloneStep.Beside, part, e.Seq, e.At, [.. answered]));
            else if (!said && answers && acted) steps.Add(new AloneStep(AloneStep.GoOn, part, e.Seq, e.At, [.. answered]));
            said = false;
            answers = false;
            answered.Clear();
        }
    }

    /// <summary>The agent acting on its prompt: what ends a turn's prompt.</summary>
    private static bool Acting(string kind) =>
        kind is SessionEventKind.Message or SessionEventKind.Thought or SessionEventKind.Tool or SessionEventKind.Plan;

    /// <summary>
    /// A word of the person's (a word with no origin, from a record kept before it was written, is theirs) or of Daoris's, taken; or the
    /// person's press that let an answer go on (<see cref="HeldNoteCodes.AnswersPassed"/>), or their own ask of a helper for the line
    /// (<see cref="PeerNoteCodes.HelperPersonAsked"/>, COWORKUX1d). Never the watcher's word to a quiet session
    /// (<see cref="SessionEvent.Watcher"/>): on a door where it is a prompt of its own it would otherwise give a line going on by answers
    /// alone two more answers after every quiet.
    /// </summary>
    private static bool Resets(SessionEvent e) =>
        e.Kind == SessionEventKind.User && e.Reaches is null && e.From != SupervisorHanded.From && e.Watcher is not true
            && (e.Origin is null or "person" || e.From == DaorisWords.From)
        || e.Kind == SessionEventKind.Note
            && e.Parts?.Any(part => part.Code == HeldNoteCodes.AnswersPassed.Code || part.Code == PeerNoteCodes.HelperPersonAsked.Code) == true;

    /// <summary>
    /// A solicited answer, taken: a helper's answer by its mark, the turn a second opinion's findings were handed in, or the supervisor's
    /// answer by its event's <c>from</c> (SUPERVISOR1a-i). The log is a home file (D156 point 7), so a forged supervisor's event only adds
    /// to the count, which holds the line sooner: the conservative side, read across a restart as every event is.
    /// </summary>
    private static bool Answers(SessionEvent e) =>
        e.Kind == SessionEventKind.User && e.Reaches is null
        && (e.Helper is not null || e.Opinion is not null || e.From == SupervisorHanded.From);

    /// <summary>Who a solicited answer is from, by the same marks (CONTEXTDRIVE1i): a helper, a second opinion or the supervisor; null for any other word.</summary>
    public static string? AnsweredBy(SessionEvent e) =>
        !Answers(e) ? null : e.Helper is not null ? GoOnBy.Helper : e.Opinion is not null ? GoOnBy.Opinion : GoOnBy.Supervisor;
}

/// <summary>Where a reading of a line's events stopped (COMMS1g): whether a turn's prompt is open, whether a reset was taken in it, and its answers.</summary>
/// <param name="Prompting">The events read last are a turn's prompt, its agent not yet acting.</param>
/// <param name="Said">A word of the person's or Daoris's was taken in that prompt.</param>
/// <param name="Answers">A solicited answer was taken in it.</param>
/// <param name="Answered">The ids of the answers it took.</param>
public sealed record AloneScan(bool Prompting, bool Said, bool Answers, IReadOnlyList<string> Answered)
{
    /// <summary>A line's first event is a prompt's.</summary>
    public static AloneScan Start { get; } = new(true, false, false, []);

    /// <summary>Equal by value, its answers in order.</summary>
    public bool Same(AloneScan other) =>
        Prompting == other.Prompting && Said == other.Said && Answers == other.Answers && Answered.SequenceEqual(other.Answered, StringComparer.Ordinal);
}

/// <summary>One thing a line's events said to its count (<see cref="AnswersAlone.Read"/>), at the event that said it.</summary>
/// <param name="Kind"><see cref="GoOn"/>, <see cref="Reset"/> or <see cref="Beside"/>.</param>
/// <param name="Part">The session whose log holds the event.</param>
/// <param name="Words">The ids of the answers the prompt took; none for a reset.</param>
public sealed record AloneStep(string Kind, string Part, long Seq, DateTimeOffset At, IReadOnlyList<string> Words)
{
    /// <summary>A turn's prompt took only answers, and its agent acted on it.</summary>
    public const string GoOn = "go-on";

    /// <summary>A word of the person's or Daoris's, or the person's press that let an answer go on.</summary>
    public const string Reset = "reset";

    /// <summary>A prompt's answers went on beside a resetting word: no go-on by answers alone.</summary>
    public const string Beside = "beside";
}

/// <summary>
/// The notes the answers-alone bound keeps in a session's conversation (COWORK1e), each by its code: the hold, the person's press that
/// lets the answer go on, which the count reads as their word, and their *Not now*. Each names the words it speaks of by their ids.
/// </summary>
internal static class AnswersAloneNotes
{
    /// <summary>The answers wait for the person: going on with them would be the third time in a row with nobody between.</summary>
    public static SessionEvent Held(string agent, IReadOnlyList<string> words) => Note(Noted.Of(
        HeldNoteCodes.AnswersHeld,
        $"{agent}'s answer waits for you: it would take this work on a third time with nobody between.",
        ("agent", agent)), words);

    /// <summary>The person let the answer go on (<em>Go on with this answer</em>): their word on the line.</summary>
    public static SessionEvent Passed(IReadOnlyList<string> words) =>
        Note(Noted.Of(HeldNoteCodes.AnswersPassed, "You let this answer go on."), words);

    /// <summary>The person kept the answer (<em>Not now</em>): it goes on only beside words of theirs.</summary>
    public static SessionEvent Kept(IReadOnlyList<string> words) =>
        Note(Noted.Of(HeldNoteCodes.AnswersKept, "You kept this answer: it goes on only with your words."), words);

    private static SessionEvent Note(Noted noted, IReadOnlyList<string> words) => new()
    {
        Kind = SessionEventKind.Note,
        Text = noted.Note,
        Parts = noted.Parts,
        Words = words.Count > 0 ? words : null,
    };
}
