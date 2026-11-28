namespace Daoris.Contracts.Sessions;

// The doors of a session's life: the home lease holder's start begins its generation, the watch's tick audits the fold in shadow beside
// the stored status, and the list reads the last audit. A local host's alone: the leases are this machine's processes.

/// <summary>What a request to the service carries beside its body for a session's life.</summary>
public static class SessionLifeHeaders
{
    /// <summary>
    /// The application's generation the caller writes in: every fact a request's writes append names it, so a process that outlived a
    /// restart writes in its own, earlier one. A request without it writes in none (0): a person's door, the host's own work.
    /// </summary>
    public const string Generation = "Daoris-Generation";
}

/// <summary>The start of the application that holds the home's lease: begins its generation. Only that holder asks it.</summary>
/// <param name="Started">When the application's process started, ISO 8601.</param>
public sealed record BeginGeneration(string? Started) : IRoute<GenerationResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/session-life/generations";
}

/// <summary>The generation begun.</summary>
public sealed record GenerationResponse(long Generation, string Started);

/// <summary>
/// The watch's tick: the fold of every live session audited in shadow against the leases the caller's markers prove, each breach of
/// the invariant and each difference from the stored status said once in the machine log, and kept as the last audit the list reads.
/// Nothing acts on it.
/// </summary>
/// <param name="Leases">Each session process its marker proves alive: its session, its generation, and whether its owner runs.</param>
public sealed record AuditSessions(IReadOnlyList<LeaseRequest>? Leases) : IRoute<FoldReadingResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/session-life/audit";
}

/// <summary>The last audit, as the list reads it: no fold is computed for a read.</summary>
public sealed record ReadFold : IRoute<FoldReadingResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/session-life/fold";
}

/// <summary>A session process proved alive by its marker.</summary>
/// <param name="Session">The session it works on.</param>
/// <param name="Generation">The generation its marker names.</param>
/// <param name="OwnerRuns">Whether the driver that started it still runs: a lease is live only while it does.</param>
public sealed record LeaseRequest(string Session, long Generation, bool OwnerRuns);

/// <summary>
/// An audit: the generation it reads in, when (none before the first tick), how many records it folded and how long that took,
/// where the fold and the stored status differ, and the sessions whose journal holds a row this build cannot read.
/// </summary>
public sealed record FoldReadingResponse(
    long Generation, string? Audited, int Folded, double Millis, IReadOnlyList<FoldDifferenceResponse> Differences,
    IReadOnlyList<string> Unreadable);

/// <summary>
/// One session where the fold differs from its stored status: the stored state's spelling, the fold's state (<c>working</c>,
/// <c>waiting</c>, <c>needs-you</c>, <c>ended</c>), its outcome, holder (a kind, with its subject), since when, its breach's code, and
/// the last fact it folded.
/// </summary>
public sealed record FoldDifferenceResponse(
    string Session, string Stored, string State, string? Outcome, string? Holder, string? Subject, string Since, string? Breach,
    SessionFactResponse Last);

/// <summary>One fact of a session's journal, as the wire spells it: its door and reason code are its witness.</summary>
public sealed record SessionFactResponse(
    long Position, string Kind, string Door, string? Reason, long Generation, string At, string? Holder = null, string? Subject = null,
    string? Step = null, int? Attempt = null, string? Outcome = null);
