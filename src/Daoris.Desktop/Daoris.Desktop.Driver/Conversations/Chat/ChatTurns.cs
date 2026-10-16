namespace Daoris.Driver;

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
/// <param name="steerNow">
/// The person's *Send now* on that door (STEER2a, D136's 2026-10-16 note): steer a word into the turn on the wire at once,
/// cutting the step in flight, returning what completes when the agent has answered the steer, or null where the agent never
/// said it steers. Called under the turns' own lock, as <paramref name="steer"/> is. Null for a door that never does.
/// </param>
/// <param name="nextStep">
/// Whether the door takes words at the turn's next step now (MSG1c), asked without sending anything: on such a door *Send now*
/// never stops a turn, since a word may be on its way (D136 point 4). Called under the turns' own lock. Null for a door that
/// never does.
/// </param>
internal sealed class ChatTurns(
    Func<Task<bool>> ready,
    Func<ChatMessage, Action, Task> take,
    Func<Task> interrupt,
    Action<ChatQueue> changed,
    Func<ChatMessage, Task?>? steer = null,
    Func<ChatMessage, Task?>? steerNow = null,
    Func<bool>? nextStep = null)
{
    private readonly object _gate = new();
    private readonly List<ChatMessage> _waiting = [];
    // The words handed to the turn in flight at its next step (MSG1c), each until the agent answered it: the turn lasts as long.
    private readonly List<Task> _steered = [];
    // Of those, the words sent at its next step and not yet answered: on their way, which no stop and no steer may overtake
    // (D136 point 4, STEER2a). Pruned as they are answered.
    private readonly List<Task> _onTheirWay = [];
    // Each turn put in flight, counted (STEER2a): a stop is decided for one turn, and the next turn waits until that stop is
    // on the wire or dropped, so a stop that lands late never cuts the turn after the one it was meant for.
    private int _turn;
    private Task _stopping = Task.CompletedTask;
    private ChatMessage? _holding;
    // The ids this queue gave the words that wait (QUEUEEDIT1a): the page's handle on one, never carried into the turn it
    // becomes, which a conversation's record has always shown without.
    private readonly HashSet<string> _stamped = [];
    private int _said;
    private bool _pumping;
    private bool _opening;
    private bool _inFlight;
    private bool _sent;
    private bool _stopPending;
    // The person stopped the turn in flight: what they say next waits for it to end, never joins a turn winding up (MSG1c).
    private bool _stopped;
    private bool _finishing;
    // Held for a hand-over's take (SWITCH1c-ii): no word starts a turn the take's end would cut.
    private bool _held;
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

    /// <summary>
    /// The line a conversation keeps of a word handed to its turn at the next step (MSG1c, D137 §2.1), once the agent answered
    /// it: where the person's stop was asked while the word was on its way and the agent answered it <c>cancelled</c>, the
    /// agent may have read it and its answer was not kept (STEER1 §3). Null for any other answer, which is in the turn.
    /// </summary>
    /// <remarks>
    /// Its code (MSG1c3), which the page words as <c>work.conversation.lost</c> in the reader's language, and the word's id, so
    /// the page settles the word it still shows waiting (MSG1f). The English beside them is the console's and the terminal's.
    /// </remarks>
    /// <param name="word">The word's id, as the record showed it waiting.</param>
    /// <param name="stoppedUnder">The person's stop was asked while the word was on its way.</param>
    /// <param name="stopReason">The agent's answer to the word.</param>
    public static SessionEvent? Lost(string word, bool stoppedUnder, string stopReason) =>
        stoppedUnder && stopReason == "cancelled"
            ? new SessionEvent { Kind = SessionEventKind.Note, Text = LostLine, Words = [word], Code = SessionEventCodes.Lost }
            : null;

    /// <summary>The driver's English for <see cref="Lost"/>: what its console says and a page that does not know the code shows.</summary>
    public const string LostLine = "— it may have read what you added; its answer was not kept.";

    /// <summary>
    /// The line a driven session's record keeps of a held word the person took back or reworded (QUEUEEDIT1a): its code, which
    /// the page words as <c>work.conversation.withdrawn</c>, and the word's id, so the page settles the word it showed waiting.
    /// </summary>
    public static SessionEvent Withdrawn(string word) => SessionNotes.Withdrawn(word);

    /// <summary>The driver's English for <see cref="Withdrawn"/>.</summary>
    public const string WithdrawnLine = SessionNotes.WithdrawnLine;

    /// <summary>
    /// Take back or reword one word that still waits (QUEUEEDIT1a): with no <paramref name="text"/> it is removed, with some its
    /// words are replaced where it waits, its place and its files kept, under a new id. The word taken while the door opens is
    /// still the queue's. <see cref="QueueEdit.Taken"/> where a turn took it already. A conversation's queued words are in no
    /// record until taken (D76), so nothing is written.
    /// </summary>
    public QueueEdit EditQueued(string id, string? text)
    {
        lock (_gate)
        {
            if (_holding is { OnRecord: false } holding && holding.Id == id)
            {
                _holding = text is null ? null : Reworded(holding, text);
            }
            else
            {
                var at = _waiting.FindIndex(message => !message.OnRecord && message.Id == id);
                if (at < 0) return QueueEdit.Taken;
                if (text is null) _waiting.RemoveAt(at);
                else _waiting[at] = Reworded(_waiting[at], text);
            }

            Publish();
            return QueueEdit.Done;
        }
    }

    private ChatMessage Reworded(ChatMessage message, string text)
    {
        var id = $"said-{++_said}";
        _stamped.Add(id);
        return message with { Text = text, Id = id };
    }

    /// <summary>Queue a turn. False once the conversation is finishing, held or gone: nothing more will be heard.</summary>
    public bool Say(ChatMessage message) => Take(message) is not null;

    /// <summary>
    /// Hold the turns for a hand-over's take (SWITCH1c-ii; the switch design §4, never mid-turn): true where no turn runs and no
    /// word waits, and from then on a word takes nothing (<see cref="Take"/> is null) until <see cref="Release"/>. False where a
    /// turn runs or a word waits, whose turn ends first; or where the conversation is finishing or gone.
    /// </summary>
    /// <remarks>
    /// Asked and set under the turns' own lock, so a word said as a move begins either started its turn first, and the move waits
    /// for that turn's end, or finds the turns held. Checked first and held after, a word between the two started a turn the take's
    /// end then cut.
    /// </remarks>
    public bool Hold()
    {
        lock (_gate)
        {
            if (_pumping || _holding is not null || _waiting.Count > 0 || _finishing || _gone || _held) return false;
            _held = true;
            return true;
        }
    }

    /// <summary>Take words again after <see cref="Hold"/>: the take did not end the conversation, or it has ended and nothing listens.</summary>
    public void Release()
    {
        lock (_gate) _held = false;
    }

    /// <summary>
    /// Take a word: handed to the turn on the wire where the door takes words at its next step and nothing waits before it
    /// (MSG1c), else queued as a turn of its own. Null once the conversation is finishing, held for a hand-over's take, or gone:
    /// nothing more is heard.
    /// </summary>
    public TurnReach? Take(ChatMessage message)
    {
        bool start;
        lock (_gate)
        {
            if (_finishing || _gone || _held) return null;

            // At its next step (D137 §2.1): only into a turn already on the wire, never one the person stopped, and never
            // ahead of a word still waiting, which would make the agent read them out of the order said.
            // A helper's answer is a turn of its own, never handed to the running one (MIXAGENT2a-iii).
            if (steer is not null && message.Helped is null && _inFlight && _sent && !_stopped && !_stopPending && _holding is null
                && _waiting.Count == 0 && steer(message) is { } answered)
            {
                _steered.Add(answered);
                _onTheirWay.Add(answered);
                return TurnReach.NextStep;
            }

            _waiting.Add(Stamped(message));
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
    /// The person's *Send now* (STEER2a, D136's 2026-10-16 note), carried out by the door it has: with no turn running the words
    /// are the next turn at once; into a turn on the wire whose agent steers, they are steered at once (<c>now</c>) and the turn
    /// lasts until the steer is answered; on a door that takes words only at its next step they go there, said so; on a door
    /// that takes none during a turn they wait as <see cref="Take"/> queues them and the turn is stopped keeping them
    /// (<see cref="StopKeepingAsync"/>), so they are the next turn (D90). Null once the conversation is finishing, held or gone.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>On a next-step door nothing is stopped and nothing overtaken</b> (D136 point 4): with a word on its way, a stop
    /// would answer it cancelled while the agent may still act on it (evidence §3), and a steer would have the agent read the
    /// later words first. Behind words on their way or waiting, the words go behind them, said <see cref="SendNowWhy.Behind"/>.</para>
    ///
    /// <para>Judged and done under one lock while a turn is in flight: queued and stopped in two steps, a turn ending between
    /// them would hand the words their own turn, and the stop would cut it.</para>
    /// </remarks>
    public async Task<SentNow?> TakeNowAsync(ChatMessage message)
    {
        Stop? stop = null;
        SentNow? queued = null;
        lock (_gate)
        {
            if (_finishing || _gone || _held) return null;
            if (_inFlight && message.Helped is null)
            {
                // Words before these: handed to this turn at its next step and not yet answered, or waiting as turns of their
                // own (a helper's answer among them). A helper's answer is never the person's press (MIXAGENT2a-iii).
                var onTheirWay = OnTheirWay();
                var behind = onTheirWay || _holding is not null || _waiting.Count > 0;
                var running = _sent && !_stopped && !_stopPending;
                if (running && !behind && steerNow?.Invoke(message) is { } steered)
                {
                    _steered.Add(steered);
                    return new SentNow(SendNowWhy.Spelled(TurnReach.Steered), Now: true, Why: null);
                }

                // At its next step, behind any word on its way, as a word said then goes; never ahead of one still waiting.
                if (running && _holding is null && _waiting.Count == 0 && steer?.Invoke(message) is { } stepped)
                {
                    _steered.Add(stepped);
                    _onTheirWay.Add(stepped);
                    return new SentNow(
                        SendNowWhy.Spelled(TurnReach.NextStep), Now: false, onTheirWay ? SendNowWhy.Behind : SendNowWhy.NoInterrupt);
                }

                _waiting.Add(Stamped(message));
                if (_stopped || _stopPending)
                {
                    // The stop already asked carries them next.
                    queued = new SentNow(SendNowWhy.Spelled(TurnReach.TurnEnd), Now: true, Why: null);
                }
                else if (onTheirWay || nextStep?.Invoke() == true)
                {
                    queued = new SentNow(
                        SendNowWhy.Spelled(TurnReach.TurnEnd), Now: false, behind ? SendNowWhy.Behind : SendNowWhy.Opening);
                }
                else
                {
                    stop = StopKeeping();
                    queued = new SentNow(SendNowWhy.Spelled(TurnReach.TurnEnd), Now: true, Why: null);
                }

                Publish();
            }
        }

        if (stop is not null) await SendStopAsync(stop).ConfigureAwait(false);
        if (queued is not null) return queued;

        // No turn in flight: the next turn at once, or behind the one the door is still opening for.
        return Take(message) switch
        {
            null => null,
            TurnReach.Now => new SentNow(null, Now: true, Why: null),
            var reach => new SentNow(SendNowWhy.Spelled(reach), Now: false, SendNowWhy.Opening),
        };
    }

    /// <summary>
    /// Stop the turn in flight and keep what waits (STEER2a, <c>CANCEL_TURN</c> with <c>keep</c>): nothing is withdrawn, and the
    /// words waiting are the next turn once it ends (D90), where <see cref="StopAsync"/> hands them back (CONV4a). A stop already
    /// asked is not asked again. <see cref="TurnStop.Nothing"/> when no turn is in flight.
    /// </summary>
    public async Task<TurnStop> StopKeepingAsync()
    {
        Stop? stop;
        lock (_gate)
        {
            if (!_inFlight) return TurnStop.Nothing;
            stop = StopKeeping();
            Publish();
        }

        if (stop is not null) await SendStopAsync(stop).ConfigureAwait(false);
        return new TurnStop(Cancelled: true, Withdrawn: []);
    }

    /// <summary>
    /// Mark the turn in flight stopped, keeping what waits: the stop to send now, or null. Taken but not yet on the wire, the
    /// interrupt goes the moment it is (see <c>take</c>); a stop already asked is not asked again. Called under the lock.
    /// </summary>
    private Stop? StopKeeping()
    {
        var asked = _stopped || _stopPending;
        _stopped = true;
        if (!_sent) _stopPending = true;
        return _sent && !asked ? Decide() : null;
    }

    /// <summary>A stop decided for one turn (STEER2a), and what tells the next turn it is on the wire or dropped.</summary>
    private sealed record Stop(int Turn, TaskCompletionSource Sent);

    /// <summary>Decide a stop for the turn in flight, which the next turn waits for (<see cref="PumpAsync"/>). Called under the lock.</summary>
    private Stop Decide()
    {
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _stopping = _stopping.IsCompleted ? sent.Task : Task.WhenAll(_stopping, sent.Task);
        return new Stop(_turn, sent);
    }

    /// <summary>
    /// Send a stop to the turn it was decided for, or drop it where that turn has ended (STEER2a); either way the next turn may
    /// then go, never before. Called outside the lock: the door's interrupt is its own.
    /// </summary>
    private async Task SendStopAsync(Stop stop)
    {
        try
        {
            bool current;
            lock (_gate) current = _inFlight && _turn == stop.Turn;
            if (current) await interrupt().ConfigureAwait(false);
        }
        finally
        {
            stop.Sent.TrySetResult();
        }
    }

    /// <summary>Whether a word handed to the turn at its next step is still unanswered: on its way. Called under the lock.</summary>
    private bool OnTheirWay()
    {
        _onTheirWay.RemoveAll(word => word.IsCompleted);
        return _onTheirWay.Count > 0;
    }

    /// <summary>The queue's id for a word that has none (QUEUEEDIT1a): the page's handle on it while it waits. Called under the lock.</summary>
    private ChatMessage Stamped(ChatMessage message)
    {
        if (message.Id is not null || message.OnRecord) return message;
        var id = $"said-{++_said}";
        _stamped.Add(id);
        return message with { Id = id };
    }

    /// <summary>
    /// Withdraw what is waiting, then stop the turn in flight. <see cref="TurnStop.Nothing"/> when there
    /// was neither.
    /// </summary>
    public async Task<TurnStop> StopAsync()
    {
        List<ChatMessage> withdrawn = [];
        bool cancelled;
        Stop? stop;
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
            // Taken but not yet on the wire: the interrupt goes the moment it is (see `take`). A word said after the stop waits
            // for it, so it too is sent for this turn alone (STEER2a).
            stop = _inFlight && _sent ? Decide() : null;
            if (_inFlight && !_sent) _stopPending = true;
            Publish();
        }

        if (stop is not null) await SendStopAsync(stop).ConfigureAwait(false);
        // The words a conversation goes on with are not handed back (MSG1c): the page never listed them, and they stay on
        // the record, which takes them off only once they went. Nor is a helper's answer (MIXAGENT2a-iii): it is no word of the
        // person's to put back in the composer, and it stays on the record for a later turn.
        withdrawn.RemoveAll(message => message.OnRecord);
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

            // 🔴 A stop decided for the turn before is on the wire, or dropped, before this one goes (STEER2a): sent after it, the
            // stop would cut this turn, whose words have left the queue and would be lost. No stop is decided meanwhile, since
            // none is decided with no turn in flight.
            Task stopping;
            lock (_gate) stopping = _stopping;
            await stopping.ConfigureAwait(false);

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
                // As it stands now: reworded while the door opened (QUEUEEDIT1a), and without the queue's id.
                message = _holding;
                if (message.Id is { } stamped && _stamped.Remove(stamped)) message = message with { Id = null };
                _holding = null;
                _inFlight = true;
                _turn++;
                _sent = false;
                _stopPending = false;
                _stopped = false;
                Publish();
            }

            try
            {
                await take(message, () =>
                {
                    Sent();
                    // A helper's answer comes off the record once its turn is on the wire (MIXAGENT2a-iii).
                    message.Delivered?.Invoke();
                }).ConfigureAwait(false);
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
        Stop? late;
        lock (_gate)
        {
            _sent = true;
            late = _stopPending ? Decide() : null;
            _stopPending = false;
        }

        if (late is not null) _ = SendStopAsync(late);
    }

    private ChatQueue Now() => new(_pumping, Snapshot(), _lastEnded, _opening);

    // The words a conversation goes on with are left out (MSG1c): its record shows them waiting already, at its foot.
    private List<ChatMessage> Snapshot()
    {
        List<ChatMessage> now = _holding is null || _holding.OnRecord ? [] : [_holding];
        now.AddRange(_waiting.Where(message => !message.OnRecord));
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
