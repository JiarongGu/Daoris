using Microsoft.Data.Sqlite;
using static Daoris.Knowledge.QuestRecordCodec;

namespace Daoris.Knowledge;

/// <summary>Where a quest is in its life. Four states, because anything finer is status for its own sake.</summary>
public enum QuestStatus
{
    /// <summary>Published, nobody has taken it.</summary>
    Open,

    /// <summary>A repository's agent has accepted it.</summary>
    Taken,

    /// <summary>Finished.</summary>
    Done,

    /// <summary>Turned down — a real answer, and the reason is the part the asker can act on.</summary>
    Declined,
}

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

/// <summary>
/// How a done answers one requirement (DRIFT1d, D133 §4): <see cref="Met"/>, saying how its check was met, or
/// <see cref="Departed"/>, with the reason and the person's own words the departure turns on, quoted. One shape at
/// every door, in the log and on the wire; the exchange judges that exactly one of the two is said.
/// </summary>
/// <param name="Requirement">Which requirement it answers: its number, from 1, in the order the quest lists them.</param>
/// <param name="Met">How its check was met — null for a departure.</param>
/// <param name="Departed">Why the work departed from it — null for one met.</param>
/// <param name="Quote">A departure's: the person's own words it turns on, quoted from what they said on the ask.</param>
public sealed record QuestAnswer(int Requirement, string? Met, string? Departed = null, string? Quote = null)
{
    /// <summary>Whether the work departed from its requirement rather than met it.</summary>
    public bool IsDeparture => Departed is not null;
}

/// <summary>
/// One thing the person requires of a quest (DRIFT1c, D133 §3): their own words, quoted, and the check that
/// proves the work meets them.
/// </summary>
/// <param name="Quote">The person's words, verbatim, found in what they said on the ask the quest is asked by.</param>
/// <param name="Check">How to tell the work meets them — written by whoever composed the quest.</param>
public sealed record QuestRequirement(string Quote, string Check)
{
    /// <summary>
    /// The facts its check turns on that Daoris reads itself (EVID1a, D144 point 1): at most
    /// <see cref="QuestEvidence.MaxItems"/>, each a path the done's commit must hold or a gate. Empty for a requirement
    /// that names none, which closes exactly as before: on the session's word.
    /// </summary>
    public IReadOnlyList<QuestEvidence> Evidence { get; init; } = [];

    // Compared by what it names, not by which list holds it: a requirement read back from the log is the one published.
    public bool Equals(QuestRequirement? other) =>
        other is not null && Quote == other.Quote && Check == other.Check && Evidence.SequenceEqual(other.Evidence);

    public override int GetHashCode() => HashCode.Combine(Quote, Check, Evidence.Count);

    /// <summary>
    /// Whether <paramref name="quote"/> stands in <paramref name="words"/> verbatim, whitespace and case aside
    /// (DRIFT1c): every run of white space reads as one space and the ends are trimmed, so a line break or a
    /// doubled space the person typed is not a different word, and neither is a capital. Nothing else is
    /// forgiven — a reworded quote is a paraphrase, and a paraphrase is what drifted.
    /// </summary>
    public static bool QuotedIn(string quote, string words)
    {
        var folded = Folded(quote);
        return folded.Length > 0 && Folded(words).Contains(folded, StringComparison.Ordinal);
    }

    private static string Folded(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}

/// <summary>One step of a chain: whom to ask next, and what (D65 §4).</summary>
/// <param name="To">The repository asked — addressable from the chain's asker, judged when composed.</param>
/// <param name="Title">One line. <c>{parent}</c> becomes the id of the quest this step follows.</param>
/// <param name="Body">Why, and the evidence. <c>{parent}</c> is expanded here too.</param>
public sealed record QuestStep(string To, string Title, string Body)
{
    /// <summary>
    /// The environment this step shows the chain's work in, which makes it a set-up step (REVIEWENV1b, D154 point 4; the
    /// design's §2.1 calls it the step's <c>review</c>, a word the chain's choice already has). Null for every other step.
    /// </summary>
    public string? SetUpIn { get; init; }
}

/// <summary>A file a quest carries, as its record names it — never where it lies on a disk.</summary>
/// <param name="Name">The file's own name, made safe to keep: what a reader and a session see.</param>
/// <param name="Sha256">Its content's hash, lowercase hex — the file's identity, and how it is found.</param>
/// <param name="Bytes">Its size.</param>
public sealed record QuestAttachment(string Name, string Sha256, long Bytes)
{
    /// <summary>Its one shape in JSON, the same in a store's column and on the wire.</summary>
    public void Write(System.Text.Json.Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("name", Name);
        writer.WriteString("sha256", Sha256);
        writer.WriteNumber("bytes", Bytes);
        writer.WriteEndObject();
    }

    /// <summary>
    /// One read back out of a store's own column, which only this service writes. The wire's reader
    /// judges what it reads instead (<see cref="QuestWire"/>), because a remote's JSON is not ours.
    /// </summary>
    public static QuestAttachment Stored(System.Text.Json.JsonElement item) => new(
        item.GetProperty("name").GetString() ?? "",
        item.GetProperty("sha256").GetString() ?? "",
        item.GetProperty("bytes").GetInt64());
}

/// <param name="Quest">The quest as it now stands — null when no such id exists.</param>
/// <param name="Moved">Whether THIS call moved it. False with a non-null quest is a refused move.</param>
/// <param name="FollowUp">The chain's next step, published by this close — null when there was none.</param>
public sealed record QuestMove(Quest? Quest, bool Moved, Quest? FollowUp = null)
{
    /// <summary>
    /// Refused because this machine's take on the quest lost, and the take that beat it stands (WAITCLAIM2): the move or
    /// wait would be made on a claim this machine never held. Said apart from the table's refusals, so the answer can say why.
    /// </summary>
    public bool ClaimLost { get; init; }

    /// <summary>
    /// With <see cref="ClaimLost"/>: the take lost to the person's reopen of the work its step grew from (FABRIC-CLOSE1c18,
    /// <see cref="QuestLog.LostToTheReopen"/>), read inside the write. On an open step a take that lost otherwise lost with the work it
    /// was made on, the step standing on another submission since (FABRIC-CLOSE1c24), and on a taken quest to another machine's take;
    /// the refusal's words say which.
    /// </summary>
    public bool LostToTheReopen { get; init; }

    /// <summary>
    /// Refused because the quest's graph closes it (FABRIC-CLOSE1a, §E5): an agent's done on a quest bound to a closure graph,
    /// which says <c>ready</c> instead, so no done slips past the graph.
    /// </summary>
    public bool ClosedByGraph { get; init; }

    /// <summary>
    /// The successor a sealed submission published (FABRIC-CLOSE1a, §E5), naming why a later <c>ready</c> was refused: it
    /// stands on the submission it grew from. Null on every other move.
    /// </summary>
    public string? SealedBy { get; init; }

    /// <summary>
    /// Refused because the work the quest grew from was reopened by the person (FABRIC-CLOSE1c, §E5): an agent's take, done or
    /// <c>ready</c> on a step withdrawn or held for the person's choice.
    /// </summary>
    public bool SourceReopened { get; init; }

    /// <summary>
    /// What a take made of the closure graph named for it (FABRIC-CLOSE1o, §E5's cutover): <see cref="QuestTakeBind.None"/> on every
    /// other move and on a take no graph was named for.
    /// </summary>
    public QuestTakeBind TakeBind { get; init; }
}

/// <summary>
/// What a take made of the closure graph the driver named for it (FABRIC-CLOSE1o, §E5's cutover), judged and consumed in the take's
/// own write: bound, or why not. Only <see cref="Bound"/> appends the bind; every other reading leaves the take unbound, closing by
/// its done as any quest.
/// </summary>
public enum QuestTakeBind
{
    /// <summary>No graph was named for this take: the owner's install, and every take before a closure graph.</summary>
    None,

    /// <summary>Bound in the take's own write.</summary>
    Bound,

    /// <summary>
    /// A pending row whose owner cannot be shown, written by a build before FABRIC-CLOSE1w in the table such builds write, whatever
    /// owner it names, since an older build's upsert there keeps another's owner: discarded.
    /// </summary>
    Unowned,

    /// <summary>Named for another session than the one taking.</summary>
    OtherSession,

    /// <summary>Named for a publication of this id since replaced, by a delete and the same ask again.</summary>
    Replaced,

    /// <summary>Named longer ago than <see cref="QuestStore.TakeBindLife"/> before the take.</summary>
    Expired,

    /// <summary>
    /// The quest's circle syncs through a remote now whose handshake does not say every machine of it knows a bound quest (FABRIC-CLOSE1i):
    /// enabled, or a machine heard undeclared, since the graph was named.
    /// </summary>
    Remote,
}

/// <summary>
/// Who takes a quest (FABRIC-CLOSE1o): the session the authenticated intake names, null for a door that names none, and how this
/// machine's remotes read at the moment of the take, by circle. What a take needs to judge the bind named for it, inside its write.
/// </summary>
/// <param name="Session">The taking session, from its connector's intake (<c>DAORIS_SESSION_ID</c>); null where no session is named.</param>
/// <param name="Remote">
/// Whether a circle syncs through a remote, by the map sync reads, that has not said every machine of it knows a bound quest
/// (FABRIC-CLOSE1i), asked inside the take's write; null reads none.
/// </param>
public sealed record QuestTaker(string? Session, Func<string, bool>? Remote = null);

/// <summary>
/// A taker's submission in a closure graph (FABRIC-CLOSE1a, the fabric events design §E5): the operation that made it, by the
/// machine and sequence that name it everywhere, which the graph's close names as the one it judged; when; and its note. Its
/// answers are the quest's <see cref="Quest.Answers"/>.
/// </summary>
/// <param name="Session">
/// The session that said its <c>ready</c> (FABRIC-CLOSE1c4), as its connector's intake named it: the one to tell when the person
/// reopens it. Null for a <c>ready</c> no session said, through a door that names none, and for every submission from before.
/// </param>
public sealed record QuestSubmission(string Machine, long Sequence, DateTimeOffset At, string? Note, string? Session = null);

/// <summary>
/// The person's reopen of a quest's own submission (FABRIC-CLOSE1c4, §E5), as the quest keeps it until a later submission answers
/// it: the <see cref="QuestOperationKind.Reopened"/> operation by its machine and sequence, the submission it reopened
/// (<paramref name="Of"/>), the person's words, and when.
/// </summary>
/// <param name="Session">
/// The session that said the reopened submission's <c>ready</c> (FABRIC-CLOSE1c8), which the reopen clears with the submission: the
/// one its taker's machine tells. Null where none did (the respond door) and for a reopen a build before it kept.
/// </param>
public sealed record QuestReopen(string Machine, long Sequence, QuestOperationRef Of, string? Note, DateTimeOffset At, string? Session = null);

/// <param name="Quest">The quest as it stood when the delete was judged — null when there is no such quest.</param>
/// <param name="Deleted">Whether THIS call deleted it. False with a quest is the table refusing: it is not open.</param>
/// <param name="Tombstoned">Whether the delete is an operation that travels, rather than the quest simply going (D95).</param>
public sealed record QuestDeletion(Quest? Quest, bool Deleted, bool Tombstoned = false);

/// <summary>
/// Quests, held by the service rather than written into anyone's repository.
/// </summary>
/// <remarks>
/// <para><b>This is a server responsibility, and the first version got it wrong.</b> The original
/// implementation had one repository's agent write a quest straight into another repository's backlog
/// file. That is the very thing `repository-owns-its-work` forbids — an outside edit is still an
/// outside edit when it is one file and uncommitted, and it arrives from the party that knows that
/// codebase least. Building the tool that way made the rule's own tooling break the rule.</para>
///
/// <para><b>So the service holds them and repositories pull.</b> An agent publishes a quest here; the
/// receiving repository's own agent reads what is addressed to it and decides what to do — including
/// materializing it into its backlog, which is then that repository editing itself. Nobody reaches
/// across.</para>
///
/// <para><b>Only a repository something can answer for is addressed.</b> That is an adopter, whose own
/// connector sees the quest, or a repository registered with a root on this machine, whose driven
/// session is handed a connector over the protocol door (D70). A quest nothing could answer would sit
/// in a queue nobody reads, so refusing at publish time says that immediately, rather than letting it
/// look delivered.</para>
///
/// <para><b>A quest is its history</b> (D68, SYNC1). Every verb appends an operation to
/// <c>quest_log</c>, stamped with this store's machine and that machine's next sequence number, and
/// the <c>quests</c> table is rewritten from replaying the quest's history through
/// <see cref="QuestTransitions"/> in the same transaction — so what the platform reads is a cache of
/// the replay, never a second truth beside it.</para>
///
/// <para><b>A remote is where histories meet</b> (D68 §3, design §8): a machine fetches what the
/// remote accepted, rebases what it has not pushed on top, and pushes; the remote accepts a quest's
/// operations only when nothing reached that quest since the push was rebased. Both halves live here,
/// behind the same table: <see cref="IntegrateAsync"/>, <see cref="PendingAsync"/> and
/// <see cref="AcceptedAsync"/> for a machine, <see cref="OperationsSinceAsync"/> and
/// <see cref="ReceiveAsync"/> for a remote.</para>
/// </remarks>
public sealed class QuestStore
{
    /// <summary>
    /// Hex characters in a quest id: 48 bits (design §7). Six were enough while a machine held its own
    /// quests; once every machine holds every quest touching its repositories, unrelated asks collide.
    /// </summary>
    internal const int IdLength = 12;

