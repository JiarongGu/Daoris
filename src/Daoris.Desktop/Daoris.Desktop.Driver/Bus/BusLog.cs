using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>What a log is handed beside its store: its clock, its machine log, its pace, and whether this process is its writer.</summary>
public sealed record BusLogOptions
{
    /// <summary>The clock its entries are stamped and its flushes paced by: the machine's, handed by the root, or a test's.</summary>
    public required TimeProvider Clock { get; init; }

    /// <summary>Where a refusal is said, once for each reason; null says none.</summary>
    public MachineLog? Log { get; init; }

    /// <summary>The least time between two flushes of plain entries.</summary>
    public TimeSpan Interval { get; init; } = BusLog.Interval;

    /// <summary>How often the measures alone are written while no entry is: the looks' stage times between entries.</summary>
    public TimeSpan MeasureEvery { get; init; } = BusLog.MeasureEvery;

    /// <summary>
    /// Whether this process holds the home's lease (null: its file could not be read this moment): only the lease holder writes the log.
    /// Asked at most every <see cref="BusLog.WriterEvery"/> on the writers' path, and afresh before each write.
    /// </summary>
    public Func<bool?> Writer { get; init; } = () => true;

    /// <summary>The home whose log this is, where writers that hold no log find it (<see cref="BusLog.Of"/>); null registers none.</summary>
    public string? Home { get; init; }
}

/// <summary>What a log has done since it opened, in memory: what a test and the stats read.</summary>
/// <param name="Appended">The entries taken.</param>
/// <param name="Flushes">The writes that carried entries.</param>
/// <param name="Missed">The entries refused, by reason (<see cref="BusMisses"/>).</param>
/// <param name="Retried">The batches a busy store sent back to wait for the next try.</param>
public sealed record BusTally(long Appended, long Flushes, IReadOnlyDictionary<string, long> Missed, long Retried = 0)
{
    /// <summary>The payloads' fields and values dropped at emit, their facts kept, by reason (<see cref="BusDrops"/>).</summary>
    public IReadOnlyDictionary<string, long> Dropped { get; init; } = new Dictionary<string, long>(StringComparer.Ordinal);
}

/// <summary>
/// The bus's log in its first form: an envelope appended in memory, given its id, order and time at once, and
/// written behind by a loop of its own: plain entries at most about once every <see cref="Interval"/> while any wait, a commit-class one
/// at once, grouped with whatever else is due. While nothing is appended nothing is written; the measures alone at most every
/// <see cref="MeasureEvery"/> while a look's stages have been timed since the last write.
/// </summary>
/// <remarks>
/// <para><b>A shadow append never fails the write it rides beside</b>: <see cref="Append"/> never throws and never waits on the store. A
/// refusal (the store's, an undeclared type, a subject that is no id, a process not the lease holder) is counted by reason and said once
/// as <c>bus.refused</c>; a payload's value its type does not declare is dropped, the fact kept (<see cref="BusType.Admit"/>), and said
/// once as <c>bus.dropped</c>. A busy store's batch goes back ahead of the next, tried again after <see cref="RetryAfter"/>, at most
/// <see cref="MostRetries"/> times running, then dropped. The lease is asked inside each write's transaction, and the write seats its
/// batch after the store's newest order, what waits moving with it, so a log that regained the lease goes on after the other holder's.</para>
/// <para><b>Measured</b> into the store's measures by the day (UTC): entries by kind, each kind's handling time where its writer knows it,
/// the flushes' count, entries and time, the rows' own bytes and the pages each write put on disk, retention's cuts, the misses, and each
/// look stage's time (<see cref="Staged"/>). <c>daoris bus stats</c> reads them.</para>
/// <para>One log for each home, opened by the process that took the home's lease, after it took it, and closed before it lets it go.</para>
/// </remarks>
public sealed class BusLog : IAsyncDisposable
{
    /// <summary>The least time between two flushes of plain entries.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>How often the measures alone are written while entries are not.</summary>
    public static readonly TimeSpan MeasureEvery = TimeSpan.FromMinutes(15);

    /// <summary>The most entries kept unflushed; past it an append is refused as <see cref="BusMisses.Full"/>.</summary>
    public const int MostPending = 10_000;

    /// <summary>The most keys held for the causes of later entries (<see cref="BusDraft.Key"/>).</summary>
    public const int MostKeys = 4096;

