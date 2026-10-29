namespace Daoris.Driver;

// FOUNDATION1e part 1 (the foundation design §3.1, §10 row 1e): the facts the course decides over, read by nothing. The decision itself,
// LineCourse.Decide, is part 2's; rows h and i adopt it one path per branch. Everything here is a value: the executor reads the world
// (the record, the account, this machine's event files for the answers-alone count) into the facts, so the judgement stays pure and the
// course never reads an event file (the answers-alone count is a fact, as AnswersAlone.Count is read once by the executor).

/// <summary>Who wrote a word waiting on a line, as the answers-alone count and the say door tell them apart (D188, SUPERVISOR1a-i).</summary>
public enum CourseAuthor
{
    /// <summary>The person: their word resets the answers-alone count.</summary>
    Person,

    /// <summary>Daoris's own words (a limit word, a retry's instruction): they reset the count too.</summary>
    Daoris,

    /// <summary>A helper's answer, a solicited answer.</summary>
    Helper,

    /// <summary>A second opinion's findings, a solicited answer.</summary>
    Findings,

    /// <summary>The supervisor's answer, a solicited answer.</summary>
    Supervisor,
}

/// <summary>A word kept on the line, waiting: its id and its author. The text is the executor's.</summary>
/// <param name="Key">
/// The key a word of Daoris's carries (<see cref="DaorisKeys"/>), which tells its resume words (a limit's, a restart's, a retry's) from
/// the words that state a fact (a rebase, a reopen): FOUNDATION1e part 2, for which words go to a new part.
/// </param>
/// <param name="AfterEnd">Said after the record ended: past a transient bound only such a word of the person's goes on (AGENTERRORS1a).</param>
public sealed record CourseWord(string Id, CourseAuthor Author, string? Key = null, bool AfterEnd = false)
{
    /// <summary>Whether it is an answer the line goes on by with nobody between (<see cref="AnswersAlone.Solicited"/>).</summary>
    public bool Solicited => Author is CourseAuthor.Helper or CourseAuthor.Findings or CourseAuthor.Supervisor;

    /// <summary>The person's own word.</summary>
    public bool Persons => Author == CourseAuthor.Person;

    /// <summary>
    /// Whether a new part an own account's cooling opens carries it (LIMITSWITCH3, <see cref="DaorisWords.NewPartCarries"/>): the person's,
    /// and Daoris's own that state a fact; never Daoris's resume words, a helper's answer or another agent's findings.
    /// </summary>
    public bool NewPartCarries => Persons || (Author == CourseAuthor.Daoris && !DaorisKeys.IsResume(Key));
}

/// <summary>Where the line stands when it is asked what next: the boundary of §3.1's inputs.</summary>
public enum CourseBoundary
{
    /// <summary>A turn is running: a word steers or waits, and an automatic move waits for the turn's end.</summary>
    TurnRunning,

    /// <summary>Between turns: the part is idle and waits for its next prompt.</summary>
    BetweenTurns,

    /// <summary>The part's process has exited: the record ended or parked.</summary>
    ProcessExited,

    /// <summary>A press holds the line's reservation (a take, a hand-over, a briefed press) and nothing else takes it.</summary>
    Reserved,

    /// <summary>The application is winding up: no part opens now.</summary>
    WindingUp,
}

/// <summary>What the person pressed on the line, if anything.</summary>
public enum CoursePressKind
{
    /// <summary>No press: the look or a word decides.</summary>
    None,

    /// <summary>A pick: the person chose an agent or an account.</summary>
    Pick,

    /// <summary>*Go on in a new session*.</summary>
    NewSession,

    /// <summary>A briefed press: *Go on with Daoris's brief*.</summary>
    Briefed,

    /// <summary>A hand-over, with its agent and account.</summary>
    HandOver,

    /// <summary>*Start a conversation with these words*.</summary>
    StartFrom,

    /// <summary>A stop.</summary>
    Stop,

    /// <summary>A pause.</summary>
    Pause,

    /// <summary>*Try again*: the release of a record ended past its transient bound, or of the person's stop (AGENTERRORS1a2).</summary>
    TryAgain,
}

