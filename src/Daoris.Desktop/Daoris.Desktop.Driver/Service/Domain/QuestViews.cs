namespace Daoris.Driver;

/// <summary>A file a quest carries, as the local service answers it.</summary>
/// <param name="Name">The file's own name.</param>
/// <param name="Sha256">Its content's hash.</param>
/// <param name="Bytes">Its size.</param>
/// <param name="Path">
/// Where THIS machine keeps it — told by the service, never derived here: the layout under the home is
/// the service's, and the driver's home is not always the service's (it is wherever `driver.json`
/// lives). Null is honest and common: a quest mirrored from another machine names files whose bytes
/// stay where it is published.
/// </param>
public sealed record QuestFileView(string Name, string Sha256, long Bytes, string? Path);

/// <summary>One step of a chain, as the service answers it.</summary>
public sealed record QuestStepView(string To, string Title, string Body)
{
    /// <summary>The environment the step shows its chain's work in, which makes it a set-up step; null otherwise.</summary>
    public string? SetUpIn { get; init; }
}

/// <summary>
/// How a done answered one requirement, as the service answers it: its number, and <paramref name="Met"/> with
/// how its check is met, or <paramref name="Departed"/> with the reason and <paramref name="Quote"/>, the person's words
/// it turns on.
/// </summary>
public sealed record QuestAnswerView(int Requirement, string? Met, string? Departed, string? Quote);

/// <summary>A taker's submission in a closure graph, as the service answers it: the operation that made it, when, and its note.</summary>
/// <param name="Commit">
/// The commit its evidence is read at, or, where it waits on no evidence, the one its session's end read alone,
/// by its full id: what a step that grew from it is brought up to date with on the person's go-on. Null where none is
/// read, and from an older host.
/// </param>
/// <param name="Session">
/// The session that said its <c>ready</c>, the one a reopen of it is told to. Null where no session said it, and from
/// an older host.
/// </param>
public sealed record QuestSubmissionView(
    string Machine, long Sequence, DateTimeOffset? At, string? Note, string? Commit = null, string? Session = null);

/// <summary>
/// The person's reopen of a quest's own submission, as the service answers it until a later submission answers it:
/// the reopen by its machine and sequence, the submission it reopened (<paramref name="Of"/>), their words, and when.
/// </summary>
/// <param name="Session">
/// The session that said the reopened submission's <c>ready</c>: the one this machine tells of the reopen. Null where
/// no session said it (the respond door), and from an older host.
/// </param>
public sealed record QuestReopenView(
    string Machine, long Sequence, QuestSubmissionRef Of, string? Note, DateTimeOffset? At, string? Session = null);

/// <summary>One thing the person requires of a quest, as the service answers it: their words, and the check that proves them.</summary>
public sealed record QuestRequirementView(string Quote, string Check)
{
    /// <summary>
    /// What Daoris reads itself to tell the check is met: each a path the done's commit must hold, or a
    /// gate. Empty for a requirement that names none, and from an older host.
    /// </summary>
    public IReadOnlyList<QuestEvidenceItem> Evidence { get; init; } = [];

    // A list compares by what it holds, so two reads of one requirement are one requirement.
    public bool Equals(QuestRequirementView? other) =>
        other is not null && Quote == other.Quote && Check == other.Check && Evidence.SequenceEqual(other.Evidence);

    public override int GetHashCode() => HashCode.Combine(Quote, Check, Evidence.Count);
}

