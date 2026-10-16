namespace Daoris.Driver;

/// <summary>
/// What a person tells a driven session while it works (SESS3), handed over where the protocol door allows: at once,
/// where the agent takes a prompt during its turn (STEER1, D136); else as the next prompt of the same session when the
/// running turn ends, or at once when the person stops the turn to send it.
/// </summary>
/// <remarks>
/// <para><b>Why a prompt and never a line.</b> A driven session is handed its whole quest in one turn, and on the
/// protocol door its stdin carries the driver's own frames (D53), which is why it takes no person's line (INT4i). A
/// prompt is the door's own way of adding words. Where the agent says it takes one during a turn, the words go at once
/// and reach it at its next step; anywhere else they wait for the turn to end, and nothing lands in the middle of a turn
/// or a frame.</para>
///
/// <para><b>What is said is told at once</b> (<see cref="OnSaid"/>), with when it reaches the session, so the record
/// shows the person's words the moment they are said rather than when they are handed over. A word said before the door
/// is known is told when it is.</para>
///
/// <para><b>Taking and closing are one step</b> (<see cref="NextOrClose"/>): the run asks for the next word when a turn
/// ends, and an inbox with nothing held and nothing on its way closes in the same breath, so a word said as the session
/// winds up is refused, and the person told, rather than kept where nobody reads it.</para>
///
/// <para><b>Sending now stops the turn and withdraws nothing</b>, the reverse of a conversation's stop (CONV4a), which
/// hands queued words back: here the person stops the turn so their words go at once. With nothing held it stops
/// nothing: ending the work is the session's own stop. Where words go at once nothing is ever held, and a stop while one
/// is on its way would answer it cancelled while the agent may still act on it, so there it stops nothing either.</para>
/// </remarks>
/// <param name="changed">Where it stands, each time that changes — the page shows what is waiting.</param>
public sealed class DrivenInbox(Action<ChatQueue> changed)
{
    private readonly object _gate = new();
    private readonly List<ChatMessage> _held = [];
    private readonly Queue<Task<string>> _sent = new();
    // The steers *Send now* sent (STEER2a), each until the agent answered it: where it answered `injected` the words were taken.
    private readonly Queue<Task<string>> _steered = new();
    // Each turn the run takes a held word for, counted (STEER2a): a turn is asked to stop once, and the run takes no held word
    // while a stop decided for the turn before is still on its way, which landing late would cut that word's own turn.
    private int _turn;
    private int _stoppedTurn = -1;
    private Task _stopping = Task.CompletedTask;
    private bool _closed;
    private bool _flowing;
    private int _said;
    private DrivenReach? _reach;
    private Func<Task>? _interrupt;
    private Func<ChatMessage, Task<string>>? _deliver;
    private Func<ChatMessage, Task<string>>? _steer;
    private Action<ChatMessage, DrivenReach>? _told;
    private Action? _closing;
    private Action<ChatMessage>? _withdrawn;

    /// <summary>Where it stands: a turn is running while it is open, and what is held waits for its end.</summary>
    public ChatQueue State
    {
        get
        {
            lock (_gate) return Now();
        }
    }

    /// <summary>
    /// When a word said now reaches the session (MSG1d, D137 §5.3), which the modules answer the page with: null until the
    /// door is known, since a word said while it opens waits for its first prompt either way, and null once it has closed.
    /// </summary>
    public DrivenReach? Reach
    {
        get
        {
            lock (_gate) return _closed ? null : _reach;
        }
    }

