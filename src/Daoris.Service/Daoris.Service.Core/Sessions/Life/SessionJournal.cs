using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// The sessions' journals of witnessed facts, one append-only table in the service's store, <c>session_facts</c>, and the
/// application's generations beside it (<see cref="AppGenerations"/>). A fact is appended in the same transaction as the stored status
/// it witnesses (<see cref="SessionWrites"/>), so no move of the status goes without its fact; nothing here updates or deletes a fact.
/// </summary>
/// <remarks>
/// While the fold runs in shadow nothing acts on it: the journal is read by the shadow (<see cref="SessionShadow"/>) alone. A record
/// older than the table has no journal and folds to none, since nothing is filled in from the stored status. A deleted record's facts
/// stay until a retention window purges them.
/// </remarks>
public sealed class SessionJournal
{
    private readonly ConnectionGate _db;

    internal SessionJournal(ConnectionGate db) => _db = db;

    /// <summary>The store's step: the journal's table and the generations'.</summary>
    internal async Task EnsureAsync(CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS session_facts (
              position   INTEGER PRIMARY KEY AUTOINCREMENT,
              session    TEXT NOT NULL,
              kind       TEXT NOT NULL,
              witness    TEXT NOT NULL,
              generation INTEGER NOT NULL,
              holder     TEXT NULL,
              step       TEXT NULL,
              attempt    INTEGER NULL,
              outcome    TEXT NULL,
              at         TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS session_facts_session ON session_facts (session, position);
            CREATE TABLE IF NOT EXISTS app_generations (
              generation INTEGER PRIMARY KEY AUTOINCREMENT,
              started    TEXT NOT NULL,
              begun      TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Append the fact <paramref name="moved"/>'s status witnesses, by <paramref name="witness"/>, in the generation the store holds now;
    /// inside the caller's transaction (<see cref="SessionWrites"/> holds one), so the fact and the move commit together.
    /// </summary>
    /// <param name="noted">That the move wrote its note, so the note's code is the caller's reason (<see cref="SessionFact.OfMove"/>).</param>
    internal Task<SessionFact> AppendAsync(Session moved, string witness, CancellationToken inside, bool noted = true) =>
        _db.RunAsync(async () =>
        {
            var generation = await AppGenerations.CurrentAsync(_db, inside).ConfigureAwait(false);
            var fact = SessionFact.OfMove(moved, witness, generation, noted);
            await using var command = _db.Command();
            command.CommandText = """
                INSERT INTO session_facts (session, kind, witness, generation, holder, step, attempt, outcome, at)
                VALUES ($session, $kind, $witness, $generation, $holder, $step, $attempt, $outcome, $at);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$session", fact.Session);
            command.Parameters.AddWithValue("$kind", fact.Kind.ToString());
            command.Parameters.AddWithValue("$witness", fact.Witness);
            command.Parameters.AddWithValue("$generation", fact.Generation);
            command.Parameters.AddWithValue("$holder", (object?)fact.Holder ?? DBNull.Value);
            command.Parameters.AddWithValue("$step", (object?)fact.Step ?? DBNull.Value);
            command.Parameters.AddWithValue("$attempt", (object?)fact.Attempt ?? DBNull.Value);
            command.Parameters.AddWithValue("$outcome", (object?)fact.Outcome?.ToString() ?? DBNull.Value);
            command.Parameters.AddWithValue("$at", Moments.Write(fact.At));
            var position = Convert.ToInt64(await command.ExecuteScalarAsync(inside).ConfigureAwait(false));
            return fact with { Position = position };
        }, inside);

    /// <summary>One session's journal, in order; empty where it has none.</summary>
    public async Task<IReadOnlyList<SessionFact>> OfAsync(string session, CancellationToken ct = default) =>
        (await OfManyAsync([session], ct).ConfigureAwait(false)).GetValueOrDefault(session) ?? [];

    /// <summary>The journals of <paramref name="sessions"/>, each in order; a session with none is left out.</summary>
    public Task<IReadOnlyDictionary<string, IReadOnlyList<SessionFact>>> OfManyAsync(
        IReadOnlyCollection<string> sessions, CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyDictionary<string, IReadOnlyList<SessionFact>>>(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = $"""
                SELECT {FactColumns} FROM session_facts
                WHERE session IN (SELECT value FROM json_each($sessions)) ORDER BY position
                """;
            command.Parameters.AddWithValue("$sessions", System.Text.Json.JsonSerializer.Serialize(sessions.Distinct(StringComparer.Ordinal)));
            return await ReadAsync(command, ct).ConfigureAwait(false);
        }, ct);

    /// <summary>
    /// The sessions the shadow folds, with their stored status: every record whose status is live, and every one whose journal does not
    /// end in an end, since either side may be the one that is wrong.
    /// </summary>
    internal Task<IReadOnlyList<(string Session, SessionState Stored)>> ShadowedAsync(CancellationToken ct) =>
        _db.RunAsync<IReadOnlyList<(string, SessionState)>>(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = $"""
                SELECT s.id, s.state FROM sessions s
                WHERE s.origin IS NULL AND (s.state IN ({SessionSchema.ActiveStates})
                   OR EXISTS (SELECT 1 FROM session_facts f WHERE f.session = s.id
                              AND f.position = (SELECT MAX(position) FROM session_facts g WHERE g.session = s.id) AND f.kind <> 'Ended'))
                """;
            var found = new List<(string, SessionState)>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                if (Session.TryParse(reader.GetString(1), out var state)) found.Add((reader.GetString(0), state));
            }

            return found;
        }, ct);

    /// <summary>The columns <see cref="ReadAsync"/> reads, in its order.</summary>
    internal const string FactColumns = "session, kind, witness, generation, holder, step, attempt, outcome, at, position";

    /// <summary>The facts a query's rows hold (<see cref="FactColumns"/>), by session; a row of a kind this build does not know is left out.</summary>
    internal static async Task<IReadOnlyDictionary<string, IReadOnlyList<SessionFact>>> ReadAsync(SqliteCommand command, CancellationToken ct)
    {
        var journals = new Dictionary<string, List<SessionFact>>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (!Enum.TryParse<SessionFactKind>(reader.GetString(1), out var kind)) continue;
            var fact = new SessionFact(reader.GetString(0), kind, reader.GetString(2), reader.GetInt64(3), Moments.Read(reader.GetString(8)) ?? DateTimeOffset.MinValue)
            {
                Holder = reader.IsDBNull(4) ? null : reader.GetString(4),
                Step = reader.IsDBNull(5) ? null : reader.GetString(5),
                Attempt = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                Outcome = !reader.IsDBNull(7) && Enum.TryParse<SessionOutcome>(reader.GetString(7), out var outcome) ? outcome : null,
                Position = reader.GetInt64(9),
            };
            if (!journals.TryGetValue(fact.Session, out var list)) journals[fact.Session] = list = [];
            list.Add(fact);
        }

        return journals.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<SessionFact>)pair.Value, StringComparer.Ordinal);
    }
}
