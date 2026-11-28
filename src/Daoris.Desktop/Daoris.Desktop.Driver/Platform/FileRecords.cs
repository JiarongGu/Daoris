namespace Daoris.Driver;

/// <summary>
/// The file records the measures handed it have counted (<see cref="PlacesWalk.Measure"/>), so a hard-linked file (a dependency placed from
/// the store) counts once across them, as its bytes are on the disk once: <paramref name="read"/> names a file's record and its count of
/// names, null where none is read, which counts as a file of one name.
/// </summary>
public sealed class FileRecords(Func<string, (string Identity, uint Names)?> read)
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    /// <summary>Whether a file's bytes count here, and whether it is a record of more than one name.</summary>
    internal (bool Counts, bool Linked) Of(string path) => read(path) is { Names: > 1 } record ? (_seen.Add(record.Identity), true) : (true, false);
}
