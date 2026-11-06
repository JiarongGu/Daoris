using System.Threading.Channels;

namespace Daoris.Driver;

/// <summary>
/// The point <c>work/event</c>'s teller (EVENTHOOK1b; the event hook design §4.1, §4.4, §4.5): one <see cref="Run"/>, one count of every
/// event minted, under one lock, and a queue and a teller for each plugin that listens, made only once one does.
/// </summary>
/// <remarks>
/// <para><b>Every <c>seq</c> is minted, told or not.</b> <see cref="Tell"/> counts each event whether or not the set holds the lease and
/// whether or not a plugin listens, so a relay sees anything not told as a gap, and a plugin's handshake counts what it was not told.</para>
/// <para><b>One order per plugin, and nobody waits.</b> The event is stamped and written to each listening plugin's queue under the lock; a
/// queue's write never waits (a full one drops its oldest, which the next frame told says as <c>missed</c>), and each plugin's teller awaits
/// each answer before the next, so a slow plugin holds only itself.</para>
/// <para><b>Three late answers in a row stop that plugin.</b> Its queue goes and the set is told, which stops its process and counts the stop
/// as an exit of its own (§4.4).</para>
/// </remarks>
/// <param name="adapters">This build's adapter names, the only ones an ending may carry.</param>
/// <param name="unsent">Said once for an event whose required field failed: its kind and that field, never the value.</param>
/// <param name="late">Said for a plugin stopped for answering late <see cref="LateInARow"/> times in a row.</param>
/// <param name="failed">Said for each call that failed, contained: the plugin and the failure.</param>
/// <param name="run">The run, where a trial gives <see cref="WorkEventGrammar.Sample"/>; otherwise one is minted.</param>
public sealed class WorkEventPoint(
    IReadOnlySet<string> adapters,
    Action<string, string>? unsent = null,
    Action<string>? late = null,
    Action<string, DriverException>? failed = null,
    string? run = null) : IAsyncDisposable
{
    /// <summary>How many late answers in a row stop a plugin at the point.</summary>
    public const int LateInARow = 3;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, Listener> _listeners = new(StringComparer.Ordinal);
    private long _seq;
    private bool _held;

    /// <summary>This set's run: twelve hex minted once, or a trial's <see cref="WorkEventGrammar.Sample"/>.</summary>
    public string Run { get; } = run ?? WorkEventGrammar.MintRun();

    /// <summary>The last <c>seq</c> minted, 0 before any.</summary>
    public long Seq
    {
        get { lock (_gate) return _seq; }
    }

    /// <summary>Whether the set holds the home's lease, as its reconcile last read it: nothing is told where it does not.</summary>
    public bool Held
    {
        get { lock (_gate) return _held; }
        set { lock (_gate) _held = value; }
    }

    /// <summary>The plugins whose queue is attached, by id.</summary>
    public IReadOnlyList<string> Listening
    {
        get { lock (_gate) return [.. _listeners.Keys.Order(StringComparer.Ordinal)]; }
    }

    /// <summary>
    /// One event minted and, where the set holds the lease, written to every listening plugin's queue in <c>seq</c> order. Never waits.
    /// </summary>
    public void Tell(WorkEvent told)
    {
        string? field;
        lock (_gate)
        {
            var seq = ++_seq;
            if (!_held || _listeners.Count == 0) return;
            var built = HookFrames.Event(told, Run, seq, 0, adapters);
            field = built.Unsent;
            foreach (var listener in _listeners.Values) listener.Queue.Writer.TryWrite(built.Frame);
        }

        if (field is not null) unsent?.Invoke(told.Kind, field);
    }

    /// <summary>
    /// A plugin's queue attached, before its process is asked to start: answers the last <c>seq</c> minted, which its handshake says, so the
    /// first event it hears is the one after it. Joining again keeps the queue it has.
    /// </summary>
    public long Join(string plugin)
    {
        lock (_gate)
        {
            if (!_listeners.ContainsKey(plugin)) _listeners[plugin] = new Listener();
            return _seq;
        }
    }

    /// <summary>The plugin's teller started, once its process answers: what waits in its queue is told first, in order.</summary>
    /// <param name="send">The call at the point; its answer is awaited before the next frame.</param>
    public void Listen(string plugin, Func<object, CancellationToken, Task> send)
    {
        Listener? listener;
        lock (_gate)
        {
            if (!_listeners.TryGetValue(plugin, out listener) || listener.Telling is not null) return;
            listener.Telling = Task.Run(() => TellingAsync(plugin, listener, send));
        }
    }

    /// <summary>A plugin's queue and teller gone: what it was not told is counted by its next handshake's <c>seq</c>.</summary>
    public async Task LeaveAsync(string plugin)
    {
        Listener? listener;
        lock (_gate)
        {
            if (!_listeners.Remove(plugin, out listener)) return;
        }

        await listener.EndAsync().ConfigureAwait(false);
    }

    /// <summary>Every queue and teller gone.</summary>
    public async ValueTask DisposeAsync()
    {
        List<Listener> all;
        lock (_gate)
        {
            all = [.. _listeners.Values];
            _listeners.Clear();
        }

        foreach (var listener in all) await listener.EndAsync().ConfigureAwait(false);
    }

    private async Task TellingAsync(string plugin, Listener listener, Func<object, CancellationToken, Task> send)
    {
        var lates = 0;
        try
        {
            await foreach (var frame in listener.Queue.Reader.ReadAllAsync(listener.Going.Token).ConfigureAwait(false))
            {
                var missed = Interlocked.Exchange(ref listener.Dropped, 0);
                var sent = missed == 0 ? frame : new Dictionary<string, object?>(frame, StringComparer.Ordinal) { ["missed"] = missed };
                try
                {
                    await send(sent, listener.Going.Token).ConfigureAwait(false);
                    lates = 0;
                }
                catch (DriverException error)
                {
                    failed?.Invoke(plugin, error);
                    if (PluginFailures.KindOf(error, PluginEvents.Errored) != PluginEvents.Late) lates = 0;
                    else if (++lates >= LateInARow)
                    {
                        lock (_gate)
                        {
                            if (_listeners.TryGetValue(plugin, out var now) && now == listener) _listeners.Remove(plugin);
                        }

                        listener.Queue.Writer.TryComplete();
                        late?.Invoke(plugin);
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The plugin left, or the set went.
        }
    }

    /// <summary>One plugin's queue, bounded as the work wire's events are, and its teller.</summary>
    private sealed class Listener
    {
        public Listener() => Queue = Channel.CreateBounded<IReadOnlyDictionary<string, object?>>(
            new BoundedChannelOptions(WorkBounds.EventsWaiting) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true },
            _ => Interlocked.Increment(ref Dropped));

        public readonly Channel<IReadOnlyDictionary<string, object?>> Queue;

        public readonly CancellationTokenSource Going = new();

        // Dropped since the last frame told: the next one carries it as `missed`.
        public int Dropped;

        public Task? Telling;

        public async Task EndAsync()
        {
            Queue.Writer.TryComplete();
            await Going.CancelAsync().ConfigureAwait(false);
            if (Telling is { } telling) await telling.ConfigureAwait(false);
            Going.Dispose();
        }
    }
}
