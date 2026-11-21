namespace Daoris.Driver;

/// <summary>
/// The bus's watchdog, in shadow: at each tick it reads the armed timers due now and, for each, appends a
/// <c>bus.stalled</c> with its retry counted and the look's reading of the same subject beside it (<see cref="IBusLook"/>), then, past the
/// type's last retry, the <c>bus.give-up</c> it would apply with what it would hand to whom (<see cref="BusChains"/>), <c>applied</c> false.
/// Nothing it records changes a run, a session or a notice; nothing reads its facts but the Stalls view.
/// </summary>
/// <remarks>
/// <para><b>Durable.</b> The timers are rows the log's own writes arm, clear and move (<see cref="BusTimers"/>), read at the first tick
/// after a start, so a restart keeps every due time, and a stall moves its timer in its own write, so one kept is never made again.</para>
/// <para><b>Never twice.</b> A stall appended and not yet written is remembered for <see cref="Remembered"/>, so a tick before the write
/// does not make it again; one the store dropped is made again after that, its timer never having moved.</para>
/// <para>One for each log, started by the lease holder beside it and stopped before the log closes.</para>
/// </remarks>
public sealed class BusWatchdog : IAsyncDisposable
{
    /// <summary>How often the timers are read.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromSeconds(30);

    /// <summary>How long a stall appended and not yet seen written holds back the same one.</summary>
    public static readonly TimeSpan Remembered = TimeSpan.FromMinutes(5);

    /// <summary>The most timers one tick reads.</summary>
    public const int MostDue = 500;

    private readonly BusLog _bus;
    private readonly IBusLook _look;
    private readonly Dictionary<(string Entry, int Retry), DateTimeOffset> _made = [];
    private readonly SemaphoreSlim _ticking = new(1, 1);
    private readonly CancellationTokenSource _closing = new();
    private Task _loop = Task.CompletedTask;

    public BusWatchdog(BusLog bus, IBusLook look)
    {
        _bus = bus;
        _look = look;
    }

    /// <summary>A watchdog over <paramref name="bus"/>, ticking every <paramref name="every"/> (<see cref="Every"/>) on the log's clock, the first an interval after its start.</summary>
    public static BusWatchdog Start(BusLog bus, IBusLook look, TimeSpan? every = null)
    {
        var watchdog = new BusWatchdog(bus, look);
        watchdog._loop = Task.Run(() => watchdog.LoopAsync(every ?? Every, watchdog._closing.Token));
        return watchdog;
    }

    /// <summary>One tick: each timer due now stalled once for its retry, and given up past its last; how many facts it appended.</summary>
    public async Task<int> TickAsync(CancellationToken ct = default)
    {
        await _ticking.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_bus.Writing) return 0;
            var now = _bus.Clock.GetUtcNow();
            foreach (var old in _made.Where(pair => now - pair.Value >= Remembered).Select(pair => pair.Key).ToList()) _made.Remove(old);
            var appended = 0;
            var due = _bus.Timers(now, MostDue);
            var undeclared = due.Where(timer => BusTypes.Of(timer.Kind) is null).Select(timer => timer.Entry).ToList();
            if (undeclared.Count > 0) _bus.DropTimers(undeclared);
            foreach (var timer in due)
            {
                if (BusTypes.Of(timer.Kind) is not { } type) continue;
                var retry = timer.Retry + 1;
                if (_made.ContainsKey((timer.Entry, retry))) continue;
                var reading = await ReadAsync(timer, ct).ConfigureAwait(false);
                var last = timer.Retry >= type.Retries;
                var drafts = new List<BusDraft>
                {
                    new(BusTypes.Stalled, timer.Subject)
                    {
                        Effect = $"stall:{timer.Entry}:{retry}",
                        Causes = [timer.Entry],
                        Payload =
                        [
                            ("entry", timer.Entry), ("kind", timer.Kind), ("obliged", timer.Obliged), ("since", timer.Armed), ("due", timer.Due),
                            ("retry", (long)retry), ("retries", (long)type.Retries), ("next", last ? null : timer.Due + type.Deadline.Within),
                            ("look", reading.State), ("says", reading.Says),
                        ],
                    },
                };
                if (last)
                {
                    var (hands, to) = BusChains.Of(type, timer.Obliged);
                    drafts.Add(new BusDraft(BusTypes.GiveUp, timer.Subject)
                    {
                        Effect = $"give-up:{timer.Entry}",
                        Causes = [timer.Entry],
                        Payload =
                        [
                            ("entry", timer.Entry), ("kind", timer.Kind), ("consequence", type.GiveUp), ("applied", false), ("hands", hands),
                            ("to", to), ("since", timer.Armed),
                        ],
                    });
                }

                if (!_bus.AppendAll(drafts)) continue;
                appended += drafts.Count;
                _made[(timer.Entry, retry)] = now;
            }

            return appended;
        }
        finally
        {
            _ticking.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _closing.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _closing.Dispose();
        _ticking.Dispose();
    }

    private async Task<BusLookReading> ReadAsync(BusTimer timer, CancellationToken ct)
    {
        try
        {
            return await _look.ReadAsync(timer, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException)
        {
            return new BusLookReading(BusLookStates.Unknown, "unread");
        }
    }

    private async Task LoopAsync(TimeSpan every, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(every, _bus.Clock, ct).ConfigureAwait(false);
                await TickAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // A tick that failed is tried again at the next, its timers armed still; said once in the machine log.
                _bus.Warn("watchdog:tick", "bus.watchdog-failed", ("why", error.Message));
            }
        }
    }
}
