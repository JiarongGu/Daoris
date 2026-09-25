namespace Daoris.Driver;

/// <summary>What stopping a turn did (CONV4a).</summary>
/// <param name="Cancelled">
/// A turn had reached the harness and was asked to stop. It ends on the harness's own word, as a
/// <c>cancelled</c> turn in the record — this is the asking, not the ending.
/// </param>
/// <param name="Withdrawn">
/// What the person had sent that had not reached the harness, in the order sent — withdrawn and handed
/// back, never sent, so nothing they queued fires after they said stop.
/// </param>
public sealed record TurnStop(bool Cancelled, IReadOnlyList<ChatMessage> Withdrawn)
{
    /// <summary>Nothing was running and nothing was waiting: an answer, never an error.</summary>
    public static readonly TurnStop Nothing = new(false, []);
}

/// <summary>Where a conversation's turns stand, as the page is told it (CONV4a, CONV4b).</summary>
/// <param name="Taking">A turn is on its way to the harness or running there — what a stop would act on.</param>
/// <param name="Queued">What the person sent that has not reached the harness, in the order sent.</param>
public sealed record ChatQueue(bool Taking, IReadOnlyList<ChatMessage> Queued)
{
    /// <summary>Nothing running and nothing waiting — a conversation between turns, or none at all.</summary>
    public static readonly ChatQueue Idle = new(false, []);
}

