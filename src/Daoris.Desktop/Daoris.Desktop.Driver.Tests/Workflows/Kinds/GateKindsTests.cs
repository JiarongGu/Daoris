using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GateFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The gates and the built-in observers as one set: what each compares in its binding, and the evaluation moving the gates on their records.</summary>
public sealed class GateKindsTests
{
    /// <summary>
    /// A run's node is kept on the run's record and observes the subject its kind names, so
    /// each observer compares its binding's subject, not its unit: <c>claimed</c> a quest, <c>handed</c>, the opinion and the look
    /// a branch, the go-ahead the run itself. A binding on another subject is no observation, and one naming none names its own unit, as
    /// every binding written before does.
    /// </summary>
    [Fact]
    public void Each_observer_compares_its_bindings_subject()
    {
        const string Run = "run:r";
        var quest = Assert.Single(QuestUnits.Read([new QuestView("q1", "game", "engine", "The work", "Body.", "Done") { Updated = Then }], []));
        var run = new WorkUnit(Run, WorkUnitKind.Run, new WorkOwner("engine", "default", null, WorkRecord.DriverWriter), new WorkSubject("r", null),
            new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "running"), []);
        var reviewed = Gate(ReviewStates.Reviewed, new QuestReviewVerdictView(ReviewVerdicts.Reviewed) { Commit = Tip, At = Then });
        var observers = new (string Kind, WorkUnit Unit, Func<WorkUnit, NodeAcceptance, NodeObservation> Observe)[]
        {
            (NodeKinds.Claimed, quest, (unit, binding) => NodeObserving.Observe(unit, Node(NodeKinds.Claimed), binding)),
            (NodeKinds.Land, Branch(Landed()), (unit, binding) => NodeObserving.Observe(unit, Node(NodeKinds.Land), binding)),
            (NodeKinds.Opinion, Branch(), (unit, binding) =>
                Reads.Opinion(unit, Node(NodeKinds.Opinion), binding, new OpinionGateState(OpinionGateStates.Settled, "engine") { Tip = Tip })),
            (NodeKinds.Look, Branch(), (unit, binding) => Reads.Look(unit, Node(NodeKinds.Look), binding, reviewed)),
            (NodeKinds.GoAhead, run, (unit, binding) => Reads.GoAhead(unit, Node(NodeKinds.GoAhead), binding, [Asked(Act, true)])),
        };

        foreach (var (kind, unit, observe) in observers)
        {
            var bound = new NodeAcceptance(Run, unit.Kind == WorkUnitKind.Branch ? Tip : unit.Subject.Revision, Scope.GraphDigest, kind, 1, 1)
            {
                Subject = unit.Id == Run ? null : unit.Id,
            };
            var seen = observe(unit, bound);
            Assert.Equal((kind, (string?)null, NodeStates.Reached, bound.Id), (kind, seen.Refused, seen.Outcome, seen.Acceptance?.Id));
            Assert.Equal((kind, NodeRefusal.NotBound), (kind, observe(unit, bound with { Subject = "unit-other" }).Refused));
            if (unit.Id != Run) Assert.Equal((kind, NodeRefusal.NotBound), (kind, observe(unit, bound with { Subject = null }).Refused));
        }
    }

    /// <summary>Through the evaluation: the look's gate and the go-ahead's answers handed in by unit move their nodes, and what is wired after them follows.</summary>
    [Fact]
    public void The_evaluation_moves_a_look_and_a_go_ahead_on_their_records_and_their_wires_follow()
    {
        NodeDeclaration[] nodes = [Node(NodeKinds.Look), Node(NodeKinds.GoAhead), new("land", NodeKinds.Land, 1, NodeReach.Observed)];
        Wire[] wires =
        [
            new("a", WireKinds.After, new WireEnd(NodeKinds.Look), new WireEnd("land")),
            new("b", WireKinds.After, new WireEnd(NodeKinds.GoAhead), new WireEnd("land")),
        ];
        var statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
        {
            [NodeKinds.Look] = Active(NodeKinds.Look),
            [NodeKinds.GoAhead] = Active(NodeKinds.GoAhead),
            ["land"] = NodeLifecycle.Start(Unit, "land"),
        };
        var run = new WireRun(Scope.GraphDigest, Scope.Run)
        {
            Revisions = new Dictionary<string, string?> { [Unit] = Tip },
            Units = new Dictionary<string, WorkUnit> { [Unit] = Branch() },
            Reviews = new Dictionary<string, ReviewGateState>
            {
                [Unit] = Gate(ReviewStates.Reviewed, new QuestReviewVerdictView(ReviewVerdicts.Reviewed) { Commit = Tip, At = Then }),
            },
            GoAheads = new Dictionary<string, IReadOnlyList<GoAheadView>> { [Unit] = [Asked(Act, true)] },
        };

        var evaluated = WireEvaluation.Evaluate(nodes, wires, statuses, run);

        Assert.Empty(evaluated.Refused);
        Assert.Equal((NodeStates.Reached, NodeStates.Reached, NodeStates.Ready),
            (evaluated.Statuses[NodeKinds.Look].State, evaluated.Statuses[NodeKinds.GoAhead].State, evaluated.Statuses["land"].State));
    }
}
