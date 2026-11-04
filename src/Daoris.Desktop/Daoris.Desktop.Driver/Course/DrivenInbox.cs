namespace Daoris.Driver;

/// <summary>
/// What a person tells a driven session while it works (SESS3), handed over where the protocol door allows and its urgency asks
/// (COWORK1a, D188): an urgent word at once, where the agent takes a prompt during its turn (STEER1, D136); an ordinary word, and
/// every word on a door that takes none during a turn, as the next prompt of the same session when the running turn ends. No
/// word stops the turn: the person's keeping stop does, pressed on its own (<see cref="SendNowAsync"/>).
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
/// <para><b>The keeping stop stops the turn and withdraws nothing</b>, the reverse of a conversation's stop (CONV4a), which
/// hands queued words back: here the person stops the turn so what is held goes next. It is the person's own press, never a
/// word's (D188 point 1): *now* on a door that holds words for the turn's end holds them and stops nothing, said so. With
/// nothing held it stops nothing: ending the work is the session's own stop. On a door that takes words at its next step it is
/// not needed, since an urgent word reaches sooner, and a stop while one is on its way would answer it cancelled while the agent
/// may still act on it, so there it stops nothing either.</para>
///
/// <para><b>Its parts.</b> The state is <see cref="InboxState"/>'s, under its one gate (its remarks say what the gate guards, the order of
/// the locks and what a race would break). The keeps on the record are <see cref="InboxKeeps"/>, the holds <see cref="InboxHolds"/>, the
/// run's side <see cref="InboxRun"/> and the keeping stop <see cref="InboxStops"/>; this type is their one door.</para>
/// </remarks>
public sealed class DrivenInbox : IAgentInbox
{
    private readonly InboxState _state;
    private readonly InboxHolds _holds;
    private readonly InboxKeeps _keeps;
    private readonly InboxRun _run;
    private readonly InboxStops _stops;

    /// <summary>An inbox for one driven session's run.</summary>
    /// <param name="changed">Where it stands, each time that changes — the page shows what is waiting.</param>
    public DrivenInbox(Action<ChatQueue> changed)
    {
        _state = new InboxState(changed);
        _holds = new InboxHolds(_state);
        _keeps = new InboxKeeps(_state);
        _run = new InboxRun(_state);
        _stops = new InboxStops(_state);
    }

    void IAgentInbox.Attach(Func<Task>? interrupt, Func<IAgentWord, Task<string>>? deliver, Func<IAgentWord, Task<string>>? steer) =>
        Attach(interrupt, deliver is null ? null : word => deliver(word), steer is null ? null : word => steer(word));
    bool IAgentInbox.HandBack(IAgentWord word) => HandBack(ChatMessage.FromAgent(word));
    AgentNext? IAgentInbox.NextOrClose() => NextOrClose() is { } next
        ? new(next.Held, next.Answer) { Waits = next.Waits } : null;
    IReadOnlyList<IAgentWord> IAgentInbox.Close() => Close();
    void IAgentInbox.OnClosing(Action closing, Func<bool> personStopped, Action<IReadOnlyList<IAgentWord>> left) =>
        OnClosing(closing, personStopped, words => left(words));
    IReadOnlyList<IAgentWord> IEpisodeWords.TakeHeld() => TakeHeld();

    /// <summary>Where it stands: a turn is running while it is open, and what is held waits for its end.</summary>
    public ChatQueue State
    {
        get
        {
            lock (_state.Gate) return _state.Now();
        }
    }

    /// <summary>
    /// Whether any word is held for the run, Daoris's own among them (COWORK1b), which <see cref="State"/> never lists as the
    /// person's: what the run reads before it waits on a session's background work.
    /// </summary>
    public bool Holds
    {
        get
        {
            // COWORK1d: a claim that only rides makes no turn, so it is no reason to go on.
            lock (_state.Gate) return !_state.Closed && _state.Turns();
        }
    }

    /// <summary>
    /// Whether another agent's claim may be held now (COWORK1d, the message-urgency design §2.3, trap 3): the door is known, the inbox
    /// open, and the answer to its last open prompt not yet read. Once that answer is read the turn is over, and a claim written as a
    /// prompt would start one of its own; it waits for the entry's next part, or the agent's own read.
    /// </summary>
    public bool TakesClaims
    {
        get
        {
            lock (_state.Gate) return !_state.Closed && _state.Reach is not null && !_state.Quiet;
        }
    }

