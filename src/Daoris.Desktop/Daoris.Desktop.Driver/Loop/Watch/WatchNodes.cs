namespace Daoris.Driver;

/// <summary>
/// Link 8 of a watch's run: the evaluation runtime, begun after the plan on every wake it hears and at the look's
/// deadline and run beside the look, with the doors its units act through: an <c>integrate</c>'s door, a request's publish, a review's
/// word, a <c>land</c>, a check's executor and a line's transport. Only where the loop holds a hook set; its passes write only where it
/// holds the home's lease.
/// </summary>
/// <remarks>
/// <para><b>Its state.</b> Two asks kept for the next pass: <see cref="_wholeAsked"/>, a whole look's, and <see cref="_superviseAsked"/>,
/// the awaited set changed. Link 8's binding sets each on the chain's thread; the pass beside the look takes each with one exchange. The
/// doors' whole-pass asks (<see cref="PassAsks"/>) are taken once per pass. The pass reads the run's latest choices through
/// <see cref="_config"/>, a reference the loop swaps whole.</para>
///
/// <para><b>What a race would break.</b> Taken by exchange, an ask set while a pass runs is kept for the pass after it, never cleared
/// unread, so a pass that follows reads the units afresh for it; read and cleared apart, it would be lost. The running set begins one
/// pass at a time and one more after it where asked (<see cref="RunningSessions.PassBeside"/>), so two passes never take the same ask. A
/// pass held by a drain answers the doors' asks it took not passed and leaves both flags set for the pass after the drain.</para>
/// </remarks>
internal sealed class WatchNodes
{
    /// <summary>The runtime's wake whose pass first runs the plugins' supervision: the awaited set it told changed.</summary>
    internal const string Awaited = "awaited";

    /// <summary>What link 8 counts as acting: an attempt sent or delivered again, an outcome applied, a retry made ready, a start.</summary>
    private static readonly HashSet<string> Acted =
        new([PerformedWhat.Sent, PerformedWhat.Redelivered, PerformedWhat.Applied, PerformedWhat.Retry, PerformedWhat.Begun], StringComparer.Ordinal);

    private readonly DriverWatch _watch;
    private readonly HookSet _hooks;
    private readonly Func<DriverConfig> _config;
    private readonly HeavyExecutor _executor;
    private readonly IntegrationTransport _transport;
    private readonly OpenHandOffs _handOffs;
    private readonly AskRunBinder _askRuns;
    private PassAsks? _asks;
    private System.Threading.Timer? _due;
    private int _wholeAsked;
    private int _superviseAsked;

    /// <summary>Link 8 for a run of <paramref name="watch"/>, or null where it holds no hook set, and the runtime reads nothing.</summary>
    internal static WatchNodes? Of(DriverWatch watch, Func<DriverConfig> config) =>
        watch.Hooks is { } hooks ? new WatchNodes(watch, hooks, config) : null;