    /// <summary>
    /// The door, once the session is open: its way of stopping the running turn, and — where the agent takes a prompt
    /// during its turn — its way of sending one (STEER1). What is held then is told with when it reaches the session; a
    /// word goes only once the door says the turn has begun (<see cref="Flow"/>), so none overtakes the prompt it follows.
    /// </summary>
    /// <param name="interrupt">
    /// Stops the running turn and keeps the session; null where nothing can stop a turn and keep the session (the native
    /// door's run, MSG1b), and *Send now* then stops nothing.
    /// </param>
    /// <param name="deliver">
    /// Sends one word as a prompt now and completes with its answer; null where the agent takes no prompt during a turn,
    /// and every word waits for the turn's end.
    /// </param>
    /// <param name="steer">
    /// The person's *Send now* on that door (STEER2a): steers one word into the running turn and completes with the agent's
    /// outcome, the words taken where it is <c>injected</c>. Null where the agent never said it steers.
    /// </param>
    public void Attach(
        Func<Task>? interrupt, Func<ChatMessage, Task<string>>? deliver = null, Func<ChatMessage, Task<string>>? steer = null)
    {
        lock (_gate)
        {
            _interrupt = interrupt;
            _deliver = deliver;
            _steer = deliver is null ? null : steer;
            _reach = deliver is null ? DrivenReach.TurnEnd : DrivenReach.NextStep;
            if (_closed) return;

            foreach (var message in _held) Tell(message, _reach.Value);
            Publish();
        }
    }

    /// <summary>
    /// The session's first prompt is on the wire (STEER1): where words go during a turn, what is held goes now, and every
    /// word after it the moment it is said. Nothing on a door that holds words for the turn's end.
    /// </summary>
    public void Flow()
    {
        lock (_gate)
        {
            _flowing = true;
            if (_closed || _reach != DrivenReach.NextStep) return;

            foreach (var message in _held) Send(message);
            _held.Clear();
            Publish();
        }
    }

    /// <summary>
    /// Told each word with when it reaches the session, the moment it is said (STEER1) — or, said before the door is
    /// known, when it is. Called under the inbox's lock, so told in order; a listener's failure is its own.
    /// </summary>
    public void OnSaid(Action<ChatMessage, DrivenReach> said)
    {
        lock (_gate) _told = said;
    }

    /// <summary>
    /// Told each held word the person took back or reworded (QUEUEEDIT1a), after it was told as said: the record settles it
    /// as withdrawn. Called under the inbox's lock, in order; a listener's failure is its own.
    /// </summary>
    public void OnWithdrawn(Action<ChatMessage> withdrawn)
    {
        lock (_gate) _withdrawn = withdrawn;
    }

    /// <summary>
    /// Take back or reword one held word (QUEUEEDIT1a): with no <paramref name="text"/> it is removed, with some its words are
    /// replaced where it is held, its place and its files kept, under a new id the record is told as said. Only a word still held
    /// (<see cref="QueueEdit.Taken"/> otherwise): one sent to the turn on the wire, or taken for the next, is the session's. The
    /// record, which showed it waiting once the door was known, settles the old id as withdrawn.
    /// </summary>
    public QueueEdit Edit(string id, string? text)
    {
        lock (_gate)
        {
            var at = _closed ? -1 : _held.FindIndex(message => message.Id == id);
            if (at < 0) return QueueEdit.Taken;

            var old = _held[at];
            // A word said before the door was known was never told: the record has nothing of it to settle.
            var told = _reach is not null;
            if (text is null)
            {
                _held.RemoveAt(at);
            }
            else
            {
                _held[at] = old with { Text = text, Id = $"said-{++_said}" };
            }

            if (told)
            {
                try
                {
                    _withdrawn?.Invoke(old);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    // The record misses it; the queue has moved either way.
                }

                if (text is not null && _reach is { } reach) Tell(_held[at], reach);
            }

            Publish();
            return QueueEdit.Done;
        }
    }

    /// <summary>Called once, when it closes — how the registry forgets it.</summary>
    internal void OnClosing(Action closing)
    {
        lock (_gate) _closing = closing;
    }

