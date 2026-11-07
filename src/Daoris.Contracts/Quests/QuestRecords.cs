using System.Text.Json;

namespace Daoris.Contracts.Quests;

// What a quest is on the wire. A field added later is optional and left out where unset, so an older reader skips it and a
// newer reader reads an older host's answer as having none. A shape named `…Wire` is the same both ways: nullable on the way
// in, where the exchange refuses what is missing naming which, and answered whole on the way out.

/// <summary>A file a quest carries, by name; its bytes travel only to a local host.</summary>
/// <param name="Path">Where this machine keeps it: answered only to a caller on a local host's own machine, and only where the bytes are
/// here, so null says named and not held.</param>
public sealed record QuestAttachmentResponse(string Name, string Sha256, long Bytes, string? Path);

/// <summary>A chain's step.</summary>
/// <param name="SetUpIn">The environment a set-up step shows the chain's work in; absent on every other step.</param>
public sealed record QuestStepWire(string? To, string? Title, string? Body, string? SetUpIn = null);

/// <summary>A chain's review choice, <c>off</c>, <c>on</c> or an environment's name, and the person's words it is set on.</summary>
public sealed record QuestReviewWire(string? Choice, string? Words = null);

/// <summary>A set-up on a set-up step, answered only.</summary>
/// <param name="Look">Where to look; absent on another machine for a local one, whose tab is on the machine that showed it.</param>
/// <param name="Session">The session that said it; absent for the person's own.</param>
/// <param name="Machine">With <paramref name="Sequence"/>, what a verdict names it by.</param>
public sealed record QuestSetUpWire(
    string Commit, string? Look, string? Shows, string? Again, string? Served, string? Run, string? Session, bool Local,
    DateTimeOffset? At, string? Machine, long? Sequence);

/// <summary>Which set-up a verdict answers, by the machine and sequence that name it everywhere.</summary>
public sealed record QuestSetUpRefWire(string? Machine, long? Sequence);

/// <summary>The person's verdict, answered only: <c>reviewed</c>, <c>not-yet</c> or <c>skipped</c>.</summary>
/// <param name="Commit">The commit of the set-up it answers; absent on a skip.</param>
public sealed record QuestReviewVerdictWire(
    string Said, QuestSetUpRefResponse? SetUp, string? Commit, string? Words, DateTimeOffset? At, string? Machine);

/// <summary>What the person requires: their words, quoted, and the check that proves them.</summary>
/// <param name="Evidence">What Daoris reads itself, each item a path or a gate; answered empty for none.</param>
public sealed record QuestRequirementWire(string? Quote, string? Check, IReadOnlyList<QuestEvidenceWire?>? Evidence = null);

/// <summary>One evidence item: exactly one of the two, or the exchange refuses it naming the requirement.</summary>
public sealed record QuestEvidenceWire(string? Path, string? Gate = null);

/// <summary>One item of what Daoris read of a done's evidence, by the requirement's number.</summary>
public sealed record QuestEvidenceReadWire(
    int? Requirement, string? Path, string? Gate, string? Result, string? Object = null, bool? Changed = null,
    string? Spelled = null);

/// <summary>A submission by the machine and sequence that made it, as a close names one.</summary>
public sealed record QuestSubmissionRefWire(string? Machine, long? Sequence);

/// <summary>How a done answers one requirement: <c>Met</c> with how, or <c>Departed</c> with the reason and the words it turns on.</summary>
public sealed record QuestAnswerWire(int? Requirement, string? Met, string? Departed = null, string? Quote = null);

/// <summary>A move that lost to another machine's, kept for a person; its machine and sequence name it everywhere.</summary>
/// <param name="LostToTheReopen">True where the take lost to the person's reopen of the work its step grew from.</param>
/// <param name="Ended">On a settled conflict only: <c>dropped</c> or <c>brought-forward</c>.</param>
/// <param name="Forward">The bring-forward that settled it, or else this machine's latest that lost again.</param>
public sealed record QuestConflictResponse(
    string Machine, [property: PageType(QuestPageTypes.Status)] string Attempted, string? Note, DateTimeOffset At, long Sequence,
    bool? LostToTheReopen = null, string? Ended = null, QuestForwardResponse? Forward = null);

/// <summary>A bring-forward's take and where it stands as a claim reads one: <c>held</c>, <c>unconfirmed</c> or <c>lost</c>.</summary>
public sealed record QuestForwardResponse(string Machine, long Sequence, DateTimeOffset At, string Standing);

/// <summary>A person's kind and workflow choice, the latest standing; one naming neither cleared them.</summary>
public sealed record AskWorkflowChoiceResponse(string? Kind, string? Workflow, DateTimeOffset At, string? Words);

