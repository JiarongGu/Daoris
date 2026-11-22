namespace Daoris.Driver;

/// <summary>What came of one key's work (<see cref="BusFlowConsumer{TWork}"/>): the layer alone turns it into the acknowledgement.</summary>
public enum BusFlowResult
{
    /// <summary>The job moved: the entries' obligations end where their type says an acknowledgement ends them.</summary>
    Moved,

    /// <summary>The job did not move (not ready, not governed, a bound passed): the entries are handled and their timers stay armed.</summary>
    NotMoved,

    /// <summary>The job failed: that key's work alone is tried again with backoff.</summary>
    Failed,
}

/// <summary>A handler's answer: what came of the work, and why where it did not move.</summary>
public readonly record struct BusFlowOutcome(BusFlowResult Result, string Why = "")
{
    public static readonly BusFlowOutcome Moved = new(BusFlowResult.Moved);

    public static BusFlowOutcome NotMoved(string why) => new(BusFlowResult.NotMoved, why);

    public static BusFlowOutcome Failed(string why) => new(BusFlowResult.Failed, why);
}

/// <summary>How much of a consumer's work runs at once: keys overall, and keys within one group (a repository).</summary>
public sealed record BusFlowBounds(int KeysAtOnce, int PerGroup = int.MaxValue);

/// <summary>One piece of work an entry names: the key it is worked under, what it asks of it, and the group that bounds it.</summary>
public sealed record BusFlowPiece<TWork>(string Key, TWork Work, string? Group = null);

/// <summary>
/// The dispatch and the stream's operators over the bus's queue: a consumer declares its kinds (its subscription), the pieces of
/// work an entry names (<see cref="PiecesAsync"/>: each its <b>key</b> and its <b>group</b>), how entries waiting on one key coalesce
/// (<see cref="Merge"/>), its bounds (keys at once, keys at once within a group) and its handler (<see cref="MoveAsync"/>), which gets one key's
/// merged work and says <i>moved</i>, <i>not moved</i> or <i>failed</i>. The layer keeps the rest, so no consumer writes it again:
/// <list type="bullet">
/// <item>one pass on a key at a time, in the order its entries came; the entries that wait on a key while a pass runs are one piece of work
/// for the next (their merge), never a pass nested in another; an entry heard in the meantime is judged by the pass after, which reads it;</item>
/// <item>the bounds, taken in one order (key, then its group, then the overall), so no two passes wait on each other;</item>
/// <item>the acknowledgement from the outcome alone: <i>moved</i> ends the entries' obligations (where <see cref="Settles"/> allows), <i>not
/// moved</i> acknowledges them as handled with their timers armed for the stall view, <i>failed</i> acknowledges them as handled too and the
/// layer tries that key alone again with backoff and its give-up, ending what it held once it moves; no lane, other key or other fact waits;</item>
/// <item>entries of a kind it coalesces that wait in a row on one subject are handed together and acknowledged only after the one result.</item>
/// </list>
/// </summary>
public abstract class BusFlowConsumer<TWork> : IBusConsumer
{
    private readonly BusFlowBounds _bounds;
    private readonly TimeSpan _pause;
    private readonly TimeProvider _clock;
    private readonly Dictionary<string, KeyState> _keys = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _groups = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Retrying> _retrying = new(StringComparer.OrdinalIgnoreCase);
    private BusConsumerHost? _host;
    private readonly SemaphoreSlim _atOnce;

    /// <param name="bounds">How much of its work runs at once.</param>
    /// <param name="clock">The clock a time batch waits on.</param>
    /// <param name="pause">Time batching: how long a pass waits, once it holds its key, for more entries of the key to arrive before it takes them (zero: none).</param>
    protected BusFlowConsumer(BusFlowBounds bounds, TimeProvider clock, TimeSpan pause = default)
    {
        _bounds = bounds;
        _clock = clock;
        _pause = pause;
        _atOnce = new SemaphoreSlim(Math.Max(1, bounds.KeysAtOnce));
    }

