namespace Daoris.Driver;

/// <summary>Where the landing gate's graph part stands for a run bound to a graph version (FABRIC-CUTOVER1d; the cutover design §4's table).</summary>
public static class GraphGateStates
{
    /// <summary>The run's last pass made its <c>handed</c> active on the branch the door lands, at the commit it would land: it lands there.</summary>
    public const string Lands = "lands";

    /// <summary>Its node record does not read, so nobody judges its graph until a person mends it.</summary>
    public const string Unread = "unread";

    /// <summary><c>handed</c> waits on a node before it, as the record says it stands.</summary>
    public const string Waits = "waits";

    /// <summary>Daoris has not judged the graph at the door's commit yet: <c>handed</c> ready, active at another commit, or the record behind.</summary>
    public const string NotJudged = "not-judged";

    /// <summary>The person stopped <c>handed</c>, or passed it over, with their words: this run does not land.</summary>
    public const string Stopped = "stopped";

    /// <summary>The door's tree is not the run's branch: its newest session's branch, which its graph judges, carries this work.</summary>
    public const string Elsewhere = "elsewhere";
}

/// <summary>
/// What the landing gate's graph part reads (FABRIC-CUTOVER1d): the run, its graph version and digest, its record as the runtime keeps it, and
/// the door's own tree, commit and gates.
/// </summary>
/// <param name="Run">The run: its chain's first quest in the repository, whose record is <c>run:&lt;run&gt;</c>.</param>
/// <param name="Graph">The graph version the run is bound to (<see cref="WorkflowProcess.Graph"/>).</param>
/// <param name="Digest">That version's digest, which every node of the run's record binds (its scope).</param>
/// <param name="Record">The run's record as read: empty where Daoris has not judged it yet, null where it does not read.</param>
public sealed record GraphGateFacts(string Run, NodeGraph Graph, string Digest, NodeRecord? Record)
{
    /// <summary>The branch unit of the door's tree, as this machine's session branches name it; null where none is known.</summary>
    public string? Unit { get; init; }

    /// <summary>The commit the door would land: the tip it pinned, else its tree's <c>HEAD</c>; null where git could not say.</summary>
    public string? Commit { get; init; }

    /// <summary>This machine's session branches, which name a branch unit by its branch.</summary>
    public IReadOnlyList<GrownBranch> Branches { get; init; } = [];

    /// <summary>Whether the door's own second-opinion part lets the work go at the commit, as it read it a moment before.</summary>
    public bool OpinionLetsGo { get; init; }

    /// <summary>Whether the door's own look part lets the work go at the commit, as it read it a moment before.</summary>
    public bool ReviewLetsGo { get; init; }
}

/// <summary>The graph part as judged: its state, and what its sentence names.</summary>
/// <param name="State">One of <see cref="GraphGateStates"/>.</param>
/// <param name="Run">The run it was judged for.</param>
public sealed record GraphGateState(string State, string Run)
{
    /// <summary>The graph's <c>handed</c>.</summary>
    public string Handed { get; init; } = "";

    /// <summary>The commit it lands, for <see cref="GraphGateStates.Lands"/>; the door's commit for any other, where it read one.</summary>
    public string? Commit { get; init; }

    /// <summary>The node <c>handed</c> waits on, for <see cref="GraphGateStates.Waits"/>, and its kind and what the run's view says of it.</summary>
    public string? Node { get; init; }

    public string? Kind { get; init; }

    public string? NodeSaid { get; init; }

    /// <summary>Whether the person may pass <see cref="Node"/> over with their words (its kind lets them, CUTOVER1f).</summary>
    public bool Skippable { get; init; }

    /// <summary>The branch the run's newest session works, for <see cref="GraphGateStates.Elsewhere"/>.</summary>
    public string? Branch { get; init; }

    /// <summary>For <see cref="GraphGateStates.Stopped"/>: whether the person passed it over rather than stopped it, and their words.</summary>
    public bool PassedOver { get; init; }

