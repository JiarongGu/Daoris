namespace Daoris.Driver;

/// <summary>
/// A top git lists whole, partitioned where a boundary lies under it (the tree-pool design §36.4, §36.5): today a copy the files
/// record holds. Its entries are listed one folder at a time along the way to each boundary and never inside a link; each copy whose
/// bytes still hold is Daoris's own, and every other entry on the way is a path of its own for the rules to sort, so the top is
/// Daoris's own whole only where every entry under it is a copy that holds.
/// </summary>
internal static class TopPartition
{
    /// <summary>The copies under <paramref name="top"/> that hold and the other entries on the way; null where no recorded copy lies under it.</summary>
    internal static (IReadOnlyList<string> Copies, IReadOnlyList<string> Others)? Of(TreeFilesState? record, string slot, string top)
    {
        if (record is null || !top.EndsWith('/')) return null;
        var boundaries = record.Copies.Keys.Where(copy => copy.StartsWith(top, StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        if (boundaries.Count == 0) return null;

        List<string> copies = [], others = [];
        var folders = new Stack<string>([top]);
        while (folders.TryPop(out var folder))
        {
            var full = Path.Combine(slot, folder.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full)) continue;
            foreach (var entry in Directory.EnumerateFileSystemEntries(full).Order(StringComparer.Ordinal))
            {
                var inward = Directory.Exists(entry) && !File.GetAttributes(entry).HasFlag(FileAttributes.ReparsePoint);
                var path = folder + Path.GetFileName(entry) + (inward || Directory.Exists(entry) ? "/" : "");
                if (boundaries.Contains(path)) (TreeCopies.IsOwn(record, slot, path) ? copies : others).Add(path);
                else if (inward && boundaries.Any(each => each.StartsWith(path, StringComparison.Ordinal))) folders.Push(path);
                else others.Add(path);
            }
        }

        return (copies, others);
    }
}
