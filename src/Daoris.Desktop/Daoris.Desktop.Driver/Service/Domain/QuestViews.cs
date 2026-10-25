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

/// <summary>A quest as the service answered it — enough to decide on, and enough to compose a target from.</summary>
public sealed record QuestView(string Id, string From, string To, string Title, string Body, string Status)
{
    /// <summary>
    /// What a list calls it, as the service answered (SESSUX1j): its publisher's short title, or the name the service read
    /// from its words. Null from a host before the field, which names it by its title.
    /// </summary>
    public string? Short { get; init; }

    /// <summary>The quest's name where one is shown (SESSUX1j): its short title, else its title.</summary>
    public string Name => Short is { Length: > 0 } named ? named : Title;

    /// <summary>Addresses the quest carries — a ticket, a page — handed to the session as given.</summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>Files the quest carries, each with where this machine keeps it, or null when it does not.</summary>
    public IReadOnlyList<QuestFileView> Attachments { get; init; } = [];

    /// <summary>What closing this done will publish next (D65 §4) — the service's to do, never the driver's.</summary>
    public IReadOnlyList<QuestStepView> Then { get; init; } = [];

    /// <summary>The quest whose close published this one, when it is a step of a chain.</summary>
    public string? Parent { get; init; }

    /// <summary>
    /// The person's kind and workflow choices on a quest no ask published, made on its page before it started (WORKFLOW1e2, the
    /// workflow design §4.1 row 1), oldest first, the latest standing; empty for none, and from a host before them.
    /// </summary>
    public IReadOnlyList<AskWorkflowChoiceView> WorkflowChoices { get; init; } = [];

    /// <summary>The machine that holds the quest, as the list answers it (SYNCMERGE1b1); null where none is named.</summary>
    public string? Holder { get; init; }

    /// <summary>This machine's id where the list answers it as the page's (SYNCMERGE1b1); null off this machine or from a host before it.</summary>
    public string? Here { get; init; }

    /// <summary>
    /// The question its taker waits on (D79) — another quest, asked of the repository that knows — or
    /// null. A taken quest carrying one is its taker's to resume once the question closes. Only a taken quest waits: a
    /// service keeps the field after the quest closes (AWAITSTALE1), and the owner's words of 2026-10-15 to a done quest's
    /// session were planned and then sent down the open door, which refused them, because the start read it. So a quest
    /// that is not taken names none here, to every reader. A taken quest keeps naming its question after the answer and after
    /// the resume it made (SAYHELD1): whether it still waits is the planner's <c>ResumedOnAnswer</c>, never this field alone.
    /// </summary>
    public string? Awaits
    {
        get => Status == "Taken" ? awaits : null;
        init => awaits = value;
    }

    private readonly string? awaits;

    /// <summary>
    /// The session whose connector published it (SESS1), as the machine that ran it spells it, or null: a person's publish,
    /// or a chain's step. How a question a session asked joins that session's work (PAUSE1a, <see cref="AskWork"/>).
    /// </summary>
    public string? PublishedBy { get; init; }

    /// <summary>What its close said — a done's note or a decline's reason. The answer a waiting session resumes with.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// Its note's coded parts as the service answered them (QUESTCLOSE1b1), or null from a host before them; the terminal
    /// stays English (D142 point 9), so a reader that words a close in the person's language takes them, as a session's do.
    /// </summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    /// <summary>
    /// The circle it is held in (D48), as the service answered it, or the default where a host answers none: which
    /// workspace's pass carries an abandon's decline of it (PAUSE1d, D132 §3.4 step 4).
    /// </summary>
    public string Workspace { get; init; } = RemoteTarget.DefaultWorkspace;

    /// <summary>
    /// Whether the service would delete it (D95): its own reading, the one the drawer's Delete is shown by.
    /// Absent is false — a host from before the delete door offers none.
    /// </summary>
    public bool Deletable { get; init; }

    /// <summary>
    /// The lanes of <see cref="To"/> it addresses (D115 §2.2), as the service answered them; empty for a
    /// quest to the whole repository. Carried, and not yet planned on: the lane locks are DEV6's.
    /// </summary>
    public IReadOnlyList<string> Lanes { get; init; } = [];

    /// <summary>Whom it asks as a person reads it: `repository`, or `repository:lane+lane` (the service's `QuestAddress.Spell`).</summary>
    public string Address => Lanes.Count == 0 ? To : $"{To}:{string.Join('+', Lanes)}";

    /// <summary>
    /// What the person requires of it (DRIFT1c, D133 §3), each their words and its check, in the service's order — the
    /// numbers its done answers by (DRIFT1d). Empty for a quest that names none, and from a host before requirements.
    /// </summary>
    public IReadOnlyList<QuestRequirementView> Requirements { get; init; } = [];

    /// <summary>
    /// Whether a departure holds it for the person's yes (DRIFT1d, D133 §4): closed done departing from what they required,
    /// not yet accepted. The service lists it among the open, so a quest waiting on it waits. Absent is false.
    /// </summary>
    public bool Held { get; init; }

