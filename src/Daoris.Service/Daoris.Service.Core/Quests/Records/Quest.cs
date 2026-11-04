namespace Daoris.Knowledge;

/// <param name="Id">Short, stable handle — quotable in a commit message.</param>
/// <param name="From">The repository that asked.</param>
/// <param name="To">The repository being asked. Must be addressable (D70), or there is nobody to answer.</param>
/// <param name="Title">One line: what is wanted.</param>
/// <param name="Body">Why, and the evidence — never the prescribed change.</param>
/// <param name="Status">Where it is.</param>
/// <param name="Note">The reason, when declined or finished.</param>
/// <param name="Filed">When it was published.</param>
/// <param name="Updated">When its status last moved.</param>
/// <param name="Workspace">
/// The circle this quest belongs to (D48). Both sides share it by construction — the exchange refuses
/// a publish that would cross — so one field, not two, and a scoped list can trust it.
/// </param>
public sealed record Quest(
    string Id,
    string From,
    string To,
    string Title,
    string Body,
    QuestStatus Status,
    string? Note,
    DateTimeOffset Filed,
    DateTimeOffset Updated,
    string Workspace = Workspaces.Default)
{
    /// <summary>
    /// The short title its publisher gave (SESSUX1j): the few words that tell it apart in a list, at most
    /// <see cref="QuestTitles.MaxShort"/> characters on one line, as written. Null where none was given, which is every
    /// quest from before the field; <see cref="Name"/> is then read from its own words.
    /// </summary>
    public string? Short { get; init; }

    /// <summary>
    /// The mark its publisher or its intake put on the work (FOUNDATION1x), <see cref="QuestWeights"/>: <c>design</c> starts it on the
    /// role taking design. Null for an unmarked quest, which is every quest from before the field, and starts on Build's order.
    /// </summary>
    public string? Weight { get; init; }

    /// <summary>
    /// The paths its publisher declared it touches (INTEGRATE1l), <see cref="QuestPaths"/>: globs a start compares with the repository's
    /// other open work. Empty for a quest declaring none, which is every quest from before the field.
    /// </summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>
    /// The key of the workflow request that published it (INTEGRATE1p), <see cref="QuestRequests"/>: <c>&lt;run&gt;/&lt;node&gt;/&lt;n&gt;</c>, part
    /// of its id. Null for every quest no request node published, which is every quest from before the field.
    /// </summary>
    public string? Request { get; init; }

    /// <summary>
    /// Its note's parts beside <see cref="Note"/> (QUESTCLOSE1b1, D142 point 2), as <see cref="Knowledge.NoteParts"/> keeps
    /// them: Daoris's lead by its code, worded in the reader's language, then someone's words as written. Set by the move that
    /// wrote the note, and cleared by one that wrote a note without them. Null for every quest from before, and for a note
    /// that is all its closer's words.
    /// </summary>
    public string? NoteParts { get; init; }

    /// <summary>
    /// What a list calls it (SESSUX1j): its publisher's short title, else a name read from its own words
    /// (<see cref="QuestTitles.Derive"/>), never written into the record.
    /// </summary>
    public string Name => Short ?? QuestTitles.Derive(Title, Body);

    /// <summary>
    /// Moves that lost to another machine's (D68 §5), in the order they were recorded — kept, not
    /// dropped, until a person acts. Empty for a quest nobody raced.
    /// </summary>
    public IReadOnlyList<QuestConflict> Conflicts { get; init; } = [];

    /// <summary>
    /// Addresses the quest carries — a ticket, a page, a document (D65 §2). They travel with it
    /// everywhere the quest does, and are given in the order the asker gave them.
    /// </summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>
    /// Files the quest carries, BY NAME (D65 §2). The bytes are machine-local — kept under the home of
    /// the machine that published them, the transcript's boundary (D47 §4) — so a record read anywhere
    /// else says a file exists without being able to open it.
    /// </summary>
    public IReadOnlyList<QuestAttachment> Attachments { get; init; } = [];

    /// <summary>
    /// What happens when this closes done (D65 §4): the next step is published as part of the close,
    /// asked on behalf of the same asker and carrying the rest. Empty for an ordinary quest. A chain is
    /// data on the quest — no engine runs it; the driver drives each step as it drives any quest.
    /// </summary>
    public IReadOnlyList<QuestStep> Then { get; init; } = [];

    /// <summary>The quest whose close published this one, when it is a step of a chain.</summary>
    public string? Parent { get; init; }

    /// <summary>
    /// The question its taker asked another repository and waits on (D79) — null when it waits on
    /// nothing. It stays set once the question is answered, while the quest is taken: the driver reads the
    /// answer from the question itself when it resumes the quest. A close (done or declined) clears it, the
    /// Waited operation staying in the history (AWAITSTALE1).
    /// </summary>
    public string? Awaits { get; init; }

    /// <summary>
    /// The session whose connector published it, when a session did (SESS1) — null for a person's
    /// publish, and for a chain step, which its parent's close published. Recorded at the source so a
    /// session's view can say what it caused without guessing from the times.
    /// </summary>
    public string? PublishedBy { get; init; }

    /// <summary>
    /// The lanes of <see cref="To"/> it addresses (D115 §2.2), as that repository declares them, sorted
    /// — `repository:lane+lane` at every door. Empty for a quest to the whole repository, which is every
    /// quest from before lanes. <see cref="To"/> stays the repository, so everything keyed on one goes
    /// on reading one.
    /// </summary>
    public IReadOnlyList<string> Lanes { get; init; } = [];

    /// <summary>
    /// What the person requires of it (DRIFT1c, D133 §3), each in their own words with the check that
    /// proves it, in the order given. Empty for a quest that names none, which is every quest from before.
    /// </summary>
    public IReadOnlyList<QuestRequirement> Requirements { get; init; } = [];

    /// <summary>
    /// How its close answered each requirement (DRIFT1d, D133 §4), in the order given: met, or departed with the
    /// reason and the person's words it turns on. Empty for a quest not closed done, and for one with none.
    /// </summary>
    public IReadOnlyList<QuestAnswer> Answers { get; init; } = [];

    /// <summary>When the person accepted its done as it stands (DRIFT1d, EVID1a), or null while nobody did.</summary>
    public DateTimeOffset? Accepted { get; init; }

    /// <summary>
    /// What Daoris last read of its evidence (EVID1a, D144 §3): the commit and each item's code. Null while nothing
    /// was read, which is every quest whose requirements name none.
    /// </summary>
    public QuestEvidenceVerdict? Evidence { get; init; }

    /// <summary>
    /// The evidence its done waits on (EVID1a, D144 §3): each item of each requirement its done answered met, by the
    /// requirement's number. A departure waits on none of its requirement's evidence, since the work departed from it. In a
    /// closure graph a current submission's answers wait on it the same way (FABRIC-CLOSE1a, §E5).
    /// </summary>
    public IReadOnlyList<(int Requirement, QuestEvidence Item)> EvidenceWanted =>
        !Judged
            ? []
            : [.. Answers
                .Where(answer => !answer.IsDeparture && answer.Requirement >= 1 && answer.Requirement <= Requirements.Count)
                .SelectMany(answer => Requirements[answer.Requirement - 1].Evidence.Select(item => (answer.Requirement, item)))
                // Once each: a publish here keeps a path once, and a requirement another build wrote twice is one fact.
                .Distinct()];

    /// <summary>
    /// Whether its done waits for Daoris to find its evidence (EVID1a, D144 §3): a met answer names some, none was
    /// found yet, and the person has not accepted the done as it stands.
    /// </summary>
    public bool AwaitsEvidence =>
        Judged && Accepted is null && Evidence is not { Found: true } && EvidenceWanted.Count > 0;

    /// <summary>
    /// The chain's review choice (REVIEWENV1b, D154 point 3): <c>off</c>, <c>on</c> or an environment's name, set at publish
    /// and inherited by each step. Null for a quest whose chain chose nothing, which is every quest from before it.
    /// </summary>
    public QuestReview? Review { get; init; }

    /// <summary>
    /// The environment a set-up step shows its chain's work in (REVIEWENV1b, D154 point 4): what makes it one. Null for
    /// every other quest, and for every quest from before it.
    /// </summary>
    public string? SetUpIn { get; init; }

    /// <summary>
    /// The set-ups said on a set-up step (REVIEWENV1b, D154 point 9), oldest first, each with its commit. Empty for any other
    /// quest, and for a set-up step nothing has shown yet.
    /// </summary>
    public IReadOnlyList<QuestSetUp> SetUps { get; init; } = [];

    /// <summary>The person's verdicts on its review (REVIEWENV1b, D154 point 8), oldest first. Empty for a quest none was given on.</summary>
    public IReadOnlyList<QuestReviewVerdict> Verdicts { get; init; } = [];

    /// <summary>
    /// The digest of the graph that closes it (FABRIC-CLOSE1a, the fabric events design §E5), bound by its taker's driver at
    /// its first start; null for a quest no closure graph holds, which is every quest from before. While it is set its taker
    /// says <c>ready</c>, and only the graph's close door, or the person's own done, writes its Done.
    /// </summary>
    public string? Closure { get; init; }

    /// <summary>
    /// Its taker's latest submission in a closure graph (FABRIC-CLOSE1a, §E5), the one the holds and the close judge; null while
    /// none was made. Its answers are <see cref="Answers"/>, as a done's are. It stays once the quest is Done, as the work closed.
    /// </summary>
    public QuestSubmission? Submission { get; init; }

    /// <summary>
    /// The commit its current submission was read at (FABRIC-CLOSE1c5, §E5): the evidence verdict read for that submission, by its
    /// machine and sequence (FABRIC-CLOSE1e), which is the one place a submission's commit is known. Null with no submission, before
    /// its evidence is read, where its requirements name none, and once a later <c>ready</c> replaced it. Read, never stored: the
    /// submission's column and its operation are as they were.
    /// </summary>
    public string? SubmissionCommit =>
        Submission is { } current && Evidence is { Submission: { } asked } read
        && asked.Machine == current.Machine && asked.Sequence == current.Sequence
            ? read.Commit
            : null;

    /// <summary>
    /// The submission of its <see cref="Parent"/> it grew from (FABRIC-CLOSE1c, §E5's <c>publishedBy: { quest, submission }</c>), by
    /// the machine and sequence that name it: set where a submission published the step, moved by a <see cref="QuestOperationKind.Rebound"/>.
    /// Null for a step a done published, for every other quest, and for every step from before. <see cref="PublishedBy"/> stays the
    /// publishing session's.
    /// </summary>
    public QuestOperationRef? GrewFrom { get; init; }

    /// <summary>
    /// The submission it grew from, reopened by the person (FABRIC-CLOSE1c, §E5), while that stands: an open step is withdrawn and
    /// a taken one is held, each for the person (<see cref="QuestHold.SourceReopened"/>). Null for every other quest, and once a
    /// <see cref="QuestOperationKind.Rebound"/> names the submission it grows from now, which clears the step's own submission and its
    /// answers, yes and verdict, made on the work reopened (FABRIC-CLOSE1c10).
    /// </summary>
    public QuestOperationRef? SourceReopened { get; init; }

    /// <summary>
    /// The person's last reopen of its own submission that no later submission answered (FABRIC-CLOSE1c4, §E5): the reopen by its
    /// machine and sequence, the submission it reopened, their words and when. Read from the log by the replay, as every machine
    /// reads it, so a mirror answers the same; the next <c>ready</c> answers it, at whatever commit, and it reads null again. Null
    /// for every quest never reopened, and for every step (its reopen is <see cref="SourceReopened"/>).
    /// </summary>
    public QuestReopen? Reopened { get; init; }

    /// <summary>
    /// The person's kind and workflow choices on it before it started (WORKFLOW1e2, the workflow design §4.1 row 1), oldest first, each
    /// with when; the latest stands, and one naming neither is a clear. Empty for a quest none was made on, which is every quest from
    /// before it and every quest an ask published, whose ask's choice is the task's.
    /// </summary>
    public IReadOnlyList<QuestWorkflowChoice> WorkflowChoices { get; init; } = [];

    /// <summary>
    /// Whether its result is judged (FABRIC-CLOSE1a, §E5): closed done, or taken with a current submission. The holds, the
    /// evidence and the review hang on it, so in a closure graph they hang on the submission exactly as they hang on a done.
    /// </summary>
    internal bool Judged => Status == QuestStatus.Done || (Status == QuestStatus.Taken && Submission is not null);

    /// <summary>
    /// Why it waits for the person, or null when nothing holds it (DRIFT1d, EVID1a; D133 §4, D144 §6): a departure,
    /// which only their yes lets go, comes first; then evidence nobody read, or evidence read and not found. A set-up step's
    /// review comes after both (REVIEWENV1b, D154 point 9), so their order is unchanged, and only the person's verdict
    /// lets it go: their yes accepts a departure or its evidence, never a review. In a closure graph each holds a current
    /// submission as it holds a done (FABRIC-CLOSE1a, §E5).
    /// </summary>
    public QuestHold? Hold =>
        // A step whose source the person reopened (FABRIC-CLOSE1c, §E5): held while it is live, whatever else it carries, since
        // what it stands on is gone; once closed, its close is what stands.
        SourceReopened is not null && Status is QuestStatus.Open or QuestStatus.Taken ? QuestHold.SourceReopened
        : !Judged ? null
        : Accepted is null && Answers.Any(answer => answer.IsDeparture) ? QuestHold.Departed
        : AwaitsEvidence ? Evidence is null ? QuestHold.EvidenceUnread : QuestHold.EvidenceMissing
        : QuestReviewing.Waits(this) ? QuestHold.Unreviewed
        : null;

    /// <summary>
    /// Whether it waits (DRIFT1d, D133 §4; EVID1a, D144 §6; REVIEWENV1b): closed done, held for one of <see cref="Hold"/>'s
    /// causes. What follows it, a chain's next step or a quest waiting on it, waits with it.
    /// </summary>
    public bool Held => Hold is not null;
}
