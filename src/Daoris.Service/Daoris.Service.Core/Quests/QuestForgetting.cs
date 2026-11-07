using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>What this machine cleared after a remote numbered it: the mark that keeps the fetches from bringing it back.</summary>
internal sealed class QuestForgetting(QuestStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>
    /// Remove one quest from this machine: its row and its whole log together, since a row with no log is given a fresh
    /// pending history at the next open and a log with no row lets a publish append to it. Where a remote numbered any
    /// of its operations it is <b>forgotten</b>: <c>quest_forgotten</c> keeps its id, and the fetch skips it from then on
    /// (<see cref="QuestStore.IntegrateAsync"/>), so no later move and no cursor at zero brings it back. No operation is
    /// written, so nothing travels: the team's copy is untouched, and a new store fetches it whole.
    /// </summary>
    /// <remarks>
    /// Blind, like every write here, and opening no transaction of its own: <see cref="HistoryDesk"/> judges the unit and
    /// calls this inside the one transaction it clears the unit in, so the mark, the log and the row go together or not at
    /// all. The gate it takes is the one that transaction's work already holds.
    /// </remarks>
    /// <returns>Whether it is forgotten, rather than simply going because nothing of it is ever numbered.</returns>
    internal Task<bool> ForgetAsync(string id, DateTimeOffset at, CancellationToken ct) => _db.RunAsync<bool>(async () =>
    {
        bool numbered;
        await using (var probe = _db.Command())
        {
            probe.CommandText = "SELECT EXISTS (SELECT 1 FROM quest_log WHERE quest = $id AND remote IS NOT NULL)";
            probe.Parameters.AddWithValue("$id", id);
            numbered = Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false)) != 0;
        }

        await using var command = _db.Command();
        command.CommandText = (numbered ? "INSERT OR IGNORE INTO quest_forgotten (id, at) VALUES ($id, $at);" : "")
                              + "DELETE FROM quest_log WHERE quest = $id; DELETE FROM quest_forwards WHERE quest = $id; DELETE FROM quests WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$at", Moments.Write(at));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return numbered;
    }, ct);

    /// <summary>Whether this machine forgot the quest: it cleared it after a remote numbered it.</summary>
    public Task<bool> ForgottenAsync(string id, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM quest_forgotten WHERE id = $id)";
        command.Parameters.AddWithValue("$id", id);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct).ConfigureAwait(false)) != 0;
    }, ct);

    /// <summary>Every quest this machine forgot: what the quest fetch and the session fetch skip.</summary>
    public Task<IReadOnlySet<string>> ForgottenAsync(CancellationToken ct = default) =>
        _db.RunAsync(() => ForgottenAsync(transaction: null, ct), ct);

    internal async Task<IReadOnlySet<string>> ForgottenAsync(SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = "SELECT id FROM quest_forgotten";
        var forgotten = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) forgotten.Add(reader.GetString(0));
        return forgotten;
    }
}
