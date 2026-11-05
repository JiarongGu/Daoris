using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The finished session records from before notes carried codes (ONEWAY1ac): a record that ended with a note and no parts holds English
/// only, which the page no longer reads out of its text, so the note is cleared. A record still in use is left as it is.
/// </summary>
public sealed class NoteRecordsStep : IHomeMigrationStep
{
    private static readonly string[] Finished = ["Completed", "Declined", "StoodDown", "Failed", "Stopped"];

    public string Name => "note-records";

    public void Run(HomeMigrationRun run)
    {
        var path = DaorisHome.Entry(run.Home, HomeEntry.Knowledge);
        if (!File.Exists(path)) return;
        run.Keep(path);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        connection.Open();
        using (var columns = connection.CreateCommand())
        {
            columns.CommandText = "SELECT COUNT(*) FROM pragma_table_info('sessions') WHERE name IN ('state', 'note', 'note_parts')";
            if (Convert.ToInt32(columns.ExecuteScalar(), CultureInfo.InvariantCulture) < 3) return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE sessions SET note = NULL
            WHERE note IS NOT NULL AND (note_parts IS NULL OR note_parts IN ('', '[]'))
              AND state IN ({string.Join(", ", Finished.Select(state => $"'{state}'"))})
            """;
        if (command.ExecuteNonQuery() > 0) run.Said(path);
    }
}
