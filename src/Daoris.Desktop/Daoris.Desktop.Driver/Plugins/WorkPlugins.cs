using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The work side of the hook set (FABRIC-PERFORM1a; the fabric events design §C4, §D6): for each plugin that speaks a work point, what its
/// steps are awaited by, the generation of its process, when that process may start again, and the events heard from it under their bounds.
/// <see cref="HookSet"/> starts and stops the processes; this says which it wants and keeps what they said.
/// </summary>
/// <remarks>
/// <para><b>Generations.</b> Each start of a plugin's process is its next generation, never reused while the loop runs. Its events are
/// tagged with the generation of the process they were read from, never with anything the frame says (§C4's first rule), and one whose
/// generation is not the plugin's current one when it arrives or is taken is refused (<see cref="WorkRefusals.NotSender"/>): an answer from
/// a process since restarted or stopped is never applied.</para>
///
/// <para><b>Resync.</b> The first frame to each generation says <c>resync</c>, and so does the next after any drop (a frame too long, too
/// many, the queue full, a frame that did not reach the plugin): at the next look a minute or more after the last resync, so a plugin that
/// floods is not asked to flood again at once. The minute after a resync takes, beside its 60, one event per awaited step it named, and as many
/// more wait, so a replay in a stable order is taken whole rather than losing its last at every resync (round 2).</para>
///
/// <para><b>Kept in memory, as the loop's record of a plugin's health is</b> (D119 §2): a new loop starts every plugin at generation 1, with
/// its waits afresh and nothing queued, so nothing a dead loop heard can be taken. Attempts are not recorded here (PERFORM1b).</para>
/// </remarks>
internal sealed class WorkPlugins(PluginLog log, Func<DateTimeOffset> clock) : IDisposable
{
    private sealed class State
    {
        public WorkRestarts Restarts { get; } = new();

        public EventRate Rate { get; } = new();

        public RefusalTally Tally { get; } = new();

        public Queue<(int Generation, JsonElement Params)> Waiting { get; } = new();

        /// <summary>The steps the last resync told it of, one replayed event each: as many more are taken in its minute, and wait (round 2).</summary>
        public int Replay { get; set; }

        /// <summary>The version and install the catalogue last named for it: another is an update, which starts its record afresh.</summary>
        public string? Build { get; set; }

        /// <summary>The last generation started.</summary>
        public int Generation { get; set; }

        /// <summary>The current generation's process, while the lease's loop listens to it.</summary>
        public IHookChannel? Listened { get; set; }

        public IReadOnlyList<WatchedUnit> Awaited { get; set; } = [];

        /// <summary>The set last told to the current generation, as written, so the same set is not told twice.</summary>
        public string? Told { get; set; }

        public bool ResyncDue { get; set; }

        public DateTimeOffset? Resynced { get; set; }
    }

    private readonly Dictionary<string, State> _states = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _going = new();

    /// <summary>Whether a plugin speaks the work wire: its hooks name a work point.</summary>
    public static bool Speaks(PluginEntry plugin) =>
        plugin.Manifest.Hooks?.Points.Any(point => HookPoints.Work.Contains(point, StringComparer.Ordinal)) == true;

    /// <summary>What each plugin's steps are awaited by, whole: a plugin not named awaits nothing. A changed set is told at once.</summary>
    public void Watch(IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> awaited)
    {
        lock (_gate)
        {
            foreach (var id in awaited.Keys) Of(id);
            foreach (var (id, state) in _states)
            {
                state.Awaited = awaited.TryGetValue(id, out var units) ? [.. units] : [];
                Tell(id, state);
            }
        }
    }

    public bool Awaits(string id)
    {
        lock (_gate) return _states.TryGetValue(id, out var state) && state.Awaited.Count > 0;
    }

    public bool MayStart(string id, DateTimeOffset now)
    {
        lock (_gate) return !_states.TryGetValue(id, out var state) || state.Restarts.MayStart(now);
    }

    public bool IsUnsound(string id)
    {
        lock (_gate) return _states.TryGetValue(id, out var state) && state.Restarts.Unsound;
    }

    public int? GenerationOf(string id)
    {
        lock (_gate) return _states.TryGetValue(id, out var state) && state.Listened is not null ? state.Generation : null;
    }

    /// <summary>The current generation's process, where the lease's loop listens to it and it is up.</summary>
    public (IHookChannel Channel, int Generation)? Performer(string id)
    {
        lock (_gate) return _states.TryGetValue(id, out var state) && state.Listened is { Alive: true } channel ? (channel, state.Generation) : null;
    }

