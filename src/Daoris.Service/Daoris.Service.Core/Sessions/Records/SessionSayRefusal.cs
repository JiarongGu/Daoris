namespace Daoris.Knowledge;

/// <summary>Why the person's words are not kept for a session to go on with, or <see cref="None"/>.</summary>
public enum SessionSayRefusal
{
    None,

    /// <summary>No words: nothing said. Or, taking words, none were named.</summary>
    Empty,

    /// <summary>No session under that id.</summary>
    NotFound,

    /// <summary>A teammate's record: its process and its conversation are on their machine and account.</summary>
    NotOurs,

    /// <summary>An intake, which is answered through its ask.</summary>
    Intake,

    /// <summary>It stood down: its quest is someone else's, so it has nothing to go on with.</summary>
    StoodDown,

    /// <summary>It runs: words to a running session reach it through the driver that runs it, not its record.</summary>
    Running,

    /// <summary>
    /// It read another session's work for a second opinion: a pass is one turn, takes no words, and
    /// is never resumed. The person asks again, with their words, instead.
    /// </summary>
    Opinion,

    /// <summary>
    /// It is a helper's record: a helper takes one turn, answers a question and is never resumed.
    /// </summary>
    Helper,

    /// <summary>
    /// Taking words back: the word no longer waits on the record, or the record runs again, so its
    /// run may already have read it. An answer, never an error, as a live queue's taken word is.
    /// </summary>
    Taken,

    /// <summary>
    /// The words name an author this build does not know (<see cref="SaidFrom"/>): kept as the person's, they would be
    /// read as theirs, so nothing is kept.
    /// </summary>
    From,

    /// <summary>
    /// The words name a level this build does not know (<see cref="SaidLevel"/>): kept, they would reach the session at a
    /// point nobody asked for, so nothing is kept.
    /// </summary>
    Level,

    /// <summary>
    /// A supervisor's say that names words of its own: its text is the service's, composed from
    /// the choice alone (<see cref="SupervisorWord.Handed"/>), so a caller's text is refused and nothing kept.
    /// </summary>
    SupervisorText,

    /// <summary>
    /// A say under a key a word already waits with, carrying another author, decision, question, choice or text:
    /// a retry is the same say, so a different one is refused, never answered with the word kept.
    /// </summary>
    KeyMismatch,

    /// <summary>A word claiming this decision already waits under another key: it is never the release's.</summary>
    DecisionTaken,

    /// <summary>The supervisor's word is kept only on a park (<c>not-parked</c>).</summary>
    NotParked,

    /// <summary>The supervisor's record holds no quest, or its quest is on no ask whose go-aheads it could read (§3.1 <c>no-quest</c>).</summary>
    NoQuest,

    /// <summary>The question on file is not the one the decision answers (§3.1 <c>question-changed</c>).</summary>
    QuestionChanged,

    /// <summary>The choice is not one of the question's labels exactly (§3.1 <c>not-offered-choice</c>).</summary>
    NotOfferedChoice,

    /// <summary>
    /// The question on file carries the agent's <c>owner</c> mark (<c>owner-question</c>): checked again at the
    /// release, so a question marked the owner's after the decision is kept is never answered by the supervisor.
    /// </summary>
    OwnerQuestion,

    /// <summary>A go-ahead on the session's ask is open: go-aheads are the owner's alone (<c>go-ahead-open</c>).</summary>
    GoAheadOpen,

    /// <summary>The ask's go-aheads cannot be read (§3.1 <c>go-ahead-unread</c>).</summary>
    GoAheadUnread,

    /// <summary>A word of the owner's waits on the record (§3.1 <c>owner-answered</c>): their words win.</summary>
    OwnerAnswered,

    /// <summary>This host keeps no home, so no question file to check: a supervisor's word is a desktop's alone (§3.4 <c>no-home</c>).</summary>
    NoHome,

    /// <summary>
    /// The record's line went on past it: words follow the line's head, which
    /// <see cref="SessionSayOutcome.Head"/> names, so nothing is kept on a record the line has left, where no go-on ever reads them. Judged
    /// inside the say door's step, after the settlement and before every other refusal, so no part opens between the judgement and the keep.
    /// </summary>
    Continued,

    /// <summary>
    /// The record's line cannot be walked to its head or read whole: it loops back on a record it already passed, or runs past
    /// <see cref="Lines.Bound"/> records, so where the words would go, or whether the key's word is already taken, is not known and nothing is
    /// kept. Never a guess at where the walk stopped.
    /// </summary>
    Unplaced,

    /// <summary>
    /// Moving a word: its part runs, so the word waits for that run in the driver's queue, where no step of the record's
    /// reaches, and nothing moved.
    /// </summary>
    NotHeld,

    /// <summary>Moving a word given with files: the files are the part's, so the word is not moved without them.</summary>
    Files,

    /// <summary>Moving a word that already waits on its line's head: there is nowhere further for it to go.</summary>
    AtHead,

    /// <summary>
    /// Moving a word that is not the person's: Daoris's, the supervisor's or another agent's words are tied to what they
    /// were said for, and only the person's own words move.
    /// </summary>
    NotYours,
}
