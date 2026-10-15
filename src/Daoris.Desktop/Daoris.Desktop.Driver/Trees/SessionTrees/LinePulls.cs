using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>
/// A repository's line judged for its pull, and pulled (WSR6; PART10j, out of <c>SessionTrees.Sync.cs</c>): only ever a
/// fast-forward. Static, since none reads anything of the trees' context.
/// </summary>
internal static class LinePulls
{
    /// <summary>What pulling the line would do, and whether the line is checked out in the repository's own checkout.</summary>
    /// <param name="refs">The judgement's refs (PROC1d-ii); null asks git.</param>
    internal static async Task<(LinePull Pull, bool InRoot)> JudgePullAsync(
        string root, string repository, string workspace, string? line, string? fetched, CancellationToken ct, RefSnapshot? refs = null)
    {
        LinePull Pull(string kind, string? from = null, string? to = null, int commits = 0, string? detail = null) =>
            new(repository, workspace, line, kind, from, to, commits, fetched, detail);

        if (line is null) return (Pull(PullKind.NoLine), false);
        var remote = await CommitOfAsync(root, $"refs/remotes/origin/{line}", ct, refs).ConfigureAwait(false);
        if (remote is null) return (Pull(PullKind.NoRemote), false);
        var local = await CommitOfAsync(root, $"refs/heads/{line}", ct, refs).ConfigureAwait(false);
        if (local is null) return (Pull(PullKind.NoLocal, to: remote), false);
        if (local == remote) return (Pull(PullKind.UpToDate, local, remote), false);

        if (!await IsAncestorAsync(root, local, remote, ct).ConfigureAwait(false))
        {
            return await IsAncestorAsync(root, remote, local, ct).ConfigureAwait(false)
                ? (Pull(PullKind.Ahead, local, remote, await CountAsync(root, $"{remote}..{local}", ct).ConfigureAwait(false)), false)
                : (Pull(PullKind.Diverged, local, remote, await CountAsync(root, $"{local}..{remote}", ct).ConfigureAwait(false)), false);
        }

        var commits = await CountAsync(root, $"{local}..{remote}", ct).ConfigureAwait(false);
        var trees = await WorktreesAsync(root, ct, refs).ConfigureAwait(false);
        if (!trees.TryGetValue(line, out var tree)) return (Pull(PullKind.FastForward, local, remote, commits), false);
        if (!SamePath(tree, root)) return (Pull(PullKind.CheckedOut, local, remote, commits), false);

        var (clean, _) = await WorkingTree.CleanAsync(root, ct).ConfigureAwait(false);
        return clean
            ? (Pull(PullKind.FastForward, local, remote, commits), true)
            : (Pull(PullKind.Dirty, local, remote, commits), true);
    }

    /// <summary>
    /// The fast-forward (WSR6): in the repository's own checkout, a <c>merge --ff-only</c> while it is clean and on the
    /// line, both read immediately before; where the line is checked out nowhere, the ref moved only from the commit
    /// it was judged at. Never a merge commit, never a force.
    /// </summary>
    internal static async Task<PullResult> PullAsync(string root, LinePull pull, bool inRoot, CancellationToken ct)
    {
        var line = pull.Line!;
        var took = $"fast-forwarded `{line}` by {pull.Commits} commit(s) to `origin/{line}`.";
        if (inRoot)
        {
            // 🔴 The checkout's state, read as late as possible: somebody may be working in it right now.
            var (clean, detail) = await WorkingTree.CleanAsync(root, ct).ConfigureAwait(false);
            if (!clean)
            {
                return new(pull, false, $"the repository's own checkout is not clean ({detail}), so `{line}` stays where it is.");
            }

            var (_, onOut, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
            if (!string.Equals(onOut.Trim(), line, StringComparison.Ordinal))
            {
                return new(pull, false, $"the repository's checkout moved to `{onOut.Trim()}` since the list, so `{line}` was left for another look.");
            }

            if (await CommitOfAsync(root, "HEAD", ct).ConfigureAwait(false) != pull.From)
            {
                return new(pull, false, $"`{line}` moved since the list, so it was left for another look.");
            }

            var (code, _, err) = await WorkingTree.GitAsync(root, ["merge", "--ff-only", "--quiet", pull.To!], ct).ConfigureAwait(false);
            return code == 0
                ? new(pull, true, took)
                : new(pull, false, $"git would not fast-forward `{line}`: {Failure(err)} The checkout is as it was.");
        }

        // The ref alone, and only from the commit it was judged at: git refuses the move if it is elsewhere now.
        var (moved, _, moveErr) = await WorkingTree.GitAsync(
            root, ["update-ref", "-m", $"daoris: fast-forward {line} to origin/{line}", $"refs/heads/{line}", pull.To!, pull.From!], ct)
            .ConfigureAwait(false);
        return moved == 0
            ? new(pull, true, took)
            : new(pull, false, $"`{line}` moved since the list, so it was left: {Failure(moveErr)}");
    }
}