    public string? Words { get; init; }

    /// <summary>Whether the graph lets the work land.</summary>
    public bool LetsGo => State == GraphGateStates.Lands;

    /// <summary>Whether the door asks the loop for a pass that reads the units afresh (the cutover design §4): the record may move at it.</summary>
    public bool AsksPass => State is GraphGateStates.NotJudged or GraphGateStates.Waits;

    /// <summary>Whether a press waits for the pass it asked, within its bound: only for a judgement not made yet.</summary>
    public bool PressWaits => State == GraphGateStates.NotJudged;

    /// <summary>What it says: the terminal's line and every door's refusal.</summary>
    public string Says => GraphGate.Says(this);
}

/// <summary>
/// The landing gate's graph part (FABRIC-CUTOVER1d; D190 point 4, the cutover design §4): for a run bound to a graph version, read from the run's
/// record, never evaluated. The runtime begins a node only ready, eligible, bound and with no attempt unreconciled, so a <c>handed</c> active is the
/// last pass's judgement that the landing may start at the commit it is bound to, on the subjects, revisions and rules it read; a door that
/// evaluated the record itself would be a second engine. Pure: the door reads the record and its own facts, and this reads them alone.
/// </summary>
/// <remarks>
/// <para><b>One table</b> (§4): a record that does not read holds, said; the person's stop holds; a door whose tree is not the branch the run's
/// <c>handed</c> is bound to holds, naming the branch that carries the work; <c>handed</c> active in this run's scope on the door's branch at the
/// door's commit lands there, and the landing is pinned to that commit on every path; anything else is a judgement not made yet, unless
/// <c>handed</c> waits on a node before it that the door's own gates do not show answered since, which is named in the words the run's view gives
/// its state.</para>
/// <para><b>A pass asked.</b> A judgement not made yet, and a wait the next pass may move, ask the loop for a whole pass; a press waits for it,
/// within a bound, only for a judgement not made yet.</para>
/// </remarks>
public static class GraphGate
{
    /// <summary>The graph part for the door's facts.</summary>
    public static GraphGateState Judge(GraphGateFacts facts)
    {
        var handed = facts.Graph.Nodes.First(node => node.Kind == NodeKinds.Handed);
        var said = new GraphGateState(GraphGateStates.NotJudged, facts.Run) { Handed = handed.Id, Commit = facts.Commit };
        if (facts.Record is not { } record) return said with { State = GraphGateStates.Unread };

        var status = record.Statuses.GetValueOrDefault(handed.Id) ?? NodeLifecycle.Start(record.Unit, handed.Id);
        if (status.State == NodeStates.Stopped || (status.State == NodeStates.Skipped && status.By == NodeCause.Person))
        {
            return said with { State = GraphGateStates.Stopped, PassedOver = status.State == NodeStates.Skipped, Words = status.Words };
        }

        var bound = status.State == NodeStates.Reached ? status.Acceptance ?? status.Binding : status.Binding;
        var ours = bound is not null && string.Equals(bound.GraphDigest, facts.Digest, StringComparison.Ordinal);

        // Active on another branch: the run's newest session's, where the door's tree opened before it; a tree opened after it is newer work the
        // last pass has not read yet, a judgement not made.
        if (ours && status.State == NodeStates.Active && facts.Unit is { } unit && !string.Equals(bound!.Observes, unit, StringComparison.Ordinal))
        {
            var door = facts.Branches.LastOrDefault(entry => entry.Unit == unit)?.At;
            var carried = facts.Branches.LastOrDefault(entry => entry.Unit == bound.Observes)?.At;
            return door is { } opened && carried is { } newer && opened > newer
                ? said
                : said with { State = GraphGateStates.Elsewhere, Branch = BranchName(bound.Observes, facts.Branches) };
        }

        if (ours && status.State is NodeStates.Active or NodeStates.Reached && facts.Unit is { } tree && facts.Commit is { } commit
            && string.Equals(bound!.Observes, tree, StringComparison.Ordinal) && string.Equals(bound.SubjectRevision, commit, StringComparison.OrdinalIgnoreCase))
        {
            return said with { State = GraphGateStates.Lands };
        }

        if (status.State != NodeStates.Blocked || record.Statuses.Count == 0) return said;
        if (WaitedOn(facts.Graph, handed, record) is not { } waited) return said;
        var waitedStatus = record.Statuses.GetValueOrDefault(waited.Id) ?? NodeLifecycle.Start(record.Unit, waited.Id);

        // The door's own gates show the opinion or the look answered since the pass read them: a judgement its next pass makes.
        if (waitedStatus.State is NodeStates.Active or NodeStates.Unknown
            && ((waited.Kind == NodeKinds.Opinion && facts.OpinionLetsGo) || (waited.Kind == NodeKinds.Look && facts.ReviewLetsGo)))
        {
            return said;
        }

        return said with
        {
            State = GraphGateStates.Waits, Node = waited.Id, Kind = waited.Kind, NodeSaid = WorkflowGraphRuns.Said(waited, waitedStatus),
            Skippable = waitedStatus.State is NodeStates.Ready or NodeStates.Active && NodeAuthorities.Of(waited)?.PersonSkips == true,
        };
    }

