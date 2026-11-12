using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The session records in the service's store in the shape it reads: a record from before the list of the person's words holds
/// its answer in the old column, which becomes the list's first word, keyed <c>answer</c> and said when the record last changed, as the
/// service's own first write after it did; the old column is emptied on every record.
/// </summary>
public sealed class SessionRecordsStep : IHomeMigrationStep
{
    public string Name => "session-records";

    public void Run(HomeMigrationRun run)
    {
        var path = DaorisHome.Entry(run.Home, HomeEntry.Knowledge);
        if (!File.Exists(path)) return;
        run.Keep(path);
        using var connection = StoreEngineSource.Connect(path, DesktopOpen.ReadWrite);
        using (var columns = connection.CreateCommand())
        {
            columns.CommandText = "SELECT COUNT(*) FROM pragma_table_info('sessions') WHERE name IN ('answer', 'said', 'updated')";
            if (Convert.ToInt32(columns.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) < 3) return;
        }

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE sessions SET said = json_array(json_object('id', 'answer', 'text', answer, 'at', updated, 'files', json('[]')))
            WHERE said IS NULL AND answer IS NOT NULL;
            UPDATE sessions SET answer = NULL WHERE answer IS NOT NULL;
            """;
        var rows = command.ExecuteNonQuery();
        transaction.Commit();
        if (rows > 0) run.Said(path);
    }
}