    /// <summary>
    /// How its done answered each requirement (DRIFT1d, D133 §4), by number, as the service answers them: what an accept
    /// shows the person before their yes (DRIFT1d2). Empty for a quest no done answered, and from a host before answers.
    /// </summary>
    public IReadOnlyList<QuestAnswerView> Answers { get; init; } = [];

    /// <summary>
    /// Why a held done waits (EVID1a, D144 §6), as the service answers <c>hold</c>: <c>departed</c>, <c>evidence-unread</c>,
    /// <c>evidence-missing</c> or, a set-up step's after both, <c>unreviewed</c> (REVIEWENV1b; <see cref="EvidenceCodes"/>).
    /// Null when nothing holds it, and from a host before the field, whose only hold was a departure.
    /// </summary>
    public string? Hold { get; init; }

    /// <summary>
    /// Whether its done waits for Daoris to find its evidence (EVID1a, D144 §3): a met answer names some and none was found
    /// yet, a departure beside it or not. What a session's end, the sweep and the terminal's check read. Absent is false.
    /// </summary>
    public bool AwaitsEvidence { get; init; }

    /// <summary>What Daoris last read of its evidence (EVID1a, D144 §5), or null while nothing was.</summary>
    public EvidenceVerdict? Evidence { get; init; }

    /// <summary>
    /// When its status last moved, as the service answered it (<c>updated</c>). For a taken quest that waits on nothing it is
    /// when it was taken (CARRY2b): a wait is the one other move a taken quest makes, and it leaves the quest waiting. Null
    /// from a host that answers none.
    /// </summary>
    public DateTimeOffset? Updated { get; init; }

    // The review's part of a quest; their element records are declared in Service/Domain.

    /// <summary>Its chain's review choice (REVIEWENV1b); null where it chose none, and from a host before the field.</summary>
    public QuestReviewChoiceView? Review { get; init; }

    /// <summary>
    /// The environment it shows its chain's work in, which makes it a set-up step (REVIEWENV1b, D154 point 4); null for every
    /// other quest, and from a host before the field.
    /// </summary>
    public string? SetUpIn { get; init; }

    /// <summary>The set-ups said on it, oldest first (REVIEWENV1b); empty where none was, and from a host before them.</summary>
    public IReadOnlyList<QuestSetUpView> SetUps { get; init; } = [];

    /// <summary>The person's verdicts on its review, oldest first (REVIEWENV1b); empty where none was given, and from a host before them.</summary>
    public IReadOnlyList<QuestReviewVerdictView> Verdicts { get; init; } = [];

    /// <summary>
    /// The digest of the graph that closes it (FABRIC-CLOSE1a, bound by the driver at its take, FABRIC-CLOSE1b), or null: a quest no
    /// closure graph holds, every quest from before, and every quest a host from before answers.
    /// </summary>
    public string? Closure { get; init; }

    /// <summary>
    /// Its taker's current submission (FABRIC-CLOSE1a): the agent's <c>ready</c>, its result while it stays Taken, by the machine and
    /// sequence a close and an evidence verdict name it by; null where there is none, and from a host before the field.
    /// </summary>
    public QuestSubmissionView? Submission { get; init; }

    /// <summary>
    /// The submission of its <see cref="Parent"/> it grew from (FABRIC-CLOSE1c, §E5), by its machine and sequence: what seals that
    /// submission while it stands (FABRIC-WORKFLOW2d). Null for a step a done published, every other quest, and from a host before it.
    /// </summary>
    public QuestSubmissionRef? GrewFrom { get; init; }

    /// <summary>
    /// The submission it grew from, reopened by the person (FABRIC-CLOSE1c, §E5), while that stands: the step is withdrawn or held, and
    /// seals nothing. Null for every other quest, once a rebind names its new source, and from a host before it.
    /// </summary>
    public QuestSubmissionRef? SourceReopened { get; init; }

    /// <summary>
    /// Whether the person's reopen of the submission it grew from holds it (FABRIC-CLOSE1c2, §E5): open, it is withdrawn, and taken, it
    /// is held, until they go on with it on the new work or abandon it. Read from <see cref="SourceReopened"/>, the service's fact, on a
    /// live quest alone, since once closed its close is what stands. False from a host before the field, and on the owner's install.
    /// </summary>
    public bool HeldByReopen => SourceReopened is not null && Status is "Open" or "Taken";

    /// <summary>
    /// The person's last reopen of its own submission that no later submission answered (FABRIC-CLOSE1c4, §E5): what tells a reopened
    /// source, with no <see cref="Submission"/>, from one whose taker has not yet said <c>ready</c>. Null once a later <c>ready</c>
    /// answered it, for every quest never reopened, and from a host before the field.
    /// </summary>
    public QuestReopenView? Reopened { get; init; }
}
