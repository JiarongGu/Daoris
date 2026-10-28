namespace Daoris.Driver;

/// <summary>
/// The parts of a tree's landing gate the review owns, as calls on the trees (XAGENT1f; REVIEWENV1c): the whole gate as the record every
/// door reads, the review's part and the second opinion's. The workplace reads the gate as <see cref="IWorkplaceGate"/>
/// (<see cref="SessionTrees.GateOfAsync"/>) and the review's <see cref="LandingGate"/> is the only gate there is, so each call keeps the
/// spelling its callers had on <see cref="SessionTrees"/>.
/// </summary>
public static class SessionTreeGates
{
    /// <summary>
    /// The whole landing gate for this tree (XAGENT1f, D155 point 9; the second-agent design §7): D154's look as <see cref="ReviewAsync"/>
    /// reads it, and the second opinion before it. Every door that lands reads it here, once, and hands it to
    /// <see cref="SessionTrees.LandAsync"/>, which refuses the first part that holds (<see cref="TreeGates.GateAsync"/>).
    /// </summary>
    public static async Task<LandingGate> GateAsync(
        this SessionTrees trees, string path, string? quest, IOpinionWorld world, string? session = null, CancellationToken ct = default,
        IWorkplaceProcess? process = null, string? tip = null, string? run = null) =>
        // The workflows assemble every gate through the workplace's port, and the gate they assemble is the review's record.
        (LandingGate)await trees.GateOfAsync(path, quest, world, session, ct, process, tip, run).ConfigureAwait(false);

    /// <summary>
    /// The review's gate for this tree (REVIEWENV1c, D154 point 7; <see cref="ReviewGate.ReadAsync"/>): the chain its quest is a step
    /// of, the review rule standing for the tree's repository here, and git's ancestry from the tree's <c>HEAD</c>. Every door that
    /// lands asks it and hands it to <see cref="SessionTrees.LandAsync"/>; a conversation, with no quest, has nothing to wait for.
    /// </summary>
    public static Task<ReviewGateState> ReviewAsync(
        this SessionTrees trees, string path, string? quest, IReviewWorld world, CancellationToken ct = default)
    {
        var context = trees.Context;
        var full = Path.GetFullPath(path);
        var (workspace, repository) = context.OwnerOf(full);
        return ReviewGate.ReadAsync(world, context.Config(), full, repository, workspace, quest, ct);
    }

    /// <inheritdoc cref="TreeGateOpinions.OpinionAsync"/>
    public static Task<OpinionGateState> OpinionAsync(
        this SessionTrees trees, string tree, string repository, string workspace, string? quest, string? session, IOpinionWorld world,
        ReviewGateState? review, string occasion, CancellationToken ct = default, IWorkplaceProcess? process = null, string? at = null,
        string? run = null) =>
        trees.Gates.OpinionAsync(tree, repository, workspace, quest, session, world, review, occasion, ct, process, at, run);
}
