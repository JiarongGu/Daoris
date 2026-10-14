namespace Daoris.Driver;

/// <summary>
/// Abandon's proof, as <see cref="OnlyHereProof"/> (PART10k): built once here, with one-line public calls for <see cref="IWorkTrees"/>,
/// which the front still answers. The records are in <c>Trees/Domain/TreeOnlyHere.cs</c> and <c>IWorkTrees.cs</c>. PART10l folds this part.
/// </summary>
public sealed partial class SessionTrees : IWorkTrees
{
    private OnlyHereProof? _onlyHere;

    /// <summary>Whether a tree's work is anywhere but Daoris's own branches, and its discard (PAUSE1d).</summary>
    private OnlyHereProof OnlyHere => _onlyHere ??= new(_context, this);

    /// <inheritdoc cref="OnlyHereProof.OnlyHereAsync"/>
    public Task<TreeOnlyHere> OnlyHereAsync(string tree, string branch, string? baseCommit, string? root, CancellationToken ct = default) =>
        OnlyHere.OnlyHereAsync(tree, branch, baseCommit, root, ct);

    /// <inheritdoc cref="OnlyHereProof.DiscardAsync"/>
    public Task<TreeRemoval> DiscardAsync(string tree, string branch, TreeOnlyHere judged, CancellationToken ct = default) =>
        OnlyHere.DiscardAsync(tree, branch, judged, ct);
}
