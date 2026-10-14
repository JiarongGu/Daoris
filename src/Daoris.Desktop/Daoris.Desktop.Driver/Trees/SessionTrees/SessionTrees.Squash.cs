namespace Daoris.Driver;

/// <summary>
/// The squash a landing rule asks for, as <see cref="TreeSquashes"/> (PART10l): built once here, with the automatic landing's
/// one-line call. The front's branch form names its statics by that class. PART10l-ii folds this part into the front.
/// </summary>
public sealed partial class SessionTrees
{
    private TreeSquashes? _squashes;

    /// <summary>The squash's commit and the record's word of what it squashed (PROCESS1b-ii).</summary>
    private TreeSquashes Squashes => _squashes ??= new(_context);

    /// <inheritdoc cref="TreeSquashes.UnlandedHereAsync"/>
    public Task<int?> UnlandedHereAsync(string tree, CancellationToken ct = default) => Squashes.UnlandedHereAsync(tree, ct);
}
