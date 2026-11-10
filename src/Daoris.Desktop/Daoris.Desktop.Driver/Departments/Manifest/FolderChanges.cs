namespace Daoris.Driver;

/// <summary>
/// What changed in a folder between two of its manifests, each of which lists every file: a path only the later lists is added, one
/// only the earlier lists removed, and one both list changed where its hash differs, or, where either could not be read, its size or
/// time. A manifest too large to follow gives nothing.
/// </summary>
public static class FolderChanges
{
    /// <summary>The paths changed from <paramref name="earlier"/> to <paramref name="later"/>.</summary>
    public static PlaceChangesSince Between(RunManifest earlier, RunManifest later)
    {
        if (earlier.TooLarge || later.TooLarge) return new([], [], []);
        var before = earlier.Entries.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
        var after = later.Entries.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
        List<string> added = [], changed = [], removed = [];
        foreach (var path in before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var (was, now) = (before.GetValueOrDefault(path), after.GetValueOrDefault(path));
            if (was is null) added.Add(path);
            else if (now is null) removed.Add(path);
            else if (Moved(was, now)) changed.Add(path);
        }

        return new(added, changed, removed);
    }

    private static bool Moved(ManifestEntry was, ManifestEntry now) =>
        was.Sha256 is not null && now.Sha256 is not null
            ? !string.Equals(was.Sha256, now.Sha256, StringComparison.Ordinal)
            : was.Size != now.Size || was.Time != now.Time;
}
