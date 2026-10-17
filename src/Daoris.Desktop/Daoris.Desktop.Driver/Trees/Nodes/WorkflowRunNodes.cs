namespace Daoris.Driver;

/// <summary>
/// The differences between a schema-1 run and its decoded graph (FABRIC-WORKFLOW2b), each by name and none hidden: where the two read a
/// run apart, the comparison says which of these it is, and the cutover settles each openly. Schema 1 keeps its behaviour meanwhile.
/// </summary>
public static class WorkflowRunDifferences
{
    /// <summary>
    /// D173's FABRIC-WIRE1g mismatch: schema 1 holds the look only while an agent is at the opinion, so the look goes on beside an opinion
    /// that waits on the person or was not read (a dispute, commits since, a required one none could give), and the look's Reviewed
    /// answers a dispute. The decoded order (every opinion reaches every look) holds the look until the opinion is reached. At the opinion.
    /// </summary>
    public const string LookBesideOpinion = "look-beside-opinion";

    /// <summary>
    /// Schema 1 shows a step it cannot start (a partial kind: a go-ahead) as cannot start and lets the steps after it go on, the landing
    /// included; the decoded order holds them behind it. At the step that cannot start.
    /// </summary>
    public const string CannotStartHoldsNothing = "cannot-start-holds-nothing";

    /// <summary>
    /// A step the work's own choice added (a look, the workflow design §4.6) is no node of the version's graph, whose digest it is not
    /// part of: schema 1 holds the landing on it, and the graph would not. At the added step.
    /// </summary>
    public const string Added = "added";

    /// <summary>
    /// Schema 1 passes a step over by a rule's choice or a fact no node's authority makes (a look the chain's or the ask's choice turns off; a
    /// pull request with nothing landed or no branch to open): the node stays pending, so the graph stands at it. At the step passed over.
    /// </summary>
    public const string PassedByRule = "passed-by-rule";

    /// <summary>
    /// A skip satisfies its <c>after</c> wire whatever stands before it (§E2, §E3): a look skipped while the opinion before it is not settled
    /// lets the decoded landing go, where schema 1 holds the landing on the opinion. At the skipped step.
    /// </summary>
    public const string SkipPassesEarlier = "skip-passes-earlier";

    /// <summary>The one table refused the move a step's state reads as (a skip without its words): the node could not take it. At that step.</summary>
    public const string Refused = "refused";

    /// <summary>A difference no name here explains. Never expected: a recorded run that finds one fails its test until it is named.</summary>
    public const string Unexplained = "unexplained";
}

/// <summary>One difference found (FABRIC-WORKFLOW2b): its name, and the step whose two readings differ, from which the rest follows.</summary>
/// <param name="Code">One of <see cref="WorkflowRunDifferences"/>.</param>
/// <param name="Step">The step it rests on: the one schema 1 and the decoded graph read apart.</param>
public sealed record WorkflowRunDifference(string Code, string Step);

/// <summary>
/// One schema-1 step beside its decoded node (FABRIC-WORKFLOW2b): the step whole, as <see cref="WorkflowRuns.Derive"/> said it (its state,
/// its detail and every fact its words and its door are made of), and the node of its id with the status the replay left it in, and
/// whether the graph offers it. No node for a step the version's graph does not hold.
/// </summary>
/// <param name="Offered">Whether the evaluation found its wires satisfied (<see cref="WireEligibility.Eligible"/>), settled or not.</param>
public sealed record WorkflowRunNode(WorkflowRunStep Step, NodeDeclaration? Node, NodeStatus? Status, bool Offered);

/// <summary>What a bound run reads as on its decoded graph (FABRIC-WORKFLOW2b), beside the run as schema 1 reads it, kept whole.</summary>
/// <param name="Run">The run as schema 1 reads it, unchanged.</param>
/// <param name="Version">The version it is bound to, decoded, or unreadable with its reason: nothing is read on an unreadable one.</param>
public sealed record WorkflowRunProjection(WorkflowRun Run, DecodedVersion Version)
{
    /// <summary>Every step of the run, in its order, each beside its node.</summary>
    public IReadOnlyList<WorkflowRunNode> Steps { get; init; } = [];