    /// <summary>
    /// Take the person's words: told at once, then held for the turn's end, or sent where the agent takes them during
    /// its turn. False once the session has stopped taking any: it is ending.
    /// </summary>
    public bool Hold(ChatMessage message)
    {
        lock (_gate)
        {
            if (_closed) return false;
            var said = message with { Id = $"said-{++_said}" };
            if (_reach is { } reach) Tell(said, reach);
            if (_reach == DrivenReach.NextStep && _flowing) Send(said);
            else _held.Add(said);
            Publish();
            return true;
        }
    }

    /// <summary>
    /// The person's *Send now* (STEER2a, D136's 2026-10-16 note), carried out by the door it has. Where the agent steers and its
    /// first prompt is on the wire, the words are steered into the running turn at once, told with the reach <c>now</c>. On a
    /// door that holds words for the turn's end they are held as <see cref="Hold"/> holds them, and the turn is stopped so they
    /// go next (D90). Where the door can do neither, or the first prompt has not left, they go as <see cref="Hold"/> sends
    /// them, and the answer says why by its code. Null once the session has stopped taking any.
    /// </summary>
    /// <remarks>
    /// <para>The steer is answered by the run (<see cref="NextOrClose"/>), which closes nothing while one is on its way. The stop
    /// is asked once per turn, once the words are held, as <see cref="SendNowAsync"/> asks it.</para>
    ///
    /// <para>🔴 <b>A steer goes past a word on its way; a stop never does</b> (D136 point 4): the steer cuts the step in flight,
    /// not the prompt the word is queued as, which is still taken at its next step, and the person chose to go first. Where the
    /// agent cannot steer, these go at its next step behind it, said <see cref="SendNowWhy.Behind"/>; that door is never
    /// stopped.</para>
    /// </remarks>
    public async Task<SentNow?> HoldNowAsync(ChatMessage message)
    {
        Stop? stop = null;
        SentNow now;
        lock (_gate)
        {
            if (_closed) return null;
            var onTheirWay = _sent.Any(answer => !answer.IsCompleted);
            if (_reach == DrivenReach.NextStep && _flowing && _steer is { } steer)
            {
                var said = message with { Id = $"said-{++_said}" };
                Tell(said, DrivenReach.Steered);
                Steer(steer, said);
                Publish();
                return new SentNow(SendNowWhy.Spelled(DrivenReach.Steered), Now: true, Why: null);
            }

            var reach = _reach;
            Hold(message);
            if (reach is null || reach == DrivenReach.NextStep && !_flowing)
            {
                now = new SentNow(SendNowWhy.Spelled(reach), Now: false, SendNowWhy.Opening);
            }
            else if (reach == DrivenReach.NextStep)
            {
                now = new SentNow(SendNowWhy.Spelled(reach), Now: false, onTheirWay ? SendNowWhy.Behind : SendNowWhy.NoInterrupt);
            }
            else if (_interrupt is not null)
            {
                stop = StopThisTurn();
                now = new SentNow(SendNowWhy.Spelled(reach), Now: true, Why: null);
            }
            else
            {
                now = new SentNow(SendNowWhy.Spelled(reach), Now: false, SendNowWhy.NoInterrupt);
            }
        }

        if (stop is not null) await SendStopAsync(stop).ConfigureAwait(false);
        return now;
    }

    /// <summary>
    /// Words the agent answered <c>promptRequired</c> (STEER2d): the turn ended between the press and the steer, and the adapter
    /// left them to the client. They go to the front of what is held, since the person chose to go first, and the run prompts
    /// them next. Not told again, since they were said once; the run's prompt takes them. Called while the run waits on the
    /// steer, so nothing has closed; false once it has, and the caller says they never reached the agent.
    /// </summary>
    public bool HandBack(ChatMessage message)
    {
        lock (_gate)
        {
            if (_closed) return false;
            _held.Insert(0, message);
            Publish();
            return true;
        }
    }

