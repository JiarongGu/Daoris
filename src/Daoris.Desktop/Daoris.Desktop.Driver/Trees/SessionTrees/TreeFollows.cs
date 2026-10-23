using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// A tree its agent moved onto a branch of its own is followed (TREEFOLLOW1, D164 point 4; PART10l, out of
/// <c>SessionTrees.Follow.cs</c>): the agent ran <c>git checkout -b feature/…</c> in its tree and committed there, as its
/// repository's own rules ask, so the session branch the layout names (<c>daoris/&lt;tree folder&gt;</c>) holds nothing and the
/// work is on a branch the layout never heard of. The tree's branch is learned from the tree, never assumed from its folder's
/// name. With the layout's own answer (<see cref="BranchOfTree"/>, <see cref="FindBranches"/>) and D88's count of what no branch
/// of the person's holds (<see cref="UnlandedAsync"/>), which every other landing class reads.
/// </summary>
internal sealed class TreeFollows(TreeContext context)
{
    /// <summary>
    /// The prefix every session branch Daoris makes carries (<see cref="SessionTrees.OpenAsync"/>): the layout's name for a tree's
    /// branch, which this class answers, and the only kind a removal takes.
    /// </summary>
    internal const string SessionPrefix = "daoris/";

    /// <summary>
    /// The branch a session tree is on where it is one the session made: any branch but a session branch (<c>daoris/…</c>), the
    /// layout's name for it. Null where the tree is on a session branch, detached, or git cannot say, which is the layout's
    /// answer then (<see cref="BranchOfTree"/>). The caller has a tree of its own: git walks up from a folder that is none.
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
    internal static async Task<int?> UnlandedInTreeAsync(string tree, string revision, CancellationToken ct = default) =>
        await UnlandedAsync(tree, revision, ct, await OwnBranchOfAsync(tree, ct).ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>
    /// How many commits on <paramref name="revision"/> no branch of the person's holds (D88) — a local
    /// branch outside <c>daoris/</c>, or a remote-tracking one. Zero is landed; null is git unable to say.
    /// </summary>
    /// <param name="except">A local branch not counted as holding them (TREEFOLLOW1): the tree's own branch, which holds its own commits.</param>
    internal static async Task<int?> UnlandedAsync(string cwd, string revision, CancellationToken ct = default, string? except = null)
    {
        var (code, log, _) = await UnlandedLogAsync(cwd, revision, ct, except).ConfigureAwait(false);
        return code != 0 ? null : log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    /// <summary>The commits <see cref="UnlandedAsync"/> counts, one line each, as git lists them.</summary>
    internal static Task<(int Code, string Stdout, string Stderr)> UnlandedLogAsync(
        string cwd, string revision, CancellationToken ct, string? except = null) =>
        WorkingTree.GitAsync(cwd,
            except is null
                ? ["log", "--oneline", revision, "--not", "--exclude=daoris/*", "--branches", "--exclude=*/daoris/*", "--remotes"]
                : ["log", "--oneline", revision, "--not", "--exclude=daoris/*", $"--exclude={except}", "--branches", "--exclude=*/daoris/*", "--remotes"],
            ct);

    /// <summary>
    /// The branch a session tree is on now, for whoever names it: the tree's own where its agent moved it, the ledger's memory of
    /// that where the tree is gone, else the layout's name. Null for a path that is no tree of this home's.
    /// </summary>
    internal async Task<string?> BranchNowAsync(string tree, CancellationToken ct = default)
    {
        if (BranchOfTree(tree) is not { } layout) return null;
        if (Directory.Exists(tree) && await WorkingTree.IsTopLevelAsync(tree, ct).ConfigureAwait(false))
        {
            return await OwnBranchOfAsync(tree, ct).ConfigureAwait(false) ?? layout.Branch;
        }

        // A removed branch's trace keeps where its tree went (FABRIC-END4a, second review), so it is still known once the tree is gone.
        return context.Grown.Following(layout.Repository, layout.Branch)?.Now ?? layout.Branch;
    }

    /// <summary>
    /// The look learns where each session tree of this repository is (TREEFOLLOW1): a tree on a branch other than the layout's has
    /// it written on its session branch's ledger entry, so it is known after the tree goes, and one that came back has it cleared.
    /// Reads each tree and writes only that field; an entry the ledger has none for is left, as the tree's work is judged live.
    /// </summary>
    internal async Task LearnTreesAsync(string workspace, string repository, CancellationToken ct)
    {
        // Both places of the trees (TREEPLACE1): a tree opened before the home's layout moved stays at the old.
        var folders = context.TreesRoots.Select(root => Path.Combine(root, workspace, repository)).Where(Directory.Exists).ToList();
        if (folders.Count == 0) return;
        try
        {
            foreach (var tree in folders.SelectMany(Directory.EnumerateDirectories))
            {
                // A linked tree's `.git` is a file; a folder with none is a leftover, which git would answer for the repository above.
                if (!File.Exists(Path.Combine(tree, ".git"))) continue;
                var layout = SessionPrefix + Path.GetFileName(tree);
                // The standing entry, else the trace the tidy left of a removed branch whose tree lives on (FABRIC-END4a, second review).
                if (context.Grown.Following(repository, layout) is not { } entry) continue;
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
                // Written in place: a trace stays a trace, never recorded standing (and pending) again.
                if (!string.Equals(entry.Now, learned, StringComparison.Ordinal)) context.Grown.Follow(repository, layout, learned);
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
    internal string? MovedOnTo(IReadOnlyList<WorktreeEntry> entries, string workspace, string repository, string branch)
    {
        if (!branch.StartsWith(SessionPrefix, StringComparison.Ordinal)) return null;
        var folder = context.TreeAt(workspace, repository, branch[SessionPrefix.Length..]);
        return entries.FirstOrDefault(entry => SamePath(entry.Path, folder)) is { Branch: { } on } && !string.Equals(on, branch, StringComparison.Ordinal)
            ? on
            : null;
    }

    /// <summary>The session branches the record holds by that name, its whole name (<c>daoris/s-…</c>) or its tree's (<c>s-…</c>).</summary>
    internal IReadOnlyList<GrownBranch> FindBranches(string named, string? repository) =>
        [.. context.Grown.All()
            .Where(entry => string.Equals(entry.Branch, named, StringComparison.Ordinal)
                            || string.Equals(entry.Branch, SessionPrefix + named, StringComparison.Ordinal))
            .Where(entry => repository is null || string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// The session branch a tree this home opened was on, from the layout Daoris chose: the tree's name under its repository
    /// is the branch's after <c>daoris/</c>. What finds a session's branch once its tree is gone; null for a path that is no
    /// tree of this home's.
    /// </summary>
    internal (string Workspace, string Repository, string Branch)? BranchOfTree(string tree)
    {
        var full = Path.GetFullPath(tree);
        if (!context.Holds(full)) return null;
        var (workspace, repository) = context.OwnerOf(full);
        return repository.Length == 0
            ? null
            : (workspace, repository, SessionPrefix + Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar)));
    }
}
