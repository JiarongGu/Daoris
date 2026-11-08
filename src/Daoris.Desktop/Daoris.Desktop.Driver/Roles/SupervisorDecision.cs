namespace Daoris.Driver;

/// <summary>
/// Whether a holder is admitted for a session's repository, and the effective supervisor's generation: asked at the keeping, at the release and at the admission, each time under the session's decision
/// lock and holding no lock after its synchronous answer. The door's judge answers it; where none is wired
/// <see cref="NoDecisionAdmission"/> refuses every act, so nothing keeps a decision outside the tests that inject one.
/// </summary>
public interface IDecisionAdmission
{
    /// <summary>The hold of the supervisor answering <paramref name="kind"/> at <paramref name="place"/>; a null act is a leave, needing no grant.</summary>
    DecisionAdmitted Admit(string kind, RolePlace place, string? act);
}

/// <summary>What <see cref="IDecisionAdmission"/> answers: the effective generation, or why no holder is admitted.</summary>
public sealed record DecisionAdmitted(string? Generation, string? Why)
{
    public static DecisionAdmitted Refused(string why) => new(null, why);

    /// <summary>
    /// The overrule window the effective supervisor's role keeps (<see cref="RoleHold.Window"/>), within the decisions' shortest and
    /// longest; null keeps <see cref="QuestionSupervisorDecisions.Window"/>.
    /// </summary>
    public TimeSpan? Window { get; init; }
}

/// <summary>The admission where no judge is wired: no terminal holds the supervisor, so every act is refused.</summary>
public sealed class NoDecisionAdmission : IDecisionAdmission
{
    public static readonly NoDecisionAdmission Instance = new();

    public DecisionAdmitted Admit(string kind, RolePlace place, string? act) => DecisionAdmitted.Refused(SupervisorWhy.NotStaffed);
}

/// <summary>
/// The states of a supervisor's decision and their only moves: <c>held</c> → <c>released</c> →
/// <c>admitted</c> → <c>went-on</c>; from <c>held</c> or <c>released</c> to <c>overruled</c>, <c>superseded</c> or <c>stale</c>;
/// from <c>admitted</c> to <c>not-handed</c> or <c>interrupted</c>; <c>left</c> for a defer. Every other move is refused.
/// </summary>
public enum SupervisorState
{
    Held,
    Released,
    Admitted,
    WentOn,
    NotHanded,
    Interrupted,
    Overruled,
    Superseded,
    Stale,
    Left,
}

public static class SupervisorStates
{
    /// <summary>The state as the files and the page spell it.</summary>
    public static string Spell(SupervisorState state) => state switch
    {
        SupervisorState.WentOn => "went-on",
        SupervisorState.NotHanded => "not-handed",
        _ => state.ToString().ToLowerInvariant(),
    };

    public static SupervisorState? Parse(string? said) =>
        Enum.GetValues<SupervisorState>().Cast<SupervisorState?>().FirstOrDefault(state => Spell(state!.Value) == said);

    /// <summary>Whether a decision in <paramref name="from"/> may move to <paramref name="to"/>: the move table, and no other.</summary>
    public static bool Moves(SupervisorState from, SupervisorState to) => (from, to) switch
    {
        (SupervisorState.Held, SupervisorState.Released) => true,
        (SupervisorState.Held or SupervisorState.Released,
            SupervisorState.Overruled or SupervisorState.Superseded or SupervisorState.Stale) => true,
        (SupervisorState.Released, SupervisorState.Admitted) => true,
        (SupervisorState.Admitted, SupervisorState.WentOn or SupervisorState.NotHanded or SupervisorState.Interrupted) => true,
        _ => false,
    };

    /// <summary>Whether nothing moves it again.</summary>
    public static bool Final(SupervisorState state) =>
        state is not (SupervisorState.Held or SupervisorState.Released or SupervisorState.Admitted);

    /// <summary>A question whose decision goes one of these is the owner's until a new question replaces it (<c>overruled</c>).</summary>
    public static bool OwnersAgain(SupervisorState state) => state is SupervisorState.Overruled or SupervisorState.Stale
        or SupervisorState.Superseded or SupervisorState.NotHanded or SupervisorState.Interrupted;
}

