using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// The memory coordinator's log of record (KNOWCENTRAL2c; the design §3): <c>memory_ops</c> in <c>knowledge.db</c>, beside the sessions and
/// the finished trees' records, so a commit point moves its session's rows in the transaction that records it, whichever process records it.
/// A connector stages rows and reads; only the service marks <c>applying</c>, calls the engine and settles. The read sets
/// (<see cref="MemoryOpReads"/>) and the changes since a cursor (<see cref="MemoryOpChanges"/>) are types of their own, and their doors here are one line each.
/// </summary>
public sealed class MemoryOpStore
{
    internal const string Columns =
        "id, repository, kind, state, at, session, workspace, quest, adapter, harness, role, role_revision, models, text, target, revision, source, "
        + "relation, approver, holds, entry, applied, settled, recheck, predecessors, carried, answer, told, began";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ConnectionGate _db;
    private readonly MemoryOpReads _reads;
    private readonly MemoryOpChanges _changes;

    internal MemoryOpStore(ConnectionGate db)
    {
        _db = db;
        _reads = new(db);
        _changes = new(db, ReadAllAsync, () => BetweenReads);
    }

    public static async Task<MemoryOpStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        var store = new MemoryOpStore(ConnectionGate.For(connection));
        await store._db.RunAsync(() => store.EnsureAsync(ct), ct).ConfigureAwait(false);
        return store;
    }

    internal async Task EnsureAsync(CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS memory_ops (
              id            TEXT PRIMARY KEY NOT NULL,
              repository    TEXT NOT NULL,
              kind          TEXT NOT NULL,
              state         TEXT NOT NULL,
              at            TEXT NOT NULL,
              session       TEXT NULL,
              workspace     TEXT NULL,
              quest         TEXT NULL,
              adapter       TEXT NULL,
              harness       TEXT NULL,
              role          TEXT NULL,
              role_revision TEXT NULL,
              models        TEXT NULL,
              text          TEXT NULL,
              target        TEXT NULL,
              revision      INTEGER NULL,
              source        TEXT NULL,
              relation      TEXT NULL,
              approver      TEXT NULL,
              holds         TEXT NULL,
              entry         TEXT NULL,
              applied       INTEGER NULL,
              settled       TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS memory_ops_session ON memory_ops (session, state);
            CREATE INDEX IF NOT EXISTS memory_ops_state ON memory_ops (state);
            CREATE INDEX IF NOT EXISTS memory_ops_target ON memory_ops (repository, target, state);
            {MemoryOpReads.Schema}
            {EventJournal.MemorySchema}
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        // KNOWCENTRAL2d: the records a write cites; a row from before reads none.
        await SchemaColumns.EnsureAsync(_db, "memory_ops", "recheck", "recheck TEXT NULL", ct).ConfigureAwait(false);
        // KNOWCENTRAL2h: a replacement's predecessors as JSON; a row from before replaces none.
        await SchemaColumns.EnsureAsync(_db, "memory_ops", "predecessors", "predecessors TEXT NULL", ct).ConfigureAwait(false);
        // KNOWCENTRAL2e: a proposal's quest, the owner's answer and when its proposer was told; a row from before reads none.
        await SchemaColumns.EnsureAsync(_db, "memory_ops", "carried", "carried TEXT NULL", ct).ConfigureAwait(false);
        await SchemaColumns.EnsureAsync(_db, "memory_ops", "answer", "answer TEXT NULL", ct).ConfigureAwait(false);
        await SchemaColumns.EnsureAsync(_db, "memory_ops", "told", "told TEXT NULL", ct).ConfigureAwait(false);
        // KNOWCENTRAL2j: when the row began applying, against which a receipt's expiry is judged; a row from before reads none.
        await SchemaColumns.EnsureAsync(_db, "memory_ops", "began", "began TEXT NULL", ct).ConfigureAwait(false);
    }

    /// <summary>An operation its session made, kept <c>staged</c> until the session's commit point; nothing staged applies by itself.</summary>
    /// <exception cref="ArgumentException">A kind this log does not keep, or no repository.</exception>
    public Task<MemoryOp> StageAsync(string repository, MemoryChange change, MemoryAuthor by, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(repository)) throw new ArgumentException("An operation names the repository whose memory it changes.", nameof(repository));
        if (!MemoryOpKind.All.Contains(change.Kind)) throw new ArgumentException($"`{change.Kind}` is no operation on a memory.", nameof(change));
        var op = new MemoryOp(NewId(), repository, MemoryOpState.Staged, change, by, by.At);
        return _db.RunAsync(async () =>
        {
            await InsertAsync(op, ct).ConfigureAwait(false);
            return op;
        }, ct);
    }

    /// <summary>The operation <paramref name="id"/>; null where none is kept.</summary>
    public Task<MemoryOp?> FindAsync(string id, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"SELECT {Columns} FROM memory_ops WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>The operations in <paramref name="state"/>, in the order they were kept.</summary>
    public Task<IReadOnlyList<MemoryOp>> ListAsync(string state, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<MemoryOp>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"SELECT {Columns} FROM memory_ops WHERE state = $state ORDER BY rowid";
        command.Parameters.AddWithValue("$state", state);
        return await ReadAllAsync(command, ct).ConfigureAwait(false);
    }, ct);

    /// <summary>
    /// The staged rows <paramref name="session"/> may see (§1): its own, and those of the sessions it continues by <c>From</c>; a session it
    /// continues sees none of its own.
    /// </summary>
    public Task<IReadOnlyList<MemoryOp>> StagedForAsync(string session, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<MemoryOp>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"""
            WITH RECURSIVE chain(id) AS (
              SELECT $session
              UNION SELECT s.from_session FROM sessions s JOIN chain c ON s.id = c.id WHERE s.from_session IS NOT NULL)
            SELECT {Columns} FROM memory_ops WHERE state = 'staged' AND session IN (SELECT id FROM chain) ORDER BY rowid
            """;
        command.Parameters.AddWithValue("$session", session);
        return await ReadAllAsync(command, ct).ConfigureAwait(false);
    }, ct);

    /// <summary>
    /// Whether <paramref name="session"/> is <paramref name="proposer"/> or of its lineage (§4): a session it continues or serves, as a
    /// helper or a partner serves, followed up to the root. A subagent calls as its session, so it is the proposer here.
    /// </summary>
    public Task<bool> InLineageAsync(string session, string proposer, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            WITH RECURSIVE up(id) AS (
              SELECT $session
              UNION SELECT s.from_session FROM sessions s JOIN up ON s.id = up.id WHERE s.from_session IS NOT NULL
              UNION SELECT s.serves FROM sessions s JOIN up ON s.id = up.id WHERE s.serves IS NOT NULL)
            SELECT EXISTS (SELECT 1 FROM up WHERE id = $proposer)
            """;
        command.Parameters.AddWithValue("$session", session);
        command.Parameters.AddWithValue("$proposer", proposer);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture) == 1;
    }, ct);

    /// <summary>
    /// The person's Keep or Drop of an ended session's staged rows and those of the sessions it continues (§1): kept, they move as its
    /// commit point would have moved them, with no turn lines, so their models read <c>unknown</c>; dropped, they never apply. Null where
    /// the session is still running or no record names it.
    /// </summary>
    public Task<int?> KeepStagedAsync(string session, bool keep, DateTimeOffset now, CancellationToken ct = default) =>
        _db.InTransactionAsync<int?>(async (_, inside) =>
        {
            await using (var state = _db.Command())
            {
                state.CommandText = "SELECT state FROM sessions WHERE id = $session";
                state.Parameters.AddWithValue("$session", session);
                if (await state.ExecuteScalarAsync(inside).ConfigureAwait(false) is not string held || Running.Contains(held)) return null;
            }

            if (keep) return await ReleaseAsync([session], predecessors: true, turns: null, inside).ConfigureAwait(false);
            await using var drop = _db.Command();
            drop.CommandText = """
                WITH RECURSIVE chain(id) AS (
                  SELECT $session
                  UNION SELECT s.from_session FROM sessions s JOIN chain c ON s.id = c.id WHERE s.from_session IS NOT NULL)
                UPDATE memory_ops SET state = 'dropped', settled = $now WHERE state = 'staged' AND session IN (SELECT id FROM chain)
                """;
            drop.Parameters.AddWithValue("$session", session);
            drop.Parameters.AddWithValue("$now", Moments.Write(now));
            return await drop.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
        }, ct);

    /// <summary>The states of a session whose own commit point is still to come.</summary>
    private static readonly string[] Running =
        [nameof(SessionState.Queued), nameof(SessionState.Starting), nameof(SessionState.Working), nameof(SessionState.AwaitingPerson)];

    /// <summary>
    /// What waits on the person (KNOWCENTRAL2e), a row per session, oldest first: ends and replacements awaiting their approver, and the staged
    /// rows of a session no longer running, which the person keeps or drops. A running session's staged rows wait for its own commit point.
    /// </summary>
    public Task<IReadOnlyList<MemoryWait>> WaitsAsync(CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<MemoryWait>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT m.session, m.state, m.kind, MIN(m.at), COUNT(*), MIN(m.rowid)
            FROM memory_ops m JOIN sessions s ON s.id = m.session
            WHERE (m.state = 'awaiting' AND m.kind IN ('end', 'supersede'))
               OR (m.state = 'staged' AND s.state NOT IN (SELECT value FROM json_each($running)))
            GROUP BY m.session, m.state, m.kind
            ORDER BY MIN(m.rowid)
            """;
        command.Parameters.AddWithValue("$running", JsonSerializer.Serialize(Running));
        var found = new List<(string Session, MemoryWait Wait)>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var (state, kind, count) = (reader.GetString(1), reader.GetString(2), (int)reader.GetInt64(4));
            var since = Moments.ReadRequired(reader.GetString(3));
            var wait = state == MemoryOpState.Staged ? new MemoryWait(reader.GetString(0), 0, 0, count, since)
                : kind == MemoryOpKind.Supersede ? new MemoryWait(reader.GetString(0), 0, count, 0, since)
                : new MemoryWait(reader.GetString(0), count, 0, 0, since);
            found.Add((wait.Session, wait));
        }

        return [.. found.GroupBy(each => each.Session).Select(group => new MemoryWait(
            group.Key, group.Sum(each => each.Wait.Ends), group.Sum(each => each.Wait.Replacements), group.Sum(each => each.Wait.Staged),
            group.Min(each => each.Wait.Since)))];
    }, ct);

    /// <summary>Every row <paramref name="session"/> made, in the order they were kept.</summary>
    public Task<IReadOnlyList<MemoryOp>> OfSessionAsync(string session, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<MemoryOp>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"SELECT {Columns} FROM memory_ops WHERE session = $session ORDER BY rowid";
        command.Parameters.AddWithValue("$session", session);
        return await ReadAllAsync(command, ct).ConfigureAwait(false);
    }, ct);

    /// <summary>
    /// The commit point's move (§1), inside the transaction that records it: the staged rows of <paramref name="sessions"/>, and with
    /// <paramref name="predecessors"/> those of the sessions each continues, become <c>ready</c>, an end or a replacement <c>awaiting</c>, each naming the
    /// models of the turn that holds its instant.
    /// </summary>
    /// <returns>How many rows moved.</returns>
    internal async Task<int> ReleaseAsync(
        IReadOnlyCollection<string> sessions, bool predecessors, IReadOnlyDictionary<string, MemoryTurns>? turns, CancellationToken inside)
    {
        var staged = new List<(string Id, string Session, string Kind, DateTimeOffset At)>();
        await using (var read = _db.Command())
        {
            read.CommandText = predecessors
                ? """
                  WITH RECURSIVE chain(id) AS (
                    SELECT value FROM json_each($sessions)
                    UNION SELECT s.from_session FROM sessions s JOIN chain c ON s.id = c.id WHERE s.from_session IS NOT NULL)
                  SELECT id, session, kind, at FROM memory_ops WHERE state = 'staged' AND session IN (SELECT id FROM chain) ORDER BY rowid
                  """
                : "SELECT id, session, kind, at FROM memory_ops WHERE state = 'staged' AND session IN (SELECT value FROM json_each($sessions)) ORDER BY rowid";
            read.Parameters.AddWithValue("$sessions", JsonSerializer.Serialize(sessions));
            await using var reader = await read.ExecuteReaderAsync(inside).ConfigureAwait(false);
            while (await reader.ReadAsync(inside).ConfigureAwait(false))
            {
                staged.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), Moments.ReadRequired(reader.GetString(3))));
            }
        }

        foreach (var (id, session, kind, at) in staged)
        {
            // A model named where the session ended (StampAsync) stands: the post that releases a tree's rows hands no turn lines.
            await using var move = _db.Command();
            move.CommandText = "UPDATE memory_ops SET state = $state, models = COALESCE(models, $models) WHERE id = $id AND state = 'staged'";
            move.Parameters.AddWithValue("$id", id);
            move.Parameters.AddWithValue("$state", MemoryOpKind.Ends(kind) ? MemoryOpState.Awaiting : MemoryOpState.Ready);
            move.Parameters.AddWithValue("$models", MemoryModels.Of(turns?.GetValueOrDefault(session), at));
            await move.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
        }

        return staged.Count;
    }

    /// <summary>
    /// The models of <paramref name="session"/>'s staged rows, and those of the sessions it continues, named from the turn lines the driver
    /// handed as it ended (§1), inside the transaction that ends it; the rows stay staged for the commit point that releases them.
    /// </summary>
    internal async Task StampAsync(string session, IReadOnlyDictionary<string, MemoryTurns> turns, CancellationToken inside)
    {
        foreach (var op in await StagedForAsync(session, inside).ConfigureAwait(false))
        {
            await using var stamp = _db.Command();
            stamp.CommandText = "UPDATE memory_ops SET models = $models WHERE id = $id AND state = 'staged' AND models IS NULL";
            stamp.Parameters.AddWithValue("$id", op.Id);
            stamp.Parameters.AddWithValue("$models", MemoryModels.Of(turns.GetValueOrDefault(op.By?.Session ?? ""), op.At));
            await stamp.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// An awaiting end decided through its row (§3), as a go-ahead is kept: approved, it is <c>ready</c>; refused, <c>declined</c>. Null
    /// where no end awaits by that id.
    /// </summary>
    public Task<MemoryOp?> DecideAsync(string id, string approver, bool approve, DateTimeOffset now, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (_, inside) =>
        {
            await using (var decide = _db.Command())
            {
                // An awaiting proposal is its owner's to decide through its own operations, never an approver's (§4).
                decide.CommandText = """
                    UPDATE memory_ops SET state = $state, approver = $approver, settled = $settled
                    WHERE id = $id AND state = 'awaiting' AND kind IN ('end', 'supersede')
                    """;
                decide.Parameters.AddWithValue("$id", id);
                decide.Parameters.AddWithValue("$state", approve ? MemoryOpState.Ready : MemoryOpState.Declined);
                decide.Parameters.AddWithValue("$approver", approver);
                decide.Parameters.AddWithValue("$settled", approve ? DBNull.Value : Moments.Write(now));
                if (await decide.ExecuteNonQueryAsync(inside).ConfigureAwait(false) == 0) return null;
            }

            return await FindAsync(id, inside).ConfigureAwait(false);
        }, ct);

    /// <summary>
    /// The targets of rows not yet settled (awaiting, ready, applying, held), by repository and kind: what a sweep does not write again. A
    /// replacement's predecessors are each an end's.
    /// </summary>
    public Task<IReadOnlySet<(string Repository, string Target, string Kind)>> PendingAsync(CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlySet<(string, string, string)>>(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = """
                SELECT repository, target, kind FROM memory_ops
                WHERE state IN ('awaiting', 'ready', 'applying', 'held') AND target IS NOT NULL
                UNION
                SELECT o.repository, json_extract(p.value, '$.entry'), 'end' FROM memory_ops o, json_each(o.predecessors) p
                WHERE o.state IN ('awaiting', 'ready', 'applying', 'held') AND o.kind = 'supersede'
                """;
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var pending = new HashSet<(string, string, string)>();
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) pending.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            return pending;
        }, ct);

    /// <summary>
    /// A sweep's rechecks written as rows (§3): a Holds as a <c>ready</c> confirmation at the revision read, or, where an end or a
    /// replacement of the entry awaits its approver, kept on that row so the revision the approver read does not move; a Failed as an
    /// <c>awaiting</c> end.
    /// </summary>
    public Task SweepAsync(IReadOnlyList<MemoryRecheck> rechecks, DateTimeOffset now, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (_, inside) =>
        {
            foreach (var recheck in rechecks)
            {
                if (recheck.Holds)
                {
                    await using var awaiting = _db.Command();
                    awaiting.CommandText = """
                        UPDATE memory_ops SET holds = $now
                        WHERE state = 'awaiting' AND repository = $repository
                          AND ((kind = 'end' AND target = $entry)
                               OR (kind = 'supersede'
                                   AND EXISTS (SELECT 1 FROM json_each(predecessors) WHERE json_extract(value, '$.entry') = $entry)))
                        """;
                    awaiting.Parameters.AddWithValue("$now", Moments.Write(now));
                    awaiting.Parameters.AddWithValue("$repository", recheck.Repository);
                    awaiting.Parameters.AddWithValue("$entry", recheck.Entry);
                    if (await awaiting.ExecuteNonQueryAsync(inside).ConfigureAwait(false) > 0) continue;
                }

                var change = recheck.Holds
                    ? new MemoryChange(MemoryOpKind.Confirm, Target: recheck.Entry, Revision: recheck.Revision)
                    : new MemoryChange(MemoryOpKind.End, Text: recheck.Reason, Target: recheck.Entry, Revision: recheck.Revision);
                var state = recheck.Holds ? MemoryOpState.Ready : MemoryOpState.Awaiting;
                await InsertAsync(new MemoryOp(NewId(), recheck.Repository, state, change, By: null, now), inside).ConfigureAwait(false);
            }

            return true;
        }, ct);

    /// <summary><c>ready</c> to <c>applying</c>, in its own transaction before the engine is called; false where it is no longer ready.</summary>
    internal Task<bool> BeginAsync(string id, DateTimeOffset? now = null, CancellationToken ct = default) => _db.InTransactionAsync(async (_, inside) =>
    {
        await using var begin = _db.Command();
        begin.CommandText = "UPDATE memory_ops SET state = 'applying', began = $began WHERE id = $id AND state = 'ready'";
        begin.Parameters.AddWithValue("$id", id);
        begin.Parameters.AddWithValue("$began", Moments.Write(now ?? DateTimeOffset.UtcNow));
        return await begin.ExecuteNonQueryAsync(inside).ConfigureAwait(false) == 1;
    }, ct);

    /// <summary>
    /// An <c>applying</c> row's outcome, in a transaction after the engine answered or recovery read it; or a <c>held</c> one's, where a
    /// receipt answers it.
    /// </summary>
    internal Task SettleAsync(string id, MemoryOutcome outcome, DateTimeOffset now, CancellationToken ct = default, string from = MemoryOpState.Applying) =>
        _db.InTransactionAsync(async (_, inside) =>
        {
            await using var settle = _db.Command();
            settle.CommandText = """
                UPDATE memory_ops SET state = $state, entry = COALESCE($entry, entry), applied = $applied, settled = $now
                WHERE id = $id AND state = $from
                """;
            settle.Parameters.AddWithValue("$from", from);
            settle.Parameters.AddWithValue("$id", id);
            settle.Parameters.AddWithValue("$state", outcome.State);
            settle.Parameters.AddWithValue("$entry", Value(outcome.Entry));
            settle.Parameters.AddWithValue("$applied", outcome.Revision is { } revision ? revision : DBNull.Value);
            settle.Parameters.AddWithValue("$now", Moments.Write(now));
            var settled = await settle.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            if (settled == 1 && outcome is { State: MemoryOpState.Applied, Revision: { } moved })
            {
                await _reads.MovedAsync(id, outcome.Entry, moved, inside).ConfigureAwait(false);
            }

            return settled;
        }, ct);

    private async Task InsertAsync(MemoryOp op, CancellationToken ct)
    {
        await using var insert = _db.Command();
        insert.CommandText = $"""
            INSERT INTO memory_ops ({Columns})
            VALUES ($id, $repository, $kind, $state, $at, $session, $workspace, $quest, $adapter, $harness, $role, $roleRevision, $models, $text,
                    $target, $revision, $source, $relation, NULL, NULL, NULL, NULL, NULL, $recheck, $predecessors, $carried, NULL, NULL, NULL)
            """;
        insert.Parameters.AddWithValue("$recheck", Value(op.Change.Recheck));
        insert.Parameters.AddWithValue(
            "$predecessors", Value(op.Change.Predecessors is { Count: > 0 } replaced ? JsonSerializer.Serialize(replaced, Json) : null));
        insert.Parameters.AddWithValue("$carried", Value(op.Carried));
        insert.Parameters.AddWithValue("$id", op.Id);
        insert.Parameters.AddWithValue("$repository", op.Repository);
        insert.Parameters.AddWithValue("$kind", op.Change.Kind);
        insert.Parameters.AddWithValue("$state", op.State);
        insert.Parameters.AddWithValue("$at", Moments.Write(op.At));
        insert.Parameters.AddWithValue("$session", Value(op.By?.Session));
        insert.Parameters.AddWithValue("$workspace", Value(op.By?.Workspace));
        insert.Parameters.AddWithValue("$quest", Value(op.By?.Quest));
        insert.Parameters.AddWithValue("$adapter", Value(op.By?.Adapter));
        insert.Parameters.AddWithValue("$harness", Value(op.By?.Harness));
        insert.Parameters.AddWithValue("$role", Value(op.By?.Role));
        insert.Parameters.AddWithValue("$roleRevision", Value(op.By?.RoleRevision));
        insert.Parameters.AddWithValue("$models", Value(op.Models));
        insert.Parameters.AddWithValue("$text", Value(op.Change.Text));
        insert.Parameters.AddWithValue("$target", Value(op.Change.Target));
        insert.Parameters.AddWithValue("$revision", op.Change.Revision is { } revision ? revision : DBNull.Value);
        insert.Parameters.AddWithValue("$source", Value(op.Change.Source));
        insert.Parameters.AddWithValue("$relation", Value(op.Change.Relation));
        await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<MemoryOp>> ReadAllAsync(SqliteCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var ops = new List<MemoryOp>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) ops.Add(Read(reader));
        return ops;
    }

    private static MemoryOp Read(SqliteDataReader reader)
    {
        string? Text(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
        long? Number(int i) => reader.IsDBNull(i) ? null : reader.GetInt64(i);
        var at = Moments.ReadRequired(reader.GetString(4));
        var by = Text(5) is { } session
            ? new MemoryAuthor(session, Text(6) ?? Workspaces.Default, Text(7), Text(8) ?? "", Text(9), Text(10), Text(11), at)
            : null;
        return new MemoryOp(
            reader.GetString(0), reader.GetString(1), reader.GetString(3),
            new MemoryChange(
                reader.GetString(2), Text(13), Text(14), Number(15), Text(16), Text(17), Text(23),
                Text(24) is { } replaced ? JsonSerializer.Deserialize<List<MemoryPredecessor>>(replaced, Json) : null), by, at)
        {
            Models = Text(12),
            Approver = Text(18),
            Holds = Moments.Read(Text(19)),
            Entry = Text(20),
            Applied = Number(21),
            Settled = Moments.Read(Text(22)),
            Carried = Text(25),
            Answer = Text(26),
            Told = Moments.Read(Text(27)),
            Began = Moments.Read(Text(28)),
        };
    }

    private static object Value(string? text) => (object?)text ?? DBNull.Value;

    internal static string NewId() => Guid.NewGuid().ToString("N")[..16].ToLower(CultureInfo.InvariantCulture);

    /// <summary>
    /// A proposal published or joined on <paramref name="quest"/> (§4), kept <c>awaiting</c> its owner under <paramref name="id"/>, the id its
    /// quest's packet names. No commit point moves it and the applier never applies it.
    /// </summary>
    public Task<MemoryOp> ProposeAsync(string id, string repository, MemoryChange change, MemoryAuthor by, string quest, CancellationToken ct = default)
    {
        var op = new MemoryOp(id, repository, MemoryOpState.Awaiting, change, by, by.At) { Carried = quest };
        return _db.RunAsync(async () =>
        {
            await InsertAsync(op, ct).ConfigureAwait(false);
            return op;
        }, ct);
    }

    /// <summary>The turn to look for, join or publish a quest for proposals from one repository to another.</summary>
    internal async Task<IDisposable> ProposingAsync(string from, string owner, CancellationToken ct)
    {
        var gate = _proposing.GetOrAdd((from, owner), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        return new Turn(gate);
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string, string), SemaphoreSlim> _proposing = new();

    private sealed class Turn(SemaphoreSlim held) : IDisposable
    {
        public void Dispose() => held.Release();
    }

    /// <summary>Every proposal, or those in <paramref name="state"/>, in the order they were kept.</summary>
    public Task<IReadOnlyList<MemoryOp>> ProposalsAsync(string? state = null, CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyList<MemoryOp>>(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = $"SELECT {Columns} FROM memory_ops WHERE kind = 'propose' AND ($state IS NULL OR state = $state) ORDER BY rowid";
            command.Parameters.AddWithValue("$state", Value(state));
            return await ReadAllAsync(command, ct).ConfigureAwait(false);
        }, ct);

    /// <summary>The proposals <paramref name="quests"/> carry, in the order they were kept.</summary>
    public Task<IReadOnlyList<MemoryOp>> CarriedAsync(IReadOnlyCollection<string> quests, CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyList<MemoryOp>>(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = $"""
                SELECT {Columns} FROM memory_ops WHERE kind = 'propose' AND carried IN (SELECT value FROM json_each($quests)) ORDER BY rowid
                """;
            command.Parameters.AddWithValue("$quests", JsonSerializer.Serialize(quests));
            return await ReadAllAsync(command, ct).ConfigureAwait(false);
        }, ct);

    /// <summary>An awaiting proposal settled by its quest or its target (§4); false where it had already settled.</summary>
    internal Task<bool> SettleProposalAsync(string id, string state, string? answer, DateTimeOffset now, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (_, inside) =>
        {
            await using var settle = _db.Command();
            settle.CommandText = """
                UPDATE memory_ops SET state = $state, answer = $answer, settled = $now WHERE id = $id AND kind = 'propose' AND state = 'awaiting'
                """;
            settle.Parameters.AddWithValue("$id", id);
            settle.Parameters.AddWithValue("$state", state);
            settle.Parameters.AddWithValue("$answer", Value(answer));
            settle.Parameters.AddWithValue("$now", Moments.Write(now));
            return await settle.ExecuteNonQueryAsync(inside).ConfigureAwait(false) == 1;
        }, ct);

    /// <summary>
    /// The proposals of <paramref name="session"/>, and of the sessions it continues, that settled since its proposer was last told, each
    /// told once.
    /// </summary>
    public Task<IReadOnlyList<MemoryOp>> TellProposalsAsync(string session, DateTimeOffset now, CancellationToken ct = default) =>
        _db.InTransactionAsync<IReadOnlyList<MemoryOp>>(async (_, inside) =>
        {
            const string Chain = """
                WITH RECURSIVE chain(id) AS (
                  SELECT $session
                  UNION SELECT s.from_session FROM sessions s JOIN chain c ON s.id = c.id WHERE s.from_session IS NOT NULL)
                """;
            IReadOnlyList<MemoryOp> settled;
            await using (var read = _db.Command())
            {
                read.CommandText = $"""
                    {Chain}
                    SELECT {Columns} FROM memory_ops
                    WHERE kind = 'propose' AND state <> 'awaiting' AND told IS NULL AND session IN (SELECT id FROM chain) ORDER BY rowid
                    """;
                read.Parameters.AddWithValue("$session", session);
                settled = await ReadAllAsync(read, inside).ConfigureAwait(false);
            }

            foreach (var op in settled)
            {
                await using var told = _db.Command();
                told.CommandText = "UPDATE memory_ops SET told = $now WHERE id = $id AND told IS NULL";
                told.Parameters.AddWithValue("$id", op.Id);
                told.Parameters.AddWithValue("$now", Moments.Write(now));
                await told.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            return settled;
        }, ct);

    /// <summary>Run between a bootstrap's two reads; a test's, to commit from another connection there.</summary>
    internal Func<Task>? BetweenReads { get; set; }

    /// <summary>The rows of <paramref name="repositories"/> still to settle and the journal's newest position, read in one transaction.</summary>
    public Task<MemoryBootstrap> BootstrapAsync(IReadOnlyCollection<string> repositories, CancellationToken ct = default) =>
        _changes.BootstrapAsync(repositories, ct);

    /// <summary>The journal's newest position over <paramref name="repositories"/>, as a cursor <see cref="ChangesAsync"/> reads past.</summary>
    public Task<MemoryCursor> CursorAsync(IReadOnlyCollection<string> repositories, CancellationToken ct = default) =>
        _changes.CursorAsync(repositories, ct);

    /// <summary>The references whose rows moved past <paramref name="cursor"/> in <paramref name="repositories"/>; a re-validation where the cursor cannot be read past.</summary>
    public Task<MemoryChanges> ChangesAsync(MemoryCursor cursor, IReadOnlyCollection<string> repositories, CancellationToken ct = default) =>
        _changes.ChangesAsync(cursor, repositories, ct);

    /// <summary>The newest references one session's read set keeps (the memory coordinator design §3).</summary>
    public const int ReadsPerSession = MemoryOpReads.PerSession;

    /// <summary>The newest references every session's read sets keep together.</summary>
    public const int ReadsInAll = MemoryOpReads.InAll;

    /// <summary>What <paramref name="session"/> read; a reference read again is kept at its new revision, as yet unmoved.</summary>
    public Task KeepReadsAsync(string session, IReadOnlyList<MemoryRead> reads, CancellationToken ct = default) =>
        _reads.KeepAsync(session, reads, ct);

    /// <summary>The references the line <paramref name="session"/> ends read that moved since, each told once.</summary>
    public Task<IReadOnlyList<MemoryReference>> TellAsync(string session, CancellationToken ct = default) => _reads.TellAsync(session, ct);

    /// <summary>What the line <paramref name="session"/> ends read, each reference once at its newest read, newest first, with the session that read it.</summary>
    public Task<IReadOnlyList<(string Session, MemoryRead Read)>> ReadsOfLineAsync(string session, CancellationToken ct = default) =>
        _reads.OfLineAsync(session, ct);

    /// <summary>The re-check text the applied write or replacement that wrote <paramref name="entry"/> kept: the records it cites; null for none.</summary>
    public Task<string?> CitedAsync(string repository, string entry, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var read = _db.Command();
        read.CommandText = """
            SELECT recheck FROM memory_ops
            WHERE repository = $repository AND entry = $entry AND kind IN ('write', 'supersede') AND state = 'applied' AND recheck IS NOT NULL
            ORDER BY rowid DESC LIMIT 1
            """;
        read.Parameters.AddWithValue("$repository", repository);
        read.Parameters.AddWithValue("$entry", entry);
        return await read.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
    }, ct);
}