    /// <summary>Every node's status after the run's steps were read onto it, the synthetic nodes' included, by node id.</summary>
    public IReadOnlyDictionary<string, NodeStatus> Statuses { get; init; } = new Dictionary<string, NodeStatus>();

    /// <summary>The nodes decoding made that stand for no step (a sink, a person's task), counted apart: never in <see cref="Frontier"/>.</summary>
    public IReadOnlyList<string> Synthetic { get; init; } = [];

    /// <summary>
    /// The graph's eligible nodes as the comparison reads them, in the graph's order: a node standing for a step, whose wires hold, and which
    /// is not settled (reached or skipped). Not raw eligibility, which keeps a completed node, nor the starts, which leave a started one out.
    /// </summary>
    public IReadOnlyList<string> Frontier { get; init; } = [];

    /// <summary>Where the decoded graph stands: its first eligible node, or null where every node of a step is settled.</summary>
    public string? At => Frontier.FirstOrDefault();

    /// <summary>Every difference found, by name, in the order found, each once.</summary>
    public IReadOnlyList<WorkflowRunDifference> Differences { get; init; } = [];

    public bool Readable => Version.Readable;

    /// <summary>Canary 38's equivalence: the graph's eligible nodes are exactly schema 1's <see cref="WorkflowRun.At"/>, and nothing differs.</summary>
    public bool Agrees => Readable && Differences.Count == 0 && Frontier.SequenceEqual(Run.At is null ? [] : [Run.At]);
}

/// <summary>
/// A D157 run's step states read as its decoded graph's statuses (FABRIC-WORKFLOW2b; the fabric events design §E7, canary 38), and the two
/// readings compared. Read-only: it moves no authority, so schema 1 governs every run as it does, and the cutover settles each difference.
/// </summary>
/// <remarks>
/// <para><b>Replayed through the graph's own evaluation and the one table.</b> Every node starts blocked; each round evaluates the wires
/// (<see cref="WireEvaluation.Evaluate"/>), starts a node only where the evaluation offers it, on the binding it offers, and moves it to
/// what its step's state reads as, each move through <see cref="NodeLifecycle.Apply"/>. So a status here is one the lifecycle could make, and
/// a step schema 1 went on with that the graph's order does not offer stays where its wires leave it, to be named as a difference. Nothing is
/// observed (no unit, git read or gate is handed in): the run's own reading is what is read, never a second one.</para>
/// <para><b>The table</b> (a step's state, its node's state): <i>not reached</i> and <i>cannot start</i> move nothing, so the node is
/// blocked or ready by its wires; <i>working</i> and <i>waiting</i> are active, the look's <i>not yet</i> the person's rejection with their
/// words; <i>not known</i> is unknown; <i>done</i> is reached by its kind's authority, its evidence naming the run's reading
/// (<see cref="Record"/>); <i>skipped</i> is the opinion's gate's skip, or the person's on the look with their words, else active, since no
/// node's authority passes it over; <i>failed</i> is failed by its kind's failure authority, else active (a claim never fails);
/// <i>stopped</i> is the person's stop, and a pull request's abandon is failed on its plugin's answer, as the node observes it.</para>
/// </remarks>
public static class WorkflowRunNodes
{
    /// <summary>What a reach's evidence names (<c>workflow-run#&lt;step&gt;</c>): the schema-1 run's reading of that step.</summary>
    public const string Record = "workflow-run";

    /// <summary>
    /// The version <paramref name="run"/> is bound to, decoded (§E7, <i>one governs a run, by its binding</i>): null where it has no binding
    /// or follows Current, which stays live. A binding that does not read, a workflow no longer saved, and every reason
    /// <see cref="WorkflowGraphs.ForRun"/> gives read as unreadable, never as the newest version or Current. Reads the two stores, writes neither.
    /// </summary>
    public static DecodedVersion? VersionOf(IWorkflowStore store, IWorkflowRunStore runs, string? run)
    {
        if (run is null) return null;
        if (runs.Read(run) is not { } binding)
        {
            // As the gate reads it (WorkflowProcesses.Read): absence is Current, and a damaged record cannot be read as Current.
            return runs.Bound(run) ? new DecodedVersion(0, null, null, null, $"the run `{run}`'s stored binding could not be read.") : null;
        }

        if (binding.IsCurrent) return null;
        var found = WorkflowNamed.IsId(binding.Workflow) ? store.Load(binding.Workflow) : null;
        return found is null
            ? new DecodedVersion(binding.Version ?? 0, null, null, null, $"no workflow `{binding.Workflow}` is saved here any more.")
            : WorkflowGraphs.ForRun(found.Read, binding);
    }

