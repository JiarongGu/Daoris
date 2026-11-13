namespace Daoris.Knowledge;

/// <summary>
/// A pack is stale by pointer: when the line moves, each pointer's blob id is compared with the file's now, and only the pointers that moved
/// are marked, so a scout is asked again for those alone and a pack whose files did not change stays as it was.
/// </summary>
public static class PackStaleness
{
    /// <summary>The pointers of <paramref name="pack"/> whose file now holds another blob. A file <paramref name="current"/> does not know is not judged.</summary>
    /// <param name="current">The blob id each repository-relative path holds now, for the paths the caller read.</param>
    public static IReadOnlyList<PackPointer> Moved(PartPack? pack, IReadOnlyDictionary<string, string> current) =>
        pack is null
            ? []
            : [.. pack.Pointers.Where(pointer => current.TryGetValue(pointer.File, out var now)
                && !string.Equals(now, pointer.Blob, StringComparison.OrdinalIgnoreCase))];
}
