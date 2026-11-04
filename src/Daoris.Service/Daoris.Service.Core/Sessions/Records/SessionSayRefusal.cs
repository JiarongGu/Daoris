namespace Daoris.Knowledge;

/// <summary>Why the person's words were not kept for a session to go on with (MSG1a, D137 §5.3) — or <see cref="None"/>.</summary>
public enum SessionSayRefusal
{
    None,

    /// <summary>No words: nothing was said. Or, taking words, none were named.</summary>
    Empty,

    /// <summary>No session under that id.</summary>
    NotFound,

    /// <summary>A teammate's record (D47 §6): its process and its conversation are on their machine and account.</summary>
    NotOurs,

    /// <summary>An intake, which is answered through its ask (INT4h).</summary>
    Intake,

    /// <summary>It stood down: its quest is someone else's, so it has nothing to go on with.</summary>
    StoodDown,

    /// <summary>It runs: words to a running session reach it through the driver that runs it (D136), not its record.</summary>
    Running,

    /// <summary>
    /// It read another session's work for a second opinion (XAGENT1c, D155 point 7): a pass is one turn, takes no words, and
    /// is never resumed. The person asks again, with their words, instead.
    /// </summary>
    Opinion,

    /// <summary>
    /// It is a helper's record (MIXAGENT2a-i, D163 point 2): a helper takes one turn, answers a question and is never resumed.
    /// </summary>
    Helper,

    /// <summary>
    /// Taking words back (SAIDBACK1a, D137 §2.4): the word no longer waits on the record, or the record runs again, so its
    /// run may already have read it. An answer, never an error, as a live queue's taken word is (QUEUEEDIT1a, D76).
    /// </summary>
    Taken,

    /// <summary>
    /// The words name an author this build does not know (COWORK1b, <see cref="SaidFrom"/>): kept as the person's, they would be
    /// read as theirs, so nothing is kept.
    /// </summary>
    From,

    /// <summary>
    /// The words name a level this build does not know (COWORK1c, <see cref="SaidLevel"/>): kept, they would reach the session at a
    /// point nobody asked for, so nothing is kept.
    /// </summary>
    Level,

    /// <summary>
    /// A supervisor's say named words of its own (SUPERVISOR1a-i, the supervisor design §6.1): its text is the service's, composed from
    /// the choice alone (<see cref="SupervisorWord.Handed"/>), so a caller's text is refused and nothing kept.
    /// </summary>
    SupervisorText,

    /// <summary>
    /// A say under a key a word already waits with, carrying another author, decision, question, choice or text (SUPERVISOR1a-i,
    /// §3.4): a retry is the same say, so a different one is refused, never answered with the word kept.
    /// </summary>
    KeyMismatch,

    /// <summary>A word claiming this decision already waits under another key (SUPERVISOR1a-i, §3.4): it was never the release's.</summary>
    DecisionTaken,

    /// <summary>The supervisor's word is kept only on a park (SUPERVISOR1a-i, §3.1 <c>not-parked</c>).</summary>
    NotParked,

    /// <summary>The supervisor's record holds no quest, or its quest is on no ask whose go-aheads it could read (§3.1 <c>no-quest</c>).</summary>
    NoQuest,

    /// <summary>The question on file is not the one the decision answers (§3.1 <c>question-changed</c>).</summary>
    QuestionChanged,

    /// <summary>The choice is not one of the question's labels exactly (§3.1 <c>not-offered-choice</c>).</summary>
    NotOfferedChoice,

    /// <summary>
    /// The question on file carries the agent's <c>owner</c> mark (SUPERVISOR1b, §3.1 <c>owner-question</c>, §4.1): checked again at the
    /// release, so a question marked the owner's after the decision was kept is never answered by the supervisor.
    /// </summary>
    OwnerQuestion,

    /// <summary>A go-ahead on the session's ask is open: go-aheads are the owner's alone (D135 point 2; §3.1 <c>go-ahead-open</c>).</summary>
    GoAheadOpen,

    /// <summary>The ask's go-aheads cannot be read (§3.1 <c>go-ahead-unread</c>).</summary>
    GoAheadUnread,

    /// <summary>A word of the owner's waits on the record (§3.1 <c>owner-answered</c>): their words win.</summary>
    OwnerAnswered,

    /// <summary>This host keeps no home, so no question file to check: a supervisor's word is a desktop's alone (§3.4 <c>no-home</c>).</summary>
    NoHome,

    /// <summary>
    /// The record's line went on past it (FOUNDATION1 row d-ii, D212; the foundation design §3.4 rule 2): words follow the line's head, which
    /// <see cref="SessionSayOutcome.Head"/> names, so nothing is kept on a record the line has left, where no go-on ever reads them. Judged
    /// inside the say door's step, after the settlement and before every other refusal, so no part opens between the judgement and the keep.
    /// </summary>
    Continued,

    /// <summary>
    /// The record's line cannot be walked to its head or read whole (row d-ii): it loops back on a record it already passed, or runs past
    /// <see cref="Lines.Bound"/> records, so where the words would go, or whether the key's word was already taken, is not known and nothing is
    /// kept. Never a guess at where the walk stopped.
    /// </summary>
    Unplaced,

    /// <summary>
    /// Moving a word (LINESTOGETHER1b): its part runs, so the word waits for that run in the driver's queue, where no step of the record's
    /// reaches, and nothing moved.
    /// </summary>
    NotHeld,

    /// <summary>Moving a word given with files (LINESTOGETHER1b): the files are the part's, so the word is not moved without them.</summary>
    Files,

    /// <summary>Moving a word that already waits on its line's head (LINESTOGETHER1b): there is nowhere further for it to go.</summary>
    AtHead,

    /// <summary>
    /// Moving a word that is not the person's (LINESTOGETHER1b): Daoris's, the supervisor's or another agent's words are tied to what they
    /// were said for, and only the person's own words move.
    /// </summary>
    NotYours,
}
