using System.Text.Json;
using System.Text.Json.Serialization;
using Daoris.Knowledge;

namespace Daoris.Knowledge.Http;

// The wire contracts, whole and in one place — every shape a route reads or writes, plus the
// source-generated serializer context (this host publishes AOT-friendly, and reflection-based JSON
// would be the one thing stopping it). Program.cs keeps the routes; the shapes live here.

public sealed record StatusResponse(bool Semantic, string Tier, string? Note);
// Provenance is SERVED, never implied (D48 §6): the deployment's copy of a repository's knowledge is
// a claim about a commit, so the answer names the commit, when it was made, which line it came from
// and which machine fed it. Absent on a deployment that reads its own checkouts — it has no feed, and
// what it shows is its own state.
public sealed record ProvenanceResponse(
    string Commit, string ShortCommit, DateTimeOffset CommittedAt, string Branch, string? Origin);
public sealed record RepositoryResponse(
    string Name, int Total, int Local, int Canonical, string Workspace, ProvenanceResponse? Fed);
// The lines (ORIENT2e): the entry's first and last line in its file, and the line its excerpt starts on, beside
// the fields every client already reads. Null where the entry names none (a row read with its columns, a feed
// that named none), never a guess.
public sealed record HitResponse(
    string Id, string Repository, string Kind, string Title, string Path, string? Excerpt, double Score,
    string Workspace, int? FirstLine = null, int? LastLine = null, int? ExcerptLine = null);
public sealed record EntryResponse(
    string Id, string Repository, string Kind, string Provenance, string Title, string Path, string Body,
    string? Anchor, string Workspace, int? FirstLine = null, int? LastLine = null);
public sealed record ConvergenceEntryResponse(
    string Id, string Repository, string Kind, string Title, string Path);
public sealed record ConvergenceResponse(
    string Method, double Similarity, IReadOnlyList<string> Repositories,
    IReadOnlyList<ConvergenceEntryResponse> Entries, string Suggestion);
// What a quest carries (D65 §2) travels on every read: links whole, files by name. `Path` is where
// THIS machine keeps a file — answered only to a caller on this machine, like a transcript (D47 §4),
// and only when the bytes are actually here: a file of a quest published on another machine is named
// and not held, and a null path is how a reader, and the driver, learn that rather than guess it.
public sealed record QuestAttachmentResponse(string Name, string Sha256, long Bytes, string? Path);
// A chain's step (D65 §4), the same shape both ways. Nullable on the way in and judged by the
// exchange, which refuses a step without its words naming which step it was. `SetUpIn` (REVIEWENV1b) makes it a set-up step,
// naming the environment it shows the chain's work in; absent on every other step.
public sealed record QuestStepWire(string? To, string? Title, string? Body, string? SetUpIn = null);
// A chain's review choice (REVIEWENV1b, D154 point 3): `off`, `on` or an environment's name, and the person's words it was set
// on — the same shape both ways, judged by the exchange.
public sealed record QuestReviewWire(string? Choice, string? Words = null);
// A set-up on a set-up step (REVIEWENV1b, D154 point 9), answered only: the commit Daoris read, where to look (absent on another
// machine for a local one, whose tab is on the machine that showed it), what it shows, how to show it again, the folder served
// and a review run's command where there were any, the session that said it (absent for the person's own), whether it was
// local, and when, where and as which operation it was kept: `Machine` and `Sequence` are what a verdict names it by.
public sealed record QuestSetUpWire(
    string Commit, string? Look, string? Shows, string? Again, string? Served, string? Run, string? Session, bool Local,
    DateTimeOffset? At, string? Machine, long? Sequence);
// Which set-up a verdict answers, by the machine and sequence that name it everywhere (REVIEWENV1b).
public sealed record QuestSetUpRefWire(string? Machine, long? Sequence);
// The person's verdict (REVIEWENV1b, D154 point 8), answered only: `reviewed`, `not-yet` or `skipped`, the set-up it answers and
// that set-up's commit (absent on a skip), their words, and when and where it was given.
public sealed record QuestReviewVerdictWire(
    string Said, QuestSetUpRefWire? SetUp, string? Commit, string? Words, DateTimeOffset? At, string? Machine);
