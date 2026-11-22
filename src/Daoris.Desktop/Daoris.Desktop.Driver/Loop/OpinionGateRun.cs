namespace Daoris.Driver;

/// <summary>
/// The opinion's gate as a loop runs it: its consumer and the bus's queue that hands it the facts, over the lease holder's log, started
/// before the loop's first look and stopped after its last. The watch and the headless runs start it alike; a run that must return
/// (<c>drive --once</c>, <c>--until-idle</c>) also hears the ledger's moves through it, which the watch hears beside its looks, and drains
/// it before it ends.
/// </summary>
internal sealed class OpinionGateRun : IAsyncDisposable
{
    /// <summary>The longest a drain waits for its queue to fall quiet: the gate's own deadline, past which the watchdog's timers stand.</summary>
    public static readonly TimeSpan Most = BusTypes.OpinionOwed.Deadline.Within;

    private readonly BusLog _bus;
    private readonly IBusConsumer _gate;
    private readonly BusQueue _queue;
    private readonly string? _home;
    private readonly CancellationTokenSource _listening = new();
    private Task _ledger = Task.CompletedTask;

    private OpinionGateRun(BusLog bus, IBusConsumer gate, BusQueue queue, string? home)
    {
        _bus = bus;
        _gate = gate;
        _queue = queue;
        _home = home is null ? null : Path.GetFullPath(home);
        DriverConfigWrites.Wrote += Written;
    }

    /// <summary>
    /// The gate over <paramref name="bus"/>, each judgment by a driver <paramref name="driver"/> makes; none where no log is held here (a loop
    /// beside an instance). With <paramref name="listens"/>, the ledger's change stream is heard for its quests' moves until the run stops.
    /// </summary>
    public static Task<OpinionGateRun?> StartAsync(
        BusLog? bus, Func<Driver> driver, ServiceClient service, string home, CancellationToken ct, bool listens = false) =>
        bus is null
            ? Task.FromResult<OpinionGateRun?>(null)
            : StartAsync(bus, OpinionGateConsumer.Over(driver, service, home, bus), home, ct, listens ? service : null);

    /// <summary>
    /// The run over <paramref name="gate"/>: the standing choices this process writes under <paramref name="home"/> teed as the gate's fact,
    /// and, with <paramref name="ledger"/>, its change stream heard for the quests' moves.
    /// </summary>
    internal static async Task<OpinionGateRun?> StartAsync(BusLog bus, IBusConsumer gate, string? home, CancellationToken ct, ServiceClient? ledger = null)
    {
        try
        {
            var queue = await BusQueue.StartAsync(bus, [gate], new BusQueueOptions { Clock = bus.Clock }, ct: ct).ConfigureAwait(false);
            var run = new OpinionGateRun(bus, gate, queue, home);
            if (ledger is not null)
            {
                var moves = new QuestMoveReader(ledger.EveryQuestAsync);
                run._ledger = LedgerChanges.RunAsync(ledger, (_, families) => Heard(bus, families, moves), null, run._listening.Token);
            }

            return run;
        }
        catch
        {
            (gate as IDisposable)?.Dispose();
            throw;
        }
    }

    /// <summary>A change stream's wake teed as the gate's fact where it names the quests, naming what changed as <paramref name="moves"/> reads the list.</summary>
    internal static void Heard(BusLog? bus, string families, QuestMoveReader moves)
    {
        if (bus is null || !LedgerChanges.Names(families, LedgerChanges.Quests)) return;
        var named = families.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        _ = moves.TeeAsync(bus, named, CancellationToken.None);
    }

    /// <summary>A write of the standing choices under this run's home, teed as the gate's fact.</summary>
    private void Written(string file)
    {
        if (_home is not null && string.Equals(Path.GetFullPath(DriverConfig.HomeOf(file)), _home, StringComparison.OrdinalIgnoreCase)) _bus.Tee(OpinionBusFacts.ChoicesWritten);
    }

    /// <summary>
    /// Drain before a run ends: wait until the queue is quiet, then until every pass or delivery the gate began beside has ended, and again
    /// for the facts those ends wrote, until both stand. Whether it handled or ran anything, or a judgment moved what the plan reads, so a
    /// run looks again; a queue not quiet within <see cref="Most"/> is said once and left to the next start, which resumes from its cursor.
    /// </summary>
    /// <param name="pace">How often <paramref name="watched"/> is told while what the gate began runs: the watcher's look, which ends a quiet one as <c>--quiet-end</c> asks.</param>
    public async Task<bool> DrainAsync(RunningSessions runs, CancellationToken ct, TimeSpan pace, Action? watched = null)
    {
        var worked = false;
        var began = _bus.Clock.GetTimestamp();
        while (true)
        {
            await _bus.FlushAsync().ConfigureAwait(false);
            worked |= !_queue.Quiet;
            if (!await _queue.QuietAsync(Most, ct).ConfigureAwait(false)) return Undrained(worked, "its queue was not quiet");

            var settled = runs.SettledAsync();
            if (settled.IsCompleted) return worked || runs.FactsAhead;
            worked = true;
            while (!settled.IsCompleted)
            {
                if (_bus.Clock.GetElapsedTime(began) >= Most) return Undrained(worked, "what it began beside did not end");
                using (var paced = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    await Task.WhenAny(settled, Task.Delay(pace, _bus.Clock, paced.Token)).ConfigureAwait(false);
                    await paced.CancelAsync().ConfigureAwait(false);
                }

                ct.ThrowIfCancellationRequested();
                if (!settled.IsCompleted) watched?.Invoke();
            }
        }
    }

    /// <summary>A drain past its bound, said once and left to the next start, which resumes from the cursor and judges every owed opinion.</summary>
    private bool Undrained(bool worked, string why)
    {
        _bus.Warn("gate:drain", "opinion.gate-undrained", ("why", $"{why} within {Most.TotalMinutes:0} minutes"));
        return worked;
    }

    public async ValueTask DisposeAsync()
    {
        DriverConfigWrites.Wrote -= Written;
        await _listening.CancelAsync().ConfigureAwait(false);
        await _ledger.ConfigureAwait(false);
        await _queue.DisposeAsync().ConfigureAwait(false);
        (_gate as IDisposable)?.Dispose();
        _listening.Dispose();
    }
}
