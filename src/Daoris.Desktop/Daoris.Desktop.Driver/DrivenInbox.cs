namespace Daoris.Driver;

/// <summary>When what a person tells a driven session reaches it (STEER1, D136).</summary>
public enum DrivenReach
{
    /// <summary>Held, and the next prompt of the same session when its turn ends (SESS3, D90).</summary>
    TurnEnd,

    /// <summary>
    /// Sent at once, as a prompt the agent takes during its turn: Claude Code folds it in at its next step
    /// (docs/2026-10-03-steer-evidence.md §1).
    /// </summary>
    NextStep,
}

/// <summary>What a run does next with what the person said: prompt a word still held, or wait for one already sent.</summary>
/// <param name="Held">A word held for the turn's end, to prompt now; null when <paramref name="Answer"/> is set.</param>
/// <param name="Answer">The answer to a word sent during the turn, in the order sent; null when <paramref name="Held"/> is set.</param>
public sealed record DrivenNext(ChatMessage? Held, Task<string>? Answer);

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
    private bool _closed;
    private bool _flowing;
    private int _said;
    private DrivenReach? _reach;
    private Func<Task>? _interrupt;
    private Func<ChatMessage, Task<string>>? _deliver;
    private Action<ChatMessage, DrivenReach>? _told;
    private Action? _closing;

    /// <summary>Where it stands: a turn is running while it is open, and what is held waits for its end.</summary>
    public ChatQueue State
    {
        get
        {
            lock (_gate) return Now();
        }
    }

    /// <summary>
    /// The door, once the session is open: its way of stopping the running turn, and — where the agent takes a prompt
    /// during its turn — its way of sending one (STEER1). What is held then is told with when it reaches the session; a
    /// word goes only once the door says the turn has begun (<see cref="Flow"/>), so none overtakes the prompt it follows.
    /// </summary>
    /// <param name="interrupt">Stops the running turn and keeps the session.</param>
    /// <param name="deliver">
    /// Sends one word as a prompt now and completes with its answer; null where the agent takes no prompt during a turn,
    /// and every word waits for the turn's end.
    /// </param>
    public void Attach(Func<Task> interrupt, Func<ChatMessage, Task<string>>? deliver = null)
    {
        lock (_gate)
        {
            _interrupt = interrupt;
            _deliver = deliver;
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
            if (_sent.TryDequeue(out var answer))
            {
                next = new DrivenNext(null, answer);
            }
            else if (_held.Count > 0)
            {
                next = new DrivenNext(_held[0], null);
                _held.RemoveAt(0);
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
            }
            else if (_sent.Count > 0)
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
    public async Task<TurnStop> SendNowAsync()
    {
        Func<Task>? interrupt;
        lock (_gate)
        {
            if (_closed || _held.Count == 0 || _interrupt is null || _reach == DrivenReach.NextStep) return TurnStop.Nothing;
            interrupt = _interrupt;
        }

        await interrupt().ConfigureAwait(false);
        return new TurnStop(Cancelled: true, Withdrawn: []);
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