// A requirement (DRIFT1c, D133 §3): the person's words, quoted, and the check that proves them — the same
// shape both ways. Nullable on the way in and judged by the exchange, which refuses one missing a half.
// `Evidence` (EVID1a, D144 §2): what Daoris reads itself, each item a `path` or a `gate`; answered `[]` for none.
public sealed record QuestRequirementWire(string? Quote, string? Check, IReadOnlyList<QuestEvidenceWire?>? Evidence = null);
// One evidence item (EVID1a): exactly one of the two, judged by the exchange, which names the requirement at fault.
public sealed record QuestEvidenceWire(string? Path, string? Gate = null);
// What Daoris read of a done's evidence (EVID1a, D144 §3, §5): the commit, how it was chosen (`session-end`, `sweep`,
// `terminal`), the session whose end was read, and each item's code — the same shape both ways. `At` and `Machine` are
// answered only: when and where it was read, which the operation keeps. Nullable on the way in and judged by the exchange.
public sealed record QuestEvidenceReadWire(
    int? Requirement, string? Path, string? Gate, string? Result, string? Object = null, bool? Changed = null,
    string? Spelled = null);
public sealed record QuestEvidenceVerdictWire(
    string? Commit, string? How, string? Session = null, IReadOnlyList<QuestEvidenceReadWire?>? Items = null,
    DateTimeOffset? At = null, string? Machine = null);
// How a done answers one requirement (DRIFT1d, D133 §4): its number, and `Met` with how its check was met, or
// `Departed` with the reason and `Quote`, the person's words it turns on — the same shape both ways. Nullable on the
// way in and judged by the exchange, which refuses one that says both or neither, naming which.
public sealed record QuestAnswerWire(int? Requirement, string? Met, string? Departed = null, string? Quote = null);
// `Then` is what this quest's close will publish next; `Parent` the quest whose close published it;
// `Conflicts` the moves that lost to another machine's (D68 §5), kept for a person.
// `Machine` and `Sequence` name the conflict on every machine — what a dismissal names (SYNC6c).
public sealed record QuestConflictResponse(string Machine, string Attempted, string? Note, DateTimeOffset At, long Sequence);
public sealed record QuestResponse(
    string Id, string From, string To, string Title, string Body,
    string Status, string? Note, DateTimeOffset Filed, DateTimeOffset Updated, string Workspace,
    IReadOnlyList<string> Links, IReadOnlyList<QuestAttachmentResponse> Attachments,
    IReadOnlyList<QuestStepWire> Then, string? Parent, IReadOnlyList<QuestConflictResponse> Conflicts,
    // The question its taker waits on (D79), or null.
    string? Awaits = null,
    // The session whose connector published it (SESS1), or null: a person's publish, or a chain step.
    string? PublishedBy = null,
    // Whether this host would delete it (D95): the exchange's own judgement, so no page re-derives it.
    // Always false at a shared deployment, which has no delete door.
    bool Deletable = false,
    // The lanes of `To` it addresses (D115 §2.2), sorted; empty for a quest to the whole repository.
    IReadOnlyList<string>? Lanes = null,
    // What the person requires (DRIFT1c), each their words and its check; empty for a quest that names none.
    IReadOnlyList<QuestRequirementWire>? Requirements = null,
    // How its done answered each (DRIFT1d); `Held` whether a departure holds it for the person's yes, and
    // `Accepted` when they gave it. Empty, false and null for a quest no departure ever held.
    IReadOnlyList<QuestAnswerWire>? Answers = null, bool Held = false, DateTimeOffset? Accepted = null,
    // What a list calls it (SESSUX1j): its publisher's short title, else a name read from its own words. A host from
    // before the field answers none, and a reader names it by its title.
    string? Short = null,
    // Why it is held (EVID1a, D144 §6): `departed`, `evidence-unread`, `evidence-missing` or `unreviewed` (REVIEWENV1b), null
    // when `Held` is false. `AwaitsEvidence` whether its done waits for Daoris to find its evidence, a departure beside it or not;
    // `Evidence` what was last read of it, null while nothing was. A host from before answers none of the three.
    string? Hold = null, bool AwaitsEvidence = false, QuestEvidenceVerdictWire? Evidence = null,
    // The review on the record (REVIEWENV1b): its chain's choice, the environment a set-up step shows its work in, the set-ups
    // said on it and the person's verdicts, oldest first. Each absent where there is none, so a quest no review touches is
    // answered as it was, and a host from before answers none.
    QuestReviewWire? Review = null, string? SetUpIn = null, IReadOnlyList<QuestSetUpWire>? SetUps = null,
    IReadOnlyList<QuestReviewVerdictWire>? Verdicts = null);
// An attachment arrives with its CONTENT at a local host — base64 on the wire, which is what a byte
// array is in JSON — and by NAME at a shared one, which keeps names and never bytes (D65 §2). The door
// decides which shape its mode takes and refuses the other; the exchange never sees the wrong one.
public sealed record QuestAttachmentRequest(string? Name, byte[]? Content, string? Sha256, long? Bytes);
public sealed record PublishQuestRequest(
    string From, string To, string Title, string Body,
    IReadOnlyList<string>? Links = null, IReadOnlyList<QuestAttachmentRequest>? Attachments = null,
    IReadOnlyList<QuestStepWire?>? Then = null, IReadOnlyList<QuestRequirementWire?>? Requirements = null,
    string? Short = null, QuestReviewWire? Review = null);
