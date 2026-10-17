namespace Daoris.Driver;

/// <summary>
/// What the evaluation runtime asks of the plugins' work processes (FABRIC-RUN1): the performer's fenced calls, and the supervision's two
/// halves, the awaited set told (<see cref="HookSet.Watch"/>) and the completions heard (<see cref="HookSet.TakeSteps"/>). A port, so the
/// runtime is proved without a process.
/// </summary>
public interface IWorkRuntimePort : IWorkPerformPort
{
    void Watch(IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> awaited);

    IReadOnlyList<WorkStepHeard> TakeSteps();
}

/// <summary>The units a pass reads (<see cref="WorkUnits"/>), and each repository's checkout here, which the awaited set names.</summary>
public sealed record NodeUnitsRead(IReadOnlyList<WorkUnit> Units, IReadOnlyDictionary<string, string> Roots);

/// <summary>
/// One pass's reading (FABRIC-RUN1): the standing choices, whether it is the look's deadline (every governed unit judged) or a wake (only
/// the units its answers concern), and the two reads it makes only once a graph naming a plugin's performed node governs something.
/// </summary>
public sealed record NodeRuntimeLook(DriverConfig Config, bool Whole)
{
    /// <summary>The units and their checkouts: read at the look's deadline, and at a wake only when none was read yet.</summary>
    public Func<CancellationToken, Task<NodeUnitsRead>> Units { get; init; } = _ => Task.FromResult(new NodeUnitsRead([], new Dictionary<string, string>()));

    /// <summary>The plugins' steps as the catalogue declares them (<see cref="PluginManifest.Steps"/>): a performed node's contract.</summary>
    public Func<IReadOnlyList<NodeDeclaration>> Steps { get; init; } = () => [];
}

/// <summary>
/// A unit a graph naming a plugin's performed node governs (FABRIC-RUN1): the graph, its digest, the run (<see cref="NodeRuns.Of"/>),
/// each node's declaration as the runtime evaluates and starts it, and the unit as the awaited set names it.
/// </summary>
/// <param name="Declared">Each node by id: a plugin's performed node with its plugin's step's <c>perform</c>, the rest as the graph declares.</param>
/// <param name="Dispatchable">The performed nodes whose plugin declares their step at the graph's version and reach: only these are sent.</param>
public sealed record GovernedUnit(
    WorkUnit Unit, NodeGraph Graph, string GraphDigest, string Run, IReadOnlyDictionary<string, NodeDeclaration> Declared,
    IReadOnlySet<string> Dispatchable, WatchedUnit Watched);

/// <summary>What one pass did (FABRIC-RUN1), for the look's report, the machine log and the tests.</summary>
/// <param name="Armed">Whether a graph naming a plugin's performed node governs anything; false, the pass read and wrote nothing.</param>
/// <param name="Evaluated">The units evaluated, by id.</param>
/// <param name="Said">What the pass says that no performed result does: a refused evaluation, a record that does not read.</param>
public sealed record NodeRuntimePass(bool Armed, IReadOnlyList<WorkPerformed> Performed, IReadOnlyList<string> Evaluated, IReadOnlyList<string> Said)
{
    public static NodeRuntimePass Idle { get; } = new(false, [], [], []);

    /// <summary>
    /// Whether the awaited set told differs from the one told before: the loop wakes once more, so a process the set now needs is started
    /// by the look's supervision and its start made, rather than at the look's deadline. The same set told again wakes nothing.
    /// </summary>
    public bool AwaitedChanged { get; init; }
}

/// <summary>
/// A run's identity under Current's graphs (FABRIC-RUN1): a digest of the unit and the graph governing it. So a run begins when a graph
/// naming a plugin's performed node first governs the unit, and again only when the governing graph changes (another digest); never at a
/// wake, a look, a restart or a new revision (a moved revision lapses the bindings it moved, WIRE1e, in the same run). WIRE1f lapses a
/// failure kept from another run, so a run begun afresh at each wake would retry a permanent failure for ever.
/// </summary>
public static class NodeRuns
{
    public static string Of(string unit, string graphDigest) =>
        "run-" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"node-run\n{unit}\n{graphDigest}\n")))[..24];
}

