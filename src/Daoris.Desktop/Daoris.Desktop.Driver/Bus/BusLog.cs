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

    /// <summary>Whether this process holds the home's lease, asked at each append: only the lease holder writes the log.</summary>
    public Func<bool> Writer { get; init; } = () => true;
}

/// <summary>What a log has done since it opened, in memory: what a test and the stats read.</summary>
/// <param name="Appended">The entries taken.</param>
/// <param name="Flushes">The writes that carried entries.</param>
/// <param name="Missed">The entries refused, by reason (<see cref="BusMisses"/>).</param>
public sealed record BusTally(long Appended, long Flushes, IReadOnlyDictionary<string, long> Missed);

/// <summary>
/// The bus's log in its first form: an envelope appended in memory, given its id, order and time at once, and
/// written behind by a loop of its own: plain entries at most about once every <see cref="Interval"/> while any wait, a commit-class one
/// at once, grouped with whatever else is due. While nothing is appended nothing is written; the measures alone at most every
/// <see cref="MeasureEvery"/> while a look's stages have been timed since the last write.
/// </summary>
/// <remarks>
/// <para><b>A shadow append never fails the write it rides beside</b>: <see cref="Append"/> never throws and never waits on the store. A
/// refusal (the store's, a payload that is no id, code, count or time, a process that is not the lease holder) is counted by its reason,
/// by the day, and said once in the machine log as <c>bus.refused</c>. A batch the store refused is dropped, never written later out of
/// its order; the measures it carried wait for the next write.</para>
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

    private readonly Dictionary<string, string> _keys = new(StringComparer.Ordinal);

    private readonly IBusLogStore _store;
    private readonly BusLogOptions _options;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private readonly BusMeasures _measures = new();
    private readonly HashSet<string> _said = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _missed = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _closing = new();
    private readonly SemaphoreSlim _flushing = new(1, 1);
    private List<BusEnvelope> _pending = [];
    private TaskCompletionSource _pulse = Pulse();
    private bool _urgent;
    private bool _closed;
    private long _next;
    private bool _ordered;
    private long _appended;
    private long _flushes;
    private DateTimeOffset _lastWrite;
    private DateTimeOffset _dirtySince;
    private readonly Task _loop;

    private BusLog(IBusLogStore store, BusLogOptions options)
    {
        _store = store;
        _options = options;
        _clock = options.Clock;
        _lastWrite = _clock.GetUtcNow();
        _loop = Task.Run(() => LoopAsync(_closing.Token));
    }

    /// <summary>A log over <paramref name="store"/>, its loop started; the log owns the store and disposes it.</summary>
    public static BusLog Over(IBusLogStore store, BusLogOptions options) => new(store, options);

    /// <summary>The home's own log, written while this process holds the home's lease.</summary>
    public static BusLog Open(string home, MachineLog? log, TimeProvider clock) =>
        Over(new DesktopBusLogStore(home), new BusLogOptions { Clock = clock, Log = log, Writer = () => HomeLease.HeldHere(home) });

    /// <summary>What it has done since it opened.</summary>
    public BusTally Tally
    {
        get
        {
            lock (_gate) return new BusTally(_appended, _flushes, new Dictionary<string, long>(_missed, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// Append one envelope behind: given its id, order and time now and written by the loop. Never throws and never waits on the store;
    /// false where refused, which is counted and said.
    /// </summary>
    public bool Append(BusDraft draft)
    {
        try
        {
            if (!Fits(draft)) return Miss(BusMisses.Shape, draft.Kind);
            if (!_options.Writer()) return Miss(BusMisses.NotHolder, draft.Kind);
            TaskCompletionSource? pulse = null;
            lock (_gate)
            {
                if (_closed) return MissLocked(BusMisses.Closed, draft.Kind);
                if (_pending.Count >= MostPending) return MissLocked(BusMisses.Full, draft.Kind);
                var at = _clock.GetUtcNow();
                var cause = draft.After.Select(key => _keys.GetValueOrDefault(key)).FirstOrDefault(id => id is not null);
                var envelope = new BusEnvelope(
                    Guid.CreateVersion7(at).ToString("N"), ++_next, draft.Kind, draft.Subject, draft.Emitter, draft.Answerer, draft.Deadline,
                    draft.Effect, cause is null || draft.Causes.Contains(cause) ? draft.Causes : [.. draft.Causes, cause], draft.Payload, at);
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
                if (_pending.Count == 1 || draft.Commit) pulse = _pulse;
            }

            pulse?.TrySetResult();
            return true;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Miss(BusMisses.Append, error.GetType().Name);
        }
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

    /// <summary>Stop the loop, write what waits, and let the store go.</summary>
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_closed) return;
            _closed = true;
        }

        await _closing.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await FlushAsync().ConfigureAwait(false);
        // The last write's own cost and cut, kept with the rest.
        await FlushAsync().ConfigureAwait(false);
        _store.Dispose();
        _closing.Dispose();
        _flushing.Dispose();
    }

    private static TaskCompletionSource Pulse() => new(TaskCreationOptions.RunContinuationsAsynchronously);

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
    /// with no entry, waits for one, or out the measures' period where a look's stages were timed since the last write.
    /// </summary>
    private async Task LoopAsync(CancellationToken ct)
    {
        // One wait for the close, shared by every idle wait, so an idle log holds one registration on the token, never one a wake.
        var closing = Task.Delay(Timeout.Infinite, ct);
        while (!ct.IsCancellationRequested)
        {
            Task pulsed;
            bool dirty, urgent, measured;
            DateTimeOffset dirtySince;
            lock (_gate)
            {
                if (_pulse.Task.IsCompleted) _pulse = Pulse();
                pulsed = _pulse.Task;
                dirty = _pending.Count > 0;
                urgent = _urgent;
                measured = _measures.Owed;
                dirtySince = _dirtySince;
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

    /// <summary>One write of whatever waits: the entries, and the measures owed with them; with none, the measures alone where asked.</summary>
    private void Flush(bool measuresAlone)
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
            // The store's first open (its steps run there) is the order's read, outside the flush's time.
            if (entries.Count > 0) Order(entries);
            var began = Stopwatch.GetTimestamp();
            written = _store.Write(new BusBatch(entries, measures, commit));
            took = Stopwatch.GetElapsedTime(began);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            lock (_gate)
            {
                _measures.Return(measures);
                _lastWrite = _clock.GetUtcNow();
            }

            foreach (var entry in entries) Miss(BusMisses.Store, error.Message);
            if (entries.Count == 0) Miss(BusMisses.Store, error.Message, counted: false);
            return;
        }

        lock (_gate)
        {
            _lastWrite = _clock.GetUtcNow();
            var day = DesktopBusLogStore.Day(_lastWrite);
            if (entries.Count > 0)
            {
                _flushes++;
                _measures.Add(day, BusMeasures.Flush, BusMeasures.Entries, entries.Count);
                _measures.Add(day, BusMeasures.Flush, BusMeasures.Micros, (long)(took.TotalMilliseconds * 1000));
                if (commit) _measures.Add(day, BusMeasures.Flush, BusMeasures.CommitClass, 1);
                _measures.Add(day, BusMeasures.Bytes, BusMeasures.Rows, written.Bytes, entries.Count, written.Most);
                if (written.Cut > 0) _measures.Add(day, BusMeasures.Cut, BusMeasures.Rows, written.Cut);
            }
            else
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
    }

    /// <summary>
    /// The first batch's orders follow the newest the store keeps, so a log opened again goes on after it; read once, at the first flush,
    /// off the writer's path.
    /// </summary>
    private void Order(List<BusEnvelope> entries)
    {
        if (_ordered) return;
        var newest = _store.Newest();
        _ordered = true;
        if (newest == 0) return;
        lock (_gate)
        {
            for (var n = 0; n < entries.Count; n++) entries[n] = entries[n] with { Seq = entries[n].Seq + newest };
            _next += newest;
            foreach (var each in _pending.Select((entry, n) => (entry, n)).ToList()) _pending[each.n] = each.entry with { Seq = each.entry.Seq + newest };
        }
    }

    private static bool Fits(BusDraft draft) =>
        draft.Kind is { Length: > 0 } && draft.Subject is { Length: > 0 } && draft.Emitter is { Length: > 0 }
        && DesktopBusLogStore.Short(draft.Kind) && DesktopBusLogStore.Short(draft.Subject) && DesktopBusLogStore.Short(draft.Emitter)
        && DesktopBusLogStore.Short(draft.Answerer) && DesktopBusLogStore.Short(draft.Effect)
        && draft.Causes.Count <= DesktopBusLogStore.MostListed && draft.Causes.All(DesktopBusLogStore.Short)
        && draft.Payload.All(pair => pair.Key is { Length: > 0 } && DesktopBusLogStore.Short(pair.Key) && DesktopBusLogStore.Fits(pair.Value));

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
