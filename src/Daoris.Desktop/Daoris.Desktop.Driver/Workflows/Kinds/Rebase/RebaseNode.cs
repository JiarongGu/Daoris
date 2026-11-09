namespace Daoris.Driver;

/// <summary>
/// A work's commits replayed onto a target in a tree of Daoris's own (<see cref="RebaseReplay"/>), what the records say may be settled
/// settled (<see cref="ReplaySettles"/>): <c>ready</c> with its commit, <c>conflicted</c> or <c>uncommitted</c> with the problem it could not
/// settle. It starts once its work is still (<see cref="RebaseWaits"/>), and its door performs it off the pass. Arriving: declared before the
/// palette offers it, so no saved graph holds one until the composition does.
/// </summary>
[NodeKind(NodeKinds.Rebase, NodeGroup.Landing, NodeKinds.Line, Arriving = true)]
internal sealed class RebaseNode : BuiltInNode, IObserves, IStarts, ITargets
{
    /// <summary>The input it replays and the outputs it carries.</summary>
    public const string Work = "work";

    public const string Commit = "commit";

    public const string Problem = "problem";

    /// <summary>Reached by git's answer, and failed by git's error. Never skipped, since a skip would read as replayed where nothing replayed.</summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Rebase, NodeReach.Performed, NodeSource.Git, NodeSource.Git, false, PersonSkips: false, MayEnd: false, Evidenced: true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Input;

    public override IReadOnlyList<NodeOptionField> Options { get; } = [NodeOptionFields.Text(RebaseOnto.Option), NodeOptionFields.Switch(RebaseOnto.Fetch)];

    public override IReadOnlyList<string> Outcomes => RebaseOutcomes.All;

    /// <summary>Its outcomes are its own three, which a declaration may name only as they are.</summary>
    public override bool TakesOutcomes(IReadOnlyList<string> outcomes) => outcomes.All(outcome => RebaseOutcomes.All.Contains(outcome, StringComparer.Ordinal));

    public override string NeverEndBecause => "its commit is put on nothing until a step it feeds moves it.";

    public override IReadOnlyDictionary<string, string> Inputs { get; } = new Dictionary<string, string>(StringComparer.Ordinal) { [Work] = WireValues.Work };

    public override IReadOnlyDictionary<string, string> Outputs { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal) { [Commit] = WireValues.Commit, [Problem] = WireValues.Problem };

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.Rebase);

    public override string? Title => "The rebase";

    public override NodeLook Look => NodeLook.Line;

    /// <summary>The target named by its <c>onto</c>: a branch by its name, else the line, which the run's branch grows from.</summary>
    public string? Target(NodeDeclaration node, string? line) => RebaseOnto.Of(node, line);

    public NodeObservation Observe(NodeReading reading) => RebaseReading.Observe(reading);

    /// <summary>Its door, which replays off the pass under the loop's lease and drain rules.</summary>
    public INodeDoor? Door(NodeDoorWiring wiring) => new RebaseDoor(wiring.Home, said => wiring.Say("rebase.run", said), wiring.MayStart);

    /// <summary>Begun in the pass once its work is still; while it is not, it stays ready, its wait said (<see cref="RebaseWaits"/>).</summary>
    public Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) => Task.FromResult<WorkPerformed?>(starting.Begin());

    /// <summary>A run's active <c>rebase</c> handed to its door once per node, attempt and tip, at the target's tip it is bound to.</summary>
    public void Keep(NodeKept kept)
    {
        if (kept.Doors.Door<IRebaseDoor>(Row.Kind) is not { } door || kept.Status.Binding is not { SubjectRevision: { } tip } binding) return;
        if (kept.Governed.Scope?.Branch is not { Records.Branch: { } grown } || binding.Target is not { Revision: { } onto } target) return;
        var watched = kept.Governed.Watched;
        door.Start(new RebaseStart(kept.Governed.Run, kept.Node.Id, kept.Status.Attempt,
            new RebaseWork(watched.Root, watched.Repository, watched.Workspace, grown.Branch, tip), new RebaseTarget(target.Ref, onto)));
    }
}
