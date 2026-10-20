using System.Text.Json;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// A session's tree locked while the session holds it (TREELOCK1, D51's note): git's own lock (<c>git worktree lock</c>), its reason
/// naming the session, so no tool that prunes merged branches or removes clean trees takes the tree from under it.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> A session's tree opens on a branch at its line's tip and has no commit until the session has work, so its branch
/// reads as merged and its tree as clean: a repository's own merge tool removed one mid-session (the FIX-LOG's 2026-10-20 DOGFOOD1
/// entry). git refuses a locked tree to <c>git worktree prune</c> and to a <c>git worktree remove</c> without <c>--force</c> twice,
/// whoever asks.</para>
///
/// <para><b>Held from the record's open to its end.</b> A driven start locks its tree once its record is open, while it still holds the
/// repository's trees for starting (<see cref="TreeLock"/>), and lets it go when the run ends, unless the record waits on the person
/// or goes on at the next look, which still hold it. A start in a tree an earlier session locked takes the lock over in its own name.
/// The look lets go of a lock whose session no longer holds its tree, so a record ended elsewhere (an orphan's sweep, a person's end
/// of a park) never keeps one, and does so before its own tidy, which refuses a locked tree.</para>
///
/// <para><b>Never a lock of anyone else's.</b> A lock whose reason names no Daoris session, a person's or an opening's
/// (<see cref="TreeOpenings"/>), is never changed or lifted here: it holds the tree already.</para>
/// </remarks>
internal static class SessionLocks
{
    /// <summary>What a session lock's reason starts with; the session's id follows.</summary>
    private const string Prefix = "daoris session ";

    /// <summary>The reason a session's lock gives, which <c>git worktree list</c> shows a person.</summary>
    internal static string Reason(string session) => Prefix + session;

    /// <summary>The session a lock's reason names; null where the reason is no session lock's.</summary>
    internal static string? SessionOf(string? reason) =>
        reason is { } said && said.StartsWith(Prefix, StringComparison.Ordinal) && said.Length > Prefix.Length ? said[Prefix.Length..] : null;

