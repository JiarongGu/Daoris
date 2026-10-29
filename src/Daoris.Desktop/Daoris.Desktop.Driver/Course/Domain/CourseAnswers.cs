namespace Daoris.Driver;

// FOUNDATION1e part 1 (the foundation design §3.1's answer table): what the course answers, read by nothing. The six answers carry
// their preconditions; a move's `why` and `by` are the closed enums at the end, today's nine reasons and three authors one to one.
// Part 2 adds LineCourse.Decide and its table beside this file; no method here decides.

/// <summary>When a move opens its new part: at once, or when the running turn ends (the move's first axis, §3.1).</summary>
public enum CourseWhen
{
    /// <summary>Now: the person's press on a quest's session, which stops it as a stop (D163 point 1, D188 point 1).</summary>
    Now,

    /// <summary>At the turn's end: an agent's ask, the curve, and the person's press on a conversation with a closing turn asked.</summary>
    TurnEnd,
}

/// <summary>How a move carries the line's context to its new part (the move's third axis, §3.1).</summary>
public enum CourseHow
{
    /// <summary>Daoris's brief of the whole line, the one composer.</summary>
    Brief,

    /// <summary>The conversation's own files carried across, once SWITCHCTX1 lands (path M); no code writes it yet.</summary>
    Carry,
}

/// <summary>Which account a new part takes (the move's second axis, §3.2 step 6).</summary>
public enum CourseAccountSource
{
    /// <summary>The line's own account: a resume keeps it, and a fresh part the person pressed for takes it where ready.</summary>
    Own,

    /// <summary>An account named: a hand-over press's, a handed quest's, a pick; it comes first.</summary>
    Named,

    /// <summary>The walk's one settled selection: an automatic new part never takes the own account where it could resume there.</summary>
    Settled,
}

/// <summary>Where a new part runs: a role and choice, and the account by <see cref="CourseAccountSource"/>.</summary>
public sealed record CourseWhere(CourseAccountSource Source, string? Role = null, string? Choice = null, string? Account = null);

/// <summary>Daoris's own word by its key, which goes beside the words kept: the limit's end and the transient retry.</summary>
public enum CourseDaorisWord
{
    /// <summary>No word of Daoris's goes.</summary>
    None,

    /// <summary>The limit word, keyed by the cool-off it waited on (<c>LimitReset</c>).</summary>
    LimitReset,

    /// <summary>The transient retry's instruction, keyed by the episode (<c>TransientRetry</c>).</summary>
    TransientRetry,
}

/// <summary>The words that go: the ids waiting, in order, and Daoris's word by its key.</summary>
public sealed record CourseWords(IReadOnlyList<string> Ids, CourseDaorisWord Daoris = CourseDaorisWord.None);

/// <summary>Why a line waits for a moment (<see cref="LineCourse.Wait"/>).</summary>
public enum CourseWaitWhy
{
    /// <summary>The own account cools and no account the line may take is ready: it waits for the earliest reset.</summary>
    AccountCooling,

    /// <summary>A limit's end: the line waits for the cool-off it ended on.</summary>
    LimitReset,

    /// <summary>A transient retry's next try.</summary>
    TransientRetry,
}

/// <summary>Why a line holds for the person (<see cref="LineCourse.Hold"/>), by reason code.</summary>
public enum CourseHoldWhy
{
    /// <summary>The answers-alone bound: the next solicited answer waits for the person (COWORK1e).</summary>
    AnswersAlone,

    /// <summary>No account carries the words and none resets: <see cref="LineAccount"/>'s hold, naming its repair.</summary>
    Account,

    /// <summary>A transient end past its bound: it waits for the person's word or *Try again* (path N).</summary>
    TransientBound,

    /// <summary>A conversation's words with nothing to carry them on by itself: *cannot go on* (path B).</summary>
    CannotGoOn,