/// <summary>
/// Whose a taken quest's take is, as this machine reads it: what the ledger asks before it carries a quest on,
/// read so a look says it before asking. Read as an abandon reads a take (<c>WorkAbandoning.QuestAct</c>).
/// </summary>
/// <param name="Claim">This machine's claim on it, as its host answers: <c>none</c>, <c>held</c>, <c>unconfirmed</c> or <c>lost</c>.</param>
/// <param name="Took">Whether a session record of this machine's, in any state, marks that it took the quest.</param>
/// <param name="Teammate">
/// The newest teammate's record on it that did not stand down, as <c>origin/id</c>, or null: what names another machine's take,
/// since the quest's log names a machine by an id no person reads.
/// </param>
/// <param name="Why">Why a lost claim lost, as the host says it beside the claim, or null.</param>
/// <param name="Forward">
/// The person's bring-forward that made the take, as the claim's answer names it, or null: what a stood-down
/// session of this machine's is carried on by (<see cref="Planner.BroughtForward"/>).
/// </param>
/// <param name="At">
/// When the take the quest stands taken by happened, as the claim's answer says it, or null from an older host: what a take
/// made here after a session ended is judged by (<see cref="Planner.TakenAt"/>), which a submission, a reopen or a go-on never moves.
/// </param>
public sealed record QuestTake(
    string Claim, bool Took = false, string? Teammate = null, string? Why = null, ForwardTake? Forward = null, DateTimeOffset? At = null)
{
    /// <summary>
    /// The claim lost to the person's reopen of the work the step grew from, not to another machine's take. Never
    /// while a bring-forward's take stands: the claim's <c>why</c> still names the reopen the take lost to.
    /// </summary>
    public bool Reopened => Claim == "lost" && Why == "source-reopened" && Forward is not { Stands: true };

    /// <summary>The take is this machine's by its claim: one a remote numbered, or one only here.</summary>
    public bool Here => Claim is "held" or "unconfirmed";
}

/// <summary>
/// The fresh take a person's bring-forward makes of a step this machine's take lost, as the claim's
/// answer names it: the take, when it is made, and where it stands in the claim's words.
/// </summary>
/// <param name="Standing"><c>held</c> once a remote numbered it, <c>unconfirmed</c> while it is only here, <c>lost</c> where it lost again.</param>
public sealed record ForwardTake(string Machine, long Sequence, DateTimeOffset At, string Standing)
{
    /// <summary>The take stands this machine's: confirmed, or unconfirmed offline, which goes on all the same.</summary>
    public bool Stands => Standing is "held" or "unconfirmed";
}

/// <summary>
/// One of this machine's session records as the goal's walk counts it: the adapter it ran on,
/// the account (null for the tool's own sign-in), when it opened, and whether it runs now. Read on loopback alone,
/// where the record names its account; a teammate's record is not this machine's.
/// </summary>
public sealed record SessionStarted(string Adapter, string? Profile, DateTimeOffset? Created, bool Running)
{
    /// <summary>The record's id, which <see cref="HarnessRoster.Release"/> reads to make a refused open's record not running.</summary>
    public string? Id { get; init; }

    /// <summary>
    /// Its record ended <c>choice-not-offered</c>: set from the end code where the look builds it, so the walk
    /// counts it neither running nor last started, and the refusing account is not read as just used.
    /// </summary>
    public bool NotOffered { get; init; }
}

/// <summary>
/// One fact a requirement names as its evidence, as the service answers it: exactly one of a
/// repository-relative <see cref="Path"/> the done's commit must hold, or a <see cref="Gate"/> the receiving repository
/// declares. The check stays the person's words; this is the part of it Daoris reads itself.
/// </summary>
public sealed record QuestEvidenceItem(string? Path, string? Gate = null)
{
    /// <summary>Its kind's word, as a verdict names it: <c>path</c> or <c>gate</c>.</summary>
    public string Kind => Path is not null ? "path" : "gate";

    /// <summary>What it names: the path, or the gate.</summary>
    public string Named => Path ?? Gate ?? "";
}

/// <summary>A submission by the machine and sequence that made it: what a verdict and a close name it by.</summary>
public sealed record QuestSubmissionRef(string Machine, long Sequence);

/// <summary>A quest as the service answers it — enough to decide on, and enough to compose a target from.</summary>
public sealed record QuestView(string Id, string From, string To, string Title, string Body, string Status)
{
    /// <summary>
    /// What a list calls it, as the service answers: its publisher's short title, or the name the service reads
    /// from its words. Null from an older host, which names it by its title.
    /// </summary>
    public string? Short { get; init; }

    /// <summary>
    /// The mark its publisher or the intake gives the work: <c>design</c> for design or decision work, which a role taking
    /// <c>design</c> takes. A fact of the work carrying no path or name; null from an older host, which starts on Build's order.
    /// </summary>
    public string? Weight { get; init; }