    /// <summary>
    /// Take another agent's words only where they reach the running turn (SESSONE2b, D155): on the door that takes words during
    /// a turn (STEER1), as <see cref="Hold"/> takes them. False on a door that holds words for the turn's end, where they would be
    /// prompted after it and extend the run, before the door is known, and once the session has stopped taking any.
    /// </summary>
    public bool HoldDuringTurn(ChatMessage message)
    {
        lock (_gate)
        {
            return !_closed && _reach == DrivenReach.NextStep && Hold(message);
        }
    }

    /// <summary>
    /// What the run does next (STEER1): wait for a word already sent, oldest first; else prompt a word held; else, with
    /// nothing held and nothing on its way, null, and the inbox closed in the same step.
    /// </summary>
    public DrivenNext? NextOrClose()
    {
        Action? closing = null;
        DrivenNext? next = null;
        // One step under the gate: a word sent between "nothing on its way" and "closed" would be answered to nobody.
        lock (_gate)
        {
            if (_closed) return null;
            if (_steered.TryDequeue(out var steered))
            {
                next = new DrivenNext(null, null) { Waits = steered };
            }
            else if (_sent.TryDequeue(out var answer))
            {
                next = new DrivenNext(null, answer);
            }
            else if (_held.Count > 0 && !_stopping.IsCompleted)
            {
                // 🔴 The stop decided for the turn that ended is on its way: the held word's own turn goes after it (STEER2a).
                next = new DrivenNext(null, null) { Waits = _stopping };
            }
            else if (_held.Count > 0)
            {
                next = new DrivenNext(_held[0], null);
                _held.RemoveAt(0);
                _turn++;
            }
            else
            {
                _closed = true;
                closing = _closing;
            }

            Publish();
        }

        closing?.Invoke();
        return next;
    }

    /// <summary>
    /// The next word held for the turn's end — or, with none held and none on its way, null, and the inbox closed in the
    /// same step. A word already sent is <see cref="NextOrClose"/>'s, and while one is on its way this takes nothing.
    /// </summary>
    public ChatMessage? TakeOrClose()
    {
        Action? closing = null;
        ChatMessage? next;
        lock (_gate)
        {
            if (_closed) return null;
            if (_held.Count > 0)
            {
                next = _held[0];
                _held.RemoveAt(0);
                _turn++;
            }
            else if (_sent.Count > 0 || _steered.Count > 0)
            {
                return null;
            }
            else
            {
                next = null;
                _closed = true;
                closing = _closing;
            }

            Publish();
        }

        closing?.Invoke();
        return next;
    }

    /// <summary>
    /// Every word held for the turn's end, in the order said — or, with none held and none on its way, none, and the inbox
    /// closed in the same step (MSG1b): the native door goes on with all of them in one resumed run, its words joined.
    /// </summary>
    public IReadOnlyList<ChatMessage> TakeAllOrClose()
    {
        Action? closing = null;
        List<ChatMessage> taken;
        lock (_gate)
        {
            if (_closed) return [];
            taken = [.. _held];
            _held.Clear();
            if (taken.Count > 0) _turn++;
            if (taken.Count == 0 && _sent.Count == 0 && _steered.Count == 0)
            {
                _closed = true;
                closing = _closing;
            }

            Publish();
        }

        closing?.Invoke();
        return taken;
    }

    /// <summary>
    /// Close it whatever is held — the session failed or was stopped — and hand back what never left it, so the driver
    /// can say so rather than drop the person's words without a trace. A word already sent is the session's: the run
    /// says which of those it never took.
    /// </summary>
    public IReadOnlyList<ChatMessage> Close()
    {
        Action? closing;
        List<ChatMessage> left;
        lock (_gate)
        {
            if (_closed) return [];
            left = [.. _held];
            _held.Clear();
            _sent.Clear();
            _steered.Clear();
            _closed = true;
            closing = _closing;
            Publish();
        }

        closing?.Invoke();
        return left;
    }

