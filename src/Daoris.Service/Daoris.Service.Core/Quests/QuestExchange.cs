namespace Daoris.Knowledge;

/// <summary>
/// The judgement half of the quest system: who may be addressed, what a refusal says, what a response
/// requires. The stores hold state; this decides.
/// </summary>
/// <remarks>
/// <para>It exists because there are two doors — MCP for an agent beside the repositories, HTTP for a
/// remote deployment — and the rules were about to be written twice. Two copies would drift, and the
/// same ask would then be deliverable through one door and refused at the other, which for a quest
/// system is the worst available bug: it looks like the sibling ignoring you.</para>
///
/// <para>The messages are composed here too, not only the verdicts. They are what an agent acts on, so
/// two hosts phrasing them differently is two behaviours in all the ways that matter.</para>
///
/// <para><b>Every verb commits here</b> (D68): a quest shared with a team is published, taken and
/// closed on this machine like any other, and reaches the remote on the next sync. Nothing fails
/// because a remote is down. One verb WAITS on it when it answers: a take on a shared quest claims by
/// push (D69), so a take that lost is known before any work starts.</para>
///
/// <para>The exchange is the root that wires: each responsibility is a type of its own
/// (<see cref="QuestPublishing"/>, <see cref="QuestResponses"/>, <see cref="QuestGraphBinds"/>, <see cref="QuestReadies"/>,
/// <see cref="QuestGraphCloses"/>, <see cref="QuestPersonMoves"/>, <see cref="QuestBringForwards"/>, <see cref="QuestEvidenceReads"/>,
/// <see cref="QuestAnswering"/>, <see cref="QuestDeletes"/>, <see cref="QuestWorkflowKinds"/>, <see cref="QuestReceived"/>,
/// <see cref="QuestCarry"/>), and each door here is one line to it.</para>
/// </remarks>
public sealed class QuestExchange
{
    /// <summary>How many requirements a quest carries — what the work is measured by, not everything said.</summary>
    public const int MaxRequirements = 20;

    /// <summary>How long a requirement's quote, or its check, may be.</summary>
    public const int MaxRequirementLength = 2000;

    /// <summary>How many links a quest carries — a ticket and its neighbours, not a bibliography.</summary>
    public const int MaxLinks = 20;

    /// <summary>How long one link may be.</summary>
    public const int MaxLinkLength = 2048;

    /// <summary>How many files a quest carries.</summary>
    public const int MaxAttachments = 10;

    /// <summary>
    /// How many bytes a quest's files come to TOGETHER — screenshots, a log, a document. Anything
    /// larger has a home of its own already, and a link to it is the better thing to carry.
    /// </summary>
    public const long MaxAttachmentBytes = 20L * 1024 * 1024;

    /// <summary>
    /// How many steps a chain carries after its first quest — develop, verify, report is two. A longer
    /// chain is a plan, and a plan belongs to whoever composes it, step by step.
    /// </summary>
    public const int MaxChain = 5;

    private readonly QuestStore quests;
    private readonly QuestReviews reviews;
    private readonly QuestCarry carry;
    private readonly QuestPublishing publishing;
    private readonly QuestReceived received;
    private readonly QuestWorkflowKinds kinds;
    private readonly QuestResponses responses;
    private readonly QuestGraphBinds binds;
    private readonly QuestGraphCloses closes;
    private readonly QuestPersonMoves personMoves;
    private readonly QuestBringForwards bringForwards;
    private readonly QuestEvidenceReads evidence;
    private readonly QuestDeletes deletes;

    /// <param name="remotes">
    /// This machine's remotes, by workspace — what a take claims at, and what a chain's composition asks,
    /// because a chain's steps must all be shared or all be local. Null on a machine with none, and on a
    /// remote itself.
    /// </param>
    /// <param name="sessions">
    /// The session records — what a delete asks whether any session was started for a quest (D95). Null
    /// where none are kept, where no session can stand in a delete's way.
    /// </param>
    /// <param name="asks">
    /// The asks — whose words a requirement's quote is checked against (D133 §3). Null where none
    /// are kept, where a requirement has no words to quote and is refused rather than kept unchecked.
    /// </param>
    public QuestExchange(
        KnowledgeService service, QuestStore quests, IRemotes? remotes = null, QuestFiles? files = null,
        SessionStore? sessions = null, AskStore? asks = null)
    {
        this.quests = quests;
        reviews = new(quests, sessions, asks);
        carry = new(files);
        kinds = new(quests, asks);
        var claims = new QuestRemoteClaims(service, quests, remotes);
        var answering = new QuestAnswering(asks, reviews);
        binds = new(quests, remotes);
        publishing = new(service, quests, remotes, asks, reviews, carry, new QuestRequirementJudge(asks), kinds);
        received = new(carry);
        responses = new(quests, reviews, answering, new QuestReadies(quests, reviews, answering), binds, claims);
        closes = new(quests);
        personMoves = new(quests, reviews);
        bringForwards = new(quests, service, sessions, claims);
        evidence = new(quests, reviews);
        deletes = new(service, quests, files, sessions, asks, claims);
    }