    /// <summary>How long the writers' path keeps the lease's answer before asking it again.</summary>
    public static readonly TimeSpan WriterEvery = TimeSpan.FromSeconds(2);

    /// <summary>The pause before a batch a busy store sent back is tried again.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(5);

    /// <summary>The tries running a busy store's batch is sent back for before it is dropped.</summary>
    public const int MostRetries = 6;

    private readonly Dictionary<string, string> _keys = new(StringComparer.Ordinal);
    private readonly Lock _leaseGate = new();
    private bool _holder = true;
    private DateTimeOffset _askedAt = DateTimeOffset.MinValue;
    private int _retries;
    private long _retried;
    private DateTimeOffset _retryAt = DateTimeOffset.MinValue;
    private Task? _disposed;

    private readonly IBusLogStore _store;
    private readonly BusLogOptions _options;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private readonly BusMeasures _measures = new();
    private readonly HashSet<string> _said = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _missed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _dropped = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _closing = new();
    private readonly SemaphoreSlim _flushing = new(1, 1);
    private List<BusEnvelope> _pending = [];
    private TaskCompletionSource _pulse = Pulse();
    private bool _urgent;
    private bool _closed;
    private long _next;
    private long _appended;
    private long _flushes;
    private DateTimeOffset _lastWrite;
    private DateTimeOffset _dirtySince;
    private readonly Task _loop;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BusLog> Homes = new(StringComparer.OrdinalIgnoreCase);

    private BusLog(IBusLogStore store, BusLogOptions options)
    {
        _store = store;
        _options = options;
        _clock = options.Clock;
        _lastWrite = _clock.GetUtcNow();
        if (options.Home is { } home) Homes[Path.GetFullPath(home)] = this;
        _loop = Task.Run(() => LoopAsync(_closing.Token));
    }

    /// <summary>A log over <paramref name="store"/>, its loop started; the log owns the store and disposes it.</summary>
    public static BusLog Over(IBusLogStore store, BusLogOptions options) => new(store, options);

    /// <summary>The home's own log, written while this process holds the home's lease.</summary>
    public static BusLog Open(string home, MachineLog? log, TimeProvider clock) =>
        Over(new DesktopBusLogStore(home), new BusLogOptions { Clock = clock, Log = log, Writer = () => HomeLease.HeldHereRead(home), Home = home });

    /// <summary>The log this process keeps open for <paramref name="home"/>, or null where none is: a writer with no log in hand tees through it.</summary>
    public static BusLog? Of(string home) => Homes.GetValueOrDefault(Path.GetFullPath(home));

    /// <summary>
    /// Told the entries each write kept, in order, at their kept orders, once they are written: the queue's wake (<see cref="BusQueue"/>).
    /// A listener that throws is said once and the others are told.
    /// </summary>
    public event Action<IReadOnlyList<BusEnvelope>>? Written;

    public IBusQueueStore? Queue => _store as IBusQueueStore;

    public bool? WriterRead() => Ask();

    public bool Writing => Holder();

    /// <summary>A warning said once for <paramref name="once"/> in this process's life, in the machine log the log holds.</summary>
    public void Warn(string once, string @event, params (string Key, object? Value)[] data)
    {
        try
        {
            lock (_gate)
            {
                if (!_said.Add("warn:" + once)) return;
            }

            _options.Log?.Warn(@event, data);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Debug.WriteLine($"bus: a warning could not be said ({error.Message})");
        }
    }