// A set-up posted on a set-up step (REVIEWENV1b, design §2.6, §3.3): the commit Daoris read and the environment's kind (`local`
// or `deployed`), with `Session`, whose said set-ups the driver posts, or the person's own `Look`, `Shows` and `Again`.
public sealed record QuestSetUpRequest(
    string? Commit, string? Kind, string? Session = null, string? Look = null, string? Shows = null, string? Again = null);
// The person's verdict (REVIEWENV1b, design §3.3–§3.6): `reviewed`, `not-yet` or `skipped`, their words (a `not-yet`'s always),
// and the set-up it answers, as their view showed it: a `reviewed`'s and a `not-yet`'s always, whole (REVIEWENV1b3); a skip's never.
public sealed record QuestReviewRequest(string? Verdict, string? Words = null, QuestSetUpRefWire? SetUp = null);
// The person's *Set it up in `<environment>`* (REVIEWENV1b, design §2.1, §3.6): the environment the set-up step shows the work in.
public sealed record SetUpStepRequest(string? Environment);
// `On` is the question a `wait` waits on (D79); `Answers` how a done answers each requirement (DRIFT1d);
// `WhileOpen` a decline that applies only while the quest is open (PAUSE1c), an abandon's.
public sealed record RespondQuestRequest(
    string? Action, string? Reason, string? On = null, IReadOnlyList<QuestAnswerWire?>? Answers = null,
    bool? WhileOpen = null);
// The person's done (QUESTCLOSE1): their words, kept on the quest after the sentence that says the done was theirs.
public sealed record PersonDoneRequest(string? Note);
// A person dismissing a conflict (SYNC6c): the one named, or — naming none — every one the quest carries.
public sealed record DismissConflictRequest(string? Machine, long? Sequence);
// An ask (D65 §1a): a sentence at a workspace. Files arrive whole — the ask door is a local host's —
// and `To` is the asker naming the receiver, which publishes at once.
// `Review` and `ReviewWords` (REVIEWENV1b, design §1.5): the person's review choice from the composer, and their words with it.
public sealed record AskRequestBody(
    string? Workspace, string? Sentence, IReadOnlyList<string>? Links,
    IReadOnlyList<QuestAttachmentRequest>? Attachments, string? To, string? Review = null, string? ReviewWords = null);
// A publish is a person's `To` alone; an intake (D65 §1b) adds its own words, carry and chain, and
// names its `Session` — which moves the ask's tier to `intake` only when it is the ask's own. Its
// `Requirements` quote the person (DRIFT1c), judged against the ask's words. `Review` is the chain's choice, an intake's only on
// the person's quoted words; `ReviewProposal` an intake's proposal with its reason, kept on the ask (REVIEWENV1b).
public sealed record AskPublishRequest(
    string? To, string? Title = null, string? Body = null, IReadOnlyList<string>? Links = null,
    IReadOnlyList<QuestAttachmentRequest>? Attachments = null, IReadOnlyList<QuestStepWire?>? Then = null,
    string? Session = null, IReadOnlyList<QuestRequirementWire?>? Requirements = null, string? Short = null,
    QuestReviewWire? Review = null, ReviewProposalWire? ReviewProposal = null);
// An intake's review proposal (REVIEWENV1b, design §1.5): a choice and its reason, at most 300 characters.
public sealed record ReviewProposalWire(string? Choice, string? Reason);
// The person's review choice on an ask's page (REVIEWENV1b, design §1.5), and their words with it.
public sealed record AskReviewRequest(string? Choice, string? Words = null);
public sealed record AskCloseRequest(string? Reason);
public sealed record DeclarationMatchResponse(string Repository, int Score, IReadOnlyList<string> Matched);
// `Tier` is said on every record (model-decoupling): which tier answered, never implied. `Intake` is
// the session that served it (D65 §1b), once one opened — the record a reader follows to its question.
// `State` is the desk's reading of it, `Done` included (USE1c); `Deletable` whether the desk would delete
// it with every quest asked by it (D95).
// `Words` (DRIFT1a, D133 §1) is every word the person gave on it, oldest first: its sentence, then each
// answer and each message added to a session, with when, the session and its quest. `WordsKeptFrom` is
// said only of an ask made before the words were kept: from when they are, since nothing is back-filled.
// `GoAheads` (KNOWUSE1a, D135 §2) is every go-ahead its sessions asked the person for, oldest first, one per act.
// `ReviewChoices` (REVIEWENV1b, design §1.5) is the person's review choices on it, oldest first, the latest standing;
// `ReviewProposals` its intake's proposals, each with its reason and the quest it came with. Each absent where there is none.
public sealed record AskResponse(
    string Id, string Workspace, string Sentence, string State, string Tier, DateTimeOffset Asked,
    DateTimeOffset Updated, string? Asker, string? Note, IReadOnlyList<string> Links,
    IReadOnlyList<QuestAttachmentResponse> Attachments, IReadOnlyList<DeclarationMatchResponse> Proposal,
    IReadOnlyList<string> Quests, string? Intake = null, bool Deletable = false,
    IReadOnlyList<AskWordResponse>? Words = null, DateTimeOffset? WordsKeptFrom = null,
    IReadOnlyList<GoAheadResponse>? GoAheads = null,
    IReadOnlyList<AskReviewChoiceResponse>? ReviewChoices = null, IReadOnlyList<AskReviewProposalResponse>? ReviewProposals = null);
