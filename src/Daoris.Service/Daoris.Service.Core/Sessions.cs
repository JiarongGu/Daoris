using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// Where a driver-started session is in its life. Observed, never self-reported: the driver moves a
/// session by what it can see — process lifetime, and the quest's own transitions.
/// </summary>
public enum SessionState
{
    /// <summary>The driver decided to start this; waiting on the repository's slot, hold, or tree.</summary>
    Queued,

    /// <summary>The process is being spawned.</summary>
    Starting,

    /// <summary>The process is alive.</summary>
    Working,

    /// <summary>Parked at a checkpoint only the person can clear. Still holds its repository.</summary>
    AwaitingPerson,

    /// <summary>Exited; its quest reached Done.</summary>
    Completed,

    /// <summary>Exited; its quest reached Declined, with the reason on the quest.</summary>
    Declined,

    /// <summary>Exited without working: the quest was already taken or closed by someone else.</summary>
    StoodDown,

    /// <summary>The process died, or exited with the quest unexplained.</summary>
    Failed,

    /// <summary>The person cancelled it.</summary>
    Stopped,
}

/// <param name="Id">Short random handle — an attempt, so never content-derived: a retry is a new record.</param>
/// <param name="Quest">The quest this session was started to serve.</param>
/// <param name="Repository">The repository it runs in — the quest's receiver, denormalized for listing.</param>
/// <param name="Adapter">Which harness adapter spawned it. A name for the record, never a model (D24).</param>
/// <param name="State">Where it is.</param>
/// <param name="Note">What the driver observed, when a state needs explaining.</param>
/// <param name="Evidence">The outcome bundle the person reviews — gate results, landed commits.</param>
/// <param name="Transcript">A machine-local path to the captured run. Diagnostic, never the record.</param>
/// <param name="Created">When the driver queued it.</param>
/// <param name="Updated">When its state last moved.</param>
public sealed record Session(
    string Id,
    string Quest,
    string Repository,
    string Adapter,
    SessionState State,
    string? Note,
    string? Evidence,
    string? Transcript,
    DateTimeOffset Created,
    DateTimeOffset Updated)
{
    /// <summary>Whether this session still holds its repository. Parked counts: the person is the flow control.</summary>
    public bool Active => State is SessionState.Queued or SessionState.Starting
        or SessionState.Working or SessionState.AwaitingPerson;

    /// <summary>This state's public spelling — what every door prints and accepts back.</summary>
    public string StateName => Spell(State);

    /// <summary>The design's spelling (D46 §4): kebab-case on the wire, enum names in the store.</summary>
    public static string Spell(SessionState state) => state switch
    {
        SessionState.AwaitingPerson => "awaiting-person",
        SessionState.StoodDown => "stood-down",
        _ => state.ToString().ToLowerInvariant(),
    };

    /// <summary>The reverse of <see cref="Spell"/>: what every door accepts back, tolerantly.</summary>
    public static bool TryParse(string value, out SessionState state) =>
        Enum.TryParse(value.Replace("-", "", StringComparison.Ordinal), ignoreCase: true, out state)
        && Enum.IsDefined(state);
}

/// <summary>
/// Session records, held by the service beside the quests they serve.
/// </summary>
/// <remarks>
/// <para><b>The record is service state; the process is not.</b> Every client benefits from the record —
/// the platform renders it, it survives a driver restart, and a remote deployment can sync records where
/// it could never sync processes. The process handle stays with the driver that spawned it (D46).</para>
///
/// <para><b>This store is deliberately as blind as the quest store.</b> What may move where is judgement,
/// and judgement lives in <see cref="SessionLedger"/> — one implementation for every door, for the same
/// reason <see cref="QuestExchange"/> exists.</para>
/// </remarks>
public sealed class SessionStore
{
    private readonly SqliteConnection _connection;

    private SessionStore(SqliteConnection connection) => _connection = connection;