    /// <summary>One measure added by the day (<see cref="BusMeasures"/>), written with the next write as a stage's time is.</summary>
    public void Measured(string measure, string name, long total)
    {
        try
        {
            lock (_gate)
            {
                if (_closed) return;
                var owed = _measures.Owed;
                _measures.Add(DesktopBusLogStore.Day(_clock.GetUtcNow()), measure, Cut(name)!, total);
                WakeLocked(owed);
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Miss(BusMisses.Append, error.GetType().Name);
        }
    }

    /// <summary>The armed timers of <paramref name="entries"/> taken away; a store that cannot is counted, never thrown.</summary>
    public void DropTimers(IReadOnlyList<string> entries)
    {
        try
        {
            _store.DropTimers(entries);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Miss(BusMisses.Store, error.Message, counted: false);
        }
    }

    /// <summary>The clock its entries are stamped by, which its watchdog reads due times on.</summary>
    public TimeProvider Clock => _clock;

    /// <summary>The store's armed timers due by <paramref name="by"/> (null: all); none where it cannot be read now, counted, never thrown.</summary>
    public IReadOnlyList<BusTimer> Timers(DateTimeOffset? by, int limit)
    {
        try
        {
            return _store.Timers(by, limit);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Miss(BusMisses.Store, error.Message, counted: false);
            return [];
        }
    }

    /// <summary>What it has done since it opened.</summary>
    public BusTally Tally
    {
        get
        {
            lock (_gate)
            {
                return new BusTally(_appended, _flushes, new Dictionary<string, long>(_missed, StringComparer.Ordinal), _retried)
                {
                    Dropped = new Dictionary<string, long>(_dropped, StringComparer.Ordinal),
                };
            }
        }
    }

    /// <summary>
    /// Append one envelope behind: given its id, order and time now and written by the loop. Never throws and never waits on the store;
    /// false where refused (an undeclared type among the reasons), which is counted and said.
    /// </summary>
    public bool Append(BusDraft draft) => AppendAll([draft]);

    /// <summary>
    /// Append <paramref name="drafts"/> as one: each given its order under one hold, so they ride one write and are kept or lost together
    /// (a last stall and its give-up). None is appended where any is refused; never throws and never waits on the store.
    /// </summary>
    public bool AppendAll(IReadOnlyList<BusDraft> drafts)
    {
        if (drafts.Count == 0) return true;
        try
        {
            foreach (var each in drafts)
            {
                if (!BusTypes.Declares(each.Type)) return Miss(BusMisses.Unknown, each.Kind);
                if (!Fits(each)) return Miss(BusMisses.Shape, each.Kind);
            }

            if (!Holder()) return Miss(BusMisses.NotHolder, drafts[0].Kind);
            TaskCompletionSource? pulse = null;
            lock (_gate)
            {
                if (_closed) return MissLocked(BusMisses.Closed, drafts[0].Kind);
                if (_pending.Count + drafts.Count > MostPending) return MissLocked(BusMisses.Full, drafts[0].Kind);
                foreach (var draft in drafts) pulse = AppendLocked(draft) ?? pulse;
            }

            pulse?.TrySetResult();
            return true;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Miss(BusMisses.Append, error.GetType().Name);
        }
    }

    /// <summary>One draft given its id, order and time and kept to be written; the pulse to give where it wakes the loop.</summary>
    private TaskCompletionSource? AppendLocked(BusDraft draft)
    {
        var admitted = draft.Type.Admit(draft.Payload);
        var at = _clock.GetUtcNow();
        var cause = draft.After.Select(key => _keys.GetValueOrDefault(key)).FirstOrDefault(id => id is not null);
        var deadline = BusTypes.Obliges(draft.Type) ? at + draft.Type.Deadline.Within : (DateTimeOffset?)null;
        var envelope = new BusEnvelope(
            Guid.CreateVersion7(at).ToString("N"), ++_next, draft.Kind, draft.Subject, draft.Emitter, draft.Answerer, deadline,
            draft.Effect, cause is null || draft.Causes.Contains(cause) || draft.Causes.Count >= BusShapes.MostListed ? draft.Causes : [.. draft.Causes, cause],
            admitted.Kept, at);
        foreach (var (field, reason) in admitted.Dropped) DropLocked(draft.Kind, field, reason, at);
        if (draft.Key is { } key)
        {
            // Bounded: a key from long ago is forgotten with the rest, and its follower's causes stay empty, as after a restart.
            if (_keys.Count >= MostKeys && !_keys.ContainsKey(key)) _keys.Clear();
            _keys[key] = envelope.Id;
        }

        _pending.Add(envelope);
        _appended++;
        var day = DesktopBusLogStore.Day(at);
        _measures.Add(day, BusMeasures.Kind, draft.Kind, 1);
        if (draft.Took is { } took) _measures.Add(day, BusMeasures.Took, draft.Kind, (long)took.TotalMilliseconds);
        if (draft.Commit) _urgent = true;
        if (_pending.Count == 1) _dirtySince = at;
        return _pending.Count == 1 || draft.Commit ? _pulse : null;
    }

    /// <summary>
    /// A writer's one call beside its own write: the draft made and appended, a failure making it counted and said, never thrown, so the
    /// write it rides beside goes on whatever the log does. Null drafts nothing.
    /// </summary>
    public bool Tee(Func<BusDraft?> draft)
    {
        try
        {
            return draft() is { } made && Append(made);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Miss(BusMisses.Append, error.GetType().Name);
        }
    }

    /// <summary>
    /// A look stage's time, measured by the day (<see cref="LookStages"/> tells each span that ends); the first since the last write wakes
    /// an idle loop, which writes it within <see cref="BusLogOptions.MeasureEvery"/> of that write.
    /// </summary>
    public void Staged(string stage, long milliseconds)
    {
        try
        {
            if (!Holder()) return;
            lock (_gate)
            {
                if (_closed) return;
                var owed = _measures.Owed;
                _measures.Add(DesktopBusLogStore.Day(_clock.GetUtcNow()), BusMeasures.Stage, stage, Math.Max(0, milliseconds));
                WakeLocked(owed);
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Miss(BusMisses.Append, error.GetType().Name);
        }
    }

    /// <summary>Write whatever waits now, entries and measures, and wait for it: a test's, and the close's.</summary>
    public async Task FlushAsync()
    {
        await _flushing.WaitAsync().ConfigureAwait(false);
        try
        {
            Flush(measuresAlone: true);
        }
        finally
        {
            _flushing.Release();
        }
    }

    /// <summary>Stop the loop, write what waits, and let the store go; a second caller waits for the first's close, and none throws.</summary>
    public async ValueTask DisposeAsync()
    {
        TaskCompletionSource? mine = null;
        Task closed;
        lock (_gate)
        {
            if (_disposed is null)
            {
                _closed = true;
                mine = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposed = mine.Task;
            }

            closed = _disposed;
        }

        if (mine is null)
        {
            await closed.ConfigureAwait(false);
            return;
        }

        try
        {
            await CloseAsync().ConfigureAwait(false);
        }
        finally
        {
            mine.SetResult();
        }
    }

    private async Task CloseAsync()
    {
        if (_options.Home is { } home) Homes.TryRemove(new KeyValuePair<string, BusLog>(Path.GetFullPath(home), this));
        await _closing.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Miss(BusMisses.Loop, error.Message);
        }

        await LastFlushAsync().ConfigureAwait(false);
        // The last write's own cost and cut, kept with the rest.
        await LastFlushAsync().ConfigureAwait(false);
        _store.Dispose();
        _closing.Dispose();
        _flushing.Dispose();
    }

    private static TaskCompletionSource Pulse() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The close's write: nothing tries again after it, so a busy store's batch is dropped and counted.</summary>
    private async Task LastFlushAsync()
    {
        await _flushing.WaitAsync().ConfigureAwait(false);
        try
        {
            Flush(measuresAlone: true, last: true);
        }
        finally
        {
            _flushing.Release();
        }
    }

    /// <summary>The lease's answer on the writers' path, asked at most every <see cref="WriterEvery"/>.</summary>
    private bool Holder()
    {
        lock (_leaseGate)
        {
            if (_clock.GetUtcNow() - _askedAt < WriterEvery) return _holder;
        }

        Ask();
        lock (_leaseGate) return _holder;
    }

    /// <summary>The lease asked now; an answer that cannot be read this moment (null) keeps the last one standing.</summary>
    private bool? Ask()
    {
        bool? read;
        try
        {
            read = _options.Writer();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            read = null;
        }

        lock (_leaseGate)
        {
            _askedAt = _clock.GetUtcNow();
            if (read is { } held) _holder = held;
        }

        return read;
    }

    /// <summary>
    /// A busy store's batch sent back ahead of the next, its order kept, to be tried after <see cref="RetryAfter"/>; false past
    /// <see cref="MostRetries"/> tries running, or where it would hold more than <see cref="MostPending"/> entries.
    /// </summary>
    private bool PutBack(List<BusEnvelope> entries, bool commit)
    {
        lock (_gate)
        {
            if (_retries >= MostRetries || entries.Count + _pending.Count > MostPending) return false;
            var now = _clock.GetUtcNow();
            _retries++;
            _retried++;
            _measures.Add(DesktopBusLogStore.Day(now), BusMeasures.Retry, BusMeasures.Entries, entries.Count);
            if (entries.Count > 0) _pending = [.. entries, .. _pending];
            _urgent |= commit;
            _retryAt = now + RetryAfter;
            return true;
        }
    }

    /// <summary>
    /// Wakes an idle loop at the first measure owing a write since the last (<paramref name="owed"/>: whether one owed it before), so the
    /// loop waits out the measures' period rather than only an entry. Its continuations run apart, so this is safe under the gate.
    /// </summary>
    private void WakeLocked(bool owed)
    {
        if (!owed && _measures.Owed && _pending.Count == 0) _pulse.TrySetResult();
    }

    /// <summary>
    /// The writer behind: waits for the first entry, then out the interval from it (a commit-class entry cuts the wait short), and writes;
    /// with no entry, waits for one, or out the measures' period where a look's stages were timed since the last write; and after a busy
    /// store's refusal, out the pause before the next try.
    /// </summary>
    private async Task LoopAsync(CancellationToken ct)
    {
        // One wait for the close, shared by every idle wait, so an idle log holds one registration on the token, never one a wake.
        var closing = Task.Delay(Timeout.Infinite, ct);
        while (!ct.IsCancellationRequested)
        {
            Task pulsed;
            bool dirty, urgent, measured;
            DateTimeOffset dirtySince, retryAt;
            lock (_gate)
            {
                if (_pulse.Task.IsCompleted) _pulse = Pulse();
                pulsed = _pulse.Task;
                dirty = _pending.Count > 0;
                urgent = _urgent;
                measured = _measures.Owed;
                dirtySince = _dirtySince;
                retryAt = _retryAt;
            }

            var now = _clock.GetUtcNow();
            TimeSpan wait;
            bool alone;
            if (dirty)
            {
                wait = urgent ? TimeSpan.Zero : dirtySince + _options.Interval - now;
                alone = false;
            }
            else if (measured)
            {
                wait = _lastWrite + _options.MeasureEvery - now;
                alone = true;
            }
            else
            {
                await Task.WhenAny(pulsed, closing).ConfigureAwait(false);
                continue;
            }

            if (retryAt - now > wait) wait = retryAt - now;
            if (wait > TimeSpan.Zero)
            {
                await Task.WhenAny(pulsed, Task.Delay(wait, _clock, ct)).ConfigureAwait(false);
                continue;
            }

            await _flushing.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                Flush(measuresAlone: alone);
            }
            finally
            {
                _flushing.Release();
            }
        }
    }

