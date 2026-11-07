namespace Daoris.Knowledge;

/// <summary>
/// The memory log's author columns under the names this build reads: a table that still spells the author's door
/// <c>adapter</c> and the version its spawn observed <c>harness</c> has both renamed as the store opens. The store moves itself
/// because a shared host opens the same schema and runs no desktop migration.
/// </summary>
internal static class MemoryAuthorColumnNames
{
    private static readonly (string Old, string Now)[] Renamed = [("adapter", "door"), ("harness", "door_version")];

    public static async Task RenameAsync(ConnectionGate db, CancellationToken ct)
    {
        foreach (var (old, now) in Renamed)
        {
            if (!await SchemaColumns.HasAsync(db, "memory_ops", old, ct).ConfigureAwait(false)
                || await SchemaColumns.HasAsync(db, "memory_ops", now, ct).ConfigureAwait(false))
            {
                continue;
            }

            await using var alter = db.Command();
            alter.CommandText = $"ALTER TABLE memory_ops RENAME COLUMN {old} TO {now}";
            await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }
}