/// <summary>The person's press, with the agent and account a hand-over names.</summary>
/// <param name="Kind">Which press.</param>
/// <param name="Agent">The agent a hand-over or a pick names; null otherwise.</param>
/// <param name="Account">The account a hand-over or a pick names; null for the walk's.</param>
/// <param name="Intent">The press's own intent key, which keys a start so a second press opens a second line (§3.1, X-3.2).</param>
/// <param name="Why">
/// A hand-over's why, as its box or a handed quest's store keeps it: the person's press, Daoris's offer at a limit, the agent's ask
/// (FOUNDATION1e part 2). Null reads as the press's own: <see cref="CourseFromWhy.Press"/>.
/// </param>
/// <param name="By">Who asked the hand-over: the person, the agent or Daoris. Null reads as the person.</param>
public sealed record CoursePress(
    CoursePressKind Kind = CoursePressKind.None, string? Agent = null, string? Account = null, string? Intent = null, CourseFromWhy? Why = null,
    CourseFromBy? By = null);

/// <summary>The newest part of the line, as the record keeps it (§3.1's first input).</summary>
/// <param name="State">The record's state (<c>parked</c>, <c>ended</c>, <c>stood-down</c>, <c>working</c>…).</param>
/// <param name="EndCode">The code the part ended by, or null while it runs.</param>
/// <param name="Limited">An account's limit ended the part.</param>
/// <param name="Role">The record's role.</param>
/// <param name="Choice">The asked choice of that role, or null.</param>
/// <param name="Adapter">The agent the part ran on.</param>
/// <param name="Profile">The account it ran as, or null for the tool's own.</param>
/// <param name="Tree">The tree it ran in, or null.</param>
/// <param name="Kept">Whether the conversation the part ran in is kept on this machine.</param>
/// <param name="DoorResumes">Whether the part's door can resume at all.</param>
/// <param name="Conversation">A repository conversation, or Ask Daoris's, rather than a driven quest's session.</param>
/// <param name="Teammate">A teammate's record: never this machine's to resume.</param>
/// <param name="Intake">An intake record, which asks the person and goes on by nothing.</param>
/// <param name="StoodDown">A stood-down record.</param>
/// <param name="SupersededBy">The part a later part of the line supersedes this one by; null while this is the head.</param>
/// <param name="Waiting">The words waiting on the record, with their ids and authors.</param>
/// <param name="Session">The record's own id: Daoris's limit word is keyed by it (FOUNDATION1e part 2, as the facts below).</param>
/// <param name="QuestStatus">A driven record's quest status (<c>Open</c>, <c>Taken</c>, or another word for closed); null for a conversation.</param>
/// <param name="AskDaoris">Ask Daoris's own conversation: it waits for its own account, never switches, and has no new session (D205 §3.5(e)).</param>
/// <param name="NotOffered">The record's agent refused its asked model at the open, so no conversation began (MODELROLE1d2).</param>
/// <param name="Protocol">The protocol door, whose resume of a missing conversation fails closed, so a limited line resumes there first.</param>
/// <param name="AgentGone">The agent the record ran on is no longer one of this machine's.</param>
/// <param name="Judged">
/// The resume judge's reason by its code (<see cref="Continuations.Judge"/>, <see cref="ContinueWhy"/>), before the wire or on it; null where
/// the record resumes its own conversation. The course reads its kinds (never, holds, lost) and nothing of its sentence.
/// </param>
/// <param name="Revision">The record's revision, which the answer's preconditions carry (§3.1, X).</param>
public sealed record CourseHead(
    string State, string? EndCode = null, bool Limited = false, string? Role = null, string? Choice = null, string? Adapter = null,
    string? Profile = null, string? Tree = null, bool Kept = false, bool DoorResumes = false, bool Conversation = false,
    bool Teammate = false, bool Intake = false, bool StoodDown = false, string? SupersededBy = null, IReadOnlyList<CourseWord>? Waiting = null,
    string? Session = null, string? QuestStatus = null, bool AskDaoris = false, bool NotOffered = false, bool Protocol = false,
    bool AgentGone = false, string? Judged = null, long? Revision = null)
{
    /// <summary>The waiting words, never null.</summary>
    public IReadOnlyList<CourseWord> Words => Waiting ?? [];

    /// <summary>Whether every waiting word is a solicited answer: the answers-alone hold's reading of them.</summary>
    public bool OnlyAnswers => Words.Count > 0 && Words.All(word => word.Solicited);

    /// <summary>A driven record whose quest is open or taken: the driver carries its words on by itself.</summary>
    public bool Carried => !Conversation && QuestStatus is "Open" or "Taken";
}