    /// <summary>
    /// One write of whatever waits: the entries, and the measures owed with them; with none, the measures alone where asked. The
    /// <paramref name="last"/> write sends nothing back to be tried again.
    /// </summary>
    private void Flush(bool measuresAlone, bool last = false)
    {
        List<BusEnvelope> entries;
        bool commit;
        IReadOnlyList<BusMeasureRow> measures;
        lock (_gate)
        {
            entries = _pending;
            _pending = [];
            commit = _urgent;
            _urgent = false;
            if (entries.Count == 0 && (!measuresAlone || !_measures.Any)) return;
            measures = _measures.Take();
        }

        BusWritten written;
        TimeSpan took;
        try
        {
            var began = Stopwatch.GetTimestamp();
            written = _store.Write(new BusBatch(entries, measures, commit) { Writer = Ask });
            took = Stopwatch.GetElapsedTime(began);
        }
        catch (BusNotWriterException)
        {
            lock (_gate)
            {
                _measures.Return(measures);
                _lastWrite = _clock.GetUtcNow();
            }

            foreach (var entry in entries) Miss(BusMisses.NotHolder, entry.Kind);
            return;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            var kept = error is BusStoreBusyException && !last && PutBack(entries, commit);
            lock (_gate)
            {
                _measures.Return(measures);
                _lastWrite = _clock.GetUtcNow();
                if (!kept) _retries = 0;
            }

            if (kept) return;
            foreach (var entry in entries) Miss(BusMisses.Store, error.Message);
            if (entries.Count == 0) Miss(BusMisses.Store, error.Message, counted: false);
            return;
        }

        lock (_gate)
        {
            _retries = 0;
            _lastWrite = _clock.GetUtcNow();
            if (written.Shift > 0) Seat(written.Shift);
            var day = DesktopBusLogStore.Day(_lastWrite);
            if (entries.Count > 0)
            {
                _flushes++;
                _measures.Add(day, BusMeasures.Flush, BusMeasures.Entries, entries.Count);
                if (!written.Opened) _measures.Add(day, BusMeasures.Flush, BusMeasures.Micros, (long)(took.TotalMilliseconds * 1000));
                if (commit) _measures.Add(day, BusMeasures.Flush, BusMeasures.CommitClass, 1);
                _measures.Add(day, BusMeasures.Bytes, BusMeasures.Rows, written.Bytes, entries.Count, written.Most);
                if (written.Cut > 0) _measures.Add(day, BusMeasures.Cut, BusMeasures.Rows, written.Cut);
            }
            else if (!written.Opened)
            {
                // A write of the measures alone owes no write of its own cost: it rides the next.
                _measures.Add(day, BusMeasures.Flush, BusMeasures.MeasuresAlone, (long)(took.TotalMilliseconds * 1000), owing: false);
            }

            // What the write cost the disk, every write's: its pages and their bytes, riding the next write as its time does.
            if (written.Pages is { } pages)
            {
                _measures.Add(day, BusMeasures.Disk, BusMeasures.Pages, pages, owing: entries.Count > 0);
                _measures.Add(day, BusMeasures.Disk, BusMeasures.Bytes, pages * written.PageSize, owing: entries.Count > 0);
            }
        }

        if (entries.Count > 0) Tell(written.Shift > 0 ? [.. entries.Select(entry => entry with { Seq = entry.Seq + written.Shift })] : entries);
    }

