using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// A tree its agent moved onto a branch of its own is followed (TREEFOLLOW1, D164 point 4): the agent ran
/// <c>git checkout -b feature/…</c> in its tree and committed there, as its repository's own rules ask, so the session branch
/// the layout names (<c>daoris/&lt;tree folder&gt;</c>) holds nothing and the work is on a branch the layout never heard of.
/// The tree's branch is learned from the tree, never assumed from its folder's name.
/// </summary>
public sealed partial class SessionTrees
{
    /// <summary>
    /// The branch a session tree is on where it is one the session made: any branch but a session branch (<c>daoris/…</c>), the
    /// layout's name for it. Null where the tree is on a session branch, detached, or git cannot say, which is the layout's
    /// answer then (SessionTrees.Tidy.cs's <see cref="BranchOfTree"/>). The caller has a tree of its own: git walks up from a
    /// folder that is none.
    /// </summary>
    internal static async Task<string?> OwnBranchOfAsync(string tree, CancellationToken ct)
    {
        try
        {
            // 🔴 git walks up from a folder that is no repository of its own, and would name the branch of the one above it.
            if (!await WorkingTree.IsTopLevelAsync(tree, ct).ConfigureAwait(false)) return null;
            return await WorkingTree.BranchAsync(tree, ct).ConfigureAwait(false) is { } branch
                   && !branch.StartsWith(SessionPrefix, StringComparison.Ordinal)
                ? branch
                : null;
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>
    /// <see cref="UnlandedAsync"/> of a session tree's commits: where the tree is on a branch of its own, that branch is not
    /// counted as holding them, since it is where they are. Zero then is work another branch of the person's holds too.
    /// </summary>
    public static async Task<int?> UnlandedInTreeAsync(string tree, string revision, CancellationToken ct = default) =>
        await UnlandedAsync(tree, revision, ct, await OwnBranchOfAsync(tree, ct).ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>
    /// The branch a session tree is on now, for whoever names it: the tree's own where its agent moved it, the ledger's memory of
    /// that where the tree is gone, else the layout's name. Null for a path that is no tree of this home's.
    /// </summary>
    public async Task<string?> BranchNowAsync(string tree, CancellationToken ct = default)
    {
        if (BranchOfTree(tree) is not { } layout) return null;
        if (Directory.Exists(tree) && await WorkingTree.IsTopLevelAsync(tree, ct).ConfigureAwait(false))
        {
            return await OwnBranchOfAsync(tree, ct).ConfigureAwait(false) ?? layout.Branch;
        }

        return Grown.Of(layout.Repository, layout.Branch)?.Now ?? layout.Branch;
    }

    /// <summary>
    /// The look learns where each session tree of this repository is (TREEFOLLOW1): a tree on a branch other than the layout's has
    /// it written on its session branch's ledger entry, so it is known after the tree goes, and one that came back has it cleared.
    /// Reads each tree and writes only that field; an entry the ledger has none for is left, as the tree's work is judged live.
    /// </summary>
    internal async Task LearnTreesAsync(string workspace, string repository, CancellationToken ct)
    {
        var folder = Path.Combine(TreesRoot, workspace, repository);
        if (!Directory.Exists(folder)) return;
        try
        {
            foreach (var tree in Directory.EnumerateDirectories(folder))
            {
                // A linked tree's `.git` is a file; a folder with none is a leftover, which git would answer for the repository above.
                if (!File.Exists(Path.Combine(tree, ".git"))) continue;
                var layout = SessionPrefix + Path.GetFileName(tree);
                if (Grown.Of(repository, layout) is not { } entry) continue;
                string? now;
                try
                {
                    now = await WorkingTree.BranchAsync(tree, ct).ConfigureAwait(false);
                }
                catch (DriverException)
                {
                    continue;
                }

                // A detached tree has no branch to follow: the layout's stays.
                if (now is null) continue;
                var learned = string.Equals(now, layout, StringComparison.Ordinal) ? null : now;
                if (!string.Equals(entry.Now, learned, StringComparison.Ordinal)) Grown.Record(entry with { Now = learned });
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A look that could not write what it learned learns it at the next one.
        }
    }

    /// <summary>
    /// The branch a session branch's tree is on where the tree moved off it (TREEFOLLOW1): git lists a tree at that folder, on a
    /// branch other than this one. Null where the tree is on this branch, detached, or not listed.
    /// </summary>
    private string? MovedOnTo(IReadOnlyList<WorktreeEntry> entries, string workspace, string repository, string branch)
    {
        if (!branch.StartsWith(SessionPrefix, StringComparison.Ordinal)) return null;
        var folder = Path.Combine(TreesRoot, workspace, repository, branch[SessionPrefix.Length..]);
        return entries.FirstOrDefault(entry => SamePath(entry.Path, folder)) is { Branch: { } on } && !string.Equals(on, branch, StringComparison.Ordinal)
            ? on
            : null;
    }
}