/// <summary>The refusals and the log's reasons, in the route's words.</summary>
public static class SupervisorWhy
{
    /// <summary>No terminal holds the supervisor or the steward there: its role is held by the person's rules alone, or by no one.</summary>
    public const string NotStaffed = "not-staffed";
    public const string NotHolder = "not-holder";

    /// <summary>The role is staffed by the terminal and its terminal is not started this run.</summary>
    public const string NotStarted = "not-started";

    public const string NotGranted = "not-granted";

    /// <summary>The hold an act names is not the role's now: its terminal is started again, or its role changed.</summary>
    public const string RoleChanged = "role-changed";
    public const string NotParked = "not-parked";
    public const string NoQuest = "no-quest";
    public const string QuestionChanged = "question-changed";
    public const string NotOfferedChoice = "not-offered-choice";
    public const string NoSource = "no-source";
    public const string GoAheadOpen = "go-ahead-open";
    public const string GoAheadUnread = "go-ahead-unread";
    public const string Answered = "answered";
    public const string Overruled = "overruled";
    public const string OwnerAnswered = "owner-answered";
    public const string Form = "form";

    /// <summary>The question carries the agent's <c>owner</c> mark: refused at the keeping and again at the release.</summary>
    public const string OwnerQuestion = "owner-question";

    /// <summary>The line's answers-alone count is at the answers-alone bound: refused at the keeping and again at the release.</summary>
    public const string SupervisorBound = "supervisor-bound";

    /// <summary>The answer would take on a line one of the person's rules holds on any count but answers alone.</summary>
    public const string RuleHolds = "rule-holds";

    /// <summary>A course act names no point a person's <c>refer</c> rule holds the line at now.</summary>
    public const string NotReferred = "not-referred";

    // Why a decision goes stale (`supervisor.stale`).
    public const string Tampered = "tampered";
    public const string Forged = "forged";
    public const string Revoked = "revoked";
    public const string Restarted = "restarted";
    public const string CannotGoOn = "cannot-go-on";
}

/// <summary>
/// The bounds a holder leaves a question to the owner by, as <c>SUPERVISOR_DEFER</c> names
/// them: the six an asking agent may also mark its question with (<see cref="QuestionFiles"/>'s <c>owner</c>), and the four only the
/// holder's judgement reaches.
/// </summary>
public static class SupervisorBounds
{
    public static readonly IReadOnlyList<string> All =
        ["go-ahead", "sign-in", "access", "spending", "delete", "repository-asks", "preference", "unrecorded", "contradicts", "instructed"];

    public static bool Known(string? bound) => bound is not null && All.Contains(bound, StringComparer.Ordinal);
}

/// <summary>A reason the holder gave, naming its source: the owner's to read, never handed to the session.</summary>
public sealed record SupervisorReason(string Says, string Source);

/// <summary>Who holds the role when the decision is kept: its kind and, as a hint, the process it is started from.</summary>
public sealed record SupervisorHolder(string Kind, string? Parent)
{
    /// <summary>An agent's hold of it (<see cref="SupervisorHolders.OfAgent"/>); none for a terminal's.</summary>
    public SupervisorAgentHolder? Agent { get; init; }
}

/// <summary>
/// What a holder answers (<c>SUPERVISOR_ANSWER</c> less its proof, which the door checks): one offered label exactly and
/// one to five reasons, each with a source.
/// </summary>
public sealed record SupervisorAnswer(
    string Session, string Question, string Generation, string Choice, IReadOnlyList<SupervisorReason> Reasons, SupervisorHolder Holder);

/// <summary>
/// What a holder leaves to the owner (<c>SUPERVISOR_DEFER</c> less its proof, which the door checks): one of
/// <see cref="SupervisorBounds.All"/> and one to five reasons, each with a source. Kept as a decision of state <c>left</c>.
/// </summary>
public sealed record SupervisorDefer(
    string Session, string Question, string Generation, string Bound, IReadOnlyList<SupervisorReason> Reasons, SupervisorHolder Holder);

