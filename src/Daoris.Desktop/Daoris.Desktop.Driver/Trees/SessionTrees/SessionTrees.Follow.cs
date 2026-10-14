namespace Daoris.Driver;

/// <summary>
/// A tree its agent moved onto a branch of its own, followed, and the layout's own answer of a tree's branch, as
/// <see cref="TreeFollows"/> (PART10l): built once here, with one-line public calls for the doors, the loop and the terminal.
/// PART10l-ii folds this part into the front.
/// </summary>
public sealed partial class SessionTrees
{
    private TreeFollows? _follows;

    /// <summary>Where each session tree's work is, and the branch the layout names for it (TREEFOLLOW1).</summary>
    private TreeFollows Follows => _follows ??= new(_context);

    /// <inheritdoc cref="TreeFollows.BranchNowAsync"/>
    public Task<string?> BranchNowAsync(string tree, CancellationToken ct = default) => Follows.BranchNowAsync(tree, ct);

    /// <inheritdoc cref="TreeFollows.FindBranches"/>
    public IReadOnlyList<GrownBranch> FindBranches(string named, string? repository) => Follows.FindBranches(named, repository);

    /// <inheritdoc cref="TreeFollows.BranchOfTree"/>
    public (string Workspace, string Repository, string Branch)? BranchOfTree(string tree) => Follows.BranchOfTree(tree);
}
