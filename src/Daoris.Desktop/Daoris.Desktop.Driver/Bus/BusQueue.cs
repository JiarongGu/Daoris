using System.Diagnostics;
using System.Threading.Channels;

namespace Daoris.Driver;

/// <summary>
/// One durable named consumer of the bus: its subscription (its name and the declared types it reads), the obliged
/// handler it stands for, and its handling of one entry. Its cursor and what it parked are the queue's, kept in the log's store.
/// </summary>
public interface IBusConsumer
{
    /// <summary>Its durable name and the declared types it reads (<see cref="BusTypes.Subscribe"/>).</summary>
    BusSubscription Subscription { get; }

    /// <summary>The obliged handler it is (<see cref="BusTypes.OpinionGate"/>): an acknowledgement that ends clears only timers obliged to it.</summary>
    string Obliged { get; }

    /// <summary>
    /// Handle one entry: what it says is done before this returns, so the acknowledgement that follows means handled, never received. A
    /// throw is said once and the entry tried again with backoff; it holds its subject alone.
    /// </summary>
    Task<BusHandled> HandleAsync(BusEnvelope entry, CancellationToken ct);

    /// <summary>Told once the consumer resumed from its cursor, its parked and missed entries handed back: where it rebuilds its view.</summary>
    Task ResumedAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>Whether entries of <paramref name="kind"/> waiting in a row on one subject are handed together: one handling stands for them all.</summary>
    bool Folds(string kind) => false;

    /// <summary>Handle a row of folded entries, oldest first (one where it folds none): all stay open until the one result, so a throw loses none.</summary>
    Task<BusHandled> HandleAsync(IReadOnlyList<BusEnvelope> row, CancellationToken ct) => HandleAsync(row[^1], ct);

    void Attached(BusConsumerHost host)
    {
    }

    int? SubjectsAtOnce => null;

    DateTimeOffset? NotBefore(BusEnvelope entry) => null;
}

public sealed record BusConsumerHost(BusLog Log, BusQueueOptions Options, CancellationToken Closing, Action<string> EndLate, Func<bool> Owned, Func<long, string, string, BusEnvelope?> Find);

/// <summary>What a handled entry's acknowledgement says: whether it also ends the entry's obligation (<see cref="BusEnd.Acknowledged"/>).</summary>
public sealed record BusHandled(bool Ends)
{
    /// <summary>Handled, and the acknowledgement ends the entry's obligation where its type declares an acknowledgement among its ends.</summary>
    public static readonly BusHandled Ended = new(true);

    /// <summary>Handled; the obligation stays for the later fact that ends it.</summary>
    public static readonly BusHandled Judged = new(false);
}

/// <summary>The log would not keep one entry's retry: the consumer counts and gives that entry up, so the queue hands the row again and charges the lane head no try.</summary>
public sealed class BusEntryNotKeptException(string entry, int refusals) : IOException($"the log did not keep the retry of entry {entry}")
{
    public string Entry { get; } = entry;

    public int Refusals { get; } = refusals;
}

/// <summary>One entry of a frame and the consumers it goes to.</summary>
public sealed record BusDelivery(BusEnvelope Entry, IReadOnlyList<string> To);

/// <summary>
/// What the queue hands its middleware and then its consumers at once: the entries written since the last frame, each with
/// the consumers it goes to, in the log's order. A replay frame carries one consumer's entries read back from its cursor at its start.
/// </summary>
public sealed record BusFrame(long Number, IReadOnlyList<BusDelivery> Deliveries, bool Replay = false)
{
    /// <summary>How many hand-ons it carries: each entry once for each consumer it goes to.</summary>
    public int Count => Deliveries.Sum(delivery => delivery.To.Count);

    /// <summary>The frame keeping the deliveries <paramref name="keep"/> keeps.</summary>
    public BusFrame Where(Func<BusDelivery, bool> keep) => this with { Deliveries = [.. Deliveries.Where(keep)] };

    /// <summary>The frame with each delivery mapped: its entry projected, its consumers narrowed, or (null) dropped.</summary>
    public BusFrame Select(Func<BusDelivery, BusDelivery?> map) => this with { Deliveries = [.. Deliveries.Select(map).OfType<BusDelivery>().Where(each => each.To.Count > 0)] };
}

