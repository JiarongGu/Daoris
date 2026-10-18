using Microsoft.Data.Sqlite;

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
    /// <see cref="QuestOperationKind.Rebound"/> names the submission it grows from now.
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

    /// <summary>The quest's circle syncs through a remote now, enabled since the graph was named.</summary>
    Remote,
}

/// <summary>
/// Who takes a quest (FABRIC-CLOSE1o): the session the authenticated intake names, null for a door that names none, and how this
/// machine's remotes read at the moment of the take, by circle. What a take needs to judge the bind named for it, inside its write.
/// </summary>
/// <param name="Session">The taking session, from its connector's intake (<c>DAORIS_SESSION_ID</c>); null where no session is named.</param>
/// <param name="Remote">Whether a circle syncs through a remote, by the map sync reads, asked inside the take's write; null reads none.</param>
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
public sealed record QuestReopen(string Machine, long Sequence, QuestOperationRef Of, string? Note, DateTimeOffset At);

/// <summary>Why the person's reopen, or their word to go on, moved nothing (FABRIC-CLOSE1c, §E5), or <see cref="None"/>.</summary>
public enum QuestReopenRefusal
{
    None,

    /// <summary>No quest under that id.</summary>
    NotFound,

    /// <summary>Not taken: open, or closed done or declined, so it has no submission to reopen.</summary>
    NotTaken,

    /// <summary>Taken on another machine, or on a take this machine lost: the submission and its seal are that machine's.</summary>
    NotHeld,

    /// <summary>The submission named is not the quest's current one, or it has none.</summary>
    NotCurrent,

    /// <summary>The submission published no step that stands on it, so nothing seals it: a later <c>ready</c> replaces it.</summary>
    NotSealed,

    /// <summary>The step it published is closed, done or declined, on the submission it grew from, and a reopen takes no close back.</summary>
    StepClosed,

    /// <summary>A word to go on with a step no reopen holds.</summary>
    NotWithdrawn,

    /// <summary>A word to go on while the source has no unheld submission to grow from, so the step waits for its work again.</summary>
    SourceNotReady,
}

/// <param name="Refusal"><see cref="QuestReopenRefusal.None"/> when it moved.</param>
/// <param name="Quest">The quest named, as it now stands; null for no such id.</param>
/// <param name="Step">The step it moved with it, as it now stands: the reopened submission's, or the source a word to go on read.</param>
public sealed record QuestReopening(QuestReopenRefusal Refusal, Quest? Quest, Quest? Step);

/// <summary>
/// Why a graph's close wrote nothing (FABRIC-CLOSE1a, §E5), or <see cref="None"/> when it closed every quest it named. Each
/// refusal names the quest it failed on, and nothing is written for any.
/// </summary>
public enum QuestCloseRefusal
{
    None,

    /// <summary>The call is not one: no key or token, no quest named, a quest named twice, or a submission half named.</summary>
    Malformed,

    /// <summary>No quest under a named id.</summary>
    NotFound,

    /// <summary>A named quest no closure graph holds: its taker's driver never bound it.</summary>
    NotBound,

    /// <summary>A named quest this machine does not hold: never taken here, or taken on another machine first (D69).</summary>
    NotHeld,

    /// <summary>A named quest is already done or declined, by another close, the person, or another machine.</summary>
    Closed,

    /// <summary>The submission named is not the quest's current one, or it has none: the close's binding is stale.</summary>
    Stale,

    /// <summary>A named quest's submission is held for the person: a departure, its evidence, or its review.</summary>
    Held,

    /// <summary>A named quest's chain step is not published from its submission, so the submission is not sealed.</summary>
    Unsealed,

    /// <summary>
    /// The key was a close's already, sent with another token or other submissions (FABRIC-CLOSE1e): not that close again, so
    /// not answered with its outcome. A new attempt takes a new key.
    /// </summary>
    KeyReused,

    /// <summary>
    /// The key was never taken before the driver asked what became of it (FABRIC-CLOSE1u), so its close never ran, and the
    /// question withdrew it: kept under the key like any outcome, so a send of it arriving after is refused and never runs. Kept
    /// by its name; a build before it reads the name as no refusal it knows, which it answers as a key not that close's.
    /// </summary>
    Withdrawn,

    /// <summary>
    /// A named quest asks another repository than the quest the close is at (FABRIC-CLOSE1v): a close is one repository's, the chain's
    /// quests there, never another's, whose own graph closes them.
    /// </summary>
    OtherRepository,

    /// <summary>
    /// A named quest is not of the chain the quest the close is at grew from (FABRIC-CLOSE1v): neither that quest nor one of its ancestors,
    /// walked through its parents as the driver walks them (<c>QuestClosing.Lineage</c>).
    /// </summary>
    NotInChain,

    /// <summary>A named quest is bound to another graph than the quest the close is at (FABRIC-CLOSE1v): one graph's close is its own.</summary>
    OtherGraph,
}

/// <summary>
/// A close's refusal as the close door names it on the wire (FABRIC-CLOSE1b): the driver keeps the code beside the sentence, so it tells a key
/// gone with another close from a check a quest's state failed without reading the words. 🔴 The driver's <c>QuestCloseOutcomes.Codes</c>
/// lists each code (twins): a code it does not know leaves the close unknown, so a code added here is added there.
/// </summary>
public static class QuestCloseCodes
{
    /// <summary>The refusal's code, or null for none.</summary>
    public static string? Of(QuestCloseRefusal refusal) => refusal switch
    {
        QuestCloseRefusal.None => null,
        QuestCloseRefusal.Malformed => "malformed",
        QuestCloseRefusal.NotFound => "not-found",
        QuestCloseRefusal.NotBound => "not-bound",
        QuestCloseRefusal.NotHeld => "not-held",
        QuestCloseRefusal.Closed => "closed",
        QuestCloseRefusal.Stale => "stale",
        QuestCloseRefusal.Held => "held",
        QuestCloseRefusal.Unsealed => "unsealed",
        QuestCloseRefusal.KeyReused => "key-reused",
        QuestCloseRefusal.Withdrawn => "withdrawn",
        QuestCloseRefusal.OtherRepository => "other-repository",
        QuestCloseRefusal.NotInChain => "not-in-chain",
        QuestCloseRefusal.OtherGraph => "other-graph",
        _ => "refused",
    };
}

/// <param name="Refusal"><see cref="QuestCloseRefusal.None"/> when every named quest is Done.</param>
/// <param name="Quest">The quest a refusal failed on, or null.</param>
/// <param name="Quests">Every named quest as it now stands, in the order named; empty where one was not found.</param>
/// <param name="Replayed">
/// Whether this was the same close again, answered with its first outcome and writing nothing: a close, or a refusal
/// (FABRIC-CLOSE1e) whose quest may have moved since.
/// </param>
/// <param name="On">The id a refusal failed on, kept with it, so a replayed refusal names it whether or not the quest is found.</param>
public sealed record QuestClosing(
    QuestCloseRefusal Refusal, Quest? Quest, IReadOnlyList<Quest> Quests, bool Replayed = false, string? On = null);