    /// <summary>
    /// A process started: its generation. Where <paramref name="listen"/> (the lease is this loop's), its events are heard and it is told
    /// the awaited set at once, with <c>resync</c>; otherwise it runs for another point alone, and is neither told nor heard.
    /// </summary>
    public int Started(string id, IHookChannel channel, bool listen, DateTimeOffset now)
    {
        lock (_gate)
        {
            var state = Of(id);
            state.Restarts.Started(now);
            var generation = state.Generation += 1;
            state.Listened = null;
            state.Told = null;
            if (!listen) return generation;

            state.Listened = channel;
            state.ResyncDue = true;
            channel.Listen(@params => Arrive(id, generation, @params));
            Tell(id, state);
            return generation;
        }
    }

    /// <summary>An exit Daoris did not ask for, or a start that failed: the wait before the next start, or null where it is now unsound.</summary>
    public TimeSpan? Exited(string id, DateTimeOffset now)
    {
        lock (_gate)
        {
            var state = Of(id);
            Unlisten(state);
            return state.Restarts.Exited(now);
        }
    }

    /// <summary>Daoris's own stop, or the lease no longer this loop's: never counted, and nothing its process says is heard after.</summary>
    public void Stopped(string id)
    {
        lock (_gate)
        {
            if (_states.TryGetValue(id, out var state)) Unlisten(state);
        }
    }

    /// <summary>The person's <i>Try again</i>: an unsound plugin may start at the next look, its waits afresh.</summary>
    public void TryAgain(string id)
    {
        lock (_gate) Of(id).Restarts.TryAgain();
    }

    /// <summary>
    /// The plugins the catalogue still contributes, each with its build (its version and install): one switched off or removed starts its
    /// waits afresh, and so does one updated (FABRIC-PERFORM1a, round 2) — told here whether its process runs or not, since an unsound plugin's
    /// does not, and an update is how its maker answers.
    /// </summary>
    public void Keep(IEnumerable<(string Id, string Build)> contributing)
    {
        var kept = contributing.ToDictionary(plugin => plugin.Id, plugin => plugin.Build, StringComparer.Ordinal);
        lock (_gate)
        {
            foreach (var (id, state) in _states)
            {
                if (!kept.TryGetValue(id, out var build))
                {
                    state.Restarts.TryAgain();
                    state.Build = null;
                    continue;
                }

                if (state.Build is { } seen && seen != build) state.Restarts.TryAgain();
                state.Build = build;
            }
        }
    }