    /// <summary>The run read on the version it is bound to (<see cref="Project"/>); null where it has none, or follows Current.</summary>
    public static WorkflowRunProjection? Read(IWorkflowStore store, IWorkflowRunStore runs, WorkflowRun run) =>
        run.Process?.Run is { } id && VersionOf(store, runs, id) is { } version ? Project(run, version, id) : null;

    /// <summary>
    /// <paramref name="run"/>'s steps read onto <paramref name="version"/>'s nodes, on one unit named by the run's id, with no revision (as a
    /// quest's), under the version's graph digest and the run; then the graph's eligible nodes compared with where schema 1 stands.
    /// </summary>
    public static WorkflowRunProjection Project(WorkflowRun run, DecodedVersion version, string runId)
    {
        if (version.Graph is not { } graph || version.GraphDigest is not { } digest)
        {
            return new(run, version) { Steps = [.. run.Steps.Select(step => new WorkflowRunNode(step, null, null, false))] };
        }

        var wire = new WireRun(digest, runId) { Revisions = new Dictionary<string, string?>(StringComparer.Ordinal) { [runId] = null } };
        // A node stands for the run's step of its id: an authored step, or a group's join for its group. A step the work's own choice added
        // is none of the version's, whatever its id; a sink or a person's task decoding made stands for no step.
        var steps = new Dictionary<string, WorkflowRunStep>(StringComparer.Ordinal);
        foreach (var step in run.Steps.Where(step => step.Added is null)) steps.TryAdd(step.Step.Id, step);
        var counted = graph.Nodes.Where(node => steps.ContainsKey(node.Id)).ToList();
        var statuses = graph.Nodes.ToDictionary(node => node.Id, node => NodeLifecycle.Start(runId, node.Id), StringComparer.Ordinal);
        var refused = new Dictionary<string, string>(StringComparer.Ordinal);

        WireEvaluated Evaluate()
        {
            var evaluated = WireEvaluation.Evaluate(graph.Nodes, graph.Wires, statuses, wire);
            foreach (var (id, status) in evaluated.Statuses) statuses[id] = status;
            return evaluated;
        }

        // Each round moves a node at most a start and an outcome, so a node's step is read within three rounds of its offer.
        var evaluated = Evaluate();
        for (var round = 0; round <= 3 * graph.Nodes.Count; round++)
        {
            var moved = false;
            foreach (var node in counted.Where(node => !refused.ContainsKey(node.Id))) moved |= Advance(node, steps[node.Id], evaluated, statuses, wire, refused);
            if (!moved) break;
            evaluated = Evaluate();
        }

        bool Offered(string node) => evaluated.Of(node)?.Eligible == true;
        bool Settled(string node) => statuses[node].State is NodeStates.Reached or NodeStates.Skipped;
        var rows = run.Steps.Select(step => step.Added is null && counted.FirstOrDefault(node => node.Id == step.Step.Id) is { } node
            ? new WorkflowRunNode(step, node, statuses[node.Id], Offered(node.Id))
            : new WorkflowRunNode(step, null, null, false)).ToList();
        var frontier = counted.Where(node => Offered(node.Id) && !Settled(node.Id)).Select(node => node.Id).ToList();

        return new(run, version)
        {
            Steps = rows,
            Statuses = statuses,
            Synthetic = [.. graph.Nodes.Where(node => !steps.ContainsKey(node.Id)).Select(node => node.Id)],
            Frontier = frontier,
            Differences = Compare(run, counted, steps, rows, frontier, statuses, refused),
        };
    }