    /// <summary>
    /// The session whose lock holds <paramref name="tree"/>, read from git's own file with no git run (FABRIC-PIPELINE1b, the pipeline
    /// design §3.2's second layer): a linked tree's <c>locked</c> in its folder under the checkout's git folder, whose text is the reason
    /// <c>git worktree lock --reason</c> wrote. Null where the tree is no linked tree, is not locked, or its lock names no session.
    /// </summary>
    internal static string? HolderOf(string tree)
    {
        if (!File.Exists(Path.Combine(tree, ".git")) || GitBranches.GitDirectory(tree) is not { } git) return null;
        try
        {
            var locked = Path.Combine(git, "locked");
            return File.Exists(locked) ? SessionOf(File.ReadAllText(locked).Trim()) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Lock <paramref name="tree"/> for <paramref name="session"/>: null where it is locked now, for it or by a lock of anyone else's;
    /// otherwise git's reason. A lock an earlier session's left is taken over.
    /// </summary>
    internal static async Task<string?> HoldAsync(string tree, string session, CancellationToken ct)
    {
        if (await EntryAsync(tree, ct).ConfigureAwait(false) is not { } entry) return $"git lists no tree at {tree}";
        if (entry.Locked)
        {
            if (entry.LockReason == Reason(session) || SessionOf(entry.LockReason) is null) return null;
            var (unlocked, _, unlockErr) = await WorkingTree.GitAsync(tree, ["worktree", "unlock", entry.Path], ct).ConfigureAwait(false);
            if (unlocked != 0) return FirstLine(unlockErr);
        }

        var (code, _, err) = await WorkingTree.GitAsync(tree, ["worktree", "lock", "--reason", Reason(session), entry.Path], ct)
            .ConfigureAwait(false);
        return code == 0 ? null : FirstLine(err);
    }

    /// <summary>
    /// <paramref name="session"/> lets go of <paramref name="tree"/>: its lock is lifted where it is that session's, and anything else
    /// is left as it is. Whether a lock of that session's was lifted.
    /// </summary>
    internal static async Task<bool> LetGoAsync(string tree, string session, CancellationToken ct)
    {
        if (!Directory.Exists(tree)) return false;
        if (await EntryAsync(tree, ct).ConfigureAwait(false) is not { Locked: true } entry || entry.LockReason != Reason(session)) return false;
        var (code, _, _) = await WorkingTree.GitAsync(tree, ["worktree", "unlock", entry.Path], ct).ConfigureAwait(false);
        return code == 0;
    }

    /// <summary>
    /// The look's half: each of the repository's trees under <paramref name="trees"/> that a session lock holds while no session running
    /// or waiting holds it, let go of. Judged again with the repository's trees held alone, so a start locking its tree meanwhile is
    /// seen holding it. The trees let go of; none where git could not list them, the service did not answer, or a start held them.
    /// </summary>
    /// <param name="trees">The repository's folder under the trees home, which holds its session trees.</param>
    internal static async Task<IReadOnlyList<string>> LetGoUnheldAsync(
        string home, string root, string trees, string workspace, string repository, Func<CancellationToken, Task<IReadOnlySet<string>>> inUse,
        CancellationToken ct)
    {
        // Asked before any git, so a repository with nothing under its trees costs nothing.
        if (!Directory.Exists(trees) || !Directory.EnumerateDirectories(trees).Any()) return [];
        if (await LockedAsync(root, trees, ct).ConfigureAwait(false) is not { Count: > 0 } first) return [];
        if (await BusyAsync(inUse, ct).ConfigureAwait(false) is not { } before || first.All(entry => before.Contains(Normal(entry.Path)))) return [];

        using var hold = TreeLock.TryReplaying(home, workspace, repository);
        if (hold is null) return [];
        if (await BusyAsync(inUse, ct).ConfigureAwait(false) is not { } busy) return [];
        var let = new List<string>();
        foreach (var entry in await LockedAsync(root, trees, ct).ConfigureAwait(false) ?? [])
        {
            if (busy.Contains(Normal(entry.Path))) continue;
            var (code, _, _) = await WorkingTree.GitAsync(root, ["worktree", "unlock", entry.Path], CancellationToken.None).ConfigureAwait(false);
            if (code == 0) let.Add(entry.Path);
        }

        return let;
    }

    /// <summary>The trees under <paramref name="trees"/> a session lock holds, as git lists them; null where git could not.</summary>
    private static async Task<List<WorktreeEntry>?> LockedAsync(string root, string trees, CancellationToken ct)
    {
        if (await TreeWorktrees.EntriesAsync(root, ct).ConfigureAwait(false) is not { } entries) return null;
        var under = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trees)) + Path.DirectorySeparatorChar;
        return [.. entries.Where(entry => entry.Locked && SessionOf(entry.LockReason) is not null
            && Path.GetFullPath(entry.Path).StartsWith(under, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>The tree's own entry in git's list, asked in the tree; null where git lists none there.</summary>
    private static async Task<WorktreeEntry?> EntryAsync(string tree, CancellationToken ct) =>
        await TreeWorktrees.EntriesAsync(tree, ct).ConfigureAwait(false) is { } entries
            ? entries.FirstOrDefault(entry => SamePath(entry.Path, tree))
            : null;

    /// <summary>The trees sessions running or waiting name, each as it is on disk; null where the service did not say.</summary>
    private static async Task<HashSet<string>?> BusyAsync(Func<CancellationToken, Task<IReadOnlySet<string>>> inUse, CancellationToken ct)
    {
        try
        {
            return (await inUse(ct).ConfigureAwait(false)).Select(Normal).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException)
        {
            return null;
        }
    }
}
