using System.Text.Json;
using System.Text.Json.Serialization;
using Daoris.Contracts.Sessions;
using Daoris.Knowledge;

namespace Daoris.Knowledge.Http;

// The wire shapes not yet declared in Daoris.Contracts beside their routes, and their source-generated serializer
// context. A domain's shapes move there with its routes.

// `Embedded` is what the semantic half made of the entries (D123): how many, the vectors they became, how
// many were longer than the window and split. Absent when it did not run, and `semanticError` says why.
public sealed record RefreshResponse(
    int Entries, int Repositories, int Withheld, string? SemanticError, IReadOnlyList<string> Absent,
    EmbeddedResponse? Embedded = null, IReadOnlyList<ReadingResponse>? Read = null);
// KNOWCURRENT1: when a repository's checkout was read and the commit it was at (null for a folder with no commit).
public sealed record ReadingResponse(string Repository, DateTimeOffset ReadAt, string? Commit);
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
// "unpushed" means nothing where there is nowhere to push. The machines of the workspace its remote last said had not declared they
// know a bound quest (FABRIC-CLOSE1i2), as the last pass heard them: empty where every one did, absent where no pass heard the handshake.
public sealed record SyncStandingResponse(
    string Workspace, bool Wired, int Ahead, IReadOnlyList<string> Behind, IReadOnlyList<string> Conflicts,
    DateTimeOffset? Synced, DateTimeOffset? Tried, string? Problem, IReadOnlyList<string>? Undeclared = null);
// A person's retirement of a machine at a circle's remote (FABRIC-CLOSE1i2): whether the remote retired one, false where it had heard
// nothing from it, and the machines it still counts undeclared after it.
public sealed record MachineRetiredResponse(
    string Workspace, string Machine, bool Retired, IReadOnlyList<string> Undeclared, string Message);
// Where this machine's claim on one quest stands: none, held, unconfirmed or lost (D68 §4). Why a lost one lost, where its take lost to
// the person's reopen of the work the step grew from (FABRIC-CLOSE1c18): `source-reopened`; null otherwise, as an older host answers.
// `Forward` (SYNCMERGE1b0): the take this machine holds the quest by, where the person's bring-forward made it, the one take a stood-down
// session is carried on over; absent otherwise, and from a host before it. `Taken` (CARRY3): when the take the quest stands taken by
// was made, on its machine's clock; null while it is not taken, and absent from a host before it.
public sealed record QuestClaimResponse(
    string Quest, string Claim, string? Why = null, QuestForwardResponse? Forward = null, DateTimeOffset? Taken = null);
// HANDBACK1 (D219): one quest's handback as `GET /api/handbacks/{id}` answers it, by the quest's id or the id of the session that
// served it, in any state. `Found`: whether the id named a quest or a session here, so a client tells this door's answer from an older
// host's 404. `Session`: the session the id named, absent for a quest's id. `Quest`: the quest, with its own note as it keeps it.
// `Handback`: where a session's close is recorded on it, whole, with the session that wrote it; absent where none is. `Said`: the read's
// words, the same the connector's `handback_read` answers, which the terminal prints.
public sealed record HandbackResponse(
    string Id, bool Found, string? Session, HandbackQuestResponse? Quest, HandbackRecordResponse? Handback, string Said)
{
    public static HandbackResponse Of(HandbackReading read) => new(
        read.Id, read.Found, read.Session?.Id,
        read.Quest is { } quest
            ? new HandbackQuestResponse(quest.Id, quest.Title, quest.Status.ToString(), quest.From, quest.To, quest.Workspace, quest.Note)
            : null,
        read.Handback is { } said
            ? new HandbackRecordResponse(
                said.Spelled, said.Note,
                said.Answers.Count == 0 ? null : [.. said.Answers.Select(answer => new QuestAnswerWire(answer.Requirement, answer.Met, answer.Departed, answer.Quote))],
                said.At, said.Machine, said.Session)
            : null,
        read.Said);
}
// The quest a handback hands back: its name, where it stands, between whom, and its note as the quest keeps it.
public sealed record HandbackQuestResponse(string Id, string Title, string Status, string From, string To, string Workspace, string? Note);
// A handback whole: its act (`done`, `ready` or `declined`), its note, how it answered each requirement (absent for none), when on
// the machine that made it, and the session that wrote it.
public sealed record HandbackRecordResponse(
    string Act, string? Note, IReadOnlyList<QuestAnswerWire>? Answers, DateTimeOffset At, string Machine, string Session);
public sealed record FeedResponse(int Accepted, string Message);
// A repository's code map is answered in Core's shape (CodeMapWire, MAP3e), not a record here: the
// host that brings a teammate's map down reads the same answer the door writes.
/// <summary>
/// A refusal the CLIENT should report rather than fix (D48 §6) — a stale or branch feed is the system
/// working, and the machine that is behind is simply behind. The flag is what lets a sync loop tell
/// "this is news" from "this is broken" without matching on the sentence: `error` stays the field
/// every reader already knows, so the message still travels verbatim.
/// </summary>
public sealed record FeedRefusalResponse(string Error, bool Information);

