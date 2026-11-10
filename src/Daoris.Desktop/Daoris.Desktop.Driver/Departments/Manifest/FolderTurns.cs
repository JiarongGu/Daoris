namespace Daoris.Driver;

/// <summary>
/// A turn in a folder that is no git repository, under the writer's lease: its start's manifest is kept under the record and compared
/// with the place's last manifest of any line, and its end's is kept once. A folder's lease is its turn's alone, so an end keeps the place
/// with nothing. A jj repository is not read here, and a place whose last manifest is git's is never read as a folder: its checkout is
/// held until it is one again.
/// </summary>
public static class FolderTurns
{
    /// <summary>Read and keep the start's manifest of <paramref name="record"/> in the folder at <paramref name="root"/>.</summary>
    /// <returns>What the session is told, or null where the place is not read as a folder.</returns>
    public static PlaceTold? Start(
        string home, string workspace, string department, string root, string record, ManifestBounds? bounds = null)
    {
        if (Directory.Exists(Path.Combine(root, ".jj")) || !Directory.Exists(root)) return null;
        var store = new ManifestStore(home, workspace, department);
        var last = store.Last();
        if (last is { Kind: not PlaceKind.Folder }) return null;
        var start = FolderManifestReader.Read(root, store.Content, last, bounds ?? ManifestBounds.Default);
        store.Keep(record, ManifestMoment.Start, start with { Own = [] });
        var outside = last is null ? [] : FolderChanges.Between(last, start).All;
        return new PlaceTold(outside, []) { Kind = PlaceKind.Folder, TooLarge = start.TooLarge };
    }

    /// <summary>Read and keep the end's manifest of a folder turn whose start is <paramref name="start"/>.</summary>
    /// <returns>No path: a folder is held by its turn alone.</returns>
    public static IReadOnlyList<string> End(ManifestStore store, string record, RunManifest start)
    {
        var end = FolderManifestReader.Read(start.Root!, store.Content, start, ManifestBounds.Default);
        store.Keep(record, ManifestMoment.End, end with { Own = [] });
        return [];
    }
}
