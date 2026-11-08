using System.Diagnostics;
using System.Threading.Channels;

namespace Daoris.Driver;

/// <summary>
/// The hook processes of the enabled plugins, shared across ticks the way the process registry is —
/// reconciled against the catalogue each tick, asked at the points, and stopped with the loop.
/// </summary>
/// <remarks>
/// <para><b>`quest/consider` is a waterfall that fails closed.</b> Plugins are asked in catalogue
/// order; the first hold ends it and its reason is the consideration's. A plugin that answers late,
/// wrongly or not at all is a hold too, naming the plugin and the failure — the person reads it,
/// disables the plugin, and the driver never stopped.</para>
///
/// <para><b>`session/ended` is contained.</b> A failure there is a line and the tick goes on.</para>
///
/// <para><b>`state/changed` is contained and off the path</b>: a plugin is a middleware in the event
/// pipeline only as an observer beside it. <see cref="Tell"/> writes the change to a queue and returns, and the set's one teller
/// tells it, so the press whose change it is has answered before any plugin hears, and no plugin can hold, change or refuse it.</para>
///
/// <para><b>Every line is the plugin's</b>, under `plugin:&lt;id&gt;` on the console — transcript-class
/// material that stays on the machine.</para>
///
/// <para><b>What the set did is kept without the plugin's words</b>: each start, call,
/// failure and stop is a <c>plugin.*</c> line in the machine log and a word in the loop's record of the plugin's
/// health, through <see cref="Log"/>. A start that fails the same way at every look is one line until it
/// starts or fails differently.</para>
/// </remarks>
public sealed class HookSet(
    string home,
    SessionOutput? output = null,
    Func<PluginEntry, Action<string>, CancellationToken, Task<IHookChannel>>? start = null,
    // Where a plugin's word goes when there is no console buffer — the headless host prints it.
    Action<string, string>? say = null,
    // The process's machine log, where what the set did with each plugin is written. Null writes none.
    MachineLog? log = null,
    // The loop's own record of each plugin's health. Null keeps none.
    PluginHealth? health = null,
    // Whether this process holds the home's lease, asked at each look. Only the lease's loop starts a plugin for the
    // work wire, listens to its events and tells it what is awaited; a `--share` loop, which holds nothing, never does.
    Func<bool>? holdsHome = null,
    // The clock the work wire's waits and bounds are read on.
    Func<DateTimeOffset>? clock = null,
    // The API token a place's work process is handed, or the refusal that stops it. A plugin with a pull-request step is
    // started per place when this is given (PlacedWork); null starts every plugin as one process, handed none.
    Func<PluginEntry, WorkPlace, CancellationToken, Task<HostTokenHand>>? hostToken = null) : IAsyncDisposable, IWorkRuntimePort
{
    private readonly PlacedWork _placed = new();

    /// <summary>A running plugin's process, with what it is started from: what a stop's reason is judged against.</summary>
    /// <param name="Work">Whether the plugin speaks the work wire: its starts are supervised by <see cref="WorkPlugins"/>.</param>
    private sealed record Held(string Signature, string Version, DateTimeOffset? Installed, IHookChannel Channel, bool Work = false);

    private readonly Dictionary<string, Held> _running = new(StringComparer.Ordinal);

    // The work wire's side, its lease and its clock. Its own writer of the same lines, since a field cannot read `Log`.
    private readonly Func<bool> _holdsHome = holdsHome ?? WorkEventPlugins.Held(home);
    private readonly Lock _eventsGate = new();
    private WorkEventMade? _events;

    /// <summary><c>work/event</c>'s teller, made at its first use: told only while this loop holds the home.</summary>
    public WorkEventPoint Events
    {
        get { lock (_eventsGate) return (_events ??= WorkEventPlugins.Made(home, log, Log, id => _ = StopLateAsync(id), _clock)).Point; }
    }

    /// <summary>One event told at <c>work/event</c>; never waits.</summary>
    public void Tell(WorkEvent told) => Events.Tell(told);
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly WorkPlugins _work = new(new PluginLog(log, health), clock ?? (() => DateTimeOffset.UtcNow));
    private readonly SemaphoreSlim _reconciling = new(1, 1);

    // Stopped by a removal or an update (StopAsync) before the catalogue says which: written at the next look.
    private readonly Dictionary<string, Held> _stopped = new(StringComparer.Ordinal);

    // The plugins whose last start failed, by the kind it failed with: a failure repeated at every look is one line.
    private readonly Dictionary<string, string> _unstarted = new(StringComparer.Ordinal);

    private readonly Func<PluginEntry, Action<string>, CancellationToken, Task<IHookChannel>> _start =
        start ?? ((plugin, onLine, ct) => HookChannels.StartAsync(plugin, home, onLine, ct));

    /// <summary>
    /// How many changes wait for the plugins at <see cref="HookPoints.StateChanged"/> before the oldest goes: a plugin
    /// that answers late holds the next change by its patience at most, and nobody who tells waits on it.
    /// </summary>
    public const int ChangesWaiting = 64;

    // The changes told, waiting for the one teller started at the first, so a tell writes here and does nothing else.
    private readonly Channel<StateChangedFrame> _changes = Channel.CreateBounded<StateChangedFrame>(
        new BoundedChannelOptions(ChangesWaiting) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    private readonly CancellationTokenSource _going = new();
    private readonly Lock _tellingGate = new();
    private Task? _telling;

    /// <summary>
    /// What the set does with each plugin, into the machine log and the loop's record: also what the
    /// driver writes the servers it hands a session through, since the set is what it is given of the plugins.
    /// </summary>
    public PluginLog Log { get; } = new(log, health);

    /// <summary>The plugins whose process is up, by id, in catalogue order.</summary>
    public IReadOnlyList<string> Running
    {
        get { lock (_running) return _running.Keys.Select(PlacedWork.PluginOf).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList(); }
    }

    /// <summary>
    /// Start what the catalogue now has and stop what it no longer has — a plugin disabled between
    /// ticks is stopped at the next one, one enabled is started, one whose manifest changed is
    /// restarted. Returns the lines worth a tick's report, each classed as it is made: a plugin now unsound is news, a
    /// start that failed or an exit waited out the next look's, the rest what moved.
    /// </summary>
    /// <param name="said">The look's classing; none classes and keeps nothing.</param>
    public async Task<IReadOnlyList<string>> ReconcileAsync(PluginCatalog catalog, CancellationToken ct, LookSaid? said = null)
    {
        var lines = new LookLines(said ?? LookSaid.None, "plugin");
        await _reconciling.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // The work wire is the lease's loop's alone, asked at each look. An exit nobody asked for, of a plugin
            // on it, is told before what is wanted is judged, so its wait holds from this look.
            var now = _clock();
            var holds = _holdsHome();
            Events.Held = holds;
            List<string> exited;
            lock (_running) exited = [.. _running.Where(pair => pair.Value.Work && !pair.Value.Channel.Alive).Select(pair => pair.Key)];
            var waits = exited.ToDictionary(id => id, id => _work.Exited(id, now), StringComparer.Ordinal);
            // A plugin that takes the host's token is placed, its work process one per place awaiting it.
            _placed.Place(hostToken is null ? [] : catalog.Contributing.Where(PlacedWork.Takes).Select(p => p.Manifest.Id));
            _placed.Apply(_work.Watch);
            var contributing = catalog.Contributing.ToDictionary(p => p.Manifest.Id, p => p, StringComparer.Ordinal);
            _work.Keep(catalog.Contributing.Select(p => (p.Manifest.Id, Build(p)))
                .Concat(_placed.Places().Where(key => contributing.ContainsKey(PlacedWork.PluginOf(key)))
                    .Select(key => (key, Build(contributing[PlacedWork.PluginOf(key)])))));

            // Only a plugin the loop has something to ask: one that speaks at a landing alone is started
            // by the landing, for that landing, and a process kept beside the ticks would idle. One on the work wire is
            // started where this loop holds the lease and a unit awaits its steps, once its wait after an unasked exit is over.
            var wanted = catalog.Contributing
                .Where(p => p.Manifest.Hooks is { } hooks
                    && (hooks.Points.Any(point => HookPoints.Loop.Contains(point, StringComparer.Ordinal)) || (holds && WorkEventPlugins.Declares(p))
                        || (holds && WorkPlugins.Speaks(p) && !_placed.IsPlaced(p.Manifest.Id) && _work.Awaits(p.Manifest.Id))))
                .Where(p => !WorkPlugins.Speaks(p) || _placed.IsPlaced(p.Manifest.Id) || _work.MayStart(p.Manifest.Id, now))
                .Where(p => !WorkEventPlugins.Declares(p) || _work.MayStart(p.Manifest.Id, now))
                .ToDictionary(p => p.Manifest.Id, p => p, StringComparer.Ordinal);
            if (holds)
            {
                foreach (var key in _placed.Places())
                {
                    if (contributing.TryGetValue(PlacedWork.PluginOf(key), out var placed) && placed.Manifest.Hooks is not null
                        && WorkPlugins.Speaks(placed) && _work.Awaits(key) && _work.MayStart(key, now))
                    {
                        wanted[key] = placed;
                    }
                }
            }

            // The stops a removal or an update asked for, said now that the catalogue says which it is.
            List<(string Id, Held Held)> asked;
            lock (_running)
            {
                asked = [.. _stopped.Select(pair => (pair.Key, pair.Value))];
                _stopped.Clear();
            }

            foreach (var (id, held) in asked) Log.Stopped(PlacedWork.PluginOf(id), Why(PlacedWork.PluginOf(id), held, catalog), PluginEvents.ByLoop);

            List<string> gone;
            lock (_running)
            {
                gone = _running.Keys.Where(id =>
                        !wanted.TryGetValue(id, out var plugin)
                        || _running[id].Signature != Signature(plugin)
                        || !_running[id].Channel.Alive
                        // One at work/event started again as the lease moves, so its handshake says where its events begin.
                        || (_running[id].Channel.Points.Contains(HookPoints.Event) && Events.Listening.Contains(id) != holds))
                    .ToList();
            }

            foreach (var id in gone)
            {
                Held held;
                lock (_running)
                {
                    held = _running[id];
                    _running.Remove(id);
                }
                var wasAlive = held.Channel.Alive;
                var code = held.Channel.ExitCode;

                // One that exited after this look noted the exits is an unasked exit all the same, its wait from
                // now. Removed uncounted and started again below, a plugin crashing on what it is told would never wait.
                if (held.Work && !wasAlive && !waits.ContainsKey(id)) waits[id] = _work.Exited(id, now);
                if (held.Work) _work.Stopped(id);
                await Events.LeaveAsync(id).ConfigureAwait(false);
                await held.Channel.DisposeAsync().ConfigureAwait(false);
                var waited = waits.TryGetValue(id, out var wait);
                var name = PlacedWork.PluginOf(id);
                if (wasAlive) lines.Progress($"plugin  {name}: stopped.");
                else if (waited) Waiting(lines, name, wait);
                else lines.Retry($"plugin  {name}: its hook process had exited — it will be started again if it is still enabled.");
                if (wasAlive) Log.Stopped(name, WorkWhy(name, held, catalog, holds) ?? Why(name, held, catalog), PluginEvents.ByLoop);
                else Log.Failed(name, PluginEvents.AtProcess, PluginEvents.Exited, code, ms: null, PluginEvents.ByLoop);
                if (waited && wait is null) Log.Failed(name, PluginEvents.AtProcess, PluginEvents.Unsound, code: null, ms: null, PluginEvents.ByLoop);
            }

            // A plugin kept for another point while this loop does not hold the lease is neither told nor heard.
            if (!holds)
            {
                List<string> work;
                lock (_running) work = [.. _running.Where(pair => pair.Value.Work).Select(pair => pair.Key)];
                foreach (var id in work) _work.Stopped(id);
            }

            // A plugin whose start failed and that the loop no longer keeps: its record ends as a stop's would.
            foreach (var id in _unstarted.Keys.Where(id => !wanted.ContainsKey(id)).ToList())
            {
                _unstarted.Remove(id);
                Log.Stopped(PlacedWork.PluginOf(id), Why(PlacedWork.PluginOf(id), held: null, catalog), PluginEvents.ByLoop);
            }

            foreach (var (key, plugin) in wanted.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                Held? kept;
                lock (_running) _running.TryGetValue(key, out kept);
                if (kept is not null)
                {
                    // A process kept for its loop point while the lease is elsewhere is heard and told again
                    // once the lease is this loop's; otherwise it stays deaf until it happens to restart.
                    if (holds && kept.Work) _work.Reattach(key, kept.Channel);
                    continue;
                }

                var id = plugin.Manifest.Id;
                // Its wait asked again: an exit counted partway through this look holds it from this start.
                if (WorkPlugins.Speaks(plugin) && !_work.MayStart(key, now)) continue;
                var took = Stopwatch.StartNew();
                try
                {
                    // The place's own hand: the token of the identity serving its origin, or the refusal that leaves it unstarted.
                    var hand = hostToken is not null && _placed.PlaceOf(key) is { } place ? await hostToken(plugin, place, ct).ConfigureAwait(false) : null;
                    if (hand?.Refusal is { } refused) throw new DriverException($"plugin `{id}`: {refused}.");
                    IHookChannel channel;
                    HookProcess.HostToken.Value = hand?.Token;
                    HookProcess.Events.Value = holds && WorkEventPlugins.Declares(plugin) ? WorkEventPlugins.Join(Events, key, home) : null;
                    try
                    {
                        channel = await _start(plugin, line => Say(id, line), ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        await Events.LeaveAsync(key).ConfigureAwait(false);
                        throw;
                    }
                    finally
                    {
                        HookProcess.HostToken.Value = null;
                        HookProcess.Events.Value = null;
                    }

                    if (holds && channel.Points.Contains(HookPoints.Event)) Events.Listen(key, channel.EventAsync);
                    else await Events.LeaveAsync(key).ConfigureAwait(false);

                    // Its next generation, heard and told only where this loop holds the lease.
                    var work = WorkPlugins.Speaks(plugin);
                    if (work) _work.Started(key, channel, listen: holds, now);
                    lock (_running) _running[key] = new Held(Signature(plugin), plugin.Manifest.Version, InstalledAt(plugin.Folder), channel, work);
                    lines.Progress($"plugin  {id}: started, listening on {(channel.Points.Count > 0 ? string.Join(", ", channel.Points) : "nothing")}.");
                    _unstarted.Remove(key);
                    Log.Started(id, channel.Points, took.ElapsedMilliseconds, PluginEvents.ByLoop);
                }
                catch (DriverException error)
                {
                    // Logged and skipped, never fatal — and said where a person will read it.
                    Say(id, error.Message);
                    lines.Retry($"plugin  {id}: could not start — {error.Message}");
                    var kind = PluginFailures.KindOf(error, PluginEvents.Unstartable);
                    if (_unstarted.GetValueOrDefault(key) != kind)
                    {
                        Log.Failed(id, PluginEvents.AtStart, kind, code: null, took.ElapsedMilliseconds, PluginEvents.ByLoop);
                    }

                    _unstarted[key] = kind;

                    // A start that failed waits as an unasked exit does, so it is not tried at every look.
                    if (WorkPlugins.Speaks(plugin) && _work.Exited(key, now) is null)
                    {
                        Waiting(lines, id, null);
                        Log.Failed(id, PluginEvents.AtStart, PluginEvents.Unsound, code: null, ms: null, PluginEvents.ByLoop);
                    }
                }
            }

            _work.Look(now);
        }
        finally
        {
            _reconciling.Release();
        }

        return lines.Said;
    }

    /// <summary>
    /// What a person reads of a work process's unasked exit: when it starts again, the next look's, or that it does
    /// not, now unsound, which is news: nobody pressed it, and it stays stopped until the person tries again.
    /// </summary>
    private static void Waiting(LookLines lines, string id, TimeSpan? wait)
    {
        if (wait is { } after)
        {
            lines.Retry($"plugin  {id}: its hook process had exited — it is started again in {(after.TotalMinutes == 1 ? "1 minute" : $"{after.TotalMinutes:0} minutes")}.");
            return;
        }

        lines.News(new LookNews(LookSaid.PluginUnsound, id),
            $"plugin  {id}: its hook process exited {WorkBounds.ExitsUnsound} times within an hour — it is not started again until you try again.");
    }

    /// <summary>
    /// Why a work process the catalogue still has, unchanged, is stopped: this loop does not hold the home, or no unit
    /// awaits its steps. Null for any other plugin or reason, which <see cref="Why"/> says.
    /// </summary>
    private static string? WorkWhy(string id, Held held, PluginCatalog catalog, bool holds) =>
        held.Work && catalog.Contributing.FirstOrDefault(p => p.Manifest.Id == id) is { } entry && Signature(entry) == held.Signature
            ? holds ? PluginEvents.Idle : PluginEvents.NotHeld
            : null;

    /// <summary>
    /// Why a plugin's process is stopped, from what the catalogue now says of it: gone from it, switched off, a
    /// new install of it (another version, or its folder replaced), or anything else about it changed.
    /// </summary>
    private static string Why(string id, Held? held, PluginCatalog catalog)
    {
        var entry = catalog.Plugins.FirstOrDefault(p => string.Equals(p.Manifest.Id, id, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return PluginEvents.Removed;
        if (!entry.Enabled) return PluginEvents.Off;
        if (held is not null && (held.Version != entry.Manifest.Version || held.Installed != InstalledAt(entry.Folder)))
        {
            return PluginEvents.Updated;
        }

        return PluginEvents.Changed;
    }

    /// <summary>A plugin's build, as <see cref="Why"/> tells an update: its version and its folder's creation time.</summary>
    private static string Build(PluginEntry plugin) => $"{plugin.Manifest.Version}\t{InstalledAt(plugin.Folder)?.UtcTicks}";

    /// <summary>A plugin's install folder's creation time: an update replaces the folder whole.</summary>
    private static DateTimeOffset? InstalledAt(string folder) => PluginFolders.MadeAt(folder);

    /// <summary>
    /// Stop one plugin's hook process now, rather than at the next tick — what removing a plugin needs
    /// first, because on Windows a running process holds its working directory.
    /// </summary>
    /// <returns>Whether a process had been running to stop.</returns>
    /// <remarks>
    /// Its <c>plugin.stopped</c> line is written at the next look, once the catalogue says whether the
    /// plugin is removed or updated: a stop asked for here cannot tell which, and the route asks for that look
    /// at once.
    /// </remarks>
    public async Task<bool> StopAsync(string id, CancellationToken ct = default)
    {
        await _reconciling.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // A placed plugin has a process for each place it serves: all of them stop.
            List<(string Key, Held Held)> stopping = [];
            lock (_running)
            {
                foreach (var key in _running.Keys.Where(key => PlacedWork.PluginOf(key) == id).ToList())
                {
                    _running.Remove(key, out var each);
                    _stopped[key] = each!;
                    stopping.Add((key, each!));
                }
            }

            WorkEventPoint? told;
            lock (_eventsGate) told = _events?.Point;
            foreach (var (key, held) in stopping)
            {
                if (held.Work) _work.Stopped(key);
                // Its queue and teller go with the process: kept, the teller would tell the dead channel and refuse the next start its place.
                if (told is not null) await told.LeaveAsync(key).ConfigureAwait(false);
                await held.Channel.DisposeAsync().ConfigureAwait(false);
            }

            return stopping.Count > 0;
        }
        finally
        {
            _reconciling.Release();
        }
    }

    /// <summary>
    /// What each plugin's steps are awaited by, whole: a plugin not named awaits nothing. A changed set is told to a
    /// running process at once; one not running is started at the next look, where the lease is held here.
    /// </summary>
    public void Watch(IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> awaited)
    {
        _placed.Await(awaited);
        _placed.Apply(_work.Watch);
    }

    /// <summary>
    /// The step events heard since the last take, each with its plugin and the generation of the process it came from, in the order they came.
    /// An event from a process since restarted or stopped is refused here (<see cref="WorkRefusals.NotSender"/>), as is one not of its shape.
    /// A place's process is named by its plugin, with a generation unique across that plugin's places (<see cref="PlacedWork.Outer"/>).
    /// </summary>
    public IReadOnlyList<WorkStepHeard> TakeSteps() =>
        [.. _work.TakeSteps().Select(heard => heard with { Plugin = PlacedWork.PluginOf(heard.Plugin), Generation = _placed.Outer(heard.Plugin, heard.Generation) })];

    /// <summary>
    /// Told a plugin's id as a step event of its current process is queued for <see cref="TakeSteps"/>: the lease's loop wakes
    /// its evaluation runtime alone, so a completion is kept at once rather than at the next look. Null tells nobody.
    /// </summary>
    public Action<string>? StepArrived
    {
        get => _work.Arrived;
        set => _work.Arrived = value;
    }

    /// <summary>The last <c>work/watch</c> frame whose send to that process completed, or null.</summary>
    public WorkWatchFrame? SentTo(string plugin, int generation) =>
        _placed.Inner(plugin, generation) is { } process ? _work.SentTo(process.Key, process.Generation) : null;

    /// <summary>
    /// A step event the performer did not believe: counted with the transport's refusals, a
    /// <c>plugin.step</c> line of plugin, code and count at most once a minute per plugin and code, never the frame's words.
    /// </summary>
    public void Refused(string plugin, string code)
    {
        // One refusal is one count: the first place this supervision holds takes it, never each place.
        foreach (var key in _placed.KeysOf(plugin))
        {
            if (_work.Refused(key, code)) break;
        }
    }

    /// <summary>The generation of the plugin's work process while one is up and listened to, else null; of a placed plugin, one of its places'.</summary>
    public int? GenerationOf(string plugin)
    {
        foreach (var key in _placed.KeysOf(plugin))
        {
            if (_work.GenerationOf(key) is { } generation) return _placed.Outer(key, generation);
        }

        return null;
    }

    /// <summary>The current generation of the process <paramref name="held"/> belongs to: the place's own where the plugin is placed.</summary>
    public int? GenerationOf(string plugin, int held)
    {
        if (_placed.Inner(plugin, held) is not { } process) return null;
        return _work.GenerationOf(process.Key) is { } generation ? _placed.Outer(process.Key, generation) : null;
    }

    /// <summary>Whether three unasked exits within an hour have marked the plugin unsound, so the loop does not start it again.</summary>
    public bool IsUnsound(string plugin) => _placed.KeysOf(plugin).Any(_work.IsUnsound);

    /// <summary>The person's <i>Try again</i>: an unsound plugin is started again at the next look, its waits afresh.</summary>
    public void TryAgain(string plugin)
    {
        foreach (var key in _placed.KeysOf(plugin)) _work.TryAgain(key);
    }

    /// <summary>
    /// One attempt sent to the plugin's current process, returning that process's generation once it acknowledged receipt. Refused
    /// where this loop does not hold the lease, the plugin's process is not up, or it did not name <c>work/perform</c> and its capability.
    /// </summary>
    /// <remarks>
    /// The answer is handed back only while the process that gave it is still the plugin's current generation: one stopped or
    /// replaced while the call is out is refused here, never returned as the new process's. <see cref="ReconcileStepAsync"/> is fenced alike.
    /// </remarks>
    public async Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct)
    {
        var (channel, generation, key) = Performer(plugin, call.Unit);
        await channel.PerformAsync(call, ct).ConfigureAwait(false);
        HandedBack(plugin, key, channel, generation, HookPoints.Perform);
        return _placed.Outer(key, generation);
    }

    /// <summary>What became of one attempt, asked of the plugin's current process, with that process's generation.</summary>
    public async Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct)
    {
        var (channel, generation, key) = Performer(plugin, call.Unit);
        var answer = await channel.ReconcileStepAsync(call, ct).ConfigureAwait(false);
        HandedBack(plugin, key, channel, generation, PluginSteps.ReconcilePoint);
        return (answer, _placed.Outer(key, generation));
    }

    /// <summary>
    /// One receipt's removal asked of the plugin's current process, with that process's generation: fenced as a
    /// performance is, so only the lease's loop asks, of a process that named <c>work/perform</c> and its capability.
    /// </summary>
    public async Task<(WorkCleanupAnswer Answer, int Generation)> CleanupAsync(string plugin, WorkCleanupCall call, CancellationToken ct)
    {
        // A receipt names no unit, so no place: a placed plugin's step creates nothing to clean, and its process is not asked.
        var (channel, generation, key) = Performer(plugin, unit: null);
        var answer = await channel.CleanupAsync(call, ct).ConfigureAwait(false);
        HandedBack(plugin, key, channel, generation, PluginSteps.CleanupPoint);
        return (answer, _placed.Outer(key, generation));
    }

    /// <summary>
    /// The plugins whose process the lease's loop keeps up though no unit awaits their steps: each owes a receipt's
    /// cleanup, which only its process answers. Whole each time; a plugin not named is owed nothing.
    /// </summary>
    public void Owe(IReadOnlySet<string> plugins) => _work.Owe(plugins);

    /// <summary>
    /// The generation fence: an answer is handed back while its process is the plugin's current one, and refused
    /// as its process's exit otherwise. An answer read just before a stop, or from stdout buffered past an exit, can complete after the next
    /// start has been given the plugin's name.
    /// </summary>
    private void HandedBack(string plugin, string key, IHookChannel channel, int generation, string point)
    {
        if (_work.IsCurrent(key, channel, generation)) return;
        throw PluginFailures.Mark(
            new DriverException(
                $"plugin `{plugin}` answered `{point}` from its process of generation {generation}, which has since stopped: the answer is not taken."),
            PluginEvents.Exited);
    }

    /// <summary>The plugin's current work process, where this loop may ask it to perform: refused with the reason otherwise.</summary>
    private (IHookChannel Channel, int Generation, string Key) Performer(string plugin, string? unit)
    {
        if (!_holdsHome())
        {
            throw new DriverException($"plugin `{plugin}` is asked to perform only by the loop that holds this home, and this process does not hold it.");
        }

        // A placed plugin is asked at the place its unit is told for, whose token its process alone holds.
        var key = _placed.KeyFor(plugin, unit)
            ?? throw new DriverException($"plugin `{plugin}` has no process for {(unit is null ? "an unplaced ask" : $"unit `{unit}`'s place")}.");
        var (channel, generation) = _work.Performer(key)
            ?? throw new DriverException($"plugin `{plugin}`'s work process is not running.");
        if (!channel.Points.Contains(HookPoints.Perform, StringComparer.Ordinal) || !channel.Capabilities.Contains(WorkWire.Capability, StringComparer.Ordinal))
        {
            throw new DriverException(
                $"plugin `{plugin}` did not name `{HookPoints.Perform}` and `{WorkWire.Capability}` at its handshake: it is never asked to perform.");
        }

        return (channel, generation, key);
    }

    /// <summary>The waterfall, for one planned start.</summary>
    public async Task<HookDecision> ConsiderAsync(Consideration start, CancellationToken ct)
    {
        var payload = HookFrames.Consider(start);

        foreach (var (id, channel) in Listening(HookPoints.QuestConsider))
        {
            HookDecision decision;
            var took = Stopwatch.StartNew();
            try
            {
                decision = await channel.ConsiderAsync(payload, ct).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                // 🔴 Fail closed, naming the plugin: the loop spends accounts, and a policy that
                // silently failed open is the one wrong result nobody would see.
                Say(id, error.Message);
                CallFailed(id, channel, HookPoints.QuestConsider, error, took);
                return HookDecision.Hold($"plugin `{id}` could not decide — {error.Message} `daoris plugin disable {id}` lets the quest go.");
            }

            // The decision's word, never its reason: the reason stays on the console, below.
            Log.Called(id, HookPoints.QuestConsider, decision.Allowed ? PluginEvents.Allow : PluginEvents.Hold, took.ElapsedMilliseconds);
            if (!decision.Allowed)
            {
                Say(id, $"holds #{start.Quest.Id} → {start.Quest.To}: {decision.Reason}");
                return HookDecision.Hold($"plugin `{id}` holds it: {decision.Reason}");
            }
        }

        return HookDecision.Allow;
    }

    /// <summary>
    /// The observation, fanned out and contained. Returns the lines a failure is worth, the log's: the plugin's own page
    /// says its failed calls, and the session's end has been said already.
    /// </summary>
    /// <param name="said">The look's classing; none classes and keeps nothing.</param>
    public async Task<IReadOnlyList<string>> EndedAsync(SessionEnded ended, CancellationToken ct, LookSaid? said = null)
    {
        var lines = new LookLines(said ?? LookSaid.None, "plugin");
        var payload = HookFrames.Ended(ended);

        foreach (var (id, channel) in Listening(HookPoints.SessionEnded))
        {
            var took = Stopwatch.StartNew();
            try
            {
                await channel.EndedAsync(payload, ct).ConfigureAwait(false);
                Log.Called(id, HookPoints.SessionEnded, PluginEvents.Answered, took.ElapsedMilliseconds);
            }
            catch (DriverException error)
            {
                Say(id, error.Message);
                lines.Progress($"plugin  {id}: {error.Message}");
                CallFailed(id, channel, HookPoints.SessionEnded, error, took);
            }
        }

        return lines.Said;
    }

    /// <summary>
    /// Tell the plugins at <see cref="HookPoints.StateChanged"/> what moved, and return at once: the
    /// change waits here for the set's one teller, so whoever tells (the shell's bus, as a press answers) never waits on a plugin,
    /// and nothing a plugin says reaches back to it. Changes are told in the order they came; past <see cref="ChangesWaiting"/>
    /// waiting, the oldest goes. A set that has gone tells nothing more.
    /// </summary>
    public void Tell(StateChangedFrame changed)
    {
        if (!_changes.Writer.TryWrite(changed)) return;
        lock (_tellingGate) _telling ??= Task.Run(TellingAsync);
    }

    /// <summary>
    /// One change told to every plugin listening at <see cref="HookPoints.StateChanged"/>, all at once and contained: a plugin that
    /// fails is said on the console and in the machine log, and the others hear it all the same. Returns the lines a failure is worth.
    /// </summary>
    public async Task<IReadOnlyList<string>> ChangedAsync(StateChangedFrame changed, CancellationToken ct)
    {
        var payload = HookFrames.Changed(changed);
        var told = await Task.WhenAll(Listening(HookPoints.StateChanged).Select(async listener =>
        {
            var (id, channel) = listener;
            var took = Stopwatch.StartNew();
            try
            {
                await channel.ChangedAsync(payload, ct).ConfigureAwait(false);
                Log.Called(id, HookPoints.StateChanged, PluginEvents.Answered, took.ElapsedMilliseconds);
                return null;
            }
            catch (DriverException error)
            {
                Say(id, error.Message);
                CallFailed(id, channel, HookPoints.StateChanged, error, took);
                return $"plugin  {id}: {error.Message}";
            }
        })).ConfigureAwait(false);

        return [.. told.OfType<string>()];
    }

    /// <summary>The set's one teller: each change in turn, until the set goes.</summary>
    private async Task TellingAsync()
    {
        try
        {
            await foreach (var changed in _changes.Reader.ReadAllAsync(_going.Token).ConfigureAwait(false))
            {
                try
                {
                    await ChangedAsync(changed, _going.Token).ConfigureAwait(false);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    // Contained: a call that failed some other way than the wire names (a channel stopped under it) is that
                    // plugin's, and the next change is told all the same. There is no tick here to catch it, so it is caught here.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The set is going.
        }
    }

    /// <summary>A plugin late at <c>work/event</c> too often, stopped: an exit of its own, started again after its wait.</summary>
    private async Task StopLateAsync(string id)
    {
        await _reconciling.WaitAsync().ConfigureAwait(false);
        try
        {
            Held? held;
            lock (_running) _running.Remove(id, out held);
            if (held is null) return;
            await held.Channel.DisposeAsync().ConfigureAwait(false);
            Log.Stopped(PlacedWork.PluginOf(id), PluginEvents.Late, PluginEvents.ByLoop);
            if (_work.Exited(id, _clock()) is null) Log.Failed(id, PluginEvents.AtProcess, PluginEvents.Unsound, null, null, PluginEvents.ByLoop);
        }
        finally
        {
            _reconciling.Release();
        }
    }

    /// <summary>A call that failed, as its kind: a plain refusal from a channel is the plugin's error.</summary>
    private void CallFailed(string id, IHookChannel channel, string point, DriverException error, Stopwatch took)
    {
        var kind = PluginFailures.KindOf(error, PluginEvents.Errored);
        Log.Failed(id, point, kind, kind == PluginEvents.Exited ? channel.ExitCode : null, took.ElapsedMilliseconds, PluginEvents.ByLoop);
    }

    /// <summary>Every process stopped with the loop, and said so: a stop a removal or an update asked for, by what the catalogue says now.</summary>
    public async ValueTask DisposeAsync()
    {
        // The changes waiting go with the set: its teller is stopped before the processes it would tell, and so is the work
        // wire's: a frame not yet sent is not sent.
        _changes.Writer.TryComplete();
        await _going.CancelAsync().ConfigureAwait(false);
        _work.Dispose();
        if (_events is { } made) await made.DisposeAsync().ConfigureAwait(false);
        Task? telling;
        lock (_tellingGate) telling = _telling;
        if (telling is not null) await telling.ConfigureAwait(false);

        List<(string Id, Held Held)> running;
        List<(string Id, Held Held)> asked;
        lock (_running)
        {
            running = [.. _running.Select(pair => (pair.Key, pair.Value))];
            asked = [.. _stopped.Select(pair => (pair.Key, pair.Value))];
            _running.Clear();
            _stopped.Clear();
        }

        foreach (var (id, held) in running)
        {
            await held.Channel.DisposeAsync().ConfigureAwait(false);
            Log.Stopped(PlacedWork.PluginOf(id), PluginEvents.Ended, PluginEvents.ByLoop);
        }

        if (asked.Count > 0)
        {
            var catalog = PluginCatalog.Load(home);
            foreach (var (id, held) in asked) Log.Stopped(PlacedWork.PluginOf(id), Why(PlacedWork.PluginOf(id), held, catalog), PluginEvents.ByLoop);
        }
    }

    private List<(string Id, IHookChannel Channel)> Listening(string point)
    {
        lock (_running)
        {
            return _running
                .Where(pair => !PlacedWork.IsPlace(pair.Key) && pair.Value.Channel.Alive && pair.Value.Channel.Points.Contains(point, StringComparer.Ordinal))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => (pair.Key, pair.Value.Channel))
                .ToList();
        }
    }

    private void Say(string id, string line)
    {
        if (output is not null) output.Append($"plugin:{id}", line);
        else say?.Invoke(id, line);
    }

    private static string Signature(PluginEntry plugin) =>
        $"{plugin.Folder}\t{plugin.Manifest.Version}\t{string.Join(" ", plugin.Manifest.Hooks!.Command)}\t{string.Join(",", plugin.Manifest.Hooks.Points)}";
}
