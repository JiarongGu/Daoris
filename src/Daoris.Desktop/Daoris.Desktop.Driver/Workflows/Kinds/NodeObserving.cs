namespace Daoris.Driver;

/// <summary>
/// What every observing kind's reading shares: the dispatch to the kind's reading, the
/// refusals a reading makes before it reads, and the builders of an observation. Pure: nothing is judged again, nothing written.
/// </summary>
/// <remarks>
/// <para><b>Each fact has its one writer</b>: the landing writes <c>handed</c>'s entry; the end judgement's keeper,
/// <see cref="EndKeeping"/>, the kept fact <c>line</c> reads; the plugin answers' writers <c>Pushed</c> and <c>PullRequestState</c>;
/// the service, through its quest exchange, the done <c>claimed</c> reads. The look's tidy is no node, and what it removed is the
/// inventory's; a run's <c>tidy</c> is Daoris's performed step, read in its unit from what its door wrote on the landing.</para>
/// <para><b>Not yet is no outcome</b>: git's proofs never fail. A record that cannot establish the outcome without a git read
/// (<see cref="NodeGitReads"/>) leaves it unknown, and an unknown owner's unit is unknown, never ended.</para>
/// <para><b>Each kind's reading is its unit's</b>: a kind that observes says so (<see cref="IObserves"/>) and reads in its own folder.</para>
/// </remarks>
public static class NodeObserving
{
    /// <summary>The schema of an observed built-in's evidence, its record's: what a <c>data</c> wire from its evidence port carries.</summary>
    public static string EvidenceSchema(string kind) => $"daoris/{kind}@1";

    /// <summary>
    /// What <paramref name="unit"/>'s records say of <paramref name="node"/>, bound for <paramref name="attempt"/> under
    /// <paramref name="graphDigest"/>, where no wire evaluated a binding: no inputs and no prerequisite receipts.
    /// </summary>
    public static NodeObservation Observe(WorkUnit unit, NodeDeclaration node, string graphDigest, int attempt, NodeGitReads? git = null) =>
        Observe(unit, node, new NodeAcceptance(unit.Id, unit.Subject.Revision, graphDigest, node.Id, node.Version, attempt), git);

    /// <summary>
    /// What <paramref name="unit"/>'s records say of <paramref name="node"/> on the binding Daoris evaluated for its attempt, with
    /// <paramref name="git"/> the only read handed: its kind's reading (<see cref="IObserves"/>), or <see cref="NodeRefusal.NotObserved"/> for a
    /// kind that observes nothing.
    /// </summary>
    public static NodeObservation Observe(
        WorkUnit unit, NodeDeclaration node, NodeAcceptance binding, NodeGitReads? git = null, WorkUnit? runBranch = null, string? runBranchTip = null)
    {
        var reading = new NodeReading(unit, node, binding) { Git = git, RunBranch = runBranch, RunBranchTip = runBranchTip };
        return NodeKit.Of(node.Kind) is IObserves observes
            ? observes.Observe(reading)
            : Seen(reading, null, "this kind is not observed from records") with { Refused = NodeRefusal.NotObserved };
    }

    /// <summary>The landing a branch unit's <c>handed</c>, <c>pushed</c> and <c>pull-request</c> read: its newest standing, else its newest; null where none.</summary>
    public static LandedBranch? LandingOf(WorkUnit unit) =>
        unit.Records.Landings.LastOrDefault(each => each.GoneAt is null) ?? unit.Records.Landings.LastOrDefault();

    /// <summary>
    /// Whether <c>pushed</c>'s observation of <paramref name="landing"/> asks git's remote-tracking ref: only where a plugin
    /// answered that it pushed, naming the commit, so a reader asks git nothing for a landing no plugin pushed.
    /// </summary>
    public static bool AsksRemote(LandedBranch landing) => landing is { Pushed: true, PushedTip: not null };

    /// <summary>
    /// A branch unit's reading refused where it is not bound to its node, or is not a branch; unknown where no record names its unit, so nothing
    /// of it is said. Null where it reads.
    /// </summary>
    internal static NodeObservation? OfBranch(NodeReading reading) =>
        Unbound(reading, WorkUnitKind.Branch)
        ?? (reading.Unit.OwnerKnown ? null : Seen(reading, NodeStates.Unknown, "no record names its unit, so nothing of it is said"));

    /// <summary>A unit of another kind than the one the node observes is no observation, nor is one it is not bound to (<see cref="NotBound"/>).</summary>
    internal static NodeObservation? Unbound(NodeReading reading, string wants) =>
        NotBound(reading) ?? (reading.Unit.Kind == wants ? null
            : Seen(reading, null, $"it observes a {wants} unit, and this is a {reading.Unit.Kind}") with { Refused = NodeRefusal.NotThisUnit });

    /// <summary>
    /// A binding of another unit, node or version is no observation (<see cref="NodeRefusal.NotBound"/>): the unit it observes, its subject on a
    /// run's record, never the record's own. Null where it is bound.
    /// </summary>
    internal static NodeObservation? NotBound(NodeReading reading)
    {
        var (unit, node, binding) = (reading.Unit, reading.Node, reading.Binding);
        return binding.Observes != unit.Id || binding.Node != node.Id || binding.NodeVersion != node.Version
            ? Seen(reading, null, "its binding names another subject, node or version") with { Refused = NodeRefusal.NotBound }
            : null;
    }

    /// <summary>What the reading's node is observed as: <paramref name="outcome"/>, or none yet, with what its records say.</summary>
    internal static NodeObservation Seen(NodeReading reading, string? outcome, string said) =>
        new(reading.Unit.Id, reading.Node.Id, reading.Node.Kind, outcome, said) { Attempt = reading.Binding.Attempt };

    /// <summary>A reach by <paramref name="source"/>, its acceptance at <paramref name="revision"/> and its evidence the record's entry.</summary>
    internal static NodeObservation Reached(NodeReading reading, string source, string? revision, string record, string artifact, string said) =>
        ReachedAt(reading, source, revision, revision, record, artifact, said);

    /// <summary>
    /// A reach, its acceptance on the binding at <paramref name="revision"/> and its evidence naming <paramref name="evidenced"/>: apart only where
    /// a run's <c>handed</c> keeps its bound tip. A reach's acceptance carries that binding's graph, attempt, inputs and
    /// prerequisite receipts, so invalidating anything it rests on reaches it; where its revision is not the binding's, the one table refuses it.
    /// </summary>
    internal static NodeObservation ReachedAt(NodeReading reading, string source, string? revision, string? evidenced, string record, string artifact, string said)
    {
        var accepted = reading.Binding with { SubjectRevision = revision, Evidence = null, Outputs = NodeAcceptance.NoPorts };
        return Seen(reading, NodeStates.Reached, said) with
        {
            Source = source,
            Acceptance = accepted with { Evidence = new NodePort(EvidenceSchema(reading.Node.Kind), $"{record}#{artifact}", evidenced, accepted.Id) },
        };
    }
}
