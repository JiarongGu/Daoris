using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>
/// The run an evaluation judges, and what its caller supplies. Nothing here is stored by the evaluation: the fired
/// triggers are the record writer's to keep, as <see cref="NodeAcceptances.Keep"/>'s results are.
/// </summary>
/// <param name="GraphDigest">The graph that binds the run.</param>
/// <param name="Run">The run's id: a skip and a transition receipt hold for this graph and run alone.</param>
public sealed record WireRun(string GraphDigest, string Run)
{
    public NodeScope Scope => new(GraphDigest, Run);

    /// <summary>The run's own branch where it has one: the work the line may already hold, which the quest's holds read before they wait.</summary>
    public WorkUnit? RunBranch { get; init; }

    /// <summary>The clock a start is stamped with, so a node says since when it waits; null stamps none.</summary>
    public Func<DateTimeOffset>? Clock { get; init; }

    /// <summary>
    /// Each node's place on a live graph, by node id (<see cref="NodeGraphs.Places"/>): a node named here is
    /// scoped, bound, receipted and fired on by its place where every other is by <see cref="GraphDigest"/>, so an edit elsewhere in the graph
    /// leaves it current. Empty on a named version's run, which is never edited.
    /// </summary>
    public IReadOnlyDictionary<string, string> Places { get; init; } = ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// The run's earlier name, worked out from the graph now (<see cref="NodeRuns.Of(string, string)"/>): what a whole-graph scope
    /// names, so a record naming the whole graph's digest is judged under it until the graph is edited. Null where the run has no other name.
    /// </summary>
    public string? Legacy { get; init; }

    /// <summary>What <paramref name="node"/> is bound and scoped to now: its place where it has one, else the whole graph's digest.</summary>
    public string GraphOf(string node) => Places.TryGetValue(node, out var place) ? place : GraphDigest;

    /// <summary>The scope a start or a skip of <paramref name="node"/> takes now: its place, or the graph, and the run.</summary>
    public NodeScope ScopeOf(string node) => new(GraphOf(node), Run);

    /// <summary>
    /// The scope a record of <paramref name="node"/> bound or skipped under <paramref name="graph"/> is judged against (like with like): a record naming
    /// the whole graph's digest where the node now has a place is judged on the graph's digest and the run's earlier name; any other on
    /// <see cref="ScopeOf"/>.
    /// </summary>
    public NodeScope Own(string node, string? graph) =>
        Places.ContainsKey(node) && graph is not null && !graph.StartsWith(NodeGraphs.PlacePrefix, StringComparison.Ordinal)
            ? new(GraphDigest, Legacy ?? Run)
            : ScopeOf(node);

    /// <summary>
    /// Each unit's current revision, by <see cref="WorkUnit.Id"/>: a branch's full tip, null for a unit with none (a run's own). A complete
    /// snapshot of the units the nodes' statuses name: a record's unit that a node not named in <see cref="Subjects"/> acts on, left out or a
    /// branch named with none, refuses the evaluation. A named subject's is judged for its own nodes alone.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Revisions { get; init; } = ImmutableDictionary<string, string?>.Empty;

    /// <summary>
    /// A node's own revision where it is not its unit's, by node id: a branch unit's observed plugin node, bound to its
    /// branch's tip read in the caller's pass rather than the end fact's revision, which lags it; a run's node after <c>handed</c>, bound to its
    /// landing's tip as the landing's record keeps it. The node's binding is made and lapses on it; every other node on
    /// <see cref="Revisions"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string?> NodeRevisions { get; init; } = ImmutableDictionary<string, string?>.Empty;

    /// <summary>
    /// The unit each node observes where it is not its record's, by node id: a run's record
    /// keeps every node of its graph under <c>run:&lt;run&gt;</c>, each observing the quest, the branch or the run its kind names. The node is
    /// observed and bound there, and its binding names it (<see cref="NodeAcceptance.Subject"/>), lapsing as <see cref="NodeLapse.Subject"/>
    /// when it moves. A node named with null has no subject found this pass, and a branch subject with no revision handed in is not read:
    /// either is judged on its wires and offered no start (<see cref="WireEligibility.Withheld"/>), a started one neither observed nor lapsed,
    /// while the rest are evaluated. A node left out observes its record's unit.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Subjects { get; init; } = ImmutableDictionary<string, string?>.Empty;

