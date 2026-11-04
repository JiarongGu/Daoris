using Microsoft.Data.Sqlite;
using static Daoris.Knowledge.QuestRecordCodec;

namespace Daoris.Knowledge;

/// <summary>Reads who holds a quest's step and how each take that lost on it stands, from the log and this machine's bring-forwards.</summary>
internal sealed class QuestTakeReadings(QuestStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>This machine's bring-forwards on a quest (SYNCMERGE1b0), in the order they were made.</summary>
    private async Task<IReadOnlyList<QuestForwardPress>> PressesAsync(string id, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT conflict_machine, conflict_sequence, take_machine, take_sequence, at FROM quest_forwards
            WHERE quest = $quest ORDER BY at, take_sequence
            """;
        command.Parameters.AddWithValue("$quest", id);
        var presses = new List<QuestForwardPress>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            presses.Add(new QuestForwardPress(
                new QuestOperationRef(reader.GetString(0), reader.GetInt64(1)), new QuestOperationRef(reader.GetString(2), reader.GetInt64(3)),
                Moments.ReadRequired(reader.GetString(4))));
        }

        return presses;
    }

    /// <summary>
    /// Who holds a quest's step and how each take that lost on it stands or ended (SYNCMERGE1b0, <see cref="QuestLog.Read"/>), with this
    /// machine's own bring-forwards: what the quest's answer and the claim's say of them, and what the session ledger carries a
    /// stand-down on by. <see cref="QuestTakeReading.None"/> for a quest this store does not hold.
    /// </summary>
    public Task<QuestTakeReading> ReadTakesAsync(string id, CancellationToken ct = default) =>
        _db.RunAsync(async () => QuestLog.Read(
            await store.HistoryAsync(id, transaction: null, ct).ConfigureAwait(false), await PressesAsync(id, ct).ConfigureAwait(false)), ct);

    /// <summary>
    /// <see cref="ReadTakesAsync(string, CancellationToken)"/> for each of <paramref name="quests"/> with something to read: taken, or one
    /// whose log holds a conflict, settled or not. By id; a quest with neither is absent, and reads as <see cref="QuestTakeReading.None"/>.
    /// A list answers many quests, so only those have their log read.
    /// </summary>
    public Task<IReadOnlyDictionary<string, QuestTakeReading>> ReadTakesAsync(IEnumerable<Quest> quests, CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyDictionary<string, QuestTakeReading>>(async () =>
        {
            var contested = new HashSet<string>(StringComparer.Ordinal);
            await using (var command = _db.Command())
            {
                command.CommandText = $"SELECT DISTINCT quest FROM quest_log WHERE kind = '{KindText(QuestOperationKind.Conflict)}'";
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false)) contested.Add(reader.GetString(0));
            }

            var read = new Dictionary<string, QuestTakeReading>(StringComparer.Ordinal);
            foreach (var quest in quests.Where(quest => quest.Status == QuestStatus.Taken || contested.Contains(quest.Id)))
            {
                read[quest.Id] = QuestLog.Read(
                    await store.HistoryAsync(quest.Id, transaction: null, ct).ConfigureAwait(false),
                    await PressesAsync(quest.Id, ct).ConfigureAwait(false));
            }

            return read;
        }, ct);
}