/// <summary>
/// The account facts, read once: <see cref="LineAccount"/>'s answer for the line's own account, so every path decides on the same reading
/// of it (§3.2). Null where the executor has not asked the rule, for a trigger that moves no account.
/// </summary>
/// <remarks>
/// FOUNDATION1e part 2: the answer is the executor's one reading of where the line's next account is, however its path reads it today (the
/// rule over the cooling store, the walk a new part takes, the walk over a role's order); the course reads it once and never walks.
/// </remarks>
/// <param name="Answer">The reading: the own account resumes, another account carries the words, they wait, or nothing carries them.</param>
/// <param name="Picked">The person picked the line's own account, kept on its binding (FOUNDATION1a-ii): it is the one account, never moved.</param>
/// <param name="Earliest">
/// The reset the line waits for where <paramref name="Answer"/> names none: a limited conversation's, the earliest of every cooling account
/// of its agent (<see cref="ResetSet.Wake"/>), where the walk handed back its own account.
/// </param>
/// <param name="Later">The role's later choice on another agent the judge-only walk found ready (LIMITGOON2); null where none is.</param>
/// <param name="Agent">The agent the answer's selection runs on, where the walk over a role's order took another than the line's (WALKAGENT1).</param>
/// <param name="Curve">The walk's first step passed an account over its usage curve: a driven carry-on's move is then the curve's.</param>
public sealed record CourseAccount(
    LineAnswer? Answer = null, bool Picked = false, CoolingEntry? Earliest = null, CourseLater? Later = null, string? Agent = null,
    bool Curve = false);

/// <summary>A later choice of the line's role on another agent, ready now (LIMITGOON2, D205 §3.5(e)).</summary>
/// <param name="Agent">The choice's agent.</param>
/// <param name="Choice">The choice, as the role's order names it.</param>
/// <param name="Account">The account the walk found ready, or null for that agent's walk at the open.</param>
public sealed record CourseLater(string Agent, string? Choice = null, string? Account = null);

/// <summary>
/// The answers-alone hold's facts (COWORK1e, D188 point 4): the count across the line's preceding parts, read from this machine's event
/// files by the executor, which writes the hold's marks; its bound; and whether the person's press released the held answer.
/// </summary>
/// <param name="Count">The go-ons by answers alone at the line's end, in a row.</param>
/// <param name="Bound">How many the line takes in a row (<see cref="AnswersAlone.Bound"/>).</param>
/// <param name="Released">The person's *Go on with this answer* let it go on (<see cref="NoteCodes.AnswersPassed"/>).</param>
/// <param name="Kept">The person kept the answer with *Not now*: it goes on only beside words of theirs (<see cref="NoteCodes.AnswersKept"/>).</param>
public sealed record CourseAlone(int Count, int Bound = AnswersAlone.Bound, bool Released = false, bool Kept = false)
{
    /// <summary>Whether one more go-on by answers alone would be held.</summary>
    public bool AtBound => !Released && Count >= Bound;
}

/// <summary>The failure the line's last part ended on, by D210's reader (the agent-errors design).</summary>
/// <param name="Kind">The four kinds every failure is read into; null where the part did not fail.</param>
/// <param name="PastBound">A transient failure whose retries are spent: the course decides only what follows the bound (D210 point 3).</param>
public sealed record CourseFailure(AgentErrorKind? Kind = null, bool PastBound = false);

