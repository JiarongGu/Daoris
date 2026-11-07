namespace Daoris.Knowledge;

/// <summary>
/// The session record's columns under the names this build reads: a table that still spells its door <c>adapter</c> and the
/// version its spawn observed <c>harness_version</c> has both renamed as the store opens, before a column is added or a row read. The
/// store moves itself, as it adds its columns, because a shared host opens it through this schema and runs no desktop migration.
/// </summary>
internal static class SessionColumnNames
{
    private static readonly (string Old, string Now)[] Renamed = [("adapter", "door"), ("harness_version", "door_version")];

    public static async Task RenameAsync(ConnectionGate db, CancellationToken ct)
    {
        foreach (var (old, now) in Renamed)
        {
            if (!await SchemaColumns.HasAsync(db, "sessions", old, ct).ConfigureAwait(false)
                || await SchemaColumns.HasAsync(db, "sessions", now, ct).ConfigureAwait(false))
            {
                continue;
            }

            await using var alter = db.Command();
            alter.CommandText = $"ALTER TABLE sessions RENAME COLUMN {old} TO {now}";
            await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }
}