/// <summary>One quest, as every quest door answers it.</summary>
/// <param name="Then">What its close publishes next.</param>
/// <param name="Parent">The quest whose close published it.</param>
/// <param name="Awaits">The question its taker waits on.</param>
/// <param name="PublishedBy">The session whose connector published it; null for a person's publish or a chain step.</param>
/// <param name="Deletable">Whether this host would delete it: the exchange's judgement, always false at a shared host.</param>
/// <param name="Lanes">The lanes of <c>To</c> it addresses, sorted; empty for the whole repository.</param>
/// <param name="Held">Whether a departure, its evidence or a review holds its done for the person; <paramref name="Hold"/> says which.</param>
/// <param name="Short">What a list calls it: its publisher's short title, else a name read from its words.</param>
/// <param name="AwaitsEvidence">Whether its done waits for Daoris to find its evidence.</param>
/// <param name="NoteParts">The note's parts beside <c>Note</c>: Daoris's lead by its code, then someone's words.</param>
/// <param name="Closure">The digest of the graph that closes it; with no closure, a submitted quest reads as Taken, as it is.</param>
/// <param name="Submission">Its taker's current submission, its result while it stays Taken.</param>
/// <param name="GrewFrom">The submission of its parent a chain's step grew from.</param>
/// <param name="SourceReopened">The one the person reopened under it, while that holds it.</param>
/// <param name="Reopened">The person's last reopen of its own submission no later submission answered.</param>
/// <param name="Settled">The conflicts settled on it, in order.</param>
/// <param name="Holder">The machine whose take holds the step, while it is taken.</param>
/// <param name="Here">This machine's id, answered only to a caller on a local host's own machine.</param>
/// <param name="WorkflowChoices">The person's kind and workflow choices on a quest published with no ask, before it started.</param>
/// <param name="Weight">The mark on the work, <c>design</c> or <c>exact</c>.</param>
/// <param name="Paths">The repository-relative globs its publisher declared it touches.</param>
/// <param name="Request">The workflow request's key, <c>&lt;run&gt;/&lt;node&gt;/&lt;n&gt;</c>.</param>
public sealed record QuestResponse(
    string Id, string From, string To, string Title, string Body,
    [property: PageType(QuestPageTypes.Status)] string Status, string? Note, DateTimeOffset Filed, DateTimeOffset Updated, string Workspace,
    IReadOnlyList<string> Links, IReadOnlyList<QuestAttachmentResponse> Attachments,
    IReadOnlyList<QuestStepResponse> Then, string? Parent, IReadOnlyList<QuestConflictResponse> Conflicts,
    string? Awaits = null,
    string? PublishedBy = null,
    bool Deletable = false,
    IReadOnlyList<string>? Lanes = null,
    IReadOnlyList<QuestRequirementResponse>? Requirements = null,
    IReadOnlyList<QuestAnswerResponse>? Answers = null, bool Held = false, DateTimeOffset? Accepted = null,
    string? Short = null,
    string? Hold = null, bool AwaitsEvidence = false, QuestEvidenceVerdictResponse? Evidence = null,
    QuestReviewChoiceResponse? Review = null, string? SetUpIn = null, IReadOnlyList<QuestSetUpWire>? SetUps = null,
    IReadOnlyList<QuestReviewVerdictWire>? Verdicts = null,
    [property: PageType(QuestPageTypes.NoteParts)] JsonElement? NoteParts = null,
    string? Closure = null, QuestSubmissionWire? Submission = null,
    QuestSubmissionRefResponse? GrewFrom = null, QuestSubmissionRefResponse? SourceReopened = null,
    QuestReopenWire? Reopened = null,
    IReadOnlyList<QuestConflictResponse>? Settled = null, string? Holder = null, string? Here = null,
    IReadOnlyList<AskWorkflowChoiceResponse>? WorkflowChoices = null,
    string? Weight = null,
    IReadOnlyList<string>? Paths = null,
    string? Request = null);

/// <summary>A quest's own reopen, answered only: the reopen by its machine and sequence, the submission it reopened, the person's words.</summary>
/// <param name="Session">The session that said the reopened submission's ready.</param>
public sealed record QuestReopenWire(string Machine, long Sequence, QuestSubmissionRefResponse Of, string? Note, DateTimeOffset At, string? Session = null);

/// <summary>A taker's submission in a closure graph, answered only; its answers are the quest's <c>Answers</c>.</summary>
/// <param name="Commit">The commit its evidence is read at, where one is.</param>
/// <param name="Session">The session its ready came from, the one a reopen of it is told to.</param>
public sealed record QuestSubmissionWire(
    string Machine, long Sequence, DateTimeOffset At, string? Note, string? Commit = null, string? Session = null);

/// <summary>
/// A file sent with a publish: its content, base64 as a byte array is in JSON, to a local host; its name, hash and size to a shared
/// one, which keeps names and never bytes. The door refuses the shape its mode does not take.
/// </summary>
public sealed record QuestAttachmentRequest(string? Name, byte[]? Content, string? Sha256, long? Bytes);

/// <summary>What a quest door did: the quest as it now stands, and the exchange's sentence.</summary>
public sealed record QuestActionResponse(QuestResponse Quest, string Message);

/// <summary>
/// One quest's moves, in the order its history replays, each by allow-list: never the publish's payload, a note's body, the answers or
/// a path. A field a move does not keep is absent, which a reader says is not recorded. An id with no history answers no moves.
/// </summary>
public sealed record QuestMovesResponse(string Quest, IReadOnlyList<QuestMoveResponse> Moves);

/// <param name="Number">The remote's number; absent while pending.</param>
/// <param name="From">The status it reached the quest at; absent before the publish.</param>
/// <param name="Hold">The hold an applied move set, or <c>none</c> where it let one go.</param>
public sealed record QuestMoveResponse(
    string Kind, DateTimeOffset At, string Machine, long Sequence, long? Number, bool Applied, string? From = null,
    string? Attempted = null, QuestMoveRefResponse? Dismisses = null, string? WaitsOn = null, string? Session = null,
    string? Evidence = null, string? SetUp = null, string? Verdict = null, string? Hold = null);

/// <summary>An operation by the machine and sequence that name it everywhere: what a dismissal names its conflict by.</summary>
public sealed record QuestMoveRefResponse(string Machine, long Sequence);