// PERSONDOOR1b (D156 point 4, design §4.2; ONEWAY1n-i): an ask for the person's confirmation. `Path` is the refused request's
// path and query exactly as sent, `Body` its body exactly as sent (absent for none), and `Session` the session asking, as its
// record names it, or absent for a terminal's ask (PERSONDOOR1j). The host makes that request itself at the Confirm.
public sealed record ConfirmationAskRequest(string? Method, string? Path, string? Body, string? Session);
// An asker's withdrawal of its own ask (ONEWAY1n-i): the token its ask was answered with.
public sealed record ConfirmationWithdrawRequest(string? Token);
// One value the request carries: an address's (a route's parameter, then the query's, by name) or the body's, by its JSON
// pointer (`/setUp/machine`, `/units/0/id`; `~0` and `~1` escape a name's `~` and `/`). `Type` is its JSON type (`string`,
// `number`, `boolean`, `null`, `object`, `array`), so a string "3" and a number 3 read apart; `Value` is a string as it is
// and anything else as its JSON. Only names the door binds are ever shown: an ask carrying another is refused.
public sealed record ConfirmationFieldResponse(string Name, string Type, string Value);
// A confirmation as the window shows it and its asker polls it (ONEWAY1n-i): `State` is `waiting`, `making`, `made`, `refused`,
// `withdrawn`, `stale` or `failed`; `Route` the door's pattern and `Act` its own name in the host's table (the form's, where the
// body makes one); `Values` the address's values and `Fields` every value of the body, so nothing the request made would carry is
// out of sight; `Session` the session the ask named, absent for a terminal's. No clock ends one. Once it ends, `Ended`; once made,
// the door's `Status` and its `Answer` as text (absent past 64 KB), and where the door refused or failed it, its `Reason`, the
// door's own sentence. Never the asker's token.
public sealed record ConfirmationResponse(
    string Id, string State, string Method, string Path, string Route, string Act,
    IReadOnlyList<ConfirmationFieldResponse> Values, IReadOnlyList<ConfirmationFieldResponse> Fields, string? Body,
    DateTimeOffset Asked, string? Session, DateTimeOffset? Ended, int? Status, string? Answer, string? Reason);
// A confirm's, a refusal's or a withdrawal's answer: the confirmation as it now stands, and a sentence the caller says.
public sealed record ConfirmationActionResponse(ConfirmationResponse Confirmation, string Message);
// An ask's answer (ONEWAY1n-i): the confirmation, the sentence its asker says while it waits, and the token that withdraws it,
// answered here alone.
public sealed record ConfirmationAskedResponse(ConfirmationResponse Confirmation, string Message, string Token);
// NOTIFY2j: what waits for the person, as a held wait answers it. `Since` is the cursor to ask from next (`<epoch>.<n>`), and
// `Waiting` every confirmation waiting now, whole, oldest first, as `GET /api/confirmations` lists them.
public sealed record ConfirmationsWaitingResponse(string Since, IReadOnlyList<ConfirmationResponse> Waiting);
// FABRIC-EVENT3b (the fabric events design §A4.2): the change stream's answer. `Since` is the cursor to ask from next
// (`<epoch>.<n>`), and `Moved` the ledger families that moved after the one asked from, by name (`sessions`, `quests`): empty
// at the wait's end with nothing moved. Families, never records, and no path (D169 §3). `Journal` (FOUNDATION1 row k part 2) is the event
// journal's place, answered to a reader that asked for it with `journal=1` and written to none else (null is left out), so an answer to a client
// from before it is what it always was; the family `journal` is likewise named only to a reader that asked.
public sealed record ChangesResponse(string Since, IReadOnlyList<string> Moved, ChangesJournalResponse? Journal = null);
// Where the event journal stands: the store's identity its positions are of, the lowest position kept (the floor) and the newest issued.
public sealed record ChangesJournalResponse(string Store, long Floor, long Newest);

[JsonSerializable(typeof(ChangesResponse))]
[JsonSerializable(typeof(JournalPage))]
[JsonSerializable(typeof(RefreshResponse))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(RegisteredResponse))]
[JsonSerializable(typeof(RetiredResponse))]
[JsonSerializable(typeof(RetiredPendingResponse))]
[JsonSerializable(typeof(WireRequest))]
[JsonSerializable(typeof(ImportRequest))]
[JsonSerializable(typeof(ImportedResponse))]
[JsonSerializable(typeof(IEnumerable<RegistrationResponse>))]
[JsonSerializable(typeof(ConfirmationAskRequest))]
[JsonSerializable(typeof(ConfirmationWithdrawRequest))]
[JsonSerializable(typeof(ConfirmationResponse))]
[JsonSerializable(typeof(IEnumerable<ConfirmationResponse>))]
[JsonSerializable(typeof(ConfirmationsWaitingResponse))]
[JsonSerializable(typeof(ConfirmationActionResponse))]
[JsonSerializable(typeof(ConfirmationAskedResponse))]

[JsonSerializable(typeof(FeedEntriesRequest))]
[JsonSerializable(typeof(FeedCodeMapRequest))]
[JsonSerializable(typeof(FeedHeldResponse))]
[JsonSerializable(typeof(SyncResponse))]
[JsonSerializable(typeof(SyncStandingResponse))]
[JsonSerializable(typeof(MachineRetiredResponse))]
[JsonSerializable(typeof(QuestClaimResponse))]
[JsonSerializable(typeof(HandbackResponse))]
[JsonSerializable(typeof(FeedResponse))]
[JsonSerializable(typeof(FeedRefusalResponse))]
internal sealed partial class ApiJson : JsonSerializerContext;
