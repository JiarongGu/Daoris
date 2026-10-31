using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>What an operation does to a memory (the memory coordinator design §3).</summary>
public static class MemoryOpKind
{
    public const string Write = "write";
    public const string Relate = "relate";
    public const string End = "end";
    public const string Confirm = "confirm";
    public const string Propose = "propose";

    internal static readonly string[] All = [Write, Relate, End, Confirm, Propose];
}

/// <summary>
/// Where an operation stands (§3): <c>staged</c> by its session; released at the session's commit point to <c>ready</c>, an end to
/// <c>awaiting</c> its approver; <c>applying</c> while the service calls the engine; then <c>applied</c>, <c>stale</c> where its guard no
/// longer held, or <c>held</c> where nothing says whether it ran. <c>dropped</c> and <c>declined</c> never apply.
/// </summary>
public static class MemoryOpState
{
    public const string Staged = "staged";
    public const string Awaiting = "awaiting";
    public const string Ready = "ready";
    public const string Applying = "applying";
    public const string Applied = "applied";
    public const string Dropped = "dropped";
    public const string Declined = "declined";
    public const string Stale = "stale";
    public const string Held = "held";
}

/// <summary>
/// What an operation asks: a write's text, or an end's reason; the entry it targets and the revision its session read; a relation's
/// source and kind. A write naming a relation and a target relates the entry it writes; its <see cref="Recheck"/> is the records it cites
/// (<see cref="MemoryCitations"/>), kept on the entry for the sweep.
/// </summary>
public sealed record MemoryChange(
    string Kind, string? Text = null, string? Target = null, long? Revision = null, string? Source = null, string? Relation = null,
    string? Recheck = null);

/// <summary>A recheck the sweep read of a cited entry (§3): it holds at <paramref name="Revision"/>, or failed for <paramref name="Reason"/>.</summary>
public sealed record MemoryRecheck(string Repository, string Entry, long Revision, bool Holds, string? Reason = null);

/// <summary>Who made an operation, from the caller's record at the call (§1), never from a tool's arguments; and the call's instant.</summary>
public sealed record MemoryAuthor(
    string Session, string Workspace, string? Quest, string Adapter, string? Harness, string? Role, string? RoleRevision, DateTimeOffset At);

/// <summary>
/// One row of <c>memory_ops</c>, the log of record of a repository's memory (§3). <see cref="By"/> is null on a row the service's sweep wrote.
/// </summary>
public sealed record MemoryOp(string Id, string Repository, string State, MemoryChange Change, MemoryAuthor? By, DateTimeOffset At)
{
    /// <summary>The models of the turn that made it, named at its commit point; null before.</summary>
    public string? Models { get; init; }

    /// <summary>Who decided an awaiting end.</summary>
    public string? Approver { get; init; }

    /// <summary>When a recheck held the target while this end awaited.</summary>
    public DateTimeOffset? Holds { get; init; }

    /// <summary>The entry the engine answered: a write's new or repeated entry, an end's or confirmation's target.</summary>
    public string? Entry { get; init; }

    /// <summary>The revision the engine answered.</summary>
    public long? Applied { get; init; }

    /// <summary>When the service settled it.</summary>
    public DateTimeOffset? Settled { get; init; }
}

/// <summary>
/// The memory coordinator's log of record (KNOWCENTRAL2c; the design §3): <c>memory_ops</c> in <c>knowledge.db</c>, beside the sessions and
/// the finished trees' records, so a commit point moves its session's rows in the transaction that records it, whichever process records it.
/// A connector stages rows and reads; only the service marks <c>applying</c>, calls the engine and settles.
/// </summary>
public sealed partial class MemoryOpStore
{
    private const string Columns =
        "id, repository, kind, state, at, session, workspace, quest, adapter, harness, role, role_revision, models, text, target, revision, source, "
        + "relation, approver, holds, entry, applied, settled, recheck";

    private readonly ConnectionGate _db;

