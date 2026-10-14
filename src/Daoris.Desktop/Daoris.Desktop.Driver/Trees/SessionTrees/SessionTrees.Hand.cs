namespace Daoris.Driver;

/// <summary>
/// A branch a landing made, handed to a landing plugin, as <see cref="TreeHandOffs"/> (PART10l): built once here, with one-line
/// public calls for the doors and the terminal. The records and the refusal's sentence (<see cref="TreeHand.NotLanded"/>) are in
/// <c>TreeHandOffs.cs</c>. PART10l-ii folds this part into the front.
/// </summary>
public sealed partial class SessionTrees
{
    private TreeHandOffs? _handOffs;

    /// <summary>The hand-off's look and its press (WSR5b).</summary>
    private TreeHandOffs HandOffs => _handOffs ??= new(_context);

    /// <inheritdoc cref="TreeHandOffs.HandPlanAsync"/>
    public Task<HandPlan> HandPlanAsync(string root, LandedBranch entry, string? plugin = null, CancellationToken ct = default) =>
        HandOffs.HandPlanAsync(root, entry, plugin, ct);

    /// <inheritdoc cref="TreeHandOffs.HandAsync"/>
    public Task<TreeHand> HandAsync(string root, LandedBranch entry, string? plugin = null, CancellationToken ct = default) =>
        HandOffs.HandAsync(root, entry, plugin, ct);
}
