namespace Daoris.Driver;

/// <summary>
/// What an abandon asks of the trees: the proof, the discard behind it, and what a kept tree holds for
/// its review. <see cref="SessionTrees"/> answers by git; a test hands in its own.
/// </summary>
public interface IWorkTrees
{
    /// <summary>Whether nothing this tree and its branch hold is anywhere else (<see cref="SessionTrees.OnlyHereAsync"/>).</summary>
    Task<TreeOnlyHere> OnlyHereAsync(string tree, string branch, string? baseCommit, string? root, CancellationToken ct = default);

    /// <summary>Discard a tree and its branch the proof cleared, or its branch alone where the tree is gone.</summary>
    Task<TreeRemoval> DiscardAsync(string tree, string branch, TreeOnlyHere judged, CancellationToken ct = default);

    /// <summary>What a kept tree holds for its review (<see cref="SessionTrees.WorkAsync"/>): the reader's proof.</summary>
    Task<TreeWork?> WorkAsync(string tree, CancellationToken ct = default);
}
