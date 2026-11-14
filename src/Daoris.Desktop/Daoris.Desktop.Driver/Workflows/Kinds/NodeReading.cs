namespace Daoris.Driver;

/// <summary>
/// What the pass hands an observing kind: the unit its node observes, the node, the binding it is begun on, and the pass's reads of
/// that unit, each kind taking its own. The reads stay the pass's: a kind asks for nothing on its own, and one not read is none.
/// </summary>
public sealed record NodeReading(WorkUnit Unit, NodeDeclaration Node, NodeAcceptance Binding)
{
    /// <summary>The git reads a record cannot establish an outcome without: a push's remote-tracking ref, a hold's content.</summary>
    public NodeGitReads? Git { get; init; }

    /// <summary>The opinion gate's judgement of the unit's work.</summary>
    public OpinionGateState? Opinion { get; init; }

    /// <summary>The review gate's verdict on the unit's work.</summary>
    public ReviewGateState? Review { get; init; }

    /// <summary>The go-aheads the work's ask holds.</summary>
    public IReadOnlyList<GoAheadView>? GoAheads { get; init; }

    /// <summary>What the ask's plan requires and what landed of it; null where it is not read.</summary>
    public PartsReport? Parts { get; init; }

    /// <summary>The quests the work published.</summary>
    public IReadOnlyList<QuestView>? Requests { get; init; }

    /// <summary>The run's own branch, which a node on another subject (the quest's holds) reads the line's hold of; null where the run has none.</summary>
    public WorkUnit? RunBranch { get; init; }

    /// <summary>The tip <see cref="RunBranch"/> stands at in the run's pass; null where not read.</summary>
    public string? RunBranchTip { get; init; }

    /// <summary>The nodes wired straight into its node by <c>after</c>: the step a <c>monitor</c> watches.</summary>
    public IReadOnlyList<NodeDeclaration> Follows { get; init; } = [];

    /// <summary>
    /// The reading of <paramref name="node"/> on <paramref name="subject"/>, its reads taken from <paramref name="run"/> and the nodes it
    /// <paramref name="follows"/>.
    /// </summary>
    public static NodeReading Of(WireRun run, string subject, WorkUnit unit, NodeDeclaration node, NodeAcceptance binding, IReadOnlyList<NodeDeclaration> follows) =>
        new(unit, node, binding)
    {
        Follows = follows,
        Git = run.Git,
        RunBranch = run.RunBranch,
        RunBranchTip = run.RunBranch is { } branch ? run.Revisions.GetValueOrDefault(branch.Id) : null,
        Opinion = run.Opinions.GetValueOrDefault(subject),
        Review = run.Reviews.GetValueOrDefault(subject),
        GoAheads = run.GoAheads.GetValueOrDefault(subject),
        Requests = run.Requests.GetValueOrDefault(subject),
        Parts = run.Parts.GetValueOrDefault(subject),
    };
}