public sealed record AskReviewChoiceResponse(string Choice, DateTimeOffset At, string? Words);
public sealed record AskReviewProposalResponse(string Choice, string Reason, DateTimeOffset At, string? Session, string? Quest);
// One word (DRIFT1a): `kind` is `asked`, `answered`, `added` or `reopened` (MSG1a: said after its session ended, which
// went on with it), or a review's `reviewed`, `not-yet` or `skipped` (REVIEWENV1b); the ask's own sentence names no session.
public sealed record AskWordResponse(string Kind, string Text, DateTimeOffset At, string? Session, string? Quest);
// One go-ahead (KNOWUSE1a): the act by its `kind` (write, release, push, sign-in, run), where it lands (`on`) and what it
// touches (`act`); `state` is `asked`, `approved` or `refused`; `asked` is each session's request, oldest first; `answer`
// the person's, absent while it waits; `near` the go-ahead whose words it shared where they could not tell.
public sealed record GoAheadResponse(
    int Number, string Kind, string On, string Act, string State, IReadOnlyList<GoAheadRequestResponse> Asked,
    GoAheadAnswerResponse? Answer, int? Near);
public sealed record GoAheadRequestResponse(string Session, string? Quest, DateTimeOffset At, string Why);
public sealed record GoAheadAnswerResponse(bool Approved, string? Words, DateTimeOffset At);
// The person's answer to a go-ahead (KNOWUSE1a): `answer` is `approved` or `refused`; `words` theirs, where they give any.
// `goesOn` false leaves a parked session that waited on it to its caller, which answers that park itself (GOAHEAD2, the
// session page's door); absent or true, the answer that leaves none of a park's go-aheads waiting is its answer.
public sealed record GoAheadAnswerRequest(string? Answer, string? Words, bool? GoesOn = null);
public sealed record AskActionResponse(AskResponse Ask, string Message, QuestResponse? Quest);
// DRIFT1a: what the person added to a running session, as its driver reports it.
public sealed record AddedRequest(string? Text);
// `Kept` false is no error: the session is on no ask, and the message says so. `Ask` is the ask it was kept on.
public sealed record AddedResponse(bool Kept, string Message, AskResponse? Ask);
public sealed record QuestActionResponse(QuestResponse Quest, string Message);
// What a delete did (D95): the record it removed, and the service's sentence, verbatim — which says
// whether the delete travels, and whether the remote has taken it yet.
public sealed record DeletedResponse(string Id, string Message);
// `Embedded` is what the semantic half made of the entries (D123): how many, the vectors they became, how
// many were longer than the window and split. Absent when it did not run, and `semanticError` says why.
public sealed record RefreshResponse(
    int Entries, int Repositories, int Withheld, string? SemanticError, IReadOnlyList<string> Absent,
    EmbeddedResponse? Embedded = null);
public sealed record EmbeddedResponse(int Entries, int Pieces, int Split, int Window);
// `Uses` is what the repository says it depends on (D91); absent is nothing declared.
public sealed record DomainRequest(
    string? Summary, IReadOnlyList<string>? Owns, IReadOnlyList<string>? Accepts, IReadOnlyList<string>? Uses = null);