    /// <summary>What an id was before it widened — a quest published then keeps the id it was quoted by.</summary>
    internal const int LegacyIdLength = 6;

    /// <summary>
    /// How long a graph named for a quest's take waits for it (FABRIC-CLOSE1o): the driver names it as the quest's first session
    /// opens, whose first act is the take, so a take later than this is not that start's, and goes unbound, saying so.
    /// </summary>
    public static readonly TimeSpan TakeBindLife = TimeSpan.FromHours(1);

    /// <summary>
    /// The version of what this build keeps in the store (CLOSETAKE1, QUESTSTOREVER1), stamped in <c>quest_store_version</c>. 1 is the
    /// build with owned take binds (FABRIC-CLOSE1w); raise it with any change to a table or a contract an older build would misread.
    /// </summary>
    public const int StoreVersion = 1;

    /// <summary>The version a newer build stamped on this store, or null where this build's is the newest that has written it.</summary>
    public int? NewerVersion { get; private set; }

    /// <summary>The connection's gate: every command here runs inside it (SQLITETX1).</summary>
    private readonly ConnectionGate _db;

    /// <summary>
    /// The clock a take measures its wait for the write on (FABRIC-CLOSE1w), so the bind named for it is judged at the moment the
    /// write began, never at the caller's moment before it queued.
    /// </summary>
    private readonly TimeProvider _time;

    private QuestStore(SqliteConnection connection, TimeProvider time)
    {
        _db = ConnectionGate.For(connection);
        _time = time;
    }

    /// <summary>
    /// This store's machine: the stable id every operation it writes is stamped with (D68 §2) — never
    /// the key, which rotates.
    /// </summary>
    /// <remarks>
    /// Kept in the store, beside the sequence it numbers, rather than in a file of its own: a machine
    /// whose store was deleted starts its sequence again at one, and under the same id that would
    /// name operations a remote already holds. A new store is a new machine, and cannot be anything else.
    /// </remarks>
    public string Machine { get; private set; } = "";

    /// <param name="connection">The store's connection, opened.</param>
    /// <param name="ct">Honoured while waiting for the connection's gate.</param>
    /// <param name="time">The clock a take measures its wait on (FABRIC-CLOSE1w); the system's where none is named.</param>
    public static Task<QuestStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default, TimeProvider? time = null) =>
        ConnectionGate.For(connection).RunAsync(async () =>
        {
            _ = Operations;
            var store = new QuestStore(connection, time ?? TimeProvider.System);
            store.NewerVersion = await QuestSchema.EnsureAsync(store._db, ct).ConfigureAwait(false);
            store.Machine = await store.EnsureMachineAsync(ct).ConfigureAwait(false);
            await store.LoadClosuresAsync(ct).ConfigureAwait(false);
            await store.GiveHistoriesAsync(ct).ConfigureAwait(false);
            await store.RecacheUnnamedConflictsAsync(ct).ConfigureAwait(false);
            await store.RecacheClosedAwaitsAsync(ct).ConfigureAwait(false);
            await store.RecacheUnkeptReopensAsync(ct).ConfigureAwait(false);
            return store;
        }, ct);

    /// <summary>
    /// A source reopened by a build before FABRIC-CLOSE1c4 has a row that keeps no reopen, though its log holds one no submission
    /// answered since. Those taken quests are replayed from the log, which reads it, so the driver tells it from one not yet ready.
    /// A step its source's reopen reached holds a reopen too, but no submission of its own, so it is never selected; and once
    /// replayed a source's row keeps its reopen, so no later open selects it again.
    /// </summary>
    private Task RecacheUnkeptReopensAsync(CancellationToken ct) =>
        RecacheAsync(
            """
            SELECT id FROM quests
            WHERE status = 'Taken' AND submission IS NULL AND reopened IS NULL
              AND EXISTS (SELECT 1 FROM quest_log WHERE quest_log.quest = quests.id AND kind = 'reopened')
              AND EXISTS (SELECT 1 FROM quest_log WHERE quest_log.quest = quests.id AND kind = 'submitted')
            """,
            ct);

    /// <summary>
    /// A cache written before dismissals (SYNC6c) holds conflicts without the sequence that names them,
    /// and a conflict that cannot be named cannot be dismissed. Those quests are replayed from the log,
    /// which has always kept the sequence — the cache is only ever what the history replays to.
    /// </summary>
    private Task RecacheUnnamedConflictsAsync(CancellationToken ct) =>
        RecacheAsync("SELECT id FROM quests WHERE conflicts <> '[]' AND conflicts NOT LIKE '%\"sequence\"%'", ct);

    /// <summary>
    /// A quest closed before a close cleared <c>awaits</c> (AWAITSTALE1, D79) still names the question it waited on.
    /// Those closed rows are replayed from the log, which clears it and keeps the Waited operation.
    /// </summary>
    private Task RecacheClosedAwaitsAsync(CancellationToken ct) =>
        RecacheAsync("SELECT id FROM quests WHERE status IN ('Done', 'Declined') AND awaits IS NOT NULL", ct);