/// <summary>
/// One step between a frame and the consumers' queues: it passes the frame on (<paramref name="next"/>), transforms it, drops deliveries
/// (counted by the queue) or observes it. Registered in one place (<see cref="BusMiddlewares"/>), run in that order; one that throws is
/// counted and said once, and the frame goes on as handed. Middleware runs for live and replay frames alike, and must be
/// safe to run on two frames at once.
/// </summary>
public interface IBusMiddleware
{
    /// <summary>Its name, as its counts and its said failure name it.</summary>
    string Name { get; }

    ValueTask HandleAsync(BusFrame frame, Func<BusFrame, ValueTask> next, CancellationToken ct);
}

/// <summary>The one place the delivery path's middleware is registered, in its order.</summary>
public static class BusMiddlewares
{
    /// <summary>The lease holder's chain: the delivery measures (the log's measures, their delivery half).</summary>
    public static IReadOnlyList<IBusMiddleware> Of(BusLog log) => [new BusDeliveryMeasures(log)];
}

/// <summary>
/// The measures of what is delivered by the day: the frames, and each kind's hand-ons, so
/// <c>daoris bus stats</c> reads a kind's appends beside its deliveries. It observes and passes every frame on unchanged.
/// </summary>
public sealed class BusDeliveryMeasures(BusLog log) : IBusMiddleware
{
    public string Name => "measures";

    public ValueTask HandleAsync(BusFrame frame, Func<BusFrame, ValueTask> next, CancellationToken ct)
    {
        log.Measured(BusMeasures.Delivered, frame.Replay ? BusMeasures.Replayed : BusMeasures.Frames, 1);
        foreach (var delivery in frame.Deliveries) log.Measured(BusMeasures.Delivered, delivery.Entry.Kind, delivery.To.Count);
        return next(frame);
    }
}

/// <summary>What a queue is handed beside its log: its pace, its retries and how many subjects of one consumer it handles at once.</summary>
public sealed record BusQueueOptions
{
    /// <summary>The least time between two frames: at most 60 a second.</summary>
    public TimeSpan Frame { get; init; } = TimeSpan.FromSeconds(1.0 / 60);

