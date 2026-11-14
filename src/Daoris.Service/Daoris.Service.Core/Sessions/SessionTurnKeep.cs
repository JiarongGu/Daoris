namespace Daoris.Knowledge;

/// <summary>
/// The write of a record's turns where a wake adds one. Blind, like every write of the store: which turn is the ledger's to say. No
/// revision, since turns never travel.
/// </summary>
internal static class SessionTurnKeep
{
    /// <summary>Add <paramref name="turn"/> to the turns of this machine's record <paramref name="record"/>, and answer it as it now reads.</summary>
    public static Task<Session> AddAsync(SessionStore store, Session record, SessionTurn turn, CancellationToken ct) =>
        KeepAsync(store, record, [.. SessionTurns.Own(record), turn], ct);

    /// <summary>
    /// Keep on the record's last turn what its door reported for it: <paramref name="spent"/> less the figures the turns before it kept.
    /// A count the door never said leaves the figure unset.
    /// </summary>
    public static Task<Session> UsageAsync(SessionStore store, Session record, TurnSpent spent, CancellationToken ct)
    {
        var own = SessionTurns.Own(record);
        var before = own.Take(own.Count - 1).ToList();
        long? Share(long? total, Func<SessionTurn, long?> kept) =>
            total is { } count ? Math.Max(0, count - before.Sum(turn => kept(turn) ?? 0)) : null;
        return KeepAsync(
            store, record, [.. before, own[^1] with { Input = Share(spent.Input, turn => turn.Input), Output = Share(spent.Output, turn => turn.Output) }], ct);
    }

    private static Task<Session> KeepAsync(SessionStore store, Session record, IReadOnlyList<SessionTurn> turns, CancellationToken ct) => store.Db.RunAsync<Session>(async () =>
    {
        await using var command = store.Db.Command();
        command.CommandText = "UPDATE sessions SET turns = $turns WHERE id = $id AND origin IS NULL";
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$turns", SessionTurns.Write(turns));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return record with { Turns = turns };
    }, ct);
}