/// <summary>
/// What the handler reads of the park at the keeping (the checks the driver makes): its repository, whether it is parked here and
/// holds a quest, the question on file, whether a word of the owner's waits, whether the ask's go-aheads are open or unread, and
/// whether its line's answers-alone count is at the answers-alone bound.
/// </summary>
public sealed record SupervisedPark(
    string Repository, bool Parked, bool HoldsQuest, ParkQuestion? Filed, bool OwnerWaits, bool GoAheadOpen, bool GoAheadUnread,
    bool AtBound = false)
{
    /// <summary>
    /// The line's counts as the keeping reads them, as the print hands them: a reason's
    /// <c>count:</c> source names one of these, and the decision keeps them. Null where none were read.
    /// </summary>
    public IReadOnlyList<CountFact>? Counts { get; init; }

    /// <summary>Whether a rule of the person's holds the line now on a count other than answers alone, its count at the rule's number.</summary>
    public bool RuleHolds => SupervisorRuleHold.Holds(Counts);
}

/// <summary>
/// What keeps <paramref name="session"/>'s line from taking a supervisor's word now, asked by the release under the decision's lock:
/// <c>supervisor-bound</c> at the answers-alone bound, <c>rule-holds</c> where another of the person's rules
/// holds it, none where the word may go on.
/// </summary>
public delegate Task<string?> SupervisorBound(string session, CancellationToken ct);

/// <summary>A decision as it stands, for the page and the tests: whole, from memory, or from the files as history.</summary>
/// <param name="Earlier">Read from the files of an earlier run of Daoris: history, granting nothing.</param>
/// <param name="Bound">A defer's bound (state <c>left</c>), its <paramref name="Choice"/> empty; null for an answer.</param>
public sealed record SupervisorDecisionView(
    string Id, string Session, ParkQuestion Question, string Choice, IReadOnlyList<SupervisorReason> Reasons, SupervisorHolder Holder,
    string Generation, DateTimeOffset KeptAt, DateTimeOffset GoesOnAt, SupervisorState State, DateTimeOffset StateAt, string? Why,
    string? Word, bool Earlier, string? Bound = null);

/// <summary>What a keeping answered: kept with the decision and when it goes on, or refused with why, keeping nothing.</summary>
public sealed record SupervisorKept(bool Kept, string? Decision, DateTimeOffset? GoesOnAt, string? Why);

/// <summary>
/// The single-use ticket an admission draws: the decision, its word and this go-on. The prompt built with it hands the paragraph
/// composed from the decision in memory, consuming it, so a second build hands no supervisor word.
/// </summary>
public sealed record SupervisorTicket(string Session, string Decision, string Word, string Id);

/// <summary>What the service's say answers the release: kept with the word's id, refused with its word, or no answer at all.</summary>
public sealed record SupervisorSaid(string? Word, string? Refusal, bool Lost = false);

/// <summary>The release's call of the service's say, with no text: the service composes it from the choice.</summary>
public delegate Task<SupervisorSaid> SupervisorSay(
    string session, string decision, string question, string choice, string key, CancellationToken ct);

/// <summary>A take-back of one waiting word through the service's door; true where it left the record.</summary>
public delegate Task<bool> SupervisorTakeBack(string session, string word, CancellationToken ct);

/// <summary>
/// The words a session is handed for the supervisor's answer: the twin of the service's <c>SupervisorWord.Handed</c>, composed
/// from the decision's label alone and held to it by a test on each side. It grants nothing, the chosen option's <c>does</c> included.
/// </summary>
public static class SupervisorHanded
{
    /// <summary>The author a supervisor's word names on a record (<c>from</c>), and on the record's <c>user</c> event.</summary>
    public const string From = SaidFrom.Supervisor;

    public static string Text(string label) =>
        $"The supervisor the owner appointed for this repository chose \"{label}\" among the answers you offered, on the owner's behalf. "
        + "These are not the owner's own words. Choosing tells you which of your offered answers to follow; it grants nothing: no "
        + "go-ahead, sign-in, access, spending, push, publication, deletion or history rewrite, and it overrides no refused go-ahead and "
        + "no repository document that asks the owner. If following it needs any of those, ask through go_ahead_ask or wait for the "
        + "owner. The owner may still say otherwise; their words win.";
}
