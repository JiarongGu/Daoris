namespace Daoris.Driver;

/// <summary>
/// A released base held to exactly what its producer's build owns (the tree-pool design §38.2, 3): every entry under the configuration's
/// folder the build's own account does not name (another project's leftovers, a dropped package's assembly, a framework or a runtime the
/// build did not target, a <c>publish/</c>) is moved into the set-aside folder of the take that kept it, with the previous line's, a folder
/// holding none of the account's files whole; a base under which the account names nothing is set aside whole. No link is followed.
/// </summary>
internal static class BaseTrim
{
    /// <summary>How many entries went aside, and whether the whole folder did.</summary>
    internal static (int Count, bool Whole) Trim(string tree, TreeBase kept, IReadOnlySet<string> account)
    {
        var folder = BaseMarker.FolderOf(tree, kept.Path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var owned = account.Where(file => file.StartsWith(folder + Path.DirectorySeparatorChar, comparison)).ToList();
        if (owned.Count == 0)
        {
            var whole = Away(tree, kept, folder);
            File.Delete(Path.Combine(whole, BaseMarker.FileName));
            return (1, true);
        }

        var count = 0;
        Walk(folder);
        return (count, false);

        void Walk(string at)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(at).Order(StringComparer.Ordinal).ToList())
            {
                if (string.Equals(entry, Path.Combine(folder, BaseMarker.FileName), comparison)) continue;
                var inward = Directory.Exists(entry) && !File.GetAttributes(entry).HasFlag(FileAttributes.ReparsePoint);
                if (inward && owned.Any(file => file.StartsWith(entry + Path.DirectorySeparatorChar, comparison)))
                {
                    Walk(entry);
                }
                else if (inward || !owned.Contains(entry, StringComparer.FromComparison(comparison)))
                {
                    Away(tree, kept, entry);
                    count++;
                }
            }
        }
    }

    /// <summary>One entry renamed into the take's set-aside folder at its own relative place, the folder marked as the previous line's first.</summary>
    private static string Away(string tree, TreeBase kept, string entry)
    {
        if (kept.Aside.Length == 0) throw new DriverException($"{kept.Path} names no set-aside folder, so nothing under it is moved");
        if (!File.Exists(PoolAsides.MarkerOf(kept.Aside)) && kept.From.Length > 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(kept.Aside))!);
            PoolAsides.Mark(kept.Aside, kept.From);
        }

        var target = Path.Combine(kept.Aside, Path.GetRelativePath(tree, entry));
        if (Path.Exists(target)) target += "-" + Guid.NewGuid().ToString("N")[..8];
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (Directory.Exists(entry)) Directory.Move(entry, target);
        else File.Move(entry, target);
        return target;
    }
}