// `Workspace` is null on the way IN when the client said nothing — which is what preserves the row
// (D48 §2). It is never null on the way out: a reader is told which circle it is looking at.
// `DefaultBranch` is the same shape for the same reason (D48 §6): the checkout that registers knows
// its canonical line and the deployment cannot ask git, but an ordinary registration says nothing
// about it — so null preserves what was declared rather than erasing it. The commit and its base are a
// checkout's, read by the same rules as a feed's (SYNC5b): a shared deployment orders declarations by
// them, and a local host, where the registration is this machine's own, reads none of them.
// `Adopted` is false only from a door that looked and found no manifest — the desktop's *add* of a
// folder that has not adopted (D70). Silence is the connector's `connect`, which runs in an adopter
// and is adoption, so it stays true; a shared deployment takes no false at all (it holds no roots, and
// an unadopted repository is never joined).
// `Lanes` (D115 §2.2) is the words of the lanes the repository declares, never their paths. Null on the
// way in is unstated and preserves the row's, as the workspace does: only `connect` reads the file, and
// it always says, `[]` for none.
public sealed record RegisterRequest(
    string Repository, IReadOnlyList<string>? Packs, DomainRequest? Domain, string? Root,
    bool? Join, bool? ShareKnowledge, string? Workspace, string? DefaultBranch,
    string? Commit = null, DateTimeOffset? CommittedAt = null, string? Branch = null, string? Base = null,
    bool? Adopted = null, IReadOnlyList<LaneWire>? Lanes = null);
// One lane's words (D115 §2.2), the same shape both ways; judged by `Declared.Lanes` on the way in.
public sealed record LaneWire(string? Id, string? Title, string? Summary, bool? Steward);
public sealed record RegisteredResponse(string Repository, DateTimeOffset At, string Workspace);
public sealed record RetiredResponse(string Repository, bool Retired, string Message);
// The repositories this machine's checkouts took out of a circle, not yet told to its deployment (SYNC5b).
public sealed record RetiredPendingResponse(string Workspace, IReadOnlyList<string> Repositories);
public sealed record WireRequest(string? Workspace);
// `Workspace` names the circle every imported row lands in (D77); silence moves nobody.
public sealed record ImportRequest(string? Folder, string? Workspace = null);
public sealed record ImportedResponse(string Folder, int Imported, IReadOnlyList<string> Repositories, string Message)
{
    /// <summary>The circle they were wired to, when the import named one.</summary>
    public string? Workspace { get; init; }
}
// `Addressable` is the exchange's own judgement (D70), answered so no page re-derives it from
// `Adopted`: a repository registered here with a root can be asked without having adopted, and a
// browser that is never told the root must still offer it as a receiver.
public sealed record RegistrationResponse(
    string Repository, bool Adopted, bool Registered, string? Summary,
    IReadOnlyList<string> Owns, IReadOnlyList<string> Accepts, IReadOnlyList<string> Packs, int Entries,
    string? Root, bool Joined, bool SharesKnowledge, string Workspace, string? DefaultBranch, bool Addressable,
    IReadOnlyList<string> Uses, IReadOnlyList<LaneWire>? Lanes = null);
// `Quest` is null for a chat (D49 §3) and `Kind` says which way in it was — the same record either
// way, which is the point: a conversation is a session, not a second kind of thing.
// `HarnessVersion` and `Profile` say which tool and which account produced this (D49 §4), and `Tree`
// which working tree it held (D51). The profile and the tree are machine-local and guarded like the
// transcript beside them: they answer to the machine that ran the session and travel no further — and
// the tree is a filesystem path, which is the sharpest reason of the three.
public sealed record SessionResponse(
    string Id, string? Quest, string Repository, string Adapter, string State,
    string? Note, string? Evidence, string? Transcript, DateTimeOffset Created, DateTimeOffset Updated,
    string Workspace, string Kind, string? HarnessVersion, string? Profile, string? Tree,
    // SURF6: the commit the tree stood at when the spawn began, so the review's range is a fact. It
    // is a repository fact rather than a machine one, but only the machine holding the checkout can
    // do anything with it — so it rides the same loopback gate as the tree beside it.
    string? BaseCommit = null,
    // D65 §1b: the ask an intake answers — null for every other session.
    string? Ask = null,
    // STANDDOWN2: whether it took its own quest, through its own connector; and the person's answer to
    // one that parked to ask them — since MSG1a the words waiting in `Said`, joined by a blank line, for a client
    // from before.
    bool Took = false,
    string? Answer = null,
    // D104: a stop that was not the person's — the sweep's, or a shutdown's — which the driver carries on.
    bool Interrupted = false,
    // TOOL4c (D125 §5.2): a failure an account's limit made. It names no account, so every caller is told.
    bool Limit = false,
    // SESSUX1f (D126 §5.4): whether the ledger would delete its record, D95's way, so the driver offers *Delete…* only
    // where it would be taken. False at a shared deployment, which has no delete door.
    bool Deletable = false,
    // MSG1a (D137 §2.4): the person's words waiting for it to go on with them, in order. Their own words, which may name
    // anything on this machine: answered to it only, like `Answer` and the transcript.
    IReadOnlyList<SaidWordResponse>? Said = null,
    // LANG1a (D142 point 2): the note's lines, each a code with its values or someone's words, beside `note`, which stays as it
    // always read. Null for a record from before parts. To another machine, every string in them cleaned as the note is.
    JsonElement? NoteParts = null);