    /// <summary>The hand-over rule does not admit an agent's ask, so it goes to the person.</summary>
    ConsentRefused,

    /// <summary>The scope's switch does not admit Daoris's own curve move.</summary>
    SwitchOff,

    /// <summary>A move held for the person stands on the line until it closes <c>resolved</c> (§3.3).</summary>
    MoveHeld,
}

/// <summary>A door out of a hold: what the person can press.</summary>
public enum CourseDoor
{
    /// <summary>*Go on in a new session*.</summary>
    GoOnNew,

    /// <summary>*Start a conversation with these words*, for a closed quest or a conversation.</summary>
    StartFrom,

    /// <summary>*Try again*, after a transient end.</summary>
    TryAgain,

    /// <summary>A word of the person's own, which resets the answers-alone count.</summary>
    Say,

    /// <summary>*Go on with this answer*, which lets a held answer go on.</summary>
    LetAnswerGoOn,

    /// <summary>*Not now*.</summary>
    NotNow,
}

/// <summary>Why a record takes no decision at all (<see cref="LineCourse.Never"/>).</summary>
public enum CourseNeverWhy
{
    /// <summary>A teammate's record: never this machine's to resume.</summary>
    Teammate,

    /// <summary>An intake record.</summary>
    Intake,

    /// <summary>A stood-down record.</summary>
    StoodDown,

    /// <summary>A record a later part supersedes.</summary>
    Superseded,

    /// <summary>The old part of a standing move, whose words are the move's (O-2).</summary>
    StandingMove,
}

/// <summary>
/// What the answer was read on, which the executor checks before it acts and asks again where one moved (§3.1): the record's revision, the
/// words' state (their ids waiting), the quest's status, the account's cooling entry and the move record's phase.
/// </summary>
public sealed record CoursePreconditions(
    long? Revision = null, IReadOnlyList<string>? WaitingIds = null, string? QuestStatus = null, CoolingEntry? Cooling = null,
    CourseMovePhase? MovePhase = null);

/// <summary>
/// How a line goes on: <see cref="Resume"/>, <see cref="Move"/>, <see cref="Start"/>, <see cref="Wait"/>, <see cref="Hold"/> or
/// <see cref="Never"/> (the foundation design §3.1). Every answer but <see cref="Never"/> carries its <see cref="CoursePreconditions"/>.
/// </summary>
public abstract record LineCourse
{
    private LineCourse()
    {
    }

    /// <summary>The same part, its own conversation and account, with the words that go and Daoris's word by its key.</summary>
    public sealed record Resume(CourseWords Words, CoursePreconditions Needs) : LineCourse;

    /// <summary>A new part of the same line, on three axes, with its closed <c>why</c> and <c>by</c> and the words it carries.</summary>
    public sealed record Move(
        CourseWhen When, CourseWhere Where, CourseHow How, CourseFromWhy Why, CourseFromBy By, CourseWords Words, CoursePreconditions Needs)
        : LineCourse;

    /// <summary>A new line with no <c>from</c>, the person's words as its first, keyed by the press's own intent key; no move record.</summary>
    public sealed record Start(string Key, CourseWords Words, CoursePreconditions Needs) : LineCourse;

    /// <summary>Until a moment, its reason and the accounts it waits on.</summary>
    public sealed record Wait(DateTimeOffset Until, CourseWaitWhy Why, IReadOnlyList<string> Accounts, CoursePreconditions Needs) : LineCourse;

    /// <summary>For the person, its reason code and the doors out, a repair named where one is the way out.</summary>
    public sealed record Hold(CourseHoldWhy Why, IReadOnlyList<CourseDoor> Doors, LineRepair Repair, CoursePreconditions Needs) : LineCourse;

    /// <summary>A record that takes no decision: it is refused at the say door and passed by the come-up's sweep.</summary>
    public sealed record Never(CourseNeverWhy Why) : LineCourse;
}

