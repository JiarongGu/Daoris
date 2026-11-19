using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A repository's local branches newest first, as the content proof's last comparison walks them: one listing for as long as the refs the
/// proof counts (<see cref="CountedRefs"/>) stand as they were. The listing is the repository's whichever tree asks, and a session branch,
/// which a live session moves at every commit, is never a holder, so a tree that moved alone asks git nothing for it.
/// </summary>
/// <remarks>
/// The look's stages walk a repository's trees at once, so one listing in flight is shared by every walk asking for the same refs: the
/// fan-out starts it once, not once per tree. It runs to its end; an asker's cancellation ends its own wait alone.
/// </remarks>
internal static class BranchesByDate
{
    private static readonly ConcurrentDictionary<string, (string Version, Task<(int Code, string Stdout, string Stderr)> Listed)> Kept = new(StringComparer.OrdinalIgnoreCase);

    internal static string[] Ask => ["for-each-ref", "--sort=-committerdate", "--format=%(objectname) %(refname)", "refs/heads/"];

    internal static void Forget() => Kept.Clear();

    /// <summary>
    /// The listing from <paramref name="cwd"/>'s repository: the one kept or in flight where its counted refs are the same, else git's,
    /// kept where it answered and the refs stood while git listed.
    /// </summary>
    internal static async Task<(int Code, string Stdout, string Stderr)> ReadAsync(string cwd, CancellationToken ct)
    {
        var common = RefFiles.CommonDirOf(cwd);
        var version = common is null ? null : CountedRefs.Read(common)?.Version;
        if (common is null || version is null) return await WorkingTree.GitAsync(cwd, Ask, ct).ConfigureAwait(false);
        while (true)
        {
            if (Kept.TryGetValue(common, out var kept) && kept.Version == version) return await kept.Listed.WaitAsync(ct).ConfigureAwait(false);
            var listing = new TaskCompletionSource<(int Code, string Stdout, string Stderr)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var mine = (version, listing.Task);
            var claimed = kept.Listed is null ? Kept.TryAdd(common, mine) : Kept.TryUpdate(common, mine, kept);
            if (!claimed) continue;
            _ = ListAsync(cwd, common, mine, listing);
            return await listing.Task.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>The one listing for <paramref name="mine"/>'s version, let go where git failed or a ref moved while git listed.</summary>
    private static async Task ListAsync(
        string cwd, string common, (string Version, Task<(int Code, string Stdout, string Stderr)> Listed) mine,
        TaskCompletionSource<(int Code, string Stdout, string Stderr)> listing)
    {
        (int Code, string Stdout, string Stderr) listed;
        try
        {
            listed = await WorkingTree.GitAsync(cwd, Ask, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            Kept.TryRemove(new KeyValuePair<string, (string, Task<(int, string, string)>)>(common, mine));
            listing.SetException(error);
            return;
        }

        if (listed.Code != 0 || CountedRefs.Read(common)?.Version != mine.Version)
        {
            Kept.TryRemove(new KeyValuePair<string, (string, Task<(int, string, string)>)>(common, mine));
        }

        listing.SetResult(listed);
    }
}
