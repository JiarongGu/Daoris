namespace Daoris.Driver;

/// <summary>
/// The evaluation runtime in the lease's loop (the fabric events design §E2, §E3, §E7, §D6, §A3): link 8 of the loop's chain
/// (<see cref="ChainRanks.Nodes"/>), heard at every look and wake. Each pass drains the completions heard into <see cref="WorkPerformer.Hear"/>,
/// asks after every attempt due (<see cref="WorkPerformer.ReconcileAsync"/>) before any new start, makes ready the retries whose persisted
/// delay passed (<see cref="WorkPerformer.Due"/>), evaluates each concerned unit's graph to <see cref="WireEvaluated.Settled"/>, keeping its
/// moves (<see cref="WorkPerformer.Keep"/>) and starting what it proposes (a plugin's performed node through
/// <see cref="WorkPerformer.StartAsync"/>, an observed built-in through <see cref="WorkPerformer.Begin"/>), and tells the plugins the
/// awaited set (<see cref="IWorkRuntimePort.Watch"/>, <see cref="NodeTelling"/>).
/// </summary>
/// <remarks>
/// <para><b>Off unless a graph naming a plugin's performed node governs something</b> (the owner's ruling), or its observed node: with no such
/// graph in the choices, a pass reads no unit, no plugin step and no record, tells and takes nothing, writes nothing and starts no process or
/// git. Which units a graph governs is <see cref="NodeGoverning"/>'s, how a graph is judged <see cref="NodeEvaluations"/>'s.</para>
/// <para><b>Observed nodes</b>: begun as a built-in is, published once active, and reached by a believed event through
/// <see cref="WorkObserver"/> before the evaluation, whose tips are read fresh (<see cref="NodeRuntimeLook.Tips"/>). A branch unit's is bound
/// to its branch's tip read in the pass, not the unit's lagging revision, and rebinds when the branch moves (<see cref="PassTips"/>: one read
/// of each checkout a pass); an event at that tip heard before the rebinding is heard again after it, in the same pass
/// (<see cref="WorkLagging"/>). Such a unit's performed nodes bind to that same tip, its plugins are told it, and an answer lapses on it, so a
/// performed node wired after an observed one starts once, on the tip it was reached on.</para>
/// <para><b>One pass at a time, one performer per loop:</b> a wake that comes while a look's pass is out waits for it, so a start is never
/// judged twice on one record; a second loop or a verb holds no lease, and every record edit asks it under the record's lock.</para>
/// <para><b>A wake runs only what it concerns:</b> the units its answers, reconciliations and retries named, and those owing a start; the
/// look's deadline (<see cref="NodeRuntimeLook.Whole"/>) reads the units afresh and judges every governed one.</para>
/// <para><b>Closure graphs</b> (<see cref="NodeClosing"/>): with a close door (<paramref name="closes"/>), a graph holding <c>quest.close</c>
/// arms it too, and so does a closure graph this home keeps (<see cref="ClosureGraphs"/>), so a close in flight is reconciled and a bound
/// quest closed whatever the choices say since. A quest is bound at its take, in the take's own write (<see cref="ClosureBinding.AtStartAsync"/>),
/// never here; a bound quest is closed by the graph it was bound to.</para>
/// <para><b>Runs bound to graph versions</b>: such a run arms it too (<see cref="GraphRuns"/>), and is governed on its own record,
/// <c>run:&lt;run&gt;</c>, each node observing the subject its kind names (<see cref="RunScope"/>), while Current's graphs govern none of its
/// quests and branches (<see cref="RunOwners"/>). Its opinion and look are begun, their gates read on its branch at the tip the pass read, and
/// its performed nodes started, since its landing doors read its record; its end is kept there (<see cref="NodeRunKeeping"/>), and its chain
/// coming back here arms it again (<see cref="Return"/>).</para>
/// </remarks>
public sealed class NodeRuntime(
    NodeRecordFiles records, IWorkRuntimePort port, Func<DateTimeOffset> clock, Func<bool>? holdsLease = null, IQuestClosePort? closes = null)
{
    /// <summary>The units one pass evaluates at most, in id order; the rest are judged at the next.</summary>
    public const int UnitsPerPass = 32;

    /// <summary>The attempts one pass sends at most, across its units: four process starts at once (§A3).</summary>
    public const int SendsPerPass = WireEvaluation.DefaultLimit;

    /// <summary>The loop's one performer: its delivered invocations live as long as the loop.</summary>
    private readonly WorkPerformer _performer = new(records, port, clock) { Receipts = new ResourceRecordFiles(records.Home, records.HoldsLease) };

    /// <summary>The loop's one cleaner (§D3): what plugins created, asked of them at a true end or a superseded attempt.</summary>
    private readonly ResourceCleaner _cleaner = new(new ResourceRecordFiles(records.Home, records.HoldsLease), port, clock);

    /// <summary>Whether each unit's end was over at the last read of the units: what a cleanup waits for. Absent is not read.</summary>
    private Dictionary<string, bool> _ends = new(StringComparer.Ordinal);

    private bool? Ended(string unit) => _ends.TryGetValue(unit, out var over) ? over : null;

    /// <summary>The loop's one observer: an observed plugin node reached by its believed event.</summary>
    private readonly WorkObserver _observer = new(records, port, clock);

    /// <summary>Daoris's own close, where the loop holds the close door; none, and no quest is closed by a graph.</summary>
    private readonly QuestCloser? _closer = closes is null ? null : new QuestCloser(records, closes, clock);

    private readonly Func<bool> _holdsLease = holdsLease ?? (() => true);

    private readonly SemaphoreSlim _one = new(1, 1);

    /// <summary>The home's runs bound to graph versions (the cutover design §3): what arms the runtime beside the choices.</summary>
    private readonly GraphRuns _graphRuns = new(records);

    private NodeTelling? _telling;

    private NodeClosing? _closing;

    private NodeRunKeeping? _keeping;

    private NodeIntegrating? _integrating;

    private NodeOwing? _owing;

    private NodeOwing Owing => LazyInitializer.EnsureInitialized(ref _owing, () => new NodeOwing(records, () => Doors.Door<IIntegrationDoor>()))!;

    private NodeTelling Telling => LazyInitializer.EnsureInitialized(ref _telling, () => new NodeTelling(records, port, _holdsLease, _cleaner, Ended))!;

    private NodeClosing Closing => LazyInitializer.EnsureInitialized(ref _closing, () => new NodeClosing(records, clock, _closer, () => _quests, () => _governed, () => _governed = null))!;

    private NodeRunKeeping Keeping => LazyInitializer.EnsureInitialized(ref _keeping, () => new NodeRunKeeping(
        records, clock, _holdsLease, _graphRuns, _performer, () => Doors, () => Say, () => PullRequests))!;

    private NodeIntegrating Integrating => LazyInitializer.EnsureInitialized(ref _integrating, () => new NodeIntegrating(
        records, clock, () => Doors.Door<IIntegrationDoor>(), () => Lines, () => _config, () => Integrated))!;

    private NodeRequesting? _requesting;

    private NodeRequesting Requesting => LazyInitializer.EnsureInitialized(ref _requesting, () => new NodeRequesting(
        records, clock, () => Doors.Door<IRequestPort>(), () => _quests))!;

    /// <summary>
    /// The doors its nodes are started on, by kind (D247 §3.5): none by default, so a runtime nobody wired starts nothing on a door and each such
    /// node waits. The watch wires them (<see cref="NodeDoors"/>); they are its performer's, which sends a journalled kind's attempts to its port.
    /// </summary>
    public NodeDoors Doors
    {
        get => _performer.Doors;
        set => _performer.Doors = value;
    }

    /// <summary>The transport an open request's line is sent to: none by default, and an admitted request then waits queued. The watch sets it.</summary>
    public IIntegrationLines? Lines { get; set; }

    /// <summary>A unit whose <c>line</c> was reached after its <c>integrate</c> (<c>work.integrated</c>, §5.1): told once, with its repository.</summary>
    public Action<string, string>? Integrated { get; set; }

    /// <summary>Where the pull request a plugin's step opened is written on its landing (G8): none by default, so a runtime nobody wired writes none.</summary>
    public IPullRequestRecords? PullRequests { get; set; }

    /// <summary>
    /// The say door a run's work is told what was sent back to it through: Daoris's words on the work's session as its next turn, under their
    /// key, so a say again is answered with the word kept. Handed the session, the words and the key. None by default: nobody is told, and
    /// the pass says so.
    /// </summary>
    public Func<string, string, string, CancellationToken, Task<SayAnswer>>? Say { get; set; }

    /// <summary>The choices the pass in hand reads, the door's rules under them: set by a pass holding <see cref="_one"/>.</summary>
    private DriverConfig? _config;

    /// <summary>Every quest the last look read, by id: a close's chain, and what its quests now read.</summary>
    private Dictionary<string, QuestView> _quests = new(StringComparer.Ordinal);

    /// <summary>
    /// The quests the runtime last read, for the doors to judge a held close against: read-only, never written by a door; null before the
    /// first read of a start, where a door judges nothing.
    /// </summary>
    public IReadOnlyDictionary<string, QuestView>? LastQuests { get; private set; }

    /// <summary>
    /// The runs held at a declined request, judged from the quests the runtime last read and each run's node record read now, so a pass over a
    /// node clears its hold before the next look. Empty before a pass has read.
    /// </summary>
    public IReadOnlyList<RequestHold> RequestHolds() =>
        _governed is { } governed ? Daoris.Driver.RequestHolds.Of(governed.Values, records.Read, _quests) : [];

    /// <summary>The runs waiting on a person's task, from the runs the runtime last governed and each run's node record read now. Empty before a pass.</summary>
    public IReadOnlyList<HumanTask> HumanTasks() => _governed is { } governed ? Daoris.Driver.HumanTasks.Of(governed.Values, records.Read) : [];

    /// <summary>
    /// The landed branches whose <c>integrate</c> waits for the owner's card (§17.4), each as its unit's last judgement found it, oldest
    /// landing first; a unit the last look no longer governs is none. Empty before a pass has judged one.
    /// </summary>
    public IReadOnlyList<IntegrationCardWait> CardWaits() => _governed is { } governed ? Integrating.CardWaits(governed) : [];

    /// <summary>Whether this process has not judged its bound runs yet: its first look judges every one once, an ended one included (§3).</summary>
    private volatile bool _restarted = true;

    /// <summary>The ended runs a start of their chain asked a pass for (<see cref="Return"/>), armed until a fresh read judges them.</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _returned = new(StringComparer.Ordinal);

    /// <summary>Whether a start asked a pass that reads the units afresh: a run it re-armed is judged on what is there now.</summary>
    private volatile bool _freshAsked;

    /// <summary>
    /// The governed units by each unit their plugins are told under (the cutover design §2.4.1): a unit's own id, and every branch a run's
    /// quests produced, mapped back to the run. What an event's told unit is looked up in.
    /// </summary>
    private Dictionary<string, GovernedUnit> _byTold = new(StringComparer.Ordinal);

    /// <summary>The binding files this process read: a look over unchanged files reads none again.</summary>
    internal int BindingsRead => _graphRuns.Reads;

    /// <summary>
    /// A start found its run bound already (<see cref="WorkflowBindCame.Already"/>; the cutover design §3): where the run is bound to a graph
    /// version and its record holds its end, its chain has come back here, so the runtime is armed for it until a pass reads the units afresh
    /// and judges it. Returns whether it was, so the caller wakes the runtime; a run not ended, or not a graph run, asks nothing.
    /// </summary>
    public bool Return(string run)
    {
        if (_graphRuns.Find(run) is not { Ended: not null, Version.Readable: true }) return false;
        _returned[run] = 0;
        _freshAsked = true;
        return true;
    }

    /// <summary>
    /// Whether a run bound to a graph version arms the runtime (the cutover design §3): one whose record does not hold its end, one a start of
    /// its chain asked a pass for, and every one at this process's first look. Lists the runs folder; a home with none costs one stat. A run
    /// whose version no longer reads arms nothing: it is held whole, and said whenever a pass reads the units.
    /// </summary>
    private bool ArmsRuns =>
        _graphRuns.Read().Any(run => run.Version.Readable && (run.Ended is null || _restarted || _returned.ContainsKey(run.Run))
            // A conversation's run is owed a pass only once a press submitted a candidate, and again at the next one.
            && (!ChatRuns.IsChatRun(run.Run) || (run.Candidate?.Newest is { } candidate
                && !string.Equals(candidate.IdIn(run.Run), run.Ended?.Quest, StringComparison.Ordinal)))
            || run.Version.Readable && run.Candidate?.Newest is { } next && !string.Equals(next.IdIn(run.Run), run.Ended?.Quest, StringComparison.Ordinal));

    /// <summary>The governed units the last look read, by id: what a wake judges with.</summary>
    private Dictionary<string, GovernedUnit>? _governed;

    /// <summary>
    /// The choices <see cref="_governed"/> was judged under, as read: a wake under other choices reads the units afresh before any attempt is
    /// heard, asked after, retried or started, so a unit whose graph was removed or edited is never judged under the old one.
    /// </summary>
    private string? _choices;

    /// <summary>Whether this pass read the units afresh: the governed revisions are then as current as the pass; set under <see cref="_one"/>.</summary>
    private bool _readThisPass;

    /// <summary>When the next timer the records persist comes due (a retry's delay, a running answer's wait, a deadline), or null.</summary>
    public DateTimeOffset? NextDue => Telling.NextDue;

    /// <inheritdoc cref="NodeGoverning.Govern"/>
    public static IReadOnlyList<GovernedUnit> Govern(
        DriverConfig config, NodeUnitsRead read, IReadOnlyList<NodeDeclaration> steps, Func<string, NodeGraph?>? kept = null,
        Func<WorkUnit, IReadOnlyCollection<QuestView>, DecodedVersion?>? versionOf = null, IReadOnlyList<GraphRun>? runs = null,
        ICollection<string>? said = null) => NodeGoverning.Govern(config, read, steps, kept, versionOf, runs, said);

    /// <inheritdoc cref="NodeGoverning.BoundIn"/>
    public static Func<WorkUnit, IReadOnlyCollection<QuestView>, DecodedVersion?> BoundIn(string home) => NodeGoverning.BoundIn(home);

    /// <inheritdoc cref="NodeGoverning.Bindings"/>
    public static IReadOnlyList<string> Bindings(DriverConfig config, NodeUnitsRead read, IReadOnlyDictionary<string, GovernedUnit> governed) =>
        NodeGoverning.Bindings(config, read, governed);

    /// <summary>The door over this home's records and integration store, built from the home the node records live in.</summary>
    public static IIntegrationDoor HomeDoor(string home) => new HomeIntegrationDoor(home);

    /// <summary>
    /// Withdraws what the runtime holds once the choices no longer arm it: the awaited set told nothing, the timers and the governed units
    /// cleared. Called before the plan's supervision, so a plugin the last graph awaited is not started again by the look that reads that
    /// graph's removal. Returns whether the choices disarm the runtime.
    /// </summary>
    public async Task<bool> WithdrawAsync(DriverConfig config, CancellationToken ct)
    {
        if (KeepsClosures || ArmsRuns || OwesCleanup) return false;
        if (!Telling.Holding && _governed is null) return true;
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
    /// Whether a plugin is owed a cleanup of what a run created: an ended run's temporary resources stay owed until their plugin settles them,
    /// so the runtime stays armed for the ask though no run is open.
    /// </summary>
    private bool OwesCleanup
    {
        get
        {
            var ended = _graphRuns.Read().Where(run => run.Ended is not null).Select(run => run.Unit).ToHashSet(StringComparer.Ordinal);
            return _cleaner.Owed(unit => ended.Contains(unit) ? true : Ended(unit)).Count > 0;
        }
    }

    /// <summary>
    /// Whether this loop closes quests and its home keeps a closure graph a quest was bound to: it stays armed whatever the choices say, so a
    /// close in flight is reconciled and a bound quest closed by its graph. One stat; a home that never bound a quest keeps none, and is
    /// judged by its choices alone.
    /// </summary>
    private bool KeepsClosures => _closer is not null && ClosureGraphs.Any(records.Home);

    /// <summary>
    /// Disarmed: the plugins are told nothing is awaited where something was, so their processes stop, and everything cached is cleared
    /// whatever was told, a pending retry's timer included, so nothing wakes the loop and a later arming reads the units afresh. Nothing is
    /// read. Called holding <see cref="_one"/>.
    /// </summary>
    private void Withdraw()
    {
        Telling.Withdraw();
        Owing.Forget();
        (_governed, _choices) = (null, null);
        _byTold = new Dictionary<string, GovernedUnit>(StringComparer.Ordinal);
        LastQuests = null;
        QuestReads.Delete(records.Home);
    }

    /// <summary>
    /// One pass, at a look (<see cref="NodeRuntimeLook.Whole"/>) or a wake: completions heard, attempts due asked after before any new start,
    /// retries due made ready, the concerned units evaluated and their starts made within <see cref="SendsPerPass"/>, the awaited set told.
    /// </summary>
    public async Task<NodeRuntimePass> PassAsync(NodeRuntimeLook look, CancellationToken ct)
    {
        // Disarmed: whatever was told or cached is withdrawn, a pending retry's timer included; nothing is read.
        if (await WithdrawAsync(look.Config, ct).ConfigureAwait(false)) return NodeRuntimePass.Idle;
        await _one.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_holdsLease()) return new NodeRuntimePass(true, [], [], []);
            _config = look.Config;

            // Governance is read afresh at the look's deadline, at the first pass, and at a wake under choices other than the ones it was judged
            // under, before any attempt is heard, asked after, retried or started; read afresh, every governed unit is judged. A word on a
            // close is judged on the quests as they stand, so a pass applying one reads them afresh; so does a start that found its ended
            // run's chain back here.
            var choices = look.Config.Source ?? look.Config.ToJson();
            var fresh = look.Whole || _governed is null || !string.Equals(choices, _choices, StringComparison.Ordinal) || _closer?.Worded() == true || _freshAsked;
            _readThisPass = fresh;
            var bindings = new List<string>();
            if (fresh)
            {
                _freshAsked = false;
                var read = await look.Units(ct).ConfigureAwait(false);
                var home = records.Home;
                var kept = _closer is null ? null : (Func<string, NodeGraph?>)(digest => ClosureGraphs.Read(home, digest));
                // The runs bound to graph versions, each owed a pass where a start of its chain asked one or this is the process's first look,
                // which judges every bound run once, so a restart loses nothing.
                var runs = _graphRuns.Read().Select(run => run with { Asked = _restarted || _returned.ContainsKey(run.Run) }).ToList();
                (_governed, _choices) = (NodeGoverning.Govern(look.Config, read, look.Steps(), kept, look.Bound, runs, bindings)
                    .ToDictionary(each => each.Unit.Id, StringComparer.Ordinal), choices);
                _restarted = false;
                foreach (var run in runs) _returned.TryRemove(run.Run, out _);
                _byTold = NodeEvaluations.ByTold(_governed.Values);
                bindings.AddRange(Owing.LeftBefore(_governed.Values));
                _quests = NodeGoverning.Quests(read);
                LastQuests = _quests;
                var ends = new Dictionary<string, bool>(StringComparer.Ordinal);
                foreach (var unit in read.Units) ends[unit.Id] = WorkEndState.Over(unit.End.State);
                // A run is its own unit: its true end is the end its record holds.
                foreach (var run in runs) ends[run.Unit] = run.Ended is not null;
                _ends = ends;
                QuestReads.Keep(home, _quests); // The terminal's `work held` judges on it; the lease is held here.
                if (_closer is not null) bindings.AddRange(NodeGoverning.Bindings(look.Config, read, _governed));
            }

            var governed = _governed!;
            _performer.Resources = look.Resources;
            NodeDeclaration? Declared(string unit, string node) => governed.TryGetValue(unit, out var each) ? each.Declared.GetValueOrDefault(node) : null;

            // The pass's tips, each checkout read once by whatever asks of it first (the hearing, a rebinding's check, a binding), never kept
            // past the pass.
            var tips = new PassTips(look.Tips, NodeEvaluations.Observing(governed.Values));

            // The look's git reads for the observed built-ins, each checkout's remote-tracking refs read once a pass, and only where a node
            // waits on them; D157's gates likewise, once a pass for a unit whose opinion or look waits on them.
            var git = new PassReads(new PassGit(look.Remotes,NodeEvaluations.Pushing(governed.Values)), new PassGates(look.Gates), NodeEvaluations.Once(look.Content));

            // One count of calls for the whole pass, reconciliation's first, so four is the pass's bound and not its starts' alone.
            var sends = new WorkSends(SendsPerPass);
            var done = new List<WorkPerformed>();
            // A completion is believed only for a node its plugin still declares under the cycle governing its unit (§C4's second rule), on
            // that unit as the plugins are told it now (the third and fourth).
            NodeDeclaration? Performed(string unit, string node) =>
                governed.TryGetValue(unit, out var each) && each.Dispatchable.Contains(node) ? each.Declared[node] : null;
            var heard = port.TakeSteps();
            // A branch unit's answer is judged on the tip its pass reads, the one its performed nodes bind to and its plugins are told, so the
            // checkout of each unit answered is read first, once, for the pass.
            var answers = heard.Where(each => each.Step.Invocation is not null).ToList();
            foreach (var unit in _performer.UnitsOf([.. answers.Select(each => each.Step.Invocation!)]))
            {
                if (!governed.TryGetValue(unit, out var each)) continue;
                if (NodeEvaluations.Tipped(each)) await tips.TipAsync(each.Watched.Root, each.Watched.Branch, ct).ConfigureAwait(false);
                // A run's answer is judged on the branch its attempt was sent for, former subjects included, read once a checkout.
                else if (each.Scope is { } scope) await tips.ReadAsync(scope.Root, [.. scope.Branches.Select(branch => branch.Subject.Artifact ?? "")], ct).ConfigureAwait(false);
            }

            // A run's attempt is judged on the unit it was sent for, as its plugin was told it.
            done.AddRange(_performer.Hear(answers, Performed, (unit, attempt) =>
                !governed.TryGetValue(unit, out var each) ? null : each.Scope is null ? Telling.Told(each, tips) : Telling.RunTold(each, attempt, tips)));
            // An event naming no invocation is an observed node's, bound to the frame its process was sent and the tips read now, and written
            // before the evaluation below fires its wires. One at the tip read now whose node still awaits an older binding is held, and heard
            // again once the evaluation has rebound the node. A unit told for a run maps to it.
            var lagging = new List<WorkLagging>();
            done.AddRange(await _observer.HearAsync([.. heard.Where(each => each.Step.Invocation is null)], _byTold, tips.ReadAsync, ct, lagging).ConfigureAwait(false));
            // The person's word on a held attempt, applied under the declaration governing now, before any new attempt.
            done.AddRange(_performer.Resolve(Declared));
            // The person's word on a held close, a close's recovery, before the closer sends any close again below; judged on the quests this
            // pass read, so a word on a close for a submission reopened or replaced since never reaches the next one.
            if (_closer is not null) done.AddRange(_closer.Resolve(Declared, _quests));
            // The person's skip or stop of a run's node, kept on its record by their door, applied before anything on it is judged; a run it
            // names is judged this pass.
            var worded = await Keeping.WordedAsync(governed.Values, ct).ConfigureAwait(false);
            done.AddRange(worded.Done);
            done.AddRange(await _performer.ReconcileAsync(Declared, sends, ct).ConfigureAwait(false));
            done.AddRange(_performer.Due(Declared));
            // What plugins created, asked of them at their unit's true end, or at once for a superseded attempt's, within the same count of
            // calls, before any new start.
            done.AddRange(await _cleaner.CleanAsync(Ended, sends, ct).ConfigureAwait(false));

            var said = new List<string>(bindings);
            said.AddRange(worded.Said);

            // A close whose answer was lost is reconciled before anything new starts, by asking its door what became of its key, never by
            // sending it again blind, so one never delivered is judged again on what is there now.
            if (_closer is not null)
            {
                done.AddRange(await _closer.ReconcileAsync(sends, ct).ConfigureAwait(false));
                said.AddRange(_closer.TakeSaid());
            }

            // A unit's request is asked after, by its key, before any new start (§17.4).
            if (Doors.Door<IIntegrationDoor>() is not null)
            {
                var (reconciled, line) = await Integrating.ReconcileIntegrationsAsync(ct).ConfigureAwait(false);
                done.AddRange(reconciled);
                said.AddRange(line);
            }

            // A run's request is read on this look's projection of its quest before any new start, its key found first (§17.5).
            if (Doors.Door<IRequestPort>() is not null)
            {
                var (reconciled, line) = await Requesting.ReconcileAsync(governed.Values, ct).ConfigureAwait(false);
                done.AddRange(reconciled);
                said.AddRange(line);
            }

            var named = done.Select(each => each.Unit).Concat(lagging.Select(each => each.Unit)).ToHashSet(StringComparer.Ordinal);
            var owing = new List<GovernedUnit>();
            foreach (var each in governed.Values)
            {
                if (fresh || named.Contains(each.Unit.Id) || Owing.OwesStart(each) || await Owing.RebindsAsync(each, tips, ct).ConfigureAwait(false)) owing.Add(each);
            }

            // A unit whose event was held is judged first, so a turn past UnitsPerPass never leaves its node unrebound.
            var concerned = Owing.Turn(owing, lagging.Select(each => each.Unit).ToHashSet(StringComparer.Ordinal));
            foreach (var each in concerned)
            {
                if (each.Closes) Closing.Settle(each, done, said);
                await EvaluateAsync(each, sends, done, said, look.Units, tips, git, ct).ConfigureAwait(false);
            }

            await HearAgainAsync(lagging, _byTold, concerned, sends, done, said, look.Units, tips, git, ct).ConfigureAwait(false);
            if (fresh || done.Count > 0) Closing.Prune();
            var changed = Telling.Tell(governed.Values, tips, said);
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
        Integrating.Forget(unit);
        // A branch unit's plugin nodes, observed and performed, bind to its branch's tip read this pass, not to the unit's revision, which is
        // the end fact's and lags a moved branch until the end judgement catches up; the pass's one read of the checkout, null where unread.
        var tipped = NodeEvaluations.Tipped(governed);
        var tip = tipped ? await tips.TipAsync(governed.Watched.Root, governed.Watched.Branch, ct).ConfigureAwait(false) : null;

        // A run's branch-side nodes bind its branch's tip read in the pass, and its gates are read there; an end its record holds from before
        // its chain came back here lapses, and the run goes on (§3).
        if (governed.Scope is { } scope)
        {
            tip = scope.Branch is { } branch ? await tips.TipAsync(scope.Root, branch.Subject.Artifact ?? "", ct).ConfigureAwait(false) : null;
            Keeping.Reopen(governed, scope);
        }

        for (var round = 0; round <= governed.Graph.Nodes.Count; round++)
        {
            if (records.Read(unit) is not { } record)
            {
                said.Add($"work  {unit}: its node record does not read, so nothing on it is judged or started until a person mends it.");
                break;
            }

            // An attempt a lapse left unreconciled is settled by its kind before anything new starts on it.
            foreach (var starts in governed.Graph.Nodes.Select(node => NodeKit.Of(node.Kind)).OfType<IStarts>().Distinct())
            {
                record = await starts.SettleAsync(records, governed, record, ct).ConfigureAwait(false);
            }

            // What git says for the built-ins this round's record holds waiting on it, read once a pass; the gates its opinion and look wait
            // on, likewise.
            var reads = await NodeEvaluations.ContentAsync(governed, record, tip, git.Content,
                    await NodeEvaluations.GitReadsAsync(governed, record, git.Git, ct).ConfigureAwait(false), ct)
                .ConfigureAwait(false);
            var gates = await NodeEvaluations.GatesAsync(governed, record, tip, git.Gates, ct).ConfigureAwait(false);
            var (before, evaluated) = governed.Scope is null
                ? NodeEvaluations.Evaluate(governed, record, tipped, tip, reads, gates)
                : NodeEvaluations.EvaluateRun(governed, record, tip, reads, gates);
            if (evaluated.Refusal is { } refusal)
            {
                said.Add($"work  {unit}: its graph was not judged ({refusal}: {string.Join(", ", evaluated.Missing)}), so nothing on it is started.");
                break;
            }

            if (!evaluated.Settled)
            {
                done.AddRange(_performer.Keep(before, evaluated));
                Integrating.IntegratedNow(governed, before, evaluated.Statuses, said);
                await Keeping.BackToWorkAsync(governed, before, evaluated, said, ct).ConfigureAwait(false);
            }

            // Each node whose wires fired starts as its kind says (D247). A start that dropped what this pass governs (a fence that found the
            // unit moved) ends its judgement here: the next pass reads it again, and no further round reads git only to find the same revision.
            var started = false;
            foreach (var start in evaluated.Starts)
            {
                var starting = new NodeStarting
                {
                    Governed = governed, Node = governed.Declared[start.Node], Start = start, Statuses = evaluated.Statuses, Tipped = tipped, Tip = tip,
                    ReadThisPass = _readThisPass, Sends = sends, Said = said, Units = units, Performer = _performer, Port = port, Closing = Closing,
                    Integrating = Integrating, Requesting = Requesting, Drop = () => _governed = null,
                };
                if (await starting.StartAsync(ct).ConfigureAwait(false) is { } performed)
                {
                    done.Add(performed);
                    started |= performed.What != PerformedWhat.Refused;
                }

                if (_governed is null) return;
            }

            if (evaluated.Settled && !started) break;
        }

        if (governed.Scope is { } run)
        {
            await Keeping.AcceptRepairsAsync(governed, run, ct).ConfigureAwait(false);
            Keeping.StartDoors(governed, run);
            Keeping.KeepPullRequests(governed);
            Keeping.End(governed, run, said);
        }
    }

    /// <summary>
    /// The events the pass's hearing held because their node lagged the tip read this pass, heard again after the evaluations, the five rules
    /// and all, on the pass's one read of each checkout, so no git is started for them. A node the evaluation rebound takes its event, and its
    /// unit is evaluated once more so the wires it satisfies fire in this pass; an event whose node was not rebound is refused <c>stale</c>
    /// and counted, once, as it was before. Its unit is judged first in the turn, so that is left to a node its evaluation did not rebind, or
    /// to more held units than a turn holds.
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
}