    public abstract BusSubscription Subscription { get; }

    public abstract string Obliged { get; }

    /// <summary>Whether entries of <paramref name="kind"/> waiting in a row on one subject are handed together.</summary>
    protected virtual bool Coalesces(string kind) => false;

    /// <summary>The pieces of work <paramref name="entry"/> names; none where nothing is owed it, which moves it. A throw is the queue's: the entry is tried again.</summary>
    protected abstract Task<IReadOnlyList<BusFlowPiece<TWork>>> PiecesAsync(BusEnvelope entry, CancellationToken ct);

    /// <summary>Two pieces of work on one key become one: what the entries waiting on it named, together.</summary>
    protected abstract TWork Merge(TWork earlier, TWork later);

    /// <summary>One key's merged work; what came of it. A throw reads as failed.</summary>
    protected abstract Task<BusFlowOutcome> MoveAsync(string key, TWork work, CancellationToken ct);

    /// <summary>Whether a move of the entry's work also ends the entry's obligation: false for a fact a later fact ends.</summary>
    protected virtual bool Settles(BusEnvelope entry) => true;

    /// <summary>Told of each entry as it is handed, before it waits for its key: what a consumer measures of the fact itself. Never throws or waits.</summary>
    protected virtual void Observe(BusEnvelope entry)
    {
    }

    /// <summary>Told once the consumer resumed from its cursor: where it rebuilds its view.</summary>
    public virtual Task ResumedAsync(CancellationToken ct) => Task.CompletedTask;

    bool IBusConsumer.Folds(string kind) => Coalesces(kind);

    Task<BusHandled> IBusConsumer.HandleAsync(BusEnvelope entry, CancellationToken ct) => HandleRowAsync([entry], ct);

    Task<BusHandled> IBusConsumer.HandleAsync(IReadOnlyList<BusEnvelope> row, CancellationToken ct) => HandleRowAsync(row, ct);

    private async Task<BusHandled> HandleRowAsync(IReadOnlyList<BusEnvelope> row, CancellationToken ct)
    {
        foreach (var entry in row) Observe(entry);
        var pieces = new Dictionary<string, BusFlowPiece<TWork>>(StringComparer.OrdinalIgnoreCase);
        var reached = new Dictionary<BusEnvelope, List<string>>();
        foreach (var entry in row)
        {
            reached[entry] = [];
            foreach (var piece in await PiecesAsync(entry, ct).ConfigureAwait(false))
            {
                pieces[piece.Key] = pieces.TryGetValue(piece.Key, out var before) ? piece with { Work = Merge(before.Work, piece.Work) } : piece;
                reached[entry].Add(piece.Key);
            }
        }

        var outcomes = (await Task.WhenAll(pieces.Values.Select(async piece => (piece.Key, Outcome: await AskAsync(piece, ct).ConfigureAwait(false)))).ConfigureAwait(false))
            .ToDictionary(each => each.Key, each => each.Outcome, StringComparer.OrdinalIgnoreCase);
        // No piece named means nothing is owed, so nothing is left unmoved.
        if (outcomes.Values.All(outcome => outcome.Result == BusFlowResult.Moved)) return row.All(Settles) ? BusHandled.Ended : BusHandled.Judged;

        // A failed key costs its own entries their ending, never the row's other keys: it is tried again here, and what moved ends what it held.
        foreach (var entry in row)
        {
            var failed = reached[entry].Where(key => outcomes[key].Result == BusFlowResult.Failed).ToList();
            if (failed.Count + reached[entry].Count(key => outcomes[key].Result == BusFlowResult.NotMoved) == 0)
            {
                if (Settles(entry)) _host?.EndLate(entry);
            }
            else if (failed.Count > 0 && reached[entry].All(key => outcomes[key].Result != BusFlowResult.NotMoved) && Settles(entry))
            {
                var hold = new Hold(entry, failed.Count);
                foreach (var key in failed) Retry(pieces[key], entry, hold, outcomes[key].Why);
            }
        }

        foreach (var (key, outcome) in outcomes)
        {
            if (outcome.Result == BusFlowResult.Moved) Retired(key);
        }

        return BusHandled.Judged;
    }

