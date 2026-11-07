using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// Each repository's workspace kept in the desktop store: <c>repository_workspaces</c>,
/// written by the look from the registry it read and read by the rules' judge in every process, the window's door and a terminal's alike,
/// so a write is weighed on the chain the lines read whichever process judges it. A store before the step, or none, keeps no workspace and
/// each place is judged on its own chain to the machine.
/// </summary>
public sealed class RuleWiderPlaceStore(string home, Func<StoreSettled>? settled = null)
{
    /// <summary>The family a write moves.</summary>
    public const string Family = "repository_workspaces";

    /// <summary>The store's version the table arrives at.</summary>
    public const int Step = 21;

    private const string Sql = "SELECT repository, workspace FROM repository_workspaces ORDER BY repository";

    private static readonly Lock _gate = new();

    private string Location => DesktopStore.LocationIn(home);

    /// <summary>Each repository's workspace as kept; none before the table or where the store does not read.</summary>
    public RuleWiderPlaces Read()
    {
        try
        {
            using var store = DesktopStoreReader.Open(Location);
            return store is null || store.Version < Step ? RuleWiderPlaces.None : RuleWiderPlaces.From(store.Query(Sql, Row));
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            return RuleWiderPlaces.None;
        }
    }

    /// <summary>
    /// Keep <paramref name="registered"/> as each repository's workspace, whole, where it differs from what is kept. An empty registry keeps
    /// what is kept: a look whose registry does not read leaves the chain as the last one that did. Never throws.
    /// </summary>
    /// <returns>Whether the kept map moved.</returns>
    public bool Keep(IEnumerable<(string Repository, string? Workspace)> registered)
    {
        var map = RuleWiderPlaces.From(registered).WorkspaceOf;
        if (map.Count == 0 || Same(Read().WorkspaceOf, map)) return false;
        lock (_gate)
        {
            try
            {
                using var store = DesktopStore.Open(Location, StoreProcess.Options(settled ?? StartConfirmation.Read));
                if (store.Stamp.Version < Step) return false;
                store.Write(Family, write =>
                {
                    write.Execute("DELETE FROM repository_workspaces");
                    foreach (var (repository, workspace) in map)
                    {
                        write.Execute("INSERT INTO repository_workspaces (repository, workspace) VALUES ($repository, $workspace)", ("$repository", repository), ("$workspace", workspace));
                    }

                    write.Moved(Family);
                });
                return true;
            }
            catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException or DriverException)
            {
                return false;
            }
        }
    }

    private static (string Repository, string? Workspace) Row(SqliteDataReader row) => (row.GetString(0), row.GetString(1));

    private static bool Same(IReadOnlyDictionary<string, string> kept, IReadOnlyDictionary<string, string> read) =>
        kept.Count == read.Count && read.All(pair => kept.TryGetValue(pair.Key, out var workspace) && string.Equals(workspace, pair.Value, StringComparison.OrdinalIgnoreCase));
}
