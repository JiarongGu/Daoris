using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The old workflow records as saved graphs (<see cref="WorkflowMigration"/>): the saved workflows and <c>driver.json</c> copied first,
/// the repositories read from the service's registry table, since the service is not up at the first start, and the file written once with
/// what the conversion left of it.
/// </summary>
public sealed class WorkflowsStep : IHomeMigrationStep
{
    public string Name => "workflows";

    public void Run(HomeMigrationRun run)
    {
        var file = DaorisHome.Entry(run.Home, HomeEntry.DriverConfig);
        var folder = WorkflowStore.FolderOf(run.Home);
        if (Directory.Exists(folder))
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)) run.Keep(path);
        }

        run.Keep(file);
        var migrated = WorkflowMigration.Convert(run.Home, DriverConfig.Load(file), Registry(run.Home));
        if (migrated.Empty) return;
        migrated.Config.Save(file);
        run.Said(file);
        if (migrated.Saved.Count > 0) run.Said(folder);
    }

    /// <summary>Each repository the service's store registers, with its workspace; none where the store or its table is not there.</summary>
    internal static IReadOnlyList<RepoView> Registry(string home)
    {
        var path = DaorisHome.Entry(home, HomeEntry.Knowledge);
        if (!File.Exists(path)) return [];
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'registrations'";
        if (Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 0) return [];
        command.CommandText = "SELECT repository, workspace, adopted, root FROM registrations ORDER BY repository";
        using var reader = command.ExecuteReader();
        var rows = new List<RepoView>();
        while (reader.Read())
        {
            rows.Add(new RepoView(reader.GetString(0), reader.GetInt64(2) != 0, reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(1)));
        }

        return rows;
    }
}
