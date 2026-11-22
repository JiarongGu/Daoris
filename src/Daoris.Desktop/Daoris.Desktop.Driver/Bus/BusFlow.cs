namespace Daoris.Driver;

/// <summary>What came of one key's work (<see cref="BusFlowConsumer{TWork}"/>): the layer alone turns it into the acknowledgement.</summary>
public enum BusFlowResult
{
    /// <summary>The job moved: the entries' obligations end where their type says an acknowledgement ends them.</summary>
    Moved,

    /// <summary>The job did not move (not ready, not governed, a bound passed): the entries are handled and their timers stay armed.</summary>
    NotMoved,

    /// <summary>The job failed: a retry fact on the bus tries that work alone again with backoff.</summary>
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
/// The dispatch and the stream's operators over the bus's queue: a consumer declares its kinds, the pieces of
/// work an entry names (<see cref="PiecesAsync"/>: each its <b>key</b> and its <b>group</b>), how entries waiting on one key coalesce
/// (<see cref="Merge"/>), its bounds (keys at once, keys at once within a group) and its handler (<see cref="MoveAsync"/>), which gets one key's
/// merged work and says <i>moved</i>, <i>not moved</i> or <i>failed</i>. The layer keeps the rest, so no consumer writes it again:
/// <list type="bullet">
/// <item>one pass on a key at a time, in the order its entries came; the entries that wait on a key while a pass runs are one piece of work
/// for the next (their merge), never a pass nested in another; an entry heard in the meantime is judged by the pass after, which reads it;</item>
/// <item>the bounds, taken in one order (key, then its group, then the overall), so no two passes wait on each other;</item>
/// <item>the acknowledgement from the outcome alone: <i>moved</i> ends the entries' obligations (where <see cref="Settles"/> allows), <i>not
/// moved</i> acknowledges them as handled with their timers armed for the stall view, <i>failed</i> acknowledges them as handled too and writes a
/// retry fact (<see cref="BusTypes.FlowRetry"/>) for that work alone, due after the type's backoff;</item>
/// <item>the retry fact, handed back through the same queue once due, runs the same pass under the same bounds, fence and lease; if it moves
/// it ends the obligations of the entry that failed, if it fails it writes the next, and past the type's retries the give-up names the entry
/// and its timer stays armed: nothing of a retry lives only in memory;</item>
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
    private readonly SemaphoreSlim _atOnce;
    private BusConsumerHost? _host;

    /// <param name="name">Its durable name in the queue.</param>
    /// <param name="kinds">The kinds it reads; the retry fact is added.</param>
    /// <param name="bounds">How much of its work runs at once.</param>
    /// <param name="clock">The clock a time batch waits on.</param>
    /// <param name="pause">Time batching: how long a pass waits, once it holds its key, for more entries of the key to arrive before it takes them (zero: none).</param>
    protected BusFlowConsumer(string name, IReadOnlyList<string> kinds, BusFlowBounds bounds, TimeProvider clock, TimeSpan pause = default)
    {
        Subscription = BusTypes.Subscribe(name, [.. kinds, BusTypes.FlowRetry.Kind]);
        _bounds = bounds;
        _clock = clock;
        _pause = pause;
        _atOnce = new SemaphoreSlim(Math.Max(1, bounds.KeysAtOnce));
    }

    public BusSubscription Subscription { get; }

    public abstract string Obliged { get; }

    /// <summary>Whether entries of <paramref name="kind"/> waiting in a row on one subject are handed together.</summary>
    protected virtual bool Coalesces(string kind) => false;

    /// <summary>The pieces of work <paramref name="entry"/> names; none where nothing is owed it, which moves it. A throw fails that entry's work alone.</summary>
    protected abstract Task<IReadOnlyList<BusFlowPiece<TWork>>> PiecesAsync(BusEnvelope entry, CancellationToken ct);

    /// <summary>The work now owed <paramref name="key"/> when its retry is due; null where none is owed any more, which moves it.</summary>
    protected abstract Task<BusFlowPiece<TWork>?> RetryWorkAsync(string key, CancellationToken ct);

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

    public void Attached(BusConsumerHost host) => _host = host;

    int? IBusConsumer.SubjectsAtOnce => 32;

    // A retry is never folded: each stands for one entry's work, so a row would run the first and acknowledge them all.
    bool IBusConsumer.Folds(string kind) => kind != BusTypes.FlowRetry.Kind && Coalesces(kind);

    DateTimeOffset? IBusConsumer.NotBefore(BusEnvelope entry) => entry.Kind == BusTypes.FlowRetry.Kind ? Moment(entry, "due") : null;

    Task<BusHandled> IBusConsumer.HandleAsync(BusEnvelope entry, CancellationToken ct) => HandleRowAsync([entry], ct);

    Task<BusHandled> IBusConsumer.HandleAsync(IReadOnlyList<BusEnvelope> row, CancellationToken ct) => HandleRowAsync(row, ct);

    private string Name => Subscription.Consumer;

