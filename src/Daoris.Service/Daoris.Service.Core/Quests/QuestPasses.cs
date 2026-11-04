using Microsoft.Data.Sqlite;
using static Daoris.Knowledge.QuestRecordCodec;

namespace Daoris.Knowledge;

/// <summary>
/// How a circle's sync passes ended and what the remote's handshake said (FABRIC-CLOSE1i): the passes a machine records, and the machines a
/// remote has heard declare that they close, retire or leave undeclared.
/// </summary>
internal sealed class QuestPasses(QuestStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>
    /// How a circle's pass ended (SYNC6a). A pass that reached the remote moves <see cref="QuestStanding.Synced"/>;
    /// one that hit a wall keeps it and names the wall, because when the circle last synced and why
    /// the last try did not are two facts.
    /// </summary>
    public Task RecordPassAsync(string workspace, QuestSyncReport report, DateTimeOffset at, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (transaction, inside) =>
        {
            await using var command = _db.Command();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO quest_passes (workspace, synced, tried, behind, problem, closure)
                VALUES ($workspace, $synced, $tried, $behind, $problem, $closure)
                ON CONFLICT (workspace) DO UPDATE SET
                  synced = COALESCE($synced, synced), tried = $tried, behind = $behind, problem = $problem, closure = $closure
                """;
            var circle = Workspaces.Normalize(workspace);
            command.Parameters.AddWithValue("$workspace", circle);
            command.Parameters.AddWithValue("$synced", report.Problem is null ? Moments.Write(at) : DBNull.Value);
            command.Parameters.AddWithValue("$tried", Moments.Write(at));
            // A list of quest ids, kept the way links are: a plain array of strings.
            command.Parameters.AddWithValue("$behind", LinksJson(report.Behind));
            command.Parameters.AddWithValue("$problem", (object?)report.Problem ?? DBNull.Value);
            // FABRIC-CLOSE1i: the undeclared machines, a plain array of ids; none where the pass heard no handshake, a wall's included.
            command.Parameters.AddWithValue("$closure", report.Closure is { } closure ? LinksJson(closure.Undeclared) : DBNull.Value);
            var written = await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            Heard(circle, report.Closure);
            return written;
        }, ct);

    /// <summary>
    /// The remote's handshake as <paramref name="workspace"/>'s last pass heard it (FABRIC-CLOSE1i): null where no pass has, a wall, or a
    /// remote before it. Read without the store's gate, so a take's own write can ask it: kept beside the row its pass recorded.
    /// </summary>
    public QuestClosure? ClosureOf(string workspace) =>
        _closures.TryGetValue(Workspaces.Normalize(workspace), out var closure) ? closure : null;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, QuestClosure> _closures = new(StringComparer.OrdinalIgnoreCase);

    private void Heard(string circle, QuestClosure? closure)
    {
        if (closure is null) _closures.TryRemove(circle, out _);
        else _closures[circle] = closure;
    }

    /// <summary>Each circle's handshake as its last pass recorded it (FABRIC-CLOSE1i), read as the store opens.</summary>
    internal async Task LoadClosuresAsync(CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT workspace, closure FROM quest_passes WHERE closure IS NOT NULL";
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            Heard(reader.GetString(0), new QuestClosure(ReadLinks(reader.GetString(1))));
        }
    }

    /// <summary>
    /// A remote's half of the workspace's handshake (FABRIC-CLOSE1i): what a push said of its machine. A push declaring
    /// <paramref name="declares"/> marks that machine declared; one declaring none, as a build before it pushes, marks every machine its
    /// operations name undeclared, so a machine run back to an older build is heard as one. A push declaring one machine says nothing
    /// of another its operations name. A machine it marks is counted again where a person had retired it (FABRIC-CLOSE1i2).
    /// </summary>
    public Task DeclaredAsync(string? declares, IReadOnlyList<QuestOperation> pushed, DateTimeOffset at, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (transaction, inside) =>
        {
            List<(string Machine, bool Closure)> marked = declares is { Length: > 0 } declared
                ? [(declared, true)]
                : [.. pushed.Select(o => o.Machine).Where(m => m.Length > 0).Distinct(StringComparer.Ordinal).Select(m => (m, false))];
            foreach (var (machine, closure) in marked)
            {
                await using var command = _db.Command();
                command.Transaction = transaction;
                command.CommandText = """
                    DELETE FROM quest_retired WHERE machine = $machine;
                    INSERT INTO quest_declared (machine, closure, at) VALUES ($machine, $closure, $at)
                    ON CONFLICT (machine) DO UPDATE SET closure = excluded.closure, at = excluded.at
                    """;
                command.Parameters.AddWithValue("$machine", machine);
                command.Parameters.AddWithValue("$closure", closure ? 1 : 0);
                command.Parameters.AddWithValue("$at", Moments.Write(at));
                await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            return marked.Count;
        }, ct);

    /// <summary>
    /// A remote's answer to the workspace's handshake (FABRIC-CLOSE1i): every machine it holds an operation or a push from, but itself,
    /// whose last push did not declare <c>closure</c>, ordered by id. A shared host holds one workspace by construction, so these are the
    /// workspace's machines as far as it has heard them; one that has never pushed has made nothing here to misread. A machine a person
    /// retired here is not counted until a push marks it again (FABRIC-CLOSE1i2).
    /// </summary>
    public Task<QuestClosure> ClosureAsync(CancellationToken ct = default) =>
        _db.RunAsync(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = """
                SELECT machine FROM (SELECT DISTINCT machine FROM quest_log UNION SELECT machine FROM quest_declared)
                WHERE machine <> $self AND machine NOT IN (SELECT machine FROM quest_declared WHERE closure = 1)
                  AND machine NOT IN (SELECT machine FROM quest_retired)
                ORDER BY machine
                """;
            command.Parameters.AddWithValue("$self", store.Machine);
            var undeclared = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) undeclared.Add(reader.GetString(0));
            return new QuestClosure(undeclared);
        }, ct);

    /// <summary>
    /// A person's retirement of <paramref name="machine"/> at a remote (FABRIC-CLOSE1i2): a machine whose store was deleted, or that
    /// left the workspace, no longer counted by <see cref="ClosureAsync"/>, until a push marks it again (<see cref="DeclaredAsync"/>).
    /// False, and nothing kept, where the remote has heard nothing from it: no operation and no push, or the remote itself.
    /// </summary>
    public Task<bool> RetireMachineAsync(string machine, DateTimeOffset at, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (transaction, inside) =>
        {
            await using var command = _db.Command();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO quest_retired (machine, at)
                SELECT $machine, $at
                WHERE $machine <> $self
                  AND (EXISTS (SELECT 1 FROM quest_log WHERE machine = $machine) OR EXISTS (SELECT 1 FROM quest_declared WHERE machine = $machine))
                ON CONFLICT (machine) DO UPDATE SET at = excluded.at
                """;
            command.Parameters.AddWithValue("$machine", machine);
            command.Parameters.AddWithValue("$self", store.Machine);
            command.Parameters.AddWithValue("$at", Moments.Write(at));
            return await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false) > 0;
        }, ct);

    /// <summary>
    /// The remote's handshake as a retirement there answered it (FABRIC-CLOSE1i2), kept as <paramref name="workspace"/>'s last pass keeps
    /// what it heard, so a bind reads it at once rather than at the next pass. Nothing where no pass has recorded the circle yet: its
    /// first pass hears the remote for itself.
    /// </summary>
    public Task HeardClosureAsync(string workspace, QuestClosure closure, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (transaction, inside) =>
        {
            var circle = Workspaces.Normalize(workspace);
            await using var command = _db.Command();
            command.Transaction = transaction;
            command.CommandText = "UPDATE quest_passes SET closure = $closure WHERE workspace = $workspace";
            command.Parameters.AddWithValue("$workspace", circle);
            command.Parameters.AddWithValue("$closure", LinksJson(closure.Undeclared));
            var written = await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            if (written > 0) Heard(circle, closure);
            return written;
        }, ct);
}
