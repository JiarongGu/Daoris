namespace Daoris.Driver;

/// <summary>
/// The run's progression as a loop runs it: its consumer (<see cref="RunStepConsumer"/>) and the bus's queue that hands it the facts, over the
/// lease holder's log, started once link 8 is bound and stopped before it closes. Beside it, the sessions' settle boxes are heard as they are
/// written (a watch on the sessions folder), each settle waiting teed as <see cref="BusTypes.StepSettled"/>, which no look takes any more; a
/// sweep once a minute tees those still waiting (a lost event, a settle the runtime is not ready to take), and the start takes them first.
/// </summary>
internal sealed class RunStepRun : IAsyncDisposable
{
    /// <summary>How often the boxes are swept for a settle no event told: the net under the watch.</summary>
    public static readonly TimeSpan Sweep = TimeSpan.FromMinutes(1);

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
    public static Task<RunStepRun?> StartAsync(BusLog? bus, DriverWatch watch, ServiceClient service, string home, CancellationToken ct) =>
        bus is null
            ? Task.FromResult<RunStepRun?>(null)
            : StartAsync(bus, new RunStepConsumer(
                (unit, token) => watch.PassWholeAsync(unit, RunStepConsumer.Bound, token),
                async token => (await StepSettleAsks.TakeAsync(
                        new NodeRecordFiles(home), watch.Processes.Delegates.AgentOf,
                        async (session, text, key, said) => (await WordsHead.FollowAsync(session, at => service.SayAsync(at, text, ct: said, from: DaorisWords.From, key: key))
                            .ConfigureAwait(false)).Said,
                        bus.Clock.GetUtcNow, token, watch.RunWorkers).ConfigureAwait(false)).Count,
                () => [.. GraphRuns.Of(home).Read().Where(run => run.Ended is null).Select(run => RunUnits.IdOf(run.Run))],
                bus, (once, why) => bus.Warn("run-step:" + once, "run.step-unjudged", ("why", why))), home, ct);

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
            watcher.Error += (_, _) => TeeWaiting();
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

    private void Tee(StepSettleKept kept) => _bus.Tee(() => StepBusFacts.StepSettled(kept.Session, kept.Ask.Run, kept.Ask.Node));

    /// <summary>Every settle waiting in a box, teed.</summary>
    internal void TeeWaiting()
    {
        try
        {
            foreach (var kept in StepSettleAsks.Waiting(_home)) Tee(kept);
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