/// <summary>
/// The evaluation runtime in the lease's loop (FABRIC-RUN1; the fabric events design §E2, §E3, §E7, §D6, §A3): link 8 of the loop's chain
/// (<see cref="ChainRanks.Nodes"/>), heard at every look and wake. Each pass drains the completions heard into <see cref="WorkPerformer.Hear"/>,
/// asks after every attempt due (<see cref="WorkPerformer.ReconcileAsync"/>) before any new start, makes ready the retries whose persisted
/// delay passed (<see cref="WorkPerformer.Due"/>), evaluates each concerned unit's graph to <see cref="WireEvaluated.Settled"/>, keeping its
/// moves (<see cref="WorkPerformer.Keep"/>) and starting what it proposes (a plugin's performed node through
/// <see cref="WorkPerformer.StartAsync"/>, an observed built-in through <see cref="WorkPerformer.Begin"/>), and tells the plugins the
/// awaited set (<see cref="IWorkRuntimePort.Watch"/>).
/// </summary>
/// <remarks>
/// <para><b>Off unless a graph naming a plugin's performed node governs something</b> (the owner's ruling for RUN1): with no such graph in
/// the choices, a pass reads no unit, no plugin step and no record, tells and takes nothing, writes nothing and starts no process or git.</para>
/// <para><b>One pass at a time, one performer per loop:</b> a wake that comes while a look's pass is out waits for it, so a start is never
/// judged twice on one record; a second loop or a verb holds no lease, and every record edit asks it under the record's lock.</para>
/// <para><b>A wake runs only what it concerns:</b> the units its answers, reconciliations and retries named, and those owing a start; the
/// look's deadline (<see cref="NodeRuntimeLook.Whole"/>) reads the units afresh and judges every governed one.</para>
/// </remarks>
public sealed class NodeRuntime(NodeRecordFiles records, IWorkRuntimePort port, Func<DateTimeOffset> clock, Func<bool>? holdsLease = null)
{
    /// <summary>The units one pass evaluates at most, in id order; the rest are judged at the next.</summary>
    public const int UnitsPerPass = 32;

    /// <summary>The attempts one pass sends at most, across its units: PROC1a's four process starts at once (§A3).</summary>
    public const int SendsPerPass = WireEvaluation.DefaultLimit;

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> NoneAwaited = new Dictionary<string, IReadOnlyList<WatchedUnit>>();

    /// <summary>The loop's one performer (PERFORM1h's "for RUN1"): its delivered invocations live as long as the loop.</summary>
    private readonly WorkPerformer _performer = new(records, port, clock);

    private readonly Func<bool> _holdsLease = holdsLease ?? (() => true);

    private readonly SemaphoreSlim _one = new(1, 1);

    /// <summary>The governed units the last look read, by id: what a wake judges with.</summary>
    private Dictionary<string, GovernedUnit>? _governed;

    /// <summary>Whether the plugins were last told a set naming anything: a disarmed pass empties it once.</summary>
    private volatile bool _told;

    /// <summary>When the next timer the records persist comes due (a retry's delay, a running answer's wait, a deadline), or null.</summary>
    public DateTimeOffset? NextDue { get; private set; }

    /// <summary>
    /// Whether any graph the choices set names a plugin's performed node: a landing rule's <c>cycle</c>, a repository's or a workspace's
    /// <c>workCycles</c>. Read from the choices alone, so an install that names none is judged without a file, a git call or a process.
    /// </summary>
    public static bool Armed(DriverConfig config) =>
        config.Landings.Values.Concat(config.WorkspaceLandings.Values)
            .Any(rule => rule.Cycle is not null && Performs(NodeGraphHomes.OfLanding(rule, LandingSource.Repository).Graph))
        || config.WorkCycles.Values.Concat(config.WorkspaceWorkCycles.Values).SelectMany(kinds => kinds.Values).Any(json => Performs(NodeGraphs.Read(json).Graph));

    /// <summary>
    /// The units a graph naming a plugin's performed node governs (<see cref="NodeGraphHomes.Work"/>), in id order, each with its graph, run
    /// and declarations. A plugin's performed node is evaluated and started under its plugin's step (<paramref name="steps"/>): that step's
    /// <c>perform</c>, and its retry, timeout and evidence where the graph's node declares none. A node whose plugin declares no such step at
    /// the graph's version and reach is never sent.
    /// </summary>
    public static IReadOnlyList<GovernedUnit> Govern(DriverConfig config, NodeUnitsRead read, IReadOnlyList<NodeDeclaration> steps)
    {
        var governed = new List<GovernedUnit>();
        foreach (var unit in read.Units.OrderBy(unit => unit.Id, StringComparer.Ordinal))
        {
            var repository = unit.Owner.Repository ?? "";
            if (NodeGraphHomes.Work(config, repository, unit.Owner.Workspace, unit.Kind).Graph is not { } graph || !Performs(graph)) continue;
            var dispatchable = new HashSet<string>(StringComparer.Ordinal);
            var declared = new Dictionary<string, NodeDeclaration>(StringComparer.Ordinal);
            foreach (var node in graph.Nodes)
            {
                declared[node.Id] = node;
                if (!Performs(node) || steps.FirstOrDefault(step => step.Kind == node.Kind) is not { } step || step.Version != node.Version || step.Reach != node.Reach)
                {
                    continue;
                }

                var under = node with
                {
                    Perform = step.Perform, Retry = node.Retry ?? step.Retry, TimeoutSeconds = node.TimeoutSeconds ?? step.TimeoutSeconds,
                    Evidence = node.Evidence ?? step.Evidence,
                };
                if (NodeDeclarations.Problem(under) is not null) continue;
                declared[node.Id] = under;
                dispatchable.Add(node.Id);
            }

            var digest = NodeGraphs.Digest(graph);
            var watched = new WatchedUnit(repository, unit.Owner.Workspace, read.Roots.GetValueOrDefault(repository) ?? "",
                unit.Kind == WorkUnitKind.Branch ? unit.Subject.Artifact ?? "" : "", unit.Subject.Revision ?? "", null, null, []);
            governed.Add(new GovernedUnit(unit, graph, digest, NodeRuns.Of(unit.Id, digest), declared, dispatchable, watched));
        }

        return governed;
    }

