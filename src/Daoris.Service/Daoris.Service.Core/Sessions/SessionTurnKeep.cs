namespace Daoris.Knowledge;

/// <summary>
/// The write of a record's turns where a wake adds one. Blind, like every write of the store: which turn is the ledger's to say. No
/// revision, since turns never travel.
/// </summary>
internal static class SessionTurnKeep
{
    /// <summary>Add <paramref name="turn"/> to the turns of this machine's record <paramref name="record"/>, and answer it as it now reads.</summary>
    public static Task<Session> AddAsync(SessionStore store, Session record, SessionTurn turn, CancellationToken ct) => store.Db.RunAsync<Session>(async () =>
    {
        IReadOnlyList<SessionTurn> turns = [.. SessionTurns.Own(record), turn];
        await using var command = store.Db.Command();
        command.CommandText = "UPDATE sessions SET turns = $turns WHERE id = $id AND origin IS NULL";
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$turns", SessionTurns.Write(turns));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return record with { Turns = turns };
    }, ct);
}
