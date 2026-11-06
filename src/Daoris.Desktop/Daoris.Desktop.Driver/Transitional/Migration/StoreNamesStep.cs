namespace Daoris.Driver;

/// <summary>
/// The desktop store's names as this build reads them (D232; AGENTPROVIDER1t): the store's own step that renames them is run here, a copy
/// of the store kept first, so a binding's door is <c>bindings.door</c> and the staffing's projection of <c>driver.json</c>'s old keys is
/// gone. A home with no store, or one already past the step, is left as it is.
/// </summary>
public sealed class StoreNamesStep : IHomeMigrationStep
{
    /// <summary>The store's version whose step renames them.</summary>
    public const int Version = 22;

    public string Name => "store-names";

    public void Run(HomeMigrationRun run)
    {
        var path = DesktopStore.LocationIn(run.Home);
        using (var read = DesktopStoreReader.Open(path))
        {
            if (read is null || read.Version >= Version) return;
        }

        run.Keep(path);
        // The migration runs before the start is confirmed, and its copy is the undo, so it opens the store as a settled start does.
        using var store = DesktopStore.Open(path, StoreProcess.Options(() => StoreSettled.Yes));
        if (store.Held is { } held) throw new DriverException($"the desktop's store was not brought to today's names: {held}");
        run.Said(path);
    }
}
