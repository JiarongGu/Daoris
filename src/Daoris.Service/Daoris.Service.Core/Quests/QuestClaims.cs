using Microsoft.Data.Sqlite;
using static Daoris.Knowledge.QuestRecordCodec;

namespace Daoris.Knowledge;

/// <summary>Where this machine's claim on a quest stands, read from the operations this machine made.</summary>
internal sealed class QuestClaims(QuestStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>
    /// Where THIS machine's claim on a quest stands: held once a remote numbered its take,
    /// unconfirmed while the take is only here, lost once the rebase made it a conflict — and none when
    /// this machine never took it. A take on a machine with no remote is never numbered, so it stays
    /// unconfirmed, which nothing acts on.
    /// </summary>
    public Task<QuestClaim> ClaimAsync(string id, CancellationToken ct = default) =>
        _db.RunAsync(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = $"SELECT {QuestStore.OperationColumns} FROM quest_log WHERE quest = $id AND machine = $machine";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$machine", store.Machine);

            var mine = new List<QuestOperation>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) mine.Add(ReadOperation(reader));
            return QuestLog.Claim(mine, store.Machine);
        }, ct);

    /// <summary>
    /// Whether THIS machine's claim on a quest reads lost because its take lost to the person's reopen of the work the step grew from
    /// (<see cref="QuestLog.LostToTheReopen"/>), not to another machine's take: what the claim's answer says beside
    /// it, so the driver stops the session in words that are true. False where the claim does not read lost.
    /// </summary>
    public async Task<bool> LostToTheReopenAsync(string id, CancellationToken ct = default)
    {
        if (await ClaimAsync(id, ct).ConfigureAwait(false) != QuestClaim.Lost) return false;
        return QuestLog.LostToTheReopen(await store.HistoryAsync(id, ct).ConfigureAwait(false), store.Machine);
    }
}