    /// <summary>
    /// Moves <paramref name="node"/> one round toward what its step's state reads as: the person's skip and stop where it stands, which need
    /// no attempt; otherwise a start where the evaluation offers one, then the outcome on that attempt. A refusal is kept, and ends its moves.
    /// </summary>
    private static bool Advance(
        NodeDeclaration node, WorkflowRunStep step, WireEvaluated evaluated, Dictionary<string, NodeStatus> statuses, WireRun wire,
        Dictionary<string, string> refused)
    {
        var to = Target(node, step);
        if (to is null || statuses[node.Id].State == to) return false;
        var moved = false;

        bool Apply(NodeMove move)
        {
            var result = NodeLifecycle.Apply(node, statuses[node.Id], move);
            if (!result.Moved)
            {
                refused[node.Id] = result.Refused!;
                return false;
            }

            statuses[node.Id] = result.Status;
            moved = true;
            return true;
        }

        var person = to == NodeStates.Skipped && SkipBy(node, step) == NodeSource.Person;
        if (person) return Apply(new NodeMove(NodeStates.Skipped, NodeCause.Person) { Words = step.Words, Scope = wire.Scope }) || moved;
        if (to == NodeStates.Stopped && statuses[node.Id].State == NodeStates.Blocked) return Apply(new NodeMove(NodeStates.Stopped, NodeCause.Person) { Words = step.Words });

        if (statuses[node.Id].State == NodeStates.Ready && evaluated.Starts.FirstOrDefault(start => start.Node == node.Id) is { } offered && !Apply(offered.Move)) return moved;
        var status = statuses[node.Id];
        if (status.State != NodeStates.Active || to == NodeStates.Active) return moved;

        var authority = NodeAuthorities.Of(node)!;
        Apply(to switch
        {
            NodeStates.Reached => new NodeMove(NodeStates.Reached, NodeCause.Authority)
            {
                Source = authority.ReachBy,
                Attempt = status.Attempt,
                Acceptance = status.Binding! with
                {
                    Evidence = authority.Evidenced
                        ? new NodePort(BuiltInNodes.EvidenceSchema(node.Kind), $"{Record}#{step.Step.Id}", status.Binding.SubjectRevision, null)
                        : null,
                },
            },
            NodeStates.Skipped => new NodeMove(NodeStates.Skipped, NodeCause.Authority) { Source = authority.SkipBy, Attempt = status.Attempt, Scope = status.Scope },
            NodeStates.Unknown => new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = status.Attempt },
            NodeStates.Failed => new NodeMove(NodeStates.Failed, NodeCause.Authority)
            {
                Source = authority.FailureBy, Attempt = status.Attempt, Code = step.Code ?? step.Detail,
            },
            NodeStates.Rejected => new NodeMove(NodeStates.Rejected, NodeCause.Person) { Attempt = status.Attempt, Words = step.Words },
            _ => new NodeMove(NodeStates.Stopped, NodeCause.Person) { Words = step.Words },
        });
        return moved;
    }

    /// <summary>The state a step's reading takes its node to (the table in the remarks), or null where none: not reached, or cannot start.</summary>
    private static string? Target(NodeDeclaration node, WorkflowRunStep step) => step.State switch
    {
        WorkflowRunStates.Done => NodeStates.Reached,
        WorkflowRunStates.Skipped => SkipBy(node, step) is null ? NodeStates.Active : NodeStates.Skipped,
        WorkflowRunStates.NotKnown => NodeStates.Unknown,
        WorkflowRunStates.Failed => NodeAuthorities.Of(node)?.FailureBy is null ? NodeStates.Active : NodeStates.Failed,
        // A pull request abandoned is its plugin's answer, which fails the node as BuiltInNodes observes it; every other stop is the person's.
        WorkflowRunStates.Stopped => node.Kind == NodeKinds.PullRequest ? NodeStates.Failed : NodeStates.Stopped,
        WorkflowRunStates.WaitingOnAgent when node.Kind == NodeKinds.Look && step.Detail == WorkflowRunDetails.NotYet => NodeStates.Rejected,
        WorkflowRunStates.Working or WorkflowRunStates.WaitingOnYou or WorkflowRunStates.WaitingOnAgent => NodeStates.Active,
        _ => null,
    };

    /// <summary>
    /// Whose a schema-1 skip is: the kind's record's where its authority names one (the opinion's gate), the person's where they skipped
    /// the look, or null: a rule's choice or a fact no node's authority makes (<see cref="WorkflowRunDifferences.PassedByRule"/>).
    /// </summary>
    private static string? SkipBy(NodeDeclaration node, WorkflowRunStep step) =>
        NodeAuthorities.Of(node) is not { } authority ? null
        : authority.SkipBy is { } record ? record
        : step.Detail == WorkflowRunDetails.Skip && authority.PersonSkips ? NodeSource.Person
        : null;

    /// <summary>
    /// Where the two readings differ, each named by its cause (<see cref="WorkflowRunDifferences"/>): a step schema 1 went on with that the
    /// graph does not offer; a node the graph offers beyond where schema 1 stands; the two standing apart; a step with no node; a refusal.
    /// </summary>
    private static List<WorkflowRunDifference> Compare(
        WorkflowRun run, List<NodeDeclaration> counted, Dictionary<string, WorkflowRunStep> steps, List<WorkflowRunNode> rows, List<string> frontier,
        Dictionary<string, NodeStatus> statuses, Dictionary<string, string> refused)
    {
        var found = new List<WorkflowRunDifference>();
        void Add(string code, string step)
        {
            if (!found.Any(each => each.Code == code && each.Step == step)) found.Add(new(code, step));
        }

        bool Settled(string node) => statuses[node].State is NodeStates.Reached or NodeStates.Skipped;
        int Index(string node) => counted.FindIndex(each => each.Id == node);

        // A step schema 1 settled and its node could not: the one table refused it, or no node's authority makes that skip.
        string? Unsettled(string node) => refused.ContainsKey(node) ? WorkflowRunDifferences.Refused
            : WorkflowRunStates.Settled(steps[node].State) && !Settled(node) ? WorkflowRunDifferences.PassedByRule
            : null;

        // Schema 1 goes on with a step the decoded order holds: named by the first node before it that is not settled.
        (string Code, string Step) Ahead(NodeDeclaration node)
        {
            var root = counted.Take(Index(node.Id)).FirstOrDefault(each => !Settled(each.Id));
            if (root is null) return (WorkflowRunDifferences.Unexplained, node.Id);
            if (Unsettled(root.Id) is { } why) return (why, root.Id);
            if (root.Kind == NodeKinds.Opinion && node.Kind == NodeKinds.Look) return (WorkflowRunDifferences.LookBesideOpinion, root.Id);
            return steps[root.Id].State == WorkflowRunStates.CannotStart
                ? (WorkflowRunDifferences.CannotStartHoldsNothing, root.Id)
                : (WorkflowRunDifferences.Unexplained, node.Id);
        }

        // The graph offers a node beyond where schema 1 stands: an added step holds it there, or a skip let it past an earlier node.
        (string Code, string Step) Beyond(string node)
        {
            var before = run.Steps.TakeWhile(step => step.Step.Id != node).ToList();
            if (before.FirstOrDefault(step => step.Added is not null && !WorkflowRunStates.Settled(step.State)) is { } added) return (WorkflowRunDifferences.Added, added.Step.Id);
            var earlier = counted.Take(Index(node)).ToList();
            var first = earlier.FindIndex(each => !Settled(each.Id));
            if (first >= 0 && earlier.Skip(first + 1).FirstOrDefault(each => statuses[each.Id].State == NodeStates.Skipped) is { } skipped)
            {
                return (WorkflowRunDifferences.SkipPassesEarlier, skipped.Id);
            }

            return Unsettled(node) is { } why ? (why, node) : (WorkflowRunDifferences.Unexplained, node);
        }

        foreach (var (node, _) in refused) Add(WorkflowRunDifferences.Refused, node);
        foreach (var row in rows)
        {
            if (row.Node is not { } node)
            {
                Add(WorkflowRunDifferences.Added, row.Step.Step.Id);
                continue;
            }

            var started = row.Step.State is not (WorkflowRunStates.NotReached or WorkflowRunStates.CannotStart or WorkflowRunStates.Skipped);
            if (started && !row.Offered)
            {
                var (code, step) = Ahead(node);
                Add(code, step);
            }
        }

        foreach (var node in frontier.Where(node => node != run.At))
        {
            var (code, step) = Beyond(node);
            Add(code, step);
        }

        var at = frontier.FirstOrDefault();
        if (at != run.At)
        {
            if (run.At is { } stands && !steps.ContainsKey(stands)) Add(WorkflowRunDifferences.Added, stands);
            else if (at is not null && Unsettled(at) is { } why) Add(why, at);
            else Add(WorkflowRunDifferences.Unexplained, run.At ?? at!);
        }

        return found;
    }
}
