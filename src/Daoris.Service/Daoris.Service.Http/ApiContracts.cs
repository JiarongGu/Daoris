using System.Text.Json.Serialization;
using Daoris.Knowledge;

namespace Daoris.Knowledge.Http;

// The wire contracts, whole and in one place — every shape a route reads or writes, plus the
// source-generated serializer context (this host publishes AOT-friendly, and reflection-based JSON
// would be the one thing stopping it). Program.cs keeps the routes; the shapes live here.

public sealed record StatusResponse(bool Semantic, string Tier, string? Note);
public sealed record RepositoryResponse(string Name, int Total, int Local, int Canonical, string Workspace);
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
public sealed record QuestResponse(
    string Id, string From, string To, string Title, string Body,
    string Status, string? Note, DateTimeOffset Filed, DateTimeOffset Updated, string Workspace);
public sealed record PublishQuestRequest(string From, string To, string Title, string Body);
public sealed record RespondQuestRequest(string? Action, string? Reason);
public sealed record QuestActionResponse(QuestResponse Quest, string Message);
public sealed record RefreshResponse(int Entries, int Repositories, int Withheld, string? SemanticError);
public sealed record DomainRequest(string? Summary, IReadOnlyList<string>? Owns, IReadOnlyList<string>? Accepts);
// `Workspace` is null on the way IN when the client said nothing — which is what preserves the row
// (D48 §2). It is never null on the way out: a reader is told which circle it is looking at.
public sealed record RegisterRequest(
    string Repository, IReadOnlyList<string>? Packs, DomainRequest? Domain, string? Root,
    bool? Join, bool? ShareKnowledge, string? Workspace);
public sealed record RegisteredResponse(string Repository, DateTimeOffset At, string Workspace);
public sealed record RegistrationResponse(
    string Repository, bool Adopted, bool Registered, string? Summary,
    IReadOnlyList<string> Owns, IReadOnlyList<string> Accepts, IReadOnlyList<string> Packs, int Entries,
    string? Root, bool Joined, bool SharesKnowledge, string Workspace);
public sealed record SessionResponse(
    string Id, string Quest, string Repository, string Adapter, string State,
    string? Note, string? Evidence, string? Transcript, DateTimeOffset Created, DateTimeOffset Updated,
    string Workspace);
public sealed record OpenSessionRequest(string Quest, string Adapter);
public sealed record AdvanceSessionRequest(string? State, string? Note, string? Evidence, string? Transcript);
public sealed record SessionActionResponse(SessionResponse Session, string Message);
public sealed record FeedSessionRecord(
    string Id, string Quest, string Repository, string? Adapter, string? State,
    string? Note, string? Evidence, DateTimeOffset Created, DateTimeOffset Updated);
public sealed record FeedSessionsRequest(IReadOnlyList<FeedSessionRecord>? Records);
public sealed record FeedEntryRecord(string? Kind, string? Title, string? Body, string? RelativePath, string? Anchor);
public sealed record FeedEntriesRequest(string Repository, IReadOnlyList<FeedEntryRecord>? Entries);
public sealed record FeedQuestRecord(
    string Id, string From, string To, string Title, string Body, string? Status, string? Note,
    DateTimeOffset Filed, DateTimeOffset Updated);
public sealed record FeedQuestsRequest(IReadOnlyList<FeedQuestRecord>? Quests);
public sealed record FeedResponse(int Accepted, string Message);
public sealed record ErrorResponse(string Error);

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
[JsonSerializable(typeof(IEnumerable<RegistrationResponse>))]
[JsonSerializable(typeof(IEnumerable<SessionResponse>))]
[JsonSerializable(typeof(OpenSessionRequest))]
[JsonSerializable(typeof(AdvanceSessionRequest))]
[JsonSerializable(typeof(SessionActionResponse))]
[JsonSerializable(typeof(FeedSessionsRequest))]
[JsonSerializable(typeof(FeedEntriesRequest))]
[JsonSerializable(typeof(FeedQuestsRequest))]
[JsonSerializable(typeof(FeedResponse))]
[JsonSerializable(typeof(ErrorResponse))]
internal sealed partial class ApiJson : JsonSerializerContext;