    internal MemoryOpStore(ConnectionGate db) => _db = db;

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
            {ReadSetSchema}
            {EventJournal.MemorySchema}
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        // KNOWCENTRAL2d: the records a write cites; a row from before reads none.
        await SchemaColumns.EnsureAsync(_db, "memory_ops", "recheck", "recheck TEXT NULL", ct).ConfigureAwait(false);
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
    /// <paramref name="predecessors"/> those of the sessions each continues, become <c>ready</c>, an end <c>awaiting</c>, each naming the
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
            move.Parameters.AddWithValue("$state", kind == MemoryOpKind.End ? MemoryOpState.Awaiting : MemoryOpState.Ready);
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
                decide.CommandText = "UPDATE memory_ops SET state = $state, approver = $approver, settled = $settled WHERE id = $id AND state = 'awaiting'";
                decide.Parameters.AddWithValue("$id", id);
                decide.Parameters.AddWithValue("$state", approve ? MemoryOpState.Ready : MemoryOpState.Declined);
                decide.Parameters.AddWithValue("$approver", approver);
                decide.Parameters.AddWithValue("$settled", approve ? DBNull.Value : Moments.Write(now));
                if (await decide.ExecuteNonQueryAsync(inside).ConfigureAwait(false) == 0) return null;
            }

            return await FindAsync(id, inside).ConfigureAwait(false);
        }, ct);

    /// <summary>The targets of rows not yet settled (awaiting, ready, applying, held), by repository and kind: what a sweep does not write again.</summary>
    public Task<IReadOnlySet<(string Repository, string Target, string Kind)>> PendingAsync(CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlySet<(string, string, string)>>(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = """
                SELECT repository, target, kind FROM memory_ops
                WHERE state IN ('awaiting', 'ready', 'applying', 'held') AND target IS NOT NULL
                """;
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var pending = new HashSet<(string, string, string)>();
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) pending.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            return pending;
        }, ct);

    /// <summary>
    /// A sweep's rechecks written as rows (§3): a Holds as a <c>ready</c> confirmation at the revision read, or, where an end of the entry
    /// awaits its approver, kept on that end's row so the revision the approver read does not move; a Failed as an <c>awaiting</c> end.
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
                        WHERE state = 'awaiting' AND kind = 'end' AND repository = $repository AND target = $entry
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
    internal Task<bool> BeginAsync(string id, CancellationToken ct = default) => _db.InTransactionAsync(async (_, inside) =>
    {
        await using var begin = _db.Command();
        begin.CommandText = "UPDATE memory_ops SET state = 'applying' WHERE id = $id AND state = 'ready'";
        begin.Parameters.AddWithValue("$id", id);
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
                await MovedAsync(id, outcome.Entry, moved, inside).ConfigureAwait(false);
            }

            return settled;
        }, ct);

    private async Task InsertAsync(MemoryOp op, CancellationToken ct)
    {
        await using var insert = _db.Command();
        insert.CommandText = $"""
            INSERT INTO memory_ops ({Columns})
            VALUES ($id, $repository, $kind, $state, $at, $session, $workspace, $quest, $adapter, $harness, $role, $roleRevision, $models, $text,
                    $target, $revision, $source, $relation, NULL, NULL, NULL, NULL, NULL, $recheck)
            """;
        insert.Parameters.AddWithValue("$recheck", Value(op.Change.Recheck));
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
            new MemoryChange(reader.GetString(2), Text(13), Text(14), Number(15), Text(16), Text(17), Text(23)), by, at)
        {
            Models = Text(12),
            Approver = Text(18),
            Holds = Moments.Read(Text(19)),
            Entry = Text(20),
            Applied = Number(21),
            Settled = Moments.Read(Text(22)),
        };
    }

    private static object Value(string? text) => (object?)text ?? DBNull.Value;

    private static string NewId() => Guid.NewGuid().ToString("N")[..16].ToLower(CultureInfo.InvariantCulture);

    /// <summary>Run between a bootstrap's two reads; a test's, to commit from another connection there.</summary>
    internal Func<Task>? BetweenReads { get; set; }

    /// <summary>
    /// The rows of <paramref name="repositories"/> still to settle (awaiting, ready, applying, held) and the journal's newest position, read in
    /// one transaction, so a row another connection commits is in the rows or past the cursor.
    /// </summary>
    public Task<MemoryBootstrap> BootstrapAsync(IReadOnlyCollection<string> repositories, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (_, inside) =>
        {
            IReadOnlyList<MemoryOp> unsettled;
            await using (var read = _db.Command())
            {
                read.CommandText = $"""
                    SELECT {Columns} FROM memory_ops
                    WHERE state IN ('awaiting', 'ready', 'applying', 'held') AND repository IN (SELECT value FROM json_each($scope))
                    ORDER BY rowid
                    """;
                read.Parameters.AddWithValue("$scope", JsonSerializer.Serialize(repositories));
                unsettled = await ReadAllAsync(read, inside).ConfigureAwait(false);
            }

            if (BetweenReads is { } between) await between().ConfigureAwait(false);
            var (store, _, newest) = await JournalAsync(inside).ConfigureAwait(false);
            return new MemoryBootstrap(new(store, newest, Scope(repositories)), unsettled);
        }, ct, deferred: true);

    /// <summary>
    /// The references whose rows moved past <paramref name="cursor"/> in <paramref name="repositories"/>; a re-validation where the cursor is
    /// another store's, below the journal's floor, or of another scope.
    /// </summary>
    public Task<MemoryChanges> ChangesAsync(MemoryCursor cursor, IReadOnlyCollection<string> repositories, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (_, inside) =>
        {
            var (store, floor, newest) = await JournalAsync(inside).ConfigureAwait(false);
            if (!string.Equals(cursor.Scope, Scope(repositories), StringComparison.Ordinal) || !string.Equals(cursor.Store, store, StringComparison.Ordinal)
                || floor > cursor.Position + 1 || newest < cursor.Position)
            {
                return new MemoryChanges(true, cursor, []);
            }

            await using var read = _db.Command();
            read.CommandText = """
                SELECT o.repository, j.entry FROM event_journal j JOIN memory_ops o ON o.id = j.record
                WHERE j.family = 'memory' AND j.position > $since AND j.position <= $newest AND j.entry IS NOT NULL
                  AND o.repository IN (SELECT value FROM json_each($scope))
                GROUP BY o.repository, j.entry ORDER BY min(j.position)
                """;
            read.Parameters.AddWithValue("$since", cursor.Position);
            read.Parameters.AddWithValue("$newest", newest);
            read.Parameters.AddWithValue("$scope", JsonSerializer.Serialize(repositories));
            var changed = new List<MemoryReference>();
            await using var reader = await read.ExecuteReaderAsync(inside).ConfigureAwait(false);
            while (await reader.ReadAsync(inside).ConfigureAwait(false)) changed.Add(new(reader.GetString(0), reader.GetString(1)));
            return new MemoryChanges(false, cursor with { Position = newest }, changed);
        }, ct, deferred: true);

    /// <summary>The journal's store, the lowest position it keeps (one past the newest where it keeps none) and its newest.</summary>
    private async Task<(string Store, long Floor, long Newest)> JournalAsync(CancellationToken inside)
    {
        await using var read = _db.Command();
        read.CommandText = """
            SELECT (SELECT id FROM store_identity WHERE one = 1),
                   COALESCE((SELECT seq FROM sqlite_sequence WHERE name = 'event_journal'), 0),
                   (SELECT min(position) FROM event_journal)
            """;
        await using var reader = await read.ExecuteReaderAsync(inside).ConfigureAwait(false);
        await reader.ReadAsync(inside).ConfigureAwait(false);
        var newest = reader.GetInt64(1);
        return (reader.IsDBNull(0) ? "" : reader.GetString(0), reader.IsDBNull(2) ? newest + 1 : reader.GetInt64(2), newest);
    }

    private static string Scope(IReadOnlyCollection<string> repositories) =>
        string.Join(',', repositories.Order(StringComparer.Ordinal).Select(name => name.Length.ToString(CultureInfo.InvariantCulture) + ":" + name));

    /// <summary>The newest references one session's read set keeps (the memory coordinator design §3).</summary>
    public const int ReadsPerSession = 256;

    /// <summary>The newest references every session's read sets keep together.</summary>
    public const int ReadsInAll = 65_536;

    private const string ReadSetSchema = """
        CREATE TABLE IF NOT EXISTS memory_reads (
          seq        INTEGER PRIMARY KEY AUTOINCREMENT,
          session    TEXT NOT NULL,
          repository TEXT NOT NULL,
          entry      TEXT NOT NULL,
          revision   INTEGER NOT NULL,
          moved      INTEGER NOT NULL DEFAULT 0,
          told       INTEGER NOT NULL DEFAULT 0,
          UNIQUE (session, repository, entry)
        );
        CREATE INDEX IF NOT EXISTS memory_reads_entry ON memory_reads (repository, entry);
        """;

    /// <summary>What <paramref name="session"/> read; a reference read again is kept at its new revision, as yet unmoved.</summary>
    public Task KeepReadsAsync(string session, IReadOnlyList<MemoryRead> reads, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (_, inside) =>
        {
            foreach (var read in reads)
            {
                await using var keep = _db.Command();
                keep.CommandText = "INSERT OR REPLACE INTO memory_reads (session, repository, entry, revision) VALUES ($session, $repository, $entry, $revision)";
                keep.Parameters.AddWithValue("$session", session);
                keep.Parameters.AddWithValue("$repository", read.Reference.Repository);
                keep.Parameters.AddWithValue("$entry", read.Reference.Entry);
                keep.Parameters.AddWithValue("$revision", read.Revision);
                await keep.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            await using var trim = _db.Command();
            trim.CommandText = $"""
                DELETE FROM memory_reads WHERE session = $session
                  AND seq < (SELECT min(seq) FROM (SELECT seq FROM memory_reads WHERE session = $session ORDER BY seq DESC LIMIT {ReadsPerSession}));
                DELETE FROM memory_reads WHERE seq < (SELECT min(seq) FROM (SELECT seq FROM memory_reads ORDER BY seq DESC LIMIT {ReadsInAll}));
                """;
            trim.Parameters.AddWithValue("$session", session);
            return await trim.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
        }, ct);

    /// <summary>The references <paramref name="session"/> read that moved since, each told once.</summary>
    public Task<IReadOnlyList<MemoryReference>> TellAsync(string session, CancellationToken ct = default) =>
        _db.InTransactionAsync<IReadOnlyList<MemoryReference>>(async (_, inside) =>
        {
            var moved = new List<MemoryReference>();
            await using (var read = _db.Command())
            {
                read.CommandText = "SELECT repository, entry FROM memory_reads WHERE session = $session AND moved = 1 AND told = 0 ORDER BY seq";
                read.Parameters.AddWithValue("$session", session);
                await using var reader = await read.ExecuteReaderAsync(inside).ConfigureAwait(false);
                while (await reader.ReadAsync(inside).ConfigureAwait(false)) moved.Add(new(reader.GetString(0), reader.GetString(1)));
            }

            await using var told = _db.Command();
            told.CommandText = "UPDATE memory_reads SET told = 1 WHERE session = $session AND moved = 1 AND told = 0";
            told.Parameters.AddWithValue("$session", session);
            await told.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            return moved;
        }, ct);

    /// <summary>Marks every read of the entry an applied row moved past the revision it was read at, in the settle's transaction.</summary>
    private async Task MovedAsync(string id, string? entry, long revision, CancellationToken inside)
    {
        await using var mark = _db.Command();
        mark.CommandText = """
            UPDATE memory_reads SET moved = 1
            WHERE moved = 0 AND revision < $revision
              AND repository = (SELECT repository FROM memory_ops WHERE id = $id)
              AND entry = COALESCE($entry, (SELECT COALESCE(entry, target) FROM memory_ops WHERE id = $id))
            """;
        mark.Parameters.AddWithValue("$id", id);
        mark.Parameters.AddWithValue("$entry", (object?)entry ?? DBNull.Value);
        mark.Parameters.AddWithValue("$revision", revision);
        await mark.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
    }
}