    /// <summary>
    /// The answer to the last prompt open on the wire was read (COWORK1d, the design §2.3): told on the reader's thread before the run
    /// judges its next step, so a claim arriving from here on is refused (<see cref="TakesClaims"/>). A prompt sent again opens it.
    /// </summary>
    public void LastAnswered()
    {
        lock (_state.Gate)
        {
            if (!_state.Closed) _state.Quiet = true;
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
            lock (_state.Gate) return _state.Closed ? null : _state.Reach;
        }
    }

    /// <summary>
    /// When a word said now at <paramref name="level"/> reaches the session (COWORK1a, the design §3.0): an ordinary one at the
    /// turn's end on every door, any other at <see cref="Reach"/>. Null as <see cref="Reach"/> is.
    /// </summary>
    public DrivenReach? ReachAt(MessageLevel level) => _state.ReachAt(level);

    /// <summary>
    /// Whether the agent steers (STEER2a): a word at <c>now</c> cuts its step in flight, where anywhere else it is told why not
    /// (COWORK1a, the design §8.1). False until the door is known, and once it has closed.
    /// </summary>
    public bool Steers
    {
        get
        {
            lock (_state.Gate) return !_state.Closed && _state.SteerWith is not null;
        }
    }

    /// <summary>
    /// Whether the door can stop a turn and keep the session (COWORK1a, the design §3.2): where words wait for the turn's end, the
    /// keeping stop is offered beside them. False on the native door's run (MSG1b), which has no such interrupt and whose stop ends
    /// the session, until the door is known, and once it has closed.
    /// </summary>
    public bool KeepingStop
    {
        get
        {
            lock (_state.Gate) return !_state.Closed && _state.Interrupt is not null;
        }
    }

    /// <inheritdoc cref="InboxHolds.Attach"/>
    public void Attach(
        Func<Task>? interrupt, Func<ChatMessage, Task<string>>? deliver = null, Func<ChatMessage, Task<string>>? steer = null) =>
        _holds.Attach(interrupt, deliver, steer);

    /// <inheritdoc cref="InboxHolds.Flow"/>
    public void Flow() => _holds.Flow();

    /// <summary>
    /// Told each word with when it reaches the session, the moment it is said (STEER1) — or, said before the door is
    /// known, when it is. Called under the inbox's lock, so told in order; a listener's failure is its own.
    /// </summary>
    public void OnSaid(Action<ChatMessage, DrivenReach> said)
    {
        lock (_state.Gate) _state.Told = said;
    }

    /// <summary>
    /// Told each held word the person took back or reworded (QUEUEEDIT1a), after it was told as said: the record settles it
    /// as withdrawn. Called under the inbox's lock, in order; a listener's failure is its own.
    /// </summary>
    public void OnWithdrawn(Action<ChatMessage> withdrawn)
    {
        lock (_state.Gate) _state.Withdrawn = withdrawn;
    }

    /// <inheritdoc cref="InboxKeeps.Edit"/>
    public QueueEdit Edit(string id, string? text, MessageLevel? level = null) => _keeps.Edit(id, text, level);

    /// <inheritdoc cref="InboxKeeps.EditAsync"/>
    public Task<QueueEdit> EditAsync(string id, string? text, MessageLevel? level = null, CancellationToken ct = default) =>
        _keeps.EditAsync(id, text, level, ct);

    /// <summary>Called once, when it closes — how the registry forgets it.</summary>
    /// <param name="closing">Told as it closes.</param>
    /// <param name="personStopped">
    /// Whether the person stopped the session (COWORK1c), asked as it closes: then the words kept for its run leave the record with
    /// it, as they always left the session; closed by anything else, they stay on the record for the session's go-on.
    /// </param>
    /// <param name="left">Told the words kept for the run that stay on the record as it closes under them (COWORK1c).</param>
    internal void OnClosing(Action closing, Func<bool>? personStopped = null, Action<IReadOnlyList<ChatMessage>>? left = null)
    {
        lock (_state.Gate)
        {
            _state.Closing = closing;
            _state.PersonStoppedAsk = personStopped;
            _state.Left = left;
        }
    }

    /// <summary>
    /// The take-offs on their way (COWORK1c), in order: what completes once every kept word handed to the wire so far has left the
    /// record, or failed to and waits to be tried again. Never faults.
    /// </summary>
    public Task TakingOff
    {
        get
        {
            lock (_state.Gate) return _state.TakingOffChain;
        }
    }

