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
public sealed record NodeUnitsRead(IReadOnlyList<WorkUnit> Units, IReadOnlyDictionary<string, string> Roots)
{
    /// <summary>
    /// The tip of each branch a bound quest's chain worked, as git answered this one read (FABRIC-CLOSE1p, <see cref="QuestClosing.TipsAsync"/>),
    /// by the branch unit's id: a commit, empty where the branch is not there, null where git did not say. What a closing quest's revision digests
    /// (<see cref="QuestClosing.Revision"/>). Null where the read asked no git at all: a close made on it waits (FABRIC-CLOSE1r).
    /// </summary>
    public IReadOnlyDictionary<string, string?>? Tips { get; init; }
}

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

    /// <summary>
    /// Each named branch's remote-tracking ref in one checkout (FABRIC-RUN1b, <see cref="NodeTips.RemotesAsync"/>): what <c>pushed</c> reads,
    /// once per checkout per pass, and only where a <c>pushed</c> node waits on a landing a plugin answered it pushed. Empty where the ref is not
    /// there; a branch it could not read is absent, and its node stays unknown.
    /// </summary>
    public Func<string, IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlyDictionary<string, string>>> Remotes { get; init; } =
        (_, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

    /// <summary>
    /// Whether one commit holds another in one checkout (FABRIC-RUN1b, <see cref="NodeTips.HoldsAsync"/>): an ahead <c>pull-request</c>'s check,
    /// asked only where one waits on a completed answer from a newer commit; null where git could not say.
    /// </summary>
    public Func<string, string, string, CancellationToken, Task<bool?>> Holds { get; init; } = (_, _, _, _) => Task.FromResult<bool?>(null);

    /// <summary>
    /// The second opinion's and the look's gates for a branch unit's work as a landing door reads them (FABRIC-RUN1c, WIRE1g, WIRE1h; NODE1b:
    /// <see cref="OpinionGate.JudgeAsync"/> and <see cref="ReviewGate"/>'s live read), what its <c>opinion</c> and <c>look</c> nodes observe:
    /// read once a pass for a unit, and only where one of them waits. Either is null where it was not read, and its node waits.
    /// </summary>
    public Func<WorkUnit, CancellationToken, Task<(OpinionGateState? Opinion, ReviewGateState? Review)>> Gates { get; init; } =
        (_, _) => Task.FromResult<(OpinionGateState?, ReviewGateState?)>((null, null));

    /// <summary>
    /// The named workflow's version a quest unit's run is bound to (FABRIC-RUN1c; §E7, D157 §5.1: the decoder's read for a run, through
    /// <see cref="WorkflowRunNodes.VersionOf"/>), given the quests the pass read for its chain; null where its run follows Current or has no
    /// binding. A unit bound to one is governed by that version, never by Current's graph (<see cref="NodeRuntime.BoundIn"/> reads the home's).
    /// </summary>
    public Func<WorkUnit, IReadOnlyCollection<QuestView>, DecodedVersion?> Bound { get; init; } = (_, _) => null;
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

    /// <summary>Each node's place in its graph (FABRIC-RUN1f, <see cref="NodeGraphs.Places"/>): what its scope, receipts and binding name.</summary>
    public IReadOnlyDictionary<string, string> Places { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
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
/// A run's identity under Current's graphs (FABRIC-RUN1f; the cutover design §9): a digest of the unit alone, so a run begins when a graph
/// naming a plugin's performed node first governs the unit and an edit of that graph begins none; never at a wake, a look, a restart or a new
/// revision either (a moved revision lapses the bindings it moved, WIRE1e, in the same run). What an edit changes is each node's place
/// (<see cref="NodeGraphs.Places"/>), which lapses that node and what rests on it alone.
/// </summary>
public static class NodeRuns
{
    public static string Of(string unit) =>
        "run-" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"node-run\n{unit}\n")))[..24];

    /// <summary>
    /// The run's name before FABRIC-RUN1f, of the unit and the graph governing it, so each edit began another: what a scope, a receipt and a
    /// firing written before name, judged on the graph now (<see cref="WireRun.Legacy"/>) so nothing lapses or fires again at the upgrade.
    /// </summary>
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
/// <para><b>Off unless a graph naming a plugin's performed node governs something</b> (the owner's ruling for RUN1), or its observed node
/// (FABRIC-PERFORM1i, the parent's ruling): with no such graph in the choices, a pass reads no unit, no plugin step and no record, tells and
/// takes nothing, writes nothing and starts no process or git.</para>
/// <para><b>Observed nodes</b> (FABRIC-PERFORM1i): begun as a built-in is, published once active, and reached by a believed event through
/// <see cref="WorkObserver"/> before the evaluation, whose tips are read fresh (<see cref="NodeRuntimeLook.Tips"/>). A branch unit's is bound
/// to its branch's tip read in the pass, not the unit's lagging revision, and rebinds when the branch moves (FABRIC-PERFORM1m,
/// <see cref="PassTips"/>: one read of each checkout a pass); an event at that tip heard before the rebinding is heard again after it, in
/// the same pass (FABRIC-PERFORM1o, <see cref="WorkLagging"/>).</para>
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

    /// <summary>
    /// The quests the runtime last read, for the doors to judge a held close against (FABRIC-CLOSE1q3): read-only, never written by a door;
    /// null before the first read of a start, where a door judges nothing.
    /// </summary>
    public IReadOnlyDictionary<string, QuestView>? LastQuests { get; private set; }

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

    /// <summary>Whether this pass read the units afresh (FABRIC-CLOSE1s): the governed revisions are then as current as the pass; set under <see cref="_one"/>.</summary>
    private bool _readThisPass;

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
    /// <param name="versionOf">
    /// The named version a quest unit's run is bound to (<see cref="NodeRuntimeLook.Bound"/>), or null where none: FABRIC-RUN1c, a quest bound
    /// to one is governed by it alone (§E7, one governs a run, by its binding), whose engine is D157's run until the cutover (FABRIC-CUTOVER1),
    /// so Current's graph never governs it beside that engine. An unreadable binding holds it the same way: its run cannot start (D157 §5.2).
    /// </param>
    public static IReadOnlyList<GovernedUnit> Govern(
        DriverConfig config, NodeUnitsRead read, IReadOnlyList<NodeDeclaration> steps, Func<string, NodeGraph?>? kept = null,
        Func<WorkUnit, IReadOnlyCollection<QuestView>, DecodedVersion?>? versionOf = null)
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
                // FABRIC-CLOSE1p: the branches' tips as git answered this read, never their records' kept revisions.
                if (closes) unit = unit with { Subject = unit.Subject with { Revision = QuestClosing.Revision(quest, quests, read.Units, read.Tips) } };
            }

            // FABRIC-RUN1c: a quest whose run is bound to a named version is that version's (§E7), run by D157's engine: Current's graph does not
            // govern it as well. A quest bound to a closure graph at its take is that graph's, as above.
            if (!closes && unit.Kind == WorkUnitKind.Quest && versionOf?.Invoke(unit, quests.Values) is not null) continue;

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
            governed.Add(new GovernedUnit(unit, graph, digest, NodeRuns.Of(unit.Id), declared, dispatchable, watched)
            {
                Closes = closes, Observable = observable, Places = NodeGraphs.Places(graph),
            });
        }

        return governed;
    }

    /// <summary>
    /// The named version a quest unit's run is bound to in <paramref name="home"/> (FABRIC-RUN1c; D157 §5.1): the run its chain's first quest in
    /// its repository names (<see cref="WorkflowRunBindings.RunOf"/>), read through the home's workflows and run bindings
    /// (<see cref="WorkflowRunNodes.VersionOf"/>) and never written; null under Current or with no binding. A home that bound no run reads one
    /// absent file a quest.
    /// </summary>
    public static Func<WorkUnit, IReadOnlyCollection<QuestView>, DecodedVersion?> BoundIn(string home)
    {
        var store = new WorkflowFiles(home);
        var runs = new WorkflowRunFiles(home);
        return (unit, quests) => unit.Records.Quest is not { } quest
            ? null
            : WorkflowRunNodes.VersionOf(store, runs, WorkflowRunBindings.RunOf(ReviewGate.ChainOf([.. quests], quest.Id), unit.Owner.Repository ?? "") ?? quest.Id);
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
        LastQuests = null;
        QuestReads.Delete(records.Home);
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
            // FABRIC-CLOSE1q2: a word on a close is judged on the quests as they stand, so a pass applying one reads them afresh.
            var fresh = look.Whole || _governed is null || !string.Equals(choices, _choices, StringComparison.Ordinal) || _closer?.Worded() == true;
            _readThisPass = fresh;
            if (fresh)
            {
                var read = await look.Units(ct).ConfigureAwait(false);
                var home = records.Home;
                var kept = _closer is null ? null : (Func<string, NodeGraph?>)(digest => ClosureGraphs.Read(home, digest));
                (_governed, _choices) = (Govern(look.Config, read, look.Steps(), kept, look.Bound).ToDictionary(each => each.Unit.Id, StringComparer.Ordinal), choices);
                _quests = Quests(read);
                LastQuests = _quests;
                QuestReads.Keep(home, _quests); // FABRIC-CLOSE1q5: the terminal's `work held` judges on it; the lease is held here.
                if (_closer is not null) Keep(look.Config, _governed.Values);
            }

            var governed = _governed!;
            NodeDeclaration? Declared(string unit, string node) => governed.TryGetValue(unit, out var each) ? each.Declared.GetValueOrDefault(node) : null;

            // FABRIC-PERFORM1m: the pass's tips, each checkout read once by whatever asks of it first (the hearing, a rebinding's check, a
            // binding), never kept past the pass.
            var tips = new PassTips(look.Tips, Observing(governed.Values));

            // FABRIC-RUN1b: the look's git reads for the observed built-ins, each checkout's remote-tracking refs read once a pass, and only where
            // a node waits on them. FABRIC-RUN1c: D157's gates likewise, once a pass for a unit whose opinion or look waits on them.
            var git = new PassReads(new PassGit(look.Remotes, look.Holds, Pushing(governed.Values)), new PassGates(look.Gates));

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
            // now, and written before the evaluation below fires its wires. FABRIC-PERFORM1o: one at the tip read now whose node still awaits an
            // older binding is held, and heard again once the evaluation has rebound the node.
            var lagging = new List<WorkLagging>();
            done.AddRange(await _observer.HearAsync([.. heard.Where(each => each.Step.Invocation is null)], governed, tips.ReadAsync, ct, lagging).ConfigureAwait(false));
            // FABRIC-PERFORM1g: the person's word on a held attempt, applied under the declaration governing now, before any new attempt.
            done.AddRange(_performer.Resolve(Declared));
            // FABRIC-CLOSE1q: the person's word on a held close, a close's recovery, before the closer sends any close again below; FABRIC-CLOSE1q2:
            // judged on the quests this pass read, so a word on a close for a submission reopened or replaced since never reaches the next one.
            if (_closer is not null) done.AddRange(_closer.Resolve(Declared, _quests));
            done.AddRange(await _performer.ReconcileAsync(Declared, sends, ct).ConfigureAwait(false));
            done.AddRange(_performer.Due(Declared));

            var said = new List<string>();

            // FABRIC-CLOSE1b, §E5 step 4: a close whose answer was lost is reconciled before anything new starts; FABRIC-CLOSE1u: by asking its
            // door what became of its key, never by sending it again blind, so one never delivered is judged again on what is there now.
            if (_closer is not null)
            {
                done.AddRange(await _closer.ReconcileAsync(sends, ct).ConfigureAwait(false));
                said.AddRange(_closer.TakeSaid());
            }

            var named = done.Select(each => each.Unit).Concat(lagging.Select(each => each.Unit)).ToHashSet(StringComparer.Ordinal);
            var owing = new List<GovernedUnit>();
            foreach (var each in governed.Values)
            {
                if (fresh || named.Contains(each.Unit.Id) || OwesStart(each) || await RebindsAsync(each, tips, ct).ConfigureAwait(false)) owing.Add(each);
            }

            var concerned = Turn(owing);
            foreach (var each in concerned)
            {
                if (each.Closes) Settle(each, done, said);
                await EvaluateAsync(each, sends, done, said, look.Units, tips, git, ct).ConfigureAwait(false);
            }

            await HearAgainAsync(lagging, governed, concerned, sends, done, said, look.Units, tips, git, ct).ConfigureAwait(false);
            if (fresh || done.Count > 0) Prune();
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
        PassTips tips, PassReads git, CancellationToken ct)
    {
        var unit = governed.Unit.Id;
        // FABRIC-PERFORM1m: a branch unit's observed plugin nodes bind to its branch's tip read this pass, not to the unit's revision, which is
        // the end fact's and lags a moved branch until the end judgement catches up; the pass's one read of the checkout, null where unread.
        var tipped = Tipped(governed);
        var tip = tipped ? await tips.TipAsync(governed.Watched.Root, governed.Watched.Branch, ct).ConfigureAwait(false) : null;
        for (var round = 0; round <= governed.Graph.Nodes.Count; round++)
        {
            if (records.Read(unit) is not { } record)
            {
                said.Add($"work  {unit}: its node record does not read, so nothing on it is judged or started until a person mends it.");
                break;
            }

            // FABRIC-RUN1b: what git says for the built-ins this round's record holds waiting on it, read once a pass; FABRIC-RUN1c: the gates
            // its opinion and look wait on, likewise.
            var reads = await GitReadsAsync(governed, record, git.Git, ct).ConfigureAwait(false);
            var gates = await GatesAsync(governed, record, git.Gates, ct).ConfigureAwait(false);
            var (before, evaluated) = Evaluate(governed, record, tipped, tip, reads, gates);
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

                    // FABRIC-CLOSE1p: a fence that found the unit moved (CloseAsync dropped what this pass governs) ends its judgement here: the
                    // next pass reads it again, and no further round reads git again only to find the same stale revision.
                    if (_governed is null) return;
                }
                else if (node.Reach == NodeReach.Observed && (Begins(node.Kind) || governed.Observable.Contains(node.Id)))
                {
                    // FABRIC-PERFORM1m: an observed plugin node whose branch's tip this pass did not read is left as it stands: nothing is
                    // bound on a guess, and the pass that reads the tip begins it there.
                    if (tipped && tip is null && governed.Observable.Contains(node.Id)) continue;

                    // FABRIC-PERFORM1i: a plugin's observed node starts as a built-in does, ready to active with its firings written, and is
                    // published to its plugin only after (Tell, below).
                    var begun = _performer.Begin(node, start, evaluated.Statuses[start.Node]);
                    done.Add(begun);
                    started |= begun.What == PerformedWhat.Begun;
                }
                else if (governed.Dispatchable.Contains(node.Id) && sends.Left > 0 && port.GenerationOf(PluginOf(node.Kind)) is not null && sends.Take())
                {
                    // FABRIC-CLOSE1s: a bound quest's revision is its chain's and branches', from the last full read, which a wake reuses: a start
                    // on a wake reads them again first, and a start whose revision moved is not made, so no attempt is bound to an old tip.
                    if (governed.Closes && !_readThisPass && governed.Unit.Records.Quest is { } bound)
                    {
                        var again = await units(ct).ConfigureAwait(false);
                        if (Moved(governed, bound, again, Quests(again)) is { } moved)
                        {
                            _governed = null;
                            var line = $"work  {unit}: its `{node.Id}` was not started: {moved}, so it is judged again on what is there now.";
                            if (!said.Contains(line, StringComparer.Ordinal)) said.Add(line);
                            return;
                        }
                    }

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
    /// A unit's graph evaluated on its record as it stands (FABRIC-RUN1): each node's status before (the record's, else a fresh one), and
    /// the evaluation on the unit's revision and the triggers its record has fired; a branch unit's observed plugin nodes on the tip its pass
    /// read (FABRIC-PERFORM1m), and what git said for its built-ins (FABRIC-RUN1b, <paramref name="git"/>), handed in. Pure: what it moves is
    /// the caller's to keep, and it reads no git itself.
    /// </summary>
    private static (Dictionary<string, NodeStatus> Before, WireEvaluated Evaluated) Evaluate(
        GovernedUnit governed, NodeRecord record, bool tipped = false, string? tip = null, NodeGitReads? git = null,
        (OpinionGateState? Opinion, ReviewGateState? Review) gates = default)
    {
        var unit = governed.Unit.Id;
        var nodes = governed.Graph.Nodes.Select(node => governed.Declared[node.Id]).ToList();
        var before = nodes.ToDictionary(node => node.Id, node => record.Statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(unit, node.Id), StringComparer.Ordinal);
        var run = new WireRun(governed.GraphDigest, governed.Run)
        {
            Places = governed.Places,
            Legacy = NodeRuns.Of(unit, governed.GraphDigest),
            Revisions = new Dictionary<string, string?>(StringComparer.Ordinal) { [unit] = governed.Unit.Subject.Revision },
            NodeRevisions = tipped ? ObservedRevisions(governed, tip, before) : new Dictionary<string, string?>(StringComparer.Ordinal),
            Fired = record.Fired.ToHashSet(StringComparer.Ordinal),
            Units = new Dictionary<string, WorkUnit>(StringComparer.Ordinal) { [unit] = governed.Unit },
            Git = git,
            Opinions = gates.Opinion is { } opinion
                ? new Dictionary<string, OpinionGateState>(StringComparer.Ordinal) { [unit] = opinion }
                : new Dictionary<string, OpinionGateState>(StringComparer.Ordinal),
            Reviews = gates.Review is { } review
                ? new Dictionary<string, ReviewGateState>(StringComparer.Ordinal) { [unit] = review }
                : new Dictionary<string, ReviewGateState>(StringComparer.Ordinal),
        };
        return (before, WireEvaluation.Evaluate(nodes, governed.Graph.Wires, before, run));
    }

    /// <summary>
    /// The gates a unit's <c>opinion</c> and <c>look</c> observe (FABRIC-RUN1c; WIRE1g, WIRE1h, NODE1b): read through the pass's one read of the
    /// unit, and only where one of them is active or unknown on its record, so a unit whose graph holds neither, or whose gates are settled,
    /// asks nothing. Nothing read leaves both null, and the nodes wait.
    /// </summary>
    private static async Task<(OpinionGateState? Opinion, ReviewGateState? Review)> GatesAsync(
        GovernedUnit governed, NodeRecord record, PassGates gates, CancellationToken ct) =>
        governed.Graph.Nodes.Any(node => node.Kind is NodeKinds.Opinion or NodeKinds.Look
            && record.Statuses.GetValueOrDefault(node.Id)?.State is NodeStates.Active or NodeStates.Unknown)
            ? await gates.OfAsync(governed.Unit, ct).ConfigureAwait(false)
            : default;

    /// <summary>One pass's reads beyond the tips (FABRIC-RUN1b, RUN1c): git's for the observed built-ins, and D157's gates.</summary>
    private sealed record PassReads(PassGit Git, PassGates Gates);

    /// <summary>
    /// What git says for a branch unit's observed built-ins that wait on it (FABRIC-RUN1b; NODE1's choice 8, the look's git reads): a
    /// <c>pushed</c> active or unknown on a landing a plugin answered it pushed reads the landed branch's remote-tracking ref, and a
    /// <c>pull-request</c> active or unknown on an answer completed from another commit asks git's ancestry, each through the pass's one read
    /// (<paramref name="git"/>). Null where nothing waits on git, so a unit with no such node asks it nothing; what was not read stays absent,
    /// and its node unknown.
    /// </summary>
    private static async Task<NodeGitReads?> GitReadsAsync(GovernedUnit governed, NodeRecord record, PassGit git, CancellationToken ct)
    {
        if (governed.Unit.Kind != WorkUnitKind.Branch || BuiltInNodes.LandingOf(governed.Unit) is not { } landing) return null;
        bool Waits(string kind) => governed.Graph.Nodes.Any(node => node.Kind == kind
            && record.Statuses.GetValueOrDefault(node.Id)?.State is NodeStates.Active or NodeStates.Unknown);

        var root = governed.Watched.Root;
        var remote = Waits(NodeKinds.Pushed) && BuiltInNodes.AsksRemote(landing);
        var tip = remote ? await git.RemoteAsync(root, landing.Branch, ct).ConfigureAwait(false) : null;
        var ancestry = Waits(NodeKinds.PullRequest) ? BuiltInNodes.AncestryOf(landing) : null;
        var holds = ancestry is var (holder, held) ? await git.HoldsAsync(root, holder, held, ct).ConfigureAwait(false) : null;
        if (!remote && ancestry is null) return null;
        return new NodeGitReads(
            remote ? each => string.Equals(each.Branch, landing.Branch, StringComparison.Ordinal) ? tip : null : null,
            ancestry is null ? null : (holder, held) => (holder, held) == ancestry ? holds : null);
    }

    /// <summary>Each checkout's landed branches a pass may judge <c>pushed</c> on (FABRIC-RUN1b): what the pass's one read of its remote refs serves.</summary>
    private static Dictionary<string, IReadOnlyCollection<string>> Pushing(IEnumerable<GovernedUnit> governed)
    {
        var branches = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        var pushing = governed.Where(each => each.Unit.Kind == WorkUnitKind.Branch && each.Watched.Root.Length > 0
                && each.Graph.Nodes.Any(node => node.Kind == NodeKinds.Pushed))
            .Select(each => (each.Watched.Root, Landing: BuiltInNodes.LandingOf(each.Unit)))
            .Where(each => each.Landing is { } landing && BuiltInNodes.AsksRemote(landing));
        foreach (var same in pushing.GroupBy(each => each.Root, StringComparer.Ordinal))
        {
            branches[same.Key] = same.Select(each => each.Landing!.Branch).Distinct(StringComparer.Ordinal).ToList();
        }

        return branches;
    }

    /// <summary>
    /// A <c>quest.close</c> the evaluation proposes to start (FABRIC-CLOSE1b, §E5): on a quest bound to this graph alone, sent once its chain's
    /// submissions are current and unheld and each other quest it names has reached its close in its own graph (FABRIC-CLOSE1t), taking one of
    /// the pass's <paramref name="sends"/>; reached without a send where its chain's close
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
            // Done by its chain's close: reached on that close, never sent. Done otherwise is the person's, which Settle reads. FABRIC-CLOSE1q:
            // only a close naming the quest at the submission it stands on now is that close; one kept for an earlier submission is not.
            return QuestClosing.Naming(records.All().Records, quest).FirstOrDefault(Closed) is { } chained
                ? _closer.ReachByChain(node, start, evaluated, chained)
                : null;
        }

        if (judged.Waits is not null)
        {
            // FABRIC-CLOSE1x: a close that waits on its own judgement says why, once a pass however many rounds propose it; none once it closes.
            var waiting = $"work  {governed.Unit.Id}: its close waits: {judged.Waits}.";
            if (!said.Contains(waiting, StringComparer.Ordinal)) said.Add(waiting);
            return null;
        }

        // FABRIC-CLOSE1t: the evaluation that proposed this start fenced this quest's own prerequisites; each other quest the close names is
        // fenced in its own graph, on its own record, before anything is sent or read again. Said once a pass, however many rounds propose it.
        if (Unreached(governed, node, judged.Named) is { } unreached)
        {
            var line = $"work  {governed.Unit.Id}: its close waits for {unreached}.";
            if (!said.Contains(line, StringComparer.Ordinal)) said.Add(line);
            return null;
        }

        if (!sends.Take()) return null;

        // FABRIC-CLOSE1l, the review's finding 5 (§E5 step 1): git (each branch's tip, a fresh read of its refs), the binding and the chain
        // are read again right before the close. The same revision means every prerequisite acceptance bound to it still stands, since this
        // loop is the record's one writer; another means a commit, a submission or a hold moved since the evaluation, and the close waits for
        // the next pass to judge it on what is there now. FABRIC-CLOSE1p: the tips are this read's own snapshot, and one git did not answer
        // leaves the close waiting.
        var again = await units(ct).ConfigureAwait(false);
        var quests = Quests(again);
        var stale = Moved(governed, quest, again, quests)
            ?? (!QuestClosing.Judge(quests[quest.Id], quests).Named.SequenceEqual(judged.Named) ? "what it would close moved" : null);
        if (stale is not null)
        {
            _governed = null;
            var line = $"work  {governed.Unit.Id}: its close was not sent: {stale}, so it is judged again on what is there now.";
            if (!said.Contains(line, StringComparer.Ordinal)) said.Add(line);
            return null;
        }

        var sent = await _closer.StartAsync(node, start, evaluated, judged.Named, ct).ConfigureAwait(false);
        said.AddRange(_closer.TakeSaid());
        return sent;
    }

    /// <summary>
    /// Why a bound quest's unit, governed on the revision of the read before, no longer stands on it in <paramref name="again"/>, the units
    /// read afresh: its quest, its binding, git's tips, or the chain's and its branches' revision (FABRIC-CLOSE1l, CLOSE1p, CLOSE1r). Null
    /// where it stands, so a close or a plugin's start made now rests on what is there.
    /// </summary>
    private static string? Moved(GovernedUnit governed, QuestView quest, NodeUnitsRead again, Dictionary<string, QuestView> quests) =>
        !quests.TryGetValue(quest.Id, out var now) ? "its quest was not read again"
        : !string.Equals(now.Closure, governed.GraphDigest, StringComparison.Ordinal) ? "its binding moved"
        // FABRIC-CLOSE1r: a read that asked no git never closes, so a production read that forgets the tips cannot bring CLOSE1p's defect back.
        : again.Tips is null ? "git's tips were not read for this look"
        : QuestClosing.Unread(now, quests, again.Units, again.Tips) is { } unread ? $"git did not say where `{unread}`, a branch it worked, stands"
        : QuestClosing.Revision(now, quests, again.Units, again.Tips) != governed.Unit.Subject.Revision ? "its chain or a branch it worked moved since it was judged"
        : null;

    /// <summary>
    /// The first quest a chain's close names, besides its own, whose own graph has not reached its close (FABRIC-CLOSE1t; §E5 step 1), said as
    /// the quest and the node it waits on; null where every one has. Each is judged on its own node record, under the graph it is bound to, at
    /// its current binding (its governed revision this pass), as its own evaluation judges it: the close is eligible there, and no prerequisite
    /// wired into it moves in that judgement, so what the close rests on is what its record holds. A record that does not read, a quest not
    /// governed by its graph here, or a graph not judged leaves the close waiting, never judged without it.
    /// </summary>
    /// <remarks>
    /// One record read for each other quest named, only for a close about to be sent; no git, since the evaluation is handed no git reads. Its
    /// revision is current at the send: each is computed from the same read as this quest's, and every input of it (its chain's quests here
    /// and the branches they worked) is an input of this quest's revision, which the fence reads again below.
    /// </remarks>
    private string? Unreached(GovernedUnit governed, NodeDeclaration close, IReadOnlyList<QuestCloseItem> named)
    {
        foreach (var each in named)
        {
            var unit = QuestUnits.IdOf(each.Id);
            if (string.Equals(unit, governed.Unit.Id, StringComparison.Ordinal)) continue;
            string Waits(string why) => $"#{each.Id}, which it would close too: {why}";
            if (_governed?.GetValueOrDefault(unit) is not { Closes: true } own || !own.Declared.ContainsKey(close.Id))
            {
                return Waits($"it is not governed here by the graph it is bound to, so its `{close.Id}` is not judged");
            }

            if (records.Read(unit) is not { } record) return Waits($"its node record does not read, so its `{close.Id}` is not judged");
            var (_, evaluated) = Evaluate(own, record);
            if (evaluated.Refusal is { } refusal) return Waits($"its graph was not judged ({refusal}), so its `{close.Id}` is not judged");
            var into = own.Graph.Wires.Where(wire => wire.To is { Unit: null } to && to.Node == close.Id && wire.From is { Unit: null }).ToList();
            if (evaluated.Of(close.Id) is not { Eligible: true })
            {
                var waiting = evaluated.Of(close.Id)?.Waiting ?? [];
                var source = into.FirstOrDefault(wire => waiting.Contains(wire.Id, StringComparer.Ordinal))?.From!.Node;
                return Waits(source is null
                    ? $"its `{close.Id}` is not reached in its own graph yet"
                    : $"its `{source}`, before its `{close.Id}` in its own graph, {Standing(record, evaluated.Statuses[source])}");
            }

            var moved = evaluated.Moves.Select(move => move.Node).ToHashSet(StringComparer.Ordinal);
            if (into.Select(wire => wire.From!.Node).FirstOrDefault(moved.Contains) is { } unkept)
            {
                return Waits($"its `{unkept}`, before its `{close.Id}` in its own graph, has moved and its record does not hold that yet");
            }
        }

        return null;

        // Where a prerequisite stands, said as the person would ask it: held, unknown, failed, or not reached yet.
        static string Standing(NodeRecord record, NodeStatus status) =>
            record.Attempts.Any(attempt => attempt.Node == status.Node && attempt.Attempt == status.Attempt && attempt.State == AttemptStates.Held)
                ? "is held for the person"
                : status.State switch
                {
                    NodeStates.Unknown => "is unknown",
                    NodeStates.Failed => "failed",
                    NodeStates.Rejected => "was rejected",
                    NodeStates.Stopped => "was stopped",
                    NodeStates.Invalidated => "was reached on work that has moved since",
                    _ => "is not reached yet",
                };
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

    /// <summary>How long a graph kept stays before the pass may drop it unbound: the take binds it within seconds of its keeping.</summary>
    private static readonly TimeSpan KeptGrace = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Drops the closure graphs nothing needs (FABRIC-CLOSE1m, §E5): one a quest bound to it that is not Done reads needs, and one whose
    /// governed unit still has a close attempt unsettled or a live close node, or a record that does not read, so the runtime disarms once
    /// the last bound quest is Done and settled. Called holding <see cref="_one"/>, after the pass's moves are kept.
    /// </summary>
    private void Prune()
    {
        if (_closer is null || _governed is null || !ClosureGraphs.Any(records.Home)) return;
        var (all, unreadable) = records.All();
        if (unreadable.Count > 0) return;
        var needed = _quests.Values.Where(quest => quest.Closure is { Length: > 0 } && quest.Status != "Done").Select(quest => quest.Closure!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var each in _governed.Values.Where(each => each.Closes))
        {
            if (all.FirstOrDefault(record => record.Unit == each.Unit.Id) is not { } record) continue;
            var closes = each.Graph.Nodes.Where(node => node.Kind == NodeKinds.QuestClose).Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
            if (record.Attempts.Any(attempt => attempt.State != AttemptStates.Settled)
                || record.Statuses.Any(status => closes.Contains(status.Key) && NodeStates.Live.Contains(status.Value.State)))
            {
                needed.Add(each.GraphDigest);
            }
        }

        ClosureGraphs.Prune(records.Home, needed.Contains, clock(), KeptGrace);
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
        // FABRIC-CLOSE1q: a close of the submission the quest stands on now that closed it, still waits for its answer, or is held with no
        // refusal kept (its door may have closed it) is the door's, never the person's; one kept for an earlier submission speaks for none.
        var naming = QuestClosing.Naming(all, quest).ToList();
        if (naming.Any(attempt => Closed(attempt) || attempt.State is AttemptStates.Recorded or AttemptStates.Unknown or AttemptStates.Sent
                                  || attempt is { State: AttemptStates.Held, Outcome: not WorkStates.Failed }))
        {
            return;
        }

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
    /// Whether a unit's observed plugin nodes bind to its branch's tip read in the pass (FABRIC-PERFORM1m): a branch unit's. A quest's revision
    /// is null by design, and a performed node's is the unit's, as the plugins are told it (<see cref="GovernedUnit.Watched"/>).
    /// </summary>
    private static bool Tipped(GovernedUnit governed) => governed.Unit.Kind == WorkUnitKind.Branch && governed.Observable.Count > 0;

    /// <summary>
    /// Whether a branch unit's observed node awaits its event on a revision its branch's tip, read this pass, has moved off (FABRIC-PERFORM1m):
    /// judged at this pass, a wake as well as a look, so it rebinds before the event at the new tip comes. The checkout is read only where such
    /// a node is active; an unread tip rebinds nothing.
    /// </summary>
    private async Task<bool> RebindsAsync(GovernedUnit governed, PassTips tips, CancellationToken ct)
    {
        if (!Tipped(governed) || records.Read(governed.Unit.Id) is not { } record) return false;
        var awaiting = governed.Observable.Select(node => record.Statuses.GetValueOrDefault(node)?.Binding is { } binding
                && record.Statuses[node].State == NodeStates.Active ? binding.SubjectRevision : null)
            .OfType<string>().ToList();
        if (awaiting.Count == 0) return false;
        var tip = await tips.TipAsync(governed.Watched.Root, governed.Watched.Branch, ct).ConfigureAwait(false);
        return tip is not null && awaiting.Any(revision => !string.Equals(revision, tip, StringComparison.Ordinal));
    }

    /// <summary>
    /// The events the pass's hearing held because their node lagged the tip read this pass (FABRIC-PERFORM1o), heard again after the
    /// evaluations, the five rules and all, on the pass's one read of each checkout, so no git is started for them. A node the evaluation
    /// rebound takes its event, and its unit is evaluated once more so the wires it satisfies fire in this pass; an event whose node was not
    /// rebound (its unit past the pass's turn) is refused <c>stale</c> and counted, once, as it was before.
    /// </summary>
    private async Task HearAgainAsync(
        List<WorkLagging> lagging, IReadOnlyDictionary<string, GovernedUnit> governed, IReadOnlyList<GovernedUnit> concerned, WorkSends sends,
        List<WorkPerformed> done, List<string> said, Func<CancellationToken, Task<NodeUnitsRead>> units, PassTips tips, PassReads git, CancellationToken ct)
    {
        if (lagging.Count == 0) return;
        var again = await _observer.HearAsync([.. lagging.Select(each => each.Heard)], governed, tips.ReadAsync, ct).ConfigureAwait(false);
        done.AddRange(again);
        var taken = again.Where(each => each.What == PerformedWhat.Applied).Select(each => each.Unit).ToHashSet(StringComparer.Ordinal);
        foreach (var each in concerned.Where(each => taken.Contains(each.Unit.Id)))
        {
            await EvaluateAsync(each, sends, done, said, units, tips, git, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The revision each of a branch unit's observed plugin nodes is judged on (FABRIC-PERFORM1m): its branch's tip read this pass, so a moved
    /// branch lapses the old binding and the node is begun again on the new tip. Where the tip was not read, a bound node keeps its binding's
    /// revision, so nothing lapses on a read that failed; a node not bound is not begun (<see cref="EvaluateAsync"/>).
    /// </summary>
    private static Dictionary<string, string?> ObservedRevisions(GovernedUnit governed, string? tip, IReadOnlyDictionary<string, NodeStatus> statuses)
    {
        var revisions = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var node in governed.Observable)
        {
            if (tip is not null) revisions[node] = tip;
            else if (statuses.GetValueOrDefault(node)?.Binding is { } binding) revisions[node] = binding.SubjectRevision;
        }

        return revisions;
    }

    /// <summary>Each checkout's branches whose unit binds an observed node to its tip (FABRIC-PERFORM1m): what the pass's one read of it serves.</summary>
    private static Dictionary<string, IReadOnlyCollection<string>> Observing(IEnumerable<GovernedUnit> governed)
    {
        var branches = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        foreach (var same in governed.Where(Tipped).Where(each => each.Watched.Root.Length > 0).GroupBy(each => each.Watched.Root, StringComparer.Ordinal))
        {
            branches[same.Key] = same.Select(each => each.Watched.Branch).Where(branch => branch.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        }

        return branches;
    }

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

    /// <summary>
    /// Whether the runtime begins a built-in kind as an observed built-in (FABRIC-RUN1c): every built-in observed from records or settled by the
    /// evaluation, D157's <c>opinion</c> and <c>look</c> on their gates (WIRE1g, WIRE1h, NODE1b), a <c>stop</c> on its path and an <c>all</c>
    /// join on its members included; RUN1 began only those <see cref="BuiltInNodes"/> reads. A pressed node's task and a waited node's wait are
    /// HUMAN1's and WAITUNIT1's to start.
    /// </summary>
    private static bool Begins(string kind) => NodeAuthorities.BuiltIn.TryGetValue(kind, out var builtIn) && builtIn.Reach == NodeReach.Observed;

    private static bool Performs(NodeDeclaration node) => node.Reach == NodeReach.Performed && NodeKinds.IsPlugin(node.Kind);

    private static bool Observes(NodeDeclaration node) => node.Reach == NodeReach.Observed && NodeKinds.IsPlugin(node.Kind);

    /// <summary>Whether a graph names a plugin's performed or observed node: what arms the runtime and makes a unit governed (FABRIC-PERFORM1i).</summary>
    private static bool Acts(NodeGraph? graph) => graph?.Nodes.Any(node => Performs(node) || Observes(node)) == true;
}