    /// <summary>
    /// The paths its publisher declares it touches: repository-relative globs a start compares with the repository's other
    /// open work (<see cref="PathOverlap"/>). Empty for none, and from an older host.
    /// </summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>The quest's name where one is shown: its short title, else its title.</summary>
    public string Name => Short is { Length: > 0 } named ? named : Title;

    /// <summary>The key a workflow's <c>request</c> node published it under, <c>&lt;run&gt;/&lt;node&gt;/&lt;n&gt;</c>; null for any other quest and from an older host.</summary>
    public string? Request { get; init; }

    /// <summary>Addresses the quest carries — a ticket, a page — handed to the session as given.</summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>Files the quest carries, each with where this machine keeps it, or null when it does not.</summary>
    public IReadOnlyList<QuestFileView> Attachments { get; init; } = [];

    /// <summary>What closing this done will publish next — the service's to do, never the driver's.</summary>
    public IReadOnlyList<QuestStepView> Then { get; init; } = [];

    /// <summary>The quest whose close published this one, when it is a step of a chain.</summary>
    public string? Parent { get; init; }

    /// <summary>
    /// The person's kind and workflow choices on a quest no ask published, made on its page before it started,
    /// oldest first, the latest standing; empty for none, and from an older host.
    /// </summary>
    public IReadOnlyList<AskWorkflowChoiceView> WorkflowChoices { get; init; } = [];

    /// <summary>The machine that holds the quest, as the list answers it; null where none is named.</summary>
    public string? Holder { get; init; }

    /// <summary>This machine's id where the list answers it as the page's; null off this machine or from an older host.</summary>
    public string? Here { get; init; }

    /// <summary>
    /// The question its taker waits on — another quest, asked of the repository that knows — or
    /// null. A taken quest carrying one is its taker's to resume once the question closes. Only a taken quest waits: a
    /// service keeps the field after the quest closes, and a start that read it would plan the owner's words to a done quest's
    /// session and send them down the open door, which refuses them. So a quest
    /// that is not taken names none here, to every reader. A taken quest keeps naming its question after the answer and after
    /// the resume it made: whether it still waits is the planner's <c>ResumedOnAnswer</c>, never this field alone.
    /// </summary>
    public string? Awaits
    {
        get => Status == "Taken" ? awaits : null;
        init => awaits = value;
    }

    private readonly string? awaits;

    /// <summary>
    /// The session whose connector published it, as the machine that ran it spells it, or null: a person's publish,
    /// or a chain's step. How a question a session asked joins that session's work (<see cref="AskWork"/>).
    /// </summary>
    public string? PublishedBy { get; init; }

    /// <summary>What its close said — a done's note or a decline's reason. The answer a waiting session resumes with.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// Its note's coded parts as the service answers them, or null from an older host; the terminal
    /// stays English, so a reader that words a close in the person's language takes them, as a session's do.
    /// </summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    /// <summary>
    /// The circle it is held in, as the service answers it, or the default where a host answers none: which
    /// workspace's pass carries an abandon's decline of it.
    /// </summary>
    public string Workspace { get; init; } = RemoteTarget.DefaultWorkspace;

    /// <summary>
    /// Whether the service would delete it: its own reading, the one the drawer's Delete is shown by.
    /// Absent is false — an older host offers none.
    /// </summary>
    public bool Deletable { get; init; }

    /// <summary>
    /// The lanes of <see cref="To"/> it addresses, as the service answers them; empty for a
    /// quest to the whole repository. Carried, and not yet planned on.
    /// </summary>
    public IReadOnlyList<string> Lanes { get; init; } = [];

    /// <summary>Whom it asks as a person reads it: `repository`, or `repository:lane+lane` (the service's `QuestAddress.Spell`).</summary>
    public string Address => Lanes.Count == 0 ? To : $"{To}:{string.Join('+', Lanes)}";

    /// <summary>
    /// What the person requires of it, each their words and its check, in the service's order — the
    /// numbers its done answers by. Empty for a quest that names none, and from an older host.
    /// </summary>
    public IReadOnlyList<QuestRequirementView> Requirements { get; init; } = [];

    /// <summary>
    /// Whether a departure holds it for the person's yes: closed done departing from what they required,
    /// not yet accepted. The service lists it among the open, so a quest waiting on it waits. Absent is false.
    /// </summary>
    public bool Held { get; init; }

