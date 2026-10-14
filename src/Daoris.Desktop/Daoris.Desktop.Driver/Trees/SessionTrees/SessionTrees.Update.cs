namespace Daoris.Driver;

/// <summary>
/// One session's tree brought up to date at the person's press, as <see cref="TreeUpdates"/> (PART10k): built once here from the
/// judging, the replay and the range the sync part holds, with its one-line public call. PART10l folds this part into the front.
/// </summary>
public sealed partial class SessionTrees
{
    private TreeUpdates? _updates;

    /// <summary>The press that brings one tree up to date (REVIEWBASE2-i).</summary>
    private TreeUpdates Updates => _updates ??= new(_context, Follows, Judging, Replays, Ranges);

    /// <inheritdoc cref="TreeUpdates.UpdateTreeAsync"/>
    public Task<TreeUpdate> UpdateTreeAsync(string tree, Func<CancellationToken, Task<bool>> running, CancellationToken ct = default) =>
        Updates.UpdateTreeAsync(tree, running, ct);
}