    /// <summary>
    /// Whether a run's record has begun its every node of <paramref name="kind"/> (the cutover design §4: the gates' own engines wait for their
    /// node): each stands past ready, so its wires let it start and the runtime began it. True where the graph holds none; false where the record
    /// does not read.
    /// </summary>
    public static bool Begun(NodeGraph graph, NodeRecord? record, string kind) =>
        graph.Nodes.Where(node => node.Kind == kind).All(node => record is not null
            && record.Statuses.GetValueOrDefault(node.Id)?.State is { } state && state is not (NodeStates.Blocked or NodeStates.Ready));

    /// <summary>
    /// Whether <paramref name="process"/>'s run, where it is bound to a graph version, has begun its node of <paramref name="kind"/>, its record
    /// read with no lease and never written; true for every other run, whose gates' engines go on as before (FABRIC-CUTOVER1d).
    /// </summary>
    public static bool Begun(string home, WorkflowProcess process, string kind) =>
        process is not { Graph: { } graph, Run: { } run } || Begun(graph, new NodeRecordFiles(home, () => false).Read(RunUnits.IdOf(run)), kind);

    /// <summary>What the graph part says (the cutover design §4's table): the terminal's line, and every door's refusal where it holds.</summary>
    public static string Says(GraphGateState gate) => gate.State switch
    {
        GraphGateStates.Lands => $"Run {gate.Run}'s graph made `{gate.Handed}` ready to land at `{Short(gate.Commit)}`, so it lands there.",
        GraphGateStates.Unread =>
            $"Run {gate.Run}'s node record does not read, so its graph is judged by nobody until a person mends it, and nothing lands for it.",
        GraphGateStates.Waits => $"Run {gate.Run}'s graph waits on `{gate.Node}` ({gate.Kind}) before `{gate.Handed}`: {gate.NodeSaid}"
            + (gate.Skippable ? $" `daoris-driver workflow skip {gate.Run} {gate.Node} \"…\"` passes it over with your words." : ""),
        GraphGateStates.Stopped => $"You {(gate.PassedOver ? "passed over" : "stopped")} `{gate.Handed}`"
            + (gate.Words is { Length: > 0 } words ? $" with your words, \"{words}\"" : "") + $", so run {gate.Run} does not land.",
        GraphGateStates.Elsewhere =>
            $"This work is carried by run {gate.Run}'s newest session, on `{gate.Branch}`, whose branch its graph judges, and lands with it.",
        _ => $"Daoris has not judged run {gate.Run}'s graph at {(gate.Commit is null ? "this work's commit" : $"`{Short(gate.Commit)}`")} yet: its "
            + "next look does, and this asks for it now.",
    };

