namespace Daoris.Driver;

/// <summary>The landing plan's per-commit judgement, read against the local line for a workflow's bound work.</summary>
internal sealed record LineHolding(string Name, LineHeld Held)
{
    public bool HoldsAll => Held.Commits.Count > 0 && Held.Kept.Count == 0;

    public string Said => $"Reached on the line `{Name}` at `{TextBounds.ShortCommit(Held.Onto, 8)}`: "
        + $"it already holds all {Held.Commits.Count} commit(s) "
        + (Held.LeftOut.All(each => each.Why == LeftOutWhy.Patch) ? "as the same change." : "by content or as the same change.");

    internal static async Task<LineHolding?> ReadAsync(TreeContext context, string tree, string tip, CancellationToken ct)
    {
        var here = await TreeGit.CommonDirAndBranchAsync(tree, ct).ConfigureAwait(false);
        if (here.Code != 0) return null;
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(here.CommonDir.Trim()))!;
        var (workspace, repository) = context.OwnerOf(Path.GetFullPath(tree));
        if ((await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch is not { } line
            || await TreeRefs.CommitOfAsync(root, $"refs/heads/{line}", ct, RefSnapshot.Take(root)).ConfigureAwait(false) is not { } onto
            || await LineHolds.JudgeAsync(root, tip, onto, line, ct).ConfigureAwait(false) is not { } held)
        {
            return null;
        }

        return new(line, held);
    }
}