    /// <summary>The entries a key's retry stands for: each ends its obligation once every key it waits on has moved.</summary>
    private sealed class Hold(BusEnvelope entry, int left)
    {
        public BusEnvelope Entry { get; } = entry;

        public int Left { get; set; } = left;
    }

    private sealed class Retrying(BusFlowPiece<TWork> piece, BusEnvelope entry)
    {
        public BusFlowPiece<TWork> Piece { get; set; } = piece;

        public BusEnvelope Entry { get; } = entry;

        public List<Hold> Holds { get; } = [];

        public CancellationTokenSource Stop { get; set; } = new();
    }

    /// <summary>Told once by the queue at its start.</summary>
    public void Attached(BusConsumerHost host) => _host = host;

    int? IBusConsumer.SubjectsAtOnce => int.MaxValue;

    /// <summary>A failed key tried again alone with backoff; the work of a fact that failed meanwhile joins it. Past its type's retries it is given up.</summary>
    private void Retry(BusFlowPiece<TWork> piece, BusEnvelope entry, Hold hold, string why)
    {
        if (_host is not { } host) return;
        host.Log.Measured(BusMeasures.Consumer, $"{Subscription.Consumer}:threw", 1);
        host.Log.Warn($"queue:threw:{Subscription.Consumer}:{entry.Kind}", "bus.consumer-threw", ("consumer", Subscription.Consumer), ("kind", entry.Kind), ("why", why));
        Retrying state;
        lock (_retrying)
        {
            if (_retrying.TryGetValue(piece.Key, out var was))
            {
                was.Piece = was.Piece with { Work = Merge(was.Piece.Work, piece.Work) };
                was.Holds.Add(hold);
                return;
            }

            state = new Retrying(piece, entry);
            state.Holds.Add(hold);
            state.Stop = CancellationTokenSource.CreateLinkedTokenSource(host.Closing);
            _retrying[piece.Key] = state;
        }

        _ = Task.Run(() => RetryAsync(host, state));
    }