/// <summary>
/// One conversation's turns: one at a time, in the order the person sent them — the same on both doors
/// (CONV3b, CONV4a).
/// </summary>
/// <remarks>
/// <para><b>A message sent while a turn runs waits for it</b>, and the door records it when it is sent,
/// not when it is typed. Recorded at once, it would sit inside the turn before it, and the conversation
/// would read as though the agent answered a question it had not been asked. The native door used to
/// write it to the harness at once, where Claude Code may fold it into the running turn — so both doors
/// hold it here instead, and neither depends on what a harness does with a line mid-turn.</para>
///
/// <para><b>Where the turns stand is told as it moves</b> (<paramref name="changed"/>): whether a turn is
/// in flight, which the page's stop follows — the record lags it — and what is waiting, which the page
/// shows as queued because nothing else shows it. The one the door is still opening for counts as
/// waiting; one the door takes at once is never announced.</para>
///
/// <para><b>A stop withdraws what was waiting and stops the turn in flight</b>, in that order: stopped
/// the other way round, the next message would start the moment the stopped turn ended. A stop that
/// lands after a turn was taken but before it reached the harness waits for it to be sent, so its
/// interrupt never overtakes the turn it means to stop.</para>
/// </remarks>
/// <param name="ready">
/// Whether the door can take a turn — the protocol door's session open. False means it never will, and
/// everything waiting is dropped.
/// </param>
/// <param name="take">
/// Send one turn and complete when it ends. It calls its second argument the moment the turn is on the
/// wire; a turn it could not send completes without calling it.
/// </param>
/// <param name="interrupt">The door's own way of stopping the turn in flight.</param>
/// <param name="changed">Where the turns stand, each time that changes.</param>
internal sealed class ChatTurns(
    Func<Task<bool>> ready,
    Func<ChatMessage, Action, Task> take,
    Func<Task> interrupt,
    Action<ChatQueue> changed)
{
    private readonly object _gate = new();
    private readonly List<ChatMessage> _waiting = [];
    private ChatMessage? _holding;
    private bool _pumping;
    private bool _inFlight;
    private bool _sent;
    private bool _stopPending;
    private bool _finishing;
    private bool _gone;
    private ChatQueue _published = ChatQueue.Idle;
    private TaskCompletionSource _drained = Completed();

    /// <summary>Where the turns stand now — what a page that just opened the conversation is told.</summary>
    public ChatQueue State
    {
        get
        {
            lock (_gate) return new ChatQueue(_pumping, Snapshot());
        }
    }

    /// <summary>Whether a turn is on its way to the harness or running there.</summary>
    public bool Running
    {
        get
        {
            lock (_gate) return _pumping;
        }
    }

    /// <summary>Queue a turn. False once the conversation is finishing or gone: nothing more will be heard.</summary>
    public bool Say(ChatMessage message)
    {
        bool start;
        lock (_gate)
        {
            if (_finishing || _gone) return false;
            _waiting.Add(message);
            start = !_pumping;
            if (start)
            {
                _pumping = true;
                _drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        // Started outside the lock: a door that takes a turn at once runs its send before this returns,
        // so the message it took is never announced as waiting.
        if (start) _ = PumpAsync();
        lock (_gate) Publish();
        return true;
    }

    /// <summary>
    /// Withdraw what is waiting, then stop the turn in flight. <see cref="TurnStop.Nothing"/> when there
    /// was neither.
    /// </summary>
    public async Task<TurnStop> StopAsync()
    {
        List<ChatMessage> withdrawn = [];
        bool cancelled;
        bool now;
        lock (_gate)
        {
            if (_holding is not null)
            {
                withdrawn.Add(_holding);
                _holding = null;
            }

            withdrawn.AddRange(_waiting);
            _waiting.Clear();
            cancelled = _inFlight;
            // Taken but not yet on the wire: the interrupt goes the moment it is (see `take`).
            now = _inFlight && _sent;
            if (_inFlight && !_sent) _stopPending = true;
            Publish();
        }

        if (now) await interrupt().ConfigureAwait(false);
        return !cancelled && withdrawn.Count == 0 ? TurnStop.Nothing : new TurnStop(cancelled, withdrawn);
    }

    /// <summary>
    /// Take no more, and complete once every turn already asked for has ended — what finishing waits on
    /// before it ends the input, on either door.
    /// </summary>
    public Task FinishAsync()
    {
        lock (_gate)
        {
            _finishing = true;
            return _drained.Task;
        }
    }

    /// <summary>The harness has gone: nothing waiting will be sent, and nothing more is taken.</summary>
    public void Gone()
    {
        lock (_gate)
        {
            _gone = true;
            _holding = null;
            _waiting.Clear();
            Publish();
        }
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            ChatMessage message;
            lock (_gate)
            {
                if (_waiting.Count == 0 || _gone)
                {
                    _pumping = false;
                    _drained.TrySetResult();
                    Publish();
                    return;
                }

                message = _waiting[0];
                _waiting.RemoveAt(0);
                _holding = message;
            }

            var readying = ready();
            if (!readying.IsCompleted)
            {
                // The door is still opening: the message is waiting on it, and says so.
                lock (_gate) Publish();
            }

            bool open;
            try
            {
                open = await readying.ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or IOException or InvalidOperationException
                                              or OperationCanceledException)
            {
                open = false;
            }

            lock (_gate)
            {
                if (!open)
                {
                    _gone = true;
                    _holding = null;
                    _waiting.Clear();
                    continue;
                }

                // Withdrawn by a stop while the door opened: never sent.
                if (_holding is null) continue;
                _holding = null;
                _inFlight = true;
                _sent = false;
                _stopPending = false;
                Publish();
            }

            try
            {
                await take(message, Sent).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The door says what failed in its own words; the queue outlives one bad turn, or every
                // later message would wait on a pump that is gone.
            }
            finally
            {
                lock (_gate)
                {
                    _inFlight = false;
                    _sent = false;
                    _stopPending = false;
                }
            }
        }
    }

    /// <summary>The turn is on the wire — and a stop that arrived while it was on its way goes now.</summary>
    private void Sent()
    {
        bool late;
        lock (_gate)
        {
            _sent = true;
            late = _stopPending;
            _stopPending = false;
        }

        if (late) _ = interrupt();
    }

    private List<ChatMessage> Snapshot()
    {
        List<ChatMessage> now = _holding is null ? [] : [_holding];
        now.AddRange(_waiting);
        return now;
    }

    /// <summary>Tell where the turns stand when it differs from what was last told. Called under the gate, so told in order.</summary>
    private void Publish()
    {
        var now = new ChatQueue(_pumping, Snapshot());
        if (now.Taking == _published.Taking && now.Queued.SequenceEqual(_published.Queued)) return;
        _published = now;
        try
        {
            changed(now);
        }
        catch (Exception)
        {
            // A listener's failure is its own; the queue has moved either way.
        }
    }

    private static TaskCompletionSource Completed()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        done.SetResult();
        return done;
    }
}
