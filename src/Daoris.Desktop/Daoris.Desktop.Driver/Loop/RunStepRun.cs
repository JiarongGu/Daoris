namespace Daoris.Driver;

/// <summary>
/// The run's progression as a loop runs it: its consumer (<see cref="RunStepConsumer"/>) and the bus's queue that hands it the facts, over the
/// lease holder's log, started once link 8 is bound and stopped before it closes. Beside it, the sessions' settle boxes are heard as they are
/// written (a watch on the sessions folder), each settle waiting teed as <see cref="BusTypes.StepSettled"/>, which no look takes any more. The
/// sweep is only the watcher's overflow net, not a look: the operating system drops events without an error where its buffer is full or a
/// share is remote, so the boxes are read once a minute for a settle no event told, and counted by how the sweep found it (<see cref="BusMeasures.Sweep"/>).
/// </summary>
internal sealed class RunStepRun : IAsyncDisposable
{
    /// <summary>The watcher's overflow guard: how long an event the system dropped silently waits for the sweep.</summary>
    public static readonly TimeSpan Sweep = TimeSpan.FromMinutes(1);

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Id, DateTimeOffset At)> _teed = new(StringComparer.Ordinal);

    private readonly BusQueue _queue;
    private readonly BusLog _bus;
    private readonly string _home;
    private readonly CancellationTokenSource _closing = new();
    private FileSystemWatcher? _watcher;
    private Task _sweeping = Task.CompletedTask;

    private RunStepRun(BusQueue queue, BusLog bus, string home)
    {
        _queue = queue;
        _bus = bus;
        _home = home;
    }

    /// <summary>The progression over <paramref name="bus"/> for a watch's loop; none where no log is held here (a loop beside an instance).</summary>
    public static async Task<RunStepRun?> StartAsync(BusLog? bus, DriverWatch watch, ServiceClient service, string home, CancellationToken ct)
    {
        if (bus is null) return null;
        try
        {
            return await StartAsync(bus, new RunStepConsumer(
                (unit, token) => watch.PassUnitAsync(unit, RunStepConsumer.Bound, token),
                async (session, token) => (await StepSettleAsks.TakeAsync(
                        new NodeRecordFiles(home), watch.Processes.Delegates.AgentOf,
                        async (said, text, key, told) => (await WordsHead.FollowAsync(said, at => service.SayAsync(at, text, ct: told, from: DaorisWords.From, key: key))
                            .ConfigureAwait(false)).Said,
                        bus.Clock.GetUtcNow, token, watch.RunWorkers, session).ConfigureAwait(false)).Count,
                bus, (once, why) => bus.Warn("run-step:" + once, "run.step-unjudged", ("why", why))), home, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException)
        {
            bus.Warn("run-step:start", "run.step-unstarted", ("why", error.Message));
            return null;
        }
    }

    /// <summary>The run over <paramref name="consumer"/>: the settle boxes heard under <paramref name="home"/>.</summary>
    internal static async Task<RunStepRun?> StartAsync(BusLog bus, RunStepConsumer consumer, string home, CancellationToken ct)
    {
        var queue = await BusQueue.StartAsync(bus, [consumer], new BusQueueOptions { Clock = bus.Clock }, ct: ct).ConfigureAwait(false);
        var run = new RunStepRun(queue, bus, home);
        run.Listen();
        run._sweeping = Task.Run(() => run.SweepAsync(run._closing.Token));
        return run;
    }

    /// <summary>One watch on the sessions folder for the settles' files: a write, a rename into place, a loss of events (all swept at once).</summary>
    private void Listen()
    {
        try
        {
            var folder = DaorisHome.Entry(_home, HomeEntry.Sessions);
            Directory.CreateDirectory(folder);
            var watcher = new FileSystemWatcher(folder, "*" + StepSettleAsks.AskSuffix)
            {
                InternalBufferSize = FolderWatcherSource.Buffer,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            watcher.Created += (_, e) => Heard(e.Name);
            watcher.Changed += (_, e) => Heard(e.Name);
            watcher.Renamed += (_, e) => Heard(e.Name);
            watcher.Error += (_, _) => TeeWaiting(overflow: true);
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or PlatformNotSupportedException)
        {
            // No watch: the sweep tees what waits, a minute after it is written.
            _bus.Warn("run-step:watch", "run.step-unwatched", ("why", error.Message));
        }
    }

    private void Heard(string? file)
    {
        if (file is null || !file.EndsWith(StepSettleAsks.AskSuffix, StringComparison.Ordinal)) return;
        var session = file[..^StepSettleAsks.AskSuffix.Length];
        if (!SessionEventText.IsLocalId(session) || StepSettleAsks.Read(_home, session) is not { } kept || StepSettleAsks.AnsweredId(_home, session) == kept.Id) return;
        Tee(kept);
    }

    private void Tee(StepSettleKept kept)
    {
        _teed[kept.Session] = (kept.Id, _bus.Clock.GetUtcNow());
        _bus.Tee(() => StepBusFacts.StepSettled(kept.Session, kept.Ask.Run, kept.Ask.Node));
    }

    /// <summary>
    /// The watcher's overflow net: every settle waiting in a box that no event told, teed and counted <c>found</c>; one the watcher told that
    /// stays open past <see cref="Sweep"/> (the runtime is not ready to take it) teed again and counted <c>retried</c>; all of them, counted
    /// <c>overflow</c>, where the watcher said it lost events.
    /// </summary>
    internal void TeeWaiting(bool overflow = false)
    {
        try
        {
            var now = _bus.Clock.GetUtcNow();
            foreach (var kept in StepSettleAsks.Waiting(_home))
            {
                var told = _teed.TryGetValue(kept.Session, out var was) && was.Id == kept.Id;
                var how = overflow ? BusMeasures.Overflow : !told ? BusMeasures.Found : now - was.At >= Sweep ? BusMeasures.Retried : null;
                if (how is null) continue;
                _bus.Measured(BusMeasures.Sweep, how, 1);
                Tee(kept);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _bus.Warn("run-step:sweep", "run.step-unswept", ("why", error.Message));
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(Sweep, _bus.Clock);
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false)) TeeWaiting();
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Whether the queue falls quiet within <paramref name="most"/>: every fact written is handled and acknowledged.</summary>
    internal Task<bool> QuietAsync(TimeSpan most, CancellationToken ct) => _queue.QuietAsync(most, ct);

    public async ValueTask DisposeAsync()
    {
        await _closing.CancelAsync().ConfigureAwait(false);
        if (_watcher is { } watcher)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        await _sweeping.ConfigureAwait(false);
        await _queue.DisposeAsync().ConfigureAwait(false);
        _closing.Dispose();
    }
}