    private async Task RecacheAsync(string stale,CancellationToken ct)
    {
        await using (var probe = _db.Command())
        {
            probe.CommandText = $"SELECT EXISTS ({stale})";
            if (Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false)) == 0) return;
        }

        await InTransactionAsync(async (transaction, inside) =>
        {
            var ids = new List<string>();
            await using (var select = _db.Command())
            {
                select.Transaction = transaction;
                select.CommandText = stale;
                await using var reader = await select.ExecuteReaderAsync(inside).ConfigureAwait(false);
                while (await reader.ReadAsync(inside).ConfigureAwait(false)) ids.Add(reader.GetString(0));
            }

            foreach (var id in ids)
            {
                if (QuestLog.Replay(await HistoryAsync(id, transaction, inside).ConfigureAwait(false)) is { } quest)
                {
                    await WriteCacheAsync(quest, transaction, inside).ConfigureAwait(false);
                }
            }

            return ids.Count;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The store's machine id, made the first time any host opens it. One fixed row, so two hosts
    /// opening a new store at once cannot each make one: the second insert is ignored and both read
    /// the first.
    /// </summary>
    private async Task<string> EnsureMachineAsync(CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = """
            INSERT OR IGNORE INTO quest_machine (one, id) VALUES (1, $id);
            SELECT id FROM quest_machine WHERE one = 1;
            """;
        command.Parameters.AddWithValue(
            "$id", Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8)).ToLowerInvariant());
        return (string)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
    }

    /// <summary>
    /// Give every quest of this store's own that the log has never seen its history, from what its row
    /// says: published when it was filed, and the move to where it stands when it last moved.
    /// </summary>
    /// <remarks>
    /// A quest is not derivable from anything, so a store from before the log is migrated rather than
    /// rebuilt (the index's rule is for what can be re-read). Checked again inside the transaction,
    /// because two hosts open one store and only one of them may write the histories.
    /// </remarks>
    private async Task GiveHistoriesAsync(CancellationToken ct)
    {
        const string Unlogged = """
            SELECT * FROM quests
            WHERE NOT EXISTS (SELECT 1 FROM quest_log WHERE quest_log.quest = quests.id)
            ORDER BY filed, id
            """;

        await using (var probe = _db.Command())
        {
            probe.CommandText = $"SELECT EXISTS ({Unlogged})";
            if (Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false)) == 0) return;
        }

        await InTransactionAsync(async (transaction, inside) =>
        {
            var unlogged = new List<Quest>();
            await using (var select = _db.Command())
            {
                select.Transaction = transaction;
                select.CommandText = Unlogged;
                await using var reader = await select.ExecuteReaderAsync(inside).ConfigureAwait(false);
                while (await reader.ReadAsync(inside).ConfigureAwait(false)) unlogged.Add(Read(reader));
            }

            foreach (var quest in unlogged) await GiveHistoryAsync(quest, transaction, inside).ConfigureAwait(false);
            return unlogged.Count;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>One quest's history, from its row — the operations that replay to exactly what it says.</summary>
    internal async Task<IReadOnlyList<QuestOperation>> GiveHistoryAsync(
        Quest quest, SqliteTransaction transaction, CancellationToken ct)
    {
        var history = new List<QuestOperation>
        {
            await AppendAsync(quest.Id, QuestOperationKind.Published, quest.Filed, null, quest, transaction, ct)
                .ConfigureAwait(false),
        };

        if (QuestTransitions.KindFor(quest.Status) is { } moved)
        {
            history.Add(await AppendAsync(quest.Id, moved, quest.Updated, quest.Note, null, transaction, ct, noteParts: quest.NoteParts)
                .ConfigureAwait(false));
        }

        return history;
    }

    /// <summary>
    /// Run <paramref name="work"/> in a write transaction taken at once (BEGIN IMMEDIATE), not at its
    /// first write: the judgement is made on what it reads, so the read must already hold the lock, or
    /// two hosts over one file could both judge the same open quest and both append a take (D47 §5).
    /// </summary>
    /// <remarks>
    /// <para>Within one host, the connection's gate holds every other request's work off until this one
    /// ends, its commands as well as its transactions (<see cref="ConnectionGate"/>, SQLITETX1).</para>
    ///
    /// <para>It COMMITS whenever the work returns, including a refusal that wrote nothing, and rolls back
    /// only on a throw. Cancellation is not one, once the transaction has begun: the wait honours the
    /// caller, and the work is handed a token nobody cancels (REV3).</para>
    /// </remarks>
    private Task<T> InTransactionAsync<T>(
        Func<SqliteTransaction, CancellationToken, Task<T>> work, CancellationToken ct) =>
        _db.InTransactionAsync(work, ct);

    /// <summary>A registered operation's verb, run in a write transaction (<see cref="InTransactionAsync{T}"/>) handed to it as its <see cref="QuestTx"/>.</summary>
    private Task<T> WriteAsync<T>(Func<QuestTx, Task<T>> write, CancellationToken ct) =>
        InTransactionAsync((transaction, inside) => write(new QuestTx(this, transaction, inside)), ct);

    /// <summary>The kinds of operation their modules register; a door reaches a module's verbs by its type.</summary>
    private static QuestOperations Operations => QuestOperations.Registered;

    internal SqliteCommand Command() => _db.Command();

    internal TimeProvider Time => _time;

    /// <summary>
    /// A handle derived from who asked, of whom, and for what — twelve hex characters (design §7).
    /// </summary>
    /// <remarks>
    /// Content-derived so publishing the same quest twice collides rather than multiplying — an agent
    /// that retries should not produce a second copy of the same ask, and the same ask made on two
    /// machines is one quest. A chain's step also derives from its PARENT (D65 §4): "Verify in the
    /// browser" is a title many chains will use, and a step that collided with an earlier quest of
    /// those words would quietly join somebody else's closed quest. The widening kept the hash, so an
    /// id from before it is exactly the first six characters of the same ask's id now. A quest to
    /// LANES widens the same way (D115 §2.2), only when there are some, so every quest to a whole
    /// repository keeps its id and a lane's ask is not the repository's. A workflow request's key widens it the same way (INTEGRATE1p,
    /// R5's C32), only when there is one, so distinct runs or nodes make distinct quests whatever their words.
    /// </remarks>
    internal static string MakeId(
        string from, string to, string title, string? parent = null, IReadOnlyList<string>? lanes = null, string? request = null) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(
                    $"{from}->{to}:{title.Trim()}" + (parent is null ? "" : $"<-{parent}")
                    + (lanes is { Count: > 0 } ? $"@{string.Join('+', lanes.Order(StringComparer.Ordinal))}" : "")
                    + (request is null ? "" : $"#{request}"))))[..IdLength]
            .ToLowerInvariant();

    /// <summary>Publish a quest. Returns the existing one unchanged if it was already asked.</summary>
    /// <param name="workspace">
    /// The circle both sides share — decided by <see cref="QuestExchange"/>, which is where the
    /// same-workspace clause lives. The store holds state; it does not judge who may ask whom.
    /// </param>
    /// <param name="links">Addresses the quest carries, already judged by the exchange.</param>
    /// <param name="attachments">Files the quest carries, by name — the bytes are never this store's.</param>
    /// <param name="then">The chain after this quest (D65 §4), already judged by the exchange.</param>
    /// <param name="publishedBy">The session whose connector published it, when one did (SESS1).</param>
    /// <param name="lanes">The lanes of <paramref name="to"/> it addresses, already judged by the exchange (D115 §2.2).</param>
    /// <param name="requirements">What the person requires, already judged by the exchange against their words (DRIFT1c).</param>
    /// <param name="shortTitle">The publisher's short title, already judged by the exchange (SESSUX1j); null for none.</param>
    /// <param name="review">The chain's review choice, already judged by the exchange (REVIEWENV1b); null for none.</param>
    /// <param name="setUpIn">A set-up step's environment, already judged by the exchange (REVIEWENV1b); null for any other quest.</param>
    /// <param name="parent">
    /// The quest a set-up step follows, when a door publishes one after its chain's close (REVIEWENV1b, D149 point 1): its id
    /// derives from it as a chain step's does, so pressing twice is one step. Null for every other publish.
    /// </param>
    public async Task<Quest> PublishAsync(
        string from, string to, string title, string body, DateTimeOffset now,
        string? workspace = null,
        IReadOnlyList<string>? links = null,
        IReadOnlyList<QuestAttachment>? attachments = null,
        IReadOnlyList<QuestStep>? then = null,
        CancellationToken ct = default,
        string? publishedBy = null,
        IReadOnlyList<string>? lanes = null,
        IReadOnlyList<QuestRequirement>? requirements = null,
        string? shortTitle = null,
        QuestReview? review = null,
        string? setUpIn = null,
        string? parent = null,
        string? weight = null,
        IReadOnlyList<string>? paths = null,
        string? request = null)
    {
        return await WriteAsync(
            tx => Operations.Of<QuestPublishOperation>().PublishAsync(
                tx, from, to, title, body, now, workspace, links, attachments, then, publishedBy, lanes, requirements, shortTitle, review,
                setUpIn, parent, weight, paths, request),
            ct).ConfigureAwait(false);
    }

    /// <summary>The lanes a publish addresses, as its id and its record hold them: once each, sorted.</summary>
    internal static List<string> Sorted(IReadOnlyList<string>? lanes) =>
        (lanes ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// The id these words would publish under, when this machine forgot that quest (HIST1b, D153 point 3), at either
    /// width an id has had; null when it did not. The exchange asks it before a publish, so the same words asked again
    /// are refused here rather than making a fresh quest the remote, which still holds the closed one, refuses for good (H5).
    /// </summary>
    internal async Task<string?> ForgottenIdAsync(
        string from, string to, string title, IReadOnlyList<string>? lanes, CancellationToken ct, string? request = null)
    {
        // A request's key widens the id as it did at its publish (INTEGRATE1p), and such an id never had the shorter width.
        var id = MakeId(from, to, title, lanes: Sorted(lanes), request: request);
        foreach (var held in request is null ? new[] { id, id[..LegacyIdLength] } : [id])
        {
            if (await ForgottenAsync(held, ct).ConfigureAwait(false)) return held;
        }

        return null;
    }

    /// <summary>
    /// Append one operation, stamped with this machine and its next sequence number — counted inside
    /// the caller's write transaction, so two hosts over one file cannot take the same number.
    /// </summary>
    /// <remarks>
    /// The next number is one past the larger of the log's highest for this machine and the store's mark (HIST1a),
    /// which the insert's trigger then moves. The log alone goes back when its newest rows are removed, and the number
    /// handed out again is one a remote already holds: it answers a push of it as a retry, and a fetch brings its own
    /// operation back under it, so the new move is lost without an error (H1).
    /// </remarks>
    internal async Task<QuestOperation> AppendAsync(
        string quest, QuestOperationKind kind, DateTimeOffset at, string? note, Quest? published,
        SqliteTransaction transaction, CancellationToken ct, QuestOperationRef? dismisses = null,
        IReadOnlyList<QuestAnswer>? answers = null, bool whileOpen = false, QuestEvidenceVerdict? evidence = null,
        QuestSetUp? setUp = null, QuestReviewVerdict? review = null, string? noteParts = null, QuestClose? close = null,
        QuestOperationRef? submission = null, string? session = null, QuestOperationRef? forward = null,
        QuestWorkflowChoice? choice = null)
    {
        // Each field kept only on the kinds that carry it, so the row written and the operation handed back read the same; a publish
        // carries the quest as asked, open from this moment.
        var operation = new QuestOperation(
            quest, kind, Machine, 0, at, note,
            published is null
                ? null
                : published with { Status = QuestStatus.Open, Note = null, NoteParts = null, Filed = at, Updated = at, Conflicts = [] },
            Dismisses: dismisses,
            Answers: answers is { Count: > 0 } ? answers : null,
            WhileOpen: whileOpen && JudgedOpen(kind),
            Evidence: kind == QuestOperationKind.Evidenced ? evidence : null,
            SetUp: kind == QuestOperationKind.SetUp ? setUp : null,
            Verdict: kind == QuestOperationKind.Verdict ? review : null,
            // Parts only beside a note, as the store keeps them (QUESTCLOSE1b1): what it reads back is what it wrote.
            NoteParts: note is null ? null : NoteParts.Normalize(NoteParts.Element(noteParts)),
            Close: kind == QuestOperationKind.Done ? close : null,
            Submission: Carries(kind) ? submission : null,
            Session: KeepsSession(kind) ? session : null,
            Forward: kind == QuestOperationKind.Dismissed ? forward : null,
            Choice: kind == QuestOperationKind.Chosen ? choice : null);

        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO quest_log (quest, kind, machine, sequence, at, payload)
            VALUES ($quest, $kind, $machine,
                    (SELECT MAX(
                       COALESCE((SELECT MAX(sequence) FROM quest_log WHERE machine = $machine), 0),
                       COALESCE((SELECT sequence FROM quest_machine WHERE one = 1 AND id = $machine), 0)) + 1),
                    $at, $payload)
            RETURNING sequence
            """;
        command.Parameters.AddWithValue("$quest", quest);
        command.Parameters.AddWithValue("$kind", KindText(kind));
        command.Parameters.AddWithValue("$machine", Machine);
        command.Parameters.AddWithValue("$at", Moments.Write(at));
        command.Parameters.AddWithValue("$payload", PayloadJson(operation));
        return operation with { Sequence = Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false)) };
    }

    /// <summary>The kinds that name a submission (FABRIC-CLOSE1c): a reopen, and a rebind.</summary>
    internal static bool Names(QuestOperationKind kind) => kind is QuestOperationKind.Reopened or QuestOperationKind.Rebound;

    /// <summary>
    /// The kinds that keep the session that said them through its connector: a ready (FABRIC-CLOSE1c4), and a done and a decline
    /// (HANDBACK1, D219), each the act a handback is written at. On any other kind it is never written, kept or read; on these it is
    /// written only where a session said it, so a close through any other door, and every one from before, reads as it did.
    /// </summary>
    internal static bool KeepsSession(QuestOperationKind kind) =>
        kind is QuestOperationKind.Submitted or QuestOperationKind.Done or QuestOperationKind.Declined;

    /// <summary>
    /// The kinds a submission may ride on: the two that name one (<see cref="Names"/>), and a take, which carries its step's source as it
    /// found it (FABRIC-CLOSE1c18), only where it found one. On any other kind it is never written, kept or read.
    /// </summary>
    internal static bool Carries(QuestOperationKind kind) => Names(kind) || kind == QuestOperationKind.Taken;

    /// <summary>
    /// The kinds a <c>whileOpen</c> mark may carry: a decline (PAUSE1c), and a publication's rebind (FABRIC-CLOSE1c15). On any other
    /// kind it is never written, kept or read.
    /// </summary>
    internal static bool JudgedOpen(QuestOperationKind kind) => kind is QuestOperationKind.Declined or QuestOperationKind.Rebound;

    /// <summary>
    /// Keep an operation another machine made, under ITS machine and sequence — what a remote receives
    /// and what a machine fetches. <paramref name="number"/> is where a remote placed it; a remote
    /// itself keeps none, because its own position is the number.
    /// </summary>
    /// <returns>The position it was kept at — on a remote, the number it was given.</returns>
    private async Task<long> KeepAsync(
        QuestOperation operation, long? number, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO quest_log (quest, kind, machine, sequence, at, payload, remote)
            VALUES ($quest, $kind, $machine, $sequence, $at, $payload, $remote)
            RETURNING position
            """;
        command.Parameters.AddWithValue("$quest", operation.Quest);
        command.Parameters.AddWithValue("$kind", KindText(operation.Kind));
        command.Parameters.AddWithValue("$machine", operation.Machine);
        command.Parameters.AddWithValue("$sequence", operation.Sequence);
        command.Parameters.AddWithValue("$at", Moments.Write(operation.At));
        command.Parameters.AddWithValue("$payload", PayloadJson(operation));
        command.Parameters.AddWithValue("$remote", (object?)number ?? DBNull.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    /// <summary>Where this store keeps an operation, by the machine and sequence that name it anywhere.</summary>
    private async Task<long?> PositionOfAsync(
        string machine, long sequence, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = "SELECT position FROM quest_log WHERE machine = $machine AND sequence = $sequence";
        command.Parameters.AddWithValue("$machine", machine);
        command.Parameters.AddWithValue("$sequence", sequence);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is { } position and not DBNull
            ? Convert.ToInt64(position)
            : null;
    }

    /// <summary>
    /// Write a quest's row as its replay gives it. The row is a cache, so it is written whole: a
    /// replay is the only thing that decides what it says.
    /// </summary>
    internal async Task WriteCacheAsync(Quest quest, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO quests (id, sender, receiver, title, body, status, note, note_parts, filed, updated, workspace, links, attachments, then_steps, parent, conflicts, awaits, published_by, lanes, requirements, answers, accepted, held, short_title, evidence, review, set_up_in, set_ups, verdicts, closure, submission, grew_from, source_reopened, reopened, workflow_choices, weight, paths, request)
            VALUES ($id, $sender, $receiver, $title, $body, $status, $note, $noteParts, $filed, $updated, $workspace, $links, $attachments, $then, $parent, $conflicts, $awaits, $publishedBy, $lanes, $requirements, $answers, $accepted, $held, $short, $evidence, $review, $setUpIn, $setUps, $verdicts, $closure, $submission, $grewFrom, $sourceReopened, $reopened, $workflowChoices, $weight, $paths, $request)
            ON CONFLICT (id) DO UPDATE SET
              sender = excluded.sender, receiver = excluded.receiver, title = excluded.title, body = excluded.body,
              status = excluded.status, note = excluded.note, note_parts = excluded.note_parts, filed = excluded.filed, updated = excluded.updated,
              workspace = excluded.workspace, links = excluded.links, attachments = excluded.attachments,
              then_steps = excluded.then_steps, parent = excluded.parent, conflicts = excluded.conflicts,
              awaits = excluded.awaits, published_by = excluded.published_by, lanes = excluded.lanes,
              requirements = excluded.requirements, answers = excluded.answers, accepted = excluded.accepted,
              held = excluded.held, short_title = excluded.short_title, evidence = excluded.evidence,
              review = excluded.review, set_up_in = excluded.set_up_in, set_ups = excluded.set_ups, verdicts = excluded.verdicts,
              closure = excluded.closure, submission = excluded.submission,
              grew_from = excluded.grew_from, source_reopened = excluded.source_reopened, reopened = excluded.reopened,
              workflow_choices = excluded.workflow_choices, weight = excluded.weight, paths = excluded.paths,
              request = excluded.request
            """;
        command.Parameters.AddWithValue("$request", (object?)quest.Request ?? DBNull.Value);
        command.Parameters.AddWithValue("$weight", (object?)quest.Weight ?? DBNull.Value);
        command.Parameters.AddWithValue("$paths", quest.Paths.Count == 0 ? DBNull.Value : LinksJson(quest.Paths));
        command.Parameters.AddWithValue("$workflowChoices", JsonFields.Written(writer =>
        {
            writer.WriteStartArray();
            foreach (var choice in quest.WorkflowChoices) WorkflowChoices.Write(writer, choice, standing: true);
            writer.WriteEndArray();
        }));
        command.Parameters.AddWithValue("$closure", (object?)quest.Closure ?? DBNull.Value);
        command.Parameters.AddWithValue("$grewFrom", quest.GrewFrom is { } from
            ? JsonFields.Written(writer => { writer.WriteStartObject(); WriteRefFields(writer, from); writer.WriteEndObject(); })
            : DBNull.Value);
        command.Parameters.AddWithValue("$sourceReopened", quest.SourceReopened is { } reopened
            ? JsonFields.Written(writer => { writer.WriteStartObject(); WriteRefFields(writer, reopened); writer.WriteEndObject(); })
            : DBNull.Value);
        command.Parameters.AddWithValue("$submission", quest.Submission is { } submitted
            ? JsonFields.Written(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("machine", submitted.Machine);
                writer.WriteNumber("sequence", submitted.Sequence);
                writer.WriteString("at", Moments.Write(submitted.At));
                if (submitted.Note is not null) writer.WriteString("note", submitted.Note);
                if (submitted.Session is not null) writer.WriteString("session", submitted.Session);
                writer.WriteEndObject();
            })
            : DBNull.Value);
        command.Parameters.AddWithValue("$reopened", quest.Reopened is { } reopen
            ? JsonFields.Written(writer =>
            {
                writer.WriteStartObject();
                WriteRefFields(writer, new QuestOperationRef(reopen.Machine, reopen.Sequence));
                WriteRef(writer, "of", reopen.Of);
                if (reopen.Note is not null) writer.WriteString("note", reopen.Note);
                writer.WriteString("at", Moments.Write(reopen.At));
                if (reopen.Session is not null) writer.WriteString("session", reopen.Session);
                writer.WriteEndObject();
            })
            : DBNull.Value);
        command.Parameters.AddWithValue(
            "$review", quest.Review is { } chosen ? JsonFields.Written(writer => Reviews.Write(writer, chosen)) : DBNull.Value);
        command.Parameters.AddWithValue("$setUpIn", (object?)quest.SetUpIn ?? DBNull.Value);
        command.Parameters.AddWithValue("$setUps", JsonFields.Written(writer =>
        {
            writer.WriteStartArray();
            foreach (var setUp in quest.SetUps) Reviews.Write(writer, setUp, standing: true);
            writer.WriteEndArray();
        }));
        command.Parameters.AddWithValue("$verdicts", JsonFields.Written(writer =>
        {
            writer.WriteStartArray();
            foreach (var verdict in quest.Verdicts) Reviews.Write(writer, verdict, standing: true);
            writer.WriteEndArray();
        }));
        command.Parameters.AddWithValue("$short", (object?)quest.Short ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$evidence", quest.Evidence is { } read ? (object)JsonFields.Written(writer => read.Write(writer, standing: true)) : DBNull.Value);
        command.Parameters.AddWithValue("$publishedBy", (object?)quest.PublishedBy ?? DBNull.Value);
        command.Parameters.AddWithValue("$lanes", LinksJson(quest.Lanes));
        command.Parameters.AddWithValue("$requirements", RequirementsJson(quest.Requirements));
        command.Parameters.AddWithValue("$answers", AnswersJson(quest.Answers));
        command.Parameters.AddWithValue("$accepted", (object?)Moments.Write(quest.Accepted) ?? DBNull.Value);
        command.Parameters.AddWithValue("$held", quest.Held ? 1 : 0);
        command.Parameters.AddWithValue("$conflicts", ConflictsJson(quest.Conflicts));
        command.Parameters.AddWithValue("$links", LinksJson(quest.Links));
        command.Parameters.AddWithValue("$attachments", AttachmentsJson(quest.Attachments));
        command.Parameters.AddWithValue("$then", StepsJson(quest.Then));
        command.Parameters.AddWithValue("$parent", (object?)quest.Parent ?? DBNull.Value);
        command.Parameters.AddWithValue("$awaits", (object?)quest.Awaits ?? DBNull.Value);
        command.Parameters.AddWithValue("$workspace", quest.Workspace);
        command.Parameters.AddWithValue("$id", quest.Id);
        command.Parameters.AddWithValue("$sender", quest.From);
        command.Parameters.AddWithValue("$receiver", quest.To);
        command.Parameters.AddWithValue("$title", quest.Title);
        command.Parameters.AddWithValue("$body", quest.Body);
        command.Parameters.AddWithValue("$status", quest.Status.ToString());
        command.Parameters.AddWithValue("$note", (object?)quest.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$noteParts", (object?)quest.NoteParts ?? DBNull.Value);
        command.Parameters.AddWithValue("$filed", Moments.Write(quest.Filed));
        command.Parameters.AddWithValue("$updated", Moments.Write(quest.Updated));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A quest's history, in the order it replays (design §8): what a remote accepted, by its number,
    /// then what this machine has not pushed, in the order it was made. Empty for an unknown id.
    /// </summary>
    public Task<IReadOnlyList<QuestOperation>> HistoryAsync(string id, CancellationToken ct = default) =>
        InGateAsync(() => HistoryAsync(id, transaction: null, ct), ct);

    internal async Task<IReadOnlyList<QuestOperation>> HistoryAsync(
        string id, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        // None handed in keeps the one the command was made in: the connection hands a new command the transaction it
        // has open, and a caller holding one without its object (HistoryDesk's, HistoryWithinAsync) must not lose it.
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = $"""
            SELECT {OperationColumns} FROM quest_log WHERE quest = $id
            ORDER BY remote IS NULL, remote, position
            """;
        command.Parameters.AddWithValue("$id", id);

        var history = new List<QuestOperation>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) history.Add(ReadOperation(reader));
        return history;
    }

    /// <inheritdoc cref="QuestTakeOperation.MoveAsync"/>
    public Task<QuestMove> MoveAsync(
        string id, QuestStatus status, string? note, DateTimeOffset now, CancellationToken ct = default,
        IReadOnlyList<QuestAnswer>? answers = null, bool whileOpen = false, string? noteParts = null, bool byAgent = false,
        QuestTaker? taker = null, string? session = null)
    {
        // FABRIC-CLOSE1w: `now` is the caller's, taken before this queued for the gate and the write, so the take measures that wait.
        var asked = _time.GetTimestamp();
        return WriteAsync(
            tx => Operations.Of<QuestTakeOperation>().MoveAsync(
                tx, id, status, note, now, asked, answers, whileOpen, noteParts, byAgent, taker, session),
            ct);
    }

    /// <inheritdoc cref="QuestBindOperation.BindAsync"/>
    public Task<QuestMove> BindAsync(string id, string graph, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestBindOperation>().BindAsync(tx, id, graph, now), ct);

    /// <inheritdoc cref="QuestBindOperation.BindAtTakeAsync"/>
    public Task<QuestMove> BindAtTakeAsync(string id, string graph, string session, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestBindOperation>().BindAtTakeAsync(tx, id, graph, session, now), ct);

    /// <inheritdoc cref="QuestSubmitOperation.SubmitAsync"/>
    public Task<QuestMove> SubmitAsync(
        string id, string? note, IReadOnlyList<QuestAnswer> answers, DateTimeOffset now, CancellationToken ct = default,
        string? session = null) =>
        WriteAsync(tx => Operations.Of<QuestSubmitOperation>().SubmitAsync(tx, id, note, answers, now, session), ct);

    /// <inheritdoc cref="QuestReopenOperation.ReopenAsync"/>
    public Task<QuestReopening> ReopenAsync(
        string id, QuestOperationRef submission, string? note, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestReopenOperation>().ReopenAsync(tx, id, submission, note, now), ct);

    /// <inheritdoc cref="QuestReopenOperation.ContinueAsync"/>
    public Task<QuestReopening> ContinueAsync(string id, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestReopenOperation>().ContinueAsync(tx, id, now), ct);

    /// <inheritdoc cref="QuestCloseOperation.CloseAsync"/>
    public Task<QuestClosing> CloseAsync(
        string at, IReadOnlyList<(string Id, QuestOperationRef Submission)> named, QuestClose close, DateTimeOffset now,
        CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestCloseOperation>().CloseAsync(tx, at, named, close, now), ct);

    /// <inheritdoc cref="QuestCloseOperation.CloseOutcomeAsync"/>
    public Task<QuestClosing> CloseOutcomeAsync(
        IReadOnlyList<(string Id, QuestOperationRef Submission)> named, QuestClose close, DateTimeOffset now,
        CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestCloseOperation>().CloseOutcomeAsync(tx, named, close, now), ct);

    /// <inheritdoc cref="QuestAcceptOperation.AcceptAsync"/>
    public Task<QuestMove> AcceptAsync(string id, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestAcceptOperation>().AcceptAsync(tx, id, now), ct);

    /// <inheritdoc cref="QuestSetUpOperation.SetUpAsync"/>
    public Task<QuestMove> SetUpAsync(string id, QuestSetUp setUp, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestSetUpOperation>().SetUpAsync(tx, id, setUp, now), ct);

    /// <inheritdoc cref="QuestVerdictOperation.VerdictAsync"/>
    public Task<QuestMove> VerdictAsync(string id, QuestReviewVerdict verdict, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestVerdictOperation>().VerdictAsync(tx, id, verdict, now), ct);

    /// <inheritdoc cref="QuestChoiceOperation.ChooseAsync"/>
    public Task<QuestMove> ChooseAsync(string id, QuestWorkflowChoice choice, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestChoiceOperation>().ChooseAsync(tx, id, choice, now), ct);

    /// <summary>
    /// Every quest of the <c>follows</c> chain <paramref name="id"/> is on (D149 point 1; REVIEWENV1b3): its first quest, found by
    /// walking <c>parent</c> up, and every quest that follows it at any depth, a chain step its close published or a set-up step
    /// a door published after it. Empty for an unknown id. The late set-up door judges a repository's part of the chain on it.
    /// </summary>
    public Task<IReadOnlyList<Quest>> ChainAsync(string id, CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyList<Quest>>(async () =>
        {
            async Task<Quest?> One(string named)
            {
                await using var command = _db.Command();
                command.CommandText = "SELECT * FROM quests WHERE id = $id";
                command.Parameters.AddWithValue("$id", named);
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
            }

            async Task<List<Quest>> Following(string parent)
            {
                await using var command = _db.Command();
                command.CommandText = "SELECT * FROM quests WHERE parent = $parent ORDER BY filed";
                command.Parameters.AddWithValue("$parent", parent);
                var steps = new List<Quest>();
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false)) steps.Add(Read(reader));
                return steps;
            }

            // Up to the first quest; a parent seen twice, which no chain makes, ends the walk rather than looping.
            if (await One(id).ConfigureAwait(false) is not { } first) return [];
            var seen = new HashSet<string>(StringComparer.Ordinal) { first.Id };
            while (first.Parent is { } parent && seen.Add(parent) && await One(parent).ConfigureAwait(false) is { } above) first = above;

            var chain = new List<Quest> { first };
            var visited = new HashSet<string>(StringComparer.Ordinal) { first.Id };
            for (var next = 0; next < chain.Count; next++)
            {
                foreach (var step in await Following(chain[next].Id).ConfigureAwait(false))
                {
                    if (visited.Add(step.Id)) chain.Add(step);
                }
            }

            return chain;
        }, ct);

    /// <inheritdoc cref="QuestEvidenceOperation.EvidenceAsync"/>
    public Task<QuestMove> EvidenceAsync(string id, QuestEvidenceVerdict verdict, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestEvidenceOperation>().EvidenceAsync(tx, id, verdict, now), ct);

    /// <inheritdoc cref="QuestWaitOperation.WaitAsync"/>
    public Task<QuestMove> WaitAsync(string id, string on, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestWaitOperation>().WaitAsync(tx, id, on, now), ct);

    /// <inheritdoc cref="QuestDismissOperation.DismissAsync"/>
    public Task<QuestDismissal> DismissAsync(
        string id, string? machine, long? sequence, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestDismissOperation>().DismissAsync(tx, id, machine, sequence, now), ct);

    /// <inheritdoc cref="QuestDismissOperation.BringForwardAsync"/>
    public Task<QuestBringingForward> BringForwardAsync(
        string id, QuestOperationRef conflict, DateTimeOffset now, CancellationToken ct = default, bool running = false) =>
        WriteAsync(tx => Operations.Of<QuestDismissOperation>().BringForwardAsync(tx, id, conflict, now, running), ct);

    /// <summary>This machine's bring-forwards on a quest (SYNCMERGE1b0), in the order they were made.</summary>
    private async Task<IReadOnlyList<QuestForwardPress>> PressesAsync(string id, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT conflict_machine, conflict_sequence, take_machine, take_sequence, at FROM quest_forwards
            WHERE quest = $quest ORDER BY at, take_sequence
            """;
        command.Parameters.AddWithValue("$quest", id);
        var presses = new List<QuestForwardPress>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            presses.Add(new QuestForwardPress(
                new QuestOperationRef(reader.GetString(0), reader.GetInt64(1)), new QuestOperationRef(reader.GetString(2), reader.GetInt64(3)),
                Moments.ReadRequired(reader.GetString(4))));
        }

        return presses;
    }

    /// <summary>
    /// Who holds a quest's step and how each take that lost on it stands or ended (SYNCMERGE1b0, <see cref="QuestLog.Read"/>), with this
    /// machine's own bring-forwards: what the quest's answer and the claim's say of them, and what the session ledger carries a
    /// stand-down on by. <see cref="QuestTakeReading.None"/> for a quest this store does not hold.
    /// </summary>
    public Task<QuestTakeReading> ReadTakesAsync(string id, CancellationToken ct = default) =>
        InGateAsync(async () => QuestLog.Read(
            await HistoryAsync(id, transaction: null, ct).ConfigureAwait(false), await PressesAsync(id, ct).ConfigureAwait(false)), ct);

    /// <summary>
    /// <see cref="ReadTakesAsync(string, CancellationToken)"/> for each of <paramref name="quests"/> with something to read: taken, or one
    /// whose log holds a conflict, settled or not. By id; a quest with neither is absent, and reads as <see cref="QuestTakeReading.None"/>.
    /// A list answers many quests, so only those have their log read.
    /// </summary>
    public Task<IReadOnlyDictionary<string, QuestTakeReading>> ReadTakesAsync(IEnumerable<Quest> quests, CancellationToken ct = default) =>
        InGateAsync<IReadOnlyDictionary<string, QuestTakeReading>>(async () =>
        {
            var contested = new HashSet<string>(StringComparer.Ordinal);
            await using (var command = _db.Command())
            {
                command.CommandText = $"SELECT DISTINCT quest FROM quest_log WHERE kind = '{KindText(QuestOperationKind.Conflict)}'";
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false)) contested.Add(reader.GetString(0));
            }

            var read = new Dictionary<string, QuestTakeReading>(StringComparer.Ordinal);
            foreach (var quest in quests.Where(quest => quest.Status == QuestStatus.Taken || contested.Contains(quest.Id)))
            {
                read[quest.Id] = QuestLog.Read(
                    await HistoryAsync(quest.Id, transaction: null, ct).ConfigureAwait(false),
                    await PressesAsync(quest.Id, ct).ConfigureAwait(false));
            }

            return read;
        }, ct);

    /// <summary>
    /// Delete an open quest (D95), judged against the replayed history inside the write, as every move
    /// is: only an open quest goes. Whether anything outside this store stands on it — a session, a
    /// waiting quest — is the exchange's judgement, made before this is asked.
    /// </summary>
    /// <param name="travels">
    /// Whether the quest may have left this machine: its circle has a remote and its receiver is joined.
    /// A quest that may have, or whose history a remote numbered, is tombstoned — a <see cref="QuestOperationKind.Deleted"/>
    /// operation the next pass carries, because a copy elsewhere would otherwise come back on the next
    /// fetch. Any other quest simply goes, its history with it, since nothing anywhere holds a copy.
    /// </param>
    public Task<QuestDeletion> DeleteAsync(string id, DateTimeOffset now, bool travels, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestDeleteOperation>().DeleteAsync(tx, id, now, travels), ct);

    // ——— Clearing finished history from this machine (HIST1b, D153 point 3, history-clearing design §3).

    /// <summary>
    /// Remove one quest from this machine: its row and its whole log together, since a row with no log is given a fresh
    /// pending history at the next open and a log with no row lets a publish append to it (H3). Where a remote numbered any
    /// of its operations it is <b>forgotten</b>: <c>quest_forgotten</c> keeps its id, and the fetch skips it from then on
    /// (<see cref="IntegrateAsync"/>), so no later move and no cursor at zero brings it back (H4). No operation is
    /// written, so nothing travels: the team's copy is untouched, and a new store fetches it whole.
    /// </summary>
    /// <remarks>
    /// Blind, like every write here, and opening no transaction of its own: <see cref="HistoryDesk"/> judges the unit and
    /// calls this inside the one transaction it clears the unit in, so the mark, the log and the row go together or not at
    /// all. The gate it takes is the one that transaction's work already holds (SQLITETX1).
    /// </remarks>
    /// <returns>Whether it was forgotten, rather than simply going because nothing of it was ever numbered.</returns>
    internal Task<bool> ForgetAsync(string id, DateTimeOffset at, CancellationToken ct) => _db.RunAsync<bool>(async () =>
    {
        bool numbered;
        await using (var probe = _db.Command())
        {
            probe.CommandText = "SELECT EXISTS (SELECT 1 FROM quest_log WHERE quest = $id AND remote IS NOT NULL)";
            probe.Parameters.AddWithValue("$id", id);
            numbered = Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false)) != 0;
        }

        await using var command = _db.Command();
        command.CommandText = (numbered ? "INSERT OR IGNORE INTO quest_forgotten (id, at) VALUES ($id, $at);" : "")
                              + "DELETE FROM quest_log WHERE quest = $id; DELETE FROM quest_forwards WHERE quest = $id; DELETE FROM quests WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$at", Moments.Write(at));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return numbered;
    }, ct);

    /// <summary>Whether this machine forgot the quest (HIST1b): it cleared it after a remote numbered it.</summary>
    public Task<bool> ForgottenAsync(string id, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM quest_forgotten WHERE id = $id)";
        command.Parameters.AddWithValue("$id", id);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct).ConfigureAwait(false)) != 0;
    }, ct);

    /// <summary>Every quest this machine forgot (HIST1b): what the quest fetch and the session fetch skip.</summary>
    public Task<IReadOnlySet<string>> ForgottenAsync(CancellationToken ct = default) =>
        InGateAsync(() => ForgottenAsync(transaction: null, ct), ct);

    private async Task<IReadOnlySet<string>> ForgottenAsync(SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = "SELECT id FROM quest_forgotten";
        var forgotten = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) forgotten.Add(reader.GetString(0));
        return forgotten;
    }

    /// <summary>
    /// A quest's history read inside a transaction the caller already holds — <see cref="HistoryDesk"/>'s, which judges a
    /// unit again where it clears it. The same read as <see cref="HistoryAsync(string, CancellationToken)"/>: the gate is
    /// reentrant within the work that holds it (SQLITETX1), and the read joins that work's transaction.
    /// </summary>
    internal Task<IReadOnlyList<QuestOperation>> HistoryWithinAsync(string id, CancellationToken ct) =>
        HistoryAsync(id, ct);

    /// <summary>
    /// The taken quests waiting on <paramref name="question"/> (D79) — what keeps a question from being
    /// deleted: deleted, the quests waiting on it would wait on nothing for good.
    /// </summary>
    public Task<IReadOnlyList<Quest>> WaitingOnAsync(string question, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<Quest>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT * FROM quests WHERE awaits = $question AND status = 'Taken' ORDER BY filed";
        command.Parameters.AddWithValue("$question", question);

        var quests = new List<Quest>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) quests.Add(Read(reader));
        return quests;
    }, ct);

    /// <summary>Every question a taken quest waits on (D79) — what <see cref="WaitingOnAsync"/> answers, for a whole list at once.</summary>
    public Task<IReadOnlySet<string>> AwaitedAsync(CancellationToken ct = default) => _db.RunAsync<IReadOnlySet<string>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT DISTINCT awaits FROM quests WHERE awaits IS NOT NULL AND status = 'Taken'";

        var awaited = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) awaited.Add(reader.GetString(0));
        return awaited;
    }, ct);

    // ——— A machine's half of the sync (D68 §3, design §8).

    /// <summary>The last number this machine fetched from a workspace's remote — zero before its first fetch.</summary>
    public Task<long> CursorAsync(string workspace, CancellationToken ct = default) =>
        InGateAsync(() => CursorAsync(workspace, transaction: null, ct), ct);

    private async Task<long> CursorAsync(string workspace, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        // None handed in keeps the one the command was made in, as HistoryAsync's does.
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = "SELECT number FROM quest_cursor WHERE workspace = $workspace";
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is { } number and not DBNull
            ? Convert.ToInt64(number)
            : 0;
    }

    /// <summary>
    /// Take what a workspace's remote accepted, in its order, and rebase what this machine has not
    /// pushed on top — through the one table. Answers the cursor it now stands at and every move that
    /// became a conflict on the way.
    /// </summary>
    /// <param name="workspace">The circle whose remote these came from: what a publish among them is filed under here (SYNC0a).</param>
    /// <param name="fetched">The remote's operations, each carrying its number.</param>
    /// <param name="through">The last number the fetch covered; the cursor moves there.</param>
    /// <remarks>
    /// An operation this machine already holds — its own, pushed and fetched back — takes the number it
    /// was given; any other is kept under the machine that made it. Then every quest the fetch touched
    /// is replayed: accepted operations by number, then pending ones. A pending move that no longer
    /// applies becomes a <see cref="QuestOperationKind.Conflict"/> and is never dropped (design §5).
    /// <para>An operation on a quest this machine forgot (<see cref="ForgetAsync"/>) is passed over, and the cursor still
    /// moves past it (HIST1b): a closed quest still takes a conflict or a dismissal from another machine, and kept, either
    /// would make half a quest here; a cursor back at zero would bring the whole of it.</para>
    /// </remarks>
    public Task<QuestIntegration> IntegrateAsync(
        string workspace, IReadOnlyList<QuestOperation> fetched, long through, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var circle = Workspaces.Normalize(workspace);
            var forgotten = await ForgottenAsync(transaction, inside).ConfigureAwait(false);
            var touched = new List<string>();
            foreach (var operation in fetched
                         .Where(o => o.Number is not null && !forgotten.Contains(o.Quest))
                         .OrderBy(o => o.Number))
            {
                if (await PositionOfAsync(operation.Machine, operation.Sequence, transaction, inside).ConfigureAwait(false)
                    is { } held)
                {
                    await NumberAsync(held, operation.Number!.Value, transaction, inside).ConfigureAwait(false);
                }
                else
                {
                    var filed = operation.Published is null
                        ? operation
                        : operation with { Published = operation.Published with { Workspace = circle } };
                    await KeepAsync(filed, operation.Number, transaction, inside).ConfigureAwait(false);
                }

                if (!touched.Contains(operation.Quest, StringComparer.Ordinal)) touched.Add(operation.Quest);
            }

            // A stray an older build left pending on a lost take (WAITCLAIM4) sits on a quest no fetch touches once the
            // take's conflict has come back, so it is rebased whether or not anything arrived for it.
            foreach (var quest in await StrayQuestsAsync(circle, transaction, inside).ConfigureAwait(false))
            {
                if (!forgotten.Contains(quest) && !touched.Contains(quest, StringComparer.Ordinal)) touched.Add(quest);
            }

            var conflicts = new List<QuestOperation>();
            foreach (var quest in touched)
            {
                conflicts.AddRange(await RebaseAsync(quest, transaction, inside).ConfigureAwait(false));
            }

            var cursor = Math.Max(await CursorAsync(circle, transaction, inside).ConfigureAwait(false), through);
            await using (var command = _db.Command())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO quest_cursor (workspace, number) VALUES ($workspace, $number)
                    ON CONFLICT (workspace) DO UPDATE SET number = excluded.number
                    """;
                command.Parameters.AddWithValue("$workspace", circle);
                command.Parameters.AddWithValue("$number", cursor);
                await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            return new QuestIntegration(cursor, conflicts);
        }, ct);

    /// <summary>
    /// Replay one quest — accepted first, pending on top — and rewrite what did not survive, each kind by
    /// <see cref="QuestLog.Lost"/>: a move becomes a conflict, a publish of an ask already held is the same
    /// ask again, and a follow-up that only a lost close published goes with it. A wait made on this machine's
    /// take that lost goes with the take, and the take's conflict names it (WAITCLAIM1); one made after the take was
    /// rewritten was refused by the store (WAITCLAIM2), and one an older build made then is lost here as the claim reads
    /// (WAITCLAIM4). Answers the conflicts made.
    /// </summary>
    private async Task<IReadOnlyList<QuestOperation>> RebaseAsync(
        string id, SqliteTransaction transaction, CancellationToken ct)
    {
        var conflicts = new List<QuestOperation>();
        Quest? quest = null;

        // Once this machine's take has lost, its later moves on the quest were made on a claim it never
        // held (D69): an offline session that finished would otherwise close the quest over the winner's
        // take, because the table allows done from taken. Every pending operation is this machine's.
        // A wait made on that take is part of its move (WAITCLAIM1): applied to the winner's quest, it would
        // hold the winner's session on a question it never asked. It goes with the take, and the take's
        // conflict, the loss this machine reports, names the question, so whoever asked learns the quest
        // no longer waits on it. A move or a wait made after the pass that rewrote the take never meets this
        // rule: the store's verbs refuse it as the claim reads (WAITCLAIM2), so it is never pending here.
        var claimLost = false;
        (long Position, int Reported)? lostTake = null;
        // The history as this rebase leaves it so far: what QuestLog.OnALostTake reads this machine's claim from.
        var replayed = new List<QuestOperation>();
        var losses = new QuestRebaseLosses(replayed, Machine);
        var history = await HistoryAsync(id, transaction, ct).ConfigureAwait(false);
        // SYNCMERGE1a (D178 point 5): a take this machine made to bring its lost take forward stands only with the dismissal marked with
        // it, written in the same write: a conflict another machine dismissed first authorizes no take, so where the mark would land
        // nowhere, the take loses too, kept as a conflict like any lost take, and the mark goes with it as the take's own.
        var bringing = history
            .Where(operation => operation is { Number: null, Kind: QuestOperationKind.Dismissed, Forward: not null })
            .DistinctBy(operation => operation.Forward)
            .ToDictionary(operation => operation.Forward!);
        foreach (var operation in history)
        {
            var lostClaim = claimLost && operation.Number is null && QuestLog.TakersOwn(operation);
            var unbrought = !lostClaim && operation is { Kind: QuestOperationKind.Taken, Number: null }
                            && bringing.TryGetValue(new QuestOperationRef(operation.Machine, operation.Sequence), out var mark)
                            && QuestLog.Applies(quest, operation) && !QuestLog.Brings(QuestLog.Step(quest, operation), mark);

            // A stray (WAITCLAIM4): a done, a decline or a wait a build before WAITCLAIM2 made after the take's conflict
            // was written, which the remote refuses at every pass (WAITCLAIM3), so the circle never pushed clean. Judged by
            // the rule the store's verbs and the remote judge by, and lost by its kind's rule as one made on a take this
            // rebase finds lost is. A forgotten wait is named on no conflict: the take's is already written, a remote may
            // have numbered it, and no machine rewrites a numbered operation. The remote never kept the wait, so no other
            // machine's copy of the quest waits on the question.
            var stray = !lostClaim && operation.Number is null && QuestLog.OnALostTake(quest, replayed, operation);
            if (!lostClaim && !stray && !unbrought && (operation.Number is not null || QuestLog.Applies(quest, operation)))
            {
                quest = QuestLog.Applies(quest, operation) ? QuestLog.Step(quest, operation) : quest;
                replayed.Add(operation);
                continue;
            }

            var position = (await PositionOfAsync(operation.Machine, operation.Sequence, transaction, ct)
                .ConfigureAwait(false))!.Value;

            // Each kind by its rule (QuestLog.Lost), which says why each is forgotten, kept or made a conflict.
            var loss = QuestLog.Lost(operation.Kind);
            if (loss == QuestLoss.Kept)
            {
                replayed.Add(operation);
                continue;
            }

            if (loss == QuestLoss.Forgotten)
            {
                await ForgetOperationAsync(position, transaction, ct).ConfigureAwait(false);
                conflicts.AddRange(await LostAsync(operation, quest, lostClaim, losses, transaction, ct).ConfigureAwait(false));
                continue;
            }

            // A move. On a quest another machine deleted first (D95) it is kept as a conflict on a quest no list shows,
            // so this machine's claim reads lost and its driver stops the session. A decline made while open that lost
            // to a take (PAUSE1c) is kept like any losing move, with its reason: the conflict says it attempted a
            // decline, and the flag, a decline's, goes with the kind. So does a take's source (FABRIC-CLOSE1c18): a conflict carries none,
            // and is reported as the log keeps it.
            var lost = operation with
            {
                Kind = QuestOperationKind.Conflict, Attempted = QuestTransitions.Target(operation.Kind), WhileOpen = false, Close = null,
                Submission = null,
            };

            // A step's take made on a submission the step no longer grew from (FABRIC-CLOSE1c25): another machine's chain published
            // the step anew on other work, so the take's own words alone would not say why it lost. Read from the stamp and the step
            // alone, as the source's own history may not be rewritten yet. A take that lost to the person's reopen of the source
            // (its rebind moves the step's GrewFrom too) keeps the take's words.
            if (operation.Kind == QuestOperationKind.Taken && operation.Submission is { } stamped
                && quest is { Status: QuestStatus.Open, SourceReopened: null, Parent: { Length: > 0 } source } && stamped != quest.GrewFrom
                && !QuestLog.LostToTheReopen(replayed.Append(lost), Machine))
            {
                lost = lost with { Note = StepPublishedAnew(source, operation.Note), NoteParts = null };
            }

            await RewriteAsync(position, lost, transaction, ct).ConfigureAwait(false);
            quest = quest is null ? null : QuestLog.Step(quest, lost);
            replayed.Add(lost);
            conflicts.Add(lost);
            if (operation.Kind == QuestOperationKind.Taken)
            {
                claimLost = true;
                lostTake = (position, conflicts.Count - 1);
            }

            conflicts.AddRange(await LostAsync(operation, quest, lostClaim, losses, transaction, ct).ConfigureAwait(false));
        }

        // The take's conflict names what went with it, after its own note (WAITCLAIM1). It is rewritten before
        // it is pushed, so every machine keeps the one note.
        if (lostTake is { } take && losses.Any)
        {
            // A note rewritten here has no parts of its own, so it keeps none: a stale lead would word a note it no longer is.
            var named = conflicts[take.Reported] with { Note = losses.Named(conflicts[take.Reported].Note), NoteParts = null };
            await RewriteAsync(take.Position, named, transaction, ct).ConfigureAwait(false);
            conflicts[take.Reported] = named;
            quest = quest is null ? null : quest with
            {
                Conflicts = quest.Conflicts
                    .Select(c => c.Machine == named.Machine && c.Sequence == named.Sequence ? c with { Note = named.Note } : c)
                    .ToList(),
            };
        }

        // A quest a fetched delete ended leaves the cache, as it left every other machine's (D95).
        if (quest is not null) await WriteCacheAsync(quest, transaction, ct).ConfigureAwait(false);
        else await DropCacheAsync(id, transaction, ct).ConfigureAwait(false);
        return conflicts;
    }

    /// <summary>What else a lost operation's kind takes with it in the rebase's write (<see cref="IQuestOperation.LostAsync"/>).</summary>
    private async Task<IReadOnlyList<QuestOperation>> LostAsync(
        QuestOperation operation, Quest? standing, bool takeLost, QuestRebaseLosses losses, SqliteTransaction transaction,
        CancellationToken ct) =>
        await Operations.For(operation.Kind)!.LostAsync(new QuestTx(this, transaction, ct), operation, standing, takeLost, losses)
            .ConfigureAwait(false);

    /// <summary>
    /// The circle's quests where this machine holds a done, a decline, a wait or a set-up not yet pushed beside a take of its
    /// own that is a conflict (WAITCLAIM4, REVIEWENV1b): where a stray can be. <see cref="RebaseAsync"/> judges each operation by
    /// <see cref="QuestLog.OnALostTake"/>, so a quest this finds and nothing strays on is replayed as it stands.
    /// </summary>
    private async Task<IReadOnlyList<string>> StrayQuestsAsync(string circle, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT DISTINCT stray.quest FROM quest_log AS stray
            JOIN quest_log AS asked ON asked.position = (
              SELECT MIN(first.position) FROM quest_log AS first
              WHERE first.quest = stray.quest AND first.kind = 'published')
            WHERE stray.remote IS NULL AND stray.machine = $machine AND stray.kind IN ('done', 'declined', 'waited', 'setup')
              AND json_extract(asked.payload, '$.workspace') = $workspace COLLATE NOCASE
              AND EXISTS (
                SELECT 1 FROM quest_log AS lost
                WHERE lost.quest = stray.quest AND lost.machine = $machine AND lost.kind = 'conflict'
                  AND json_extract(lost.payload, '$.attempted') = 'Taken' COLLATE NOCASE)
            ORDER BY stray.quest
            """;
        command.Parameters.AddWithValue("$machine", Machine);
        command.Parameters.AddWithValue("$workspace", circle);

        var quests = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) quests.Add(reader.GetString(0));
        return quests;
    }

    /// <summary>Take a quest's row out of the cache — the replay says there is no quest (D95).</summary>
    private async Task DropCacheAsync(string id, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM quests WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A follow-up published only by a close that has now lost (D65 §4): it was the chain moving on,
    /// and the chain did not move. Forgotten only while every operation of it is this machine's and none was
    /// pushed; one another machine holds, or published itself on its own close, stays.
    /// </summary>
    /// <remarks>
    /// FABRIC-CLOSE1e2: what this machine did on it goes with it, as a lost submission's step's does (<see cref="ForgetStepsAsync"/>): a
    /// take of it becomes a conflict naming the close that went, so its claim reads lost and its session is stopped, where it once stood
    /// and synced on its own, numbered on a done that lost, until a person declined it. A remote holds no such follow-up while its parent
    /// is not done there (<see cref="AwaitsItsSourceAsync"/>), so the raced order reaches this too.
    /// </remarks>
    /// <param name="published">What published it, as its take's conflict names it: the parent's close, or the yes or verdict that let it go.</param>
    /// <returns>The takes rewritten, as the rebase reports its conflicts.</returns>
    internal Task<IReadOnlyList<QuestOperation>> ForgetFollowUpsAsync(
        string parent, bool takeLost, string published, bool reopened, SqliteTransaction transaction, CancellationToken ct) =>
        ForgetStepsAsync(parent, takeLost, reopened, transaction, ct, published);

    /// <summary>
    /// The steps a lost submission published (FABRIC-CLOSE1e, §E5), with what this machine did on them since, and the steps they
    /// published in turn: each grew from a submission that no longer stands, so none was asked. A step is forgotten only while
    /// every operation of it is this machine's and none was pushed; one a remote holds is every machine's, and stays. A lost close's
    /// follow-ups go the same way (<see cref="ForgetFollowUpsAsync"/>, FABRIC-CLOSE1e2).
    /// </summary>
    /// <remarks>
    /// Where this machine took a step, its take is not forgotten but rewritten as a conflict, which the claim reads as lost
    /// (<see cref="QuestLog.Claim"/>), so the driver stops the step's session as it stops any session whose take lost (D69). Its
    /// publication gone, the conflict is on a quest no list shows and no push carries, as one on a quest deleted first is (D95),
    /// and its note says why: the parent's submission went. Should the winner's chain publish the same step later (its id comes
    /// from the parent and the step), the conflict stands on it and goes up, as any take that lost is shown on the winner's quest.
    /// <para>Where the parent's take lost to the person's reopen (<paramref name="reopened"/>, FABRIC-CLOSE1c22), the conflict says so in
    /// its words and in <see cref="QuestOperation.LostToTheReopen"/>: the step's history holds only the conflict, so the claim's answer
    /// (<see cref="LostToTheReopenAsync"/>) could not read the reopen from it, and the driver would stop the session in another
    /// machine's words.</para>
    /// </remarks>
    /// <param name="published">What of the parent published it, as its take's conflict names it: its submission, or its close.</param>
    /// <returns>The takes rewritten, as the rebase reports its conflicts.</returns>
    private async Task<IReadOnlyList<QuestOperation>> ForgetStepsAsync(
        string parent, bool takeLost, bool reopened, SqliteTransaction transaction, CancellationToken ct, string published = "submission")
    {
        var children = new List<string>();
        await using (var command = _db.Command())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT id FROM quests WHERE parent = $parent";
            command.Parameters.AddWithValue("$parent", parent);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) children.Add(reader.GetString(0));
        }

        var told = new List<QuestOperation>();
        foreach (var child in children)
        {
            var history = await HistoryAsync(child, transaction, ct).ConfigureAwait(false);
            if (history is not [{ Kind: QuestOperationKind.Published }, ..]
                || history.Any(operation => operation.Number is not null || operation.Machine != Machine))
            {
                continue;
            }

            told.AddRange(await ForgetStepsAsync(child, takeLost, reopened, transaction, ct, published).ConfigureAwait(false));
            foreach (var operation in history)
            {
                var position = (await PositionOfAsync(operation.Machine, operation.Sequence, transaction, ct).ConfigureAwait(false))!.Value;
                if (operation.Kind != QuestOperationKind.Taken)
                {
                    await ForgetOperationAsync(position, transaction, ct).ConfigureAwait(false);
                    continue;
                }

                var lost = operation with
                {
                    Kind = QuestOperationKind.Conflict, Attempted = QuestStatus.Taken,
                    Note = StepWentWith(parent, takeLost, operation.Note, reopened, published),
                    NoteParts = null, WhileOpen = false, Close = null, Submission = null, LostToTheReopen = reopened,
                };
                await RewriteAsync(position, lost, transaction, ct).ConfigureAwait(false);
                told.Add(lost);
            }

            await DropCacheAsync(child, transaction, ct).ConfigureAwait(false);
        }

        return told;
    }

    /// <summary>
    /// A lost step's take's note as it was given, then why the step went (FABRIC-CLOSE1e): the parent's submission that published
    /// it no longer stands, with the parent's take or to another machine's move on the parent. A parent's take lost to the person's
    /// reopen (<paramref name="reopened"/>, FABRIC-CLOSE1c22) has no winner, so it says the reopen, as the take's refusals do
    /// (<see cref="QuestExchange.LostTake"/>). A step a close published names that close instead (<paramref name="published"/>,
    /// FABRIC-CLOSE1e2).
    /// </summary>
    internal static string StepWentWith(string parent, bool takeLost, string? note, bool reopened = false, string published = "submission")
    {
        var said = $"This quest was the next step of #{parent}'s {published}, which "
                   + (!takeLost ? $"lost to another machine's move on #{parent}"
                      : reopened ? $"went with this machine's take of #{parent}: the person's reopen of the work it grew from reached the "
                                   + "remote first"
                      : $"went with this machine's take of #{parent}: another machine's take reached the remote first")
                   + ". The step went with it, so its work is not wanted.";
        return string.IsNullOrWhiteSpace(note) ? said : $"{note.TrimEnd()} {said}";
    }

    /// <summary>
    /// A lost step's take's note as it was given, then why it lost (FABRIC-CLOSE1c25): the step was published anew on other work,
    /// from the submission another machine made of <paramref name="parent"/>, so a take made on the work that went is not wanted.
    /// </summary>
    internal static string StepPublishedAnew(string parent, string? note)
    {
        var said = $"This quest was the next step of #{parent}'s submission, and was published anew on other work, from the submission "
                   + $"another machine made of #{parent}. This machine's take of it, made on the work that went, is not wanted.";
        return string.IsNullOrWhiteSpace(note) ? said : $"{note.TrimEnd()} {said}";
    }

    private async Task NumberAsync(long position, long number, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = "UPDATE quest_log SET remote = $number WHERE position = $position";
        command.Parameters.AddWithValue("$number", number);
        command.Parameters.AddWithValue("$position", position);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Drop one pending operation a rebase found was never anybody's decision (a quest's clear is <see cref="ForgetAsync"/>).</summary>
    private async Task ForgetOperationAsync(long position, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM quest_log WHERE position = $position AND remote IS NULL";
        command.Parameters.AddWithValue("$position", position);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Rewrite a pending operation in place — a rebase rewriting a commit nobody else has seen.</summary>
    private async Task RewriteAsync(
        long position, QuestOperation operation, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText =
            "UPDATE quest_log SET kind = $kind, payload = $payload WHERE position = $position AND remote IS NULL";
        command.Parameters.AddWithValue("$kind", KindText(operation.Kind));
        command.Parameters.AddWithValue("$payload", PayloadJson(operation));
        command.Parameters.AddWithValue("$position", position);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// What this machine has not pushed for a workspace's quests, in the order it was made — only for
    /// quests whose receiver <paramref name="shared"/> says may leave the machine (design §8).
    /// </summary>
    /// <param name="shared">
    /// Whether a receiver is joined in this workspace — here or on a teammate's machine. Silence means
    /// local: a quest to anything else never appears here, however long it waits.
    /// </param>
    /// <remarks>
    /// The receiver and the circle are read from the quest's own first publish in the log, not from the
    /// cache: a deleted quest has no row, and its tombstone must travel all the same (D95). The two
    /// agree for every quest that has one, since the row is the publish replayed.
    /// </remarks>
    public Task<IReadOnlyList<QuestOperation>> PendingAsync(
        string workspace, Func<string, bool> shared, CancellationToken ct = default) =>
        InGateAsync<IReadOnlyList<QuestOperation>>(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = $"""
                SELECT {OperationColumns}, json_extract(asked.payload, '$.to') FROM quest_log
                JOIN quest_log AS asked ON asked.position = (
                  SELECT MIN(first.position) FROM quest_log AS first
                  WHERE first.quest = quest_log.quest AND first.kind = 'published')
                WHERE quest_log.remote IS NULL
                  AND json_extract(asked.payload, '$.workspace') = $workspace COLLATE NOCASE
                ORDER BY quest_log.position
                """;
            command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));

            var pending = new List<QuestOperation>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                if (shared(reader.GetString(7))) pending.Add(ReadOperation(reader));
            }

            return pending;
        }, ct);

    /// <summary>
    /// Where THIS machine's claim on a quest stands (D68 §4, D69): held once a remote numbered its take,
    /// unconfirmed while the take is only here, lost once the rebase made it a conflict — and none when
    /// this machine never took it. A take on a machine with no remote is never numbered, so it stays
    /// unconfirmed, which nothing acts on.
    /// </summary>
    public Task<QuestClaim> ClaimAsync(string id, CancellationToken ct = default) =>
        InGateAsync(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = $"SELECT {OperationColumns} FROM quest_log WHERE quest = $id AND machine = $machine";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$machine", Machine);

            var mine = new List<QuestOperation>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) mine.Add(ReadOperation(reader));
            return QuestLog.Claim(mine, Machine);
        }, ct);

    /// <summary>
    /// Whether THIS machine's claim on a quest reads lost because its take lost to the person's reopen of the work the step grew from
    /// (FABRIC-CLOSE1c18, <see cref="QuestLog.LostToTheReopen"/>), not to another machine's take: what the claim's answer says beside
    /// it, so the driver stops the session in words that are true. False where the claim does not read lost.
    /// </summary>
    public async Task<bool> LostToTheReopenAsync(string id, CancellationToken ct = default)
    {
        if (await ClaimAsync(id, ct).ConfigureAwait(false) != QuestClaim.Lost) return false;
        return QuestLog.LostToTheReopen(await HistoryAsync(id, ct).ConfigureAwait(false), Machine);
    }

    /// <summary>
    /// How a circle's pass ended (SYNC6a). A pass that reached the remote moves <see cref="QuestStanding.Synced"/>;
    /// one that hit a wall keeps it and names the wall, because when the circle last synced and why
    /// the last try did not are two facts.
    /// </summary>
    public Task RecordPassAsync(string workspace, QuestSyncReport report, DateTimeOffset at, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            await using var command = _db.Command();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO quest_passes (workspace, synced, tried, behind, problem, closure)
                VALUES ($workspace, $synced, $tried, $behind, $problem, $closure)
                ON CONFLICT (workspace) DO UPDATE SET
                  synced = COALESCE($synced, synced), tried = $tried, behind = $behind, problem = $problem, closure = $closure
                """;
            var circle = Workspaces.Normalize(workspace);
            command.Parameters.AddWithValue("$workspace", circle);
            command.Parameters.AddWithValue("$synced", report.Problem is null ? Moments.Write(at) : DBNull.Value);
            command.Parameters.AddWithValue("$tried", Moments.Write(at));
            // A list of quest ids, kept the way links are: a plain array of strings.
            command.Parameters.AddWithValue("$behind", LinksJson(report.Behind));
            command.Parameters.AddWithValue("$problem", (object?)report.Problem ?? DBNull.Value);
            // FABRIC-CLOSE1i: the undeclared machines, a plain array of ids; none where the pass heard no handshake, a wall's included.
            command.Parameters.AddWithValue("$closure", report.Closure is { } closure ? LinksJson(closure.Undeclared) : DBNull.Value);
            var written = await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            Heard(circle, report.Closure);
            return written;
        }, ct);

    /// <summary>
    /// The remote's handshake as <paramref name="workspace"/>'s last pass heard it (FABRIC-CLOSE1i): null where no pass has, a wall, or a
    /// remote before it. Read without the store's gate, so a take's own write can ask it: kept beside the row its pass recorded.
    /// </summary>
    public QuestClosure? ClosureOf(string workspace) =>
        _closures.TryGetValue(Workspaces.Normalize(workspace), out var closure) ? closure : null;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, QuestClosure> _closures = new(StringComparer.OrdinalIgnoreCase);

    private void Heard(string circle, QuestClosure? closure)
    {
        if (closure is null) _closures.TryRemove(circle, out _);
        else _closures[circle] = closure;
    }

    /// <summary>Each circle's handshake as its last pass recorded it (FABRIC-CLOSE1i), read as the store opens.</summary>
    private async Task LoadClosuresAsync(CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT workspace, closure FROM quest_passes WHERE closure IS NOT NULL";
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            Heard(reader.GetString(0), new QuestClosure(ReadLinks(reader.GetString(1))));
        }
    }

    /// <summary>
    /// A remote's half of the workspace's handshake (FABRIC-CLOSE1i): what a push said of its machine. A push declaring
    /// <paramref name="declares"/> marks that machine declared; one declaring none, as a build before it pushes, marks every machine its
    /// operations name undeclared, so a machine run back to an older build is heard as one. A push declaring one machine says nothing
    /// of another its operations name. A machine it marks is counted again where a person had retired it (FABRIC-CLOSE1i2).
    /// </summary>
    public Task DeclaredAsync(string? declares, IReadOnlyList<QuestOperation> pushed, DateTimeOffset at, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            List<(string Machine, bool Closure)> marked = declares is { Length: > 0 } declared
                ? [(declared, true)]
                : [.. pushed.Select(o => o.Machine).Where(m => m.Length > 0).Distinct(StringComparer.Ordinal).Select(m => (m, false))];
            foreach (var (machine, closure) in marked)
            {
                await using var command = _db.Command();
                command.Transaction = transaction;
                command.CommandText = """
                    DELETE FROM quest_retired WHERE machine = $machine;
                    INSERT INTO quest_declared (machine, closure, at) VALUES ($machine, $closure, $at)
                    ON CONFLICT (machine) DO UPDATE SET closure = excluded.closure, at = excluded.at
                    """;
                command.Parameters.AddWithValue("$machine", machine);
                command.Parameters.AddWithValue("$closure", closure ? 1 : 0);
                command.Parameters.AddWithValue("$at", Moments.Write(at));
                await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            return marked.Count;
        }, ct);

    /// <summary>
    /// A remote's answer to the workspace's handshake (FABRIC-CLOSE1i): every machine it holds an operation or a push from, but itself,
    /// whose last push did not declare <c>closure</c>, ordered by id. A shared host holds one workspace by construction, so these are the
    /// workspace's machines as far as it has heard them; one that has never pushed has made nothing here to misread. A machine a person
    /// retired here is not counted until a push marks it again (FABRIC-CLOSE1i2).
    /// </summary>
    public Task<QuestClosure> ClosureAsync(CancellationToken ct = default) =>
        InGateAsync(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = """
                SELECT machine FROM (SELECT DISTINCT machine FROM quest_log UNION SELECT machine FROM quest_declared)
                WHERE machine <> $self AND machine NOT IN (SELECT machine FROM quest_declared WHERE closure = 1)
                  AND machine NOT IN (SELECT machine FROM quest_retired)
                ORDER BY machine
                """;
            command.Parameters.AddWithValue("$self", Machine);
            var undeclared = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) undeclared.Add(reader.GetString(0));
            return new QuestClosure(undeclared);
        }, ct);

    /// <summary>
    /// A person's retirement of <paramref name="machine"/> at a remote (FABRIC-CLOSE1i2): a machine whose store was deleted, or that
    /// left the workspace, no longer counted by <see cref="ClosureAsync"/>, until a push marks it again (<see cref="DeclaredAsync"/>).
    /// False, and nothing kept, where the remote has heard nothing from it: no operation and no push, or the remote itself.
    /// </summary>
    public Task<bool> RetireMachineAsync(string machine, DateTimeOffset at, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            await using var command = _db.Command();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO quest_retired (machine, at)
                SELECT $machine, $at
                WHERE $machine <> $self
                  AND (EXISTS (SELECT 1 FROM quest_log WHERE machine = $machine) OR EXISTS (SELECT 1 FROM quest_declared WHERE machine = $machine))
                ON CONFLICT (machine) DO UPDATE SET at = excluded.at
                """;
            command.Parameters.AddWithValue("$machine", machine);
            command.Parameters.AddWithValue("$self", Machine);
            command.Parameters.AddWithValue("$at", Moments.Write(at));
            return await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false) > 0;
        }, ct);

    /// <summary>
    /// The remote's handshake as a retirement there answered it (FABRIC-CLOSE1i2), kept as <paramref name="workspace"/>'s last pass keeps
    /// what it heard, so a bind reads it at once rather than at the next pass. Nothing where no pass has recorded the circle yet: its
    /// first pass hears the remote for itself.
    /// </summary>
    public Task HeardClosureAsync(string workspace, QuestClosure closure, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var circle = Workspaces.Normalize(workspace);
            await using var command = _db.Command();
            command.Transaction = transaction;
            command.CommandText = "UPDATE quest_passes SET closure = $closure WHERE workspace = $workspace";
            command.Parameters.AddWithValue("$workspace", circle);
            command.Parameters.AddWithValue("$closure", LinksJson(closure.Undeclared));
            var written = await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            if (written > 0) Heard(circle, closure);
            return written;
        }, ct);

    /// <summary>
    /// Where a circle stands (SYNC6a): what this machine has not pushed, the quests carrying a conflict,
    /// and how its last pass ended.
    /// </summary>
    /// <param name="shared">Whether a receiver may leave the machine — what <see cref="PendingAsync"/> reads.
    /// What never leaves is not ahead of anything.</param>
    public async Task<QuestStanding> StandingAsync(
        string workspace, Func<string, bool> shared, CancellationToken ct = default)
    {
        var circle = Workspaces.Normalize(workspace);
        var ahead = (await PendingAsync(circle, shared, ct).ConfigureAwait(false)).Count;
        return await InGateAsync(async () =>
        {
            var conflicts = new List<string>();
            await using (var command = _db.Command())
            {
                command.CommandText =
                    "SELECT id FROM quests WHERE workspace = $workspace COLLATE NOCASE AND conflicts <> '[]' ORDER BY updated DESC";
                command.Parameters.AddWithValue("$workspace", circle);
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false)) conflicts.Add(reader.GetString(0));
            }

            await using (var command = _db.Command())
            {
                command.CommandText = "SELECT synced, tried, behind, problem FROM quest_passes WHERE workspace = $workspace";
                command.Parameters.AddWithValue("$workspace", circle);
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    return new QuestStanding(
                        ahead,
                        ReadLinks(reader.GetString(2)),
                        conflicts,
                        reader.IsDBNull(0) ? null : Moments.ReadRequired(reader.GetString(0)),
                        Moments.ReadRequired(reader.GetString(1)),
                        reader.IsDBNull(3) ? null : reader.GetString(3));
                }
            }

            return new QuestStanding(ahead, [], conflicts, Synced: null, Tried: null, Problem: null);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Record the numbers a push was given — the operations stop being pending.</summary>
    public Task AcceptedAsync(IReadOnlyList<QuestAcceptance> accepted, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            foreach (var acceptance in accepted)
            {
                if (await PositionOfAsync(acceptance.Machine, acceptance.Sequence, transaction, inside).ConfigureAwait(false)
                    is { } position)
                {
                    await NumberAsync(position, acceptance.Number, transaction, inside).ConfigureAwait(false);
                }
            }

            return accepted.Count;
        }, ct);

    // ——— A remote's half.

    /// <summary>What this store accepted after <paramref name="since"/>, in order — numbered by position.</summary>
    /// <param name="limit">A page; <see cref="QuestFetch.More"/> says whether another follows.</param>
    public Task<QuestFetch> OperationsSinceAsync(long since, int limit = 500, CancellationToken ct = default) =>
        InGateAsync(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = $"""
                SELECT {OperationColumns}, position FROM quest_log
                WHERE position > $since ORDER BY position LIMIT $take
                """;
            command.Parameters.AddWithValue("$since", since);
            command.Parameters.AddWithValue("$take", limit + 1);

            var operations = new List<QuestOperation>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                operations.Add(ReadOperation(reader) with { Number = reader.GetInt64(7) });
            }

            var more = operations.Count > limit;
            var page = more ? operations[..limit] : operations;
            return new QuestFetch(page, page.Count == 0 ? since : page[^1].Number!.Value, more);
        }, ct);

    /// <summary>
    /// Judge a push, quest by quest (design §8): <b>behind</b> when anything reached the quest after
    /// <paramref name="base"/> that this push did not carry, or it is a step whose source holds no submission it grew from here
    /// (<see cref="AwaitsItsSourceAsync"/>, FABRIC-CLOSE1c21), or, naming none, whose parent is not done here (FABRIC-CLOSE1e2);
    /// <b>refused</b> when an operation does not
    /// apply through the table, is made on a take its machine lost (<see cref="QuestLog.OnALostTake"/>,
    /// WAITCLAIM3), or is a publish that fails <paramref name="judge"/>; otherwise every operation is
    /// kept under the machine that made it and numbered.
    /// </summary>
    /// <param name="base">The number the pushing machine rebased on — its cursor.</param>
    /// <param name="judge">The exchange's say over a publish, null when it is fit.</param>
    /// <param name="workspaceOf">Where a publish is filed HERE — the receiving side's wiring, never the push's (SYNC0a).</param>
    public Task<QuestPush> ReceiveAsync(
        long @base, IReadOnlyList<QuestOperation> pushed, Func<Quest, string?> judge, Func<Quest, string> workspaceOf,
        CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var accepted = new List<QuestAcceptance>();
            var behind = new List<string>();
            var refused = new List<QuestPushRefusal>();
            var carried = pushed.Select(o => (o.Machine, o.Sequence)).ToHashSet();

            foreach (var group in pushed.GroupBy(o => o.Quest, StringComparer.Ordinal))
            {
                var fresh = new List<QuestOperation>();
                foreach (var operation in group)
                {
                    // Already here — a push retried after its answer was lost: the number it was given.
                    if (await PositionOfAsync(operation.Machine, operation.Sequence, transaction, inside).ConfigureAwait(false)
                        is { } held)
                    {
                        accepted.Add(new(operation.Machine, operation.Sequence, held));
                    }
                    else
                    {
                        fresh.Add(operation);
                    }
                }

                if (fresh.Count == 0) continue;
                if (await MovedSinceAsync(group.Key, @base, carried, transaction, inside).ConfigureAwait(false)
                    || await AwaitsItsSourceAsync(fresh[0], transaction, inside).ConfigureAwait(false))
                {
                    behind.Add(group.Key);
                    continue;
                }

                var history = await HistoryAsync(group.Key, transaction, inside).ConfigureAwait(false);
                var quest = QuestLog.Replay(history);
                // Whether this deployment has held the quest at all — a delete leaves a history and no quest.
                var known = history.Count > 0;
                string? why = null;
                var staged = new List<QuestOperation>();
                foreach (var operation in fresh)
                {
                    var filed = operation.Published is null
                        ? operation
                        : operation with { Published = operation.Published with { Workspace = workspaceOf(operation.Published) } };
                    why = filed.Published is { } asked ? judge(asked) : null;

                    var onDeleted = quest is null && known && Operations.For(filed.Kind) is { KeptOnADeletedQuest: true };
                    if (why is null && !onDeleted && !QuestLog.Applies(quest, filed))
                    {
                        why = quest is null
                            ? known
                                ? $"`{KindText(filed.Kind)}` on quest `#{group.Key}`, which was deleted here."
                                : $"`{KindText(filed.Kind)}` on quest `#{group.Key}`, which this deployment has never had published."
                            : filed.WhileOpen
                                // PAUSE1c: named as the decline it is, so the refusal its machine relays says why it did not land; a
                                // publication's rebind likewise (FABRIC-CLOSE1c15).
                                ? $"`{KindText(filed.Kind)}` applies to quest `#{group.Key}` only while it is open (`whileOpen`), "
                                  + $"and it is {quest.Status} here, so the quest stays {quest.Status}."
                                : filed is { Kind: QuestOperationKind.Taken, Submission: { } found } && quest.Status == QuestStatus.Open
                                    // FABRIC-CLOSE1c18: a take made on the step as it grew from work the person reopened since.
                                    ? $"`taken` on quest `#{group.Key}` was made on the step as it grew from submission "
                                      + $"`{found.Machine}:{found.Sequence}`, and the person's reopen has moved the step from that work since, "
                                      + "so the take does not apply and the step stays Open."
                                    : $"`{KindText(filed.Kind)}` does not apply to quest `#{group.Key}`, which is {quest.Status}.";
                    }

                    // A done, a decline or a wait from a machine whose take on the quest lost (WAITCLAIM3): made after the
                    // pass that found the take lost, by a build whose store does not refuse it (WAITCLAIM2). The table
                    // allows it, and applied it would close or hold the winner's quest. Refused by the rule the store
                    // judges by, in the words the store's refusal is answered in, so the pushing machine's pass relays the
                    // lost take and its stand-down.
                    // FABRIC-CLOSE1c18: on an open step the take was lost to the person's reopen, and is said so; or it lost with the
                    // work it was made on, the step published anew on another submission (FABRIC-CLOSE1c24), said as that.
                    if (why is null && QuestLog.OnALostTake(quest, [.. history, .. staged], filed))
                    {
                        var open = quest is { Status: QuestStatus.Open };
                        var reopened = open && QuestLog.LostToTheReopen([.. history, .. staged], filed.Machine);
                        why = QuestExchange.LostTake(group.Key, filed.Kind == QuestOperationKind.Waited
                            ? $"so it does not wait on `#{filed.Note}`. The remote kept nothing this push carried for the quest; "
                              + $"`#{filed.Note}` stays a quest of its own."
                            : $"so this `{KindText(filed.Kind)}` is not this machine's to make. The remote kept nothing this push "
                              + "carried for the quest.", reopened, outgrown: open && !reopened);
                    }

                    if (why is not null) break;
                    if (!onDeleted) quest = QuestLog.Step(quest, filed);
                    known = true;
                    staged.Add(filed);
                }

                if (why is not null)
                {
                    refused.Add(new(group.Key, why));
                    continue;
                }

                foreach (var operation in staged)
                {
                    var number = await KeepAsync(operation, number: null, transaction, inside).ConfigureAwait(false);
                    accepted.Add(new(operation.Machine, operation.Sequence, number));
                }

                if (quest is not null) await WriteCacheAsync(quest, transaction, inside).ConfigureAwait(false);
                else await DropCacheAsync(group.Key, transaction, inside).ConfigureAwait(false);
            }

            return new QuestPush(accepted, behind, refused);
        }, ct);

    /// <summary>
    /// Whether a pushed step's publication waits on the submission it grew from (FABRIC-CLOSE1c21, §E5): this store holds the step's
    /// source quest but not that submission, so the source's own operations did not land in this push (behind, or refused). Kept alone,
    /// the step would stand here on work its machine's rebase may yet lose with a take, as a take lost to the person's reopen loses its
    /// submission and the step it published (FABRIC-CLOSE1e's *Left*, the parent behind). Answered behind, so the machine goes round:
    /// its rebase forgets the step with the submission, or pushes it again beside it. The source's group comes first in a push, since its
    /// submission was written before the step's publication, so a submission kept by this push is held here by then. A source this store
    /// never held (its receiver stays on its machine) waits on nothing.
    /// <para>A step naming no source (FABRIC-CLOSE1e2) was published by its parent's close, in the close's own write, so it waits until this
    /// store holds the parent done; a done is terminal here, so once held it is held for good. Kept alone, it would stand on a close the
    /// machine's rebase loses, as CLOSE1e's <i>Left</i> had it for a lost done's follow-up; answered behind, the machine goes round and its
    /// rebase forgets the follow-up with the done (<see cref="ForgetFollowUpsAsync"/>), or pushes the done again beside it. A parent deleted
    /// here was never done, since a delete takes only an open quest nobody moved, so its follow-up waits too, and goes as the done does.</para>
    /// </summary>
    private async Task<bool> AwaitsItsSourceAsync(QuestOperation first, SqliteTransaction transaction, CancellationToken ct) =>
        first is { Kind: QuestOperationKind.Published, Published: { Parent: { Length: > 0 } parent } step }
        && await HistoryAsync(parent, transaction, ct).ConfigureAwait(false) is { Count: > 0 } history
        && (step.GrewFrom is { } source
            ? await PositionOfAsync(source.Machine, source.Sequence, transaction, ct).ConfigureAwait(false) is null
            // FABRIC-CLOSE1e2: a step naming no source was published by its parent's close (a chain's next step, or a set-up step, which
            // follows a done one), so it waits until this store holds the parent done: a close this push did not land here.
            : QuestLog.Replay(history) is not { Status: QuestStatus.Done });

    /// <summary>Whether anything this push did not carry reached a quest after <paramref name="base"/>.</summary>
    private async Task<bool> MovedSinceAsync(
        string quest, long @base, IReadOnlySet<(string Machine, long Sequence)> carried,
        SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = "SELECT machine, sequence FROM quest_log WHERE quest = $quest AND position > $base";
        command.Parameters.AddWithValue("$quest", quest);
        command.Parameters.AddWithValue("$base", @base);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (!carried.Contains((reader.GetString(0), reader.GetInt64(1)))) return true;
        }

        return false;
    }

    /// <summary>
    /// A read inside the connection's gate, as every command here is (SQLITETX1): it never meets another
    /// request's open transaction, so a pending operation is never read from one that then rolled back.
    /// </summary>
    private Task<T> InGateAsync<T>(Func<Task<T>> read, CancellationToken ct) => _db.RunAsync(read, ct);

    /// <summary>The columns <see cref="ReadOperation"/> reads, in its order.</summary>
    internal const string OperationColumns =
        "quest_log.quest, quest_log.kind, quest_log.machine, quest_log.sequence, quest_log.at, quest_log.payload, quest_log.remote";

    public Task<Quest?> FindAsync(string id, CancellationToken ct = default) =>
        InGateAsync(() => FindAsync(id, transaction: null, ct), ct);

    internal async Task<Quest?> FindAsync(string id, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        // None handed in keeps the one the command was made in, as HistoryAsync's does.
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = "SELECT * FROM quests WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>
    /// Quests addressed to one repository, or every quest when none is named.
    /// </summary>
    /// <remarks>
    /// Open and taken first: what is outstanding is the question worth asking, and a finished queue
    /// pushing live work off the end is how a list stops being read. A done that a departure holds for the
    /// person's yes is outstanding too (DRIFT1d): it waits on someone, and what follows it waits with it — the
    /// driver keeps a quest waiting on it waiting because it is listed here.
    /// </remarks>
    public Task<IReadOnlyList<Quest>> ListAsync(
        string? receiver = null, bool includeClosed = false, string? workspace = null,
        CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<Quest>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"""
            SELECT * FROM quests
            WHERE ($receiver IS NULL OR receiver = $receiver)
              AND ($workspace IS NULL OR workspace = $workspace COLLATE NOCASE)
              {(includeClosed ? "" : "AND (status IN ('Open', 'Taken') OR held = 1)")}
            ORDER BY CASE status WHEN 'Open' THEN 0 WHEN 'Taken' THEN 1 ELSE 2 END, filed
            """;
        command.Parameters.AddWithValue("$receiver", (object?)receiver ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$workspace", workspace is null ? DBNull.Value : Workspaces.Normalize(workspace));

        var quests = new List<Quest>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) quests.Add(Read(reader));
        return quests;
    }, ct);

    /// <summary>
    /// Every quest one sender asked, closed ones included — or, <paramref name="startingWith"/>, every
    /// quest whose sender begins with it. What an ask's standing is read from (USE1c): its quests are
    /// the ones asked BY it, chain steps included, whichever machine last moved them.
    /// </summary>
    public Task<IReadOnlyList<Quest>> FromAsync(
        string sender, bool startingWith = false, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<Quest>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = startingWith
            ? "SELECT * FROM quests WHERE substr(sender, 1, length($sender)) = $sender ORDER BY filed"
            : "SELECT * FROM quests WHERE sender = $sender ORDER BY filed";
        command.Parameters.AddWithValue("$sender", sender);

        var quests = new List<Quest>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) quests.Add(Read(reader));
        return quests;
    }, ct);

}
