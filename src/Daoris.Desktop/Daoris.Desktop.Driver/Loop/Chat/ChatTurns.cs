namespace Daoris.Driver;

/// <summary>
/// One conversation's turns: one at a time, in the order the person sent them — the same on both doors.
/// </summary>
/// <remarks>
/// <para><b>A message sent while a turn runs waits for it</b>, and the door records it when it is sent,
/// not when it is typed. Recorded at once, it would sit inside the turn before it, and the conversation
/// would read as though the agent answered a question it had not been asked. Written to the harness
/// at once, a word may be folded into the running turn by Claude Code — so both doors
/// hold it here, and neither depends on what a harness does with a line mid-turn.</para>
///
/// <para><b>Where the turns stand is told as it moves</b> (the constructor's <c>changed</c>): whether a turn is
/// in flight, which the page's stop follows — the record lags it — and what is waiting, which the page
/// shows as queued because nothing else shows it. The one the door is still opening for counts as
/// waiting; one the door takes at once is never announced.</para>
///
/// <para><b>A stop withdraws what is waiting and stops the turn in flight</b>, in that order: stopped
/// the other way round, the next message would start the moment the stopped turn ended. A stop that
/// lands after a turn is taken but before it reached the harness waits for it to be sent, so its
/// interrupt never overtakes the turn it means to stop.</para>
///
/// <para><b>Its parts.</b> The state is <see cref="TurnState"/>'s, under its one gate (its remarks say what the gate guards, the order of
/// the locks and what a race would break). The pump is <see cref="TurnPump"/>, a word's take <see cref="TurnTakes"/>, the keeps on the
/// record <see cref="TurnKeeps"/>, the holds <see cref="TurnHolds"/> and the stops <see cref="TurnStops"/>; this type is their one door.</para>
/// </remarks>
internal sealed class ChatTurns : IChatTurns
{
    private readonly TurnState _state;
    private readonly TurnStops _stops;
    private readonly TurnPump _pump;
    private readonly TurnTakes _takes;
    private readonly TurnKeeps _keeps;
    private readonly TurnHolds _holds;

    /// <summary>A conversation's turns over its door.</summary>
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
    /// The door's next step: hand a word to the turn on the wire at once, returning what completes when the
    /// agent has answered it, or null where the door takes no word during a turn now. Called under the turns' own lock, so it
    /// must not call back into them. Null for a door that never does (the native door, a text pipe).
    /// </param>
    /// <param name="steerNow">
    /// The person's *Send now* on that door: steer a word into the turn on the wire at once,
    /// cutting the step in flight, returning what completes when the agent has answered the steer, or null where the agent never
    /// said it steers. Called under the turns' own lock, as <paramref name="steer"/> is. Null for a door that never does.
    /// </param>
    /// <param name="nextStep">
    /// Whether the door takes words at the turn's next step now, asked without sending anything: on such a door *Send now*
    /// never stops a turn, since a word may be on its way. Called under the turns' own lock. Null for a door that
    /// never does.
    /// </param>
    /// <param name="settled">
    /// Awaited by the pump after a turn is taken and again each time the words handed to it have all been answered, before
    /// the turn is found to have ended: the door settles there what those words' own turns came to, so a word taken whose turn failed
    /// transient is tried again inside the turn, which lasts. Never faults the pump. Null for a door that settles nothing.
    /// </param>
    /// <param name="cutsIn">
    /// Whether <paramref name="interrupt"/> stops a turn keeping the conversation, so the person's *Send now* that the door cannot
    /// take mid-turn cuts in (<see cref="TurnCutIn"/>) rather than waiting for the turn's end.
    /// </param>
    internal ChatTurns(
        Func<Task<bool>> ready,
        Func<ChatMessage, Action, Task> take,
        Func<Task> interrupt,
        Action<ChatQueue> changed,
        Func<ChatMessage, Task?>? steer = null,
        Func<ChatMessage, Task?>? steerNow = null,
        Func<bool>? nextStep = null,
        Func<Task>? settled = null,
        bool cutsIn = false)
    {
        _state = new TurnState(changed);
        _stops = new TurnStops(_state, interrupt);
        _pump = new TurnPump(_state, _stops, ready, take, settled);
        _takes = new TurnTakes(_state, _pump, steer, steerNow, nextStep, cutsIn ? new TurnCutIn(_state, _stops.StopKeepingAsync) : null);
        _keeps = new TurnKeeps(_state, _takes, _pump, steer, steerNow);
        _holds = new TurnHolds(_state, _pump);
    }

