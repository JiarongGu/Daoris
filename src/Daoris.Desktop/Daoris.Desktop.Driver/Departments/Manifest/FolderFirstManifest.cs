namespace Daoris.Driver;

/// <summary>The manifest read when a folder is added, kept as the place's last, so its first turn has a base to find what changed since.</summary>
public static class FolderFirstManifest
{
    private const string Record = "added";

    /// <summary>Read the manifest of the folder at <paramref name="root"/>, passing by the registration's <paramref name="excludes"/>.</summary>
    public static void Keep(string home, string workspace, string department, string root, IReadOnlyList<string>? excludes)
    {
        var store = new ManifestStore(home, workspace, department);
        store.Keep(Record, ManifestMoment.End,
            FolderManifestReader.Read(root, store.Content, null, ManifestBounds.Of(home),PlaceExcludes.Of(excludes)) with { Own = [] });
    }
}
