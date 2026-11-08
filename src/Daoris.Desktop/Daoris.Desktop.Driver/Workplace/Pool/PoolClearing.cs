namespace Daoris.Driver;

/// <summary>
/// What a take does to the slot's ignored tops around its switch: Daoris's own copies of machine-local files deleted before it, so the
/// switch never refuses a slot for a file Daoris made, and after it the runnable outputs (and the intermediates and caches where the
/// toolchain moved) renamed whole into a set-aside folder of the line they came from. Nothing is deleted but a copy, and no link is followed.
/// </summary>
internal static class PoolClearing
{
    /// <summary>Daoris's own copies deleted, a file at a time.</summary>
    internal static void DeleteCopies(string slot, IReadOnlyList<string> copies)
    {
        foreach (var copy in copies)
        {
            var path = Path.Combine(slot, copy.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) continue;
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
    }

    /// <summary>
    /// The outputs at the slot's present commit, and the intermediates where <paramref name="toolchainMoved"/>, renamed into the set-aside
    /// folder of <paramref name="from"/>, each at its own relative place. The tops moved; none where nothing was to move.
    /// </summary>
    /// <exception cref="IOException">A rename was refused, because something holds the folder.</exception>
    internal static async Task<IReadOnlyList<string>> ClearAsync(PoolSite site, string slot, string from, bool toolchainMoved, CancellationToken ct)
    {
        var head = await WorkingTree.HeadAsync(slot, ct).ConfigureAwait(false);
        if (head is null) return [];
        var status = await FillJudgement.StatusAsync(slot, ignored: true, ct).ConfigureAwait(false);
        var sorted = await SlotIgnored.ReadAsync(site, slot, status, head, ct).ConfigureAwait(false);
        IReadOnlyList<string> moving = toolchainMoved ? [.. sorted.Outputs, .. sorted.Intermediates] : sorted.Outputs;
        if (moving.Count == 0) return [];

        var aside = Path.Combine(AsideRoot(site), $"{from}-{DateTimeOffset.UtcNow.ToString(TreeSetAside.SetAsideStamp, System.Globalization.CultureInfo.InvariantCulture)}");
        foreach (var top in moving)
        {
            var relative = top.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar);
            var target = Path.Combine(aside, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var source = Path.Combine(slot, relative);
            if (Directory.Exists(source)) Directory.Move(source, target);
            else if (File.Exists(source)) File.Move(source, target);
        }

        return moving;
    }

    /// <summary>The folder a repository's set-aside folders are made in, beside the ones a tidy makes.</summary>
    private static string AsideRoot(PoolSite site) =>
        Path.Combine(DaorisHome.Entry(site.Home, HomeEntry.Trees), TreeSetAside.SetAsideFolder, site.Workspace, site.Repository);
}