// MSG1a: one word waiting on a record — its id, which the record's events say again where the session took it; the
// words; when; its files' names, never where they are; and whether it was said after the record ended.
public sealed record SaidWordResponse(string Id, string Text, DateTimeOffset At, IReadOnlyList<string> Files, bool Reopens);
// MSG1a (D137 §5.3): what the person says to a parked or ended session. `Files` are the names of what they gave with it;
// a path is cut to its name before it is kept.
public sealed record SayRequest(string? Text, IReadOnlyList<string>? Files = null);
// The session as it stands, the ledger's sentence, and the word as kept (to this machine only).
public sealed record SaidResponse(SessionResponse Session, string Message, SaidWordResponse? Said);
// A say refused: the sentence as `error`, beside its word (`refusal`: `no-words`, `not-found`, `not-ours`, `intake`,
// `stood-down`, `running`), which a reader acts on instead of the sentence, and the quest, ask or machine it names.
public sealed record SessionSayRefusalResponse(
    string Error, string Refusal, string? Quest = null, string? Ask = null, string? Origin = null);
// MSG1a (D137 §2.4): the words a session took, by their ids; `By` the session that took them where it is not this one
// (a fallback's new session).
public sealed record TakenRequest(IReadOnlyList<string?>? Said, string? By = null);
// SESSUX1f (D126 §5.4): a session delete refused, or its judgement alone. `error` is the ledger's sentence, the field every
// reader already knows; `refusal` its word, which a reader acts on instead of the sentence; and the facts the word names.
public sealed record SessionDeletionResponse(
    bool Deletable, string? Error = null, string? Refusal = null,
    string? Quest = null, string? Ask = null, string? Origin = null, string? Workspace = null);
// HIST1b (D153; the history-clearing design §6.3): why a unit stays, or a piece of it is kept, as SESSUX1f's refusals travel:
// `error` the desk's sentence, `refusal` its word (`unknown`, `open`, `asked`, `live`, `needs-you`, `awaited`, `unpushed`,
// `not-ours`), which the driver reads instead of the sentence, and the quest, ask, session, machine or workspace it names.
// HIST1l: `waits`, beside `needs-you` only, names what waits on the person (`parked`, `held`, `conflict`, `ask`, `proposal`), so
// the driver says the sentence meant without reading the records again; absent for every other word, and from a host before it.
// HIST1m, the same way: `stands`, beside `open` only, how the work in progress stands (`open`, `taken`), and `by`, beside
// `awaited` only, the open work that names it (`question`, `asker`, `step`). The words themselves are unchanged.
public sealed record HistoryRefusalResponse(
    string Refusal, string Error, string? Quest = null, string? Ask = null, string? Session = null, string? Origin = null,
    string? Workspace = null, string? Waits = null, string? Stands = null, string? By = null);
// One unit (`kind`: `quest`, `ask` or `failed`): what a clear takes, by id, and whether it may go now. `forgotten` is which of
// its quests a remote numbered, forgotten here rather than simply removed; `teammates` this machine's copies of a teammate's
// records; `kept` the pieces listed and kept while the unit goes (a teammate's failed session).
public sealed record HistoryUnitResponse(
    string Kind, string Id, string? Workspace, bool Clearable, IReadOnlyList<string> Quests, IReadOnlyList<string> Forgotten,
    IReadOnlyList<string> Asks, IReadOnlyList<string> Sessions, IReadOnlyList<string> Teammates,
    HistoryRefusalResponse? Refusal, IReadOnlyList<HistoryRefusalResponse> Kept);
public sealed record HistoryPlanResponse(IReadOnlyList<HistoryUnitResponse> Units);
// The second press names its units as the first listed them: each `kind` and `id`.
public sealed record HistoryUnitRequest(string? Kind, string? Id);
public sealed record HistoryClearRequest(IReadOnlyList<HistoryUnitRequest?>? Units);
// What the press did to one unit: the unit as judged where it was cleared, whether it went, and the desk's sentence. HIST1j:
// `failed`, only where a removal failed, names the quests and asks whose kept files the disk would not let go of, so the driver
// counts them as failed and frees none of their bytes; absent otherwise, so a driver from before reads the answer as it did.
public sealed record HistoryClearedResponse(HistoryUnitResponse Unit, bool Cleared, string Message, HistoryFailedResponse? Failed = null);
public sealed record HistoryFailedResponse(IReadOnlyList<string> Quests, IReadOnlyList<string> Asks);
public sealed record HistoryClearResponse(IReadOnlyList<HistoryClearedResponse> Units);
// STANDDOWN2: the person's words to a session that parked to ask them. Blank is "carry on".
public sealed record AnswerSessionRequest(string? Answer);
// The tree comes IN from the driver, which is the half that knows: the service has no checkout to
// look at, exactly as it has no binaries to probe (D46 §7). Unstated resolves to the registered root.
public sealed record OpenSessionRequest(
    string Quest, string Adapter, string? HarnessVersion, string? Profile, string? Tree,
    string? BaseCommit = null);