    /// <summary>
    /// A running process this loop did not listen to, now that the lease is its own again (FABRIC-PERFORM1a, round 2): a plugin kept for a
    /// loop point while the lease was elsewhere is heard again on its own generation, and told the awaited set with a resync.
    /// </summary>
    public void Reattach(string id, IHookChannel channel)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(id, out var state) || state.Listened is not null || !channel.Alive) return;
            var generation = state.Generation;
            state.Listened = channel;
            state.ResyncDue = true;
            channel.Listen(@params => Arrive(id, generation, @params));
            Tell(id, state);
        }
    }

    /// <summary>
    /// Whether an answer read from <paramref name="channel"/>, generation <paramref name="generation"/>, may be handed back now (round 2): that
    /// process is still the plugin's current one, listened to. One stopped or replaced since the call was sent answers for nobody.
    /// </summary>
    public bool IsCurrent(string id, IHookChannel channel, int generation)
    {
        lock (_gate) return _states.TryGetValue(id, out var state) && ReferenceEquals(state.Listened, channel) && state.Generation == generation;
    }

    /// <summary>The events heard since the last take, by plugin and in the order they came; a stale or misshapen one is refused here.</summary>
    public IReadOnlyList<WorkStepHeard> TakeSteps()
    {
        var now = clock();
        var heard = new List<WorkStepHeard>();
        lock (_gate)
        {
            foreach (var (id, state) in _states.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                while (state.Waiting.TryDequeue(out var waiting))
                {
                    if (state.Listened is null || waiting.Generation != state.Generation) Refuse(id, state, WorkRefusals.NotSender, now);
                    else if (WorkWire.ReadStep(waiting.Params, out _) is { } step) heard.Add(new WorkStepHeard(id, waiting.Generation, step));
                    else Refuse(id, state, WorkRefusals.Shape, now);
                }
            }
        }

        return heard;
    }

    /// <summary>At each look: the refusals held past their minute written, and a drop's resync told where a minute has passed since the last.</summary>
    public void Look(DateTimeOffset now)
    {
        lock (_gate)
        {
            foreach (var (id, state) in _states)
            {
                foreach (var (code, count) in state.Tally.Due(now)) log.Refused(id, code, count);
                if (state.ResyncDue && (state.Resynced is not { } last || now - last >= WorkBounds.Minute)) Tell(id, state);
            }
        }
    }

    /// <summary>The frames not yet sent are not sent: the loop is going.</summary>
    public void Dispose()
    {
        _going.Cancel();
        _going.Dispose();
    }

    private State Of(string id)
    {
        if (!_states.TryGetValue(id, out var state)) _states[id] = state = new State();
        return state;
    }

    private static void Unlisten(State state)
    {
        state.Listened?.Listen(null);
        state.Listened = null;
        state.Told = null;
    }

    /// <summary>An event read from generation <paramref name="generation"/>'s process, or null for a frame dropped unread: taken under the bounds.</summary>
    private void Arrive(string id, int generation, JsonElement? @params)
    {
        var now = clock();
        lock (_gate)
        {
            var state = Of(id);
            if (state.Listened is null || generation != state.Generation)
            {
                Refuse(id, state, WorkRefusals.NotSender, now);
                return;
            }

            if (@params is not { } frame)
            {
                Dropped(id, state, WorkRefusals.TooLong, now);
                return;
            }

            if (!state.Rate.Admit(now))
            {
                Dropped(id, state, WorkRefusals.TooFast, now);
                return;
            }

            if (state.Waiting.Count >= WorkBounds.EventsWaiting + state.Replay)
            {
                state.Waiting.Dequeue();
                Dropped(id, state, WorkRefusals.QueueFull, now);
            }

            state.Waiting.Enqueue((generation, frame));
        }
    }

    private void Dropped(string id, State state, string code, DateTimeOffset now)
    {
        state.ResyncDue = true;
        Refuse(id, state, code, now);
    }

    private void Refuse(string id, State state, string code, DateTimeOffset now)
    {
        if (state.Tally.Refused(code, now) is { } count) log.Refused(id, code, count);
    }

    /// <summary>
    /// The awaited set told to the listened process where it changed or a resync is due: at once, its answer not waited for here, so no look
    /// and no caller of <see cref="Watch"/> waits on a plugin. The peer writes frames in the order they are told.
    /// </summary>
    private void Tell(string plugin, State state)
    {
        if (state.Listened is not { Alive: true } channel) return;
        var resync = state.ResyncDue;
        var said = WorkWire.Watch(new WorkWatchFrame(state.Generation, false, state.Awaited)).ToJsonString();
        if (!resync && said == state.Told) return;

        state.Told = said;
        state.ResyncDue = false;
        if (resync)
        {
            // Round 2: the replay this asks for is taken whole, one event per awaited step beyond the minute's 60, and waits whole. Admitting
            // the first 60 of a stable replay dropped the same last ones at every resync, for ever. Daoris's own set sizes it, never the plugin.
            var now = clock();
            state.Resynced = now;
            state.Replay = state.Awaited.Sum(unit => unit.Awaiting.Count);
            state.Rate.Allow(state.Replay, now + WorkBounds.Minute);
        }

        var generation = state.Generation;
        Task sending;
        try
        {
            sending = channel.WatchAsync(new WorkWatchFrame(generation, resync, state.Awaited), _going.Token);
        }
        catch (Exception error) when (error is DriverException or ObjectDisposedException or InvalidOperationException)
        {
            sending = Task.FromException(error);
        }

        _ = sending.ContinueWith(
            task => Unsaid(plugin, generation, task), CancellationToken.None, TaskContinuationOptions.NotOnRanToCompletion, TaskScheduler.Default);
    }

    /// <summary>A frame that did not reach the plugin: a failure in the machine log, and the set told again with a resync at a later look.</summary>
    private void Unsaid(string id, int generation, Task task)
    {
        if (task.IsCanceled) return;
        lock (_gate)
        {
            if (_states.TryGetValue(id, out var state) && state.Generation == generation && state.Listened is not null)
            {
                state.ResyncDue = true;
                state.Told = null;
            }
        }

        var kind = task.Exception?.InnerException is { } error ? PluginFailures.KindOf(error, PluginEvents.Errored) : PluginEvents.Errored;
        log.Failed(id, HookPoints.Watch, kind, code: null, ms: null, PluginEvents.ByLoop);
    }
}