    /// <summary>The wait before a thrown entry's first retry, doubled at each until <see cref="RetryMost"/>.</summary>
    public TimeSpan RetryFirst { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>The longest wait between two retries.</summary>
    public TimeSpan RetryMost { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>How many subjects of one consumer are handled at once; a subject's own entries go one at a time, in order.</summary>
    public int SubjectsAtOnce { get; init; } = 4;

    /// <summary>The clock frames are paced and retries waited on, handed by the composition root.</summary>
    public required TimeProvider Clock { get; init; }
}

/// <summary>
/// The bus's durable queue, in process: each write of the log wakes it with the entries it kept (no polling), which
/// it hands, in frames of at most 60 a second, through its middleware to each consumer subscribed to their types. Delivery is at least
/// once, ordered per subject and parallel across subjects; a consumer's acknowledgement is written after its handling, in one transaction
/// with its cursor and the timers it ends, so <i>handled</i> is a fact. A consumer resumes from its cursor at a start, its parked entries
/// first. One that throws is said once, its entry parked and tried again with backoff while its other subjects go on; past its type's
/// retries the queue appends the give-up it hands to the holder chain and goes on. Its ownership carries a fenced epoch: a second claim
/// raises it, and an acknowledgement of an older epoch is refused, so two processes never both execute a consumer.
/// </summary>
public sealed class BusQueue : IAsyncDisposable
{
    private readonly BusLog _log;
    private readonly IBusQueueStore _store;
    private readonly IReadOnlyList<IBusMiddleware> _middleware;
    private readonly BusQueueOptions _options;
    private readonly Dictionary<string, BusConsumerRun> _runs = new(StringComparer.Ordinal);
    private readonly Channel<IReadOnlyList<BusEnvelope>> _written = Channel.CreateUnbounded<IReadOnlyList<BusEnvelope>>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _closing = new();
    private long _frames;
    private long _unframed;
    private Task _pump = Task.CompletedTask;

    private BusQueue(BusLog log, IBusQueueStore store, IReadOnlyList<IBusMiddleware> middleware, BusQueueOptions options)
    {
        _log = log;
        _store = store;
        _middleware = middleware;
        _options = options;
    }

    /// <summary>
    /// The queue over <paramref name="log"/>, each consumer claimed (its epoch raised) and resumed from its cursor, its frames then
    /// pumped from each write. A consumer's claim the store refuses is said and leaves it out; the others run.
    /// </summary>
    /// <exception cref="InvalidOperationException">The log's store keeps no queue.</exception>
    public static async Task<BusQueue> StartAsync(
        BusLog log, IReadOnlyList<IBusConsumer> consumers, BusQueueOptions options, IReadOnlyList<IBusMiddleware>? middleware = null,
        CancellationToken ct = default)
    {
        var store = log.Queue ?? throw new InvalidOperationException("the bus log's store keeps no queue.");
        var queue = new BusQueue(log, store, middleware ?? BusMiddlewares.Of(log), options);
        // Woken from here on, so nothing written while the consumers resume is missed: a live frame waits for its consumer's replay.
        log.Written += queue.OnWritten;
        foreach (var consumer in consumers)
        {
            var name = consumer.Subscription.Consumer;
            try
            {
                var claim = store.Claim(name, [.. consumer.Subscription.Types.Select(type => type.Kind)], log.WriterRead, options.Clock.GetUtcNow());
                var run = new BusConsumerRun(queue, consumer, claim);
                queue._runs[name] = run;
                consumer.Attached(new BusConsumerHost(log, options, queue._closing.Token, run.EndLate, run.Holds, run.Find));
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                log.Warn($"queue:claim:{name}", "bus.consumer-unclaimed", ("consumer", name), ("why", error.Message));
            }
        }

        queue._pump = Task.Run(() => queue.PumpAsync(queue._closing.Token));
        foreach (var run in queue._runs.Values) await run.ResumeAsync(ct).ConfigureAwait(false);
        return queue;
    }

    /// <summary>A consumer's run, by its name: what a test reads of its cursor and its parked entries.</summary>
    internal BusConsumerRun? Run(string consumer) => _runs.GetValueOrDefault(consumer);

    internal BusLog Log => _log;

    internal IBusQueueStore Store => _store;

    internal BusQueueOptions Options => _options;

    internal CancellationToken Closing => _closing.Token;

    public async ValueTask DisposeAsync()
    {
        _log.Written -= OnWritten;
        _written.Writer.TryComplete();
        await _closing.CancelAsync().ConfigureAwait(false);
        try
        {
            await _pump.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        foreach (var run in _runs.Values) await run.StopAsync().ConfigureAwait(false);
        _closing.Dispose();
    }

    private void OnWritten(IReadOnlyList<BusEnvelope> entries)
    {
        Interlocked.Increment(ref _unframed);
        if (!_written.Writer.TryWrite(entries)) Interlocked.Decrement(ref _unframed);
    }

    /// <summary>
    /// Wait until nothing written waits to be handled: what the log holds written, each write framed, and every consumer's entries
    /// acknowledged or parked, its start's rebuild ended. A run that must return drains it before it ends; false where
    /// <paramref name="most"/> ran out first.
    /// </summary>
    public async Task<bool> QuietAsync(TimeSpan most, CancellationToken ct)
    {
        await _log.FlushAsync().ConfigureAwait(false);
        var began = _options.Clock.GetTimestamp();
        while (!Quiet)
        {
            if (_options.Clock.GetElapsedTime(began) >= most) return false;
            await Task.Delay(Settle, _options.Clock, ct).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Whether every write it is told of is framed and every consumer is quiet (<see cref="BusConsumerRun.Quiet"/>), at this moment.</summary>
    public bool Quiet => Volatile.Read(ref _unframed) == 0 && _runs.Values.All(run => run.Quiet);

    /// <summary>The step a drain waits between two readings of whether its queue is quiet.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(20);

    /// <summary>The frames: whatever the writes kept since the last, routed to the consumers of their types, at most one each <see cref="BusQueueOptions.Frame"/>.</summary>
    private async Task PumpAsync(CancellationToken ct)
    {
        var reader = _written.Reader;
        while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            var began = _options.Clock.GetTimestamp();
            var entries = new List<BusEnvelope>();
            var batches = 0;
            while (reader.TryRead(out var batch))
            {
                entries.AddRange(batch);
                batches++;
            }

            var deliveries = entries
                .Select(entry => new BusDelivery(entry, [.. _runs.Values.Where(run => run.Reads(entry.Kind)).Select(run => run.Name)]))
                .Where(delivery => delivery.To.Count > 0)
                .ToList();
            try
            {
                if (deliveries.Count > 0) await ChainAsync(new BusFrame(Interlocked.Increment(ref _frames), deliveries), ct).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Add(ref _unframed, -batches);
            }

            var wait = _options.Frame - _options.Clock.GetElapsedTime(began);
            if (wait > TimeSpan.Zero) await Task.Delay(wait, _options.Clock, ct).ConfigureAwait(false);
        }
    }

    /// <summary>A replay frame of one consumer's entries, through the same chain as every frame.</summary>
    internal Task ReplayAsync(string consumer, IReadOnlyList<BusEnvelope> entries, CancellationToken ct) =>
        ChainAsync(new BusFrame(Interlocked.Increment(ref _frames), [.. entries.Select(entry => new BusDelivery(entry, [consumer]))], Replay: true), ct).AsTask();

    /// <summary>
    /// The frame through each middleware in order, then into the consumers' queues. A middleware that throws is counted and said once,
    /// and the frame goes on as handed; one that called on first has sent it on already. What a middleware drops is counted
    /// against its name.
    /// </summary>
    internal ValueTask ChainAsync(BusFrame frame, CancellationToken ct) => StepAsync(0, frame, ct);

    private async ValueTask StepAsync(int at, BusFrame frame, CancellationToken ct)
    {
        if (at == _middleware.Count)
        {
            foreach (var delivery in frame.Deliveries)
            {
                foreach (var consumer in delivery.To)
                {
                    if (_runs.TryGetValue(consumer, out var run)) run.Offer(delivery.Entry, frame.Replay);
                }
            }

            return;
        }

        var middleware = _middleware[at];
        var on = false;
        try
        {
            await middleware.HandleAsync(frame, next =>
            {
                on = true;
                var dropped = frame.Count - next.Count;
                if (dropped > 0) _log.Measured(BusMeasures.Middleware, $"{middleware.Name}:dropped", dropped);
                return StepAsync(at + 1, next, ct);
            }, ct).ConfigureAwait(false);
            if (!on) _log.Measured(BusMeasures.Middleware, $"{middleware.Name}:dropped", frame.Count);
        }
        catch (Exception error) when (error is not OutOfMemoryException and not OperationCanceledException)
        {
            _log.Measured(BusMeasures.Middleware, $"{middleware.Name}:threw", 1);
            _log.Warn($"queue:middleware:{middleware.Name}", "bus.middleware-threw", ("middleware", middleware.Name), ("why", error.Message));
            if (!on) await StepAsync(at + 1, frame, ct).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// One consumer's queue: what it has been handed and not yet acknowledged, its subjects' lanes (one entry at a time each, in order, a few
/// subjects at once) and its cursor, the highest order below which every entry is acknowledged or parked.
/// </summary>
internal sealed class BusConsumerRun
{
    private readonly BusQueue _queue;
    private readonly IBusConsumer _consumer;
    private readonly HashSet<string> _kinds;
    private readonly Lock _gate = new();
    private readonly SortedSet<long> _open = [];
    private readonly Dictionary<string, Queue<(BusEnvelope Entry, int Tries)>> _lanes = new(StringComparer.Ordinal);
    private readonly HashSet<long> _parked = [];
    private readonly HashSet<long> _deferred = [];
    private readonly List<BusEnvelope> _waiting = [];
    private readonly SemaphoreSlim _atOnce;
    private readonly List<Task> _running = [];
    private readonly long _epoch;
    private long _highest;
    private long _cursor;
    private bool _live;
    private bool _fenced;
    private Task? _resumed;
    private IReadOnlyList<BusParkedEntry> _claimedParked;

    public BusConsumerRun(BusQueue queue, IBusConsumer consumer, BusClaim claim)
    {
        _queue = queue;
        _consumer = consumer;
        _kinds = [.. consumer.Subscription.Types.Select(type => type.Kind)];
        _atOnce = new SemaphoreSlim(Math.Max(1, consumer.SubjectsAtOnce ?? queue.Options.SubjectsAtOnce));
        _epoch = claim.Epoch;
        _cursor = claim.Cursor;
        _highest = claim.Cursor;
        _claimedParked = claim.Parked;
    }

    public string Name => _consumer.Subscription.Consumer;

    /// <summary>Its cursor as this process last wrote it.</summary>
    public long Cursor
    {
        get
        {
            lock (_gate) return _cursor;
        }
    }

    /// <summary>Whether a later claim fenced it off: it handles nothing more.</summary>
    public bool Fenced => Volatile.Read(ref _fenced);

    /// <summary>Whether it holds nothing handed and unhandled (a parked entry waits durably) and its start's rebuild ended.</summary>
    public bool Quiet
    {
        get
        {
            lock (_gate) return Fenced || (_open.Count == _deferred.Count && _waiting.Count == 0 && _resumed is { IsCompleted: true });
        }
    }

    public bool Reads(string kind) => _kinds.Contains(kind);

    /// <summary>Its parked entries first, each with the tries it had, then every entry of its types after its cursor, then the live frames.</summary>
    public async Task ResumeAsync(CancellationToken ct)
    {
        foreach (var parked in _claimedParked)
        {
            lock (_gate)
            {
                // Parked, so the replay below hands it no second time; the cursor does not move past what lies between.
                _parked.Add(parked.Entry.Seq);
                Enqueue(parked.Entry, parked.Tries);
            }
        }

        _claimedParked = [];
        var after = Cursor;
        var kinds = _kinds.ToList();
        while (true)
        {
            var read = _queue.Store.Read(after, kinds, 500);
            if (read.Count == 0) break;
            after = read[^1].Seq;
            await _queue.ReplayAsync(Name, read, ct).ConfigureAwait(false);
        }

        lock (_gate)
        {
            _live = true;
            foreach (var entry in _waiting) Admit(entry);
            _waiting.Clear();
        }

        // Beside, so a consumer's rebuild never holds its host's start; stopped with its lanes.
        var resumed = Task.Run(async () =>
        {
            try
            {
                await _consumer.ResumedAsync(_queue.Closing).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OutOfMemoryException and not OperationCanceledException)
            {
                _queue.Log.Warn($"queue:resumed:{Name}", "bus.consumer-threw", ("consumer", Name), ("kind", "resumed"), ("why", error.Message));
            }
        }, CancellationToken.None);
        lock (_gate)
        {
            _running.Add(resumed);
            _resumed = resumed;
        }
    }

    /// <summary>An entry handed by a frame: a live one waits until the replay is done; one at or below what it holds is not handed again.</summary>
    public void Offer(BusEnvelope entry, bool replay)
    {
        lock (_gate)
        {
            if (!replay && !_live)
            {
                _waiting.Add(entry);
                return;
            }

            Admit(entry);
        }
    }

    public async Task StopAsync()
    {
        Task[] running;
        lock (_gate) running = [.. _running];
        try
        {
            await Task.WhenAll(running).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Admit(BusEnvelope entry)
    {
        if (entry.Seq <= _highest || _parked.Contains(entry.Seq) || _fenced) return;
        _highest = entry.Seq;
        _open.Add(entry.Seq);
        Enqueue(entry, 0);
    }

    /// <summary>Into its subject's lane; a lane not running starts. Under the gate.</summary>
    private void Enqueue(BusEnvelope entry, int tries)
    {
        if (_consumer.NotBefore(entry) is { } at && at > _queue.Options.Clock.GetUtcNow() && !_fenced)
        {
            // Not handed before its time: held here, off every lane, and offered again then; a restart reads its time from the entry again.
            _deferred.Add(entry.Seq);
            _running.Add(Task.Run(() => OfferAtAsync(entry, tries, at)));
            _running.RemoveAll(each => each.IsCompleted);
            return;
        }

        if (_lanes.TryGetValue(entry.Subject, out var lane))
        {
            lane.Enqueue((entry, tries));
            return;
        }

        _lanes[entry.Subject] = new Queue<(BusEnvelope, int)>([(entry, tries)]);
        var task = Task.Run(() => LaneAsync(entry.Subject, _queue.Closing));
        _running.Add(task);
        _running.RemoveAll(each => each.IsCompleted);
    }

    private async Task OfferAtAsync(BusEnvelope entry, int tries, DateTimeOffset at)
    {
        try
        {
            // Reached late, the time already past: offered at once, never a negative wait (one of -1 ms waits forever, a lower one throws).
            var wait = at - _queue.Options.Clock.GetUtcNow();
            if (wait > TimeSpan.Zero) await Task.Delay(wait, _queue.Options.Clock, _queue.Closing).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_gate)
        {
            _deferred.Remove(entry.Seq);
            if (!_fenced) Enqueue(entry, tries);
        }
    }

    /// <summary>One subject's entries, one at a time in order, until its lane is empty.</summary>
    private async Task LaneAsync(string subject, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            BusEnvelope entry;
            int tries;
            List<BusEnvelope> row;
            lock (_gate)
            {
                if (_fenced || !_lanes.TryGetValue(subject, out var lane) || lane.Count == 0)
                {
                    _lanes.Remove(subject);
                    return;
                }

                (entry, tries) = lane.Peek();
                // A row of a kind it folds is handed together and stays in the lane until its result is known.
                row = [entry];
                if (_consumer.Folds(entry.Kind)) row.AddRange(lane.Skip(1).TakeWhile(next => next.Entry.Kind == entry.Kind).Select(next => next.Entry));
            }

            // Its ownership read before it acts: a later claim (another process's) fences this one off, so the two never both execute it.
            if (!Owned()) return;

            BusHandled? handled = null;
            Exception? thrown = null;
            await _atOnce.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                handled = await _consumer.HandleAsync(row, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                thrown = error;
            }
            finally
            {
                _atOnce.Release();
            }

            if (handled is not null)
            {
                foreach (var each in row)
                {
                    if (!Acknowledge(each, handled.Ends)) return;
                }

                Dequeue(subject, row.Count);
                continue;
            }

            var refused = thrown as BusEntryNotKeptException;
            if (refused is null) tries++;
            var type = BusTypes.Of(entry.Kind);
            _queue.Log.Measured(BusMeasures.Consumer, $"{Name}:threw", 1);
            _queue.Log.Warn($"queue:threw:{Name}:{entry.Kind}", "bus.consumer-threw", ("consumer", Name), ("kind", entry.Kind), ("why", thrown!.Message));
            if (refused is null && (type is null || tries > type.Retries))
            {
                // Past its budget: handed to its holder chain, said on the log and on Stalls, and its subject goes on.
                GiveUp(entry, type, tries);
                if (!Acknowledge(entry, ends: false)) return;
                Dequeue(subject);
                continue;
            }

            if (!Park(subject, tries)) return;
            var wait = TimeSpan.FromTicks(Math.Min(_queue.Options.RetryMost.Ticks, _queue.Options.RetryFirst.Ticks << Math.Min(20, Math.Max(0, (refused?.Refusals ?? tries) - 1))));
            try
            {
                await Task.Delay(wait, _queue.Options.Clock, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Whether its epoch is still the kept one; a store that cannot say this moment leaves it to the acknowledgement's fence.</summary>
    private bool Owned()
    {
        try
        {
            if (_queue.Store.Consumer(Name) is not { } kept || kept.Epoch == _epoch) return true;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return true;
        }

        Volatile.Write(ref _fenced, true);
        _queue.Log.Warn($"queue:fenced:{Name}", "bus.consumer-fenced", ("consumer", Name), ("why", "claimed again"));
        return false;
    }

    private void Dequeue(string subject, int count = 1)
    {
        lock (_gate)
        {
            if (!_lanes.TryGetValue(subject, out var lane)) return;
            for (var each = 0; each < count && lane.Count > 0; each++) lane.Dequeue();
        }
    }

    /// <summary>Its cursor now: below the lowest entry handed and neither acknowledged nor parked. Under the gate.</summary>
    private long CursorLocked() => _open.Count == 0 ? _highest : _open.Min - 1;

    /// <summary>The acknowledgement, its cursor and the timers it ends in one write; false where the store fenced this run off.</summary>
    private bool Acknowledge(BusEnvelope entry, bool ends)
    {
        long cursor;
        bool parked;
        lock (_gate)
        {
            _open.Remove(entry.Seq);
            parked = _parked.Remove(entry.Seq);
            cursor = CursorLocked();
        }

        var ended = ends && BusTypes.Of(entry.Kind) is { } type && BusTypes.Ends(type).Contains(BusEnd.Acknowledged) ? _consumer.Obliged : null;
        return Written(() => _queue.Store.Acknowledge(new BusAck(Name, _epoch, cursor, entry) { Ends = ended, Unpark = parked, Writer = _queue.Log.WriterRead }), cursor) is not false;
    }

    /// <summary>
    /// The lane's head and every entry behind it parked durably with their tries, so the cursor passes them while they wait; false where the
    /// store fenced this run off. A park the store did not keep leaves the entries it would have parked open, so no cursor passes them.
    /// </summary>
    private bool Park(string subject, int tries)
    {
        List<(long Seq, int Tries)> rows;
        List<long> fresh = [];
        long cursor;
        lock (_gate)
        {
            if (!_lanes.TryGetValue(subject, out var lane) || lane.Count == 0) return true;
            var head = lane.Dequeue();
            var rest = lane.ToList();
            lane.Clear();
            lane.Enqueue((head.Entry, tries));
            foreach (var each in rest) lane.Enqueue(each);
            rows = [.. lane.Select(each => (each.Entry.Seq, each.Tries))];
            foreach (var (seq, _) in rows)
            {
                if (_open.Remove(seq) && _parked.Add(seq)) fresh.Add(seq);
            }

            cursor = CursorLocked();
        }

        var written = Written(() => _queue.Store.Park(new BusPark(Name, _epoch, cursor, rows) { Writer = _queue.Log.WriterRead }), cursor);
        if (written is null)
        {
            lock (_gate)
            {
                foreach (var seq in fresh)
                {
                    _parked.Remove(seq);
                    _open.Add(seq);
                }
            }
        }

        return written is not false;
    }

    /// <summary>
    /// A write of this run's, its cursor kept once written (true); a fence or a lost lease stops it, said once (false); a busy store is said
    /// and passed, nothing kept (null).
    /// </summary>
    private bool? Written(Action write, long cursor)
    {
        try
        {
            write();
            lock (_gate) _cursor = Math.Max(_cursor, cursor);
            return true;
        }
        catch (Exception error) when (error is BusFencedException or BusNotWriterException)
        {
            Volatile.Write(ref _fenced, true);
            _queue.Log.Warn($"queue:fenced:{Name}", "bus.consumer-fenced", ("consumer", Name), ("why", error.Message));
            return false;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Not kept: the entry is handled again after a restart (at least once), never lost.
            _queue.Log.Warn($"queue:unwritten:{Name}", "bus.consumer-unwritten", ("consumer", Name), ("why", error.Message));
            Debug.WriteLine($"bus queue: {Name}'s write failed ({error.Message})");
            return null;
        }
    }

    /// <summary>The give-up past an entry's retries, its consequence its type's and what it hands to whom (<see cref="BusChains"/>).</summary>
    private void GiveUp(BusEnvelope entry, BusType? type, int tries) => GiveUp(_queue.Log, Name, _consumer.Obliged, entry.Id, entry.Kind, entry.Subject, entry.At, type, tries, entry.Correlation);

    public bool Holds() => !Fenced && Owned();

    public void EndLate(string id)
    {
        if (!Holds()) return;
        long cursor;
        lock (_gate) cursor = _cursor;
        var stub = new BusEnvelope(id, 0, "", "", "", null, null, null, [], [], DateTimeOffset.UnixEpoch);
        Written(() => _queue.Store.Acknowledge(new BusAck(Name, _epoch, cursor, stub) { Ends = _consumer.Obliged, Writer = _queue.Log.WriterRead }), cursor);
    }

    public BusEnvelope? Find(long seq, string kind, string id) => _queue.Store.Read(seq - 1, [kind], 1).FirstOrDefault(each => each.Seq == seq && each.Id == id);

    internal static void GiveUp(BusLog log, string name, string obliged, string id, string kind, string subject, DateTimeOffset since, BusType? type, int tries, string? correlation = null)
    {
        if (type is null) return;
        var (hands, to) = BusChains.Of(type, obliged);
        log.Append(new BusDraft(BusTypes.GiveUp, subject)
        {
            Effect = $"give-up:{name}:{id}",
            Causes = [id],
            Correlation = correlation,
            Payload =
            [
                ("entry", id), ("kind", kind), ("consequence", type.GiveUp), ("applied", false), ("hands", hands), ("to", to),
                ("since", since), ("tries", (long)tries), ("consumer", name),
            ],
        });
    }
}