    /// <summary>The kept entries told to each listener (<see cref="Written"/>), one that throws said once and passed.</summary>
    private void Tell(IReadOnlyList<BusEnvelope> kept)
    {
        if (Written is not { } listeners) return;
        foreach (var listener in listeners.GetInvocationList().Cast<Action<IReadOnlyList<BusEnvelope>>>())
        {
            try
            {
                listener(kept);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Warn("written:" + error.GetType().Name, "bus.listener-threw", ("why", Cut(error.Message)));
            }
        }
    }

    /// <summary>
    /// What waits, and the orders still to give, moved by <paramref name="shift"/> after a write the store seated after its newest: a log
    /// opened again goes on after its last entry, and one that regained the lease after the other holder's, every later batch with it.
    /// </summary>
    private void Seat(long shift)
    {
        _next += shift;
        for (var n = 0; n < _pending.Count; n++) _pending[n] = _pending[n] with { Seq = _pending[n].Seq + shift };
    }

    private static bool Fits(BusDraft draft) =>
        draft.Subject is { Length: > 0 } && BusShapes.Short(draft.Subject) && BusShapes.Short(draft.Answerer) && BusShapes.Short(draft.Effect)
        && draft.Causes.Count <= BusShapes.MostListed && draft.Causes.All(cause => BusShapes.Short(cause));

