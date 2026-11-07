namespace Daoris.Knowledge;

/// <summary>
/// Where the store's writes stand: a mark that moves on every write to the file, this host's own connection's (the rows it has
/// changed) and any other's, a connector's from its own process included (the file's data version). Two reads that find the same
/// mark read the same rows, so an answer made from the rows after a mark is read may be answered again while the mark holds.
/// </summary>
/// <remarks>
/// It moves on writes no answer reads (a pass recorded, a cursor kept), which costs a reader one answer made again and never a
/// stale one. A write rolled back moves it too, for the same price.
/// </remarks>
public readonly record struct StoreMark(long Changes, long Version)
{
    internal static Task<StoreMark> ReadAsync(ConnectionGate db, CancellationToken ct) => db.RunAsync(async () =>
    {
        await using var command = db.Command();
        command.CommandText = "SELECT total_changes(), (SELECT data_version FROM pragma_data_version)";
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await reader.ReadAsync(ct).ConfigureAwait(false);
        return new StoreMark(reader.GetInt64(0), reader.GetInt64(1));
    }, ct);
}