/// <summary>What a dismissal did: the quest as it now stands (null when there is no such quest), and how many conflicts went.</summary>
public sealed record QuestDismissal(Quest? Quest, int Dismissed);

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
    private const int LegacyIdLength = 6;

    /// <summary>
    /// How long a graph named for a quest's take waits for it (FABRIC-CLOSE1o): the driver names it as the quest's first session
    /// opens, whose first act is the take, so a take later than this is not that start's, and goes unbound, saying so.
    /// </summary>
    public static readonly TimeSpan TakeBindLife = TimeSpan.FromHours(1);

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
            var store = new QuestStore(connection, time ?? TimeProvider.System);
            await store.EnsureSchemaAsync(ct).ConfigureAwait(false);
            store.Machine = await store.EnsureMachineAsync(ct).ConfigureAwait(false);
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

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using (var command = _db.Command())
        {
            // The log's position is this store's order of appending — and, on a remote, the NUMBER it
            // gives what it accepts (design §8). Machine + sequence names one operation anywhere;
            // position names it here; `remote` is where the remote placed it, null while pending.
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS quest_log (
                  position  INTEGER PRIMARY KEY,
                  quest     TEXT NOT NULL,
                  kind      TEXT NOT NULL,
                  machine   TEXT NOT NULL,
                  sequence  INTEGER NOT NULL,
                  at        TEXT NOT NULL,
                  payload   TEXT NOT NULL,
                  remote    INTEGER NULL,
                  UNIQUE (machine, sequence)
                );
                CREATE INDEX IF NOT EXISTS quest_log_quest ON quest_log (quest, position);
                -- `sequence` is the highest this machine has issued (HIST1a): the log's own maximum
                -- goes back when its newest rows are removed, and a remote already holds that number.
                CREATE TABLE IF NOT EXISTS quest_machine (
                  one      INTEGER PRIMARY KEY CHECK (one = 1),
                  id       TEXT NOT NULL,
                  sequence INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS quest_cursor (
                  workspace TEXT PRIMARY KEY COLLATE NOCASE,
                  number    INTEGER NOT NULL
                );
                -- How each circle's last pass ended (SYNC6a): when it last reached its remote, when it
                -- last tried, the quests it left behind and the wall it hit. Kept in the store rather
                -- than in a host's memory, so a restarted host still knows when the circle last synced.
                CREATE TABLE IF NOT EXISTS quest_passes (
                  workspace TEXT PRIMARY KEY COLLATE NOCASE,
                  synced    TEXT NULL,
                  tried     TEXT NOT NULL,
                  behind    TEXT NOT NULL,
                  problem   TEXT NULL
                );
                -- The quests this machine cleared whose operations a remote had numbered (HIST1b, D153 point 3):
                -- the absence made a record, since a fetch by cursor cannot carry one. Never pushed or served.
                CREATE TABLE IF NOT EXISTS quest_forgotten (
                  id TEXT PRIMARY KEY,
                  at TEXT NOT NULL
                );
                -- Each closure graph's close this machine answered (FABRIC-CLOSE1e, §E5 step 4): its key, its token, the
                -- submissions it named and how it ended, a refusal as much as a close, on which quest. The same close sent again
                -- answers that first outcome. The Done keeps the key as provenance; this keeps the call. Never pushed or served.
                CREATE TABLE IF NOT EXISTS quest_closes (
                  key     TEXT PRIMARY KEY,
                  token   TEXT NOT NULL,
                  named   TEXT NOT NULL,
                  refusal TEXT NOT NULL,
                  quest   TEXT NULL,
                  at      TEXT NOT NULL
                );
                -- The closure graph each open quest binds at its take (FABRIC-CLOSE1l, §E5's cutover), as the driver named it
                -- when it opened the quest's first session: the take on this machine binds it in the take's own write, and
                -- the row goes with it. Never pushed or served. Its owner (FABRIC-CLOSE1o): the session it was named for, the
                -- publication it was named on and when it lapses, nullable so a build before them still writes its three columns, and
                -- a row without them owns nothing. Since FABRIC-CLOSE1w only builds before it write here, and no row here owns anything.
                CREATE TABLE IF NOT EXISTS quest_take_binds (
                  id    TEXT PRIMARY KEY,
                  graph TEXT NOT NULL,
                  at    TEXT NOT NULL,
                  session            TEXT NULL,
                  published_machine  TEXT NULL,
                  published_sequence INTEGER NULL,
                  expires            TEXT NULL
                );
                -- The binds this build names, owned (FABRIC-CLOSE1w): the compatible-build boundary. A build before it knows only
                -- quest_take_binds, so its upsert there can never replace an owned bind's graph, nor its take bind one by the id alone.
                -- Every column is required, since no build that writes here leaves an owner out. Never pushed or served.
                CREATE TABLE IF NOT EXISTS quest_owned_take_binds (
                  id                 TEXT PRIMARY KEY,
                  graph              TEXT NOT NULL,
                  at                 TEXT NOT NULL,
                  session            TEXT NOT NULL,
                  published_machine  TEXT NOT NULL,
                  published_sequence INTEGER NOT NULL,
                  expires            TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await using (var command = _db.Command())
        {
            command.CommandText = $"""
                CREATE TABLE IF NOT EXISTS quests (
                  id        TEXT PRIMARY KEY,
                  sender    TEXT NOT NULL,
                  receiver  TEXT NOT NULL,
                  title     TEXT NOT NULL,
                  body      TEXT NOT NULL,
                  status    TEXT NOT NULL,
                  note      TEXT NULL,
                  filed     TEXT NOT NULL,
                  updated   TEXT NOT NULL,
                  workspace   TEXT NOT NULL DEFAULT '{Workspaces.Default}',
                  links       TEXT NOT NULL DEFAULT '[]',
                  attachments TEXT NOT NULL DEFAULT '[]',
                  then_steps  TEXT NOT NULL DEFAULT '[]',
                  parent      TEXT NULL,
                  conflicts   TEXT NOT NULL DEFAULT '[]',
                  awaits      TEXT NULL,
                  published_by TEXT NULL,
                  lanes       TEXT NOT NULL DEFAULT '[]',
                  requirements TEXT NOT NULL DEFAULT '[]',
                  answers     TEXT NOT NULL DEFAULT '[]',
                  accepted    TEXT NULL,
                  held        INTEGER NOT NULL DEFAULT 0,
                  short_title TEXT NULL,
                  evidence    TEXT NULL,
                  review      TEXT NULL,
                  set_up_in   TEXT NULL,
                  set_ups     TEXT NOT NULL DEFAULT '[]',
                  verdicts    TEXT NOT NULL DEFAULT '[]',
                  note_parts  TEXT NULL,
                  closure     TEXT NULL,
                  submission  TEXT NULL,
                  grew_from   TEXT NULL,
                  source_reopened TEXT NULL,
                  reopened    TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS quests_receiver ON quests (receiver, status);
                """;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // A store from before workspaces has no workspace; one from before quests carried anything
        // (D65) has neither list; one from before sync has no conflicts, and a log with no numbers.
        // Their quests survive the upgrade — the one workspace there was, and nothing carried or
        // raced, which is exactly right, because everything in them was its own.
        foreach (var (table, column, definition) in new[]
        {
            ("quests", "workspace", $"workspace TEXT NOT NULL DEFAULT '{Workspaces.Default}'"),
            ("quests", "links", "links TEXT NOT NULL DEFAULT '[]'"),
            ("quests", "attachments", "attachments TEXT NOT NULL DEFAULT '[]'"),
            // A chain (D65 §4): `then` is an SQL keyword, so the column says what it holds.
            ("quests", "then_steps", "then_steps TEXT NOT NULL DEFAULT '[]'"),
            ("quests", "parent", "parent TEXT NULL"),
            ("quests", "conflicts", "conflicts TEXT NOT NULL DEFAULT '[]'"),
            // Ask and wait (D79): the question a taken quest waits on.
            ("quests", "awaits", "awaits TEXT NULL"),
            // The session that published it (SESS1); a quest from before says none, which is true.
            ("quests", "published_by", "published_by TEXT NULL"),
            // The lanes it addresses (D115 §2.2); a quest from before asked the whole repository.
            ("quests", "lanes", "lanes TEXT NOT NULL DEFAULT '[]'"),
            // What the person requires (DRIFT1c); a quest from before named none.
            ("quests", "requirements", "requirements TEXT NOT NULL DEFAULT '[]'"),
            // How a done answered them, and the person's yes to a departure (DRIFT1d); a quest from before has
            // neither, and nothing it carries waits. `held` is the replay's own reading, kept so a list can ask it.
            ("quests", "answers", "answers TEXT NOT NULL DEFAULT '[]'"),
            ("quests", "accepted", "accepted TEXT NULL"),
            ("quests", "held", "held INTEGER NOT NULL DEFAULT 0"),
            // The publisher's short title (SESSUX1j); a quest from before was given none, and is named from its words.
            ("quests", "short_title", "short_title TEXT NULL"),
            // What Daoris last read of a done's evidence (EVID1a); a quest from before named none, so none was read.
            ("quests", "evidence", "evidence TEXT NULL"),
            // The chain's review choice, a set-up step's environment, its set-ups and the person's verdicts (REVIEWENV1b); a
            // quest from before chose nothing, is no set-up step, and was never shown or reviewed.
            ("quests", "review", "review TEXT NULL"),
            ("quests", "set_up_in", "set_up_in TEXT NULL"),
            ("quests", "set_ups", "set_ups TEXT NOT NULL DEFAULT '[]'"),
            ("quests", "verdicts", "verdicts TEXT NOT NULL DEFAULT '[]'"),
            // The note's parts (QUESTCLOSE1b1); a quest from before has a note with none, which the page shows as recorded.
            ("quests", "note_parts", "note_parts TEXT NULL"),
            // A closure graph's binding and its current submission (FABRIC-CLOSE1a); a quest from before has neither.
            ("quests", "closure", "closure TEXT NULL"),
            ("quests", "submission", "submission TEXT NULL"),
            // The submission a step grew from, and the one its source's reopen withdrew it from (FABRIC-CLOSE1c); a quest from before
            // has neither, which reads as before.
            ("quests", "grew_from", "grew_from TEXT NULL"),
            ("quests", "source_reopened", "source_reopened TEXT NULL"),
            // A quest's own reopen no later submission answered (FABRIC-CLOSE1c4); a row from before keeps none, and the next replay
            // that writes it reads the log.
            ("quests", "reopened", "reopened TEXT NULL"),
            ("quest_log", "remote", "remote INTEGER NULL"),
            // A bind named for a take, owned (FABRIC-CLOSE1o): CLOSE1l's rows named only the quest. `CREATE TABLE IF NOT EXISTS`
            // upgrades nothing, so the columns are added here, on a local store and a shared one alike. FABRIC-CLOSE1w keeps them for
            // the builds between, which write them; this build reads no owner there.
            ("quest_take_binds", "session", "session TEXT NULL"),
            ("quest_take_binds", "published_machine", "published_machine TEXT NULL"),
            ("quest_take_binds", "published_sequence", "published_sequence INTEGER NULL"),
            ("quest_take_binds", "expires", "expires TEXT NULL"),
        })
        {
            await SchemaColumns.EnsureAsync(_db, table, column, definition, ct).ConfigureAwait(false);
        }

        // FABRIC-CLOSE1o: a pending bind whose owner cannot be shown is discarded, since no take can show it is the one it was named
        // for. FABRIC-CLOSE1w: that is every row of quest_take_binds, since this build names its own in quest_owned_take_binds, and a
        // row there, whatever owner it names, may be an older build's upsert over another's graph. The quest and its history are
        // untouched: nothing is bound until a take, so its take is today's, closing by its done.
        await using (var unowned = _db.Command())
        {
            unowned.CommandText = "DELETE FROM quest_take_binds";
            await unowned.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // The mirror is gone (design §8). Its rows were copies of a remote's quests, so they are
        // dropped rather than migrated: this machine's cursor starts at zero, and its first fetch
        // brings every one of them back as history. The column that marked them goes with them.
        if (await SchemaColumns.HasAsync(_db, "quests", "home", ct).ConfigureAwait(false))
        {
            await using var drop = _db.Command();
            drop.CommandText = """
                DELETE FROM quests WHERE home IS NOT NULL;
                ALTER TABLE quests DROP COLUMN home;
                """;
            await drop.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // HIST1a: a store from before the mark starts it at the highest sequence its own machine holds in the log, which
        // is what every number so far was one past. Nothing changes for a store that never removed anything; one that
        // removes rows after the upgrade cannot take the sequence back.
        if (!await SchemaColumns.HasAsync(_db, "quest_machine", "sequence", ct).ConfigureAwait(false))
        {
            await using var mark = _db.Command();
            mark.CommandText = """
                ALTER TABLE quest_machine ADD COLUMN sequence INTEGER NOT NULL DEFAULT 0;
                UPDATE quest_machine SET sequence =
                  (SELECT COALESCE(MAX(sequence), 0) FROM quest_log WHERE machine = quest_machine.id);
                """;
            await mark.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // After the column, which it names. The mark is moved by the statement that writes the operation, not by its
        // callers, so no way into the log can forget it (the registry's tombstones are kept the same way). An operation
        // of this machine's kept back from a remote raises it too: that number was this machine's, and is held there.
        await using (var trigger = _db.Command())
        {
            trigger.CommandText = """
                CREATE TRIGGER IF NOT EXISTS quest_sequence_issued AFTER INSERT ON quest_log
                BEGIN
                  UPDATE quest_machine SET sequence = NEW.sequence
                  WHERE one = 1 AND id = NEW.machine AND sequence < NEW.sequence;
                END;
                """;
            await trigger.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
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
    private async Task<IReadOnlyList<QuestOperation>> GiveHistoryAsync(
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
    /// repository keeps its id and a lane's ask is not the repository's.
    /// </remarks>
    internal static string MakeId(
        string from, string to, string title, string? parent = null, IReadOnlyList<string>? lanes = null) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(
                    $"{from}->{to}:{title.Trim()}" + (parent is null ? "" : $"<-{parent}")
                    + (lanes is { Count: > 0 } ? $"@{string.Join('+', lanes.Order(StringComparer.Ordinal))}" : ""))))[..IdLength]
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
        string? parent = null)
    {
        var sorted = Sorted(lanes);
        var id = MakeId(from, to, title, parent, sorted);
        return await InTransactionAsync(async (transaction, inside) =>
        {
            // The same ask already held is the answer, whichever width of id it was published under.
            var existing = await FindAsync(id, transaction, inside).ConfigureAwait(false)
                           ?? (parent is null ? await FindAsync(id[..LegacyIdLength], transaction, inside).ConfigureAwait(false) : null);
            if (existing is not null) return existing;

            var asked = new Quest(
                id, from, to, title, body, QuestStatus.Open, null, now, now,
                Workspace: Workspaces.Normalize(workspace))
            {
                Links = links ?? [],
                Attachments = attachments ?? [],
                Then = then ?? [],
                PublishedBy = string.IsNullOrWhiteSpace(publishedBy) ? null : publishedBy.Trim(),
                Lanes = sorted,
                Requirements = requirements ?? [],
                Short = string.IsNullOrWhiteSpace(shortTitle) ? null : shortTitle.Trim(),
                Review = review,
                SetUpIn = setUpIn,
                Parent = parent,
            };

            return await PublishInAsync(asked, transaction, inside).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>The lanes a publish addresses, as its id and its record hold them: once each, sorted.</summary>
    private static List<string> Sorted(IReadOnlyList<string>? lanes) =>
        (lanes ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// The id these words would publish under, when this machine forgot that quest (HIST1b, D153 point 3), at either
    /// width an id has had; null when it did not. The exchange asks it before a publish, so the same words asked again
    /// are refused here rather than making a fresh quest the remote, which still holds the closed one, refuses for good (H5).
    /// </summary>
    internal async Task<string?> ForgottenIdAsync(
        string from, string to, string title, IReadOnlyList<string>? lanes, CancellationToken ct)
    {
        var id = MakeId(from, to, title, lanes: Sorted(lanes));
        foreach (var held in new[] { id, id[..LegacyIdLength] })
        {
            if (await ForgottenAsync(held, ct).ConfigureAwait(false)) return held;
        }

        return null;
    }

    /// <summary>
    /// Append a quest's `published` and write its row from the replay, inside a transaction. A <paramref name="note"/>
    /// stays in the history and never on the quest: what a chain step's publish says of what it inherited (EVID1a).
    /// </summary>
    private async Task<Quest> PublishInAsync(Quest asked, SqliteTransaction transaction, CancellationToken ct, string? note = null)
    {
        var published = await AppendAsync(
            asked.Id, QuestOperationKind.Published, asked.Filed, note, asked, transaction, ct).ConfigureAwait(false);
        var quest = QuestLog.Replay([published])!;
        await WriteCacheAsync(quest, transaction, ct).ConfigureAwait(false);
        return quest;
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
    private async Task<QuestOperation> AppendAsync(
        string quest, QuestOperationKind kind, DateTimeOffset at, string? note, Quest? published,
        SqliteTransaction transaction, CancellationToken ct, QuestOperationRef? dismisses = null,
        IReadOnlyList<QuestAnswer>? answers = null, bool whileOpen = false, QuestEvidenceVerdict? evidence = null,
        QuestSetUp? setUp = null, QuestReviewVerdict? review = null, string? noteParts = null, QuestClose? close = null,
        QuestOperationRef? submission = null, string? session = null)
    {
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
        var flagged = whileOpen && kind == QuestOperationKind.Declined;
        var verdict = kind == QuestOperationKind.Evidenced ? evidence : null;
        var shown = kind == QuestOperationKind.SetUp ? setUp : null;
        var said = kind == QuestOperationKind.Verdict ? review : null;
        // Parts only beside a note, as the store keeps them (QUESTCLOSE1b1): what it reads back is what it wrote.
        var parts = note is null ? null : NoteParts.Normalize(NoteParts.Element(noteParts));
        // A close's provenance is a done's alone (FABRIC-CLOSE1a).
        var closed = kind == QuestOperationKind.Done ? close : null;
        // The submission a reopen or a rebind names is theirs alone (FABRIC-CLOSE1c).
        var named = Names(kind) ? submission : null;
        // The session that said a ready is the submission's alone (FABRIC-CLOSE1c4).
        var by = kind == QuestOperationKind.Submitted ? session : null;
        command.Parameters.AddWithValue(
            "$payload", PayloadJson(note, published, attempted: null, dismisses, answers, flagged, verdict, shown, said, parts, closed, named, by));
        var sequence = Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));

        // Handed back as it will read back: a publish carries the quest as asked, open from this moment.
        return new QuestOperation(
            quest, kind, Machine, sequence, at, note,
            published is null
                ? null
                : published with { Status = QuestStatus.Open, Note = null, NoteParts = null, Filed = at, Updated = at, Conflicts = [] },
            Dismisses: dismisses,
            Answers: answers is { Count: > 0 } ? answers : null,
            WhileOpen: flagged,
            Evidence: verdict,
            SetUp: shown,
            Verdict: said,
            NoteParts: parts,
            Close: closed,
            Submission: named,
            Session: by);
    }

    /// <summary>The kinds that name a submission (FABRIC-CLOSE1c): a reopen, and a rebind.</summary>
    internal static bool Names(QuestOperationKind kind) => kind is QuestOperationKind.Reopened or QuestOperationKind.Rebound;

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
        command.Parameters.AddWithValue(
            "$payload", PayloadJson(
                operation.Note, operation.Published, operation.Attempted, operation.Dismisses, operation.Answers,
                operation.WhileOpen && operation.Kind == QuestOperationKind.Declined,
                operation.Kind == QuestOperationKind.Evidenced ? operation.Evidence : null,
                operation.Kind == QuestOperationKind.SetUp ? operation.SetUp : null,
                operation.Kind == QuestOperationKind.Verdict ? operation.Verdict : null,
                operation.NoteParts,
                operation.Kind == QuestOperationKind.Done ? operation.Close : null,
                Names(operation.Kind) ? operation.Submission : null,
                operation.Kind == QuestOperationKind.Submitted ? operation.Session : null));
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
    private async Task WriteCacheAsync(Quest quest, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO quests (id, sender, receiver, title, body, status, note, note_parts, filed, updated, workspace, links, attachments, then_steps, parent, conflicts, awaits, published_by, lanes, requirements, answers, accepted, held, short_title, evidence, review, set_up_in, set_ups, verdicts, closure, submission, grew_from, source_reopened, reopened)
            VALUES ($id, $sender, $receiver, $title, $body, $status, $note, $noteParts, $filed, $updated, $workspace, $links, $attachments, $then, $parent, $conflicts, $awaits, $publishedBy, $lanes, $requirements, $answers, $accepted, $held, $short, $evidence, $review, $setUpIn, $setUps, $verdicts, $closure, $submission, $grewFrom, $sourceReopened, $reopened)
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
              grew_from = excluded.grew_from, source_reopened = excluded.source_reopened, reopened = excluded.reopened
            """;
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

    private async Task<IReadOnlyList<QuestOperation>> HistoryAsync(
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

    /// <summary>
    /// The chain's next step as the close of <paramref name="parent"/> publishes it: asked on behalf of
    /// the same asker, of the step's receiver, with <c>{parent}</c> expanded, carrying the rest and the
    /// parent's requirements (DRIFT1c, D133 §3) — a step is measured by what the person asked, not by the
    /// closing note of the work it follows.
    /// </summary>
    /// <remarks>
    /// A step to the same repository inherits each requirement whole; a step to another inherits it without its
    /// evidence (EVID1a, D144 §2), since a path is a fact about one repository's tree. <c>Left</c> says which
    /// requirements lost theirs, for the step's history; null when none did.
    /// </remarks>
    private static (Quest Step, string? Left)? NextStep(Quest parent, DateTimeOffset now)
    {
        if (parent.Then.Count == 0) return null;
        var step = parent.Then[0];
        var title = step.Title.Replace("{parent}", $"#{parent.Id}", StringComparison.Ordinal);
        var sameTree = string.Equals(step.To, parent.To, StringComparison.OrdinalIgnoreCase);
        var left = sameTree
            ? []
            : parent.Requirements.Select((requirement, index) => (requirement, Number: index + 1))
                .Where(each => each.requirement.Evidence.Count > 0).Select(each => each.Number).ToList();
        var next = new Quest(
            MakeId(parent.From, step.To, title, parent.Id), parent.From, step.To, title,
            step.Body.Replace("{parent}", $"#{parent.Id}", StringComparison.Ordinal),
            QuestStatus.Open, null, now, now, Workspace: parent.Workspace)
        {
            Then = parent.Then.Skip(1).ToList(),
            Parent = parent.Id,
            // The submission that publishes it (FABRIC-CLOSE1c, §E5): what a reopen names it by, and what seals it. A done names none.
            GrewFrom = parent is { Status: QuestStatus.Taken, Submission: { } from } ? new QuestOperationRef(from.Machine, from.Sequence) : null,
            Requirements = left.Count == 0
                ? parent.Requirements
                : [.. parent.Requirements.Select(requirement => requirement with { Evidence = [] })],
            // The chain's review choice is every step's (REVIEWENV1b, D154 point 3), and a set-up step is one by its step.
            Review = parent.Review,
            SetUpIn = step.SetUpIn,
        };

        return (next, left.Count == 0
            ? null
            : $"{(left.Count == 1 ? "requirement" : "requirements")} {string.Join(", ", left)} named evidence in `{parent.To}`'s "
              + $"tree; this step asks `{step.To}`, so it inherits {(left.Count == 1 ? "it" : "them")} without the evidence.");
    }

    /// <summary>
    /// Move a quest to a new status, atomically, honouring the transition table. Declining without a
    /// reason is refused by the caller; an illegal move is refused HERE, in the store, because judging
    /// the replayed history inside the write transaction is what makes "the quest state machine is the
    /// only lock" true when two hosts' callers race over one file (D47 §5) — a check the caller ran a
    /// moment earlier decides nothing.
    /// </summary>
    /// <returns>
    /// The quest as it now stands and whether this call moved it; a null quest means no such id.
    /// A refused move returns the row unchanged, so the caller can name the state that refused it, and says
    /// <see cref="QuestMove.ClaimLost"/> where the state allowed it but this machine's take on the quest lost (WAITCLAIM2).
    /// </returns>
    /// <param name="answers">
    /// How a done answers the quest's requirements (DRIFT1d, D133 §4), already judged by the exchange. A departure
    /// among them holds the chain's next step: it is published by the person's yes (<see cref="AcceptAsync"/>), not here.
    /// </param>
    /// <param name="whileOpen">
    /// A decline that applies only while the quest is open (PAUSE1c, D132 point 10): refused here on a quest no longer
    /// open, and kept with the flag, so a rebase or a remote judges it by the same rule. Ignored on any other move.
    /// </param>
    /// <param name="noteParts">
    /// The note's parts beside it (QUESTCLOSE1b1, D142 point 2), as <see cref="NoteParts"/> keeps them: Daoris's lead by its
    /// code, then someone's words. Only with a note; a move that writes its note without them clears the quest's.
    /// </param>
    /// <param name="byAgent">
    /// An agent's done (FABRIC-CLOSE1a, §E5): refused here, with <see cref="QuestMove.ClosedByGraph"/>, on a quest a closure
    /// graph binds, judged inside the write so a bind landing a moment earlier is seen. The person's done is never one.
    /// </param>
    /// <param name="taker">
    /// Who takes (FABRIC-CLOSE1o): a take binds the graph named for it only for the session it was named for, on the publication it
    /// was named on, before it expires, and where the quest's circle still syncs through no remote. Null names no session.
    /// </param>
    public Task<QuestMove> MoveAsync(
        string id, QuestStatus status, string? note, DateTimeOffset now, CancellationToken ct = default,
        IReadOnlyList<QuestAnswer>? answers = null, bool whileOpen = false, string? noteParts = null, bool byAgent = false,
        QuestTaker? taker = null)
    {
        // FABRIC-CLOSE1w: `now` is the caller's, taken before this queued for the gate and the write, so the take measures that wait.
        var asked = _time.GetTimestamp();
        return InTransactionAsync(async (transaction, inside) =>
        {
            var held = await FindAsync(id, transaction, inside).ConfigureAwait(false);

            // Every verb commits here, whichever machines share the quest (D68 §1) — and nothing moves
            // TO open.
            if (held is null || QuestTransitions.KindFor(status) is not { } kind)
            {
                return new QuestMove(held, Moved: false);
            }

            var history = await HistoryAsync(id, transaction, inside).ConfigureAwait(false);
            if (history.Count == 0)
            {
                // A row the log never saw — written by something older than the log since this store
                // was opened. It is given its history now, exactly as the open would have, and keeps it
                // even if the move is refused: that write is the store's, and true.
                history = await GiveHistoryAsync(held, transaction, inside).ConfigureAwait(false);
            }

            // Judged by the replay's own rule, so the store refuses here exactly what a rebase or a remote
            // would not apply — a decline made while open (PAUSE1c) on a quest no longer open among them.
            var open = whileOpen && status == QuestStatus.Declined;
            var standing = QuestLog.Replay(history);
            var judged = new QuestOperation(id, kind, Machine, 0, now, note, WhileOpen: open);
            if (!QuestLog.Applies(standing, judged))
            {
                // FABRIC-CLOSE1c: a step its source's reopen withdrew is taken by nobody, said apart so the answer can say why.
                return new QuestMove(held, Moved: false)
                {
                    SourceReopened = status == QuestStatus.Taken && standing is { Status: QuestStatus.Open, SourceReopened: not null },
                };
            }

            // A close on the winner's take, made here after the pass that found this machine's take lost (WAITCLAIM2).
            if (QuestLog.OnALostTake(standing, history, judged)) return new QuestMove(held, Moved: false) { ClaimLost = true };

            // FABRIC-CLOSE1c: a step its source's reopen held goes no further by its agent's done; the person chooses whether it goes
            // on. Their own done and any decline stay theirs.
            if (byAgent && status == QuestStatus.Done && standing is { SourceReopened: not null })
            {
                return new QuestMove(held, Moved: false) { SourceReopened = true };
            }

            // An agent's done where the graph closes the quest (FABRIC-CLOSE1a): its result is a submission, so no done slips past.
            if (byAgent && status == QuestStatus.Done && standing is { Closure: not null })
            {
                return new QuestMove(held, Moved: false) { ClosedByGraph = true };
            }

            var operation = await AppendAsync(
                id, kind, now, note, null, transaction, inside,
                answers: status == QuestStatus.Done ? answers : null, whileOpen: open, noteParts: noteParts).ConfigureAwait(false);
            var moved = QuestLog.Replay([.. history, operation])!;

            // FABRIC-CLOSE1l (CLOSE1f): a take whose graph the driver named binds in this same write, judged by the replay's rule as
            // every bind is, so no agent's done can land between the take and its bind. FABRIC-CLOSE1o: the row is consumed here
            // whatever it makes of it, and binds only for its owner, judged at the take's own moment, never by a look before it.
            // FABRIC-CLOSE1w: that moment is the one inside this write, the caller's advanced by the wait for it, so a take queued past
            // the expiry binds nothing. The operations keep the caller's moment, as every verb's do.
            var takeBind = QuestTakeBind.None;
            if (status == QuestStatus.Taken && await TakeBindAsync(id, transaction, inside).ConfigureAwait(false) is { } named)
            {
                takeBind = named.Judge(taker, history, moved.Workspace, now + _time.GetElapsedTime(asked));
                var bind = new QuestOperation(id, QuestOperationKind.Bound, Machine, 0, now, named.Graph);
                if (takeBind == QuestTakeBind.Bound && QuestLog.Applies(moved, bind))
                {
                    var bound = await AppendAsync(id, QuestOperationKind.Bound, now, named.Graph, null, transaction, inside).ConfigureAwait(false);
                    moved = QuestLog.Replay([.. history, operation, bound])!;
                }
                else if (takeBind == QuestTakeBind.Bound)
                {
                    takeBind = QuestTakeBind.None;
                }
            }

            await WriteCacheAsync(moved, transaction, inside).ConfigureAwait(false);

            // A close that finishes a chain's step publishes the next one IN THE SAME TRANSACTION
            // (D65 §4): there is no moment at which the work is done and the chain lost, and a close
            // another host wins publishes nothing here. A step already published is the one that
            // stands — the id is the ask. A departure holds it for the person's yes (DRIFT1d).
            Quest? followUp = null;
            if (status == QuestStatus.Done && !moved.Held)
            {
                followUp = await PublishNextAsync(moved, now, transaction, inside).ConfigureAwait(false);
            }

            return new QuestMove(moved, Moved: true, followUp) { TakeBind = takeBind };
        }, ct);
    }

    /// <summary>
    /// The chain's next step after <paramref name="closed"/>, published in the caller's transaction — or the one
    /// already published, since the id is the ask — or null when the chain ends here.
    /// </summary>
    private async Task<Quest?> PublishNextAsync(Quest closed, DateTimeOffset now, SqliteTransaction transaction, CancellationToken ct)
    {
        if (NextStep(closed, now) is not { } next) return null;
        if (await FindAsync(next.Step.Id, transaction, ct).ConfigureAwait(false) is not { } standing)
        {
            return await PublishInAsync(next.Step, transaction, ct, next.Left).ConfigureAwait(false);
        }

        // FABRIC-CLOSE1c: a step its source's reopen withdrew, never taken, is the same step again: rebound to the submission that
        // publishes it now, keeping its id and its history, so no second quest is asked and no withdrawal outlives the work it named.
        // A started one waits for the person's word (ContinueAsync).
        if (standing is { Status: QuestStatus.Open, SourceReopened: not null } && next.Step.GrewFrom is { } from)
        {
            return await ReboundInAsync(standing.Id, from, now, transaction, ct).ConfigureAwait(false) ?? standing;
        }

        return standing;
    }

    /// <summary>
    /// A <see cref="QuestOperationKind.Rebound"/> on <paramref name="id"/>, naming <paramref name="from"/>, judged by the replay's rule
    /// in the caller's transaction (FABRIC-CLOSE1c); the step as it then stands, or null where the rule refused it.
    /// </summary>
    private async Task<Quest?> ReboundInAsync(
        string id, QuestOperationRef from, DateTimeOffset now, SqliteTransaction transaction, CancellationToken ct)
    {
        var history = await HistoryAsync(id, transaction, ct).ConfigureAwait(false);
        var standing = QuestLog.Replay(history);
        if (!QuestLog.Applies(standing, new QuestOperation(id, QuestOperationKind.Rebound, Machine, 0, now, Submission: from))) return null;
        var operation = await AppendAsync(
            id, QuestOperationKind.Rebound, now, null, null, transaction, ct, submission: from).ConfigureAwait(false);
        var rebound = QuestLog.Step(standing, operation)!;
        await WriteCacheAsync(rebound, transaction, ct).ConfigureAwait(false);
        return rebound;
    }

    /// <summary>
    /// Bind a taken quest to the graph that closes it (FABRIC-CLOSE1a, §E5): a <see cref="QuestOperationKind.Bound"/> operation
    /// naming the graph's digest, judged against the replayed history inside the write.
    /// </summary>
    /// <returns>
    /// The quest as it now stands and whether this call bound it; a null quest means no such id. Refused with
    /// <see cref="QuestMove.ClaimLost"/> where this machine's take lost, and unmoved where this machine holds no take on it.
    /// </returns>
    public Task<QuestMove> BindAsync(string id, string graph, DateTimeOffset now, CancellationToken ct = default) =>
        TakersMoveAsync(id, new QuestOperation(id, QuestOperationKind.Bound, Machine, 0, now, graph), now, ct);

    /// <summary>
    /// Name the closure graph an open quest binds at its take (FABRIC-CLOSE1l, folding CLOSE1f; §E5's cutover): kept in this store
    /// alone, so the take on this machine appends the bind in its own write (<see cref="MoveAsync"/>), and no agent's `done` can
    /// land between the take and the bind. The same graph again is the same; another replaces it, since nothing is bound yet.
    /// FABRIC-CLOSE1o: owned by <paramref name="session"/>, the session it is named for, and by the quest's publication as it stands,
    /// and lapsing <see cref="TakeBindLife"/> after <paramref name="now"/>; naming it again names it anew, for the session named then.
    /// </summary>
    /// <returns>The quest as it stands and whether the graph is named now; unmoved where it is not open, a null quest for no such id.</returns>
    public Task<QuestMove> BindAtTakeAsync(string id, string graph, string session, DateTimeOffset now, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var held = await FindAsync(id, transaction, inside).ConfigureAwait(false);
            if (held is null) return new QuestMove(null, Moved: false);
            var history = await HistoryAsync(id, transaction, inside).ConfigureAwait(false);
            // A row the log never saw is given its history now, as a move gives it: its publication is what the bind is owned by.
            if (history.Count == 0) history = await GiveHistoryAsync(held, transaction, inside).ConfigureAwait(false);
            var standing = QuestLog.Replay(history);
            if (standing is not { Status: QuestStatus.Open } || PublicationOf(history) is not { } publication)
            {
                return new QuestMove(held, Moved: false);
            }

            // FABRIC-CLOSE1w: in the table no build before this one knows, so none can replace the graph and keep the owner.
            await using var command = _db.Command();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO quest_owned_take_binds (id, graph, at, session, published_machine, published_sequence, expires)
                VALUES ($id, $graph, $at, $session, $machine, $sequence, $expires)
                ON CONFLICT(id) DO UPDATE SET graph = excluded.graph, at = excluded.at, session = excluded.session,
                  published_machine = excluded.published_machine, published_sequence = excluded.published_sequence,
                  expires = excluded.expires
                """;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$graph", graph);
            command.Parameters.AddWithValue("$at", Moments.Write(now));
            command.Parameters.AddWithValue("$session", session);
            command.Parameters.AddWithValue("$machine", publication.Machine);
            command.Parameters.AddWithValue("$sequence", publication.Sequence);
            command.Parameters.AddWithValue("$expires", Moments.Write(now + TakeBindLife));
            await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            return new QuestMove(held, Moved: true);
        }, ct);

    /// <summary>
    /// The operation that published the quest as it now stands (FABRIC-CLOSE1o): the latest Published in its history, since a quest
    /// deleted while open and asked again under the same id is published anew. Null where the history holds none.
    /// </summary>
    private static QuestOperationRef? PublicationOf(IReadOnlyList<QuestOperation> history) =>
        history.LastOrDefault(operation => operation.Kind == QuestOperationKind.Published) is { } published
            ? new QuestOperationRef(published.Machine, published.Sequence)
            : null;

    /// <summary>
    /// The bind named for <paramref name="id"/>'s take, taken from the store in the caller's transaction, owned or not; null where none
    /// was. Consumed by the take whatever the take makes of it (FABRIC-CLOSE1o), so no row outlives the take it was named for.
    /// FABRIC-CLOSE1w: the owned bind is this build's, in <c>quest_owned_take_binds</c>; a row in <c>quest_take_binds</c> is an older
    /// build's, whose owner cannot be shown, so it is consumed too and read as owning nothing.
    /// </summary>
    private async Task<PendingTakeBind?> TakeBindAsync(string id, SqliteTransaction transaction, CancellationToken ct)
    {
        string? older;
        await using (var discard = _db.Command())
        {
            discard.Transaction = transaction;
            discard.CommandText = "DELETE FROM quest_take_binds WHERE id = $id RETURNING graph";
            discard.Parameters.AddWithValue("$id", id);
            older = await discard.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
        }

        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM quest_owned_take_binds WHERE id = $id
            RETURNING graph, session, published_machine, published_sequence, expires
            """;
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return older is null ? null : new PendingTakeBind(older, Session: null, Publication: null, Expires: null);
        }

        return new PendingTakeBind(
            reader.GetString(0),
            reader.GetString(1),
            new QuestOperationRef(reader.GetString(2), reader.GetInt64(3)),
            Moments.Read(reader.GetString(4)));
    }

    /// <summary>
    /// A bind named for a take, as the take reads it (FABRIC-CLOSE1o): its graph, and its owner where one was kept: the session it was
    /// named for, the publication it was named on and when it lapses.
    /// </summary>
    private sealed record PendingTakeBind(string Graph, string? Session, QuestOperationRef? Publication, DateTimeOffset? Expires)
    {
        /// <summary>
        /// What the take makes of it, inside the take's write: bound only for the session it was named for, on the publication it was
        /// named on, before it lapses at <paramref name="now"/>, the moment inside that write (FABRIC-CLOSE1w), and where the circle
        /// syncs through no remote.
        /// </summary>
        public QuestTakeBind Judge(QuestTaker? taker, IReadOnlyList<QuestOperation> history, string workspace, DateTimeOffset now)
        {
            if (Session is null || Publication is null || Expires is null) return QuestTakeBind.Unowned;
            if (!string.Equals(taker?.Session, Session, StringComparison.Ordinal)) return QuestTakeBind.OtherSession;
            if (PublicationOf(history) != Publication) return QuestTakeBind.Replaced;
            if (now >= Expires) return QuestTakeBind.Expired;
            if (taker?.Remote?.Invoke(workspace) == true) return QuestTakeBind.Remote;
            return QuestTakeBind.Bound;
        }
    }

    /// <summary>
    /// Submit a closure graph's work (FABRIC-CLOSE1a, §E5): a <see cref="QuestOperationKind.Submitted"/> operation carrying a
    /// done's note and answers, and the chain's next step it publishes when nothing holds it, in the same transaction.
    /// </summary>
    /// <returns>
    /// As <see cref="BindAsync"/>, and refused with <see cref="QuestMove.SealedBy"/> where the current submission already
    /// published its successor, which stands: the step grew from that submission, and only the person reopens it.
    /// </returns>
    public Task<QuestMove> SubmitAsync(
        string id, string? note, IReadOnlyList<QuestAnswer> answers, DateTimeOffset now, CancellationToken ct = default,
        string? session = null) =>
        TakersMoveAsync(
            id,
            new QuestOperation(
                id, QuestOperationKind.Submitted, Machine, 0, now, note, Answers: answers.Count > 0 ? answers : null,
                Session: session is { Length: > 0 } ? session : null),
            now, ct);

    /// <summary>
    /// A closure graph's bind or submission (FABRIC-CLOSE1a), made by the machine holding the take, judged by the replay's rule
    /// inside the write as every move is. A submission nothing holds publishes the chain's next step in the same transaction,
    /// as an unheld done does, and is sealed by it (§E5).
    /// </summary>
    private Task<QuestMove> TakersMoveAsync(string id, QuestOperation judged, DateTimeOffset now, CancellationToken ct) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var held = await FindAsync(id, transaction, inside).ConfigureAwait(false);
            if (held is null) return new QuestMove(null, Moved: false);

            var history = await HistoryAsync(id, transaction, inside).ConfigureAwait(false);
            if (history.Count == 0) history = await GiveHistoryAsync(held, transaction, inside).ConfigureAwait(false);
            var standing = QuestLog.Replay(history);
            if (!QuestLog.Applies(standing, judged)) return new QuestMove(held, Moved: false);
            if (QuestLog.OnALostTake(standing, history, judged)) return new QuestMove(held, Moved: false) { ClaimLost = true };

            // Only the machine holding the take binds or submits (§E5): another machine's take is its own to finish.
            if (QuestLog.Claim(history, Machine) is not (QuestClaim.Held or QuestClaim.Unconfirmed)) return new QuestMove(held, Moved: false);

            // Sealed (§E5): a submission whose successor stands takes no later one, so the step never stands on work replaced
            // under it. Only the take's machine submits, so this machine's store is where the seal is judged.
            if (judged.Kind == QuestOperationKind.Submitted
                && await SealingAsync(standing!, transaction, inside).ConfigureAwait(false) is { } successor)
            {
                return new QuestMove(held, Moved: false) { SealedBy = successor.Id };
            }

            // FABRIC-CLOSE1c: a step its source's reopen holds goes no further; the person chooses whether it goes on.
            if (judged.Kind == QuestOperationKind.Submitted && standing!.SourceReopened is not null)
            {
                return new QuestMove(held, Moved: false) { SourceReopened = true };
            }

            var operation = await AppendAsync(
                id, judged.Kind, now, judged.Note, null, transaction, inside, answers: judged.Answers, session: judged.Session)
                .ConfigureAwait(false);
            var moved = QuestLog.Step(standing, operation)!;
            await WriteCacheAsync(moved, transaction, inside).ConfigureAwait(false);

            var followUp = judged.Kind == QuestOperationKind.Submitted && !moved.Held
                ? await PublishNextAsync(moved, now, transaction, inside).ConfigureAwait(false)
                : null;
            return new QuestMove(moved, Moved: true, followUp);
        }, ct);

    /// <summary>The chain's next step <paramref name="quest"/>'s close or submission published, where it stands; else null.</summary>
    private async Task<Quest?> SuccessorAsync(Quest quest, SqliteTransaction transaction, CancellationToken ct) =>
        NextStep(quest, quest.Updated) is { } next ? await FindAsync(next.Step.Id, transaction, ct).ConfigureAwait(false) : null;

    /// <summary>
    /// The person's reopen of a sealed submission (FABRIC-CLOSE1c, §E5), in one transaction on the machine holding the take: a
    /// <see cref="QuestOperationKind.Reopened"/> on the quest, which leaves it taken with no current submission, and one on the step
    /// that submission published, which withdraws it while open and holds it while taken.
    /// </summary>
    /// <remarks>
    /// Judged inside the write, so a close or a <c>ready</c> landing a moment earlier is seen: a close first leaves the quest Done,
    /// and the reopen is refused; a reopen first leaves the close naming a submission that is current no more, and the close door
    /// refuses it as stale. A step closed on the submission refuses the reopen: a reopen takes no close back, and a push made is
    /// never retracted.
    /// </remarks>
    public Task<QuestReopening> ReopenAsync(
        string id, QuestOperationRef submission, string? note, DateTimeOffset now, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var held = await FindAsync(id, transaction, inside).ConfigureAwait(false);
            if (held is null) return new QuestReopening(QuestReopenRefusal.NotFound, null, null);
            var history = await HistoryAsync(id, transaction, inside).ConfigureAwait(false);
            if (history.Count == 0) history = await GiveHistoryAsync(held, transaction, inside).ConfigureAwait(false);
            var standing = QuestLog.Replay(history)!;

            QuestReopening Refused(QuestReopenRefusal refusal, Quest? step = null) => new(refusal, standing, step);
            if (standing.Status != QuestStatus.Taken) return Refused(QuestReopenRefusal.NotTaken);
            // Only the take's machine submits, so only its store judges the seal (CLOSE1a's choice 3).
            if (QuestLog.OnALostTake(standing, history, new QuestOperation(id, QuestOperationKind.Done, Machine, 0, now))
                || QuestLog.Claim(history, Machine) is not (QuestClaim.Held or QuestClaim.Unconfirmed))
            {
                return Refused(QuestReopenRefusal.NotHeld);
            }

            // The quest's own current submission, never one it grew from: that is its source's to reopen.
            if (standing.Submission is not { } current || current.Machine != submission.Machine || current.Sequence != submission.Sequence)
            {
                return Refused(QuestReopenRefusal.NotCurrent);
            }

            var step = await SuccessorAsync(standing, transaction, inside).ConfigureAwait(false);
            if (step is { Status: QuestStatus.Done or QuestStatus.Declined } && Grows(step, submission))
            {
                return Refused(QuestReopenRefusal.StepClosed, step);
            }

            if (step is null || await SealingAsync(standing, transaction, inside).ConfigureAwait(false) is null)
            {
                return Refused(QuestReopenRefusal.NotSealed, step);
            }

            var operation = await AppendAsync(
                id, QuestOperationKind.Reopened, now, note, null, transaction, inside, submission: submission).ConfigureAwait(false);
            var reopened = QuestLog.Step(standing, operation)!;
            await WriteCacheAsync(reopened, transaction, inside).ConfigureAwait(false);

            // The step it published, in the same write: withdrawn while open, held while taken, by the same submission's name.
            var stepHistory = await HistoryAsync(step.Id, transaction, inside).ConfigureAwait(false);
            var stepStanding = QuestLog.Replay(stepHistory);
            var onStep = await AppendAsync(
                step.Id, QuestOperationKind.Reopened, now, null, null, transaction, inside, submission: submission).ConfigureAwait(false);
            var withdrawn = QuestLog.Step(stepStanding, onStep)!;
            await WriteCacheAsync(withdrawn, transaction, inside).ConfigureAwait(false);
            return new QuestReopening(QuestReopenRefusal.None, reopened, withdrawn);
        }, ct);

    /// <summary>
    /// The person's word to go on with a step its source's reopen held (FABRIC-CLOSE1c, §E5): a <see cref="QuestOperationKind.Rebound"/>
    /// naming the source's current submission, once that is unheld.
    /// </summary>
    public Task<QuestReopening> ContinueAsync(string id, DateTimeOffset now, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var held = await FindAsync(id, transaction, inside).ConfigureAwait(false);
            if (held is null) return new QuestReopening(QuestReopenRefusal.NotFound, null, null);
            var standing = QuestLog.Replay(await HistoryAsync(id, transaction, inside).ConfigureAwait(false)) ?? held;
            if (standing is not { Status: QuestStatus.Open or QuestStatus.Taken, SourceReopened: not null })
            {
                return new QuestReopening(QuestReopenRefusal.NotWithdrawn, standing, null);
            }

            // The source as this store holds it: its current submission, unheld, is what the step grows from now. Another machine's
            // submission reaches this store by sync, and the word waits for it.
            var source = standing.Parent is { Length: > 0 } parent ? await FindAsync(parent, transaction, inside).ConfigureAwait(false) : null;
            if (source is not { Status: QuestStatus.Taken, Submission: { } current, Held: false })
            {
                return new QuestReopening(QuestReopenRefusal.SourceNotReady, standing, source);
            }

            var rebound = await ReboundInAsync(id, new QuestOperationRef(current.Machine, current.Sequence), now, transaction, inside)
                .ConfigureAwait(false);
            return rebound is null
                ? new QuestReopening(QuestReopenRefusal.SourceNotReady, standing, source)
                : new QuestReopening(QuestReopenRefusal.None, rebound, source);
        }, ct);

    /// <summary>
    /// The step that seals <paramref name="quest"/>'s current submission (FABRIC-CLOSE1a, FABRIC-CLOSE1c; §E5): its chain's next step,
    /// standing on that submission, withdrawn by no reopen. Null where none does, so a later <c>ready</c> replaces it, and the close
    /// door reads it unsealed. A step from before names no submission, and seals whichever is current, as it always did.
    /// </summary>
    private async Task<Quest?> SealingAsync(Quest quest, SqliteTransaction transaction, CancellationToken ct) =>
        quest.Submission is { } current
        && await SuccessorAsync(quest, transaction, ct).ConfigureAwait(false) is { } step
        && Grows(step, new QuestOperationRef(current.Machine, current.Sequence))
            ? step
            : null;

    /// <summary>Whether <paramref name="step"/> stands on <paramref name="submission"/> (FABRIC-CLOSE1c): grown from it, no reopen since.</summary>
    private static bool Grows(Quest step, QuestOperationRef submission) =>
        step.SourceReopened is null && (step.GrewFrom is not { } from || from == submission);

    /// <summary>
    /// The graph's close (FABRIC-CLOSE1a, §E5): every named quest Done, each on its named submission, in one transaction. Each
    /// quest is judged on its history replayed inside the transaction: bound to a closure graph, held by this machine's take,
    /// its named submission the current one, nothing holding it, and its chain's next step published from it. Any failed check
    /// writes nothing, and answers which check on which quest. Only then is Done appended to each, in the order named, with its
    /// submission's note and answers and the close's key and token as provenance.
    /// </summary>
    /// <remarks>
    /// The key is what makes a close once (§E5 step 4). Each outcome is kept under its key, a refusal as much as a close, beside
    /// the token and the submissions named, in this store and never as an operation (FABRIC-CLOSE1e). The same close sent again
    /// after a timeout or a lost answer (the same key, token and submissions) is answered with that first outcome, writing nothing,
    /// though what refused it is gone since. Anything else sent with that key is not that close, and is refused.
    /// <para>FABRIC-CLOSE1v: the close holds its own scope before any quest's state, so no driver's filter is its only guard. Every quest
    /// named asks <paramref name="at"/>'s repository, is <paramref name="at"/> or one it grew from (<see cref="LineageAsync"/>), and is
    /// bound to its graph; else the close is refused on that quest, by its own code, kept under its key, and nothing is written.</para>
    /// </remarks>
    /// <param name="at">The quest the close is at, the route's: its chain's last quest here, which must be among those named.</param>
    public Task<QuestClosing> CloseAsync(
        string at, IReadOnlyList<(string Id, QuestOperationRef Submission)> named, QuestClose close, DateTimeOffset now,
        CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            // The exchange refuses such a call as malformed before it gets here, keeping no key.
            if (!named.Any(each => string.Equals(each.Id, at, StringComparison.Ordinal)))
            {
                throw new ArgumentException($"A close at `#{at}` names `#{at}` among its quests.", nameof(at));
            }

            var judged = new List<(Quest Quest, IReadOnlyList<QuestOperation> History)>();
            string? missing = null;
            foreach (var (id, _) in named)
            {
                var history = await HistoryAsync(id, transaction, inside).ConfigureAwait(false);
                if (QuestLog.Replay(history) is not { } quest)
                {
                    missing = id;
                    break;
                }

                judged.Add((quest, history));
            }

            var standing = judged.Select(each => each.Quest).ToList();
            var invocation = Invocation(named);
            if (await CloseKeptAsync(close.Key, transaction, inside).ConfigureAwait(false) is { } first)
            {
                if (first.Token != close.Token || first.Named != invocation)
                {
                    return new QuestClosing(QuestCloseRefusal.KeyReused, null, standing);
                }

                return new QuestClosing(
                    first.Refusal, standing.FirstOrDefault(quest => quest.Id == first.On), standing, Replayed: true, On: first.On);
            }

            // Kept under its key in the same transaction as what it judged, so a retry answers this outcome and no other.
            async Task<QuestClosing> Refuse(QuestCloseRefusal refusal, Quest? quest, string on)
            {
                await KeepCloseAsync(close, invocation, refusal, on, now, transaction, inside).ConfigureAwait(false);
                return new QuestClosing(refusal, quest, standing, On: on);
            }

            if (missing is not null) return await Refuse(QuestCloseRefusal.NotFound, null, missing).ConfigureAwait(false);

            // FABRIC-CLOSE1v: the close's scope, before any quest's state. It is one repository's, one chain's and one graph's: those of
            // the quest it is at. A quest outside it is refused whatever its state, so a close naming one is never made in part.
            var terminal = standing.First(quest => string.Equals(quest.Id, at, StringComparison.Ordinal));
            IReadOnlySet<string>? lineage = null;
            foreach (var quest in standing)
            {
                if (ReferenceEquals(quest, terminal)) continue;
                lineage ??= await LineageAsync(terminal, transaction, inside).ConfigureAwait(false);
                var scope = quest switch
                {
                    // The service's own rule for one tree, as a chain's next step reads it (NextStep).
                    _ when !string.Equals(quest.To, terminal.To, StringComparison.OrdinalIgnoreCase) => QuestCloseRefusal.OtherRepository,
                    _ when !lineage.Contains(quest.Id) => QuestCloseRefusal.NotInChain,
                    // An unbound quest is the state check's NotBound, below: it names the bind missing, not another graph.
                    { Closure: { } graph } when terminal.Closure is { } own && !string.Equals(graph, own, StringComparison.Ordinal)
                        => QuestCloseRefusal.OtherGraph,
                    _ => QuestCloseRefusal.None,
                };

                if (scope != QuestCloseRefusal.None) return await Refuse(scope, quest, quest.Id).ConfigureAwait(false);
            }

            foreach (var ((quest, history), (_, submission)) in judged.Zip(named))
            {
                var done = new QuestOperation(quest.Id, QuestOperationKind.Done, Machine, 0, now);
                var refusal = quest switch
                {
                    { Status: QuestStatus.Done or QuestStatus.Declined } => QuestCloseRefusal.Closed,
                    { Closure: null } => QuestCloseRefusal.NotBound,
                    _ when QuestLog.OnALostTake(quest, history, done)
                           || QuestLog.Claim(history, Machine) is not (QuestClaim.Held or QuestClaim.Unconfirmed) => QuestCloseRefusal.NotHeld,
                    { Submission: null } => QuestCloseRefusal.Stale,
                    _ when quest.Submission!.Machine != submission.Machine || quest.Submission.Sequence != submission.Sequence
                        => QuestCloseRefusal.Stale,
                    { Held: true } => QuestCloseRefusal.Held,
                    _ => QuestCloseRefusal.None,
                };

                if (refusal == QuestCloseRefusal.None && quest.Then.Count > 0
                    && await SealingAsync(quest, transaction, inside).ConfigureAwait(false) is null)
                {
                    refusal = QuestCloseRefusal.Unsealed;
                }

                if (refusal != QuestCloseRefusal.None) return await Refuse(refusal, quest, quest.Id).ConfigureAwait(false);
            }

            var closed = new List<Quest>();
            foreach (var (quest, history) in judged)
            {
                var operation = await AppendAsync(
                    quest.Id, QuestOperationKind.Done, now, quest.Submission!.Note, null, transaction, inside,
                    answers: quest.Answers, close: close).ConfigureAwait(false);
                var done = QuestLog.Replay([.. history, operation])!;
                await WriteCacheAsync(done, transaction, inside).ConfigureAwait(false);
                closed.Add(done);
            }

            await KeepCloseAsync(close, invocation, QuestCloseRefusal.None, null, now, transaction, inside).ConfigureAwait(false);
            return new QuestClosing(QuestCloseRefusal.None, null, closed);
        }, ct);

    /// <summary>
    /// The ids of <paramref name="quest"/> and every quest it grew from (FABRIC-CLOSE1v), walked through its parents in every repository as
    /// the driver's <c>QuestClosing.Lineage</c> walks the quests this store lists: the chain a close at it may name. A parent this store does
    /// not hold ends the walk, so what lies above it is not shown to be of the chain; a parent seen twice, which no chain makes, ends it too.
    /// </summary>
    private async Task<IReadOnlySet<string>> LineageAsync(Quest quest, SqliteTransaction transaction, CancellationToken ct)
    {
        var lineage = new HashSet<string>(StringComparer.Ordinal) { quest.Id };
        for (var at = quest; at.Parent is { Length: > 0 } parent && lineage.Add(parent);)
        {
            if (await FindAsync(parent, transaction, ct).ConfigureAwait(false) is not { } before) break;
            at = before;
        }

        return lineage;
    }

    /// <summary>
    /// What a close named, as its record compares it (FABRIC-CLOSE1e): each quest with its submission, in the quests' order, so
    /// the same close sent again names the same whatever order it lists them in.
    /// </summary>
    private static string Invocation(IReadOnlyList<(string Id, QuestOperationRef Submission)> named) =>
        JsonFields.Written(writer =>
        {
            writer.WriteStartArray();
            foreach (var (id, submission) in named.OrderBy(each => each.Id, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("quest", id);
                writer.WriteString("machine", submission.Machine);
                writer.WriteNumber("sequence", submission.Sequence);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });

    /// <summary>
    /// What became of a close sent with <paramref name="close"/>'s key (FABRIC-CLOSE1u; §E5 step 4), answered in one transaction
    /// from what is kept under the key, never by judging or writing a Done: the first outcome where the key was taken (a close, a
    /// refusal, or <see cref="QuestCloseRefusal.KeyReused"/> where this is not that close), replayed as a send of it again would
    /// be. A key never taken is <see cref="QuestCloseRefusal.Withdrawn"/>, kept under it in the same transaction with the token and
    /// submissions named, so the close it names never runs: a send of it still on its way is answered that refusal.
    /// </summary>
    /// <remarks>
    /// The driver asks this of every close whose answer it does not hold, before anything else, and never sends that close again:
    /// one never delivered was judged on evidence that may have moved since, and a key's idempotency protects an outcome already
    /// kept, not a key never used. Its close is judged again on what is there now, under a new key.
    /// </remarks>
    public Task<QuestClosing> CloseOutcomeAsync(
        IReadOnlyList<(string Id, QuestOperationRef Submission)> named, QuestClose close, DateTimeOffset now,
        CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var standing = new List<Quest>();
            foreach (var (id, _) in named)
            {
                if (QuestLog.Replay(await HistoryAsync(id, transaction, inside).ConfigureAwait(false)) is { } quest) standing.Add(quest);
            }

            var invocation = Invocation(named);
            if (await CloseKeptAsync(close.Key, transaction, inside).ConfigureAwait(false) is not { } first)
            {
                await KeepCloseAsync(close, invocation, QuestCloseRefusal.Withdrawn, null, now, transaction, inside).ConfigureAwait(false);
                return new QuestClosing(QuestCloseRefusal.Withdrawn, null, standing);
            }

            if (first.Token != close.Token || first.Named != invocation) return new QuestClosing(QuestCloseRefusal.KeyReused, null, standing);
            return new QuestClosing(first.Refusal, standing.FirstOrDefault(quest => quest.Id == first.On), standing, Replayed: true, On: first.On);
        }, ct);

    /// <summary>A close's first outcome, as kept under its key: its token, what it named, how it ended and on which quest.</summary>
    private sealed record KeptClose(string Token, string Named, QuestCloseRefusal Refusal, string? On);

    /// <summary>The outcome kept under <paramref name="key"/>, or null where no close was sent with it.</summary>
    private async Task<KeptClose?> CloseKeptAsync(string key, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = "SELECT token, named, refusal, quest FROM quest_closes WHERE key = $key";
        command.Parameters.AddWithValue("$key", key);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

        // A refusal this build does not name is still a refusal: answered as a key that is not this close's, never as a close.
        var refusal = Enum.TryParse<QuestCloseRefusal>(reader.GetString(2), out var kept) ? kept : QuestCloseRefusal.KeyReused;
        return new KeptClose(reader.GetString(0), reader.GetString(1), refusal, reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    /// <summary>Keep a close's outcome under its key (FABRIC-CLOSE1e): the store's own, never pushed or served.</summary>
    private async Task KeepCloseAsync(
        QuestClose close, string invocation, QuestCloseRefusal refusal, string? on, DateTimeOffset now,
        SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO quest_closes (key, token, named, refusal, quest, at) VALUES ($key, $token, $named, $refusal, $quest, $at)
            """;
        command.Parameters.AddWithValue("$key", close.Key);
        command.Parameters.AddWithValue("$token", close.Token);
        command.Parameters.AddWithValue("$named", invocation);
        command.Parameters.AddWithValue("$refusal", refusal.ToString());
        command.Parameters.AddWithValue("$quest", (object?)on ?? DBNull.Value);
        command.Parameters.AddWithValue("$at", Moments.Write(now));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The person accepts a done as it stands (DRIFT1d, D133 §4; EVID1a, D144 §6): its departure, or its evidence unread
    /// or missing. An <see cref="QuestOperationKind.Accepted"/> operation, judged against the replayed history inside the
    /// write as every move is, and the chain's next step it held published in the same transaction, as a close publishes one.
    /// </summary>
    /// <returns>
    /// The quest as it now stands, whether this call accepted it — false for a quest nothing holds — and the
    /// step it published; a null quest means no such id.
    /// </returns>
    public Task<QuestMove> AcceptAsync(string id, DateTimeOffset now, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var history = await HistoryAsync(id, transaction, inside).ConfigureAwait(false);
            if (QuestLog.Replay(history) is not { } quest)
            {
                return new QuestMove(await FindAsync(id, transaction, inside).ConfigureAwait(false), Moved: false);
            }

            // The replay's own rule: a departure or its evidence takes a yes, and a review's hold does not (REVIEWENV1b).
            if (!QuestLog.Applies(quest, new QuestOperation(id, QuestOperationKind.Accepted, Machine, 0, now)))
            {
                return new QuestMove(quest, Moved: false);
            }

            var operation = await AppendAsync(
                id, QuestOperationKind.Accepted, now, note: null, published: null, transaction, inside).ConfigureAwait(false);
            var accepted = QuestLog.Step(quest, operation)!;
            await WriteCacheAsync(accepted, transaction, inside).ConfigureAwait(false);
            // A set-up step accepted for its departure still waits for its review, and what follows it waits with it.
            return new QuestMove(
                accepted, Moved: true,
                accepted.Held ? null : await PublishNextAsync(accepted, now, transaction, inside).ConfigureAwait(false));
        }, ct);

    /// <summary>
    /// A set-up step's set-up (REVIEWENV1b, D154 point 9): a <see cref="QuestOperationKind.SetUp"/> operation, judged against
    /// the replayed history inside the write as every move is, so it applies only to a set-up step not declined, a second
    /// post of the same set-up is no move, and one from a take this machine lost is refused (<see cref="QuestLog.OnALostTake"/>).
    /// It releases nothing: a newer set-up holds a reviewed step for the person again.
    /// </summary>
    /// <returns>
    /// The quest as it now stands and whether this call kept the set-up; a null quest means no such id. Refused with
    /// <see cref="QuestMove.ClaimLost"/> where this machine's take on the quest lost.
    /// </returns>
    public Task<QuestMove> SetUpAsync(string id, QuestSetUp setUp, DateTimeOffset now, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var history = await HistoryAsync(id, transaction, inside).ConfigureAwait(false);
            if (QuestLog.Replay(history) is not { } quest)
            {
                return new QuestMove(await FindAsync(id, transaction, inside).ConfigureAwait(false), Moved: false);
            }

            // Its identity is made here, where its local address is still known, and crosses with it (REVIEWENV1b3).
            var said = setUp with { At = null, Machine = null, Sequence = null };
            var shown = said with { Id = said.Id ?? Reviews.IdentityOf(said) };
            var judged = new QuestOperation(id, QuestOperationKind.SetUp, Machine, 0, now, SetUp: shown);
            if (!QuestLog.Applies(quest, judged)) return new QuestMove(quest, Moved: false);
            if (QuestLog.OnALostTake(quest, history, judged)) return new QuestMove(quest, Moved: false) { ClaimLost = true };

            var operation = await AppendAsync(
                id, QuestOperationKind.SetUp, now, note: null, published: null, transaction, inside, setUp: shown).ConfigureAwait(false);
            var kept = QuestLog.Step(quest, operation)!;
            await WriteCacheAsync(kept, transaction, inside).ConfigureAwait(false);
            return new QuestMove(kept, Moved: true);
        }, ct);

    /// <summary>
    /// The person's verdict on a set-up step's review, or their skip of a review (REVIEWENV1b, D154 point 8): a
    /// <see cref="QuestOperationKind.Verdict"/> operation, judged against the replayed history inside the write. A
    /// <c>reviewed</c> or a skip that lets the step's review go publishes the chain's next step in the same transaction, as
    /// the yes publishes one; a <c>not-yet</c> keeps it held.
    /// </summary>
    /// <returns>
    /// The quest as it now stands, whether this call kept the verdict, and the step it published; a null quest means no
    /// such id.
    /// </returns>
    public Task<QuestMove> VerdictAsync(string id, QuestReviewVerdict verdict, DateTimeOffset now, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var history = await HistoryAsync(id, transaction, inside).ConfigureAwait(false);
            if (QuestLog.Replay(history) is not { } quest)
            {
                return new QuestMove(await FindAsync(id, transaction, inside).ConfigureAwait(false), Moved: false);
            }

            var said = verdict with { At = null, Machine = null };
            if (!QuestLog.Applies(quest, new QuestOperation(id, QuestOperationKind.Verdict, Machine, 0, now, Verdict: said)))
            {
                return new QuestMove(quest, Moved: false);
            }

            var operation = await AppendAsync(
                id, QuestOperationKind.Verdict, now, note: null, published: null, transaction, inside, review: said).ConfigureAwait(false);
            var given = QuestLog.Step(quest, operation)!;
            await WriteCacheAsync(given, transaction, inside).ConfigureAwait(false);
            var released = quest.Held && !given.Held;
            return new QuestMove(
                given, Moved: true, released ? await PublishNextAsync(given, now, transaction, inside).ConfigureAwait(false) : null);
        }, ct);

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

    /// <summary>
    /// Daoris read a done's evidence (EVID1a, D144 §3): an <see cref="QuestOperationKind.Evidenced"/> operation, judged
    /// against the replayed history inside the write as every move is, so it applies only to a done still waiting on
    /// exactly what it reads. Found, and nothing else holding the done, the chain's next step is published in the same
    /// transaction, as the yes publishes one; missing, the done stays held, now for that.
    /// </summary>
    /// <returns>
    /// The quest as it now stands, whether this call kept the verdict — false for a quest not waiting on what it reads —
    /// and the step it published; a null quest means no such id.
    /// </returns>
    public Task<QuestMove> EvidenceAsync(string id, QuestEvidenceVerdict verdict, DateTimeOffset now, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var history = await HistoryAsync(id, transaction, inside).ConfigureAwait(false);
            if (QuestLog.Replay(history) is not { } quest)
            {
                return new QuestMove(await FindAsync(id, transaction, inside).ConfigureAwait(false), Moved: false);
            }

            var read = verdict with { At = null, Machine = null };
            if (!QuestLog.Applies(quest, new QuestOperation(id, QuestOperationKind.Evidenced, Machine, 0, now, Evidence: read)))
            {
                return new QuestMove(quest, Moved: false);
            }

            var operation = await AppendAsync(
                id, QuestOperationKind.Evidenced, now, note: null, published: null, transaction, inside, evidence: read)
                .ConfigureAwait(false);
            var evidenced = QuestLog.Step(quest, operation)!;
            await WriteCacheAsync(evidenced, transaction, inside).ConfigureAwait(false);
            return new QuestMove(
                evidenced, Moved: true,
                evidenced.Held ? null : await PublishNextAsync(evidenced, now, transaction, inside).ConfigureAwait(false));
        }, ct);

    /// <summary>
    /// Its taker waits on a question it asked another repository (D79): a <see cref="QuestOperationKind.Waited"/>
    /// operation naming the question, judged against the replayed history inside the write, as every
    /// move is. It moves no status.
    /// </summary>
    /// <returns>
    /// The quest as it now stands and whether this call marked it; a null quest means no such id. Refused with
    /// <see cref="QuestMove.ClaimLost"/> where this machine's take on the quest lost (WAITCLAIM2).
    /// </returns>
    public Task<QuestMove> WaitAsync(string id, string on, DateTimeOffset now, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var history = await HistoryAsync(id, transaction, inside).ConfigureAwait(false);
            if (QuestLog.Replay(history) is not { } quest)
            {
                return new QuestMove(await FindAsync(id, transaction, inside).ConfigureAwait(false), Moved: false);
            }

            // Judged before anything is written: only a taken quest waits (QuestLog.Applies), and a wait
            // the log held but the quest did not show would be a move nobody could see.
            if (quest.Status != QuestStatus.Taken || string.IsNullOrWhiteSpace(on))
            {
                return new QuestMove(quest, Moved: false);
            }

            // A wait on the winner's take, made here after the pass that found this machine's take lost (WAITCLAIM2).
            if (QuestLog.OnALostTake(quest, history, new QuestOperation(id, QuestOperationKind.Waited, Machine, 0, now, on)))
            {
                return new QuestMove(quest, Moved: false) { ClaimLost = true };
            }

            var operation = await AppendAsync(
                id, QuestOperationKind.Waited, now, on, null, transaction, inside).ConfigureAwait(false);
            var waiting = QuestLog.Step(quest, operation)!;
            await WriteCacheAsync(waiting, transaction, inside).ConfigureAwait(false);
            return new QuestMove(waiting, Moved: true);
        }, ct);

    /// <summary>
    /// A person dismisses a conflict (SYNC6c) — one named by its machine and sequence, or, naming none,
    /// every one the quest carries. Each is a <see cref="QuestOperationKind.Dismissed"/> operation,
    /// committed here and carried by the next pass like any other, so the conflict goes on every
    /// machine. It moves no status.
    /// </summary>
    /// <returns>The quest as it now stands (null: no such quest) and how many conflicts went.</returns>
    public Task<QuestDismissal> DismissAsync(
        string id, string? machine, long? sequence, DateTimeOffset now, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var history = await HistoryAsync(id, transaction, inside).ConfigureAwait(false);
            if (QuestLog.Replay(history) is not { } quest)
            {
                // No such quest — or a row the log never saw, which has raced nobody and carries none.
                return new QuestDismissal(await FindAsync(id, transaction, inside).ConfigureAwait(false), 0);
            }

            // Naming NONE is every conflict. A sequence without its machine names none: sequences are
            // per machine, and reading it as "none named" dismissed every one (REV3).
            var named = quest.Conflicts
                .Where(conflict => (machine is null && sequence is null)
                    || (conflict.Machine == machine && (sequence is null || conflict.Sequence == sequence)))
                .ToList();
            var written = new List<QuestOperation>(history);
            foreach (var conflict in named)
            {
                written.Add(await AppendAsync(
                    id, QuestOperationKind.Dismissed, now, note: null, published: null, transaction, inside,
                    new QuestOperationRef(conflict.Machine, conflict.Sequence)).ConfigureAwait(false));
            }

            var standing = QuestLog.Replay(written)!;
            if (named.Count > 0) await WriteCacheAsync(standing, transaction, inside).ConfigureAwait(false);
            return new QuestDismissal(standing, named.Count);
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
        InTransactionAsync(async (transaction, inside) =>
        {
            var history = await HistoryAsync(id, transaction, inside).ConfigureAwait(false);
            var quest = history.Count > 0
                ? QuestLog.Replay(history)
                // A row the log never saw has never been pushed: what it says is all there is of it.
                : await FindAsync(id, transaction, inside).ConfigureAwait(false);
            if (quest is null) return new QuestDeletion(null, Deleted: false);
            if (quest.Status != QuestStatus.Open) return new QuestDeletion(quest, Deleted: false);

            // A push reads the log, so a quest with no history was never pushed, and goes like a local one.
            var tombstoned = history.Count > 0 && (travels || history.Any(operation => operation.Number is not null));
            await using (var command = _db.Command())
            {
                command.Transaction = transaction;
                command.CommandText = tombstoned
                    ? "DELETE FROM quests WHERE id = $id"
                    : "DELETE FROM quest_log WHERE quest = $id; DELETE FROM quests WHERE id = $id";
                command.Parameters.AddWithValue("$id", id);
                await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            if (tombstoned)
            {
                await AppendAsync(id, QuestOperationKind.Deleted, now, note: null, published: null, transaction, inside)
                    .ConfigureAwait(false);
            }

            return new QuestDeletion(quest, Deleted: true, tombstoned);
        }, ct);

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
                              + "DELETE FROM quest_log WHERE quest = $id; DELETE FROM quests WHERE id = $id;";
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
        var waitsLost = new List<string>();
        var submissionLost = false;
        // The history as this rebase leaves it so far: what QuestLog.OnALostTake reads this machine's claim from.
        var replayed = new List<QuestOperation>();
        foreach (var operation in await HistoryAsync(id, transaction, ct).ConfigureAwait(false))
        {
            var lostClaim = claimLost && operation.Number is null && QuestLog.TakersOwn(operation.Kind);

            // A stray (WAITCLAIM4): a done, a decline or a wait a build before WAITCLAIM2 made after the take's conflict
            // was written, which the remote refuses at every pass (WAITCLAIM3), so the circle never pushed clean. Judged by
            // the rule the store's verbs and the remote judge by, and lost by its kind's rule as one made on a take this
            // rebase finds lost is. A forgotten wait is named on no conflict: the take's is already written, a remote may
            // have numbered it, and no machine rewrites a numbered operation. The remote never kept the wait, so no other
            // machine's copy of the quest waits on the question.
            var stray = !lostClaim && operation.Number is null && QuestLog.OnALostTake(quest, replayed, operation);
            if (!lostClaim && !stray && (operation.Number is not null || QuestLog.Applies(quest, operation)))
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
                if (lostClaim && operation is { Kind: QuestOperationKind.Waited, Note: { } on } && !waitsLost.Contains(on))
                {
                    waitsLost.Add(on);
                }

                // A yes that lost takes the step it published with it, as a lost close's does (DRIFT1d). A verdict on a
                // done that lost needs nothing more here: the done came first in the history, and its conflict took the
                // step with it (EVID1a). A verdict that lost to another machine's, or to the person's yes, leaves the
                // step standing, since the winner published the same one.
                if (operation.Kind == QuestOperationKind.Accepted && quest is not { Accepted: not null })
                {
                    await ForgetFollowUpsAsync(id, transaction, ct).ConfigureAwait(false);
                }

                // A review's verdict that lost while the step is still held takes the step it published with it, as a lost
                // yes does (REVIEWENV1b); one that lost to another machine's leaves the step the winner published.
                if (operation.Kind == QuestOperationKind.Verdict && quest is { Held: true })
                {
                    await ForgetFollowUpsAsync(id, transaction, ct).ConfigureAwait(false);
                }

                // A submission that lost takes the step it published with it, its publication pending on the same take
                // (FABRIC-CLOSE1a, §E5), and the take's conflict names it. One that lost to another machine's done leaves the
                // step that done published, which is the same one. The step goes even once this machine took it, and its
                // take reads lost, so its session is stopped (FABRIC-CLOSE1e).
                if (operation.Kind == QuestOperationKind.Submitted && quest is not { Status: QuestStatus.Done })
                {
                    conflicts.AddRange(await ForgetStepsAsync(id, lostClaim, transaction, ct).ConfigureAwait(false));
                    submissionLost |= lostClaim;
                }

                continue;
            }

            // A move. On a quest another machine deleted first (D95) it is kept as a conflict on a quest no list shows,
            // so this machine's claim reads lost and its driver stops the session. A decline made while open that lost
            // to a take (PAUSE1c) is kept like any losing move, with its reason: the conflict says it attempted a
            // decline, and the flag, a decline's, goes with the kind.
            var lost = operation with
            {
                Kind = QuestOperationKind.Conflict, Attempted = QuestTransitions.Target(operation.Kind), WhileOpen = false, Close = null,
            };
            await RewriteAsync(position, lost, transaction, ct).ConfigureAwait(false);
            quest = quest is null ? null : QuestLog.Step(quest, lost);
            replayed.Add(lost);
            conflicts.Add(lost);
            if (operation.Kind == QuestOperationKind.Taken)
            {
                claimLost = true;
                lostTake = (position, conflicts.Count - 1);
            }

            if (operation.Kind == QuestOperationKind.Done)
            {
                await ForgetFollowUpsAsync(id, transaction, ct).ConfigureAwait(false);
            }
        }

        // The take's conflict names the waits that went with it, after its own note (WAITCLAIM1). It is rewritten before
        // it is pushed, so every machine keeps the one note.
        if (lostTake is { } take && (waitsLost.Count > 0 || submissionLost))
        {
            // A note rewritten here has no parts of its own, so it keeps none: a stale lead would word a note it no longer is.
            var note = waitsLost.Count > 0 ? WaitsWentWith(conflicts[take.Reported].Note, waitsLost) : conflicts[take.Reported].Note;
            var named = conflicts[take.Reported] with
            {
                Note = submissionLost ? SubmissionWentWith(note) : note, NoteParts = null,
            };
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

    /// <summary>
    /// A lost take's note as it was given, then the waits that went with it (WAITCLAIM1): said on the conflict, which every
    /// machine shows, so the words hold wherever they are read.
    /// </summary>
    internal static string WaitsWentWith(string? note, IReadOnlyList<string> questions)
    {
        var named = questions.Select(q => $"#{q}").ToList();
        var said = named.Count == 1
            ? $"This take's wait on {named[0]} went with it, so this quest does not wait on that question's answer."
            : $"This take's waits on {string.Join(", ", named.Take(named.Count - 1))} and {named[^1]} went with it, "
              + "so this quest does not wait on those questions' answers.";
        return string.IsNullOrWhiteSpace(note) ? said : $"{note.TrimEnd()} {said}";
    }

    /// <summary>
    /// A lost take's note, then the submission that went with it (FABRIC-CLOSE1a, §E5), said on the conflict every machine
    /// shows: the winner's work is what the quest's graph now closes.
    /// </summary>
    internal static string SubmissionWentWith(string? note)
    {
        const string Said = "This take's submission went with it, and so did any step it published; the quest's close is the winner's.";
        return string.IsNullOrWhiteSpace(note) ? Said : $"{note.TrimEnd()} {Said}";
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
    /// and the chain did not move. Forgotten only while nothing else has happened to it — a follow-up
    /// somebody has acted on, or that another machine holds, stays.
    /// </summary>
    private async Task ForgetFollowUpsAsync(string parent, SqliteTransaction transaction, CancellationToken ct)
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

        foreach (var child in children)
        {
            var history = await HistoryAsync(child, transaction, ct).ConfigureAwait(false);
            if (history is not [{ Kind: QuestOperationKind.Published, Number: null } only]) continue;

            await ForgetOperationAsync(
                (await PositionOfAsync(only.Machine, only.Sequence, transaction, ct).ConfigureAwait(false))!.Value,
                transaction, ct).ConfigureAwait(false);
            await using var drop = _db.Command();
            drop.Transaction = transaction;
            drop.CommandText = "DELETE FROM quests WHERE id = $id";
            drop.Parameters.AddWithValue("$id", child);
            await drop.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The steps a lost submission published (FABRIC-CLOSE1e, §E5), with what this machine did on them since, and the steps they
    /// published in turn: each grew from a submission that no longer stands, so none was asked. A step is forgotten only while
    /// every operation of it is this machine's and none was pushed; one a remote holds is every machine's, and stays.
    /// </summary>
    /// <remarks>
    /// Where this machine took a step, its take is not forgotten but rewritten as a conflict, which the claim reads as lost
    /// (<see cref="QuestLog.Claim"/>), so the driver stops the step's session as it stops any session whose take lost (D69). Its
    /// publication gone, the conflict is on a quest no list shows and no push carries, as one on a quest deleted first is (D95),
    /// and its note says why: the parent's submission went. Should the winner's chain publish the same step later (its id comes
    /// from the parent and the step), the conflict stands on it and goes up, as any take that lost is shown on the winner's quest.
    /// </remarks>
    /// <returns>The takes rewritten, as the rebase reports its conflicts.</returns>
    private async Task<IReadOnlyList<QuestOperation>> ForgetStepsAsync(
        string parent, bool takeLost, SqliteTransaction transaction, CancellationToken ct)
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

            told.AddRange(await ForgetStepsAsync(child, takeLost, transaction, ct).ConfigureAwait(false));
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
                    Kind = QuestOperationKind.Conflict, Attempted = QuestStatus.Taken, Note = StepWentWith(parent, takeLost, operation.Note),
                    NoteParts = null, WhileOpen = false, Close = null,
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
    /// it no longer stands, with the parent's take or to another machine's move on the parent.
    /// </summary>
    internal static string StepWentWith(string parent, bool takeLost, string? note)
    {
        var said = $"This quest was the next step of #{parent}'s submission, which "
                   + (takeLost ? $"went with this machine's take of #{parent}: another machine's take reached the remote first"
                               : $"lost to another machine's move on #{parent}")
                   + ". The step went with it, so its work is not wanted.";
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
        // A move rewritten as a conflict keeps its note's parts beside the note (QUESTCLOSE1b1).
        command.Parameters.AddWithValue(
            "$payload", PayloadJson(operation.Note, operation.Published, operation.Attempted, noteParts: operation.NoteParts));
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
                INSERT INTO quest_passes (workspace, synced, tried, behind, problem)
                VALUES ($workspace, $synced, $tried, $behind, $problem)
                ON CONFLICT (workspace) DO UPDATE SET
                  synced = COALESCE($synced, synced), tried = $tried, behind = $behind, problem = $problem
                """;
            command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));
            command.Parameters.AddWithValue("$synced", report.Problem is null ? Moments.Write(at) : DBNull.Value);
            command.Parameters.AddWithValue("$tried", Moments.Write(at));
            // A list of quest ids, kept the way links are: a plain array of strings.
            command.Parameters.AddWithValue("$behind", LinksJson(report.Behind));
            command.Parameters.AddWithValue("$problem", (object?)report.Problem ?? DBNull.Value);
            return await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
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
    /// <paramref name="base"/> that this push did not carry; <b>refused</b> when an operation does not
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
                if (await MovedSinceAsync(group.Key, @base, carried, transaction, inside).ConfigureAwait(false))
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

                    // A take that lost to a delete arrives as a conflict on a quest that is gone (D95), and
                    // is kept like any conflict, so its machine's claim reads lost on every side.
                    var onDeleted = quest is null && known && filed.Kind == QuestOperationKind.Conflict;
                    if (why is null && !onDeleted && !QuestLog.Applies(quest, filed))
                    {
                        why = quest is null
                            ? known
                                ? $"`{KindText(filed.Kind)}` on quest `#{group.Key}`, which was deleted here."
                                : $"`{KindText(filed.Kind)}` on quest `#{group.Key}`, which this deployment has never had published."
                            : filed.WhileOpen
                                // PAUSE1c: named as the decline it is, so the refusal its machine relays says why it did not land.
                                ? $"`{KindText(filed.Kind)}` applies to quest `#{group.Key}` only while it is open (`whileOpen`), "
                                  + $"and it is {quest.Status} here, so the quest stays {quest.Status}."
                                : $"`{KindText(filed.Kind)}` does not apply to quest `#{group.Key}`, which is {quest.Status}.";
                    }

                    // A done, a decline or a wait from a machine whose take on the quest lost (WAITCLAIM3): made after the
                    // pass that found the take lost, by a build whose store does not refuse it (WAITCLAIM2). The table
                    // allows it, and applied it would close or hold the winner's quest. Refused by the rule the store
                    // judges by, in the words the store's refusal is answered in, so the pushing machine's pass relays the
                    // lost take and its stand-down.
                    if (why is null && QuestLog.OnALostTake(quest, [.. history, .. staged], filed))
                    {
                        why = QuestExchange.LostTake(group.Key, filed.Kind == QuestOperationKind.Waited
                            ? $"so it does not wait on `#{filed.Note}`. The remote kept nothing this push carried for the quest; "
                              + $"`#{filed.Note}` stays a quest of its own."
                            : $"so this `{KindText(filed.Kind)}` is not this machine's to make. The remote kept nothing this push "
                              + "carried for the quest.");
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
    private const string OperationColumns =
        "quest_log.quest, quest_log.kind, quest_log.machine, quest_log.sequence, quest_log.at, quest_log.payload, quest_log.remote";

    public Task<Quest?> FindAsync(string id, CancellationToken ct = default) =>
        InGateAsync(() => FindAsync(id, transaction: null, ct), ct);

    private async Task<Quest?> FindAsync(string id, SqliteTransaction? transaction, CancellationToken ct)
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

    private static Quest Read(SqliteDataReader reader) => new(
        reader.GetString(reader.GetOrdinal("id")),
        reader.GetString(reader.GetOrdinal("sender")),
        reader.GetString(reader.GetOrdinal("receiver")),
        reader.GetString(reader.GetOrdinal("title")),
        reader.GetString(reader.GetOrdinal("body")),
        Enum.Parse<QuestStatus>(reader.GetString(reader.GetOrdinal("status"))),
        reader.IsDBNull(reader.GetOrdinal("note")) ? null : reader.GetString(reader.GetOrdinal("note")),
        Moments.ReadRequired(reader.GetString(reader.GetOrdinal("filed"))),
        Moments.ReadRequired(reader.GetString(reader.GetOrdinal("updated"))),
        Workspaces.Normalize(reader.GetString(reader.GetOrdinal("workspace"))))
    {
        Links = ReadLinks(reader.GetString(reader.GetOrdinal("links"))),
        Attachments = ReadAttachments(reader.GetString(reader.GetOrdinal("attachments"))),
        Then = ReadSteps(reader.GetString(reader.GetOrdinal("then_steps"))),
        Parent = reader.IsDBNull(reader.GetOrdinal("parent")) ? null : reader.GetString(reader.GetOrdinal("parent")),
        Conflicts = ReadConflicts(reader.GetString(reader.GetOrdinal("conflicts"))),
        Awaits = reader.IsDBNull(reader.GetOrdinal("awaits")) ? null : reader.GetString(reader.GetOrdinal("awaits")),
        PublishedBy = reader.IsDBNull(reader.GetOrdinal("published_by")) ? null : reader.GetString(reader.GetOrdinal("published_by")),
        Lanes = ReadLinks(reader.GetString(reader.GetOrdinal("lanes"))),
        Requirements = ReadRequirements(reader.GetString(reader.GetOrdinal("requirements"))),
        Answers = ReadAnswers(reader.GetString(reader.GetOrdinal("answers"))),
        Accepted = reader.IsDBNull(reader.GetOrdinal("accepted"))
            ? null
            : Moments.ReadRequired(reader.GetString(reader.GetOrdinal("accepted"))),
        Short = reader.IsDBNull(reader.GetOrdinal("short_title")) ? null : reader.GetString(reader.GetOrdinal("short_title")),
        NoteParts = reader.IsDBNull(reader.GetOrdinal("note_parts")) ? null : reader.GetString(reader.GetOrdinal("note_parts")),
        Evidence = reader.IsDBNull(reader.GetOrdinal("evidence")) ? null : ReadVerdict(reader.GetString(reader.GetOrdinal("evidence"))),
        Review = reader.IsDBNull(reader.GetOrdinal("review")) ? null : ReadColumn(reader.GetString(reader.GetOrdinal("review")), Reviews.JudgedReview),
        SetUpIn = reader.IsDBNull(reader.GetOrdinal("set_up_in")) ? null : reader.GetString(reader.GetOrdinal("set_up_in")),
        SetUps = ReadList(reader.GetString(reader.GetOrdinal("set_ups")), Reviews.JudgedSetUp),
        Verdicts = ReadList(reader.GetString(reader.GetOrdinal("verdicts")), Reviews.JudgedVerdict),
        Closure = reader.IsDBNull(reader.GetOrdinal("closure")) ? null : reader.GetString(reader.GetOrdinal("closure")),
        Submission = reader.IsDBNull(reader.GetOrdinal("submission")) ? null : ReadSubmission(reader.GetString(reader.GetOrdinal("submission"))),
        GrewFrom = ReadRefColumn(reader, "grew_from"),
        SourceReopened = ReadRefColumn(reader, "source_reopened"),
        Reopened = reader.IsDBNull(reader.GetOrdinal("reopened")) ? null : ReadReopen(reader.GetString(reader.GetOrdinal("reopened"))),
    };

    /// <summary>A current submission as this store's own column keeps it (FABRIC-CLOSE1a; its session, FABRIC-CLOSE1c4).</summary>
    private static QuestSubmission ReadSubmission(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;
        return new QuestSubmission(
            root.GetProperty("machine").GetString() ?? "", root.GetProperty("sequence").GetInt64(),
            Moments.ReadRequired(root.GetProperty("at").GetString()!),
            root.TryGetProperty("note", out var note) ? note.GetString() : null,
            root.TryGetProperty("session", out var session) ? session.GetString() : null);
    }

    /// <summary>A quest's own reopen as this store's own column keeps it (FABRIC-CLOSE1c4); one not whole is none.</summary>
    private static QuestReopen? ReadReopen(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;
        return ReadRef(root) is { } named && root.TryGetProperty("of", out var of) && ReadRef(of) is { } reopened
            && root.TryGetProperty("at", out var at) && at.GetString() is { } when
            ? new QuestReopen(
                named.Machine, named.Sequence, reopened, root.TryGetProperty("note", out var note) ? note.GetString() : null,
                Moments.ReadRequired(when))
            : null;
    }

    /// <summary>One value of this store's own column, read through the judge the wire reads it with (REVIEWENV1b).</summary>
    private static T? ReadColumn<T>(string json, Func<System.Text.Json.JsonElement, T?> judged) where T : class
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return judged(document.RootElement);
    }

    /// <summary>A list of this store's own column, each item read through its judge; one that is not whole is passed over.</summary>
    private static IReadOnlyList<T> ReadList<T>(string json, Func<System.Text.Json.JsonElement, T?> judged) where T : class
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array
            ? []
            : [.. document.RootElement.EnumerateArray().Select(judged).OfType<T>()];
    }

    private static QuestEvidenceVerdict? ReadVerdict(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return QuestEvidenceVerdict.Judged(document.RootElement);
    }

    /// <summary>An operation as its log row holds it (<see cref="OperationColumns"/>) — a publish's payload is the quest as asked.</summary>
    private static QuestOperation ReadOperation(SqliteDataReader reader)
    {
        var quest = reader.GetString(0);
        var kind = Enum.Parse<QuestOperationKind>(reader.GetString(1), ignoreCase: true);
        var at = Moments.ReadRequired(reader.GetString(4));

        using var document = System.Text.Json.JsonDocument.Parse(reader.GetString(5));
        var payload = document.RootElement;
        var note = payload.TryGetProperty("note", out var said) ? said.GetString() : null;
        QuestStatus? attempted = payload.TryGetProperty("attempted", out var tried)
            ? Enum.Parse<QuestStatus>(tried.GetString()!, ignoreCase: true)
            : null;
        var published = kind != QuestOperationKind.Published
            ? null
            : new Quest(
                quest,
                payload.GetProperty("from").GetString() ?? "",
                payload.GetProperty("to").GetString() ?? "",
                payload.GetProperty("title").GetString() ?? "",
                payload.GetProperty("body").GetString() ?? "",
                QuestStatus.Open, null, at, at,
                Workspace: Workspaces.Normalize(payload.GetProperty("workspace").GetString()))
            {
                Links = ReadLinks(payload.GetProperty("links")),
                Attachments = ReadAttachments(payload.GetProperty("attachments")),
                Then = ReadSteps(payload.GetProperty("then")),
                Parent = payload.TryGetProperty("parent", out var parent) ? parent.GetString() : null,
                PublishedBy = payload.TryGetProperty("publishedBy", out var by) ? by.GetString() : null,
                // Absent from every publish before lanes, which asked the whole repository (D115 §2.2).
                Lanes = payload.TryGetProperty("lanes", out var lanes) ? ReadLinks(lanes) : [],
                // Absent from every publish before requirements, and from one that names none (DRIFT1c).
                Requirements = payload.TryGetProperty("requirements", out var required) ? ReadRequirements(required) : [],
                // Absent from every publish before short titles, and from one that gave none (SESSUX1j).
                Short = payload.TryGetProperty("short", out var shortTitle) ? shortTitle.GetString() : null,
                // Absent from every publish before reviews, and from one that chose none or is no set-up step (REVIEWENV1b).
                Review = payload.TryGetProperty("review", out var chosen) ? Reviews.JudgedReview(chosen) : null,
                SetUpIn = payload.TryGetProperty("setUpIn", out var setUpIn) ? setUpIn.GetString() : null,
                // Absent from every publish but a step a submission published (FABRIC-CLOSE1c).
                GrewFrom = payload.TryGetProperty("grewFrom", out var grew) ? ReadRef(grew) : null,
            };

        QuestOperationRef? dismisses = payload.TryGetProperty("dismisses", out var named)
            ? new QuestOperationRef(named.GetProperty("machine").GetString() ?? "", named.GetProperty("sequence").GetInt64())
            : null;

        return new QuestOperation(
            quest, kind, reader.GetString(2), reader.GetInt64(3), at, note, published, attempted,
            reader.IsDBNull(6) ? null : reader.GetInt64(6), dismisses,
            // Absent from every done before answers, and from one on a quest with no requirements (DRIFT1d).
            payload.TryGetProperty("answers", out var answered) ? ReadAnswers(answered) : null,
            // Absent from every decline before PAUSE1c and from every plain one: a plain decline.
            kind == QuestOperationKind.Declined
            && payload.TryGetProperty("whileOpen", out var open) && open.ValueKind == System.Text.Json.JsonValueKind.True,
            // An evidenced operation's verdict (EVID1a), which this store wrote only after the exchange judged it.
            kind == QuestOperationKind.Evidenced && payload.TryGetProperty("evidence", out var read)
                ? QuestEvidenceVerdict.Judged(read)
                : null,
            // A set-up and a review's verdict (REVIEWENV1b), each written only after the exchange judged it.
            kind == QuestOperationKind.SetUp && payload.TryGetProperty("setUp", out var shown) ? Reviews.JudgedSetUp(shown) : null,
            kind == QuestOperationKind.Verdict && payload.TryGetProperty("verdict", out var given) ? Reviews.JudgedVerdict(given) : null,
            // Absent from every operation before note parts, and from every note written without them (QUESTCLOSE1b1).
            note is not null && payload.TryGetProperty("noteParts", out var parts) ? NoteParts.Normalize(parts) : null,
            // Absent from every done but a graph's close's (FABRIC-CLOSE1a), which this store wrote whole.
            kind == QuestOperationKind.Done && payload.TryGetProperty("close", out var close) ? ReadClose(close) : null,
            // A reopen's or a rebind's submission (FABRIC-CLOSE1c), which this store wrote whole.
            Names(kind) && payload.TryGetProperty("submission", out var submission) ? ReadRef(submission) : null,
            // Absent from every submission before FABRIC-CLOSE1c4, and from one no session said.
            kind == QuestOperationKind.Submitted && payload.TryGetProperty("session", out var speaker)
            && speaker.ValueKind == System.Text.Json.JsonValueKind.String
                ? speaker.GetString()
                : null);
    }

    /// <summary>A close's provenance (FABRIC-CLOSE1a), or null where it is not whole: a key and a token, both strings.</summary>
    internal static QuestClose? ReadClose(System.Text.Json.JsonElement close) =>
        close.ValueKind == System.Text.Json.JsonValueKind.Object
        && close.TryGetProperty("key", out var key) && key.ValueKind == System.Text.Json.JsonValueKind.String
        && close.TryGetProperty("token", out var token) && token.ValueKind == System.Text.Json.JsonValueKind.String
        && key.GetString() is { Length: > 0 } named && token.GetString() is { Length: > 0 } computed
            ? new QuestClose(named, computed)
            : null;

    /// <summary>A close's provenance (FABRIC-CLOSE1a), as the log and the wire write it.</summary>
    internal static void WriteClose(System.Text.Json.Utf8JsonWriter writer, QuestClose close)
    {
        writer.WriteStartObject("close");
        writer.WriteString("key", close.Key);
        writer.WriteString("token", close.Token);
        writer.WriteEndObject();
    }

    /// <summary>How a kind is written in the log: its name in lowercase, as the design names it.</summary>
    private static string KindText(QuestOperationKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>
    /// What an operation carries. A publish carries the quest's words and everything it carries — all
    /// a replay needs to make the quest, on this machine or another — and a move carries its note.
    /// </summary>
    private static string PayloadJson(
        string? note, Quest? published, QuestStatus? attempted, QuestOperationRef? dismisses = null,
        IReadOnlyList<QuestAnswer>? answers = null, bool whileOpen = false, QuestEvidenceVerdict? evidence = null,
        QuestSetUp? setUp = null, QuestReviewVerdict? review = null, string? noteParts = null, QuestClose? close = null,
        QuestOperationRef? submission = null, string? session = null) =>
        JsonFields.Written(writer =>
    {
        if (dismisses is not null)
        {
            // A dismissal carries the conflict it names (SYNC6c), and nothing else.
            writer.WriteStartObject();
            writer.WriteStartObject("dismisses");
            writer.WriteString("machine", dismisses.Machine);
            writer.WriteNumber("sequence", dismisses.Sequence);
            writer.WriteEndObject();
            writer.WriteEndObject();
            return;
        }

        if (attempted is { } lost)
        {
            writer.WriteStartObject();
            writer.WriteString("attempted", lost.ToString());
            if (note is not null) writer.WriteString("note", note);
            WriteNoteParts(writer, note, noteParts);
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();
        if (published is not null)
        {
            writer.WriteString("from", published.From);
            writer.WriteString("to", published.To);
            writer.WriteString("title", published.Title);
            writer.WriteString("body", published.Body);
            writer.WriteString("workspace", published.Workspace);
            writer.WritePropertyName("links");
            WriteLinks(writer, published.Links);
            writer.WritePropertyName("attachments");
            WriteAttachments(writer, published.Attachments);
            writer.WritePropertyName("then");
            WriteSteps(writer, published.Then);
            if (published.Parent is not null) writer.WriteString("parent", published.Parent);
            if (published.PublishedBy is not null) writer.WriteString("publishedBy", published.PublishedBy);
            // Only when it names some (D115 §2.2): a publish to the whole repository reads as it always did.
            if (published.Lanes.Count > 0)
            {
                writer.WritePropertyName("lanes");
                WriteLinks(writer, published.Lanes);
            }

            // Only when it names some (DRIFT1c), for the lanes' reason: a publish with none reads as it did.
            if (published.Requirements.Count > 0)
            {
                writer.WritePropertyName("requirements");
                WriteRequirements(writer, published.Requirements);
            }

            // Only when its publisher gave one (SESSUX1j), for the lanes' reason; a derived name is never written.
            if (published.Short is not null) writer.WriteString("short", published.Short);

            // Only when set (REVIEWENV1b), for the lanes' reason: a quest that chose nothing and is no set-up step reads as it did.
            if (published.Review is { } chosen)
            {
                writer.WritePropertyName("review");
                Reviews.Write(writer, chosen);
            }

            if (published.SetUpIn is not null) writer.WriteString("setUpIn", published.SetUpIn);

            // Only on a step a submission published (FABRIC-CLOSE1c), for the lanes' reason: every other publish reads as it did.
            if (published.GrewFrom is { } from) WriteRef(writer, "grewFrom", from);
        }

        if (note is not null) writer.WriteString("note", note);
        WriteNoteParts(writer, note, noteParts);

        // Only when set (PAUSE1c), for the lanes' reason: a plain decline is recorded exactly as before it.
        if (whileOpen) writer.WriteBoolean("whileOpen", true);

        // Only when it answers some (DRIFT1d), for the lanes' reason: a done on a quest with none reads as it did.
        if (answers is { Count: > 0 })
        {
            writer.WritePropertyName("answers");
            WriteAnswers(writer, answers);
        }

        // Only on an evidenced operation (EVID1a): its when and machine are the operation's own columns.
        if (evidence is not null)
        {
            writer.WritePropertyName("evidence");
            evidence.Write(writer);
        }

        // Only on a set-up or a verdict (REVIEWENV1b), for the evidence's reason: its when and machine are the operation's own.
        if (setUp is not null)
        {
            writer.WritePropertyName("setUp");
            Reviews.Write(writer, setUp);
        }

        if (review is not null)
        {
            writer.WritePropertyName("verdict");
            Reviews.Write(writer, review);
        }

        // Only on a done the graph's close wrote (FABRIC-CLOSE1a): every other done reads as it did.
        if (close is not null) WriteClose(writer, close);

        // Only on a reopen or a rebind (FABRIC-CLOSE1c), for the lanes' reason: every other operation reads as it did.
        if (submission is not null) WriteRef(writer, "submission", submission);

        // Only on a submission a session said (FABRIC-CLOSE1c4), for the lanes' reason: every other operation reads as it did.
        if (session is not null) writer.WriteString("session", session);

        writer.WriteEndObject();
    });

    /// <summary>An operation named by its machine and sequence, as the log and the wire write it (FABRIC-CLOSE1c).</summary>
    internal static void WriteRef(System.Text.Json.Utf8JsonWriter writer, string name, QuestOperationRef named)
    {
        writer.WriteStartObject(name);
        WriteRefFields(writer, named);
        writer.WriteEndObject();
    }

    private static void WriteRefFields(System.Text.Json.Utf8JsonWriter writer, QuestOperationRef named)
    {
        writer.WriteString("machine", named.Machine);
        writer.WriteNumber("sequence", named.Sequence);
    }

    /// <summary>An operation's name as this store's own column keeps it (FABRIC-CLOSE1c).</summary>
    private static QuestOperationRef? ReadRefColumn(SqliteDataReader reader, string column)
    {
        var at = reader.GetOrdinal(column);
        if (reader.IsDBNull(at)) return null;
        using var document = System.Text.Json.JsonDocument.Parse(reader.GetString(at));
        return ReadRef(document.RootElement);
    }

    /// <summary>
    /// An operation's name as the log and the wire hold it (FABRIC-CLOSE1c), or null where it is not whole: a machine that is a
    /// non-empty string, and a positive sequence.
    /// </summary>
    internal static QuestOperationRef? ReadRef(System.Text.Json.JsonElement named) =>
        named.ValueKind == System.Text.Json.JsonValueKind.Object
        && named.TryGetProperty("machine", out var machine) && machine.ValueKind == System.Text.Json.JsonValueKind.String
        && machine.GetString() is { Length: > 0 } at
        && named.TryGetProperty("sequence", out var sequence) && sequence.ValueKind == System.Text.Json.JsonValueKind.Number
        && sequence.TryGetInt64(out var count) && count > 0
            ? new QuestOperationRef(at, count)
            : null;

    /// <summary>
    /// A note's parts beside it (QUESTCLOSE1b1, D142 point 2): a list, only where the note was written with some, so an
    /// operation without them is recorded exactly as before, and a reader from before ignores the field.
    /// </summary>
    internal static void WriteNoteParts(System.Text.Json.Utf8JsonWriter writer, string? note, string? noteParts)
    {
        if (note is null || NoteParts.Element(noteParts) is not { } parts) return;
        writer.WritePropertyName("noteParts");
        parts.WriteTo(writer);
    }

    private static string AnswersJson(IReadOnlyList<QuestAnswer> answers) =>
        JsonFields.Written(writer => WriteAnswers(writer, answers));

    /// <summary>
    /// A done's answers, one shape in the cache, its log's payload and on the wire (DRIFT1d): each its requirement's
    /// number and either <c>met</c>, or <c>departed</c> with the <c>quote</c> it turns on.
    /// </summary>
    internal static void WriteAnswers(System.Text.Json.Utf8JsonWriter writer, IReadOnlyList<QuestAnswer> answers)
    {
        writer.WriteStartArray();
        foreach (var answer in answers)
        {
            writer.WriteStartObject();
            writer.WriteNumber("requirement", answer.Requirement);
            if (answer.Met is not null) writer.WriteString("met", answer.Met);
            if (answer.Departed is not null) writer.WriteString("departed", answer.Departed);
            if (answer.Quote is not null) writer.WriteString("quote", answer.Quote);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static IReadOnlyList<QuestAnswer> ReadAnswers(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return ReadAnswers(document.RootElement);
    }

    // The store's own column and log, which only this service writes; the wire judges what it reads instead.
    private static IReadOnlyList<QuestAnswer> ReadAnswers(System.Text.Json.JsonElement answers) =>
        answers.EnumerateArray()
            .Select(item => new QuestAnswer(
                item.GetProperty("requirement").GetInt32(),
                item.TryGetProperty("met", out var met) ? met.GetString() : null,
                item.TryGetProperty("departed", out var departed) ? departed.GetString() : null,
                item.TryGetProperty("quote", out var quote) ? quote.GetString() : null))
            .ToList();

    private static string ConflictsJson(IReadOnlyList<QuestConflict> conflicts) => JsonFields.Written(writer =>
    {
        writer.WriteStartArray();
        foreach (var conflict in conflicts)
        {
            writer.WriteStartObject();
            writer.WriteString("machine", conflict.Machine);
            writer.WriteNumber("sequence", conflict.Sequence);
            writer.WriteString("attempted", conflict.Attempted.ToString());
            if (conflict.Note is not null) writer.WriteString("note", conflict.Note);
            writer.WriteString("at", Moments.Write(conflict.At));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    });

    private static IReadOnlyList<QuestConflict> ReadConflicts(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(item => new QuestConflict(
                item.GetProperty("machine").GetString() ?? "",
                Enum.Parse<QuestStatus>(item.GetProperty("attempted").GetString()!, ignoreCase: true),
                item.TryGetProperty("note", out var note) ? note.GetString() : null,
                Moments.ReadRequired(item.GetProperty("at").GetString()!),
                // Absent from a cache written before dismissals: zero, which the open refills.
                item.TryGetProperty("sequence", out var sequence) ? sequence.GetInt64() : 0))
            .ToList();
    }

    private static string StepsJson(IReadOnlyList<QuestStep> steps) => JsonFields.Written(writer => WriteSteps(writer, steps));

    private static void WriteSteps(System.Text.Json.Utf8JsonWriter writer, IReadOnlyList<QuestStep> steps)
    {
        writer.WriteStartArray();
        foreach (var step in steps)
        {
            writer.WriteStartObject();
            writer.WriteString("to", step.To);
            writer.WriteString("title", step.Title);
            writer.WriteString("body", step.Body);
            // Only on a set-up step (REVIEWENV1b), for the lanes' reason: every other step reads as it did.
            if (step.SetUpIn is not null) writer.WriteString("setUpIn", step.SetUpIn);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static IReadOnlyList<QuestStep> ReadSteps(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return ReadSteps(document.RootElement);
    }

    private static IReadOnlyList<QuestStep> ReadSteps(System.Text.Json.JsonElement steps) =>
        steps.EnumerateArray()
            .Select(item => new QuestStep(
                item.GetProperty("to").GetString() ?? "",
                item.GetProperty("title").GetString() ?? "",
                item.GetProperty("body").GetString() ?? "")
            {
                SetUpIn = item.TryGetProperty("setUpIn", out var setUpIn) ? setUpIn.GetString() : null,
            })
            .ToList();

    private static string RequirementsJson(IReadOnlyList<QuestRequirement> requirements) =>
        JsonFields.Written(writer => WriteRequirements(writer, requirements));

    /// <summary>A quest's requirements, one shape in its column, its log's payload and on the wire (DRIFT1c, EVID1a).</summary>
    internal static void WriteRequirements(System.Text.Json.Utf8JsonWriter writer, IReadOnlyList<QuestRequirement> requirements)
    {
        writer.WriteStartArray();
        foreach (var requirement in requirements)
        {
            writer.WriteStartObject();
            writer.WriteString("quote", requirement.Quote);
            writer.WriteString("check", requirement.Check);
            // Only when it names some (EVID1a), for the lanes' reason: a requirement with none reads as it did.
            if (requirement.Evidence.Count > 0)
            {
                writer.WritePropertyName("evidence");
                QuestEvidence.Write(writer, requirement.Evidence);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static IReadOnlyList<QuestRequirement> ReadRequirements(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return ReadRequirements(document.RootElement);
    }

    // The store's own column and log, which only this service writes; the wire judges what it reads instead.
    private static IReadOnlyList<QuestRequirement> ReadRequirements(System.Text.Json.JsonElement requirements) =>
        requirements.EnumerateArray()
            .Select(item => new QuestRequirement(
                item.GetProperty("quote").GetString() ?? "",
                item.GetProperty("check").GetString() ?? "")
            {
                // Absent from every requirement before evidence, and from one that names none (EVID1a).
                Evidence = item.TryGetProperty("evidence", out var evidence) ? QuestEvidence.Judged(evidence) ?? [] : [],
            })
            .ToList();

    // Written and read by hand rather than through the reflection serializer, for the same reason the
    // registration store's lists are: nothing here may quietly stop working under AOT.
    private static string LinksJson(IReadOnlyList<string> links) => JsonFields.Written(writer => WriteLinks(writer, links));

    private static void WriteLinks(System.Text.Json.Utf8JsonWriter writer, IReadOnlyList<string> links)
    {
        writer.WriteStartArray();
        foreach (var link in links) writer.WriteStringValue(link);
        writer.WriteEndArray();
    }

    private static string AttachmentsJson(IReadOnlyList<QuestAttachment> attachments) =>
        JsonFields.Written(writer => WriteAttachments(writer, attachments));

    private static void WriteAttachments(System.Text.Json.Utf8JsonWriter writer, IReadOnlyList<QuestAttachment> attachments)
    {
        writer.WriteStartArray();
        foreach (var attachment in attachments) attachment.Write(writer);
        writer.WriteEndArray();
    }


    private static IReadOnlyList<string> ReadLinks(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return ReadLinks(document.RootElement);
    }

    private static IReadOnlyList<string> ReadLinks(System.Text.Json.JsonElement links) =>
        links.EnumerateArray()
            .Select(item => item.GetString())
            .OfType<string>()
            .ToList();

    private static IReadOnlyList<QuestAttachment> ReadAttachments(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return ReadAttachments(document.RootElement);
    }

    private static IReadOnlyList<QuestAttachment> ReadAttachments(System.Text.Json.JsonElement attachments) =>
        attachments.EnumerateArray().Select(QuestAttachment.Stored).ToList();
}