public sealed record OpenChatRequest(
    string Repository, string? Adapter, string? HarnessVersion, string? Profile, string? Tree,
    string? BaseCommit = null);
// An intake (D65 §1b): the ask it answers and the room it runs in — the driver's, which is the half
// that is about to run a process there. The circle comes from the ask, never from the caller.
public sealed record OpenIntakeRequest(
    string? Ask, string? Adapter, string? Room, string? HarnessVersion = null, string? Profile = null);
// Ask Daoris (HELP1a, D89): the room its conversation runs in, the driver's to name.
public sealed record OpenHelpRequest(
    string? Adapter, string? Room, string? HarnessVersion = null, string? Profile = null);
// `Interrupted` (D104): a move to `stopped` that was not the person's. `Limit` (TOOL4c): a move to `failed`
// that an account's limit made. Silence is the old reading of both. `NoteParts` (LANG1a): the note's lines by code, beside
// it; a note sent without them clears the record's, and an older driver sends none.
public sealed record AdvanceSessionRequest(
    string? State, string? Note, string? Evidence, string? Transcript, bool? Interrupted = null, bool? Limit = null,
    JsonElement? NoteParts = null);
public sealed record SessionActionResponse(SessionResponse Session, string Message);
// `FirstLine` and `LastLine` (ORIENT2e): the lines of its file an entry is, as `/api/entries` answers them, taken
// only where they can be its body's; a feed that names none is read as before.
public sealed record FeedEntryRecord(
    string? Kind, string? Title, string? Body, string? RelativePath, string? Anchor, int? FirstLine = null, int? LastLine = null);
// The three provenance fields are the feed's claim about WHICH point in the history it speaks for
// (D48 §6). Nullable on the wire and judged at the door: a feed that names no commit cannot be
// compared with what is held, and a wholesale replacement that cannot be compared is the flapping
// this exists to end. `Base` is the held commit the feeding machine asked git about (SYNC5a); absent,
// commit time decides.
public sealed record FeedEntriesRequest(
    string Repository, IReadOnlyList<FeedEntryRecord>? Entries,
    string? Commit, DateTimeOffset? CommittedAt, string? Branch, string? Base = null);
// A repository's code map, fed (MAP3b): the file's text as one string, judged whole again at the door,
// or null when the checkout keeps none at that commit. The provenance is the entries feed's, field for
// field — one judgement, two feeds.
public sealed record FeedCodeMapRequest(
    string Repository, string? File, string? Map,
    string? Commit, DateTimeOffset? CommittedAt, string? Branch, string? Base = null);
// Which commit a repository's knowledge, code map and declaration stand on at this deployment — what a
// feeding machine asks git about before it feeds (SYNC5a, SYNC5b). Null where nothing has fed.
public sealed record FeedHeldResponse(string Repository, string? Knowledge, string? CodeMap, string? Registration);
// A machine's sync pass (D69, SYNC4), as its driver reads it: what the quest half pushed, the moves of
// this machine that became conflicts, what the remote would not keep, what is still behind; how many
// session records went up and came down; and the wall, if there was one. `Machine` is this store's id.
// Neither operations nor records cross this door — the remote's doors speak Core's `QuestWire` and
// `SessionWire`, and this one only says what a pass did.
public sealed record QuestConflictNote(string Quest, string Attempted);
public sealed record QuestPushRefusalWire(string Quest, string Reason);
// `CodeMapsFetched` is how many of the team's code maps came down, new or moved (MAP3e).
public sealed record SyncResponse(
    string Workspace, string Machine, bool Wired, int Pushed, IReadOnlyList<QuestConflictNote> Conflicts,
    IReadOnlyList<QuestPushRefusalWire> Refused, IReadOnlyList<string> Behind,
    int SessionsPushed, int SessionsFetched, string? Problem, int CodeMapsFetched);
// Where a circle stands on this machine (SYNC6a): what it has not pushed, the quests the last pass
// left behind, the quests carrying a conflict, when a pass last reached the remote and last tried, and
// the wall it hit. A circle with no remote here answers `wired: false` and nothing else, because
// "unpushed" means nothing where there is nowhere to push.
public sealed record SyncStandingResponse(
    string Workspace, bool Wired, int Ahead, IReadOnlyList<string> Behind, IReadOnlyList<string> Conflicts,
    DateTimeOffset? Synced, DateTimeOffset? Tried, string? Problem);