    /// <inheritdoc cref="TurnState.Unsent"/>
    public Action<int>? Unsent
    {
        get => _state.Unsent;
        set => _state.Unsent = value;
    }

    /// <inheritdoc cref="TurnState.Ahead"/>
    public Func<ChatMessage, ChatMessage>? Ahead
    {
        get => _state.Ahead;
        set => _state.Ahead = value;
    }

    /// <summary>
    /// The record's door for words kept there before this queue took them: a helper's answer, kept by the driver, taken off by
    /// its id once its turn is on the wire, in the same order as the person's words. The first door named stays.
    /// </summary>
    public void Keeps(ISaidDoor door)
    {
        lock (_state.Gate) _state.Door ??= door;
    }

    /// <summary>
    /// The answers-alone bound's judge of a helper's answer as its turn comes: true where the bound holds it for the person,
    /// and the answer then goes no further, kept on its record. Named by whoever says a helper's answer, as <see cref="Keeps"/> names the
    /// door; the first named stays.
    /// </summary>
    public void Bounds(Func<ChatMessage, Task<bool>> alone)
    {
        lock (_state.Gate) _state.Alone ??= alone;
    }

    /// <inheritdoc cref="TurnState.TakingOff"/>
    public Task TakingOff => _state.TakingOff;

    /// <inheritdoc cref="TurnKeeps.KeepAsync"/>
    public Task<TurnKept?> KeepAsync(ISaidDoor door, ChatMessage message, CancellationToken ct) => _keeps.KeepAsync(door, message, ct);

    /// <summary>Where the turns stand now — what a page that just opened the conversation is told.</summary>
    public ChatQueue State
    {
        get
        {
            lock (_state.Gate) return _state.Now();
        }
    }

    /// <summary>Whether a turn is on its way to the harness or running there.</summary>
    public bool Running
    {
        get
        {
            lock (_state.Gate) return _state.Pumping;
        }
    }

    /// <summary>Whether the person stopped the turn in flight, which a word said now then waits for.</summary>
    public bool Stopped
    {
        get
        {
            lock (_state.Gate) return _state.InFlight && _state.Stopped;
        }
    }

    /// <summary>
    /// The line a conversation keeps of a word handed to its turn at the next step, once the agent answered
    /// it: where the person's stop is asked while the word is on its way and the agent answers it <c>cancelled</c>, the
    /// agent may have read it and its answer is not kept. Null for any other answer, which is in the turn.
    /// </summary>
    /// <remarks>
    /// Its code, which the page words as <c>work.conversation.lost</c> in the reader's language, and the word's id, so
    /// the page settles the word it still shows waiting. The English beside them is the console's and the terminal's.
    /// </remarks>
    /// <param name="word">The word's id, as the record showed it waiting.</param>
    /// <param name="stoppedUnder">The person's stop is asked while the word is on its way.</param>
    /// <param name="stopReason">The agent's answer to the word.</param>
    public static SessionEvent? Lost(string word, bool stoppedUnder, string stopReason) =>
        stoppedUnder && stopReason == "cancelled"
            ? new SessionEvent { Kind = SessionEventKind.Note, Text = LostLine, Words = [word], Code = SessionEventCodes.Lost }
            : null;

    /// <summary>The driver's English for <see cref="Lost"/>: what its console says and a page that does not know the code shows.</summary>
    public const string LostLine = SessionNotes.LostLine;

    /// <summary>
    /// The line a driven session's record keeps of a held word the person took back or reworded: its code, which
    /// the page words as <c>work.conversation.withdrawn</c>, and the word's id, so the page settles the word it showed waiting.
    /// </summary>
    public static SessionEvent Withdrawn(string word) => SessionNotes.Withdrawn(word);

    /// <summary>The driver's English for <see cref="Withdrawn"/>.</summary>
    public const string WithdrawnLine = SessionNotes.WithdrawnLine;

    /// <inheritdoc cref="TurnKeeps.EditQueued"/>
    public QueueEdit EditQueued(string id, string? text, MessageLevel? level = null) => _keeps.EditQueued(id, text, level);

    /// <inheritdoc cref="TurnKeeps.EditQueuedAsync"/>
    public Task<QueueEdit> EditQueuedAsync(string id, string? text, MessageLevel? level = null, CancellationToken ct = default) =>
        _keeps.EditQueuedAsync(id, text, level, ct);

    /// <summary>Queue a turn. False once the conversation is finishing, held or gone: nothing more will be heard.</summary>
    public bool Say(ChatMessage message) => _takes.Take(message) is not null;