    public static async Task<SessionStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        var store = new SessionStore(connection);
        await store.EnsureSchemaAsync(ct).ConfigureAwait(false);
        return store;
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
              id         TEXT PRIMARY KEY,
              quest      TEXT NOT NULL,
              repository TEXT NOT NULL,
              adapter    TEXT NOT NULL,
              state      TEXT NOT NULL,
              note       TEXT NULL,
              evidence   TEXT NULL,
              transcript TEXT NULL,
              created    TEXT NOT NULL,
              updated    TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS sessions_repository ON sessions (repository, state);
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Record a new attempt, queued. Random id: two attempts at one quest are two records.</summary>
    public async Task<Session> CreateAsync(
        string quest, string repository, string adapter, DateTimeOffset now, CancellationToken ct = default)
    {
        var session = new Session(
            Guid.NewGuid().ToString("N")[..8], quest, repository, adapter,
            SessionState.Queued, null, null, null, now, now);

        await using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sessions (id, quest, repository, adapter, state, note, evidence, transcript, created, updated)
            VALUES ($id, $quest, $repository, $adapter, $state, NULL, NULL, NULL, $created, $updated)
            """;
        command.Parameters.AddWithValue("$id", session.Id);
        command.Parameters.AddWithValue("$quest", session.Quest);
        command.Parameters.AddWithValue("$repository", session.Repository);
        command.Parameters.AddWithValue("$adapter", session.Adapter);
        command.Parameters.AddWithValue("$state", session.State.ToString());
        command.Parameters.AddWithValue("$created", session.Created.ToString("O"));
        command.Parameters.AddWithValue("$updated", session.Updated.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return session;
    }

    /// <summary>
    /// Move a session and attach what the move carries. An attachment left null keeps its old value:
    /// a later move must not erase the transcript an earlier one recorded.
    /// </summary>
    public async Task<Session?> SetStateAsync(
        string id, SessionState state, string? note, string? evidence, string? transcript,
        DateTimeOffset now, CancellationToken ct = default)
    {
        var session = await FindAsync(id, ct).ConfigureAwait(false);
        if (session is null) return null;

        var moved = session with
        {
            State = state,
            Note = note ?? session.Note,
            Evidence = evidence ?? session.Evidence,
            Transcript = transcript ?? session.Transcript,
            Updated = now,
        };

        await using var command = _connection.CreateCommand();
        command.CommandText = """
            UPDATE sessions SET state = $state, note = $note, evidence = $evidence,
              transcript = $transcript, updated = $updated WHERE id = $id
            """;
        command.Parameters.AddWithValue("$state", moved.State.ToString());
        command.Parameters.AddWithValue("$note", (object?)moved.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$evidence", (object?)moved.Evidence ?? DBNull.Value);
        command.Parameters.AddWithValue("$transcript", (object?)moved.Transcript ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", moved.Updated.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return moved;
    }

    /// <summary>
    /// Copy another machine's session record into this store, whole (D47 §6). The judgement already ran
    /// where the process lived — the ledger's rules governed the original — so a fed record upserts
    /// verbatim and is never re-judged. The caller keys it by origin + id; the transcript never arrives,
    /// because the feed has no field for a machine path.
    /// </summary>
    public async Task MirrorAsync(Session record, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sessions (id, quest, repository, adapter, state, note, evidence, transcript, created, updated)
            VALUES ($id, $quest, $repository, $adapter, $state, $note, $evidence, NULL, $created, $updated)
            ON CONFLICT (id) DO UPDATE SET
              state = $state, note = $note, evidence = $evidence, updated = $updated
            """;
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$quest", record.Quest);
        command.Parameters.AddWithValue("$repository", record.Repository);
        command.Parameters.AddWithValue("$adapter", record.Adapter);
        command.Parameters.AddWithValue("$state", record.State.ToString());
        command.Parameters.AddWithValue("$note", (object?)record.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$evidence", (object?)record.Evidence ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", record.Created.ToString("O"));
        command.Parameters.AddWithValue("$updated", record.Updated.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<Session?> FindAsync(string id, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM sessions WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>The session holding a repository, if any — the one-session-per-repository question.</summary>
    public async Task<Session?> ActiveForAsync(string repository, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = $"""
            SELECT * FROM sessions
            WHERE repository = $repository AND state IN ({ActiveStates}) LIMIT 1
            """;
        command.Parameters.AddWithValue("$repository", repository);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>
    /// Sessions in one repository, or everywhere. Live work first, closed records on request —
    /// the same shape as the quest list, for the same reason.
    /// </summary>
    public async Task<IReadOnlyList<Session>> ListAsync(
        string? repository = null, bool includeClosed = false, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = $"""
            SELECT * FROM sessions
            WHERE ($repository IS NULL OR repository = $repository)
              {(includeClosed ? "" : $"AND state IN ({ActiveStates})")}
            ORDER BY CASE WHEN state IN ({ActiveStates}) THEN 0 ELSE 1 END, created
            """;
        command.Parameters.AddWithValue("$repository", (object?)repository ?? DBNull.Value);

        var sessions = new List<Session>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) sessions.Add(Read(reader));
        return sessions;
    }

    private const string ActiveStates = "'Queued', 'Starting', 'Working', 'AwaitingPerson'";

    private static Session Read(SqliteDataReader reader) => new(
        reader.GetString(reader.GetOrdinal("id")),
        reader.GetString(reader.GetOrdinal("quest")),
        reader.GetString(reader.GetOrdinal("repository")),
        reader.GetString(reader.GetOrdinal("adapter")),
        Enum.Parse<SessionState>(reader.GetString(reader.GetOrdinal("state"))),
        reader.IsDBNull(reader.GetOrdinal("note")) ? null : reader.GetString(reader.GetOrdinal("note")),
        reader.IsDBNull(reader.GetOrdinal("evidence")) ? null : reader.GetString(reader.GetOrdinal("evidence")),
        reader.IsDBNull(reader.GetOrdinal("transcript")) ? null : reader.GetString(reader.GetOrdinal("transcript")),
        DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("created"))),
        DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("updated"))));
}
