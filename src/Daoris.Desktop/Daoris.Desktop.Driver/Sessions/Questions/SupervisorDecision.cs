namespace Daoris.Driver;

/// <summary>
/// Whether a holder is admitted for a session's repository, and the effective appointment's generation (SUPERVISOR1a-i, D208; the
/// supervisor design §11 row 1a-i): asked at the keeping, at the release and at the admission, each time under the session's decision
/// lock and holding no lock after its synchronous answer. The door's judge (1a-ii) answers it; until then
/// <see cref="NoDecisionAdmission"/> refuses every act, so nothing keeps a decision outside the tests that inject one.
/// </summary>
public interface IDecisionAdmission
{
    /// <summary>The effective appointment for <paramref name="repository"/>, or why none admits (<c>not-offered</c>, <c>not-appointed</c>).</summary>
    DecisionAdmitted Admit(string repository);
}

/// <summary>What <see cref="IDecisionAdmission"/> answered: the effective generation, or why no holder is admitted.</summary>
public sealed record DecisionAdmitted(string? Generation, string? Why)
{
    public static DecisionAdmitted Refused(string why) => new(null, why);
}

/// <summary>The production admission until the door lands (1a-ii): the role is not offered, so every act is refused.</summary>
public sealed class NoDecisionAdmission : IDecisionAdmission
{
    public static readonly NoDecisionAdmission Instance = new();

    public DecisionAdmitted Admit(string repository) => DecisionAdmitted.Refused(SupervisorWhy.NotOffered);
}

/// <summary>
/// The states of a supervisor's decision and their only moves (the supervisor design §3.6): <c>held</c> → <c>released</c> →
/// <c>admitted</c> → <c>went-on</c>; from <c>held</c> or <c>released</c> to <c>overruled</c>, <c>superseded</c> or <c>stale</c>;
/// from <c>admitted</c> to <c>not-handed</c> or <c>interrupted</c>; <c>left</c> for a defer (1b). Every other move is refused.
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

    /// <summary>Whether a decision in <paramref name="from"/> may move to <paramref name="to"/>: §3.6's table, and no other.</summary>
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

    /// <summary>A question whose decision went one of these is the owner's until a new question replaces it (§3.1 <c>overruled</c>).</summary>
    public static bool OwnersAgain(SupervisorState state) => state is SupervisorState.Overruled or SupervisorState.Stale
        or SupervisorState.Superseded or SupervisorState.NotHanded or SupervisorState.Interrupted;
}

/// <summary>The §3.1 refusals and §6.1's log reasons, in the route's words.</summary>
public static class SupervisorWhy
{
    public const string NotOffered = "not-offered";
    public const string AppointmentChanged = "appointment-changed";
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

    // Why a decision went stale (§6.1's `supervisor.stale`).
    public const string Tampered = "tampered";
    public const string Forged = "forged";
    public const string Revoked = "revoked";
    public const string Restarted = "restarted";
    public const string CannotGoOn = "cannot-go-on";
}

/// <summary>A reason the holder gave, naming its source (D135 point 4): the owner's to read, never handed to the session.</summary>
public sealed record SupervisorReason(string Says, string Source);

/// <summary>Who held the role when the decision was kept: its kind and, as a hint, the process it was started from (§2.2).</summary>
public sealed record SupervisorHolder(string Kind, string? Parent);

/// <summary>
/// What a holder answers (§3.1's <c>SUPERVISOR_ANSWER</c> less its proof, which the door checks in 1a-ii): one offered label exactly and
/// one to five reasons, each with a source.
/// </summary>
public sealed record SupervisorAnswer(
    string Session, string Question, string Generation, string Choice, IReadOnlyList<SupervisorReason> Reasons, SupervisorHolder Holder);

/// <summary>
/// What the handler read of the park at the keeping (§3.1's checks the driver makes): its repository, whether it is parked here and
/// holds a quest, the question on file, whether a word of the owner's waits, and whether the ask's go-aheads are open or unread.
/// </summary>
public sealed record SupervisedPark(
    string Repository, bool Parked, bool HoldsQuest, ParkQuestion? Filed, bool OwnerWaits, bool GoAheadOpen, bool GoAheadUnread);

/// <summary>A decision as it stands, for the page and the tests: whole, from memory, or from the files as history.</summary>
/// <param name="Earlier">Read from the files of an earlier run of Daoris: history, granting nothing (§6.1).</param>
public sealed record SupervisorDecisionView(
    string Id, string Session, ParkQuestion Question, string Choice, IReadOnlyList<SupervisorReason> Reasons, SupervisorHolder Holder,
    string Generation, DateTimeOffset KeptAt, DateTimeOffset GoesOnAt, SupervisorState State, DateTimeOffset StateAt, string? Why,
    string? Word, bool Earlier);

/// <summary>What a keeping answered: kept with the decision and when it goes on, or refused with why, keeping nothing.</summary>
public sealed record SupervisorKept(bool Kept, string? Decision, DateTimeOffset? GoesOnAt, string? Why);

/// <summary>
/// The single-use ticket an admission draws (§6.3): the decision, its word and this go-on. The prompt built with it hands the paragraph
/// composed from the decision in memory, consuming it, so a second build hands no supervisor word.
/// </summary>
public sealed record SupervisorTicket(string Session, string Decision, string Word, string Id);

/// <summary>What the service's say answered the release (§3.4): kept with the word's id, refused with its word, or no answer at all.</summary>
public sealed record SupervisorSaid(string? Word, string? Refusal, bool Lost = false);

/// <summary>The release's call of the service's say, with no text: the service composes it from the choice.</summary>
public delegate Task<SupervisorSaid> SupervisorSay(
    string session, string decision, string question, string choice, string key, CancellationToken ct);

/// <summary>A take-back of one waiting word through the service's door (SAIDBACK1a); true where it left the record.</summary>
public delegate Task<bool> SupervisorTakeBack(string session, string word, CancellationToken ct);

/// <summary>
/// The words a session is handed for the supervisor's answer (§4.5): the twin of the service's <c>SupervisorWord.Handed</c>, composed
/// from the decision's label alone and held to it by a test on each side. It grants nothing, the chosen option's <c>does</c> included.
/// </summary>
public static class SupervisorHanded
{
    /// <summary>The author a supervisor's word names on a record (<c>from</c>), and on the record's <c>user</c> event.</summary>
    public const string From = "supervisor";

    public static string Text(string label) =>
        $"The supervisor the owner appointed for this repository chose \"{label}\" among the answers you offered, on the owner's behalf. "
        + "These are not the owner's own words. Choosing tells you which of your offered answers to follow; it grants nothing: no "
        + "go-ahead, sign-in, access, spending, push, publication, deletion or history rewrite, and it overrides no refused go-ahead and "
        + "no repository document that asks the owner. If following it needs any of those, ask through go_ahead_ask or wait for the "
        + "owner. The owner may still say otherwise; their words win.";
}