    /// <summary>The target each step with one reads, by node id, at the tip read this pass: a start binds it (<see cref="WireTargets"/>).</summary>
    public IReadOnlyDictionary<string, NodePort> Targets { get; init; } = ImmutableDictionary<string, NodePort>.Empty;

    /// <summary>The trigger firings already recorded (<see cref="WireFiring.Key"/>): a trigger fires once per receipt, whatever replays.</summary>
    public IReadOnlySet<string> Fired { get; init; } = ImmutableHashSet<string>.Empty;

    /// <summary>How many performed attempts may run at once: a fork starts its targets within it.</summary>
    public int Limit { get; init; } = WireEvaluation.DefaultLimit;

    /// <summary>
    /// How many waited attempts may wait at once, apart from <see cref="Limit"/>: a parked <c>wait.unit</c> runs no process,
    /// so it never holds a performed node's slot.
    /// </summary>
    public int WaitLimit { get; init; } = WireEvaluation.DefaultWaitLimit;

    /// <summary>The units an observed built-in is read from, by id: where one is absent, its active node is not observed here.</summary>
    public IReadOnlyDictionary<string, WorkUnit> Units { get; init; } = ImmutableDictionary<string, WorkUnit>.Empty;

    /// <summary>
    /// The git reads an observation needs: <c>pushed</c>'s remote-tracking ref, and an ahead <c>pull-request</c>'s
    /// ancestry. Absent, those outcomes stay unknown, never guessed.
    /// </summary>
    public NodeGitReads? Git { get; init; }

    /// <summary>
    /// The second opinion's gate for each unit's work, by <see cref="WorkUnit.Id"/>, as <see cref="OpinionGate.JudgeAsync"/> judged it: the
    /// caller's read, which an <c>opinion</c> node observes (<see cref="OpinionNode"/>). A unit it leaves out has no judgement
    /// here, so its opinion stays pending, never guessed.
    /// </summary>
    public IReadOnlyDictionary<string, OpinionGateState> Opinions { get; init; } = ImmutableDictionary<string, OpinionGateState>.Empty;

    /// <summary>
    /// The review's gate for each unit's work, by <see cref="WorkUnit.Id"/>, as <see cref="ReviewGate"/> read it: the caller's read, which a
    /// <c>look</c> node observes only for the choice that turns the look off (<see cref="LookNode"/>). A unit it leaves out
    /// has no read, so its look stays the person's to answer.
    /// </summary>
    public IReadOnlyDictionary<string, ReviewGateState> Reviews { get; init; } = ImmutableDictionary<string, ReviewGateState>.Empty;

    /// <summary>The parts each unit's ask requires and has landed, by <see cref="WorkUnit.Id"/>: what a started <c>parts</c> node observes (<see cref="PartsNode"/>).</summary>
    public IReadOnlyDictionary<string, PartsReport> Parts { get; init; } = ImmutableDictionary<string, PartsReport>.Empty;

    /// <summary>
    /// The go-aheads each unit's work holds on its ask, by <see cref="WorkUnit.Id"/>, as the service answers them: the caller's read,
    /// which a started <c>go-ahead</c> node observes (<see cref="GoAheadNode"/>). A unit it leaves out has none read, so its
    /// go-ahead waits, never guessed.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<GoAheadView>> GoAheads { get; init; } = ImmutableDictionary<string, IReadOnlyList<GoAheadView>>.Empty;

    /// <summary>
    /// The quests each run's sessions published here, by <see cref="WorkUnit.Id"/>, as the exchange keeps them: the caller's read,
    /// which a started <c>work.requests</c> node observes (<see cref="WorkRequestsNode"/>). A unit it leaves out has none read, so its node stays
    /// active, never reached on a guess.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<QuestView>> Requests { get; init; } = ImmutableDictionary<string, IReadOnlyList<QuestView>>.Empty;
}
