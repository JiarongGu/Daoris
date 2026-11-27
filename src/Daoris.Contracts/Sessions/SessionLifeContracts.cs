namespace Daoris.Contracts.Sessions;

// The doors of a session's life: the application's start begins its generation, and the fold is read in shadow beside the stored
// status. A local host's alone: the leases are this machine's processes.

/// <summary>The application's start: begins its generation, which every fact after is stamped with and every lease is named by.</summary>
/// <param name="Started">When the application's process started, ISO 8601: a process that started before it is of an earlier generation.</param>
public sealed record BeginGeneration(string? Started) : IRoute<GenerationResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/session-life/generations";
}

/// <summary>The generation begun.</summary>
public sealed record GenerationResponse(long Generation, string Started);

/// <summary>
/// The fold of every live session in shadow, against the processes the caller proved alive: answered where it differs from the stored
/// status, each difference with both sides and the facts. Nothing acts on it.
/// </summary>
/// <param name="Leases">Each process proved alive by its marker: its session and when it started.</param>
public sealed record FoldSessions(IReadOnlyList<LeaseRequest>? Leases) : IRoute<FoldSessionsResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/session-life/fold";
}

/// <summary>A process proved alive for a session: <paramref name="Started"/> is its start, ISO 8601.</summary>
public sealed record LeaseRequest(string Session, string Started);

/// <summary>What the shadow folded: the generation, how many records, how long it took, and where the fold and the status differ.</summary>
public sealed record FoldSessionsResponse(long Generation, int Folded, double Millis, IReadOnlyList<FoldDifferenceResponse> Differences);

/// <summary>
/// One session where the fold differs from its stored status: the stored state's spelling, the fold's state (<c>working</c>,
/// <c>waiting</c>, <c>needs-you</c>, <c>ended</c>), its outcome, holder, since when and breach, and the facts it read.
/// </summary>
public sealed record FoldDifferenceResponse(
    string Session, string Stored, string State, string? Outcome, string? Holder, string Since, string? Breach,
    IReadOnlyList<SessionFactResponse> Facts);

/// <summary>One fact of a session's journal, as the wire spells it.</summary>
public sealed record SessionFactResponse(
    long Position, string Kind, string Witness, long Generation, string At, string? Holder = null, string? Step = null,
    int? Attempt = null, string? Outcome = null);
