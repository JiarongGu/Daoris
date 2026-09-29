namespace Daoris.Driver;

/// <summary>
/// What a person tells a driven session while it works (SESS3), held until the protocol door can hand
/// it over: as the next prompt of the same session when the running turn ends, or at once when the
/// person stops the turn to send it.
/// </summary>
/// <remarks>
/// <para><b>Why held and never written.</b> A driven session is handed its whole quest in one turn, and
/// on the protocol door its stdin carries the driver's own frames (D53), which is why it takes no
/// person's line (INT4i). A prompt between turns is the door's own way of adding words, so the words
/// wait for one: the session keeps its context, and nothing lands in the middle of a turn or a frame.</para>
///
/// <para><b>Taking and closing are one step</b> (<see cref="TakeOrClose"/>): the capture asks for the
/// next word when a turn ends, and an inbox with nothing held closes in the same breath, so a word said
/// as the session winds up is refused, and the person told, rather than kept where nobody reads it.</para>
///
/// <para><b>Sending now stops the turn and withdraws nothing</b>, the reverse of a conversation's stop
/// (CONV4a), which hands queued words back: here the person stops the turn so their words go at once.
/// With nothing held it stops nothing: ending the work is the session's own stop.</para>
/// </remarks>
/// <param name="changed">Where it stands, each time that changes — the page shows what is waiting.</param>
public sealed class DrivenInbox(Action<ChatQueue> changed)
{
    private readonly object _gate = new();
    private readonly List<ChatMessage> _held = [];
    private bool _closed;
    private Func<Task>? _interrupt;
    private Action? _closing;

    /// <summary>Where it stands: a turn is running while it is open, and what is held waits for its end.</summary>
    public ChatQueue State
    {
        get
        {
            lock (_gate) return Now();
        }
    }

    /// <summary>The door's way of stopping the running turn, once the session is open.</summary>
    public void Attach(Func<Task> interrupt)
    {
        lock (_gate) _interrupt = interrupt;
    }

    /// <summary>Called once, when it closes — how the registry forgets it.</summary>
    internal void OnClosing(Action closing)
    {
        lock (_gate) _closing = closing;
    }

    /// <summary>Hold the person's words. False once the session has stopped taking any: it is ending.</summary>
    public bool Hold(ChatMessage message)
    {
        lock (_gate)
        {
            if (_closed) return false;
            _held.Add(message);
            Publish();
            return true;
        }
    }

    /// <summary>The next word to hand over — or, with none held, null, and the inbox closed in the same step.</summary>
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
    /// Close it whatever is held — the session failed or was stopped — and hand back what never reached
    /// it, so the driver can say so rather than drop the person's words without a trace.
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
            _closed = true;
            closing = _closing;
            Publish();
        }

        closing?.Invoke();
        return left;
    }

    /// <summary>
    /// Stop the running turn so what is held goes next. <see cref="TurnStop.Nothing"/> with nothing held,
    /// or before the session is open.
    /// </summary>
    public async Task<TurnStop> SendNowAsync()
    {
        Func<Task>? interrupt;
        lock (_gate)
        {
            if (_closed || _held.Count == 0 || _interrupt is null) return TurnStop.Nothing;
            interrupt = _interrupt;
        }

        await interrupt().ConfigureAwait(false);
        return new TurnStop(Cancelled: true, Withdrawn: []);
    }

    private ChatQueue Now() => _closed ? ChatQueue.Idle : new ChatQueue(Taking: true, [.. _held]);

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
