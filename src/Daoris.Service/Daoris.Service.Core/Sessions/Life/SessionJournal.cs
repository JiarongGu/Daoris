using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>A row of the journal this build cannot read: its place, its session, the column and what it held. Refused, never skipped.</summary>
public sealed record UnreadableFact(long Position, string Session, string Column, string? Value);

/// <summary>The journals a read found, by session, and the rows it refused: a session with a refused row is left out whole.</summary>
public sealed record JournalRead(IReadOnlyDictionary<string, IReadOnlyList<SessionFact>> Journals, IReadOnlyList<UnreadableFact> Unreadable);

/// <summary>A session the shadow folds: its stored status and its journal's last fact, null where it has none.</summary>
public sealed record ShadowedSession(string Session, SessionState Stored, SessionFact? Last);

/// <summary>A trigger-written state move of <c>event_journal</c> no fact witnesses: its place, its record, and the state it moved to.</summary>
public sealed record UnwitnessedMove(long Position, string Session, string Change, string? State);

/// <summary>
/// The sessions' journals of witnessed facts, one append-only table in the service's store, <c>session_facts</c>, and the
/// application's generations beside it (<see cref="AppGenerations"/>). A fact is appended in the same transaction as the stored status
/// it witnesses (<see cref="SessionWrites"/>), with the generation its writer names (<see cref="FactWriter"/>); nothing here updates or
/// deletes a fact.
/// </summary>
/// <remarks>
/// <para><b>Beside <c>event_journal</c>.</b> The trigger-written <c>event_journal</c> keeps every raw transition of a record, bounded to its
/// newest rows, for the change stream and the history; its sessions' <c>opened</c> and <c>state</c> rows say the stored status moved, and
/// each names the fact that witnessed it (<see cref="SessionFact.Minted"/>), which is the census (<see cref="UnwitnessedAsync"/>). The
/// stored status and those two kinds of row end with the switch to the fold; the facts are then a session's one record of its life.</para>
/// <para>While the fold runs in shadow nothing acts on it. A record older than the table has no journal and the shadow folds it to none;
/// a deleted record's facts stay until a retention window purges them.</para>
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
              door       TEXT NOT NULL,
              reason     TEXT NULL,
              generation INTEGER NOT NULL,
              holder     TEXT NOT NULL,
              subject    TEXT NULL,
              step       TEXT NULL,
              attempt    INTEGER NULL,
              outcome    TEXT NULL,
              minted     INTEGER NULL,
              at         TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS session_facts_session ON session_facts (session, position);
            CREATE INDEX IF NOT EXISTS session_facts_minted ON session_facts (minted) WHERE minted IS NOT NULL;
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await AppGenerations.EnsureAsync(_db, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Append the fact <paramref name="moved"/>'s status witnesses, by <paramref name="door"/>, in the generation its writer names; inside
    /// the caller's transaction (<see cref="SessionWrites"/> holds one), so the fact and the move commit together.
    /// </summary>
    /// <param name="minted">That the write minted an <c>event_journal</c> state row (an open, or a move to another state), which the fact names.</param>
    /// <param name="noted">That the move wrote its note, so the note's code is the fact's reason (<see cref="SessionFact.OfMove"/>).</param>
    /// <param name="step">The step a session opened for, where its open names one.</param>
    /// <param name="attempt">That step's attempt.</param>
    internal Task<SessionFact> AppendAsync(
        Session moved, WitnessDoor door, bool minted, CancellationToken inside, bool noted = true, string? step = null, int? attempt = null) =>
        _db.RunAsync(async () =>
        {
            var fact = SessionFact.OfMove(moved, door, FactWriter.Generation, noted);
            if (step is not null) fact = fact with { Step = step, Attempt = attempt };
            await using var command = _db.Command();
            command.CommandText = """
                INSERT INTO session_facts (session, kind, door, reason, generation, holder, subject, step, attempt, outcome, minted, at)
                VALUES ($session, $kind, $door, $reason, $generation, $holder, $subject, $step, $attempt, $outcome,
                        CASE WHEN $minted THEN (SELECT MAX(position) FROM event_journal
                                                WHERE record = $session AND change IN ('opened', 'state')) END,
                        $at);
                SELECT position, minted FROM session_facts WHERE position = last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$session", fact.Session);
            command.Parameters.AddWithValue("$kind", Vocabulary<SessionFactKind>.Spell(fact.Kind));
            command.Parameters.AddWithValue("$door", Vocabulary<WitnessDoor>.Spell(fact.Witness.Door));
            command.Parameters.AddWithValue("$reason", (object?)fact.Witness.Reason ?? DBNull.Value);
            command.Parameters.AddWithValue("$generation", fact.Generation);
            command.Parameters.AddWithValue("$holder", Vocabulary<HolderKind>.Spell(fact.Holder.Kind));
            command.Parameters.AddWithValue("$subject", (object?)fact.Holder.Subject ?? DBNull.Value);
            command.Parameters.AddWithValue("$step", (object?)fact.Step ?? DBNull.Value);
            command.Parameters.AddWithValue("$attempt", (object?)fact.Attempt ?? DBNull.Value);
            command.Parameters.AddWithValue("$outcome", fact.Outcome is { } outcome ? Vocabulary<SessionOutcome>.Spell(outcome) : DBNull.Value);
            command.Parameters.AddWithValue("$minted", minted);
            command.Parameters.AddWithValue("$at", Moments.Write(fact.At));
            await using var reader = await command.ExecuteReaderAsync(inside).ConfigureAwait(false);
            await reader.ReadAsync(inside).ConfigureAwait(false);
            return fact with { Position = reader.GetInt64(0), Minted = reader.IsDBNull(1) ? null : reader.GetInt64(1) };
        }, inside);

    /// <summary>One session's journal, in order; empty where it has none.</summary>
    /// <exception cref="InvalidDataException">A row of it this build cannot read.</exception>
    public async Task<IReadOnlyList<SessionFact>> OfAsync(string session, CancellationToken ct = default)
    {
        var read = await OfManyAsync([session], ct).ConfigureAwait(false);
        if (read.Unreadable is [var row, ..]) throw new InvalidDataException(Said(row));
        return read.Journals.GetValueOrDefault(session) ?? [];
    }

    /// <summary>The journals of <paramref name="sessions"/>, each in order, and the rows refused; a session with none is left out.</summary>
    public Task<JournalRead> OfManyAsync(IReadOnlyCollection<string> sessions, CancellationToken ct = default) =>
        _db.RunAsync(async () =>
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
    /// The sessions the shadow folds, with their stored status and their journal's last fact alone (the fold reads no more): every record
    /// whose status is live, and every one whose journal does not end in an end, since either side may be the one that is wrong.
    /// </summary>
    internal Task<(IReadOnlyList<ShadowedSession> Sessions, IReadOnlyList<UnreadableFact> Unreadable)> ShadowedAsync(CancellationToken ct) =>
        _db.RunAsync<(IReadOnlyList<ShadowedSession>, IReadOnlyList<UnreadableFact>)>(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = $"""
                WITH last AS (SELECT session, MAX(position) AS position FROM session_facts GROUP BY session)
                SELECT s.id, s.state, {Prefixed("f.")} FROM sessions s
                LEFT JOIN last l ON l.session = s.id
                LEFT JOIN session_facts f ON f.position = l.position
                WHERE s.origin IS NULL AND (s.state IN ({SessionSchema.ActiveStates}) OR (f.kind IS NOT NULL AND f.kind <> 'ended'))
                """;
            var found = new List<ShadowedSession>();
            var refused = new List<UnreadableFact>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                if (!Session.TryParse(reader.GetString(1), out var stored)) continue;
                SessionFact? last = null;
                if (!reader.IsDBNull(2))
                {
                    last = Fact(reader, 2, out var unreadable);
                    if (unreadable is not null)
                    {
                        refused.Add(unreadable);
                        continue;
                    }
                }

                found.Add(new ShadowedSession(reader.GetString(0), stored, last));
            }

            return (found, refused);
        }, ct);

    /// <summary>
    /// The census: every trigger-written <c>opened</c> and <c>state</c> row of a session in <c>event_journal</c> that no fact witnesses,
    /// from the first row a fact witnesses on (a store's rows from before the journal witness nothing); none where either table is not there.
    /// </summary>
    public Task<IReadOnlyList<UnwitnessedMove>> UnwitnessedAsync(CancellationToken ct = default) =>
        _db.RunAsync(() => UnwitnessedAsync(_db, ct), ct);

    internal static async Task<IReadOnlyList<UnwitnessedMove>> UnwitnessedAsync(ConnectionGate db, CancellationToken ct)
    {
        await using (var tables = db.Command())
        {
            tables.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name IN ('event_journal', 'session_facts')";
            if (Convert.ToInt64(await tables.ExecuteScalarAsync(ct).ConfigureAwait(false)) < 2) return [];
        }

        await using var command = db.Command();
        command.CommandText = """
            SELECT j.position, j.record, j.change, j.state_new FROM event_journal j
            WHERE j.family = 'sessions' AND j.change IN ('opened', 'state')
              AND j.position >= COALESCE((SELECT MIN(minted) FROM session_facts), (SELECT MAX(position) FROM event_journal) + 1)
              AND NOT EXISTS (SELECT 1 FROM session_facts f WHERE f.minted = j.position)
            ORDER BY j.position
            """;
        var found = new List<UnwitnessedMove>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            found.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return found;
    }

    /// <summary>The columns <see cref="ReadAsync"/> reads, in its order.</summary>
    internal const string FactColumns = "session, kind, door, reason, generation, holder, subject, step, attempt, outcome, minted, at, position";

    private static string Prefixed(string prefix) => string.Join(", ", FactColumns.Split(", ").Select(column => prefix + column));

    /// <summary>The facts a query's rows hold (<see cref="FactColumns"/>), by session, and the rows refused, each session with one left out whole.</summary>
    internal static async Task<JournalRead> ReadAsync(SqliteCommand command, CancellationToken ct)
    {
        var journals = new Dictionary<string, List<SessionFact>>(StringComparer.Ordinal);
        var refused = new List<UnreadableFact>();
        var refusedSessions = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var fact = Fact(reader, 0, out var unreadable);
            if (unreadable is not null)
            {
                refused.Add(unreadable);
                refusedSessions.Add(unreadable.Session);
                continue;
            }

            if (!journals.TryGetValue(fact.Session, out var list)) journals[fact.Session] = list = [];
            list.Add(fact);
        }

        return new(
            journals.Where(pair => !refusedSessions.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<SessionFact>)pair.Value, StringComparer.Ordinal),
            refused);
    }

    /// <summary>What a refused row says, once: its place, its session, the column and its value.</summary>
    public static string Said(UnreadableFact row) =>
        $"the journal's fact {row.Position} of session {row.Session} holds '{row.Value}' in `{row.Column}`, which this build does not read";

    /// <summary>The fact in <see cref="FactColumns"/> from <paramref name="at"/>; <paramref name="unreadable"/> names the first column it cannot read.</summary>
    private static SessionFact Fact(SqliteDataReader reader, int at, out UnreadableFact? unreadable)
    {
        var session = reader.GetString(at);
        var position = reader.GetInt64(at + 12);
        string? Text(int column) => reader.IsDBNull(at + column) ? null : reader.GetString(at + column);
        UnreadableFact Refused(string column, string? value) => new(position, session, column, value);

        unreadable = null;
        var kind = Vocabulary<SessionFactKind>.Read(Text(1));
        var door = Vocabulary<WitnessDoor>.Read(Text(2));
        var holder = Vocabulary<HolderKind>.Read(Text(5));
        var outcome = Text(9) is { } spelled ? Vocabulary<SessionOutcome>.Read(spelled) : null;
        var moment = Moments.Read(Text(11) ?? "");
        unreadable = kind is null ? Refused("kind", Text(1))
            : door is null ? Refused("door", Text(2))
            : holder is null ? Refused("holder", Text(5))
            : Text(9) is not null && outcome is null ? Refused("outcome", Text(9))
            : moment is null ? Refused("at", Text(11))
            : null;
        if (unreadable is not null) return null!;

        return new SessionFact(session, kind!.Value, new FactWitness(door!.Value, Text(3)), reader.GetInt64(at + 4), moment!.Value)
        {
            Holder = new SessionHolder(holder!.Value, Text(6)),
            Step = Text(7),
            Attempt = reader.IsDBNull(at + 8) ? null : reader.GetInt32(at + 8),
            Outcome = outcome,
            Minted = reader.IsDBNull(at + 10) ? null : reader.GetInt64(at + 10),
            Position = position,
        };
    }
}
