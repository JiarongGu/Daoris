namespace Daoris.Driver;

/// <summary>
/// The <c>bin</c> entry's old place, the home's <c>bin/</c> an earlier layout kept the service hosts in: removed, uncopied, once the
/// install's own <c>app/</c> carries both hosts, which the locators rank above it, so nothing reads it again. A home that is no install, an
/// <c>app/</c> missing either host and an entry whose two places are one are left, and the step run again changes nothing.
/// </summary>
public sealed class OldBinStep : IHomeMigrationStep
{
    public string Name => "old-bin";

    public void Run(HomeMigrationRun run)
    {
        var (folder, _) = DaorisHome.FolderOf(run.Home);
        if (!File.Exists(Path.Combine(folder, DaorisHome.InstallMarker))) return;
        var carried = File.Exists(Path.Combine(folder, "app", "daoris-knowledge-http", ServiceHostLocator.ExecutableName))
                      && File.Exists(Path.Combine(folder, "app", KnowledgeConnector.Folder, KnowledgeConnector.ExecutableName));
        if (!carried) return;
        var places = DaorisHome.Places(run.Home, HomeEntry.Bin);
        if (places.Count < 2 || !Directory.Exists(places[^1])) return;
        try
        {
            Directory.Delete(places[^1], recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A file held open there keeps the folder, which nothing reads, for a hand to remove.
            return;
        }

        run.Count(Name, "removed");
    }
}