    /// <summary>
    /// Stop the running turn so what is held goes next. <see cref="TurnStop.Nothing"/> with nothing held, before the
    /// session is open, or where words go at once (STEER1): a stop while one is on its way would answer it cancelled while
    /// the agent may still act on it (docs/2026-10-03-steer-evidence.md §3).
    /// </summary>
    /// <remarks>A turn is asked to stop once, however often the person presses (STEER2a): the answer still says it was.</remarks>
    public async Task<TurnStop> SendNowAsync()
    {
        Stop? stop;
        lock (_gate)
        {
            if (_closed || _held.Count == 0 || _interrupt is null || _reach == DrivenReach.NextStep) return TurnStop.Nothing;
            stop = StopThisTurn();
        }

        if (stop is not null) await SendStopAsync(stop).ConfigureAwait(false);
        return new TurnStop(Cancelled: true, Withdrawn: []);
    }

    /// <summary>A stop decided for one turn (STEER2a): the door's interrupt, the turn, and what tells the run it is on the wire or dropped.</summary>
    private sealed record Stop(Func<Task> Interrupt, int Turn, TaskCompletionSource Sent);

    /// <summary>
    /// Decide the stop for the turn running now, once per turn; null where it was asked already or nothing can stop a turn. The
    /// run takes no held word until it is on the wire or dropped (<see cref="NextOrClose"/>). Called under the gate.
    /// </summary>
    private Stop? StopThisTurn()
    {
        if (_interrupt is not { } interrupt || _stoppedTurn == _turn) return null;
        _stoppedTurn = _turn;
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _stopping = _stopping.IsCompleted ? sent.Task : Task.WhenAll(_stopping, sent.Task);
        return new Stop(interrupt, _turn, sent);
    }

    /// <summary>
    /// Send a stop to the turn it was decided for, or drop it where the run took another turn or the inbox closed; the run may
    /// then take the next held word, never before. Called outside the gate: the door's interrupt is its own.
    /// </summary>
    private async Task SendStopAsync(Stop stop)
    {
        try
        {
            bool current;
            lock (_gate) current = !_closed && _turn == stop.Turn;
            if (current) await stop.Interrupt().ConfigureAwait(false);
        }
        finally
        {
            stop.Sent.TrySetResult();
        }
    }

    private ChatQueue Now() => _closed ? ChatQueue.Idle : new ChatQueue(Taking: true, [.. _held]);

    /// <summary>Send one word now, under the gate so words leave in the order said; its answer waits for the run.</summary>
    private void Send(ChatMessage message)
    {
        Task<string> answer;
        try
        {
            answer = _deliver!(message);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            answer = Task.FromException<string>(error);
        }

        // Observed here as well as by the run, which may have ended before it reads this one.
        _ = answer.ContinueWith(static sent => sent.Exception, TaskScheduler.Default);
        _sent.Enqueue(answer);
    }

    /// <summary>Steer one word into the running turn now (STEER2a), under the gate as <see cref="Send"/> sends; the run waits for its answer.</summary>
    private void Steer(Func<ChatMessage, Task<string>> steer, ChatMessage message)
    {
        Task<string> answer;
        try
        {
            answer = steer(message);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            answer = Task.FromException<string>(error);
        }

        _ = answer.ContinueWith(static steered => steered.Exception, TaskScheduler.Default);
        _steered.Enqueue(answer);
    }

    /// <summary>Tell the record what was said. Called under the gate, so told in order; a listener's failure is its own.</summary>
    private void Tell(ChatMessage message, DrivenReach reach)
    {
        try
        {
            _told?.Invoke(message, reach);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The words still go: the record misses them, and the session does not.
        }
    }

    /// <summary>Tell where it stands. Called under the gate, so told in order; a listener's failure is its own.</summary>
    private void Publish()
    {
        try
        {
            changed(Now());
        }
        catch (Exception)
        {
            // The inbox has moved either way.
        }
    }
}
