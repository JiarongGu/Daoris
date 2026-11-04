using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Contracts.Knowledge;

// The knowledge reads: what the host is, what it holds of each repository, and its searches. Every read that spans
// repositories takes `workspace`; absent spans every circle the host holds, and only the caller knows which it speaks for.

/// <summary>What this host is: its tier, its home's id, its version and the doors it knows.</summary>
/// <param name="Prove">A nonce the host answers <see cref="StatusResponse.Proof"/> for, where it holds a person key.</param>
public sealed record ReadStatus(string? Prove = null) : IRoute<StatusResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/status";
}

/// <summary>
/// The tier is said on every answer, not only when the semantic half is absent: a caller with results cannot otherwise tell
/// complete from complete-for-word-overlap. A field added later is never renamed, so a reader older than it skips it.
/// </summary>
/// <param name="Proof">The proof of possession of the person key, answered only to a <c>prove</c> and only by a host holding one.</param>
/// <param name="Home">A local host's home's id, which a terminal checks before it takes an address; absent from a shared host.</param>
/// <param name="Version">The informational version the release workflow stamped.</param>
/// <param name="Closure">The host knows a closure graph's operations and its bind and close doors.</param>
/// <param name="CloseOutcome">The host also answers the outcome door, the close's recovery; a driver binds only where both are said.</param>
/// <param name="CloseRemote">The host judges a bind in a workspace synced through a remote by the remote's handshake.</param>
public sealed record StatusResponse(
    bool Semantic, string Tier, string? Note, string? Proof = null, string? Home = null, string? Version = null, bool? Closure = null,
    bool? CloseOutcome = null, bool? CloseRemote = null);

/// <summary>How a newer client tells what this host does.</summary>
public sealed record ReadProtocol : IRoute<ProtocolResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/protocol";
}

/// <param name="Run">An id the host draws at its start, so a host replaced under one client is told from its predecessor.</param>
/// <param name="Marks">Each protocol the host keeps, by name, at its version; a client keeps none for a name the host does not say.</param>
public sealed record ProtocolResponse(string Run, IReadOnlyDictionary<string, int> Marks);

/// <summary>Each repository the host holds, with what it holds of it.</summary>
public sealed record ListRepositories(string? Workspace = null) : IRoute<RepositoryResponse[]>
{
    public static string Method => "GET";
    public static string Path => "/api/repositories";
}

/// <summary>
/// A fed copy is a claim about a commit, so the answer names the commit, when it was made, its line and the machine that fed
/// it: staleness a person can see beats freshness they must assume. A host that reads its own checkouts has no feed.
/// </summary>
public sealed record ProvenanceResponse(string Commit, string ShortCommit, DateTimeOffset CommittedAt, string Branch, string? Origin);

/// <param name="IndexedAt">When the host read the repository's checkout; null where it read none.</param>
/// <param name="IndexedCommit">The commit that reading was at.</param>
public sealed record RepositoryResponse(
    string Name, int Total, int Local, int Canonical, string Workspace, ProvenanceResponse? Fed,
    DateTimeOffset? IndexedAt = null, string? IndexedCommit = null);

/// <summary>
/// A search of the index, which answers the hits as an array and the tier that answered in the <c>x-daoris-tier</c> header, so
/// the body stays the array every client reads.
/// </summary>
/// <param name="Q">The words; blank is refused.</param>
/// <param name="Kinds">Comma-separated kinds; a kind nobody has is refused rather than widened to every kind.</param>
/// <param name="Repositories">Comma-separated repository names.</param>
/// <param name="LocalOnly">Local entries alone; absent is true.</param>
/// <param name="Limit">How many, 1 to 100; absent is 20.</param>
public sealed record Search(
    string Q, string? Kinds = null, string? Repositories = null, bool? LocalOnly = null, int? Limit = null, string? Workspace = null)
    : IRoute<HitResponse[]>
{
    public static string Method => "GET";
    public static string Path => "/api/search";
}

/// <param name="FirstLine">The entry's first line in its file; null where the entry names none, never a guess.</param>
/// <param name="LastLine">Its last line.</param>
/// <param name="ExcerptLine">The line the excerpt starts on.</param>
public sealed record HitResponse(
    string Id, string Repository, string Kind, string Title, string Path, string? Excerpt, double Score,
    string Workspace, int? FirstLine = null, int? LastLine = null, int? ExcerptLine = null);

/// <summary>One entry, whole; 404 for an id the index does not hold.</summary>
public sealed record ReadEntry(string Id) : IRoute<EntryResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/entry";
}

/// <summary>
/// One repository's own knowledge, whole: what a sync loop feeds from. Local provenance only, the one class a feed may carry;
/// canonical doctrine is distributed by sync.
/// </summary>
public sealed record ListEntries(string Repository) : IRoute<EntryResponse[]>
{
    public static string Method => "GET";
    public static string Path => "/api/entries";
}

public sealed record EntryResponse(
    string Id, string Repository, string Kind, string Provenance, string Title, string Path, string Body,
    string? Anchor, string Workspace, int? FirstLine = null, int? LastLine = null);

/// <summary>Where repositories wrote the same thing: the landing view's read, which tells a person which question to ask.</summary>
/// <param name="MinimumSimilarity">0 to 1; absent is 0.82.</param>
/// <param name="Kinds">Comma-separated kinds; a kind nobody has is refused.</param>
/// <param name="Limit">How many, 1 to 100; absent is 25.</param>
public sealed record FindConvergence(double? MinimumSimilarity = null, string? Kinds = null, int? Limit = null, string? Workspace = null)
    : IRoute<ConvergenceResponse[]>
{
    public static string Method => "GET";
    public static string Path => "/api/convergence";
}

public sealed record ConvergenceEntryResponse(string Id, string Repository, string Kind, string Title, string Path);

/// <param name="Suggestion">The command to run where the change belongs, never an edit: review happens in the repository that owns the file.</param>
public sealed record ConvergenceResponse(
    string Method, double Similarity, IReadOnlyList<string> Repositories, IReadOnlyList<ConvergenceEntryResponse> Entries,
    string Suggestion);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ReadStatus))]
[JsonSerializable(typeof(StatusResponse))]
[JsonSerializable(typeof(ReadProtocol))]
[JsonSerializable(typeof(ProtocolResponse))]
[JsonSerializable(typeof(ListRepositories))]
[JsonSerializable(typeof(RepositoryResponse[]))]
[JsonSerializable(typeof(Search))]
[JsonSerializable(typeof(HitResponse[]))]
[JsonSerializable(typeof(ReadEntry))]
[JsonSerializable(typeof(EntryResponse))]
[JsonSerializable(typeof(ListEntries))]
[JsonSerializable(typeof(EntryResponse[]))]
[JsonSerializable(typeof(FindConvergence))]
[JsonSerializable(typeof(ConvergenceResponse[]))]
internal sealed partial class KnowledgeJson : JsonSerializerContext;
