namespace Daoris.Driver;

/// <summary>
/// What a person reads of a live conversation's turns and does to them: where they stand, what a word said now would do, the press that holds
/// them, the words waiting for a turn, and stopping the one taking place.
/// </summary>
internal sealed class ChatTurnDoors(ChatContext context)
{
    /// <summary>Whether a conversation's turn is on its way to the harness or running there — false on a text-only door.</summary>
    public bool Taking(string sessionId) => context.Turned.TryGetValue(sessionId, out var chat) && chat.Turns.Running;

    /// <summary>
    /// What a word said now to a live conversation would do, in the record's spelling: <c>next-step</c> while a turn runs on a door whose agent
    /// takes words then, <c>turn-end</c> while one runs anywhere else, and null where it would be the next turn at once, or the door carries
    /// only text.
    /// </summary>
    public string? Reach(string sessionId) =>
        context.Turned.TryGetValue(sessionId, out var chat) && chat.Turns.Running
            ? chat is ProtocolChat { Session.NextStep: true } && !chat.Turns.Stopped ? "next-step" : "turn-end"
            : null;

    /// <summary>
    /// What a live conversation's door can do with a word while a turn runs: whether its agent steers, so a word at <c>now</c> cuts its step;
    /// and whether the door can stop a turn and keep the conversation, so the keeping stop is offered beside words held for the turn's end.
    /// Null for one nothing here holds, and for a text-only door, which has no turns and honours no level.
    /// </summary>
    public TurnDoor? Door(string sessionId) =>
        context.Turned.TryGetValue(sessionId, out var chat)
            ? chat is ProtocolChat protocol
                ? new TurnDoor(Steers: protocol.Session is { Steers: true }, KeepingStop: true)
                : new TurnDoor(Steers: false, KeepingStop: chat is NativeChat { Interrupts: true })
            : null;

    /// <summary>Whether this runner holds the conversation: its turns, its text door or its go-on. What it does not hold, it knows nothing of.</summary>
    public bool Holds(string sessionId) =>
        context.Turned.ContainsKey(sessionId) || context.Talking.ContainsKey(sessionId) || context.GoingOn.ContainsKey(sessionId);

    /// <summary>Where a conversation's turns stand; <see cref="ChatQueue.Idle"/> for one nothing here holds.</summary>
    public ChatQueue Queue(string sessionId) =>
        context.Turned.TryGetValue(sessionId, out var chat) ? chat.Turns.State : ChatQueue.Idle;

    /// <summary>
    /// Whether a conversation this runner holds has ended its turn with nothing waiting or opening, as a curve's move reads a turn's end
    /// (<see cref="CurveMove.TurnJustEnded"/>): the word the page's foot offers its <i>Accept…</i> by, which the session list's look in this
    /// process reads too (<see cref="SessionGroups.Between"/>). Null for one nothing here holds.
    /// </summary>
    public bool? TurnEnded(string sessionId) =>
        context.Turned.TryGetValue(sessionId, out var chat)
            ? chat.Turns.State is { Taking: false, Opening: false, Queued.Count: 0, LastTurnEnded: not null }
            : null;

    /// <inheritdoc cref="IChatPressing.HoldAsync"/>
    public Task<PressHold> HoldAsync(string sessionId, CancellationToken ct = default) =>
        context.Turned.TryGetValue(sessionId, out var chat) ? chat.Turns.PressAsync(ct) : Task.FromResult(PressHold.NotHere);

    /// <inheritdoc cref="IChatPressing.Release"/>
    public void Release(string sessionId)
    {
        if (context.Turned.TryGetValue(sessionId, out var chat)) chat.Turns.Pressed();
    }

    /// <summary>
    /// Take back or reword one word a conversation holds for its turn; <see cref="QueueEdit.Taken"/> where a turn took it already, or nothing
    /// here holds the conversation.
    /// </summary>
    /// <param name="level">A waiting word's new urgency, under its own id; null leaves it as it is.</param>
    public QueueEdit EditQueued(string sessionId, string id, string? text, MessageLevel? level = null) =>
        context.Turned.TryGetValue(sessionId, out var chat) ? chat.Turns.EditQueued(id, text, level) : QueueEdit.Taken;

    /// <summary>
    /// <see cref="EditQueued"/>, where a word kept on the conversation's record is taken back there too, and reworded as a word said is kept.
    /// </summary>
    public Task<QueueEdit> EditQueuedAsync(
        string sessionId, string id, string? text, MessageLevel? level = null, CancellationToken ct = default) =>
        context.Turned.TryGetValue(sessionId, out var chat) ? chat.Turns.EditQueuedAsync(id, text, level, ct) : Task.FromResult(QueueEdit.Taken);

    /// <summary>
    /// Stop the turn a conversation is taking and keep the conversation: what was waiting is withdrawn and handed back, and the turn in flight
    /// ends on the harness's own word.
    /// </summary>
    /// <param name="keep">
    /// Keep what was waiting for the next turn rather than withdraw it: the turn-end door's <i>Send now</i>, nothing handed back.
    /// </param>
    /// <returns><see cref="TurnStop.Nothing"/> when nothing here holds the conversation, or nothing ran.</returns>
    /// <exception cref="DriverException">The conversation's door carries only text, so it has no turn to stop.</exception>
    public Task<TurnStop> CancelTurnAsync(string sessionId, bool keep = false)
    {
        if (context.Turned.TryGetValue(sessionId, out var chat))
        {
            // What it kept is the next turn, whose end the person waits on.
            if (keep) return chat.Turns.StopKeepingAsync();

            // The turn ends on the person's own press: no answer to tell them of.
            context.Answers.Stopped(sessionId);
            return chat.Turns.StopAsync();
        }

        if (context.Talking.TryGetValue(sessionId, out var adapter))
        {
            throw new DriverException(
                $"the `{adapter.Name}` agent carries only text, so the driver cannot tell where one turn ends "
                + "and the next begins — there is no turn to stop. Finish the conversation, or stop it.");
        }

        return Task.FromResult(TurnStop.Nothing);
    }
}