    /// <summary>
    /// How its done answered each requirement, by number, as the service answers them: what an accept
    /// shows the person before their yes. Empty for a quest no done answered, and from an older host.
    /// </summary>
    public IReadOnlyList<QuestAnswerView> Answers { get; init; } = [];

    /// <summary>
    /// Why a held done waits, as the service answers <c>hold</c>: <c>departed</c>, <c>evidence-unread</c>,
    /// <c>evidence-missing</c> or, a set-up step's after both, <c>unreviewed</c> (<see cref="EvidenceCodes"/>).
    /// Null when nothing holds it, and from an older host, whose only hold is a departure.
    /// </summary>
    public string? Hold { get; init; }

    /// <summary>
    /// Whether its done waits for Daoris to find its evidence: a met answer names some and none is found
    /// yet, a departure beside it or not. What a session's end, the sweep and the terminal's check read. Absent is false.
    /// </summary>
    public bool AwaitsEvidence { get; init; }

    /// <summary>What Daoris last read of its evidence, or null while nothing is read.</summary>
    public EvidenceVerdict? Evidence { get; init; }

    /// <summary>
    /// When its status last moved, as the service answers it (<c>updated</c>). For a taken quest that waits on nothing it is
    /// when it is taken: a wait is the one other move a taken quest makes, and it leaves the quest waiting. Null
    /// from a host that answers none.
    /// </summary>
    public DateTimeOffset? Updated { get; init; }

    // The review's part of a quest; their element records are declared in Service/Domain.

    /// <summary>Its chain's review choice; null where it chooses none, and from an older host.</summary>
    public QuestReviewChoiceView? Review { get; init; }

    /// <summary>
    /// The environment it shows its chain's work in, which makes it a set-up step; null for every
    /// other quest, and from an older host.
    /// </summary>
    public string? SetUpIn { get; init; }

    /// <summary>The set-ups said on it, oldest first; empty where there are none, and from an older host.</summary>
    public IReadOnlyList<QuestSetUpView> SetUps { get; init; } = [];

    /// <summary>The person's verdicts on its review, oldest first; empty where none is given, and from an older host.</summary>
    public IReadOnlyList<QuestReviewVerdictView> Verdicts { get; init; } = [];

    /// <summary>
    /// The digest of the graph that closes it, bound by the driver at its take, or null: a quest no
    /// closure graph holds, and every quest an older host answers.
    /// </summary>
    public string? Closure { get; init; }

    /// <summary>
    /// Its taker's current submission: the agent's <c>ready</c>, its result while it stays Taken, by the machine and
    /// sequence a close and an evidence verdict name it by; null where there is none, and from an older host.
    /// </summary>
    public QuestSubmissionView? Submission { get; init; }

    /// <summary>
    /// The submission of its <see cref="Parent"/> it grew from, by its machine and sequence: what seals that
    /// submission while it stands. Null for a step a done published, every other quest, and from an older host.
    /// </summary>
    public QuestSubmissionRef? GrewFrom { get; init; }

    /// <summary>
    /// The submission it grew from, reopened by the person, while that stands: the step is withdrawn or held, and
    /// seals nothing. Null for every other quest, once a rebind names its new source, and from an older host.
    /// </summary>
    public QuestSubmissionRef? SourceReopened { get; init; }

    /// <summary>
    /// Whether the person's reopen of the submission it grew from holds it: open, it is withdrawn, and taken, it
    /// is held, until they go on with it on the new work or abandon it. Read from <see cref="SourceReopened"/>, the service's fact, on a
    /// live quest alone, since once closed its close is what stands. False from an older host, and on the owner's install.
    /// </summary>
    public bool HeldByReopen => SourceReopened is not null && Status is "Open" or "Taken";

    /// <summary>
    /// The person's last reopen of its own submission that no later submission answers: what tells a reopened
    /// source, with no <see cref="Submission"/>, from one whose taker has not yet said <c>ready</c>. Null once a later <c>ready</c>
    /// answers it, for every quest never reopened, and from an older host.
    /// </summary>
    public QuestReopenView? Reopened { get; init; }
}
