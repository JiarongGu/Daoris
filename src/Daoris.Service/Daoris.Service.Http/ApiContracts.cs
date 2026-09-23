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
public sealed record HitResponse(
    string Id, string Repository, string Kind, string Title, string Path, string? Excerpt, double Score,
    string Workspace);
public sealed record EntryResponse(
    string Id, string Repository, string Kind, string Provenance, string Title, string Path, string Body,
    string? Anchor, string Workspace);
public sealed record ConvergenceEntryResponse(
    string Id, string Repository, string Kind, string Title, string Path);
public sealed record ConvergenceResponse(
    string Method, double Similarity, IReadOnlyList<string> Repositories,
    IReadOnlyList<ConvergenceEntryResponse> Entries, string Suggestion);
// What a quest carries (D65 §2) travels on every read: links whole, files by name. `Path` is where
// THIS machine keeps a file — answered only to a caller on this machine, like a transcript (D47 §4),
// and only when the bytes are actually here: a mirrored quest's file is named and not held, and a
// null path is how a reader, and the driver, learn that rather than guess it.
public sealed record QuestAttachmentResponse(string Name, string Sha256, long Bytes, string? Path);
public sealed record QuestResponse(
    string Id, string From, string To, string Title, string Body,
    string Status, string? Note, DateTimeOffset Filed, DateTimeOffset Updated, string Workspace,
    IReadOnlyList<string> Links, IReadOnlyList<QuestAttachmentResponse> Attachments);
// An attachment arrives with its CONTENT at a local host — base64 on the wire, which is what a byte
// array is in JSON — and by NAME at a shared one, which keeps names and never bytes (D65 §2). The door
// decides which shape its mode takes and refuses the other; the exchange never sees the wrong one.
public sealed record QuestAttachmentRequest(string? Name, byte[]? Content, string? Sha256, long? Bytes);
public sealed record PublishQuestRequest(
    string From, string To, string Title, string Body,
    IReadOnlyList<string>? Links = null, IReadOnlyList<QuestAttachmentRequest>? Attachments = null);
public sealed record RespondQuestRequest(string? Action, string? Reason);
public sealed record QuestActionResponse(QuestResponse Quest, string Message);
public sealed record RefreshResponse(
    int Entries, int Repositories, int Withheld, string? SemanticError, IReadOnlyList<string> Absent);
public sealed record DomainRequest(string? Summary, IReadOnlyList<string>? Owns, IReadOnlyList<string>? Accepts);
// `Workspace` is null on the way IN when the client said nothing — which is what preserves the row
// (D48 §2). It is never null on the way out: a reader is told which circle it is looking at.
// `DefaultBranch` is the same shape for the same reason (D48 §6): the checkout that registers knows
// its canonical line and the deployment cannot ask git, but an ordinary registration says nothing
// about it — so null preserves what was declared rather than erasing it.
public sealed record RegisterRequest(
    string Repository, IReadOnlyList<string>? Packs, DomainRequest? Domain, string? Root,
    bool? Join, bool? ShareKnowledge, string? Workspace, string? DefaultBranch);
public sealed record RegisteredResponse(string Repository, DateTimeOffset At, string Workspace);
public sealed record RetiredResponse(string Repository, bool Retired, string Message);
public sealed record WireRequest(string? Workspace);
public sealed record ImportRequest(string? Folder);
public sealed record ImportedResponse(string Folder, int Imported, IReadOnlyList<string> Repositories, string Message);
public sealed record RegistrationResponse(
    string Repository, bool Adopted, bool Registered, string? Summary,
    IReadOnlyList<string> Owns, IReadOnlyList<string> Accepts, IReadOnlyList<string> Packs, int Entries,
    string? Root, bool Joined, bool SharesKnowledge, string Workspace, string? DefaultBranch);
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
    string? BaseCommit = null);
// The tree comes IN from the driver, which is the half that knows: the service has no checkout to
// look at, exactly as it has no binaries to probe (D46 §7). Unstated resolves to the registered root.
public sealed record OpenSessionRequest(
    string Quest, string Adapter, string? HarnessVersion, string? Profile, string? Tree,
    string? BaseCommit = null);
public sealed record OpenChatRequest(
    string Repository, string? Adapter, string? HarnessVersion, string? Profile, string? Tree,
    string? BaseCommit = null);
public sealed record AdvanceSessionRequest(string? State, string? Note, string? Evidence, string? Transcript);
public sealed record SessionActionResponse(SessionResponse Session, string Message);
// `Quest` is null for a chat and `Kind` says which it was (D49 §3) — both travel, because a teammate
// seeing a record deserves to know somebody was talking rather than that work was planned. So does
// `HarnessVersion` (D49 §4): which tool produced this is a fact about a tool. There is deliberately
// NO profile field — which account a session ran as is machine-local, like the transcript.
public sealed record FeedSessionRecord(
    string Id, string? Quest, string Repository, string? Adapter, string? State,
    string? Note, string? Evidence, DateTimeOffset Created, DateTimeOffset Updated, string? Kind,
    string? HarnessVersion);
public sealed record FeedSessionsRequest(IReadOnlyList<FeedSessionRecord>? Records);
public sealed record FeedEntryRecord(string? Kind, string? Title, string? Body, string? RelativePath, string? Anchor);
// The three provenance fields are the feed's claim about WHICH point in the history it speaks for
// (D48 §6). Nullable on the wire and judged at the door: a feed that names no commit cannot be
// compared with what is held, and a wholesale replacement that cannot be compared is the flapping
// this exists to end.
public sealed record FeedEntriesRequest(
    string Repository, IReadOnlyList<FeedEntryRecord>? Entries,
    string? Commit, DateTimeOffset? CommittedAt, string? Branch);
// A mirrored quest carries what its home's record carries — links, and files BY NAME: the bytes are
// on the machine that published them, and a mirror is exactly where a file is named and not held.
public sealed record FeedQuestAttachment(string? Name, string? Sha256, long? Bytes);
public sealed record FeedQuestRecord(
    string Id, string From, string To, string Title, string Body, string? Status, string? Note,
    DateTimeOffset Filed, DateTimeOffset Updated,
    IReadOnlyList<string>? Links = null, IReadOnlyList<FeedQuestAttachment>? Attachments = null);
public sealed record FeedQuestsRequest(IReadOnlyList<FeedQuestRecord>? Quests);
public sealed record FeedResponse(int Accepted, string Message);
public sealed record ErrorResponse(string Error);
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
[JsonSerializable(typeof(QuestActionResponse))]
[JsonSerializable(typeof(RefreshResponse))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(RegisteredResponse))]
[JsonSerializable(typeof(RetiredResponse))]
[JsonSerializable(typeof(WireRequest))]
[JsonSerializable(typeof(ImportRequest))]
[JsonSerializable(typeof(ImportedResponse))]
[JsonSerializable(typeof(IEnumerable<RegistrationResponse>))]
[JsonSerializable(typeof(IEnumerable<SessionResponse>))]
[JsonSerializable(typeof(OpenSessionRequest))]
[JsonSerializable(typeof(OpenChatRequest))]
[JsonSerializable(typeof(AdvanceSessionRequest))]
[JsonSerializable(typeof(SessionActionResponse))]
[JsonSerializable(typeof(FeedSessionsRequest))]
[JsonSerializable(typeof(FeedEntriesRequest))]
[JsonSerializable(typeof(FeedQuestsRequest))]
[JsonSerializable(typeof(FeedResponse))]
[JsonSerializable(typeof(FeedRefusalResponse))]
[JsonSerializable(typeof(ErrorResponse))]
internal sealed partial class ApiJson : JsonSerializerContext;