/// <summary>The rules the course applies, handed in as the verdicts of the functions that hold them (§3.1's rules).</summary>
/// <param name="HandOverAdmits">The hand-over rule's consent for an agent's ask (<c>HandOverRules</c>), true where the ask is admitted.</param>
/// <param name="SwitchAdmits">The curve's scope switch admits Daoris's own move (<c>CurveMove</c>), true where it does.</param>
/// <param name="CurveAccount">The account <c>SwitchDecision.Decide</c> names, or null where it names none.</param>
/// <param name="LaterChoices">How many later choices of the role the line may take (step 4 of the account rule).</param>
/// <param name="HandOverAsks">
/// Where the hand-over rule does not admit an agent's ask, whether it asks the person (<c>ask</c>) rather than refusing it (<c>off</c>).
/// </param>
/// <param name="Runner">The shell's chat runner is here: a closed quest's session goes on as a conversation in its tree only where it is.</param>
public sealed record CourseRules(
    bool HandOverAdmits = false, bool SwitchAdmits = false, string? CurveAccount = null, int LaterChoices = 0, bool HandOverAsks = false,
    bool Runner = false);

/// <summary>
/// What Daoris's own word on a resume is keyed by (FOUNDATION1e part 2): a conversation's wake keys its limit word by the wake's clock, a
/// driven line by the cool-off's <c>until</c> as its note said it, an update's restart by its build, a released transient end by when it ended.
/// </summary>
/// <param name="Until">The cool-off's <c>until</c> as the driven record's limit line says it (<see cref="LimitResume.Until"/>); null for unsaid.</param>
/// <param name="Woke">The wake's clock, which a conversation's limit word is keyed by.</param>
/// <param name="Build">The build an update closed the record for, whose restart word resumes it.</param>
/// <param name="Ended">When the record ended past its transient bound, which <i>Try again</i>'s go-on is keyed by.</param>
public sealed record CourseLimit(string? Until = null, DateTimeOffset? Woke = null, string? Build = null, DateTimeOffset? Ended = null);

/// <summary>The phases of a move record (§3.3): a move stands from <c>Decided</c> until it ends delivered, fell back or closed.</summary>
public enum CourseMovePhase
{
    /// <summary>Decided: the course made it and nothing has begun.</summary>
    Decided,

    /// <summary>Delivering: the new part is open and its words are going across.</summary>
    Delivering,

    /// <summary>Held for the person: it stands until it closes <c>resolved</c> by §3.3's one rule.</summary>
    Held,

    /// <summary>Delivered: the move ended.</summary>
    Delivered,

    /// <summary>Fell back: the move ended on the old part.</summary>
    FellBack,

    /// <summary>Closed: the move ended (superseded, resolved, abandoned).</summary>
    Closed,
}

/// <summary>A move record standing for the line (§3.3), as the course reads it.</summary>
/// <param name="Phase">The record's phase.</param>
/// <param name="Destination">Whether this part is the move's destination (it takes no move of its own until the move ends).</param>
/// <param name="OldPart">Whether this part is the move's old part (its words are the move's).</param>
public sealed record CourseStanding(CourseMovePhase Phase, bool Destination = false, bool OldPart = false)
{
    /// <summary>Whether the move still stands: from decided until it ends delivered, fell back or closed.</summary>
    public bool Stands => Phase is CourseMovePhase.Decided or CourseMovePhase.Delivering or CourseMovePhase.Held;
}

/// <summary>
/// Everything the course decides over (the foundation design §3.1's inputs): the line's head, the boundary it stands at, the account
/// facts, the answers-alone hold, the failure, the person's press, the rules and a move record standing for the line.
/// </summary>
/// <param name="Limit">What Daoris's word on a resume is keyed by (FOUNDATION1e part 2).</param>
/// <param name="CarryOn">Which of the planner's carry-ons asked, with <see cref="CourseTrigger.CarryOn"/> (FOUNDATION1e part 2).</param>
public sealed record CourseFacts(
    CourseHead Head, CourseBoundary Boundary = CourseBoundary.BetweenTurns, CourseAccount? Account = null, CourseAlone? Alone = null,
    CourseFailure? Failure = null, CoursePress? Press = null, CourseRules? Rules = null, CourseStanding? Standing = null,
    CourseLimit? Limit = null, CourseCarryOn? CarryOn = null);