    /// <summary>
    /// One pass, at a look (<see cref="NodeRuntimeLook.Whole"/>) or a wake: completions heard, attempts due asked after before any new start,
    /// retries due made ready, the concerned units evaluated and their starts made within <see cref="SendsPerPass"/>, the awaited set told.
    /// </summary>
    public async Task<NodeRuntimePass> PassAsync(NodeRuntimeLook look, CancellationToken ct)
    {
        var armed = Armed(look.Config);
        if (!armed && !_told) return NodeRuntimePass.Idle;
        await _one.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!armed)
            {
                // Disarmed since the last pass: the plugins are told nothing is awaited, once, so their processes stop; nothing else is read.
                port.Watch(NoneAwaited);
                (_told, _toldText, _governed, NextDue) = (false, "", null, null);
                return NodeRuntimePass.Idle;
            }

            if (!_holdsLease()) return new NodeRuntimePass(true, [], [], []);
            if (look.Whole || _governed is null)
            {
                var read = await look.Units(ct).ConfigureAwait(false);
                _governed = Govern(look.Config, read, look.Steps()).ToDictionary(each => each.Unit.Id, StringComparer.Ordinal);
            }

            var governed = _governed;
            NodeDeclaration? Declared(string unit, string node) => governed.TryGetValue(unit, out var each) ? each.Declared.GetValueOrDefault(node) : null;

            var done = new List<WorkPerformed>();
            done.AddRange(_performer.Hear(port.TakeSteps(), Declared));
            done.AddRange(await _performer.ReconcileAsync(Declared, ct).ConfigureAwait(false));
            done.AddRange(_performer.Due(Declared));

            var said = new List<string>();
            var named = done.Select(each => each.Unit).ToHashSet(StringComparer.Ordinal);
            var concerned = governed.Values
                .Where(each => look.Whole || named.Contains(each.Unit.Id) || OwesStart(each))
                .Take(UnitsPerPass).ToList();
            var sends = SendsPerPass;
            foreach (var each in concerned)
            {
                sends = await EvaluateAsync(each, sends, done, said, ct).ConfigureAwait(false);
            }

