namespace Daoris.Driver;

/// <summary>A file a quest carries, as the local service answered it.</summary>
/// <param name="Name">The file's own name.</param>
/// <param name="Sha256">Its content's hash.</param>
/// <param name="Bytes">Its size.</param>
/// <param name="Path">
/// Where THIS machine keeps it — told by the service, never derived here: the layout under the home is
/// the service's, and the driver's home is not always the service's (it is wherever `driver.json`
/// lives). Null is honest and common: a quest mirrored from another machine names files whose bytes
/// stayed where it was published.
/// </param>
public sealed record QuestFileView(string Name, string Sha256, long Bytes, string? Path);

/// <summary>One step of a chain, as the service answered it.</summary>
public sealed record QuestStepView(string To, string Title, string Body)
{
    /// <summary>The environment the step shows its chain's work in, which makes it a set-up step (REVIEWENV1b); null otherwise.</summary>
    public string? SetUpIn { get; init; }
}

/// <summary>
/// How a done answered one requirement (DRIFT1d), as the service answers it: its number, and <paramref name="Met"/> with
/// how its check was met, or <paramref name="Departed"/> with the reason and <paramref name="Quote"/>, the person's words
/// it turns on.
/// </summary>
public sealed record QuestAnswerView(int Requirement, string? Met, string? Departed, string? Quote);

/// <summary>A taker's submission in a closure graph (FABRIC-CLOSE1a), as the service answers it: the operation that made it, when, and its note.</summary>
/// <param name="Commit">
/// The commit its evidence was read at (FABRIC-CLOSE1c5), or, where it waits on no evidence, the one its session's end read alone
/// (FABRIC-CLOSE1c6), by its full id: what a step that grew from it is brought up to date with on the person's go-on. Null where none was
/// read, and from a host before the field.
/// </param>
/// <param name="Session">
/// The session that said its <c>ready</c> (FABRIC-CLOSE1c4), the one a reopen of it is told to. Null where no session said it, and from
/// a host before the field.
/// </param>
public sealed record QuestSubmissionView(
    string Machine, long Sequence, DateTimeOffset? At, string? Note, string? Commit = null, string? Session = null);

/// <summary>
/// The person's reopen of a quest's own submission (FABRIC-CLOSE1c4), as the service answers it until a later submission answers it:
/// the reopen by its machine and sequence, the submission it reopened (<paramref name="Of"/>), their words, and when.
/// </summary>
/// <param name="Session">
/// The session that said the reopened submission's <c>ready</c> (FABRIC-CLOSE1c8): the one this machine tells of the reopen. Null where
/// no session said it (the respond door), and from a host before the field.
/// </param>
public sealed record QuestReopenView(
    string Machine, long Sequence, QuestSubmissionRef Of, string? Note, DateTimeOffset? At, string? Session = null);

/// <summary>One thing the person requires of a quest (DRIFT1c), as the service answers it: their words, and the check that proves them.</summary>
public sealed record QuestRequirementView(string Quote, string Check)
{
    /// <summary>
    /// What Daoris reads itself to tell the check was met (EVID1a, D144 §2): each a path the done's commit must hold, or a
    /// gate. Empty for a requirement that names none, and from a host before evidence.
    /// </summary>
    public IReadOnlyList<QuestEvidenceItem> Evidence { get; init; } = [];

    // A list compares by what it holds, so two reads of one requirement are one requirement.
    public bool Equals(QuestRequirementView? other) =>
        other is not null && Quote == other.Quote && Check == other.Check && Evidence.SequenceEqual(other.Evidence);

    public override int GetHashCode() => HashCode.Combine(Quote, Check, Evidence.Count);
}