    /// <summary>
    /// The store this exchange judges over — for the ask desk, which reads an ask's standing from the
    /// quests asked by it (USE1c) and judges nothing of its own about them.
    /// </summary>
    internal QuestStore Store => quests;

    /// <summary>Post a set-up on a set-up step; the doors are <see cref="QuestReviews.PostSetUpAsync"/>'s.</summary>
    public Task<QuestRespondOutcome> PostSetUpAsync(string id, QuestSetUpPost post, DateTimeOffset now, CancellationToken ct = default) =>
        reviews.PostSetUpAsync(id, post, now, ct);

    /// <summary>The person's verdict on a set-up step; see <see cref="QuestReviews.ReviewAsync"/>.</summary>
    public Task<QuestRespondOutcome> ReviewAsync(
        string id, string? verdict, string? words, QuestOperationRef? setUp, DateTimeOffset now, CancellationToken ct = default) =>
        reviews.ReviewAsync(id, verdict, words, setUp, now, ct);

    /// <summary>The person's "Set it up in an environment"; see <see cref="QuestReviews.PublishSetUpStepAsync"/>.</summary>
    public Task<QuestRespondOutcome> PublishSetUpStepAsync(
        string follows, string? environment, DateTimeOffset now, CancellationToken ct = default) =>
        reviews.PublishSetUpStepAsync(follows, environment, now, ct);

    /// <summary>The person sets the kind and workflow of a quest published with no ask; see <see cref="QuestWorkflowKinds.ChooseWorkflowAsync"/>.</summary>
    public Task<QuestRespondOutcome> ChooseWorkflowAsync(
        string id, string? kind, string? workflow, string? words, DateTimeOffset now, CancellationToken ct = default) =>
        kinds.ChooseWorkflowAsync(id, kind, workflow, words, now, ct);

    /// <summary>A quest that carries nothing but its words — every caller before D65.</summary>
    public Task<QuestPublishOutcome> PublishAsync(
        string from, string to, string title, string body, DateTimeOffset now, CancellationToken ct = default) =>
        PublishAsync(new QuestAsk(from, to, title, body), now, ct);

    /// <summary>Publish a quest, judged before anything is written; see <see cref="QuestPublishing.PublishAsync"/>.</summary>
    public Task<QuestPublishOutcome> PublishAsync(QuestAsk ask, DateTimeOffset now, CancellationToken ct = default) =>
        publishing.PublishAsync(ask, now, ct);

    /// <summary>What would let a repository that declares no lanes hand its own work out; see <see cref="QuestLanes.NoLanesWay"/>.</summary>
    public static string NoLanesWay(string repository) => QuestLanes.NoLanesWay(repository);

    /// <summary>A remote's say over a publish a machine pushed; see <see cref="QuestReceived.Judge"/>. Null when the quest is fit to keep here.</summary>
    public string? JudgeReceived(Quest asked, IReadOnlyList<Registration> registered) => received.Judge(asked, registered);

    /// <summary>The same judgement over what an ASK carries; see <see cref="QuestCarry.JudgeCarry"/>.</summary>
    public (QuestPublishRefusal? Refusal, string Message, IReadOnlyList<string> Links) JudgeCarry(
        IReadOnlyList<string> links, IReadOnlyList<QuestUpload> uploads) => carry.JudgeCarry(links, uploads);

    /// <summary>Answer a quest: take, done, ready, decline or wait; see <see cref="QuestResponses.RespondAsync"/>.</summary>
    public Task<QuestRespondOutcome> RespondAsync(
        string id, string action, string? reason, DateTimeOffset now, CancellationToken ct = default,
        string? on = null, IReadOnlyList<QuestAnswer>? answers = null, bool whileOpen = false, string? session = null,
        bool handback = false) =>
        responses.RespondAsync(id, action, reason, now, ct, on, answers, whileOpen, session, handback);

    /// <summary>The lost take's words, for a store move or a remote's relayed refusal; see <see cref="QuestWords.LostTake(string, string, bool, bool)"/>.</summary>
    internal static string LostTake(string id, string what, bool reopened = false, bool outgrown = false) =>
        QuestWords.LostTake(id, what, reopened, outgrown);