            var changed = Tell(governed.Values);
            return new NodeRuntimePass(true, done, [.. concerned.Select(each => each.Unit.Id)], said) { AwaitedChanged = changed };
        }
        finally
        {
            _one.Release();
        }
    }

    /// <summary>
    /// One unit's graph evaluated to <see cref="WireEvaluated.Settled"/>, at most a round per node and one more: each round on the record as
    /// it is then, its moves kept and its starts made. Returns the sends left in the pass.
    /// </summary>
    private async Task<int> EvaluateAsync(GovernedUnit governed, int sends, List<WorkPerformed> done, List<string> said, CancellationToken ct)
    {
        var unit = governed.Unit.Id;
        var nodes = governed.Graph.Nodes.Select(node => governed.Declared[node.Id]).ToList();
        for (var round = 0; round <= nodes.Count; round++)
        {
            if (records.Read(unit) is not { } record)
            {
                said.Add($"work  {unit}: its node record does not read, so nothing on it is judged or started until a person mends it.");
                break;
            }

            var before = nodes.ToDictionary(node => node.Id, node => record.Statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(unit, node.Id), StringComparer.Ordinal);
            var run = new WireRun(governed.GraphDigest, governed.Run)
            {
                Revisions = new Dictionary<string, string?>(StringComparer.Ordinal) { [unit] = governed.Unit.Subject.Revision },
                Fired = record.Fired.ToHashSet(StringComparer.Ordinal),
                Units = new Dictionary<string, WorkUnit>(StringComparer.Ordinal) { [unit] = governed.Unit },
            };
            var evaluated = WireEvaluation.Evaluate(nodes, governed.Graph.Wires, before, run);
            if (evaluated.Refusal is { } refusal)
            {
                said.Add($"work  {unit}: its graph was not judged ({refusal}: {string.Join(", ", evaluated.Missing)}), so nothing on it is started.");
                break;
            }

            if (!evaluated.Settled) done.AddRange(_performer.Keep(before, evaluated));
            var started = false;
            foreach (var start in evaluated.Starts)
            {
                var node = governed.Declared[start.Node];
                if (node.Reach == NodeReach.Observed && BuiltInNodes.Observed.Contains(node.Kind))
                {
                    var begun = _performer.Begin(node, start, evaluated.Statuses[start.Node]);
                    done.Add(begun);
                    started |= begun.What == PerformedWhat.Begun;
                }
                else if (governed.Dispatchable.Contains(node.Id) && sends > 0 && port.GenerationOf(PluginOf(node.Kind)) is not null)
                {
                    // A process not up yet is awaited (Tell below), and the start is made at a later pass once it is: never sent blind.
                    sends--;
                    var sent = await _performer.StartAsync(node, start, ct).ConfigureAwait(false);
                    done.Add(sent);
                    started |= sent.What != PerformedWhat.Refused;
                }

                // A pressed node's task and a waited node's wait are HUMAN1's and WAITUNIT1's to start: left ready.
            }

            if (evaluated.Settled && !started) break;
        }

        return sends;
    }

    /// <summary>Whether a unit's record holds one of its dispatchable nodes ready: a start owed, judged at a wake as well as at the look.</summary>
    private bool OwesStart(GovernedUnit governed) =>
        records.Read(governed.Unit.Id) is { } record
        && governed.Dispatchable.Any(node => record.Statuses.GetValueOrDefault(node)?.State == NodeStates.Ready);

    /// <summary>
    /// The awaited set told (§C4's <c>work/watch</c>, PERFORM1a's supervision): each governed unit with a dispatchable node ready, active or
    /// unknown, or an attempt still waiting for its answer, under that node's plugin and step; and the next timer the records persist.
    /// Returns whether the set differs from the one told before.
    /// </summary>
    private bool Tell(IEnumerable<GovernedUnit> governed)
    {
        var awaited = new SortedDictionary<string, List<WatchedUnit>>(StringComparer.Ordinal);
        DateTimeOffset? due = null;
        void Due(DateTimeOffset? at)
        {
            if (at is { } moment && (due is null || moment < due)) due = moment;
        }

        foreach (var each in governed)
        {
            if (records.Read(each.Unit.Id) is not { } record) continue;
            var steps = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (var id in each.Dispatchable)
            {
                var waiting = record.Attempts.Any(attempt => attempt.Node == id && attempt.State is AttemptStates.Recorded or AttemptStates.Sent or AttemptStates.Unknown);
                if (!waiting && record.Statuses.GetValueOrDefault(id)?.State is not (NodeStates.Ready or NodeStates.Active or NodeStates.Unknown)) continue;
                var kind = each.Declared[id].Kind;
                var plugin = PluginOf(kind);
                if (!steps.TryGetValue(plugin, out var named)) steps[plugin] = named = new SortedSet<string>(StringComparer.Ordinal);
                named.Add(kind[(plugin.Length + 1)..]);
            }

            foreach (var (plugin, named) in steps)
            {
                if (!awaited.TryGetValue(plugin, out var units)) awaited[plugin] = units = [];
                units.Add(each.Watched with { Awaiting = [.. named] });
            }

            foreach (var attempt in record.Attempts)
            {
                Due(attempt.RetryAt);
                if (attempt.State is AttemptStates.Recorded or AttemptStates.Unknown or AttemptStates.Sent) Due(attempt.AskAt);
                if (attempt.State == AttemptStates.Sent && attempt.AskAt is null) Due(attempt.Call.DeadlineAt);
            }
        }

        port.Watch(awaited.ToDictionary(each => each.Key, each => (IReadOnlyList<WatchedUnit>)each.Value, StringComparer.Ordinal));
        _told = awaited.Count > 0;
        NextDue = due;
        var text = string.Join("\n", awaited.SelectMany(each => each.Value.Select(unit =>
            $"{each.Key}\t{unit.Repository}\t{unit.Workspace}\t{unit.Branch}\t{unit.Tip}\t{string.Join(",", unit.Awaiting)}")));
        var changed = !string.Equals(text, _toldText, StringComparison.Ordinal);
        _toldText = text;
        return changed;
    }

    /// <summary>The awaited set last told, as text: what <see cref="NodeRuntimePass.AwaitedChanged"/> compares.</summary>
    private string _toldText = "";

    private static string PluginOf(string kind) => kind[..kind.IndexOf('/')];

    private static bool Performs(NodeDeclaration node) => node.Reach == NodeReach.Performed && NodeKinds.IsPlugin(node.Kind);

    private static bool Performs(NodeGraph? graph) => graph?.Nodes.Any(Performs) == true;
}
