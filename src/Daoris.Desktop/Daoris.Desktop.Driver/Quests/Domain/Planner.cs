namespace Daoris.Driver;

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

    /// <summary>The machine that holds the quest, as the list answers it (SYNCMERGE1b1); null where none is named.</summary>
    public string? Holder { get; init; }

    /// <summary>This machine's id where the list answers it as the page's (SYNCMERGE1b1); null off this machine or from a host before it.</summary>
    public string? Here { get; init; }

    /// <summary>
    /// The question its taker waits on (D79) — another quest, asked of the repository that knows — or
    /// null. A taken quest carrying one is its taker's to resume once the question closes. Only a taken quest waits: a
    /// service keeps the field after the quest closes (AWAITSTALE1), and the owner's words of 2026-10-15 to a done quest's
    /// session were planned and then sent down the open door, which refused them, because the start read it. So a quest
    /// that is not taken names none here, to every reader.
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
/// How a done answered one requirement (DRIFT1d), as the service answers it: its number, and <paramref name="Met"/> with
/// how its check was met, or <paramref name="Departed"/> with the reason and <paramref name="Quote"/>, the person's words
/// it turns on.
/// </summary>
public sealed record QuestAnswerView(int Requirement, string? Met, string? Departed, string? Quote);

/// <summary>The session this machine last ran on a quest, and the tree it ran in (D79, D80).</summary>
/// <param name="Session">Its record's id.</param>
/// <param name="Tree">Where it ran — its own tree where the repository opted in, the root otherwise; null when unsaid.</param>
/// <param name="State">How its record ended — `failed` is a cut-off, which a later session carries on (D80).</param>
/// <param name="Note">What its record said about that ending — the words a session carrying on is told.</param>
/// <param name="Repository">Where it ran — whether a chain's next step can build on its tree (CHAIN2).</param>
/// <param name="Answer">The person's answer when it parked to ask them (STANDDOWN2) — what a carry-on is handed.</param>
/// <param name="Interrupted">
/// A `stopped` ending that was not the person's (D104): the orphan sweep's, or the driver's shutdown — a
/// cut-off, carried on like a `failed` one.
/// </param>
public sealed record PriorSession(
    string Session, string? Tree, string State = "", string? Note = null, string? Repository = null,
    string? Answer = null, bool Interrupted = false)
{
    /// <summary>
    /// Whether it ended because the person stopped it (D104): <c>stopped</c>, and not by the sweep or a shutdown. Such a stop
    /// holds its quest until the person releases it (SESSUX1b, D126 §3.3).
    /// </summary>
    public bool PersonStopped => string.Equals(State, "stopped", StringComparison.OrdinalIgnoreCase) && !Interrupted;

    /// <summary>
    /// The account it ran on, as its record names it, or null for the tool's own home (D49 §4): what a carry-on is
    /// compared with, to say whether it runs on another account (TOOL4f, D125 §3.5). Served on loopback alone, which is
    /// where this machine's driver reads its own records.
    /// </summary>
    public string? Profile { get; init; }

    /// <summary>Its record says an account's limit made its failure (TOOL4c): what a carry-on is told after one (TOOL4f).</summary>
    public bool Limit { get; init; }

    /// <summary>The adapter its record opened on (ANSWER1a, D131 §1): an answer resumes its conversation only on the same one.</summary>
    public string? Adapter { get; init; }

    /// <summary>
    /// The access mode an Ask Daoris record last ran under (HELPMODE1a2, D183 point 3), as the service answers it on loopback; null for
    /// every other record and from a host before the field, which <see cref="HelpModes.Recorded"/> reads as restricted.
    /// </summary>
    public string? HelpMode { get; init; }

    /// <summary>
    /// The access setup a driven record started under (AUTONOMY1a, D184 point 1), each pause with its value, as the service answers it on
    /// loopback: what a resume or a carry-on compares its own with, to say the change, its permission prompts
    /// (AUTONOMY1b, <see cref="DrivenAccess.ChangedSince"/>) and its go-aheads (AUTONOMY1c, <see cref="Continuations.GoAheadsChanged"/>).
    /// Null from a record before the setting and from a host before the field, which <see cref="DrivenAccess.Ran"/> reads as today's.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Access { get; init; }

    /// <summary>The harness version its record opened on: a resumed run says so where the version moved since (D131 §1).</summary>
    public string? HarnessVersion { get; init; }

    /// <summary>
    /// The commit its tree stood at when it opened (SURF6): a resumed run's evidence is counted from it, so the review's
    /// range is the whole session's (ANSWER1a, D131 §3). Served on loopback, beside the tree.
    /// </summary>
    public string? BaseCommit { get; init; }

    /// <summary>
    /// A park the person answered, still parked (D131 §1): its own record goes on, and its harness conversation resumes
    /// where it can. An answer a service from before ANSWER1b took has already ended the record, and is a carry-on.
    /// </summary>
    public bool AnsweredPark => string.Equals(State, "awaiting-person", StringComparison.OrdinalIgnoreCase) && Answer is not null;

    /// <summary>
    /// The person's words waiting on its record for it to go on with (MSG1a, D137 §2.4), in the order said; null where the
    /// host answers no <c>said</c>, one from before MSG1a, and empty where nothing waits.
    /// </summary>
    public IReadOnlyList<SaidWordView>? Said { get; init; }

    /// <summary>The ask an intake answers (D65 §1b), or null for every other record: an intake never goes on (D137 §2.2).</summary>
    public string? Ask { get; init; }

    /// <summary>
    /// <c>driven</c> or <c>chat</c>, as the record says: an ended chat the person wrote to goes on through the chat runner
    /// (MSG1c), never the planner, which plans a quest's sessions.
    /// </summary>
    public string Kind { get; init; } = "driven";

    /// <summary>
    /// Its note's lines, each by its code with its values (LANG1a, D142 point 2): what a line the driver adds to its note carries
    /// on. Null for a record from before parts, whose note is then carried whole.
    /// </summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    /// <summary>Its note as composed: its parts, or its English whole as one part from before them.</summary>
    public Noted AsNoted() => Noted.From(Note, NoteParts);

    /// <summary>Waiting on the person (D83): <c>awaiting-person</c>, the one state a record goes on from without leaving an ended one.</summary>
    public bool Parked => string.Equals(State, "awaiting-person", StringComparison.OrdinalIgnoreCase);

    /// <summary>A record that came down from the team, keyed <c>origin/id</c> (D47 §6): never this machine's to go on with.</summary>
    public bool Teammate => Session.Contains('/');

    /// <summary>
    /// When its record last moved, as the service answered it (<c>updated</c>): for an ended record, when it ended. What a
    /// take made here is compared with, to say whether it came after (CARRY2b). Null where unsaid.
    /// </summary>
    public DateTimeOffset? Updated { get; init; }

    /// <summary>
    /// When its record was made, as the service answered it (<c>created</c>): for a conversation that continues another by the
    /// usage curve (<see cref="FromWhy"/> <c>curve</c>), when it moved, which holds the next move for thirty minutes (SWITCH1c-ii,
    /// the switch design §2). Null where unsaid.
    /// </summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>
    /// Whether the person's words wait for it to go on with (MSG1b, D137 §2.2): its <c>said</c> holds any, parked or ended;
    /// from a host before <c>said</c>, an answered park is the one case that waits.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Never while it runs</b> (ANSWER2): a resumed run moves its record to working and takes the words off it only as
    /// it concludes, so for the whole run they are still on it. Read as waiting, a look took the record up a second time,
    /// whose move to working the ledger refused (working → working), and the driver failed the session it was resuming.
    /// </remarks>
    public bool WordsWaiting => !Running && (Said is { } said ? said.Count > 0 : AnsweredPark);

    /// <summary>
    /// Queued, starting or working: a run has the record (D46 §4), so the words on it are on their way to the session, and
    /// nothing waits for a look to take it up (ANSWER2). A park is not running: it waits on the person.
    /// </summary>
    public bool Running => State.ToLowerInvariant() is "queued" or "starting" or "working";

    /// <summary>
    /// The words it goes on with, in order: each of <see cref="Said"/>, or a host's answer from before <c>said</c>. A word a shell before
    /// this one attempted is settled as this one comes up, before any go-on (COWORK1c, <see cref="UncertainWords"/>); one this shell
    /// attempted and kept waiting after a failed turn goes again (TURNFAIL1).
    /// </summary>
    public IReadOnlyList<SaidWordView> Waiting =>
        Said is { } said ? said
        : Answer is { } answer ? [new SaidWordView(null, answer, DateTimeOffset.MinValue, [], false)]
        : [];

    /// <summary>
    /// The second opinions whose findings wait among its words (XAGENT1e, D155 point 7), each once, in the order handed: another
    /// agent's claims, which go on only in this session's own conversation and never to a new session as the person's words.
    /// </summary>
    public IReadOnlyList<string> Findings => [.. Waiting.Select(word => word.By).OfType<string>().Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The helper sessions whose answers wait among its words (MIXAGENT2a-ii), each once, in the order handed: another agent's
    /// claims, which go on only in this session's own conversation, as findings do.
    /// </summary>
    public IReadOnlyList<string> Helpers => [.. Waiting.Select(word => word.Helper).OfType<string>().Distinct(StringComparer.Ordinal)];

    /// <summary>The session this one answers, when it is a helper's record (MIXAGENT2a-i's <c>serves</c>); null for every other.</summary>
    public string? Serves { get; init; }

    /// <summary>The second opinion this one reads for, when it is a reviewer's record; null for every other.</summary>
    public string? Opinion { get; init; }

    /// <summary>
    /// The session this one continues (MIXAGENT1a-iii's <c>from</c>): a conversation handed over, or a quest's session carried on
    /// after a hand-over or onto another account after a limit; null for every other, and from a host from before.
    /// </summary>
    public string? From { get; init; }

    /// <summary>Why it continues <see cref="From"/>, by <see cref="HandOverWhy"/>'s codes; null with it.</summary>
    public string? FromWhy { get; init; }

    /// <summary>Who asked for it to continue <see cref="From"/>, by <see cref="HandOverBy"/>'s codes; null with it.</summary>
    public string? FromBy { get; init; }

    /// <summary>The workspace the record belongs to, as it names it: where a rule is resolved for it.</summary>
    public string? Workspace { get; init; }

    /// <summary>
    /// The session entry it is a part of (SESSONE1a, D167 point 2), <c>&lt;origin&gt;/&lt;repository&gt;</c>, as the service answers
    /// it: what a later quest of the same line goes into its tree by (SESSONE1d). Null from a host before the key.
    /// </summary>
    public string? Entry { get; init; }

    /// <summary>The quest it served, as its record names it; null for a conversation's. What a later part is told it worked on.</summary>
    public string? Quest { get; init; }
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
public sealed record QuestTake(string Claim, bool Took = false, string? Teammate = null, string? Why = null, ForwardTake? Forward = null)
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
/// Whose take holds a quest that sits <see cref="StartVerdict.TakenElsewhere"/> (CARRY2c), as facts: so the page says the
/// sentence in the reader's language from them, as it says a stop's from <see cref="Consideration.HeldBy"/>, and never by
/// reading the driver's English, which stays the terminal's and an older page's.
/// </summary>
/// <param name="Last">The session this machine last ran on the quest, which is not carried on over the take.</param>
/// <param name="Machine">
/// The machine that took it, as a teammate's record names it (the origin of its <c>origin/id</c>); null where no record of
/// theirs came, and for a take made here.
/// </param>
/// <param name="Session">That teammate's session, as <c>origin/id</c>, where a record names one; null otherwise.</param>
/// <param name="Here">Taken on this machine after <paramref name="Last"/> ended, which no record here marks: a chat's or work outside Daoris.</param>
/// <param name="Reopened">The take was lost to the person's reopen of the work the step grew from, not to another machine (FABRIC-CLOSE1c23).</param>
public sealed record TakenBy(string Last, string? Machine = null, string? Session = null, bool Here = false, bool Reopened = false);

/// <summary>
/// Why the planner sits a review set-up step it cannot show (<see cref="StartVerdict.CannotShow"/>), as a fact the page words
/// (CANNOTSHOW1a, D142): a code, the environment the step names, and the address where it is known. The sentence beside it
/// (<see cref="Consideration.Reason"/>) is unchanged.
/// </summary>
/// <param name="Code"><see cref="Unnamed"/>, <see cref="NoAddress"/> or <see cref="NoWindow"/>.</param>
/// <param name="Environment">The environment's name the step shows its work in.</param>
/// <param name="Address">Where a local environment shows, for <see cref="NoWindow"/>; null otherwise.</param>
public sealed record CannotShowFact(string Code, string Environment, string? Address = null)
{
    /// <summary>No review rule here names the environment.</summary>
    public const string Unnamed = "unnamed";

    /// <summary>A local environment that declares no address.</summary>
    public const string NoAddress = "no-address";

    /// <summary>A local environment and no window in this loop to show it in.</summary>
    public const string NoWindow = "no-window";
}

/// <summary>A sit: the fact and the sentence, both from the same branch of <see cref="ReviewSetUps.Sits"/>.</summary>
public sealed record CannotShowSit(CannotShowFact Fact, string Reason);

/// <summary>
/// One of the person's words waiting on a record (MSG1a, D137 §2.4), as the service answers it to this machine: its id,
/// which the record's events say again where the session took it, the words, when, its files' names, and whether it was
/// said after the record ended.
/// </summary>
/// <param name="Id">Its id on the record; null for an answer a host from before <c>said</c> kept, which has none.</param>
public sealed record SaidWordView(string? Id, string Text, DateTimeOffset At, IReadOnlyList<string> Files, bool Reopens)
{
    /// <summary>
    /// The second opinion whose findings these are (XAGENT1e, D155 point 7, D137 as it amends it): another agent's claims in
    /// Daoris's fixed words, never the person's. Null for the person's words, as every word kept before it was.
    /// </summary>
    public string? By { get; init; }

    /// <summary>
    /// The helper session whose answer this is (MIXAGENT2a-ii, D163 point 4): another agent's claim, never the person's words and
    /// no second opinion's findings. Null for every other word.
    /// </summary>
    public string? Helper { get; init; }

    /// <summary>
    /// Its author where that is not the person (COWORK1b, D188; the message-urgency design §9): <see cref="DaorisWords.From"/> for
    /// Daoris's own words, a reopen or a step's go-on. Null for every other word, and for a host's from before the field, whose
    /// words read as they did.
    /// </summary>
    public string? From { get; init; }

    /// <summary>Its producer's key (COWORK1b, the design §6): Daoris's fact's, read by <see cref="DaorisKeys"/>; null for a word kept with none.</summary>
    public string? Key { get; init; }

    /// <summary>
    /// When its sender asked it to reach a working session (COWORK1c, the design §9); null for a word kept with none, which reads as
    /// its old reach: the person's as urgent, any other as ordinary.
    /// </summary>
    public MessageLevel? Level { get; init; }

    /// <summary>
    /// When a dispatcher first wrote it to a harness (COWORK1c, the design §6): a word with one, still waiting when a shell comes up,
    /// may have reached the agent. Null for a word never attempted, and a host's from before the field.
    /// </summary>
    public DateTimeOffset? Attempt { get; init; }

    /// <summary>Whether these are Daoris's own words (COWORK1b): read from its mark, never its sentence.</summary>
    public bool Daoris => From == DaorisWords.From;

    /// <summary>Whether these are the person's words: no second opinion's, no helper's and not Daoris's.</summary>
    public bool Persons => By is null && Helper is null && From is null;
}

/// <summary>One step of a chain, as the service answered it.</summary>
public sealed record QuestStepView(string To, string Title, string Body)
{
    /// <summary>The environment the step shows its chain's work in, which makes it a set-up step (REVIEWENV1b); null otherwise.</summary>
    public string? SetUpIn { get; init; }
}

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

/// <summary>A registration as the service answered it. The root is present only from a local service.</summary>
/// <param name="Workspace">
/// The circle this repository is wired into on THIS machine (D48 §2) — the registry row, which is the
/// only honest source for it. The toolchain reads it to pick a workspace's credential profile
/// (D49 §4): a work account for the work circle, a personal one at home.
/// </param>
public sealed record RepoView(
    string Repository, bool Adopted, string? Root, string Workspace = RemoteTarget.DefaultWorkspace);

public static class Repositories
{
    /// <summary>
    /// What the registry holds, as one string — equal when the same repositories stand in the same
    /// circles at the same roots, whatever the order. The shell forwards a tick to the page when this
    /// changes (FG4), as it does for <see cref="Asks.Signature"/>: a folder imported from a terminal
    /// moves nothing else a tick reports, and the page said *no workspace yet* until a reload.
    /// </summary>
    public static string Signature(IEnumerable<RepoView> registry) =>
        string.Join("\n", registry
            .Select(repo => $"{repo.Repository}\t{repo.Adopted}\t{repo.Workspace}\t{repo.Root}")
            .OrderBy(line => line, StringComparer.Ordinal));
}

/// <summary>An ACTIVE session as the service answered it — closed ones never reach the planner.</summary>
/// <param name="State">
/// Where the record stands. The planner has never needed it — "is this repository busy" is the only
/// question it asks — but <see cref="AttentionWatch"/> does, because a park is a state change nothing
/// local performs and is therefore only visible by looking (SURF5b).
/// </param>
/// <param name="Note">What the session said about that state, where it said anything.</param>
public sealed record SessionView(
    string Id, string Repository, string State = "", string? Note = null)
{
    /// <summary>
    /// The ask an INTAKE answers (D65 §1b), or null for every other session. Its "repository" is
    /// <c>ask #id</c>, which no quest names — so the planner never finds it busy with anything.
    /// </summary>
    public string? Ask { get; init; }

    /// <summary>
    /// The tree it holds (D51), or null when unsaid — where a repository opens a tree per session, the
    /// tree is the lock the planner asks about (PAR1).
    /// </summary>
    public string? Tree { get; init; }

    /// <summary>
    /// The quest it serves, or null for a conversation's or an intake's. A session outlives the look that
    /// started it (DEV3), so the next look can find its quest still open, and must not start it again.
    /// </summary>
    public string? Quest { get; init; }

    /// <summary>
    /// The commit its tree stood at when it opened (SURF6), or null where unsaid: what a lost session's done is read as changed
    /// from when the sweep ends it (EVID1b, D144 §3). Answered on loopback alone, beside the tree.
    /// </summary>
    public string? BaseCommit { get; init; }

    /// <summary>Its note's lines by code (LANG1a), handed on wherever its note is; null for a record from before parts.</summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    /// <summary>
    /// The person's answer to its park (STANDDOWN2), or null: an answered park goes on at the driver's next look, so it waits
    /// on nobody (ANSWER1c), which Ask Daoris's room reads as the Sessions list does (ENTRY1f2). Answered on loopback alone.
    /// </summary>
    public string? Answer { get; init; }

    /// <summary>
    /// The workspace its record is filed under (D48), <c>default</c> where a host from before answers none: what Ask Daoris's
    /// start hands the running conversation of its own workspace by (ASKSCOPE1a).
    /// </summary>
    public string Workspace { get; init; } = RemoteTarget.DefaultWorkspace;
}

/// <summary>
/// One of this machine's session records as the goal's walk counts it (TOOL6b, D130 §4.2, §16.2): the adapter it ran on,
/// the account (null for the tool's own sign-in), when it was opened, and whether it runs now. Read on loopback alone,
/// where the record names its account (D47 §4); a teammate's record is not this machine's.
/// </summary>
public sealed record SessionStarted(string Adapter, string? Profile, DateTimeOffset? Created, bool Running);

/// <summary>
/// A start of this machine's loop that has not yet opened its record (PARALLEL1b, D181 point 3): reserved by the look that began it,
/// which no longer waits for it, until the record opens or the start comes to nothing. No snapshot the service answers holds it, so
/// the look hands it to the planner (<see cref="Snapshot.Starting"/>), which holds its quest, its place under the cap, the root of a
/// repository whose sessions run there, and the tree it goes back into, as it holds a live session's.
/// </summary>
/// <param name="Quest">The quest it serves, or null for an ask's intake.</param>
/// <param name="Ask">The ask an intake answers, or null for a quest's start.</param>
/// <param name="Repository">Where it runs: the quest's receiver, or an intake's <c>ask #id</c>, which no quest names.</param>
/// <param name="Tree">The tree it goes back into (a resume, a carry-on, a further part), or null for one that grows its own.</param>
public sealed record StartReservation(string? Quest, string? Ask, string Repository, string? Tree);

public static class ActiveSessions
{
    /// <summary>
    /// Which sessions are active and where each stands, as one string — equal when the same ones stand
    /// in the same states, whatever the order. The shell forwards a tick to the page when this changes
    /// (UX5 U13), as it does for <see cref="Asks.Signature"/>: a conversation started or ended from the
    /// main window moves nothing else a tick reports, so the other windows never heard of it.
    /// </summary>
    public static string Signature(IEnumerable<SessionView> sessions) =>
        string.Join("\n", sessions
            .Select(session => $"{session.Id}\t{session.State}")
            .OrderBy(line => line, StringComparer.Ordinal));
}

/// <summary>Everything a tick's decisions are made from, fetched once so the plan is coherent.</summary>
/// <param name="Strikes">
/// How many sessions have <b>failed</b> on each quest, by quest id — <b>derived</b> from the session
/// records this machine already wrote, never a tally the driver keeps (DRV6). Only `failed` counts, and a
/// stop that was not the person's (D104): a stand-down means somebody else got there first, a decline is
/// a real answer, and the person's stop was theirs. A failure an account's limit made is not counted
/// either (D125 §5.2): the account cools, and the quest waits for it. A quest nobody has failed is simply absent.
/// </param>
public sealed record Snapshot(
    IReadOnlyList<QuestView> Quests,
    IReadOnlyList<RepoView> Repositories,
    IReadOnlyList<SessionView> Active,
    IReadOnlyDictionary<string, int>? Strikes = null)
{
    public IReadOnlyDictionary<string, int> Strikes { get; init; } =
        Strikes ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The session THIS machine last ran on each quest, by quest id — derived from the same records as
    /// the strikes, never kept (D79). It is how a waiting quest is known to be this machine's to resume,
    /// and where: a taken quest no session here ran is somebody else's take.
    /// </summary>
    public IReadOnlyDictionary<string, PriorSession> LastRun { get; init; } =
        new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The newest record of THIS machine's on each quest, by quest id, where it stood down (SYNCMERGE1b): a take that lost, which
    /// <see cref="LastRun"/> never holds, and which the person's bring-forward carries on once (<see cref="Planner.BroughtForward"/>).
    /// Derived from the same records, never kept. Absent is none.
    /// </summary>
    public IReadOnlyDictionary<string, PriorSession> StoodDown { get; init; } =
        new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The record of this machine's that each taken quest's standing reopen names (FABRIC-CLOSE1c9, §E5), by quest id, where it
    /// is not the quest's <see cref="LastRun"/>: the session that said the reopened <c>ready</c>, which the reopen is told to, so
    /// its words go on in it though a later session ran. Derived from the same records, never kept. Absent is none.
    /// </summary>
    public IReadOnlyDictionary<string, PriorSession> NamedByReopen { get; init; } =
        new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every record this machine keeps, as the goal's walk counts them (TOOL6b): which account each ran on, when, and
    /// whether it runs now — derived from the same records, never kept. The roster reads it at each look.
    /// </summary>
    public IReadOnlyList<SessionStarted> Started { get; init; } = [];

    /// <summary>
    /// The quests of every paused work on this machine, each against the pause that holds it (PAUSE1b, D132 point 3): the
    /// look computes it from <c>driver.json</c>'s pauses and <see cref="AskWork"/> (<see cref="PausedWork.LookAsync"/>), as it
    /// derives <see cref="LastRun"/>, so the planner never holds a second copy of the work's rule. Absent is none.
    /// </summary>
    public IReadOnlyDictionary<string, PausedBy> Paused { get; init; } =
        new Dictionary<string, PausedBy>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The closed quests whose last session here has the person's words waiting (MSG1b, D137 §2.2): no open list holds them,
    /// and the session goes on all the same, its quest staying as it closed. Absent is none.
    /// </summary>
    public IReadOnlyList<QuestView> Closed { get; init; } = [];

    /// <summary>
    /// The marks of words a record could not go on with, by session (<see cref="GoOnMarks"/>): read at each look, so the
    /// planner leaves those words waiting instead of trying them again. Absent is none.
    /// </summary>
    public IReadOnlyDictionary<string, GoOnMark> Unable { get; init; } =
        new Dictionary<string, GoOnMark>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whose the take is on each taken quest a look would carry on (CARRY2b), by quest id: read only for those, since the
    /// ledger refuses a carry-on over a take that is not this machine's (CARRY2). Absent is unread, and the quest is carried
    /// on as before, for the ledger to judge.
    /// </summary>
    public IReadOnlyDictionary<string, QuestTake> Takes { get; init; } =
        new Dictionary<string, QuestTake>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The quests a second opinion sits, by quest id, each with its sentence (XAGENT1f; <see cref="StartVerdict.WaitsForOpinion"/>):
    /// the look reads them from the opinions owed and the gate's facts, as it reads <see cref="Paused"/>. Absent is none.
    /// </summary>
    public IReadOnlyDictionary<string, string> Opinions { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The quests held on another agent (MIXAGENT1a-ii, D163 point 1), by quest id, read from <see cref="HandedQuests"/> at each look.
    /// The stop of the session a store names is its own release: the planner reads it as released, so the quest is carried on with
    /// no strike and no write to driver.json's <c>released</c>. Absent is none.
    /// </summary>
    public IReadOnlyDictionary<string, HandedQuest> Handed { get; init; } =
        new Dictionary<string, HandedQuest>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every record's entry key by its id (SESSONE1d), this machine's and the teammates' mirrors (<c>origin/id</c>), as the service
    /// answered it: a quest's publisher names the line its sessions join (<see cref="EntryParts.KeyOf"/>). Absent is none answered.
    /// </summary>
    public IReadOnlyDictionary<string, string> Entries { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The starts of this machine's loop not yet open (PARALLEL1b, D181 point 3), as the look reserved them: the planner holds each
    /// as it holds a live session (<see cref="StartReservation"/>), and counts under the cap each one no active session already
    /// stands for. Absent is none, as for a caller that waits for each start to open.
    /// </summary>
    public IReadOnlyList<StartReservation> Starting { get; init; } = [];
}

public enum StartVerdict
{
    /// <summary>Start a session for this quest, now.</summary>
    Start,

    /// <summary>The receiver has not opted into driving on this machine — the person's choice (D46 §2).</summary>
    NotDrivable,

    /// <summary>The person holds this repository: drivable, and nothing new starts there until they resume it (D46 §3).</summary>
    Held,

    /// <summary>
    /// Nothing here could answer it: the receiver is not registered on this machine, or it has not
    /// adopted and either has no root or would be carried by the pipe door, which has no connector to
    /// hand it (D70). The name predates D70, and the reason says which.
    /// </summary>
    NotAdopted,

    /// <summary>No filesystem root is known — nowhere to spawn. `connect` from the repository fixes it.</summary>
    NoRoot,

    /// <summary>An active session (or an older quest this tick) holds the repository, or the quest itself (DEV3).</summary>
    RepositoryBusy,

    /// <summary>The concurrency cap is spent.</summary>
    AtCapacity,

    /// <summary>
    /// Enough sessions have failed on this quest that trying again is spending an account rather than
    /// making progress (DRV6). The person restarts it deliberately.
    /// </summary>
    Exhausted,

    /// <summary>
    /// Planned to start, and held at SPAWN by what only the spawn can see — a dirty tree, an absent
    /// or logged-out harness, a tree that would not grow, a trust flag never given. The reason is the
    /// hold's own sentence, which names the fix. Never produced by the planner: it is what a tick
    /// reports when what happened differs from what was decided, so "sitting" still says why.
    /// </summary>
    Blocked,

    /// <summary>
    /// Taken, and waiting on a question asked of another repository (D79). It resumes, in the same
    /// tree, once that quest is answered — nothing for the person to do.
    /// </summary>
    Waiting,

    /// <summary>
    /// Held by the person's stop (SESSUX1b, D126 §3.3): its last session here ended because the person stopped it, open or
    /// taken, and nothing starts it on this machine until they release that stop with *Try again*
    /// (<see cref="DriverConfig.Released"/>). A stop is not a strike (D58), and another machine may still take an open one.
    /// </summary>
    Stopped,

    /// <summary>
    /// In the work of an ask or a quest the person paused on this machine (PAUSE1b, D132 points 2–3): nothing of it starts
    /// here until *Resume*, which releases the stops the pause made. The reason the quest sits before every other, a
    /// person's stop included, so a release of a stop starts nothing while it holds. A pause is this machine's: another
    /// machine may still take an open one, and a taken one stays taken here, its take, tree and strikes kept.
    /// </summary>
    Paused,

    /// <summary>
    /// Taken, and the take is not this machine's (CARRY2b): another machine's, by the quest's log, or one made here after this
    /// machine's last session on it ended, by a chat or by work outside Daoris. The ledger refuses a carry-on over it (CARRY2),
    /// so the quest sits with the ledger's own sentence and nothing is asked. No strike: nothing ran.
    /// </summary>
    TakenElsewhere,

    /// <summary>
    /// A set-up step this machine cannot honestly start (REVIEWENV1c, D154 point 4; the review environment design §2.1): no review
    /// rule here names its environment, or a local one has no address or no window here to show the work in (a headless loop).
    /// The reason names the door that would let it start. No strike: nothing ran.
    /// </summary>
    CannotShow,

    /// <summary>
    /// Waits for a second opinion (XAGENT1f, D155 point 9; the second-agent design §7, §8.1): a set-up step while an agent is still
    /// at work on the opinion of the work it shows, or, under <c>steps</c>, a chain's next step while the opinion on the step
    /// before is unsettled. The reason says which, and what moves it. No strike: nothing ran.
    /// </summary>
    WaitsForOpinion,

    /// <summary>
    /// A chain's step whose source the person reopened (FABRIC-CLOSE1c2, §E5; <see cref="QuestView.HeldByReopen"/>): withdrawn while
    /// open, held while taken, and nothing starts it, carries it on or goes on with words written to its session until the person
    /// chooses to go on with it on the new work or to abandon it. Daoris never chooses. No strike: nothing ran.
    /// </summary>
    SourceReopened,
}

/// <param name="Quest">The quest considered.</param>
/// <param name="Verdict"><see cref="StartVerdict.Start"/>, or why not.</param>
/// <param name="Reason">The sentence a person reads. "Sitting" must always say why (D46 §3).</param>
/// <param name="Root">Where a start would spawn — carried so the executor never re-derives it.</param>
/// <param name="Workspace">The receiver's circle, carried for the same reason — and read by the toolchain.</param>
public sealed record Consideration(
    QuestView Quest, StartVerdict Verdict, string Reason, string? Root = null, string? Workspace = null)
{
    /// <summary>
    /// For a start that RESUMES a waiting quest (D79): the session that asked and waited, whose tree
    /// the resume carries on in. Null for every first start.
    /// </summary>
    public PriorSession? Resumes { get; init; }

    /// <summary>
    /// For a chain's next step in the same repository (CHAIN2): the step before's last run, whose
    /// branch this step's tree grows from, so it sees the unmerged work it builds on or checks.
    /// </summary>
    public PriorSession? BuildsOn { get; init; }

    /// <summary>
    /// For a first start that is a further part of a session (SESSONE1d, D167 point 4 as amended): its entry, the parts it already
    /// has here, and whether the start goes into the entry's tree or runs alongside. Null where no part of its entry is here, where
    /// no record names its entry, where the repository's sessions run in its checkout, and for a chain's step <see cref="BuildsOn"/>.
    /// </summary>
    public EntryParts? Entry { get; init; }

    /// <summary>
    /// The line of work on an ask a start is a part of (SESSONE2b, D167 point 7), whose channel its instruction is handed what
    /// waited on: set by the look for a start (<see cref="ChannelEntry.ForQuest"/>). Null for a quest on no ask.
    /// </summary>
    public ChannelEntry? Channel { get; init; }

    /// <summary>
    /// For a quest the person's stop holds (<see cref="StartVerdict.Stopped"/>): the session they stopped, which *Try
    /// again* names to release it (SESSUX1b). Null for every other verdict.
    /// </summary>
    public PriorSession? HeldBy { get; init; }

    /// <summary>
    /// For a quest a pause holds (<see cref="StartVerdict.Paused"/>): whose pause, an ask's or the quest's own, which
    /// *Resume* names (PAUSE1b), so the page says the sentence from facts as it says a stop's from <see cref="HeldBy"/>.
    /// Null for every other verdict.
    /// </summary>
    public PausedBy? PausedBy { get; init; }

    /// <summary>
    /// For a quest whose take is not this machine's (<see cref="StartVerdict.TakenElsewhere"/>): whose it is, the machine and
    /// its session where a record names them, and the session here not carried on (CARRY2c), so the page says the sentence
    /// from facts. Null for every other verdict.
    /// </summary>
    public TakenBy? TakenBy { get; init; }

    /// <summary>
    /// For a set-up step the planner sits (<see cref="StartVerdict.CannotShow"/>): the code and the environment and address its
    /// sentence is said from (CANNOTSHOW1a), so the page words it. Null for every other verdict.
    /// </summary>
    public CannotShowFact? CannotShow { get; init; }

    /// <summary>
    /// For a start the person's words make (MSG1b, D137 §2.2): <see cref="Resumes"/> is the record they wait on, parked or
    /// ended, which goes on in its own conversation where it can, and else hands them on.
    /// </summary>
    public bool GoesOn { get; init; }

    /// <summary>
    /// For a carry-on of a stood-down session whose lost take the person brought forward (SYNCMERGE1b, D178 point 3): the fresh take, so
    /// the start brings its tree up to date with the step's current source (<see cref="Planner.BringsForwardToSource"/>) and says the
    /// press. Null for every other start.
    /// </summary>
    public ForwardTake? BroughtForward { get; init; }

    /// <summary>
    /// For a start held at spawn (<see cref="StartVerdict.Blocked"/>): the accounts it passed not signed in (TOOL6g), whether
    /// it then waited on a cooling one or on none, so the page says each and its sign-in from facts. Null for every other.
    /// </summary>
    public SignedOutAccounts? SignedOut { get; init; }

    /// <summary>
    /// For a quest its failed sessions park (<see cref="StartVerdict.Exhausted"/>): how many have failed on it here in all, as
    /// the records count them (<see cref="Snapshot.Strikes"/>), before any mark (RETRY1b). *Try again* marks the quest at this
    /// count, so it starts and the limit's count parks it again; a mark at the limit left a quest parked a second time
    /// parked. Null for every other verdict.
    /// </summary>
    public int? Failures { get; init; }
}

public static class Considerations
{
    /// <summary>
    /// What a set of considerations SAYS, as one string — equal when the same quests carry the same
    /// verdicts and reasons, whatever the order. The shell forwards a tick to the page when this
    /// changes: a page that shows why each quest is sitting needs the change, and nothing else,
    /// because every tick it receives refetches four queries.
    /// </summary>
    public static string Signature(IEnumerable<Consideration> considered) =>
        string.Join("\n", considered
            .Select(c => $"{c.Quest.Id}\t{c.Verdict}\t{c.Reason}")
            .OrderBy(line => line, StringComparer.Ordinal));

    /// <summary>
    /// The plan as it turned out: every start that was held at spawn becomes
    /// <see cref="StartVerdict.Blocked"/> with the hold's own sentence, and everything else is the
    /// plan's word. The plan is not edited — what was decided and what happened are two records.
    /// </summary>
    /// <remarks>
    /// 🔴 Found on the deployed application: the quest it held every tick — trust flag never given —
    /// read <c>Start</c> in every consideration, because the hold lived only in the event line. The
    /// Overview, asked to say why each quest sits, had nothing to say under the one that mattered.
    /// </remarks>
    /// <param name="signedOut">The accounts each held start passed not signed in (TOOL6g), carried beside its hold.</param>
    public static IReadOnlyList<Consideration> Blocked(
        IReadOnlyList<Consideration> plan, IReadOnlyDictionary<string, string> heldAt,
        IReadOnlyDictionary<string, SignedOutAccounts>? signedOut = null) =>
        plan.Select(c =>
                c.Verdict == StartVerdict.Start && heldAt.TryGetValue(c.Quest.Id, out var why)
                    ? c with { Verdict = StartVerdict.Blocked, Reason = why, SignedOut = signedOut?.GetValueOrDefault(c.Quest.Id) }
                    : c)
            .ToList();
}

/// <summary>
/// The decision half of a tick: which open quests start, and why every other one is sitting.
/// </summary>
/// <remarks>
/// <para><b>Pure, deliberately.</b> Plan and apply are separate functions — the CLI's own convention,
/// held here for the same reason: a decision that can be asserted without spawning anything stays
/// tested, and a printed plan is a driver that can explain itself.</para>
///
/// <para><b>The plan never writes quest state and never outranks the ledger.</b> The service re-judges
/// every open through <c>SessionLedger</c>; this planner exists to avoid asking for work the ledger
/// would refuse, and to give every refusal a sentence before it happens.</para>
/// </remarks>
public static class Planner
{
    /// <param name="door">
    /// Which door this machine's starts ride (D53): the configured adapter's wire. It decides whether a
    /// repository that registered without adopting can be driven (D70). Defaults to the pipe, the door
    /// with the stricter requirement, so a caller that does not know cannot start more than it should.
    /// </param>
    /// <param name="window">
    /// Whether this loop carries Daoris's browser (D78), the window a local set-up step shows its chain's work in (REVIEWENV1c,
    /// design §2.1). Defaults to none, the headless loop's case, so a caller that does not know sits such a step rather than
    /// start one with nowhere to show it.
    /// </param>
    public static IReadOnlyList<Consideration> Plan(
        Snapshot snapshot, DriverConfig config, SessionWire door = SessionWire.Pipe, bool window = false)
    {
        var considerations = new List<Consideration>();
        // 🔴 Grouped, never keyed straight off the list: two active sessions in one repository is a
        // state D51 allows (a conversation in the checkout, another in a tree of its own), and a
        // dictionary built from the list threw on the second — every tick failed while both were open.
        // Either blocks the repository; the first is the one the reason names.
        var blockedBy = snapshot.Active
            .GroupBy(s => s.Repository, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
        var startedThisTick = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // The trees a start planned at this look goes into, each with its quest (SESSONE1d): a tree takes one session (D51), and a
        // further part of a session may now go into a tree a resume or a carry-on goes back into, so each holds it for the other.
        var treesThisTick = new List<(string Tree, string Quest)>();
        // PARALLEL1b: a start of this machine's still opening takes its place as a live session does, counted once where its record
        // has opened since and an active session already stands for its quest.
        var opening = Opening(snapshot);
        var slots = config.Cap - snapshot.Active.Count - opening.Count;

        // The service already orders open-oldest-first; keeping its order is what makes "oldest starts
        // first" one implementation rather than two that drift. 🔴 But a record the person wrote to goes first (MSG1b, D137
        // §2.2): a reopen goes before every start the driver planned itself, so the quests whose last session here has words
        // waiting are planned ahead of the rest, the open list's in its order and then the closed ones no list holds.
        var writtenTo = snapshot.Quests.Concat(snapshot.Closed)
            .Where(WrittenTo)
            .DistinctBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var quest in writtenTo.Concat(snapshot.Quests.Where(quest => !WrittenTo(quest))))
        {
            // 🔴 A step whose source the person reopened (FABRIC-CLOSE1c2, §E5): withdrawn while open and held while taken, until they
            // go on with it on the new work or abandon it. Before every other reason, a pause and the person's stop included, since
            // neither Resume nor Try again moves it: only their choice does, at the service. It spends no slot and no turn.
            if (quest.HeldByReopen && Plans(quest))
            {
                considerations.Add(Reopened(quest));
                continue;
            }

            // 🔴 A pause holds every quest of its work this planner would plan (PAUSE1b, D132 point 3), before every other reason,
            // the person's stop included: Resume is the one press that moves it, so a released stop starts nothing meanwhile.
            // First, too, so a paused quest spends no slot and no repository's turn, and the next starts where it would have.
            // Words written to its session wait for Resume too (D137 §2.2).
            if (snapshot.Paused.TryGetValue(quest.Id, out var pause) && Plans(quest))
            {
                considerations.Add(Paused(quest, pause));
                continue;
            }

            // The person wrote to its last session here, parked or ended (MSG1b, D137 §2.2): that record goes on with their
            // words. Before the person's stop, which the words release as Try again does: the same session goes on with them.
            // Or the session its reopen names was told it (FABRIC-CLOSE1c9): see WordsOn.
            if (WordsOn(quest) is { } written)
            {
                considerations.Add(GoOn(quest, written));
                continue;
            }

            // 🔴 A person's stop holds its quest, open or taken, until they release that stop (SESSUX1b, D126 §3.3). Before
            // everything else, since it is the one reason the quest sits that the person must act on: a taken one was never
            // looked at again and sat with no sentence, and an open one was planned again at the next look (M3).
            if (StopHolding(quest) is { } last)
            {
                considerations.Add(Stopped(quest, last));
                continue;
            }

            if (quest.Status == "Open")
            {
                var considered = Consider(quest);
                // A chain's next step in the repository its parent ran in builds on that run (CHAIN2); any other first start may be
                // a further part of a session, which goes into its tree or runs alongside (SESSONE1d).
                considerations.Add(considered.Verdict != StartVerdict.Start ? considered
                    : quest.Parent is { } parent
                      && snapshot.LastRun.TryGetValue(parent, out var before)
                      && string.Equals(before.Repository, quest.To, StringComparison.OrdinalIgnoreCase)
                        ? considered with { BuildsOn = before }
                        : Joining(considered));
            }
            else if (quest is { Status: "Taken", Awaits: { Length: > 0 } awaits }
                     && snapshot.LastRun.TryGetValue(quest.Id, out var prior))
            {
                considerations.Add(Resume(quest, awaits, prior));
            }
            else if (quest is { Status: "Taken", Awaits: null or "" } && CarriedOnFrom(quest) is { } cutOff)
            {
                // Whose the take is, before anything a carry-on would sit for (CARRY2b): the ledger refuses one over a take
                // that is not this machine's (CARRY2), whatever is opted in or held here.
                considerations.Add(TakenElsewhere(quest, cutOff) ?? CarryOn(quest, cutOff));
            }
        }

        return considerations;

        // The record a taken quest that waits on nothing is carried on from (D80): a newer stand-down whose lost take the person brought
        // forward (SYNCMERGE1b), first, since that is the work they chose and its tree; else its last session here, where that carries on.
        PriorSession? CarriedOnFrom(QuestView quest) =>
            BroughtForwardFrom(quest)
            ?? (snapshot.LastRun.TryGetValue(quest.Id, out var last) && CarriesOn(last) ? last : null);

        // 🔴 The stand-down a bring-forward carries on (SYNCMERGE1b, D178 point 3), or null: the quest's newest record here stood down, and
        // the take the claim reads stands by a press made after that record began. Once, by that alone, as the ledger opens it (SYNCMERGE1b0
        // point 1): the record carried on is newer than the press, so its own stand-down carries nothing. No claim read, or no forward, or one
        // that lost again, is none, and the quest reads as it did before.
        PriorSession? BroughtForwardFrom(QuestView quest) =>
            snapshot.StoodDown.TryGetValue(quest.Id, out var lost)
            && snapshot.Takes.TryGetValue(quest.Id, out var take)
            && BroughtForward(lost, take)
                ? lost
                : null;

        // Whether the loop below says anything of this quest: every open one, and a taken one whose last session here is
        // a stop, a cut-off, an answered park or the asker of a question (the branches below). A take this machine never ran,
        // and one whose session runs or waits on the person, is not planned, so a pause says nothing of it either.
        bool Plans(QuestView quest) =>
            quest.Status == "Open"
            || WrittenTo(quest)
            || (quest.Status == "Taken"
                && snapshot.LastRun.TryGetValue(quest.Id, out var run)
                && (quest.Awaits is { Length: > 0 }
                    || run.AnsweredPark
                    || string.Equals(run.State, "failed", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(run.State, "stopped", StringComparison.OrdinalIgnoreCase)
                    || run is { State: "completed", Answer: not null }
                    || StoppedByReopen(run)))
            // A stand-down the person's bring-forward carries on (SYNCMERGE1b), whatever ran here before it.
            || (quest.Status == "Taken" && BroughtForwardFrom(quest) is not null);

        // Held for the person's choice (FABRIC-CLOSE1c2): the sentence names the step it grew from and both choices. The service
        // refuses its take, its done and its ready meanwhile, so nothing here would move it anyway.
        static Consideration Reopened(QuestView quest) =>
            new(quest, StartVerdict.SourceReopened,
                $"Quest `#{quest.Id}` grew from the work of `#{quest.Parent}`, which you reopened: "
                + (quest.Status == "Open" ? "it is withdrawn, and nothing starts it" : "it is held, and nothing carries it on")
                + " until you go on with it on the new work or abandon it.");

        // Whether the quest was handed to another agent from this session (MIXAGENT1a-ii): the store names the session whose stop it is.
        bool HandedFrom(QuestView quest, PriorSession stop) =>
            snapshot.Handed.TryGetValue(quest.Id, out var handed) && string.Equals(handed.From, stop.Session, StringComparison.Ordinal);

        // The person's stop of the quest's last session here, where it still holds the quest (SESSUX1b), or null.
        PriorSession? StopHolding(QuestView quest) =>
            quest.Status is "Open" or "Taken"
            && snapshot.LastRun.TryGetValue(quest.Id, out var last)
            && last.PersonStopped
            && !config.Releases(quest.Id, last.Session)
            // The stop that handed the quest to another agent is its own release (MIXAGENT1a-ii): the person asked for it.
            && !HandedFrom(quest, last)
                ? last
                : null;

        // Whether the person's words wait on this quest's record here for it to go on with (see WordsOn).
        bool WrittenTo(QuestView quest) => WordsOn(quest) is not null;

        // The record the person's words wait on for this quest (MSG1b, D137 §2.2), parked or ended, and not all words a reopen
        // already found it could not take (MSG1a's open point): its last session here, or null. A taken quest waiting on a
        // question waits with them: D79's resume is its next start, and the words stay on the record they were said to. A closed
        // quest waits on nothing, whatever question the service still keeps for it (QuestView.Awaits reads none).
        // 🔴 FABRIC-CLOSE1c9: the session a taken quest's reopen names, where a later one is its last here, goes first with the words
        // it was told (its marks judged as the last's are); the last one's go on at a later look. Not while the person's stop of the
        // last session holds the quest: the reopen's words are Daoris's, and release no stop the person made (D173's note).
        PriorSession? WordsOn(QuestView quest)
        {
            if (quest.Awaits is { Length: > 0 }) return null;
            if (snapshot.NamedByReopen.TryGetValue(quest.Id, out var named) && Waits(named) && StopHolding(quest) is null) return named;
            return snapshot.LastRun.TryGetValue(quest.Id, out var written) && Waits(written) ? written : null;

            bool Waits(PriorSession record) =>
                record.WordsWaiting && !GoOnMarks.Judged(record, snapshot.Unable.GetValueOrDefault(record.Session));
        }

        // Words going on in the record they were said to (MSG1b, D137 §2.2), `continuing` it: a park is still an active record
        // holding its tree, its quest and its slot, which it is never busy with itself; an ended record holds nothing, so it
        // takes a slot, ahead of every start planned after it. The sentence names whose words: a park's answer, as ANSWER1a
        // said it, or what the person wrote to a session that had ended.
        Consideration GoOn(QuestView quest, PriorSession written)
        {
            var considered = Consider(quest, into: written, continuing: true);
            if (considered.Verdict != StartVerdict.Start) return considered;

            var waiting = written.Waiting;
            var said = waiting.Count == 1 ? waiting[0].Text : $"{waiting[0].Text} (and {waiting.Count - 1} more)";
            return considered with
            {
                Reason = written.Parked
                    ? $"carrying on in `{quest.To}` — you answered session `{written.Session}`: {written.Answer ?? said}"
                    : $"going on in `{quest.To}` — you wrote to session `{written.Session}`: {said}",
                Resumes = written,
                GoesOn = true,
            };
        }

        // Held by a pause (PAUSE1b, design §2.3): the sentence names the pause, what Resume does and the terminal's door. It
        // says paused, never held, since a hold is a repository's and stops nothing that runs (design §8.1).
        static Consideration Paused(QuestView quest, PausedBy pause)
        {
            var resumes = quest.Status == "Taken" ? "carries it on" : "starts it";
            return new(quest, StartVerdict.Paused, pause.Scope == WorkScope.Ask
                ? $"paused with ask `#{pause.Id}`; Resume {resumes} — `daoris-driver ask --resume {pause.Id}`."
                : $"you paused `#{pause.Id}`; Resume {resumes} — `daoris-driver quest resume {pause.Id}`.")
            {
                PausedBy = pause,
            };
        }

        // Held by the person's stop (SESSUX1b, D126 §3.3): the sentence says whose stop, what Try again does, and the
        // terminal's door, which names the session since `daoris driver` cannot see this verdict (D50).
        static Consideration Stopped(QuestView quest, PriorSession stop) =>
            new(quest, StartVerdict.Stopped,
                $"you stopped session `{stop.Session}`; Try again {(quest.Status == "Taken" ? "carries it on" : "starts it again")} — "
                + $"`daoris driver retry {quest.Id} --session {stop.Session}`.")
            {
                HeldBy = stop,
            };

        // A cut-off (D80): this machine's session took the quest and failed before closing it — timed
        // out, refused, crashed, or ended by the sweep or a shutdown (D104) — so the take is still here
        // and the work is in its tree. Carried on like a failed start is retried: the strikes count every
        // cut-off, and the third parks it — bar one an account's limit made, which is no strike: its carry-on is
        // planned as any is, and held at spawn while the account cools (TOOL4d, D125 §4).
        // A record the person's words wait on, an answered park among them, is planned by GoOn above.
        // A stand-down the person brought forward (SYNCMERGE1b) is carried on the same way, its sentence naming the press and, offline,
        // that the fresh take is not settled (D178 point 8).
        Consideration CarryOn(QuestView quest, PriorSession cutOff)
        {
            var considered = Consider(quest, into: cutOff);
            var forward = string.Equals(cutOff.State, "stood-down", StringComparison.OrdinalIgnoreCase)
                ? snapshot.Takes.GetValueOrDefault(quest.Id)?.Forward
                : null;
            return considered.Verdict == StartVerdict.Start
                ? considered with
                {
                    Reason = forward is not null
                        ? $"carrying on in `{quest.To}` — you brought the work of session `{cutOff.Session}` forward after its take lost"
                          + (forward.Standing == "held"
                              ? ", and the remote confirmed the fresh take."
                              : "; the fresh take is unconfirmed until a sync reaches the remote, and may still lose there.")
                        : cutOff.Answer is { } answer
                        ? $"carrying on in `{quest.To}` — you answered session `{cutOff.Session}`: {answer}"
                        : StoppedByReopen(cutOff)
                            ? $"carrying on in `{quest.To}` — you went on with it on the new work of `#{quest.Parent}`, after session "
                              + $"`{cutOff.Session}` stopped where that work was reopened."
                        : cutOff.PersonStopped && snapshot.Handed.TryGetValue(quest.Id, out var handed) && HandedFrom(quest, cutOff)
                            ? $"carrying on in `{quest.To}` — session `{cutOff.Session}` was handed over to {handed.Agent}."
                        : cutOff.PersonStopped
                            ? $"carrying on in `{quest.To}` — you stopped session `{cutOff.Session}`, and released it."
                            : $"carrying on in `{quest.To}` — session `{cutOff.Session}` was cut off: "
                              + (cutOff.Note is { Length: > 0 } note ? note : "it ended before closing the quest."),
                    Resumes = cutOff,
                    BroughtForward = forward,
                }
                : considered;
        }

        // A take that is not this machine's (CARRY2b), as the ledger judges it (CARRY2) and in its words: another machine's by
        // the claim, named by a teammate's record where one came, or one made here after the session ended that no record
        // here marks, a chat's or work outside Daoris. An unmarked take made while the session ran is still its own, as the
        // ledger reads it: the HTTP door marks none. Null where the take is this machine's, or was not read. Whose it is rides
        // beside the sentence as facts (CARRY2c), from the same reading, so the page's words and the driver's never disagree.
        Consideration? TakenElsewhere(QuestView quest, PriorSession cutOff)
        {
            if (!snapshot.Takes.TryGetValue(quest.Id, out var take)) return null;

            var over = $"the take is theirs, so session `{cutOff.Session}` is not carried on over it.";
            if (take.Reopened)
                return new(quest, StartVerdict.TakenElsewhere,
                    $"Quest `#{quest.Id}` was lost to your reopen of the work it grew from: the take is not this machine's, so session `{cutOff.Session}` is not carried on over it.")
                {
                    TakenBy = new TakenBy(cutOff.Session, Reopened: true),
                };

            if (!take.Here)
            {
                var machine = take.Teammate?.Split('/')[0];
                return new(quest, StartVerdict.TakenElsewhere, take.Teammate is { } teammate
                    ? $"Quest `#{quest.Id}` is taken on `{machine}`, by session `{teammate}`: {over}"
                    : $"Quest `#{quest.Id}` is taken on another machine: {over}")
                {
                    TakenBy = new TakenBy(cutOff.Session, machine, take.Teammate),
                };
            }

            // A take made after it ended by the person's bring-forward of it is the press's (SYNCMERGE1b), never a chat's.
            return !take.Took && quest.Updated is { } taken && cutOff.Updated is { } ended && taken > ended && !BroughtForward(cutOff, take)
                ? new(quest, StartVerdict.TakenElsewhere,
                    $"Quest `#{quest.Id}` was taken here after session `{cutOff.Session}` ended, by a chat or by work outside Daoris: {over}")
                {
                    TakenBy = new TakenBy(cutOff.Session, Here: true),
                }
                : null;
        }

        // 🔴 A further part of a session (SESSONE1d, D167 point 4 as amended): a new quest of an entry with a part here goes into the
        // tree its newest part held when nothing live holds it (D51), and otherwise starts at once alongside, in a tree of its own, as
        // a further part; nothing waits (EntryBusy is not built). Its entry is read from the records (EntryParts.KeyOf), never
        // re-derived. Different entries in one repository stay side by side (PAR1), and a repository whose sessions run in its
        // checkout keeps one at a time (D51): the entry changes only its grouping there.
        Consideration Joining(Consideration start)
        {
            var quest = start.Quest;
            if (!config.OpensOwnTree(quest.To) || EntryParts.KeyOf(quest, snapshot) is not { } key) return start;
            var parts = EntryParts.Earlier(key, quest, snapshot);
            if (parts.Count == 0) return start;

            var entry = new EntryParts(key, parts);
            if (parts[0] is not { Tree: { Length: > 0 } tree } newest) return start with { Entry = entry };

            if (snapshot.Active.FirstOrDefault(s => SameTree(s.Tree, tree)) is { } holder)
            {
                return start with
                {
                    Entry = entry with { HeldBy = holder.Id },
                    Reason = $"starting in `{quest.To}`, alongside session `{holder.Id}`, which is at work in its session's tree — "
                             + "a further part, in a tree of its own.",
                };
            }

            if (treesThisTick.FirstOrDefault(claim => SameTree(claim.Tree, tree)) is { Quest: { } ahead })
            {
                return start with
                {
                    Entry = entry,
                    Reason = $"starting in `{quest.To}`, alongside quest `#{ahead}`, which goes into its session's tree at this look — "
                             + "a further part, in a tree of its own.",
                };
            }

            treesThisTick.Add((tree, quest.Id));
            return start with
            {
                Entry = entry with { Into = newest },
                Reason = $"starting in `{quest.To}`, in the tree session `{newest.Session}` worked in — a further part of its session.",
            };
        }

        // A waiting quest (D79): the open list holds open and taken quests only, so a question still in
        // it is unanswered, and one absent from it has closed. A resume is a start in every other way —
        // the person's hold, the strikes, busy and the cap all still stand between it and a spawn.
        Consideration Resume(QuestView quest, string awaits, PriorSession prior)
        {
            var question = snapshot.Quests.FirstOrDefault(q =>
                string.Equals(q.Id, awaits, StringComparison.OrdinalIgnoreCase));
            // A question closed done departing from what the person required stays on the open list, held for their yes
            // (DRIFT1d, D133 §4), and what waits on it waits with it: the sentence names whose move it is, and its door.
            // One held for its evidence (EVID1b, D144 §6) is not the person's while the session that made the done runs, since
            // its end reads the evidence; after it, the sentence names the cause and both doors, the check and the yes.
            if (question is { Held: true })
            {
                return new(quest, StartVerdict.Waiting, HeldQuestion(question));
            }

            if (question is not null)
            {
                return new(quest, StartVerdict.Waiting,
                    $"waits on `#{question.Id}`, asked of `{question.To}` — it resumes, in the same tree, "
                    + "once that is answered.");
            }

            var considered = Consider(quest, into: prior);
            return considered.Verdict == StartVerdict.Start
                ? considered with { Reason = $"resuming in `{quest.To}` — `#{awaits}` is answered.", Resumes = prior }
                : considered;
        }

        // Why a held question waits, and whose move it is (DRIFT1d; EVID1b, D144 §6). A host before evidence answers no cause,
        // and its one hold was a departure. The check names a commit where no session here ended on the question, since a done
        // no driven end read must be read at a commit the person names (D144 §3).
        string HeldQuestion(QuestView question)
        {
            var yes = $"once you accept the done as it stands, your yes: `daoris-driver quest accept {question.Id}`.";
            var named = snapshot.LastRun.ContainsKey(question.Id) ? "" : " --commit <sha>";
            return question.Hold switch
            {
                EvidenceCodes.Unread when snapshot.Active.FirstOrDefault(s => string.Equals(s.Quest, question.Id, StringComparison.OrdinalIgnoreCase))
                    is { } running =>
                    $"waits on `#{question.Id}`, which `{question.To}` closed done: its evidence is read when session `{running.Id}` "
                    + "ends — it resumes, in the same tree, once that evidence is found.",
                EvidenceCodes.Unread =>
                    $"waits on `#{question.Id}`, which `{question.To}` closed done, and its evidence is not read yet — it resumes, in "
                    + $"the same tree, once the evidence is found, `daoris-driver quest check {question.Id}{named}`, or {yes}",
                EvidenceCodes.MissingHold =>
                    $"waits on `#{question.Id}`, which `{question.To}` closed done without the evidence it names — it resumes, in the "
                    + $"same tree, once a later commit holds it, `daoris-driver quest check {question.Id} --commit <sha>`, or {yes}",
                // A set-up step waits for the person's review (REVIEWENV1c, D154 point 9), which a yes does not give.
                EvidenceCodes.Unreviewed =>
                    $"waits on `#{question.Id}`, which `{question.To}` closed done: it waits for your review"
                    + (question.SetUpIn is { } environment ? $" in `{environment}`" : "")
                    + $" — it resumes, in the same tree, once you say it is reviewed, `daoris-driver quest review {question.Id} reviewed`, "
                    + $"or skip the review, `daoris-driver quest review {question.Id} skip`.",
                _ =>
                    $"waits on `#{question.Id}`, which `{question.To}` closed done departing from what you required — it "
                    + $"resumes, in the same tree, once you accept that, your yes: `daoris-driver quest accept {question.Id}`.",
            };
        }

        // `into`: the earlier session whose tree a resume or a carry-on goes back into — the one tree a
        // live session there would hold (PAR1). Null for a first start, which grows a tree of its own.
        // `continuing`: `into` is the record the person's words wait on, which goes on itself (ANSWER1a; MSG1b), so it holds
        // nothing against itself.
        Consideration Consider(QuestView quest, PriorSession? into = null, bool continuing = false)
        {
            bool Itself(SessionView session) => continuing && string.Equals(session.Id, into?.Session, StringComparison.OrdinalIgnoreCase);

            var repo = snapshot.Repositories.FirstOrDefault(r =>
                string.Equals(r.Repository, quest.To, StringComparison.OrdinalIgnoreCase));

            if (repo is null)
            {
                return new(quest, StartVerdict.NotAdopted,
                    $"`{quest.To}` is not registered on this machine, so there is nowhere to start it.");
            }

            // Registered is drivable over the protocol door; adopted is disciplined (D70). The pipe
            // door's session reaches the knowledge tools only through the repository's own `.mcp.json`,
            // which adoption writes and the driver may never write for it (D32) — so there it sits.
            if (!repo.Adopted)
            {
                if (repo.Root is null)
                {
                    return new(quest, StartVerdict.NotAdopted,
                        $"`{quest.To}` has not adopted Daoris and no root is known for it on this machine, "
                        + "so there is nothing to start and nothing there to see the quest.");
                }

                if (door != SessionWire.Acp)
                {
                    return new(quest, StartVerdict.NotAdopted,
                        $"`{quest.To}` has not adopted Daoris, so a session there would have no connector to "
                        + "take the quest with — only the protocol door hands it one. Drive with an ACP "
                        + "agent, or adopt it.");
                }
            }

            if (!config.Drivable.Contains(quest.To, StringComparer.OrdinalIgnoreCase))
            {
                return new(quest, StartVerdict.NotDrivable,
                    $"`{quest.To}` has not been opted into driving on this machine.");
            }

            if (config.Holds.Contains(quest.To, StringComparer.OrdinalIgnoreCase))
            {
                return new(quest, StartVerdict.Held, $"`{quest.To}` is held by the person.");
            }

            // A set-up step it cannot honestly start (REVIEWENV1c, design §2.1): no rule here names its environment, or a local one
            // has nowhere to show it. It sits saying why, and no session starts with nothing to show the work in.
            if (ReviewSetUps.Sits(config, quest, repo.Workspace, window) is { } sits)
            {
                return new(quest, StartVerdict.CannotShow, sits.Reason) { CannotShow = sits.Fact };
            }

            // The second opinion (XAGENT1f, the second-agent design §7, §8.1): a set-up step sits while an agent is still at work on
            // the opinion of the work it shows, and under `steps` a chain's next step sits while the opinion on the step before is
            // unsettled. The look reads each from its facts; the planner holds no copy of the gate. No strike: nothing ran.
            if (snapshot.Opinions.TryGetValue(quest.Id, out var opinion))
            {
                return new(quest, StartVerdict.WaitsForOpinion, opinion);
            }

            if (repo.Root is null)
            {
                return new(quest, StartVerdict.NoRoot,
                    $"no root is known for `{quest.To}` — run `daoris connect` from that repository.");
            }

            // 🔴 Before busy and before capacity, because those are waits and this is a stop: a
            // parked quest must not read as "queued" in a surface that renders the reason.
            var strikes = snapshot.Strikes.TryGetValue(quest.Id, out var failures)
                ? failures - config.ForgivenAt(quest.Id)
                : 0;
            // The person's words to its session are their Try again (MSG1b, D137 §2.2): a failure they write to is forgiven as
            // its record leaves `failed`, and a quest the strikes parked goes on with them, as a retry would start it.
            if (config.Strikes > 0 && strikes >= config.Strikes && !(continuing && into is { WordsWaiting: true }))
            {
                return new(quest, StartVerdict.Exhausted,
                    $"{strikes} session(s) have failed on `#{quest.Id}` without landing anything — "
                    + $"parked, because trying again spends an account rather than making progress. "
                    + $"`daoris driver retry {quest.Id}` starts it again once you know why.")
                {
                    // What *Try again* marks (RETRY1b): the records' count, not the count past the mark this sentence says.
                    Failures = failures,
                };
            }

            // 🔴 A session outlives the look that started it (DEV3), so its quest can still be open while it
            // works — before it takes it, or when it never will. That session holds the quest: a second one
            // would double its work, and in a tree of its own nothing below would stop it.
            if (snapshot.Active.FirstOrDefault(s => string.Equals(s.Quest, quest.Id, StringComparison.OrdinalIgnoreCase) && !Itself(s))
                is { } serving)
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"session `{serving.Id}` is already working on `#{quest.Id}`.");
            }

            // PARALLEL1b: a start of this machine's that has not yet opened its record holds the quest as its session will: the look
            // that began it no longer waits for it, so a wake meanwhile plans the quest again, and would start it twice.
            if (opening.Any(start => string.Equals(start.Quest, quest.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"`#{quest.Id}` is starting on this machine already: its session opens once its tree and account are ready.");
            }

            // 🔴 The TREE is the lock (D51), and where every session here opens its own there is no
            // reason to run one at a time (PAR1). What still holds is the one tree a resume or a carry-on goes back into.
            if (config.OpensOwnTree(quest.To))
            {
                if (into is { Tree: { Length: > 0 } tree }
                    && snapshot.Active.FirstOrDefault(s => SameTree(s.Tree, tree) && !Itself(s)) is { } holder)
                {
                    return new(quest, StartVerdict.RepositoryBusy,
                        $"session `{holder.Id}` is active in the tree `#{quest.Id}` goes back into — one session per tree.");
                }

                // PARALLEL1b: a start still opening holds the tree it goes back into, as its session will.
                if (into is { Tree: { Length: > 0 } reserved }
                    && opening.FirstOrDefault(start => SameTree(start.Tree, reserved)) is { Quest: { } going })
                {
                    return new(quest, StartVerdict.RepositoryBusy,
                        $"queued behind quest `#{going}`, which is starting in the tree `#{quest.Id}` goes back into — one session per tree.");
                }

                // A further part planned into that tree at this look holds it as a live session would (SESSONE1d).
                if (into is { Tree: { Length: > 0 } claimed } && treesThisTick.FirstOrDefault(claim => SameTree(claim.Tree, claimed)) is { Quest: { } ahead })
                {
                    return new(quest, StartVerdict.RepositoryBusy,
                        $"queued behind quest `#{ahead}`, which goes into the tree `#{quest.Id}` goes back into at this look — one session per tree.");
                }
            }
            else if ((continuing
                         ? snapshot.Active.FirstOrDefault(s =>
                             string.Equals(s.Repository, quest.To, StringComparison.OrdinalIgnoreCase) && !Itself(s))?.Id
                         : blockedBy.GetValueOrDefault(quest.To)) is { } session)
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"session `{session}` is active in `{quest.To}` — one session per repository, whose root "
                    + "is its one tree. Sessions there run side by side once they open trees of their own.");
            }
            else if (startedThisTick.TryGetValue(quest.To, out var ahead))
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"queued behind quest `#{ahead}` in `{quest.To}` — oldest first, one at a time.");
            }
            // PARALLEL1b: a start still opening in the root holds it, as its session will once it opens.
            else if (opening.FirstOrDefault(start => string.Equals(start.Repository, quest.To, StringComparison.OrdinalIgnoreCase)
                                                     && !string.Equals(start.Quest, quest.Id, StringComparison.OrdinalIgnoreCase))
                     is { Quest: { } before })
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"queued behind quest `#{before}`, which is starting in `{quest.To}` — one session per repository, whose root is its one tree.");
            }

            // An answered park already holds its slot among the active (ANSWER1a): going on, it starts no second session. An
            // ended record holds none (MSG1b), so going on takes one, as a start does; it was planned first, so it is first to.
            if (!(continuing && snapshot.Active.Any(Itself)))
            {
                if (slots <= 0)
                {
                    return new(quest, StartVerdict.AtCapacity,
                        $"the concurrency cap ({config.Cap}) is spent — it frees as sessions finish.");
                }

                slots--;
            }

            startedThisTick[quest.To] = quest.Id;
            if (config.OpensOwnTree(quest.To) && into is { Tree: { Length: > 0 } back }) treesThisTick.Add((back, quest.Id));
            return new(quest, StartVerdict.Start, $"starting in `{quest.To}`.", repo.Root, repo.Workspace);
        }
    }

    /// <summary>
    /// Whether a taken quest whose last session here ended so is carried on from it (D80): what the planner plans a carry-on
    /// for, and what a look reads whose the take is for (CARRY2b), one rule for both.
    /// </summary>
    internal static bool CarriesOn(PriorSession last) =>
        string.Equals(last.State, "failed", StringComparison.OrdinalIgnoreCase)
        // A stop that was not the person's — the sweep's or a shutdown's (D104).
        || last is { State: "stopped", Interrupted: true }
        // The person's own stop, once they released it (SESSUX1b): a held one never reaches the planner's carry-on.
        || last.PersonStopped
        // The person answered a session that parked to ask them (STANDDOWN2).
        || last is { State: "completed", Answer: not null }
        // The person's reopen of the work it grew from stopped it (FABRIC-CLOSE1c2): carried on once they go on, held until then.
        || StoppedByReopen(last);

    /// <summary>
    /// Whether <paramref name="last"/> ended because the person reopened the work its quest grew from (FABRIC-CLOSE1c2,
    /// <see cref="NoteCodes.EndedSourceReopened"/>): <c>completed</c>, by its note's code, never its English.
    /// </summary>
    internal static bool StoppedByReopen(PriorSession last) =>
        string.Equals(last.State, "completed", StringComparison.OrdinalIgnoreCase)
        && last.NoteParts?.Any(part => part.Code == NoteCodes.EndedSourceReopened.Code) == true;

    /// <summary>
    /// The step before, named where a session ends stopped by the person's reopen of the work its quest grew from (what
    /// <see cref="Observation.Conclude"/> ends as <see cref="NoteCodes.EndedSourceReopened"/>): its quest held by the reopen as it ends
    /// (FABRIC-CLOSE1c2), or (FABRIC-CLOSE1c16) gone on with since <paramref name="started"/>, the reopen and the go-on both made while it ran,
    /// so the step reads unheld, taken, grown from another submission than at its start, with no result of its own since. Either way the
    /// person's go-on carries it on with its branch brought up to date with the source's new submission (<see cref="ReopenTelling.BringsToSource"/>);
    /// read as an ordinary end, its record ended after the go-on, which neither that carry-on nor the telling reads. Null for every other end.
    /// </summary>
    /// <param name="started">The quest as the start that ran the session read it.</param>
    /// <param name="after">The quest as it stands at the session's end; null where the service no longer answers it.</param>
    internal static string? ReopenedFrom(QuestView started, QuestView? after) =>
        after is { HeldByReopen: true, Parent: { Length: > 0 } held } ? held
        : after is { Status: "Taken", HeldByReopen: false, Submission: null, GrewFrom: { } now, Parent: { Length: > 0 } source }
          && !Equals(started.GrewFrom, now)
            ? source
            : null;

    /// <summary>
    /// Whether the person went on with <paramref name="step"/> on its source's new submission after <paramref name="last"/>, its last
    /// session here, had ended normally (FABRIC-CLOSE1c13, §E5): what <see cref="ReopenTelling"/> tells that session, so the go-on with
    /// Daoris's words goes on in it. The service's fact, read from the step and the record alone: the step is taken and unheld, grew from
    /// a submission, and has no result of its own, which the go-on cleared (FABRIC-CLOSE1c10), nor a reopen of one (FABRIC-CLOSE1c8 tells
    /// that); the record is this machine's, parked with nothing said to it or completed, and the step moved after it did, by the rebound.
    /// </summary>
    /// <remarks>
    /// A session the reopen stopped is carried on instead (<see cref="StoppedByReopen"/>), and a cut-off, a stop, a stand-down, a record
    /// with words waiting and a running one read as before. <b>Once</b>, with nothing kept: the record that goes on moves after the step,
    /// and a <c>ready</c> gives the step a result again. A step whose session ran across the go-on is not read here: its record ended after
    /// the step moved, as the reopen stopped it (<see cref="ReopenedFrom"/>, FABRIC-CLOSE1c16), so it is carried on instead.
    /// </remarks>
    internal static bool GoneOnSince(QuestView step, PriorSession last) =>
        step is { Status: "Taken", Awaits: null or "", HeldByReopen: false, Submission: null, Reopened: null, GrewFrom: not null, Parent: { Length: > 0 } }
        && !last.Teammate && !last.Running && !last.WordsWaiting && last.Answer is null
        && (last.Parked || (string.Equals(last.State, "completed", StringComparison.OrdinalIgnoreCase) && !StoppedByReopen(last)))
        && last.Updated is { } ended && step.Updated is { } moved && moved > ended;

    /// <summary>
    /// The commit a step the person went on with is brought up to date with (FABRIC-CLOSE1c5, §E5): its source's current submission's,
    /// where that is the one the step grew from now. Null where the source is not read, names another submission or none, or names no
    /// commit (a host before the field, or a submission whose commit no session's end read: FABRIC-CLOSE1c6 posts it at the end of the
    /// session that said the <c>ready</c>, evidence asked or not).
    /// </summary>
    internal static string? SourceCommit(QuestView step, QuestView? source) =>
        step.GrewFrom is { } grew && source is { Submission: { Commit: { } commit } current } && source.Id == step.Parent
        && current.Machine == grew.Machine && current.Sequence == grew.Sequence
            ? commit
            : null;

    /// <summary>
    /// Whether a taken quest's take is not this machine's, as <c>TakenElsewhere</c> reads it (CARRY2b) and the hand-over's judge
    /// asks it before a quest's session goes on with another agent (MIXAGENT1a-ii): another's by the claim, or made here after
    /// <paramref name="last"/> ended by a chat or work outside Daoris that no record marks.
    /// </summary>
    internal static bool TakenAway(QuestView quest, PriorSession last, QuestTake take) =>
        !take.Here
        || (!take.Took && quest.Updated is { } taken && last.Updated is { } ended && taken > ended && !BroughtForward(last, take));

    /// <summary>
    /// Whether <paramref name="lost"/>, a record of this machine's that stood down, is carried on by the person's bring-forward of its lost
    /// take (SYNCMERGE1b, D178 point 3), as the ledger opens it (SYNCMERGE1b0 point 1): <paramref name="take"/> stands this machine's by a
    /// press made after the record was created. Its creation, not its end, so a stand-down written after the press still goes on. Once:
    /// the session carried on is opened after the press, so its own stand-down is no carry-on.
    /// </summary>
    /// <remarks>A record with no creation said is none: nothing could tell the press after it.</remarks>
    internal static bool BroughtForward(PriorSession lost, QuestTake take) =>
        string.Equals(lost.State, "stood-down", StringComparison.OrdinalIgnoreCase)
        && !lost.Teammate
        && take is { Here: true, Forward: { Stands: true } forward }
        && lost.Created is { } created
        && forward.At > created;

    /// <summary>
    /// Whether a bring-forward's carry-on (SYNCMERGE1b, <see cref="Consideration.BroughtForward"/>) is first brought up to date with its
    /// step's current source, then the line, as the go-on's is (FABRIC-CLOSE1c5's <c>GoOnInSyncAsync</c>): a step that grew from a source,
    /// whose bring-forward took it as it stands. Any other carry-on of it syncs with the line alone. The sibling of
    /// <see cref="ReopenTelling.BringsToSource"/>, which keys on the person's reopen.
    /// </summary>
    internal static bool BringsForwardToSource(QuestView step, Consideration start) =>
        start.BroughtForward is not null && step is { GrewFrom: not null, Parent: { Length: > 0 } };

    /// <summary>
    /// The starts still opening a snapshot carries that take a place of their own (PARALLEL1b): each but one whose quest an active
    /// session already stands for, its record opened since, which the active sessions count. What the planner and a look's intakes
    /// count under the cap.
    /// </summary>
    internal static IReadOnlyList<StartReservation> Opening(Snapshot snapshot) =>
        [.. snapshot.Starting.Where(start => start.Quest is null
                                             || !snapshot.Active.Any(s => string.Equals(s.Quest, start.Quest, StringComparison.OrdinalIgnoreCase)))];

    /// <summary>Two tree paths are one tree — separators and case aside, as Windows sees them.</summary>
    internal static bool SameTree(string? a, string? b) =>
        a is { Length: > 0 } && b is { Length: > 0 }
        && string.Equals(
            a.Replace('\\', '/').TrimEnd('/'), b.Replace('\\', '/').TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
}