    /// <summary>
    /// The node <c>handed</c> waits on: of the nodes a wire path leads from into it, the first in the graph's order that is not settled and stands
    /// somewhere a move can come from (not blocked behind another); else the first not settled. Null where none is.
    /// </summary>
    private static NodeDeclaration? WaitedOn(NodeGraph graph, NodeDeclaration handed, NodeRecord record)
    {
        var before = new HashSet<string>(StringComparer.Ordinal);
        var next = new Stack<string>([handed.Id]);
        while (next.TryPop(out var at))
        {
            foreach (var wire in graph.Wires.Where(wire => wire.To.Unit is null && wire.To.Node == at && wire.From is { Unit: null }))
            {
                if (before.Add(wire.From!.Node)) next.Push(wire.From.Node);
            }
        }

        string StateOf(string node) => record.Statuses.GetValueOrDefault(node)?.State ?? NodeStates.Blocked;
        var open = graph.Nodes.Where(node => before.Contains(node.Id) && StateOf(node.Id) is not (NodeStates.Reached or NodeStates.Skipped)).ToList();
        return open.FirstOrDefault(node => StateOf(node.Id) != NodeStates.Blocked) ?? open.FirstOrDefault();
    }

    /// <summary>A branch unit by its branch's name, as this machine's session branches record it; else its id.</summary>
    private static string BranchName(string unit, IReadOnlyList<GrownBranch> branches) =>
        branches.LastOrDefault(entry => entry.Unit == unit) is { } entry ? entry.Now ?? entry.Branch : unit;

    private static string Short(string? commit) => commit is null ? "" : TextBounds.ShortCommit(commit, 8);
}

/// <summary>
/// The person's press on a run held by its graph (FABRIC-CUTOVER1d; the cutover design §4): the press keeps its order, its answer kept first and
/// the gate read with the graph's part; where that part holds for a judgement not made yet, the press asks the loop for a whole pass, waits for
/// it within <see cref="Bound"/>, and reads the gate again, so it lands at the same press where that pass made <c>handed</c> active at the commit.
/// Past the bound it is refused as read, its answer kept, and the next press or look lands it.
/// </summary>
public static class GraphPasses
{
    /// <summary>
    /// How long a press waits for the pass it asked: a whole pass reads the units afresh (the service's quests and the inventory) and judges every
    /// governed run, a few seconds at most on an install; twenty leaves a slow one room, and holds a press no longer than a landing's own git.
    /// </summary>
    public static readonly TimeSpan Bound = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The gate a press lands with: <paramref name="answer"/> keeps the press's answer and reads the gate; where the graph's part is what holds it
    /// and the next pass may move it, a pass is asked (<paramref name="pass"/>), and waited for within <see cref="Bound"/> only for a judgement not
    /// made yet, after which the gate is read again (<paramref name="read"/>). A press with no loop to ask (a terminal's alone) is refused as read.
    /// </summary>
    /// <param name="pass">Asks the loop for a whole pass and waits for it within the bound given (none: asks and waits for nothing); true where it came.</param>
    public static async Task<LandingGate> PressAsync(
        Func<CancellationToken, Task<LandingGate>> answer, Func<CancellationToken, Task<LandingGate>> read,
        Func<TimeSpan, CancellationToken, Task<bool>>? pass, CancellationToken ct)
    {
        var gate = await answer(ct).ConfigureAwait(false);
        if (pass is null || !gate.GraphHolds || gate.Graph is not { AsksPass: true } graph) return gate;
        if (!graph.PressWaits)
        {
            await pass(TimeSpan.Zero, ct).ConfigureAwait(false);
            return gate;
        }

        return await pass(Bound, ct).ConfigureAwait(false) ? await read(ct).ConfigureAwait(false) : gate;
    }
}