    /// <inheritdoc cref="TurnHolds.Hold"/>
    public bool Hold(bool keeps = false) => _holds.Hold(keeps);

    /// <inheritdoc cref="TurnHolds.HoldForClose"/>
    public bool HoldForClose() => _holds.HoldForClose();

    /// <inheritdoc cref="TurnState.Closing"/>
    public Func<bool>? Closing
    {
        get => _state.Closing;
        set => _state.Closing = value;
    }

    /// <inheritdoc cref="TurnHolds.Release"/>
    public void Release() => _holds.Release();

    /// <inheritdoc cref="TurnHolds.WaitForLimit"/>
    public bool WaitForLimit() => _holds.WaitForLimit();

    /// <summary>Whether its words wait for its account's limit to reset (<see cref="WaitForLimit"/>).</summary>
    public bool Limited
    {
        get
        {
            lock (_state.Gate) return _state.Limited;
        }
    }

    /// <inheritdoc cref="TurnHolds.WakeFromLimit"/>
    public bool WakeFromLimit(Func<IReadOnlyList<ChatMessage>, ChatMessage> compose) => _holds.WakeFromLimit(compose);

    /// <inheritdoc cref="TurnHolds.HoldLimited"/>
    public IReadOnlyList<ChatMessage>? HoldLimited(bool keeps = false) => _holds.HoldLimited(keeps);

    /// <inheritdoc cref="TurnKeeps.PressAsync"/>
    public Task<PressHold> PressAsync(CancellationToken ct = default) => _keeps.PressAsync(ct);

    /// <summary>Whether the person's press holds the conversation (<see cref="PressAsync"/>).</summary>
    public bool Pressing
    {
        get
        {
            lock (_state.Gate) return _state.Pressing;
        }
    }

    /// <inheritdoc cref="TurnHolds.Pressed"/>
    public void Pressed() => _holds.Pressed();

    /// <inheritdoc cref="TurnHolds.MoveOut"/>
    public IReadOnlyList<ChatMessage> MoveOut(Func<ChatMessage, bool> onward) => _holds.MoveOut(onward);

    /// <inheritdoc cref="TurnKeeps.SendBackAsync"/>
    public Task<SendBack> SendBackAsync(int pressed, Func<int> history, Func<CancellationToken, Task<ChatMessage?>> hand, CancellationToken ct = default) =>
        _keeps.SendBackAsync(pressed, history, hand, ct);

    /// <inheritdoc cref="TurnHolds.TakeHeld(ChatMessage)"/>
    public Task? TakeHeld(ChatMessage message) => _holds.TakeHeld(message);

    /// <inheritdoc cref="TurnTakes.Take"/>
    public TurnReach? Take(ChatMessage message) => _takes.Take(message);

    /// <inheritdoc cref="TurnTakes.TakeMoved"/>
    public TurnReach? TakeMoved(ISaidDoor door, ChatMessage message) => _takes.TakeMoved(door, message);

    /// <inheritdoc cref="TurnTakes.HandBack"/>
    public bool HandBack(ChatMessage message) => _takes.HandBack(message);

    /// <inheritdoc cref="TurnHolds.Settling"/>
    internal void Settling() => _holds.Settling();

    /// <inheritdoc cref="TurnHolds.Waiting"/>
    internal Task Waiting() => _holds.Waiting();

    /// <inheritdoc cref="TurnHolds.Waited"/>
    internal void Waited() => _holds.Waited();

    /// <inheritdoc cref="TurnHolds.TakeHeld()"/>
    internal IReadOnlyList<ChatMessage> TakeHeld() => _holds.TakeHeld();

    /// <inheritdoc cref="TurnTakes.TakeNowAsync"/>
    public Task<SentNow?> TakeNowAsync(ChatMessage message) => _takes.TakeNowAsync(message);

    /// <inheritdoc cref="TurnStops.StopKeepingAsync"/>
    public Task<TurnStop> StopKeepingAsync() => _stops.StopKeepingAsync();

    /// <inheritdoc cref="TurnStops.StopAsync"/>
    public Task<TurnStop> StopAsync() => _stops.StopAsync();

    /// <inheritdoc cref="TurnHolds.FinishAsync"/>
    public Task FinishAsync() => _holds.FinishAsync();

    /// <inheritdoc cref="TurnHolds.Gone"/>
    public void Gone(bool personStopped = false) => _holds.Gone(personStopped);
}