    /// <summary>The lost take's words for a move the store refused with <see cref="QuestMove.ClaimLost"/>.</summary>
    internal static string LostTake(string id, string what, QuestMove move) => QuestWords.LostTake(id, what, move);

    /// <summary>The driver binds a taken quest to the graph that closes it; see <see cref="QuestGraphBinds.BindAsync"/>.</summary>
    public Task<QuestRespondOutcome> BindAsync(
        string id, string? graph, DateTimeOffset now, CancellationToken ct = default, bool atTake = false, string? session = null) =>
        binds.BindAsync(id, graph, now, ct, atTake, session);

    /// <summary>The graph's close door; see <see cref="QuestGraphCloses.CloseAsync"/>.</summary>
    public Task<QuestCloseOutcome> CloseAsync(
        string id, string? key, string? token, IReadOnlyList<(string Id, QuestOperationRef Submission)> named,
        DateTimeOffset now, CancellationToken ct = default) =>
        closes.CloseAsync(id, key, token, named, now, ct);

    /// <summary>What became of a close sent with a key; see <see cref="QuestGraphCloses.CloseOutcomeAsync"/>.</summary>
    public Task<QuestCloseOutcome> CloseOutcomeAsync(
        string id, string? key, string? token, IReadOnlyList<(string Id, QuestOperationRef Submission)> named,
        DateTimeOffset now, CancellationToken ct = default) =>
        closes.CloseOutcomeAsync(id, key, token, named, now, ct);

    /// <summary>The person accepts a done's departure or a done held for its evidence; see <see cref="QuestPersonMoves.AcceptAsync"/>.</summary>
    public Task<QuestRespondOutcome> AcceptAsync(string id, DateTimeOffset now, CancellationToken ct = default) =>
        personMoves.AcceptAsync(id, now, ct);

    /// <summary>The person reopens a sealed submission; see <see cref="QuestPersonMoves.ReopenAsync"/>.</summary>
    public Task<QuestRespondOutcome> ReopenAsync(
        string id, QuestOperationRef submission, string? note, DateTimeOffset now, CancellationToken ct = default) =>
        personMoves.ReopenAsync(id, submission, note, now, ct);

    /// <summary>The person's word to go on with a step its source's reopen held; see <see cref="QuestPersonMoves.ContinueAsync"/>.</summary>
    public Task<QuestRespondOutcome> ContinueAsync(string id, DateTimeOffset now, CancellationToken ct = default) =>
        personMoves.ContinueAsync(id, now, ct);

    /// <summary>The person brings this machine's lost take forward; see <see cref="QuestBringForwards.BringForwardAsync"/>.</summary>
    public Task<QuestRespondOutcome> BringForwardAsync(
        string id, QuestOperationRef conflict, DateTimeOffset now, CancellationToken ct = default) =>
        bringForwards.BringForwardAsync(id, conflict, now, ct);

    /// <summary>The sentence a person's done writes on its quest; see <see cref="QuestPersonMoves.PersonDoneNote"/>.</summary>
    public static string PersonDoneNote(string? words) => QuestPersonMoves.PersonDoneNote(words);

    /// <summary>The person marks a quest done; see <see cref="QuestPersonMoves.PersonDoneAsync"/>.</summary>
    public Task<QuestRespondOutcome> PersonDoneAsync(string id, string? words, DateTimeOffset now, CancellationToken ct = default) =>
        personMoves.PersonDoneAsync(id, words, now, ct);

    /// <summary>Daoris read a done's evidence; see <see cref="QuestEvidenceReads.EvidenceAsync"/>.</summary>
    public Task<QuestRespondOutcome> EvidenceAsync(
        string id, QuestEvidenceVerdict verdict, DateTimeOffset now, CancellationToken ct = default) =>
        evidence.EvidenceAsync(id, verdict, now, ct);

    /// <summary>Delete a quest nobody has started on; see <see cref="QuestDeletes.DeleteAsync"/>.</summary>
    public Task<QuestDeleteOutcome> DeleteAsync(
        string id, DateTimeOffset now, CancellationToken ct = default, bool askGoesToo = false) =>
        deletes.DeleteAsync(id, now, ct, askGoesToo);

    /// <summary>Which of the candidates may be deleted; see <see cref="QuestDeletes.DeletableAsync"/>.</summary>
    public Task<IReadOnlySet<string>> DeletableAsync(IEnumerable<Quest> candidates, CancellationToken ct = default) =>
        deletes.DeletableAsync(candidates, ct);

    /// <summary>Why a quest's record must stay, or null when it may go; see <see cref="QuestDeletes.KeptAsync"/>.</summary>
    internal Task<(string Stands, string Instead)?> KeptAsync(Quest quest, CancellationToken ct) => deletes.KeptAsync(quest, ct);
}
