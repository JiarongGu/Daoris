using Daoris.Contracts.Quests;

namespace Daoris.Contracts.Sessions;

// The doors a local host serves for what it keeps beside a session: a finished tree's record, the memory a session changed, and a
// session's delete. Machine-local, as the rows are: a shared host maps none, and a caller off this machine is answered not found.

/// <summary>
/// A finished tree's record: the driver posts the manifest's facts and the commits read from the tip, after the branch went and outside
/// the repository's hold. Idempotent on (workspace, repository, folder): a second post of a kept identity answers as the first did and
/// writes nothing. A store a newer build stamped is a 409 with code <c>RECORDS_NEWER</c>.
/// </summary>
/// <param name="Folder">The tree's set-aside identity, <c>&lt;tree&gt;-&lt;moment&gt;</c>.</param>
/// <param name="Tree">The tree's path before the move: the sessions whose row names it are the record's.</param>
/// <param name="Commits">Oldest first; null with <paramref name="CommitsUnread"/> where the tip no longer read.</param>
public sealed record PostWorkRecord(
    string? Workspace, string? Repository, string? Folder, string? Tree, DateTimeOffset? Moved, string? Unit, string? Branch, string? Line,
    WorkRecordHoldWire? Hold, string? Tip, string? GrewFrom, string? Ref, List<WorkRecordCommitWire>? Commits, string? CommitsUnread,
    WorkRecordLeftWire? Left, bool Whole = false, WorkRecordLandingWire? Landing = null, string? LandingWhy = null,
    Dictionary<string, MemoryTurnsRequest>? Turns = null) : IRoute<WorkRecordResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/trees/record";
}

/// <summary>History's age applied to the finished trees' records no session names: each kept at or before <c>Before</c> goes by its own time.</summary>
/// <param name="Workspace">Narrows it to one circle.</param>
public sealed record AgeWorkRecords(DateTimeOffset? Before, string? Workspace = null) : IRoute<WorkRecordAgeResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/trees/record/age";
}

/// <summary>The record of the finished tree a session ran in, for the session's own page once its tree is let go; none kept is not found.</summary>
public sealed record ReadSessionRecord(string Id) : IRoute<WorkRecordShownResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/sessions/{id}/record";
}

/// <summary>
/// A session's memory changes for its record's page, an end's reason without the row's token. A proposal is read as its quest and target
/// left it, settled first.
/// </summary>
public sealed record ReadSessionMemory(string Id) : IRoute<SessionMemoryResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/sessions/{id}/memory";
}

/// <summary>Every memory wait on the person, for the desktop's <em>What needs you</em>.</summary>
public sealed record ListMemoryWaits : IRoute<MemoryWaitsResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/memory/waits";
}

/// <summary>
/// What the line ending at a session read, each entry read again and its cited records rechecked, for the brief of the part that
/// continues it; what is answered is kept as the line's read. Another repository's entry is read through its view.
/// </summary>
public sealed record ReadLineMemory(string Id) : IRoute<MemoryReadResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/memory/read";
}

/// <summary>The person's approval or decline of an awaiting end, kept on its row.</summary>
/// <param name="Id">The row's.</param>
/// <param name="Approve">True approves the end, false declines it; required.</param>
public sealed record DecideMemory(string Id, bool? Approve) : IRoute<MemoryDecisionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/memory/{id}/decide";
}

/// <summary>The person's keep or drop of an ended session's staged memory changes.</summary>
/// <param name="Keep">True keeps the staged changes, false drops them; required.</param>
public sealed record KeepSessionMemory(string Id, bool? Keep) : IRoute<MemoryDecisionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sessions/{id}/memory";
}

/// <summary>
/// Deleting a conversation that served no quest: the ledger judges the record's half and removes it, and the driver, which judged the
/// machine's half first, removes what this machine kept of it after this yes. A refusal answers a <see cref="SessionDeletionResponse"/>
/// (404 not found; 409 for a live record, another machine's, or a record something names).
/// </summary>
public sealed record DeleteSession(string Id) : IRoute<DeletedResponse>
{
    public static string Method => "DELETE";
    public static string Path => "/api/sessions/{id}";
}

/// <summary>The same judgement, deleting nothing, so a refusal is said in the order a person meets it. Answers 404 for an unknown record.</summary>
public sealed record JudgeSessionDelete(string Id) : IRoute<SessionDeletionResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/sessions/{id}/deletable";
}