/// <summary>
/// Why a new part continues another, closed (the foundation design §3.1, <c>from_why</c>): <see cref="HandOverWhy"/>'s nine reasons one to
/// one. <c>ContinueWhy.Account</c> is a box reason a closed quest's hand-over carries and is never a <c>from_why</c> (O3-13).
/// </summary>
public enum CourseFromWhy
{
    /// <summary>The person pressed it.</summary>
    Press,

    /// <summary>The account was near its limit.</summary>
    Near,

    /// <summary>The account reached its limit.</summary>
    Limit,

    /// <summary>The agent asked for it.</summary>
    Agent,

    /// <summary>Daoris moved it by the person's switching setting.</summary>
    Curve,

    /// <summary>Its own conversation was proven lost, so it went on in a new part.</summary>
    Resume,

    /// <summary>A carry-on on the same account after a cut-off, a time-out, a crash or a refused turn.</summary>
    CarryOn,

    /// <summary>A driven record's fallback: its resume was refused.</summary>
    Retry,

    /// <summary>A replacement: the agent did not offer the model asked, so the record never began.</summary>
    NotOffered,
}

/// <summary>Who asked for a new part (the foundation design §3.1, <c>from_by</c>): <see cref="HandOverBy"/>'s three authors one to one.</summary>
public enum CourseFromBy
{
    /// <summary>The person, by a press or the terminal verb.</summary>
    Person,

    /// <summary>Daoris, by its own look.</summary>
    Daoris,

    /// <summary>The agent, by its own ask.</summary>
    Agent,
}

/// <summary>The closed <c>from_why</c> and <c>from_by</c> beside the codes records keep today, which stay as they are.</summary>
public static class CourseFrom
{
    /// <summary>The closed reason for a code a record keeps; null for a word that is none (<c>account</c> among them).</summary>
    public static CourseFromWhy? WhyOf(string? code) => code switch
    {
        HandOverWhy.Press => CourseFromWhy.Press,
        HandOverWhy.Near => CourseFromWhy.Near,
        HandOverWhy.Limit => CourseFromWhy.Limit,
        HandOverWhy.Agent => CourseFromWhy.Agent,
        HandOverWhy.Curve => CourseFromWhy.Curve,
        HandOverWhy.Resume => CourseFromWhy.Resume,
        HandOverWhy.CarryOn => CourseFromWhy.CarryOn,
        HandOverWhy.Retry => CourseFromWhy.Retry,
        HandOverWhy.NotOffered => CourseFromWhy.NotOffered,
        _ => null,
    };

    /// <summary>The code a record keeps for a closed reason.</summary>
    public static string Code(CourseFromWhy why) => why switch
    {
        CourseFromWhy.Press => HandOverWhy.Press,
        CourseFromWhy.Near => HandOverWhy.Near,
        CourseFromWhy.Limit => HandOverWhy.Limit,
        CourseFromWhy.Agent => HandOverWhy.Agent,
        CourseFromWhy.Curve => HandOverWhy.Curve,
        CourseFromWhy.Resume => HandOverWhy.Resume,
        CourseFromWhy.CarryOn => HandOverWhy.CarryOn,
        CourseFromWhy.Retry => HandOverWhy.Retry,
        _ => HandOverWhy.NotOffered,
    };

    /// <summary>The closed author for a code a record keeps; null for a word that is none.</summary>
    public static CourseFromBy? ByOf(string? code) => code switch
    {
        HandOverBy.Person => CourseFromBy.Person,
        HandOverBy.Daoris => CourseFromBy.Daoris,
        HandOverBy.Agent => CourseFromBy.Agent,
        _ => null,
    };

    /// <summary>The code a record keeps for a closed author.</summary>
    public static string Code(CourseFromBy by) => by switch
    {
        CourseFromBy.Person => HandOverBy.Person,
        CourseFromBy.Daoris => HandOverBy.Daoris,
        _ => HandOverBy.Agent,
    };
}
