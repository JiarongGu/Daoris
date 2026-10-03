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
/// <param name="LastTurnEnded">
/// When this machine last saw one of its turns end (RAIL2), or null before any has. A live chat's last
/// move: its record moves on state changes only, and writing it every turn would carry a chat's activity
/// to a teammate's machine (D47 §4), so it is told here, machine-local, and never recorded.
/// </param>
/// <param name="Opening">
/// The door is still opening (HELP4): what is queued waits for the session to be ready, not for a turn
/// to end. The page says so; told only <paramref name="Taking"/>, a conversation's first words read as
/// waiting behind a turn when nothing had answered yet.
/// </param>
public sealed record ChatQueue(
    bool Taking, IReadOnlyList<ChatMessage> Queued, DateTimeOffset? LastTurnEnded = null, bool Opening = false)
{
    /// <summary>Nothing running and nothing waiting — a conversation between turns, or none at all.</summary>
    public static readonly ChatQueue Idle = new(false, []);
}

/// <summary>What became of a word said to a conversation (MSG1c, D137 §2.1), in the reach the record and the page speak of.</summary>
public enum TurnReach
{
    /// <summary>No turn ran: it is the next turn, sent at once.</summary>
    Now,

    /// <summary>Handed to the turn on the wire at once, read at the agent's next step (D136).</summary>
    NextStep,

    /// <summary>Waiting for the running turn to end, then its own turn (CONV4a).</summary>
    TurnEnd,
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
/// <param name="steer">
/// The door's next step (MSG1c, D137 §2.1): hand a word to the turn on the wire at once, returning what completes when the
/// agent has answered it, or null where the door takes no word during a turn now. Called under the turns' own lock, so it
/// must not call back into them. Null for a door that never does (the native door, a text pipe).
/// </param>
internal sealed class ChatTurns(
    Func<Task<bool>> ready,
    Func<ChatMessage, Action, Task> take,
    Func<Task> interrupt,
    Action<ChatQueue> changed,
    Func<ChatMessage, Task?>? steer = null)
{
    private readonly object _gate = new();
    private readonly List<ChatMessage> _waiting = [];
    // The words handed to the turn in flight at its next step (MSG1c), each until the agent answered it: the turn lasts as long.
    private readonly List<Task> _steered = [];
    private ChatMessage? _holding;
    private bool _pumping;
    private bool _opening;
    private bool _inFlight;
    private bool _sent;
    private bool _stopPending;
    // The person stopped the turn in flight: what they say next waits for it to end, never joins a turn winding up (MSG1c).
    private bool _stopped;
    private bool _finishing;
    private bool _gone;
    private ChatQueue _published = ChatQueue.Idle;
    private TaskCompletionSource _drained = Completed();
    private DateTimeOffset? _lastEnded;

    /// <summary>Where the turns stand now — what a page that just opened the conversation is told.</summary>
    public ChatQueue State
    {
        get
        {
            lock (_gate) return Now();
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

    /// <summary>Whether the person stopped the turn in flight, which a word said now then waits for (MSG1c).</summary>
    public bool Stopped
    {
        get
        {
            lock (_gate) return _inFlight && _stopped;
        }
    }

    /// <summary>Queue a turn. False once the conversation is finishing or gone: nothing more will be heard.</summary>
    public bool Say(ChatMessage message) => Take(message) is not null;

    /// <summary>
    /// Take a word: handed to the turn on the wire where the door takes words at its next step and nothing waits before it
    /// (MSG1c), else queued as a turn of its own. Null once the conversation is finishing or gone: nothing more is heard.
    /// </summary>
    public TurnReach? Take(ChatMessage message)
    {
        bool start;
        lock (_gate)
        {
            if (_finishing || _gone) return null;

            // At its next step (D137 §2.1): only into a turn already on the wire, never one the person stopped, and never
            // ahead of a word still waiting, which would make the agent read them out of the order said.
            if (steer is not null && _inFlight && _sent && !_stopped && !_stopPending && _holding is null && _waiting.Count == 0
                && steer(message) is { } answered)
            {
                _steered.Add(answered);
                return TurnReach.NextStep;
            }

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
        return start ? TurnReach.Now : TurnReach.TurnEnd;
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
            if (_inFlight) _stopped = true;
            // Taken but not yet on the wire: the interrupt goes the moment it is (see `take`).
            now = _inFlight && _sent;
            if (_inFlight && !_sent) _stopPending = true;
            Publish();
        }

        if (now) await interrupt().ConfigureAwait(false);
        // The words a conversation goes on with are not handed back (MSG1c): the page never listed them, and they stay on
        // the record, which takes them off only once they went.
        withdrawn.RemoveAll(message => message.GoesOn is not null);
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
                lock (_gate)
                {
                    _opening = true;
                    Publish();
                }
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
                _opening = false;
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
                _stopped = false;
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

            // 🔴 The turn lasts until every word handed to it at its next step is answered (MSG1c, D137 §2.1): the agent
            // answers the prompt before them when it takes them, and works on. Found empty under the lock that ends the
            // turn, so no word is handed to a turn that has already ended.
            while (true)
            {
                Task[] steered;
                lock (_gate)
                {
                    steered = [.. _steered];
                    _steered.Clear();
                    if (steered.Length == 0)
                    {
                        // The end of a turn that reached the harness is the chat's last move (RAIL2), told
                        // at once: the next message may start straight away and leave nothing else changed
                        // for the page to hear. One that never went out moved nothing.
                        if (_sent)
                        {
                            _lastEnded = DateTimeOffset.UtcNow;
                            Publish();
                        }

                        _inFlight = false;
                        _sent = false;
                        _stopPending = false;
                        break;
                    }
                }

                try
                {
                    await Task.WhenAll(steered).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Each word's door says what became of it; the turn ends all the same.
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

    private ChatQueue Now() => new(_pumping, Snapshot(), _lastEnded, _opening);

    // The words a conversation goes on with are left out (MSG1c): its record shows them waiting already, at its foot.
    private List<ChatMessage> Snapshot()
    {
        List<ChatMessage> now = _holding is null || _holding.GoesOn is not null ? [] : [_holding];
        now.AddRange(_waiting.Where(message => message.GoesOn is null));
        return now;
    }

    /// <summary>Tell where the turns stand when it differs from what was last told. Called under the gate, so told in order.</summary>
    private void Publish()
    {
        var now = Now();
        if (now.Taking == _published.Taking && now.Queued.SequenceEqual(_published.Queued)
            && now.LastTurnEnded == _published.LastTurnEnded && now.Opening == _published.Opening) return;
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
