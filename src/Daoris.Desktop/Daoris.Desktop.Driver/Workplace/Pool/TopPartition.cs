namespace Daoris.Driver;

/// <summary>
/// A top git lists whole, partitioned where a boundary lies under it (the tree-pool design §36.4, §36.5): a copy or a share the files
/// record holds, or a path a share's entry declares. Its entries are listed one folder at a time along the way to each boundary and
/// never inside a link; a copy whose bytes hold and a share whose link leads to its place are Daoris's own, and every other entry on
/// the way is a path of its own for the rules to sort, so the top is Daoris's own only where every entry under it is a boundary that holds.
/// </summary>
internal static class TopPartition
{
    /// <summary>The test seam: run with each folder the partition lists. Production sets none.</summary>
    internal static readonly AsyncLocal<Action<string>?> Listing = new();

    /// <summary>The copies and shares under <paramref name="top"/> that hold and the other entries on the way; null where no boundary lies under it.</summary>
    internal static (IReadOnlyList<string> Copies, IReadOnlyList<string> Shares, IReadOnlyList<string> Others)? Of(
        TreeFilesState? record, string slot, string top, IReadOnlyCollection<string>? declared = null)
    {
        if (!top.EndsWith('/')) return null;
        var shares = (record?.Shares ?? []).Where(share => TreeShares.Standing(slot, share) == TreeShareStanding.Holds).Select(share => share.Path).ToHashSet(StringComparer.Ordinal);
        var boundaries = (record?.Copies.Keys ?? []).Concat(record?.Shares.Select(share => share.Path) ?? []).Concat(declared ?? [])
            .Where(each => each.StartsWith(top, StringComparison.Ordinal) && each != top).ToHashSet(StringComparer.Ordinal);
        if (boundaries.Count == 0) return null;

        List<string> copies = [], held = [], others = [];
        var folders = new Stack<string>([top]);
        while (folders.TryPop(out var folder))
        {
            var full = Path.Combine(slot, folder.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full)) continue;
            Listing.Value?.Invoke(folder);
            foreach (var entry in Directory.EnumerateFileSystemEntries(full).Order(StringComparer.Ordinal))
            {
                var inward = Directory.Exists(entry) && !File.GetAttributes(entry).HasFlag(FileAttributes.ReparsePoint);
                var path = folder + Path.GetFileName(entry) + (inward || Directory.Exists(entry) ? "/" : "");
                if (boundaries.Contains(path)) (shares.Contains(path) ? held : TreeCopies.IsOwn(record, slot, path) ? copies : others).Add(path);
                else if (inward && boundaries.Any(each => each.StartsWith(path, StringComparison.Ordinal))) folders.Push(path);
                else others.Add(path);
            }
        }

        return (copies, held, others);
    }
}
