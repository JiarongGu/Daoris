namespace Daoris.Knowledge;

/// <summary>
/// An opinion's reviewer under the names this build reads (D232): a row whose asking JSON still spells the reviewer's door
/// <c>adapter</c> has it renamed <c>door</c> as the store opens, the other parts of the row untouched. The store moves itself because
/// a shared host opens the same schema and runs no desktop migration.
/// </summary>
internal static class OpinionReviewerNames
{
    public static async Task RenameAsync(ConnectionGate db, CancellationToken ct)
    {
        await using var update = db.Command();
        update.CommandText = """
            UPDATE opinions
               SET asking = json_set(json_remove(asking, '$.reviewer.adapter'), '$.reviewer.door', json_extract(asking, '$.reviewer.adapter'))
             WHERE json_type(asking, '$.reviewer.adapter') = 'text' AND json_type(asking, '$.reviewer.door') IS NULL
            """;
        await update.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
