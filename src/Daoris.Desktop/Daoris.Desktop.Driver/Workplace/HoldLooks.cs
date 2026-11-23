using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>
/// The look's tidy's read of a tree under the repository's hold (<see cref="TidyHold"/>): only what can have changed since the judgement it
/// made with the repository free, and no walk of the tree. The branch's tip, the repository's worktree list, the sessions asked again, and
/// the tree's change stamps (<see cref="TreeStamp"/>), each a read of a few refs, one git listing or one folder's top, so the hold lasts
/// for the writes that remove the tree and a promotion waiting on it is not kept out while a large tree is read again.
/// </summary>
internal static class HoldLooks
{
    /// <summary>
    /// Why the tree is kept, as the result's code, sentence and cause, where any read says it changed since its judgement; null where it
    /// stands as judged. What the judgement rested on besides the tip is not read again: its removal verifies each ref it rested on, at the
    /// commit it read, in the one transaction that deletes the branch, and a ref moved since keeps the branch there.
    /// </summary>
    /// <param name="judged">The row as its judgement made it, at the commit <paramref name="tip"/>.</param>
    /// <param name="stamp">The tree's stamps taken before that judgement began; null where its folder is not here.</param>
    /// <param name="busy">The trees sessions running or waiting name, asked under the hold.</param>
    internal static async Task<(string Code, string Sentence, string? Cause)?> AskAsync(
        TreeContext context, string root, SweepItem judged, string tip, TreeStamp? stamp, IReadOnlySet<string> busy, CancellationToken ct)
    {
        const string changed = "it changed since the list, and is kept";
        var tree = judged.Tree;
        if (tree is not null && busy.Select(Normal).Contains(Normal(tree), StringComparer.OrdinalIgnoreCase)) return (TidyKept.Refused, changed, null);

        var refs = RefSnapshot.Take(root);
        var (known, atTip) = await refs.CommitAsync($"refs/heads/{judged.Branch}", ct).ConfigureAwait(false);
        if (!known || !string.Equals(atTip, tip, StringComparison.OrdinalIgnoreCase)) return (TidyKept.Refused, changed, null);

        if (await context.Worktrees.ListAsync(root, ct, refs).ConfigureAwait(false) is not { } listed) return (TidyKept.Unread, $"{ListUnread}, so it is kept", TidyCause.List);
        var worktree = listed.FirstOrDefault(entry => string.Equals(entry.Branch, judged.Branch, StringComparison.Ordinal));
        var same = tree is null ? worktree is null : worktree is not null && SamePath(worktree.Path, tree);
        if (!same) return (TidyKept.Refused, changed, null);
        if (worktree is { Locked: true } locked)
        {
            return (TidyKept.Refused, "its tree is locked" + (locked.LockReason is { Length: > 0 } reason ? $" ({reason})" : "")
                + "; `git worktree unlock` lets it go", null);
        }

        if (tree is null) return null;
        if (context.Worktrees.LinkBelowTreesRoot(tree) is not null) return (TidyKept.Linked, "its tree is reached through a link under the trees home", null);
        if (stamp is null || !Directory.Exists(tree)) return null;
        if (TreeStamp.Take(tree) is not { } now) return (TidyKept.Unread, "its tree's folder could not be read again", TidyCause.Folders);
        return now.Differences(stamp) is { } difference ? (TidyKept.Refused, difference, null) : null;
    }
}