    /// <summary>A payload's field or value dropped at emit: counted by reason, and by the day as reason, type and field; said once for each.</summary>
    private void DropLocked(string kind, string field, string reason, DateTimeOffset at)
    {
        var named = Cut($"{reason}|{kind}|{field}")!;
        _dropped[reason] = _dropped.GetValueOrDefault(reason) + 1;
        _measures.Add(DesktopBusLogStore.Day(at), BusMeasures.Drop, named, 1);
        if (_said.Add("drop:" + named)) _options.Log?.Warn("bus.dropped", ("reason", reason), ("kind", kind), ("field", Cut(field)));
    }

    private bool Miss(string reason, string? what, bool counted = true)
    {
        lock (_gate) return MissLocked(reason, what, counted);
    }

    private bool MissLocked(string reason, string? what, bool counted = true)
    {
        try
        {
            if (counted)
            {
                _missed[reason] = _missed.GetValueOrDefault(reason) + 1;
                var owed = _measures.Owed;
                _measures.Add(DesktopBusLogStore.Day(_clock.GetUtcNow()), BusMeasures.Miss, reason, 1);
                if (!_closed) WakeLocked(owed);
            }

            // Said once for each reason in this process's life; the count says the rest.
            if (_said.Add(reason)) _options.Log?.Warn("bus.refused", ("reason", reason), ("what", Cut(what)));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Debug.WriteLine($"bus: a refusal could not be said ({error.Message})");
        }

        return false;
    }

    private static string? Cut(string? what) => what is null ? null : what.Length <= 120 ? what : what[..120];
}