// Where this machine's claim on one quest stands: none, held, unconfirmed or lost (D68 §4).
public sealed record QuestClaimResponse(string Quest, string Claim);
public sealed record FeedResponse(int Accepted, string Message);
// A repository's code map is answered in Core's shape (CodeMapWire, MAP3e), not a record here: the
// host that brings a teammate's map down reads the same answer the door writes.
// `Code` (ORIGIN1): a word a client reads instead of the sentence, where a refusal has one; absent elsewhere, so every
// answer from before reads as it did.
public sealed record ErrorResponse(string Error, string? Code = null);
/// <summary>
/// A refusal the CLIENT should report rather than fix (D48 §6) — a stale or branch feed is the system
/// working, and the machine that is behind is simply behind. The flag is what lets a sync loop tell
/// "this is news" from "this is broken" without matching on the sentence: `error` stays the field
/// every reader already knows, so the message still travels verbatim.
/// </summary>
public sealed record FeedRefusalResponse(string Error, bool Information);

[JsonSerializable(typeof(StatusResponse))]
[JsonSerializable(typeof(IEnumerable<RepositoryResponse>))]
[JsonSerializable(typeof(IEnumerable<HitResponse>))]
[JsonSerializable(typeof(EntryResponse))]
[JsonSerializable(typeof(IEnumerable<EntryResponse>))]
[JsonSerializable(typeof(IEnumerable<ConvergenceResponse>))]
[JsonSerializable(typeof(IEnumerable<QuestResponse>))]
[JsonSerializable(typeof(PublishQuestRequest))]
[JsonSerializable(typeof(RespondQuestRequest))]
[JsonSerializable(typeof(PersonDoneRequest))]
[JsonSerializable(typeof(DismissConflictRequest))]
[JsonSerializable(typeof(QuestEvidenceVerdictWire))]
[JsonSerializable(typeof(QuestSetUpRequest))]
[JsonSerializable(typeof(QuestReviewRequest))]
[JsonSerializable(typeof(SetUpStepRequest))]
[JsonSerializable(typeof(AskReviewRequest))]
[JsonSerializable(typeof(AskRequestBody))]
[JsonSerializable(typeof(AskPublishRequest))]
[JsonSerializable(typeof(AskCloseRequest))]
[JsonSerializable(typeof(AskActionResponse))]
[JsonSerializable(typeof(AskResponse))]
[JsonSerializable(typeof(IEnumerable<AskResponse>))]
[JsonSerializable(typeof(GoAheadAnswerRequest))]
[JsonSerializable(typeof(AddedRequest))]
[JsonSerializable(typeof(AddedResponse))]
[JsonSerializable(typeof(QuestActionResponse))]
[JsonSerializable(typeof(DeletedResponse))]
[JsonSerializable(typeof(RefreshResponse))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(RegisteredResponse))]
[JsonSerializable(typeof(RetiredResponse))]
[JsonSerializable(typeof(RetiredPendingResponse))]
[JsonSerializable(typeof(WireRequest))]
[JsonSerializable(typeof(ImportRequest))]
[JsonSerializable(typeof(ImportedResponse))]
[JsonSerializable(typeof(IEnumerable<RegistrationResponse>))]
[JsonSerializable(typeof(IEnumerable<SessionResponse>))]
[JsonSerializable(typeof(OpenSessionRequest))]
[JsonSerializable(typeof(OpenChatRequest))]
[JsonSerializable(typeof(OpenIntakeRequest))]
[JsonSerializable(typeof(OpenHelpRequest))]
[JsonSerializable(typeof(AdvanceSessionRequest))]
[JsonSerializable(typeof(SessionActionResponse))]
[JsonSerializable(typeof(SessionDeletionResponse))]
[JsonSerializable(typeof(SayRequest))]
[JsonSerializable(typeof(SaidResponse))]
[JsonSerializable(typeof(SessionSayRefusalResponse))]
[JsonSerializable(typeof(TakenRequest))]
[JsonSerializable(typeof(HistoryPlanResponse))]
[JsonSerializable(typeof(HistoryClearRequest))]
[JsonSerializable(typeof(HistoryClearResponse))]

[JsonSerializable(typeof(FeedEntriesRequest))]
[JsonSerializable(typeof(FeedCodeMapRequest))]
[JsonSerializable(typeof(FeedHeldResponse))]
[JsonSerializable(typeof(SyncResponse))]
[JsonSerializable(typeof(SyncStandingResponse))]
[JsonSerializable(typeof(QuestClaimResponse))]
[JsonSerializable(typeof(FeedResponse))]
[JsonSerializable(typeof(FeedRefusalResponse))]
[JsonSerializable(typeof(ErrorResponse))]
internal sealed partial class ApiJson : JsonSerializerContext;