    /// <inheritdoc cref="InboxKeeps.KeepAsync"/>
    public Task<InboxKept> KeepAsync(ISaidDoor door, ChatMessage message, CancellationToken ct) => _keeps.KeepAsync(door, message, ct);

    /// <summary>
    /// <see cref="KeepAsync"/> where the record can keep the word, and held in this process alone, as before, where it cannot: no
    /// service yet (<paramref name="door"/> null), one from before COWORK1c that refuses a running record, a record that refuses it
    /// for any reason, or one that did not answer. Words said to a session this process runs are never lost to a record that will not
    /// keep them; a restart loses only those. Never <see cref="InboxKeep.Refused"/>. A record that is no longer its line's head
    /// (FOUNDATION1d-ii) answers <see cref="InboxKeep.Continued"/>, never held here: the caller says the word at the head.
    /// </summary>
    public async Task<InboxKept> KeepOrHoldAsync(ISaidDoor? door, ChatMessage message, CancellationToken ct)
    {
        if (door is not null)
        {
            try
            {
                var kept = await KeepAsync(door, message, ct).ConfigureAwait(false);
                if (kept.Outcome != InboxKeep.Refused) return kept;
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or ObjectDisposedException
                                              or System.Text.Json.JsonException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                // Unanswered: held here alone, below.
            }
        }

        if (message.Level == MessageLevel.Now)
        {
            return await HoldNowAsync(message).ConfigureAwait(false) is { } now
                ? new InboxKept(InboxKeep.Held) { Reach = ReachAt(MessageLevel.Now), Now = now }
                : InboxKept.Closed;
        }

        return Hold(message) ? new InboxKept(InboxKeep.Held) { Reach = ReachAt(message.Level) } : InboxKept.Closed;
    }

    /// <inheritdoc cref="InboxHolds.Hold"/>
    public bool Hold(ChatMessage message) => _holds.Hold(message);

    /// <inheritdoc cref="InboxHolds.HoldNowAsync"/>
    public Task<SentNow?> HoldNowAsync(ChatMessage message) => _holds.HoldNowAsync(message);

    /// <inheritdoc cref="InboxHolds.HandBack"/>
    public bool HandBack(ChatMessage message) => _holds.HandBack(message);

    /// <inheritdoc cref="InboxHolds.HoldClaim"/>
    public bool HoldClaim(ChatMessage message) => _holds.HoldClaim(message);

    /// <inheritdoc cref="InboxHolds.HoldKept"/>
    public bool HoldKept(ChatMessage word, ISaidDoor door) => _holds.HoldKept(word, door);

    /// <inheritdoc cref="InboxHolds.PassOn"/>
    public bool PassOn(string channel) => _holds.PassOn(channel);

    /// <inheritdoc cref="InboxHolds.DropClaims"/>
    public void DropClaims(IReadOnlySet<string> channels) => _holds.DropClaims(channels);

    /// <inheritdoc cref="InboxRun.NextOrClose"/>
    public DrivenNext? NextOrClose() => _run.NextOrClose();

    /// <inheritdoc cref="InboxRun.TakeOrClose"/>
    public ChatMessage? TakeOrClose() => _run.TakeOrClose();

    /// <inheritdoc cref="InboxRun.TakeAllOrClose"/>
    public IReadOnlyList<ChatMessage> TakeAllOrClose() => _run.TakeAllOrClose();

    /// <inheritdoc cref="InboxRun.Close"/>
    public IReadOnlyList<ChatMessage> Close() => _run.Close();

    /// <inheritdoc cref="InboxStops.SendNowAsync"/>
    public Task<TurnStop> SendNowAsync() => _stops.SendNowAsync();

    /// <inheritdoc cref="InboxRun.Waiting"/>
    public Task Waiting() => _run.Waiting();

    /// <inheritdoc cref="InboxRun.Settling"/>
    public void Settling() => _run.Settling();

    /// <inheritdoc cref="InboxRun.Waited"/>
    public void Waited() => _run.Waited();

    /// <inheritdoc cref="InboxRun.TakeSent"/>
    public Task<string>? TakeSent() => _run.TakeSent();

    /// <inheritdoc cref="InboxRun.TakeHeld"/>
    public IReadOnlyList<ChatMessage> TakeHeld() => _run.TakeHeld();
}
