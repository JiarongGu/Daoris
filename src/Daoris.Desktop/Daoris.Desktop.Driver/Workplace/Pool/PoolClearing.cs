namespace Daoris.Driver;

/// <summary>The tops moved aside and the set-aside folder they went into.</summary>
internal sealed record PoolMoved(IReadOnlyList<string> Tops, string? Aside)
{
    internal static readonly PoolMoved None = new([], null);

    internal int Count => Tops.Count;

    /// <summary>The top a refused rename left in the slot, because something holds it; null where every top moved.</summary>
    internal string? Held { get; init; }
}

/// <summary>
/// What a take does to the slot's ignored tops around its switch: Daoris's own copies of machine-local files deleted before it, so the
/// switch never refuses a slot for a file Daoris made, and after it the runnable outputs (and the intermediates and caches where the
/// toolchain moved) renamed whole into a set-aside folder of the line they came from, all or none. Nothing is deleted but a copy and an
/// empty folder of its own, and no link is followed.
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
    /// Exactly the plan's tops moved, reading no rule (its outputs, its listed-again entries, and where <paramref name="toolchainMoved"/> its intermediates and bases),
    /// into its set-aside folder marked as <paramref name="line"/>'s first, a base under a moved top forgotten with its marker. A rename refused, because something holds
    /// the top, moves back what moved, in reverse, and answers the top held: a full move-back leaves no folder and no mark, and one that
    /// left a top keeps both, so the folder is held for the line and gathered at its finish.
    /// </summary>
    /// <param name="renaming">Called before each rename, forward and back.</param>
    internal static async Task<PoolMoved> ClearAsync(PoolSite site, string slot, TakePlan plan, string line, bool toolchainMoved, Func<string, string, Task>? renaming, CancellationToken ct)
    {
        var relisted = await PlanRelisting.MovingAsync(site.Home, slot, plan, toolchainMoved, ct).ConfigureAwait(false);
        IReadOnlyList<string> moving = toolchainMoved
            ? [.. plan.Outputs, .. plan.Intermediates, .. plan.Bases.Select(each => each.Path), .. relisted]
            : [.. plan.Outputs, .. relisted];
        if (moving.Count == 0) return PoolMoved.None;

        var aside = plan.Aside;
        if (line.Length > 0)
        {
            Directory.CreateDirectory(AsideRoot(site));
            PoolAsides.Mark(aside, line);
        }

        var moved = new List<(string Top, string Source, string Target)>();
        foreach (var top in moving)
        {
            var relative = top.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar);
            var (source, target) = (Path.Combine(slot, relative), Path.Combine(aside, relative));
            try
            {
                if (await RenameAsync(source, target, renaming).ConfigureAwait(false)) moved.Add((top, source, target));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                var left = await MoveBackAsync(moved, renaming).ConfigureAwait(false);
                if (left.Count == 0) Forget(aside);
                return new PoolMoved(left, left.Count == 0 ? null : aside) { Held = top };
            }
        }

        TreeBases.Forget(site.Home, slot, aside, moving);
        return new PoolMoved(moving, aside);
    }

    /// <summary>The tops moved back to the slot, the last moved first; the tops left aside, where a rename back is refused.</summary>
    private static async Task<IReadOnlyList<string>> MoveBackAsync(List<(string Top, string Source, string Target)> moved, Func<string, string, Task>? renaming)
    {
        var left = new List<string>();
        for (var at = moved.Count - 1; at >= 0; at--)
        {
            var (top, source, target) = moved[at];
            try
            {
                await RenameAsync(target, source, renaming).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                left.Add(top);
            }
        }

        return left;
    }

    private static async Task<bool> RenameAsync(string source, string target, Func<string, string, Task>? renaming)
    {
        var folder = Directory.Exists(source);
        if (!folder && !File.Exists(source)) return false;
        if (renaming is not null) await renaming(source, target).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (folder) Directory.Move(source, target);
        else File.Move(source, target);
        return true;
    }

    /// <summary>The set-aside folder a full move-back emptied, and its mark, removed: both Daoris's own and holding nothing.</summary>
    private static void Forget(string aside)
    {
        if (Directory.Exists(aside) && !Directory.EnumerateFiles(aside, "*", SearchOption.AllDirectories).Any()) Directory.Delete(aside, recursive: true);
        if (!Directory.Exists(aside)) File.Delete(PoolAsides.MarkerOf(aside));
    }

    /// <summary>The folder a repository's set-aside folders are made in, beside the ones a tidy makes.</summary>
    internal static string AsideRoot(PoolSite site) =>
        Path.Combine(DaorisHome.Entry(site.Home, HomeEntry.Trees), TreeSetAside.SetAsideFolder, site.Workspace, site.Repository);
}