    private async Task<BusHandled> HandleRowAsync(IReadOnlyList<BusEnvelope> row, CancellationToken ct)
    {
        if (row[0].Kind == BusTypes.FlowRetry.Kind) return await RetryAsync(row[0], ct).ConfigureAwait(false);
        foreach (var entry in row) Observe(entry);
        var pieces = new Dictionary<string, BusFlowPiece<TWork>>(StringComparer.OrdinalIgnoreCase);
        var reached = new Dictionary<BusEnvelope, List<string>>();
        var broken = new List<(BusEnvelope Entry, string Why)>();
        foreach (var entry in row)
        {
            IReadOnlyList<BusFlowPiece<TWork>> named;
            try
            {
                named = await PiecesAsync(entry, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException)
            {
                broken.Add((entry, error.Message));
                continue;
            }

            reached[entry] = [];
            foreach (var piece in named)
            {
                pieces[piece.Key] = pieces.TryGetValue(piece.Key, out var before) ? piece with { Work = Merge(before.Work, piece.Work) } : piece;
                reached[entry].Add(piece.Key);
            }
        }

        var outcomes = (await Task.WhenAll(pieces.Values.Select(async piece => (piece.Key, Outcome: await AskAsync(piece, ct).ConfigureAwait(false)))).ConfigureAwait(false))
            .ToDictionary(each => each.Key, each => each.Outcome, StringComparer.OrdinalIgnoreCase);
        // No piece named means nothing is owed, so nothing is left unmoved.
        if (broken.Count == 0 && outcomes.Values.All(outcome => outcome.Result == BusFlowResult.Moved)) return row.All(Settles) ? BusHandled.Ended : BusHandled.Judged;

        // A failure costs its own entry's work alone: a retry fact for it, and what moved ends what it held.
        var kept = true;
        foreach (var (entry, why) in broken) kept &= await WriteRetryAsync(Cause.Of(entry), BusFlowScope.Entry, [], 1, why, Settles(entry)).ConfigureAwait(false);
        foreach (var (entry, keys) in reached)
        {
            var failed = keys.Where(key => outcomes[key].Result == BusFlowResult.Failed).ToList();
            var settles = Settles(entry) && !keys.Any(key => outcomes[key].Result == BusFlowResult.NotMoved);
            if (failed.Count == 0)
            {
                if (settles) _host?.EndLate(entry.Id);
                continue;
            }

            kept &= await WriteRetryAsync(Cause.Of(entry), BusFlowScope.Key, failed, 1, outcomes[failed[0]].Why, settles).ConfigureAwait(false);
        }

        // A retry fact the log did not keep leaves the row unacknowledged, so the queue hands it again rather than the failure being lost.
        if (!kept) throw new IOException("a retry fact was not kept");
        return BusHandled.Judged;
    }

    /// <summary>An entry whose work failed, as a retry names it and its give-up reports it.</summary>
    private sealed record Cause(string Id, string Kind, string Subject, DateTimeOffset At, long Seq)
    {
        public static Cause Of(BusEnvelope entry) => new(entry.Id, entry.Kind, entry.Subject, entry.At, entry.Seq);
    }

    /// <summary>
    /// The retry fact for <paramref name="cause"/>'s work on <paramref name="keys"/>, due after its type's backoff for <paramref name="attempt"/>,
    /// on a subject of its own so no fact's lane waits behind it; the give-up where its type's retries are spent. False where the log did not keep it.
    /// </summary>
    private async Task<bool> WriteRetryAsync(Cause cause, string scope, IReadOnlyList<string> keys, int attempt, string why, bool settles)
    {
        if (_host is not { } host || !host.Owned()) return true;
        host.Log.Measured(BusMeasures.Consumer, $"{Name}:threw", 1);
        host.Log.Warn($"queue:threw:{Name}:{cause.Kind}", "bus.consumer-threw", ("consumer", Name), ("kind", cause.Kind), ("why", why));
        var type = BusTypes.Of(cause.Kind);
        if (type is null || attempt > type.Retries)
        {
            BusConsumerRun.GiveUp(host.Log, Name, Obliged, cause.Id, cause.Kind, cause.Subject, cause.At, type, attempt);
            return true;
        }

        var wait = TimeSpan.FromTicks(Math.Min(host.Options.RetryMost.Ticks, host.Options.RetryFirst.Ticks << Math.Min(20, attempt - 1)));
        // More failed keys than a fact lists, or a key the fact cannot carry (it would be dropped, leaving nothing to retry), are
        // retried as the whole entry, its pieces named again.
        var listed = keys.Count <= BusShapes.MostListed && keys.All(key => key.Length > 0 && BusShapes.Short(key));
        return await AppendAsync(host.Log, new BusDraft(BusTypes.FlowRetry, $"retry:{Name}:{cause.Id}")
        {
            Commit = true,
            Causes = [cause.Id],
            Payload =
            [
                ("consumer", Name), ("kind", cause.Kind), ("scope", listed ? scope : BusFlowScope.Entry), ("seq", cause.Seq), ("keys", listed ? keys : (IReadOnlyList<string>)[]),
                ("attempt", (long)attempt), ("due", host.Options.Clock.GetUtcNow() + wait), ("since", cause.At), ("settles", settles), ("subject", cause.Subject),
            ],
        }).ConfigureAwait(false);
    }

    /// <summary>The write of a retry fact, waited on until the store has it: whether the log kept it.</summary>
    protected virtual async Task<bool> AppendAsync(BusLog log, BusDraft draft)
    {
        if (!log.Append(draft)) return false;
        await log.FlushAsync().ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// A retry fact handed back (the queue holds it until its due time): it runs its entry's failed keys through the same pass as any fact.
    /// The entry's obligation ends once every key has moved, one that moved is not listed again; any that failed write the next attempt,
    /// kept by the log before this one is acknowledged, so a crash repeats a retry and never loses one.
    /// </summary>
    private async Task<BusHandled> RetryAsync(BusEnvelope retry, CancellationToken ct)
    {
        if (Text(retry, "consumer") != Name || _host is not { } host) return BusHandled.Judged;
        if (!host.Owned() || retry.Causes.Count == 0) return BusHandled.Judged;
        var cause = new Cause(retry.Causes[0], Text(retry, "kind") ?? "", Text(retry, "subject") ?? retry.Subject, Moment(retry, "since"), Number(retry, "seq"));
        var attempt = (int)Number(retry, "attempt");
        var scope = Text(retry, "scope") ?? BusFlowScope.Key;
        var settles = retry.Payload.FirstOrDefault(pair => pair.Key == "settles").Value is true;
        var keys = retry.Payload.FirstOrDefault(pair => pair.Key == "keys").Value as IReadOnlyList<string> ?? [];
        var outcomes = new List<(string Key, BusFlowOutcome Outcome)>();
        try
        {
            if (scope == BusFlowScope.Key)
            {
                foreach (var key in keys) outcomes.Add((key, await RetryKeyAsync(key, ct).ConfigureAwait(false)));
            }
            else outcomes.Add(("", await RetryEntryAsync(host, cause, ct).ConfigureAwait(false)));
        }
        catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException)
        {
            outcomes.Add(("", BusFlowOutcome.Failed(error.Message)));
        }

        var failed = outcomes.Where(each => each.Outcome.Result == BusFlowResult.Failed).ToList();
        var open = outcomes.Any(each => each.Outcome.Result == BusFlowResult.NotMoved);
        if (failed.Count == 0)
        {
            if (!open && settles && host.Owned()) host.EndLate(cause.Id);
            return BusHandled.Judged;
        }

        // Only the keys still failing are listed again; an entry-scope failure names none, so its pieces are named again.
        var again = scope == BusFlowScope.Key && failed.All(each => each.Key.Length > 0) ? failed.Select(each => each.Key).ToList() : [];
        if (!await WriteRetryAsync(cause, again.Count > 0 ? BusFlowScope.Key : BusFlowScope.Entry, again, attempt + 1, failed[0].Outcome.Why, settles && !open).ConfigureAwait(false)) throw new IOException("a retry fact was not kept");
        return BusHandled.Judged;
    }

    private async Task<BusFlowOutcome> RetryKeyAsync(string key, CancellationToken ct) =>
        await RetryWorkAsync(key, ct).ConfigureAwait(false) is { } piece ? await AskAsync(piece, ct).ConfigureAwait(false) : BusFlowOutcome.Moved;

    /// <summary>An entry whose pieces could not be named: named again from the entry as the log keeps it, each piece asked.</summary>
    private async Task<BusFlowOutcome> RetryEntryAsync(BusConsumerHost host, Cause cause, CancellationToken ct)
    {
        if (host.Find(cause.Seq, cause.Kind, cause.Id) is not { } entry) return BusFlowOutcome.NotMoved("the entry is gone from the log");
        var named = await PiecesAsync(entry, ct).ConfigureAwait(false);
        var outcomes = await Task.WhenAll(named.Select(piece => AskAsync(piece, ct))).ConfigureAwait(false);
        return outcomes.FirstOrDefault(each => each.Result == BusFlowResult.Failed) is { Result: BusFlowResult.Failed } failed ? failed
            : outcomes.Any(each => each.Result == BusFlowResult.NotMoved) ? BusFlowOutcome.NotMoved("not moved") : BusFlowOutcome.Moved;
    }

    private static string? Text(BusEnvelope entry, string key) => entry.Payload.FirstOrDefault(pair => pair.Key == key).Value as string;

    private static long Number(BusEnvelope entry, string key) => entry.Payload.FirstOrDefault(pair => pair.Key == key).Value switch { long number => number, int number => number, _ => 0 };

    private static DateTimeOffset Moment(BusEnvelope entry, string key) => entry.Payload.FirstOrDefault(pair => pair.Key == key).Value switch
    {
        DateTimeOffset at => at,
        long ms => DateTimeOffset.FromUnixTimeMilliseconds(ms),
        _ => DateTimeOffset.UnixEpoch,
    };

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

/// <summary>What a retry fact retries: one key's work, or an entry whose pieces could not be named.</summary>
internal static class BusFlowScope
{
    public const string Key = "key";

    public const string Entry = "entry";
}
