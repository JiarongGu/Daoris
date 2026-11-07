namespace Daoris.Driver;

/// <summary>
/// Which session branches no branch of the person's holds, for every session branch of a checkout in one <c>rev-list</c>, instead of a
/// <c>log tip --not &lt;the person's branches&gt;</c> proof of each branch the look judged, one start a branch. A branch's tip is
/// held exactly when it is not among the commits the one walk lists, so the answer for any tip the snapshot holds a session branch at is
/// read from that list. Kept with the judgement's refs (<see cref="RefSnapshot.Unheld"/>), so a write of the flow drops it with them.
/// </summary>
internal sealed class UnheldTips
{
    private readonly object _gate = new();
    private Task<(HashSet<string> Tips, HashSet<string> Open)?>? _read;

    /// <summary>
    /// Whether a branch of the person's holds every commit of <paramref name="tip"/>, as <see cref="TreeFollows.UnlandedAsync"/> counts
    /// zero; null where git could not say. A tip that is not a session branch's commit in <paramref name="refs"/> is asked of git on its own.
    /// </summary>
    internal static async Task<bool?> HeldAsync(string root, string tip, RefSnapshot? refs, CancellationToken ct)
    {
        if (refs is not null && await refs.Unheld.ReadAsync(root, refs, ct).ConfigureAwait(false) is { } read && read.Tips.Contains(tip))
        {
            return !read.Open.Contains(tip);
        }

        return await TreeFollows.UnlandedAsync(root, tip, ct).ConfigureAwait(false) is { } unlanded ? unlanded == 0 : null;
    }

    private Task<(HashSet<string> Tips, HashSet<string> Open)?> ReadAsync(string root, RefSnapshot refs, CancellationToken ct)
    {
        lock (_gate)
        {
            // A read that threw or was cancelled, or that git could not give, is asked again.
            if (_read is { } kept && !kept.IsFaulted && !kept.IsCanceled && !(kept.IsCompletedSuccessfully && kept.Result is null)) return kept;
            return _read = LoadAsync(root, refs, ct);
        }
    }

    private static async Task<(HashSet<string> Tips, HashSet<string> Open)?> LoadAsync(string root, RefSnapshot refs, CancellationToken ct)
    {
        if (await refs.ListedAsync(ct).ConfigureAwait(false) is not { } listed) return null;
        var tips = listed.Entries
            .Where(entry => entry.Type == "commit" && entry.Name.StartsWith($"refs/heads/{TreeFollows.SessionPrefix}", StringComparison.Ordinal))
            .Select(entry => entry.Object)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (tips.Count == 0) return (tips, []);
        // The same branches the landing check counts against (`TreeFollows.UnlandedLogAsync`): a local branch outside `daoris/` or a remote-tracking one outside `*/daoris/`.
        var (code, open, _) = await WorkingTree.GitAsync(
            root, ["rev-list", $"--branches={TreeFollows.SessionPrefix}*", "--not", $"--exclude={TreeFollows.SessionPrefix}*", "--branches", "--exclude=*/daoris/*", "--remotes"], ct)
            .ConfigureAwait(false);
        if (code != 0) return null;
        return (tips, open.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase));
    }
}
