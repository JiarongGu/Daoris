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

    /// <summary>
    /// The last <c>work/watch</c> frame whose send to <paramref name="plugin"/>'s process of <paramref name="generation"/> completed
    /// (FABRIC-PERFORM1i, §C4's third rule), or null where none did: what that process was actually told, never the set desired since.
    /// </summary>
    WorkWatchFrame? SentTo(string plugin, int generation) => null;
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

    /// <summary>
    /// Each named branch's tip in one checkout, read fresh (FABRIC-PERFORM1i, §C4's fourth rule): one read of the checkout's refs per pass
    /// (<see cref="NodeTips.ReadAsync"/>), never a cached unit's revision. A branch it could not read is absent, and establishes nothing.
    /// </summary>
    public Func<string, IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlyDictionary<string, string>>> Tips { get; init; } =
        (_, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
}

/// <summary>
/// A unit a graph naming a plugin's performed node governs (FABRIC-RUN1): the graph, its digest, the run (<see cref="NodeRuns.Of"/>),
/// each node's declaration as the runtime evaluates and starts it, and the unit as the awaited set names it.
/// </summary>
/// <param name="Declared">Each node by id: a plugin's performed node with its plugin's step's <c>perform</c>, the rest as the graph declares.</param>
/// <param name="Dispatchable">The performed nodes whose plugin declares their step at the graph's version and reach: only these are sent.</param>
public sealed record GovernedUnit(
    WorkUnit Unit, NodeGraph Graph, string GraphDigest, string Run, IReadOnlyDictionary<string, NodeDeclaration> Declared,
    IReadOnlySet<string> Dispatchable, WatchedUnit Watched)
{
    /// <summary>
    /// Whether its graph's <c>quest.close</c> closes it (FABRIC-CLOSE1b): a quest bound at its take to this very graph. An unbound quest, or one
    /// bound to another graph, is never closed by one, and keeps today's done.
    /// </summary>
    public bool Closes { get; init; }

    /// <summary>
    /// The plugins' observed nodes whose plugin declares their step at the graph's version and reach (FABRIC-PERFORM1i): only these are
    /// begun, awaited and reached by a believed event.
    /// </summary>
    public IReadOnlySet<string> Observable { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}

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
/// <remarks>
/// <para><b>Closure graphs</b> (FABRIC-CLOSE1b, CLOSE1l): with a close door (<paramref name="closes"/>), a graph holding <c>quest.close</c> arms it
/// too, and so does a closure graph this home keeps (<see cref="ClosureGraphs"/>), so a close in flight is reconciled and a bound quest closed
/// whatever the choices say since. A quest is bound at its take, in the take's own write (<see cref="ClosureBinding.AtStartAsync"/>), never
/// here; a bound quest is closed by the graph it was bound to.</para>
/// </remarks>
public sealed class NodeRuntime(
    NodeRecordFiles records, IWorkRuntimePort port, Func<DateTimeOffset> clock, Func<bool>? holdsLease = null, IQuestClosePort? closes = null)
{
    /// <summary>The units one pass evaluates at most, in id order; the rest are judged at the next.</summary>
    public const int UnitsPerPass = 32;

    /// <summary>The attempts one pass sends at most, across its units: PROC1a's four process starts at once (§A3).</summary>
    public const int SendsPerPass = WireEvaluation.DefaultLimit;

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> NoneAwaited = new Dictionary<string, IReadOnlyList<WatchedUnit>>();

    /// <summary>The loop's one performer (PERFORM1h's "for RUN1"): its delivered invocations live as long as the loop.</summary>
    private readonly WorkPerformer _performer = new(records, port, clock);

    /// <summary>The loop's one observer (FABRIC-PERFORM1i): an observed plugin node reached by its believed event.</summary>
    private readonly WorkObserver _observer = new(records, port, clock);

    /// <summary>Daoris's own close (FABRIC-CLOSE1b), where the loop holds the close door; none, and no quest is closed by a graph.</summary>
    private readonly QuestCloser? _closer = closes is null ? null : new QuestCloser(records, closes, clock);

    /// <summary>Every quest the last look read, by id: a close's chain, and what its quests now read (FABRIC-CLOSE1b).</summary>
    private Dictionary<string, QuestView> _quests = new(StringComparer.Ordinal);

    private readonly Func<bool> _holdsLease = holdsLease ?? (() => true);

    private readonly SemaphoreSlim _one = new(1, 1);

    /// <summary>The governed units the last look read, by id: what a wake judges with.</summary>
    private Dictionary<string, GovernedUnit>? _governed;

    /// <summary>
    /// The choices <see cref="_governed"/> was judged under, as read (FABRIC-RUN1e): a wake under other choices reads the units afresh before
    /// any attempt is heard, asked after, retried or started, so a unit whose graph was removed or edited is never judged under the old one.
    /// </summary>
    private string? _choices;

    /// <summary>The last unit a pass evaluated when the units concerned passed <see cref="UnitsPerPass"/>: the next pass begins after it (FABRIC-RUN1e).</summary>
    private string? _after;

    /// <summary>Whether the plugins were last told a set naming anything: a disarmed pass empties it once.</summary>
    private volatile bool _told;

    /// <summary>When the next timer the records persist comes due (a retry's delay, a running answer's wait, a deadline), or null.</summary>
    public DateTimeOffset? NextDue { get; private set; }

    /// <summary>
    /// Whether any graph the choices set names a plugin's performed node: a landing rule's <c>cycle</c>, a repository's or a workspace's
    /// <c>workCycles</c>. Read from the choices alone, so an install that names none is judged without a file, a git call or a process.
    /// </summary>
    /// <remarks>
    /// FABRIC-PERFORM1i: a plugin's observed node arms it too, the parent's ruling for the row (the owner's aim, "maximize the automation", and a
    /// graph a person wrote naming the step): RUN1 armed on performed nodes alone. Choices naming neither still read, tell, write and start nothing.
    /// </remarks>
    public static bool Armed(DriverConfig config) =>
        config.Landings.Values.Concat(config.WorkspaceLandings.Values)
            .Any(rule => rule.Cycle is not null && Acts(NodeGraphHomes.OfLanding(rule, LandingSource.Repository).Graph))
        || config.WorkCycles.Values.Concat(config.WorkspaceWorkCycles.Values).SelectMany(kinds => kinds.Values)
            .Any(json => NodeGraphs.Read(json).Graph is var graph && (Acts(graph) || QuestClosing.Holds(graph)));

    /// <summary>
    /// The units a graph naming a plugin's performed node governs (<see cref="NodeGraphHomes.Work"/>), in id order, each with its graph, run
    /// and declarations. A plugin's performed node is evaluated and started under its plugin's step (<paramref name="steps"/>): that step's
    /// <c>perform</c>, and its retry, timeout and evidence where the graph's node declares none. A node whose plugin declares no such step at
    /// the graph's version and reach is never sent.
    /// </summary>
    /// <param name="kept">The closure graph this home keeps under a digest (<see cref="ClosureGraphs.Read"/>), or null where it keeps none.</param>
    public static IReadOnlyList<GovernedUnit> Govern(
        DriverConfig config, NodeUnitsRead read, IReadOnlyList<NodeDeclaration> steps, Func<string, NodeGraph?>? kept = null)
    {
        var governed = new List<GovernedUnit>();
        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        var quests = Quests(read);
        foreach (var each in read.Units.OrderBy(unit => unit.Id, StringComparer.Ordinal))
        {
            var unit = each;
            var repository = unit.Owner.Repository ?? "";
            var graph = NodeGraphHomes.Work(config, repository, unit.Owner.Workspace, unit.Kind).Graph;

            // FABRIC-CLOSE1b: a closure graph governs a quest bound to it at its take, and only that quest; any other keeps today's done.
            // FABRIC-CLOSE1l, the review's finding 6: the graph it was bound to, kept by this home where the choices hold another or none.
            var closes = false;
            if (unit.Kind == WorkUnitKind.Quest && unit.Records.Quest is { Closure: { Length: > 0 } bound } quest)
            {
                if (graph is not null && QuestClosing.Holds(graph) && NodeGraphs.Digest(graph) == bound) closes = true;
                else if (kept?.Invoke(bound) is { } bin)
                {
                    graph = bin;
                    closes = true;
                }

                // Its revision is its chain's and its branches' (the review's findings 5 and 8), so a commit or an ancestor's move lapses it.
                if (closes) unit = unit with { Subject = unit.Subject with { Revision = QuestClosing.Revision(quest, quests, read.Units) } };
            }

            if (graph is null || (!Acts(graph) && !closes)) continue;
            var dispatchable = new HashSet<string>(StringComparer.Ordinal);
            var observable = new HashSet<string>(StringComparer.Ordinal);
            var declared = new Dictionary<string, NodeDeclaration>(StringComparer.Ordinal);
            foreach (var node in graph.Nodes)
            {
                declared[node.Id] = node;
                if (!(Performs(node) || Observes(node)) || steps.FirstOrDefault(step => step.Kind == node.Kind) is not { } step
                    || step.Version != node.Version || step.Reach != node.Reach)
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
                (Performs(node) ? dispatchable : observable).Add(node.Id);
            }

            // FABRIC-PERFORM1i: the host's half of the frame, the unit it is told for and its repository's identity, read once per checkout.
            var digest = NodeGraphs.Digest(graph);
            var root = read.Roots.GetValueOrDefault(repository) ?? "";
            if (!identities.TryGetValue(root, out var identity)) identities[root] = identity = RepositoryIdentity.Of(root);
            var watched = new WatchedUnit(repository, unit.Owner.Workspace, root,
                unit.Kind == WorkUnitKind.Branch ? unit.Subject.Artifact ?? "" : "", unit.Subject.Revision ?? "", null, null, [])
            {
                Unit = unit.Id, Identity = identity,
            };
            governed.Add(new GovernedUnit(unit, graph, digest, NodeRuns.Of(unit.Id, digest), declared, dispatchable, watched)
            {
                Closes = closes, Observable = observable,
            });
        }

        return governed;
    }

    /// <summary>
    /// Withdraws what the runtime holds once the choices no longer arm it (FABRIC-RUN1e): the awaited set told nothing, the timers and the
    /// governed units cleared. Called before the plan's supervision, so a plugin the last graph awaited is not started again by the look that
    /// reads that graph's removal. Returns whether the choices disarm the runtime.
    /// </summary>
    public async Task<bool> WithdrawAsync(DriverConfig config, CancellationToken ct)
    {
        if (Armed(config) || KeepsClosures) return false;
        if (!Holding) return true;
        await _one.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Withdraw();
        }
        finally
        {
            _one.Release();
        }

        return true;
    }

    /// <summary>
    /// Whether this loop closes quests and its home keeps a closure graph a quest was bound to (FABRIC-CLOSE1l, the review's finding 6): it stays
    /// armed whatever the choices say, so a close in flight is reconciled and a bound quest closed by its graph. One stat; a home that never
    /// bound a quest keeps none, and is judged by its choices alone.
    /// </summary>
    private bool KeepsClosures => _closer is not null && ClosureGraphs.Any(records.Home);

    /// <summary>Every quest a read holds, by id.</summary>
    private static Dictionary<string, QuestView> Quests(NodeUnitsRead read) =>
        read.Units.Select(unit => unit.Records.Quest).OfType<QuestView>()
            .GroupBy(quest => quest.Id, StringComparer.Ordinal).ToDictionary(same => same.Key, same => same.First(), StringComparer.Ordinal);

    /// <summary>Whether the runtime holds anything a disarm withdraws: an awaited set told, units governed, a timer.</summary>
    private bool Holding => _told || _governed is not null || NextDue is not null || _toldText.Length > 0;

    /// <summary>
    /// Disarmed (FABRIC-RUN1e): the plugins are told nothing is awaited where something was, so their processes stop, and everything cached is
    /// cleared whatever was told, a pending retry's timer included, so nothing wakes the loop and a later arming reads the units afresh.
    /// Nothing is read. Called holding <see cref="_one"/>.
    /// </summary>
    private void Withdraw()
    {
        if (_told) port.Watch(NoneAwaited);
        (_told, _toldText, _governed, _choices, _after, NextDue) = (false, "", null, null, null, null);
    }

    /// <summary>
    /// One pass, at a look (<see cref="NodeRuntimeLook.Whole"/>) or a wake: completions heard, attempts due asked after before any new start,
    /// retries due made ready, the concerned units evaluated and their starts made within <see cref="SendsPerPass"/>, the awaited set told.
    /// </summary>
    public async Task<NodeRuntimePass> PassAsync(NodeRuntimeLook look, CancellationToken ct)
    {
        // Disarmed (FABRIC-RUN1e): whatever was told or cached is withdrawn, a pending retry's timer included; nothing is read.
        if (await WithdrawAsync(look.Config, ct).ConfigureAwait(false)) return NodeRuntimePass.Idle;
        await _one.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_holdsLease()) return new NodeRuntimePass(true, [], [], []);

            // FABRIC-RUN1e: governance is read afresh at the look's deadline, at the first pass, and at a wake under choices other than the
            // ones it was judged under, before any attempt is heard, asked after, retried or started; read afresh, every governed unit is judged.
            var choices = look.Config.Source ?? look.Config.ToJson();
            var fresh = look.Whole || _governed is null || !string.Equals(choices, _choices, StringComparison.Ordinal);
            if (fresh)
            {
                var read = await look.Units(ct).ConfigureAwait(false);
                var home = records.Home;
                var kept = _closer is null ? null : (Func<string, NodeGraph?>)(digest => ClosureGraphs.Read(home, digest));
                (_governed, _choices) = (Govern(look.Config, read, look.Steps(), kept).ToDictionary(each => each.Unit.Id, StringComparer.Ordinal), choices);
                _quests = Quests(read);
                if (_closer is not null) Keep(look.Config, _governed.Values);
            }

            var governed = _governed!;
            NodeDeclaration? Declared(string unit, string node) => governed.TryGetValue(unit, out var each) ? each.Declared.GetValueOrDefault(node) : null;

            // FABRIC-RUN1e: one count of calls for the whole pass, reconciliation's first, so four is the pass's bound and not its starts' alone.
            var sends = new WorkSends(SendsPerPass);
            var done = new List<WorkPerformed>();
            // FABRIC-PERFORM1c: a completion is believed only for a node its plugin still declares under the cycle governing its unit (§C4's
            // second rule), on that unit as the plugins are told it now (the third and fourth).
            NodeDeclaration? Performed(string unit, string node) =>
                governed.TryGetValue(unit, out var each) && each.Dispatchable.Contains(node) ? each.Declared[node] : null;
            var heard = port.TakeSteps();
            done.AddRange(_performer.Hear([.. heard.Where(each => each.Step.Invocation is not null)], Performed, unit => governed.TryGetValue(unit, out var each) ? each.Watched : null));
            // FABRIC-PERFORM1i: an event naming no invocation is an observed node's, bound to the frame its process was sent and the tips read
            // now, and written before the evaluation below fires its wires.
            done.AddRange(await _observer.HearAsync([.. heard.Where(each => each.Step.Invocation is null)], governed, look.Tips, ct).ConfigureAwait(false));
            // FABRIC-PERFORM1g: the person's word on a held attempt, applied under the declaration governing now, before any new attempt.
            done.AddRange(_performer.Resolve(Declared));
            done.AddRange(await _performer.ReconcileAsync(Declared, sends, ct).ConfigureAwait(false));
            done.AddRange(_performer.Due(Declared));

            var said = new List<string>();

            // FABRIC-CLOSE1b, §E5 step 4: a close whose answer was lost is sent again, the same close, before anything new starts.
            if (_closer is not null)
            {
                done.AddRange(await _closer.ReconcileAsync(sends, ct).ConfigureAwait(false));
                said.AddRange(_closer.TakeSaid());
            }

            var named = done.Select(each => each.Unit).ToHashSet(StringComparer.Ordinal);
            var concerned = Turn(governed.Values.Where(each => fresh || named.Contains(each.Unit.Id) || OwesStart(each)));
            foreach (var each in concerned)
            {
                if (each.Closes) Settle(each, done, said);
                await EvaluateAsync(each, sends, done, said, look.Units, ct).ConfigureAwait(false);
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
    /// it is then, its moves kept and its starts made, each send taking one of the pass's <paramref name="sends"/>.
    /// </summary>
    private async Task EvaluateAsync(
        GovernedUnit governed, WorkSends sends, List<WorkPerformed> done, List<string> said, Func<CancellationToken, Task<NodeUnitsRead>> units,
        CancellationToken ct)
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
                if (node.Kind == NodeKinds.QuestClose)
                {
                    var closed = await CloseAsync(governed, node, start, evaluated.Statuses[start.Node], sends, said, units, ct).ConfigureAwait(false);
                    if (closed is not null) done.Add(closed);
                    started |= closed is { What: not PerformedWhat.Refused };
                }
                else if (node.Reach == NodeReach.Observed && (BuiltInNodes.Observed.Contains(node.Kind) || governed.Observable.Contains(node.Id)))
                {
                    // FABRIC-PERFORM1i: a plugin's observed node starts as a built-in does, ready to active with its firings written, and is
                    // published to its plugin only after (Tell, below).
                    var begun = _performer.Begin(node, start, evaluated.Statuses[start.Node]);
                    done.Add(begun);
                    started |= begun.What == PerformedWhat.Begun;
                }
                else if (governed.Dispatchable.Contains(node.Id) && sends.Left > 0 && port.GenerationOf(PluginOf(node.Kind)) is not null && sends.Take())
                {
                    // A process not up yet is awaited (Tell below), and the start is made at a later pass once it is: never sent blind.
                    var sent = await _performer.StartAsync(node, start, ct).ConfigureAwait(false);
                    done.Add(sent);
                    started |= sent.What != PerformedWhat.Refused;
                }

                // A pressed node's task and a waited node's wait are HUMAN1's and WAITUNIT1's to start: left ready.
            }

            if (evaluated.Settled && !started) break;
        }
    }

    /// <summary>
    /// A <c>quest.close</c> the evaluation proposes to start (FABRIC-CLOSE1b, §E5): on a quest bound to this graph alone, sent once its chain's
    /// submissions are current and unheld, taking one of the pass's <paramref name="sends"/>; reached without a send where its chain's close
    /// already closed its quest; left ready while it waits. Null where nothing was done.
    /// </summary>
    private async Task<WorkPerformed?> CloseAsync(
        GovernedUnit governed, NodeDeclaration node, WireStart start, NodeStatus evaluated, WorkSends sends, List<string> said,
        Func<CancellationToken, Task<NodeUnitsRead>> units, CancellationToken ct)
    {
        if (_closer is null || !governed.Closes || governed.Unit.Records.Quest is not { } quest) return null;
        var judged = QuestClosing.Judge(quest, _quests);
        if (judged.Overridden)
        {
            // Done by its chain's close: reached on that close, never sent. Done otherwise is the person's, which Settle reads.
            return QuestClosing.Naming(records.All().Records, quest.Id).FirstOrDefault(Closed) is { } chained
                ? _closer.ReachByChain(node, start, evaluated, chained)
                : null;
        }

        if (judged.Waits is not null || !sends.Take()) return null;

        // FABRIC-CLOSE1l, the review's finding 5 (§E5 step 1): git (each branch's tip, a fresh read of its refs), the binding and the chain
        // are read again right before the close. The same revision means every prerequisite acceptance bound to it still stands, since this
        // loop is the record's one writer; another means a commit, a submission or a hold moved since the evaluation, and the close waits for
        // the next pass to judge it on what is there now.
        var again = await units(ct).ConfigureAwait(false);
        var quests = Quests(again);
        var stale = !quests.TryGetValue(quest.Id, out var now) ? "its quest was not read again"
            : !string.Equals(now.Closure, governed.GraphDigest, StringComparison.Ordinal) ? "its binding moved"
            : QuestClosing.Revision(now, quests, again.Units) != governed.Unit.Subject.Revision ? "its chain or a branch it worked moved since it was judged"
            : !QuestClosing.Judge(now, quests).Named.SequenceEqual(judged.Named) ? "what it would close moved"
            : null;
        if (stale is not null)
        {
            _governed = null;
            said.Add($"work  {governed.Unit.Id}: its close was not sent: {stale}, so it is judged again on what is there now.");
            return null;
        }

        var sent = await _closer.StartAsync(node, start, evaluated, judged.Named, ct).ConfigureAwait(false);
        said.AddRange(_closer.TakeSaid());
        return sent;
    }

    /// <summary>
    /// Keeps each closure graph a governed quest is closed by, as the choices hold it now (FABRIC-CLOSE1l, the review's finding 6): a quest
    /// bound before a graph was edited or removed is closed by the graph it was bound to. Kept once, by its digest.
    /// </summary>
    private void Keep(DriverConfig config, IEnumerable<GovernedUnit> governed)
    {
        foreach (var each in governed.Where(each => each.Closes))
        {
            var home = NodeGraphHomes.Work(config, each.Unit.Owner.Repository ?? "", each.Unit.Owner.Workspace, WorkUnitKind.Quest);
            if (home.Graph is { } graph && home.Cycle is { } json && NodeGraphs.Digest(graph) == each.GraphDigest) ClosureGraphs.Keep(records.Home, each.GraphDigest, json);
        }
    }

    /// <summary>A close attempt that closed what it named.</summary>
    private static bool Closed(NodeAttempt attempt) => attempt is { State: AttemptStates.Settled, Outcome: WorkStates.Reached };

    /// <summary>
    /// What a closure graph's unit owes before it is evaluated (FABRIC-CLOSE1b, §E5): a close found closed only in part since, said once,
    /// naming each quest no longer done on the submission it closed; and the person's own done, which ends the graph as overridden, stopping
    /// every live node, said once. A close still waiting for its answer is reconciled first, and a chain's close is never the person's.
    /// </summary>
    private void Settle(GovernedUnit governed, List<WorkPerformed> done, List<string> said)
    {
        if (governed.Unit.Records.Quest is not { } quest) return;
        var unit = governed.Unit.Id;
        var now = clock();
        var all = records.All().Records;
        if (all.FirstOrDefault(record => record.Unit == unit) is { } own)
        {
            foreach (var attempt in own.Attempts.Where(attempt => attempt.Plugin == QuestClosing.Sender && Closed(attempt)))
            {
                foreach (var lost in attempt.Close!.Quests.Where(named => Lost(named) && own.History.All(entry => entry.Why != $"{QuestClosing.Partial} #{named.Id}")))
                {
                    var (refused, _) = records.Edit(unit, record =>
                        (record with { History = [.. record.History, new NodeHistory(now, $"{QuestClosing.Partial} #{lost.Id}", WorkPerformer.StatusOf(record, attempt.Node))] }, true));
                    if (refused is not null) continue;
                    said.Add($"work  {unit}: partially closed: #{lost.Id} is no longer done on the submission its close judged, since its take was lost "
                             + "to another machine, which holds its work now. The others stay done, and Daoris never closes it over that take; stop the "
                             + $"close node `{attempt.Node}` to leave it.");
                }
            }
        }

        if (quest.Status != "Done" || !QuestClosing.Judge(quest, _quests).Overridden) return;
        var naming = QuestClosing.Naming(all, quest.Id).ToList();
        if (naming.Any(attempt => Closed(attempt) || attempt.State is AttemptStates.Recorded or AttemptStates.Unknown or AttemptStates.Sent)) return;

        // §E5's override: the person closed every quest the close would close. Its pending nodes are stopped, an attempt in flight still reconciled.
        var (_, stopped) = records.Edit<List<string>>(unit, record =>
        {
            var names = new List<string>();
            var next = record;
            foreach (var node in governed.Graph.Nodes)
            {
                var status = WorkPerformer.StatusOf(next, node.Id);
                if (!NodeStates.Live.Contains(status.State)) continue;
                var moved = NodeLifecycle.Apply(governed.Declared[node.Id], status, new NodeMove(NodeStates.Stopped, NodeCause.Person) { Words = "closed by you before its end" });
                if (!moved.Moved) continue;
                next = WorkPerformer.Kept(next, moved, QuestClosing.Overridden, now);
                names.Add(node.Id);
            }

            return (names.Count == 0 ? null : next, names);
        });
        if (stopped is not { Count: > 0 }) return;
        done.AddRange(stopped.Select(node => new WorkPerformed(unit, node, PerformedWhat.Applied, NodeStates.Stopped)));
        said.Add($"work  {unit}: #{quest.Id} closed by you before its end, so its graph ends as overridden; stopped: {string.Join(", ", stopped)}.");

        // A quest the close named reads lost: not done, and no longer on the submission the close judged (a rebase forgot it with its take).
        bool Lost(QuestCloseItem named) =>
            _quests.TryGetValue(named.Id, out var standing) && standing.Status != "Done"
            && !(standing.Submission is { } current && current.Machine == named.Machine && current.Sequence == named.Sequence);
    }

    /// <summary>
    /// The units a pass evaluates, in id order (FABRIC-RUN1e): all of them up to <see cref="UnitsPerPass"/>; past it, the next
    /// <see cref="UnitsPerPass"/> after the last unit the pass before reached, round again from the first, so no unit waits behind the same
    /// prefix at every pass.
    /// </summary>
    private List<GovernedUnit> Turn(IEnumerable<GovernedUnit> concerned)
    {
        var ordered = concerned.OrderBy(each => each.Unit.Id, StringComparer.Ordinal).ToList();
        if (ordered.Count <= UnitsPerPass) return ordered;
        var next = _after is null ? 0 : ordered.FindIndex(each => string.CompareOrdinal(each.Unit.Id, _after) > 0);
        if (next < 0) next = 0;
        var turn = ordered.Skip(next).Concat(ordered.Take(next)).Take(UnitsPerPass).ToList();
        _after = turn[^1].Unit.Id;
        return turn;
    }

    /// <summary>Whether a unit's record holds one of its dispatchable nodes ready: a start owed, judged at a wake as well as at the look.</summary>
    private bool OwesStart(GovernedUnit governed) =>
        records.Read(governed.Unit.Id) is { } record
        && governed.Dispatchable.Concat(governed.Observable).Any(node => record.Statuses.GetValueOrDefault(node)?.State == NodeStates.Ready);

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
            foreach (var id in each.Dispatchable.Concat(each.Observable))
            {
                // FABRIC-PERFORM1i: an observed node is published once begun, never while ready, so an event never precedes its start.
                var waiting = record.Attempts.Any(attempt => attempt.Node == id && attempt.State is AttemptStates.Recorded or AttemptStates.Sent or AttemptStates.Unknown);
                var state = record.Statuses.GetValueOrDefault(id)?.State;
                if (each.Observable.Contains(id) ? state != NodeStates.Active : !waiting && state is not (NodeStates.Ready or NodeStates.Active or NodeStates.Unknown)) continue;
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

        // FABRIC-RUN1e: published only while this loop still holds the lease, asked after the pass's awaits; a loop that lost it meanwhile
        // tells nothing and keeps no timer, and the lease's next holder tells its own set.
        if (!_holdsLease())
        {
            NextDue = null;
            return false;
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

    private static bool Observes(NodeDeclaration node) => node.Reach == NodeReach.Observed && NodeKinds.IsPlugin(node.Kind);

    /// <summary>Whether a graph names a plugin's performed or observed node: what arms the runtime and makes a unit governed (FABRIC-PERFORM1i).</summary>
    private static bool Acts(NodeGraph? graph) => graph?.Nodes.Any(node => Performs(node) || Observes(node)) == true;
}