    private WatchNodes(DriverWatch watch, HookSet hooks, Func<DriverConfig> config)
    {
        _watch = watch;
        _hooks = hooks;
        _config = config;
        var home = watch.Home;
        var service = watch.Service;
        _askRuns = new AskRunBinder(home, WorkflowPorts.Store(home), WorkflowPorts.Runs(home), new ServiceTrainPlans(service));

        // A closure graph arms it too, and its `quest.close` is sent through this loop's service client, the local host's close
        // door. A quest is bound at its take, as its first session opens (Driver's start), never here.
        var nodes = Runtime = new NodeRuntime(new NodeRecordFiles(home), hooks, () => DateTimeOffset.UtcNow, () => HomeLease.HeldHere(home), service);
        // What a run's review sends back is said on its work's session as Daoris's words, under their key, at its line's head.
        nodes.Say = async (session, text, key, ct) =>
            (await WordsHead.FollowAsync(session, at => service.SayAsync(at, text, null, ct, from: DaorisWords.From, key: key)).ConfigureAwait(false)).Said;

        // Where the pull request a plugin's step opened is written on its landing.
        nodes.PullRequests = new PullRequestRecords(home);

        nodes.Landings = watch.Supervisor.LandingWords;
        watch.Supervisor.LandingApplied = LandingApplied.Of(home);

        // The heavy executor a check is handed to; its answer asks a pass, which reconciles it.
        _executor = new HeavyExecutor(home, new VerifyWorld
        {
            Answered = (run, _) => watch.NodesWoken(RunUnits.IdOf(run)),
            Mapping = repository => HeavyMapping.For(DriverConfig.Load(DriverConfig.ResolvePath(home)), repository),
        });

        // Each starting kind builds its door from this loop's wiring, starting only where the loop holds the home's lease and never
        // while an update drains. A run's `land` lands as the review's Accept lands it; a unit's `integrate`
        // asks the door a test hands, else the home's own; a run's `request` publishes through this loop's service client.
        var landingPlugins = new LandingPlugins(home, say: (plugin, line) => watch.Output?.Append($"plugin:{plugin}", line), pluginLog: hooks.Log);
        // A run's `resolve` asks the tree's session through the say door and stops its turn through the session's stop door.
        var resolvers = new SessionResolvers(new ResolverSessions(
            service.RecordsAsync,
            (session, text, key, token) => service.SayAsync(session, text, null, token, from: DaorisWords.From, key: key),
            session => watch.Processes.Running.Contains(session, StringComparer.OrdinalIgnoreCase),
            (session, note) => watch.Processes.Stop(session, reason: note)));
        _handOffs = new OpenHandOffs(home, resolvers, said => Log?.Info("resolve.run", ("said", said)));
        nodes.Doors = NodeDoors.Wired(new NodeDoorWiring(home, () => HomeLease.HeldHere(home) && !watch.DrainsNow())
        {
            Log = (code, said) => Log?.Info(code, ("said", said)),
            Landing = () => new LandPerformer(home, new ServiceReviewLandingWorld(home, service), new SessionTrees(home, landingPlugins),
                watch.Events ?? new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions)), service.LandingSaid) { Told = hooks.Tell },
            Executor = _executor,
            Integration = watch.IntegrationDoor,
            Requests = service,
            Turning = ct => TidyHolders.InUseAsync(home, service, null, ct),
            Resolvers = resolvers,
            Reserve = (row, ct) => TreeReserving.ReserveAsync(home, service, new TreeHandOffs(home), row, ct),
            // A plugin step declaring a goal is performed by a session of the run's own, started off the pass by a driver built as a look's is.
            StepSessions = new StepSessionTurn(() => new Driver(
                service, config(), watch.Adapters, home, watch.Processes, watch.Sync, watch.Output, watch.Harnesses, watch.Usage, hooks, watch.Events,
                watch.Browser, watch.Running) { Log = Log }),
            StepAsks = (unit, attempt, node, ct) => Runtime.StepAskAsync(unit, attempt, node, ct),
            Wake = () => watch.NodesWoken("step"),
        });

        // The transport an open request's line is sent to, run off the pass beside the heavy
        // executor; a member's conflict told to its session through the say door, each step told to the log, and its answer asking a pass.
        _transport = new IntegrationTransport(home, new IntegrationTransportWorld
        {
            Config = () => DriverConfig.Load(DriverConfig.ResolvePath(home)),
            Plans = new ServiceTrainPlans(service),
            Root = async (repository, workspace, token) => (await RefWatch.RepositoriesAsync(home, service.RegistryAsync, token).ConfigureAwait(false))
                .FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)
                                        && string.Equals(each.Workspace, workspace, StringComparison.OrdinalIgnoreCase))?.Root,
            Say = async (session, text, key, token) =>
                (await WordsHead.FollowAsync(session, at => service.SayAsync(at, text, null, token, from: SaidFrom.Daoris, key: key)).ConfigureAwait(false)).Said,
            Tidy = IntegrationTidies.Through(new SessionTrees(home)),
            Beside = token => BranchTidying.BesideAsync(home, service, token),
            Told = told => Log?.Info(told.Code, ("repository", told.Repository), ("line", told.Line), ("candidate", told.Candidate), ("request", told.Request),
                ("outcome", told.Outcome)),
            Said = said => Log?.Info("integration.said", ("said", said)),
            Answered = () => watch.NodesWoken("integration"),
            // As the pass: only where this loop holds the home's lease, and never while an update drains.
            Runs = () => HomeLease.HeldHere(home) && !watch.DrainsNow(),
        });
        nodes.Lines = _transport;
        nodes.Plans = new ServiceTrainPlans(service);
        nodes.Integrated = (unit, repository) => Log?.Info("work.integrated", ("unit", unit), ("repository", repository));
        _transport.Start();
    }

    /// <summary>The run's evaluation runtime.</summary>
    internal NodeRuntime Runtime { get; }

    private MachineLog? Log => _watch.Log;

    /// <summary>
    /// Link 8 bound on the run's <paramref name="chain"/>: it begins the pass beside the look on the running set, so
    /// a read or a plugin that holds a pass holds no next look, and the closing loop waits for it as it waits for the sessions. A whole look's
    /// ask is kept until a pass takes it. The runtime's own wakes and a plugin's step fire link 8 alone, beside whatever look runs;
    /// one that fails is logged and the next wake or look passes again.
    /// </summary>
    internal void Bind(Chain chain, System.Threading.Timer due, WatchDoors doors, CancellationToken ct)
    {
        _due = due;
        // A landing door's ask for a whole pass wakes link 8 alone, as the runtime's own wakes do; the pass that begins after
        // it takes it, reading the units afresh, and answers it once it ends.
        var asks = _asks = new PassAsks(() => _watch.NodesWoken("door"));
        chain.Bind(ChainRanks.Nodes, (heard, token) =>
        {
            if (heard.Any(cause => cause.Key == WatchRun.Whole)) Interlocked.Exchange(ref _wholeAsked, 1);
            if (heard.Any(cause => cause is { Type: ChainEvents.Nodes, Subject: Awaited })) Interlocked.Exchange(ref _superviseAsked, 1);
            _watch.Running.PassBeside(PassAsync, token);
            return Task.FromResult(ChainFired.Nothing);
        });

        doors.Waking(wake => _ = Task.Run(async () =>
        {
            try
            {
                await chain.FireAsync([wake], ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The loop closed: nothing is passed after it.
            }
            catch (Exception error)
            {
                Log?.Failed("the evaluation runtime's own wake", error);
            }
        }, CancellationToken.None));
        _hooks.StepArrived = plugin => _watch.WakeNodes(new ChainEvent(ChainEvents.PluginStep, plugin, ChangeTopics.Nodes));
        doors.Asking(asks);
    }

    /// <summary>
    /// The run's close of what link 8 runs off the pass: a check's job ends written no result, so the next loop's reconcile sends it again;
    /// then the line's run, whose cut candidate is the next come-up's to go on with.
    /// </summary>
    internal async Task CloseAsync()
    {
        await _executor.DisposeAsync().ConfigureAwait(false);
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>One pass, on the choices the latest look read. What it says joins the next look's report; what it acted on is in the records.</summary>
    private async Task<IReadOnlyList<string>> PassAsync(CancellationToken token)
    {
        var asks = _asks!;
        var home = _watch.Home;
        var service = _watch.Service;

        // A pass performs plugins' steps, starts their processes and acts on trees, none of which the drain waits on. A door
        // waiting on the pass is answered that none read; the whole look's ask and the awaited set's stay set for the pass after the drain.
        if (_watch.DrainsNow())
        {
            asks.Take().Done(passed: false);
            return [];
        }

        var asked = asks.Take();
        var whole = (Interlocked.Exchange(ref _wholeAsked, 0) == 1) | asked.Asked;
        var lines = new List<string>();
        NodeRuntimePass pass;
        try
        {
            // A hand-off the driver's last run left open is put back before this run's first pass starts anything in its tree.
            if (HomeLease.HeldHere(home)) await _handOffs.BeforePassAsync(token).ConfigureAwait(false);

            // A set naming a plugin anew starts its process here, with no plan, as the plan's supervision does at the look it
            // wakes; withdrawn first where the choices no longer arm the runtime, so it starts nothing for a removed graph.
            if (Interlocked.Exchange(ref _superviseAsked, 0) == 1 && !await Runtime.WithdrawAsync(_config(), token).ConfigureAwait(false))
            {
                lines.AddRange(await _watch.Stages.TimeAsync("supervise", () => _hooks.ReconcileAsync(PluginCatalog.Load(home), token), beside: true)
                    .ConfigureAwait(false));
            }

            // At a whole look, each line a landed route verifies binds the run it owes before the pass, which reads it
            // among the runs and so is armed by it.
            var config = _config();
            if (whole && LineBatching.Armed(config) && HomeLease.HeldHere(home))
            {
                lines.AddRange(await LineBatching.BindAsync(home, config, await service.RegistryAsync(token).ConfigureAwait(false), new NodeRecordFiles(home),
                    DateTimeOffset.UtcNow, token).ConfigureAwait(false));
            }

            // At a whole look, each planned ask binds the delivery run its plan's repositories owe before the pass reads the runs.
            if (whole && HomeLease.HeldHere(home))
            {
                lines.AddRange(await _askRuns.BindAsync(
                    config, await service.AsksAsync(token).ConfigureAwait(false), await service.RegistryAsync(token).ConfigureAwait(false),
                    WorkflowSteps.PluginsOf(PluginCatalog.Load(home)), DateTimeOffset.UtcNow, token).ConfigureAwait(false));
            }

            var gates = new NodeGates(home, service);
            pass =await _watch.Stages.TimeAsync("nodes", () => Runtime.PassAsync(new NodeRuntimeLook(_config(), whole)
            {
                Asked = asked.Units,
                Judged = asked.Judged,
                Units = token => ReadNodeUnitsAsync(home, service, token),
                Steps = () => [.. PluginCatalog.Load(home).Contributing.SelectMany(plugin => plugin.Manifest.Steps)],
                // A receipt's role is what its plugin's manifest declares of its schema.
                Resources = plugin => PluginCatalog.Load(home).Contributing.FirstOrDefault(each => each.Manifest.Id == plugin)?.Manifest.Resources,
                // An observed node's event is bound to its branch's tip read now, once per checkout per pass.
                Tips = NodeTips.ReadAsync,
                // The look's git reads for `pushed` and an ahead `pull-request`, asked only where one waits on them.
                Remotes = NodeTips.RemotesAsync,
                // The gates for a unit whose opinion or look waits, read as a landing door reads them; and a quest whose run
                // is bound to a named version left to that version.
                Gates = gates.ReadAsync,
                // What the line holds of a run's branch, where its `content.holds` waits.
                Content = gates.ContentAsync,
                OnLine = gates.OnLineAsync,
                Bound = NodeRuntime.BoundIn(home),
            }, token), beside: true).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException
                                          or UnauthorizedAccessException)
        {
            asked.Done(passed: false);
            return [.. lines, $"work  the plugins' steps were not judged this time, and are judged again at the next look: {error.Message}"];
        }
        catch
        {
            asked.Done(passed: false);
            throw;
        }

        // The doors that asked this pass read the runs' records it kept.
        asked.Done(passed: true);
        Due(_due!, Runtime.NextDue);
        _watch.Passed?.Invoke(pass);

        // A set naming a plugin anew wakes link 8 once more, whose supervision starts the process the start then goes to.
        if (pass.AwaitedChanged) _watch.NodesWoken(Awaited);
        var acted = pass.Performed.Count(done => Acted.Contains(done.What));
        if (acted > 0) Log?.Info("chain.fired", ("link", ChainRanks.Nodes), ("topic", ChangeTopics.Nodes), ("count", acted));

        // A failed verification whose check says `failed: ask` asks for its fix with no press, at a whole look.
        if (whole && LineBatching.Armed(_config()) && HomeLease.HeldHere(home))
        {
            try
            {
                lines.AddRange(await VerifyFixStanding.AskAsync(service, new NodeRecordFiles(home), token).ConfigureAwait(false));
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException)
            {
                lines.Add($"work  a failed verification's fix was not asked this time, and is asked again at the next look: {error.Message}");
            }
        }

        return [.. lines, .. NodeRuntimeLines(pass)];
    }

    /// <summary>
    /// The units a pass of the evaluation runtime reads (link 8's <see cref="NodeRuntimeLook.Units"/>), each repository's checkout the awaited
    /// set names, and the tip of each branch a bound quest's chain worked.
    /// </summary>
    /// <remarks>
    /// No roots go to the inventory: its recovery refs are its own entries, not a node's, and gathering them (a listing and a <c>rev-list</c> of
    /// every repository the home names) on every read would be git starts nothing here reads. The tips are one <see cref="RefSnapshot"/> per
    /// repository a bound quest's chain worked in, taken for this read alone (<see cref="QuestClosing.TipsAsync"/>): a home with no quest
    /// bound to a closure graph, the owner's install, starts no git here.
    /// </remarks>
    internal static async Task<NodeUnitsRead> ReadNodeUnitsAsync(string home, ServiceClient service, CancellationToken token)
    {
        var unread = new List<string>();
        var unlinked = new List<string>();
        var registry = await service.RegistryAsync(token).ConfigureAwait(false);
        var units = await WorkUnits.ReadAsync(home, new Dictionary<string, string>(), new HashSet<string>(), service, unread, token, unlinked).ConfigureAwait(false);
        // The doors find a checkout's repository from the files alone, so the registry's roots are kept for them.
        GitFolders.Remember(home, registry.Where(repo => repo.Root is { Length: > 0 }).Select(repo => (repo.Root!, repo.Repository, repo.Workspace)));
        var roots = registry.Where(repo => repo.Root is { Length: > 0 }).GroupBy(repo => repo.Repository, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Root!, StringComparer.Ordinal);
        // Why the work's attribution is not read travels with the units, so a run's branch is never
        // handed to Current on a link the outage dropped.
        var branches = await AskRunReads.ReadAsync(home, roots, DriverConfig.Load(DriverConfig.ResolvePath(home)), token).ConfigureAwait(false);
        return new NodeUnitsRead(units, roots)
        {
            Tips = await QuestClosing.TipsAsync(units, roots, token).ConfigureAwait(false), Unread = unlinked, AskBranches = branches,
            Parts = await PartsReports.ReadAsync(new IntegrationStore(home), new ServiceTrainPlans(service), token, branches.Values.Select(each => each.Ask))
                .ConfigureAwait(false),
        };
    }

    /// <summary>What a person reads of a pass: each attempt sent, answered, retried, unknown or held, and what the pass said.</summary>
    internal static IReadOnlyList<string> NodeRuntimeLines(NodeRuntimePass pass) =>
    [
        .. pass.Performed
            .Where(done => done.What is PerformedWhat.Sent or PerformedWhat.Redelivered or PerformedWhat.Applied or PerformedWhat.Retry
                or PerformedWhat.Unknown or PerformedWhat.Held or PerformedWhat.Stale)
            .Select(done => $"work  {done.Unit}'s {done.Node}: {done.What}{(done.Code is { } code ? $" ({code})" : "")}"),
        .. pass.Said,
    ];

    /// <summary>
    /// The next timer the node records persist, set to wake a plan at its moment. A moment already past is left to the look's
    /// deadline: the pass that just ran judged everything due then, so waking again at once would only spin on what it could not act on.
    /// </summary>
    internal static void Due(System.Threading.Timer timer, DateTimeOffset? at)
    {
        var left = at is { } moment ? moment - DateTimeOffset.UtcNow : TimeSpan.Zero;
        timer.Change(left <= TimeSpan.Zero ? Timeout.InfiniteTimeSpan : left > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : left, Timeout.InfiniteTimeSpan);
    }
}