/// <summary>
/// Whose a taken quest's take is, as this machine reads it (CARRY2b): what the ledger asks before it carries a quest on
/// (CARRY2), read so a look says it before asking. Read as an abandon reads a take (<c>WorkAbandoning.QuestAct</c>).
/// </summary>
/// <param name="Claim">This machine's claim on it, as its host answers (D68 §4): <c>none</c>, <c>held</c>, <c>unconfirmed</c> or <c>lost</c>.</param>
/// <param name="Took">Whether a session record of this machine's, in any state, marks that it took the quest (STANDDOWN2).</param>
/// <param name="Teammate">
/// The newest teammate's record on it that did not stand down, as <c>origin/id</c>, or null: what names another machine's take,
/// since the quest's log names a machine by an id no person reads.
/// </param>
/// <param name="Why">Why a lost claim lost, as the host said it beside the claim (FABRIC-CLOSE1c18), or null.</param>
/// <param name="Forward">
/// The person's bring-forward the take was made by (SYNCMERGE1b0 point 3), as the claim's answer names it, or null: what a stood-down
/// session of this machine's is carried on by (SYNCMERGE1b, <see cref="Planner.BroughtForward"/>).
/// </param>
/// <param name="At">
/// When the take the quest stands taken by was made, as the claim's answer says it (CARRY3), or null from a host before it: what a take
/// made here after a session ended is judged by (<see cref="Planner.TakenAt"/>), which a submission, a reopen or a go-on never moves.
/// </param>
public sealed record QuestTake(
    string Claim, bool Took = false, string? Teammate = null, string? Why = null, ForwardTake? Forward = null, DateTimeOffset? At = null)
{
    /// <summary>
    /// The claim was lost to the person's reopen of the work the step grew from, not to another machine's take (FABRIC-CLOSE1c20). Never
    /// while a bring-forward's take stands: the claim's <c>why</c> still names the reopen the take once lost to (SYNCMERGE1b).
    /// </summary>
    public bool Reopened => Claim == "lost" && Why == "source-reopened" && Forward is not { Stands: true };

    /// <summary>The take is this machine's by its claim: one a remote numbered, or one only here (D68 §4).</summary>
    public bool Here => Claim is "held" or "unconfirmed";
}

/// <summary>
/// The fresh take a person's bring-forward made of a step this machine's take lost (SYNCMERGE1b0, D178 points 3 and 8), as the claim's
/// answer names it: the take, when it was made, and where it stands in the claim's words.
/// </summary>
/// <param name="Standing"><c>held</c> once a remote numbered it, <c>unconfirmed</c> while it is only here, <c>lost</c> where it lost again.</param>
public sealed record ForwardTake(string Machine, long Sequence, DateTimeOffset At, string Standing)
{
    /// <summary>The take stands this machine's: confirmed, or unconfirmed offline, which goes on all the same (D68 rule 1, D178 point 8).</summary>
    public bool Stands => Standing is "held" or "unconfirmed";
}

/// <summary>
/// One of this machine's session records as the goal's walk counts it (TOOL6b, D130 §4.2, §16.2): the adapter it ran on,
/// the account (null for the tool's own sign-in), when it was opened, and whether it runs now. Read on loopback alone,
/// where the record names its account (D47 §4); a teammate's record is not this machine's.
/// </summary>
public sealed record SessionStarted(string Adapter, string? Profile, DateTimeOffset? Created, bool Running)
{
    /// <summary>The record's id, which <see cref="HarnessRoster.Release"/> reads to make a refused open's record not running.</summary>
    public string? Id { get; init; }

    /// <summary>
    /// Its record ended <c>choice-not-offered</c> (D205 §3.5(c), MODELROLE1c2): set from the end code where the look builds it, so the walk
    /// counts it neither running nor last started, and the refusing account is not read as just used.
    /// </summary>
    public bool NotOffered { get; init; }
}

/// <summary>
/// One fact a requirement names as its evidence (EVID1a, D144 point 1), as the service answers it: exactly one of a
/// repository-relative <see cref="Path"/> the done's commit must hold, or a <see cref="Gate"/> the receiving repository
/// declares. The check stays the person's words; this is the part of it Daoris reads itself (EVID1b).
/// </summary>
public sealed record QuestEvidenceItem(string? Path, string? Gate = null)
{
    /// <summary>Its kind's word, as a verdict names it: <c>path</c> or <c>gate</c>.</summary>
    public string Kind => Path is not null ? "path" : "gate";

    /// <summary>What it names: the path, or the gate.</summary>
    public string Named => Path ?? Gate ?? "";
}

/// <summary>A submission by the machine and sequence that made it (FABRIC-CLOSE1e): what a verdict and a close name it by.</summary>
public sealed record QuestSubmissionRef(string Machine, long Sequence);