    private async Task RetryAsync(BusConsumerHost host, Retrying state)
    {
        var name = Subscription.Consumer;
        var type = BusTypes.Of(state.Entry.Kind);
        var tries = 1;
        try
        {
            while (true)
            {
                if (type is null || tries > type.Retries)
                {
                    BusConsumerRun.GiveUp(host.Log, name, Obliged, state.Entry, type, tries);
                    return;
                }

                var wait = TimeSpan.FromTicks(Math.Min(host.Options.RetryMost.Ticks, host.Options.RetryFirst.Ticks << Math.Min(20, tries - 1)));
                await Task.Delay(wait, host.Options.Clock, state.Stop.Token).ConfigureAwait(false);
                BusFlowPiece<TWork> piece;
                lock (_retrying) piece = state.Piece;
                var outcome = await AskAsync(piece, state.Stop.Token).ConfigureAwait(false);
                if (outcome.Result == BusFlowResult.Failed)
                {
                    tries++;
                    continue;
                }

                if (outcome.Result == BusFlowResult.Moved)
                {
                    List<Hold> done;
                    lock (_retrying) done = [.. state.Holds.Where(hold => --hold.Left == 0)];
                    foreach (var hold in done) host.EndLate(hold.Entry);
                }

                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lock (_retrying)
            {
                if (_retrying.TryGetValue(state.Piece.Key, out var now) && now == state) _retrying.Remove(state.Piece.Key);
            }
        }
    }

    /// <summary>A key a fact moved needs no retry of an earlier failure: the pass that moved it read what the failed one named.</summary>
    private void Retired(string key)
    {
        Retrying? state;
        lock (_retrying)
        {
            if (!_retrying.Remove(key, out state)) return;
        }

        state.Stop.Cancel();
        if (_host is { } host)
        {
            foreach (var hold in state.Holds)
            {
                if (--hold.Left == 0) host.EndLate(hold.Entry);
            }
        }
    }

    /// <summary>
    /// One piece of work asked of its key's pass, from a fact or from a turn of the consumer's own: it waits behind the pass running on the
    /// key and is merged with whatever else waits, then runs within the bounds. A throw of the handler reads as failed.
    /// </summary>
    public async Task<BusFlowOutcome> AskAsync(BusFlowPiece<TWork> piece, CancellationToken ct)
    {
        var ticket = new Ticket(piece.Work);
        var state = Enter(piece.Key);
        try
        {
            lock (state) state.Waiting.Add(ticket);
            while (!ticket.Done)
            {
                await state.Pass.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (ticket.Done) break;
                    if (_pause > TimeSpan.Zero) await Task.Delay(_pause, _clock, ct).ConfigureAwait(false);
                    Ticket[] taken;
                    lock (state)
                    {
                        taken = [.. state.Waiting];
                        state.Waiting.Clear();
                    }

                    BusFlowOutcome outcome;
                    try
                    {
                        outcome = await RunAsync(piece.Key, taken.Select(each => each.Work).Aggregate(Merge), piece.Group, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // This asker's wait ended: the work it took goes back to those whose asks still stand.
                        lock (state) state.Waiting.InsertRange(0, taken.Where(each => !each.Done));
                        throw;
                    }

                    foreach (var each in taken) each.Finish(outcome);
                }
                finally
                {
                    state.Pass.Release();
                }
            }

            return ticket.Outcome;
        }
        finally
        {
            lock (state) state.Waiting.Remove(ticket);
            Leave(piece.Key, state);
        }
    }

    private async Task<BusFlowOutcome> RunAsync(string key, TWork work, string? group, CancellationToken ct)
    {
        var place = group is null || _bounds.PerGroup == int.MaxValue ? null : _groups.GetOrAdd(group, _ => new SemaphoreSlim(Math.Max(1, _bounds.PerGroup)));
        var atPlace = false;
        var atOnce = false;
        try
        {
            // Both waited inside, so a wait cancelled here still gives the group's place back.
            if (place is not null)
            {
                await place.WaitAsync(ct).ConfigureAwait(false);
                atPlace = true;
            }

            await _atOnce.WaitAsync(ct).ConfigureAwait(false);
            atOnce = true;
            return await MoveAsync(key, work, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException && !(error is OperationCanceledException && ct.IsCancellationRequested))
        {
            return BusFlowOutcome.Failed(error.Message);
        }
        finally
        {
            if (atOnce) _atOnce.Release();
            if (atPlace) place!.Release();
        }
    }

    private KeyState Enter(string key)
    {
        lock (_keys)
        {
            if (!_keys.TryGetValue(key, out var state)) _keys[key] = state = new KeyState();
            state.Users++;
            return state;
        }
    }

    private void Leave(string key, KeyState state)
    {
        lock (_keys)
        {
            if (--state.Users == 0) _keys.Remove(key);
        }
    }

    private sealed class KeyState
    {
        public SemaphoreSlim Pass { get; } = new(1, 1);

        public List<Ticket> Waiting { get; } = [];

        public int Users { get; set; }
    }

    private sealed class Ticket(TWork work)
    {
        private BusFlowOutcome _outcome;
        private volatile bool _done;

        public TWork Work { get; } = work;

        public bool Done => _done;

        public BusFlowOutcome Outcome => _outcome;

        public void Finish(BusFlowOutcome outcome)
        {
            _outcome = outcome;
            _done = true;
        }
    }
}
