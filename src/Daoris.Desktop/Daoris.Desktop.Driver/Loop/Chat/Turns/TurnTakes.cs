namespace Daoris.Driver;

/// <summary>
/// How a conversation takes a word said to it (<see cref="ChatTurns"/>): at its level, into the turn on the wire at its next step
/// or by <i>Send now</i> where the door allows, or queued as a turn of its own; a moved word and a word handed back taken as the record's.
/// Its state is <see cref="TurnState"/>'s; the door's steers are called under the gate, as their contract says.
/// </summary>
/// <param name="steer">The door's next step: hand a word to the turn on the wire, or null where it takes none now.</param>
/// <param name="steerNow">The person's <i>Send now</i> on that door: steer a word into the turn at once, or null.</param>
/// <param name="nextStep">Whether the door takes words at the turn's next step now, asked without sending anything.</param>
internal sealed class TurnTakes(
    TurnState state, TurnPump pump, Func<ChatMessage, Task?>? steer, Func<ChatMessage, Task?>? steerNow, Func<bool>? nextStep)
{
    /// <summary>
    /// Take a word at the level it carries: an urgent one handed to the turn on the wire where the door takes words at
    /// its next step and no urgent word waits before it; an ordinary one, and any other, queued as a turn of its own. Null
    /// once the conversation is finishing, held for a hand-over's take, or gone: nothing more is heard.
    /// </summary>
    public TurnReach? Take(ChatMessage message)
    {
        bool start;
        lock (state.Gate)
        {
            // Moved to the replacement opening in its place, the word joins the words moved there.
            if (state.Onward is { } onward) return onward(message) ? TurnReach.TurnEnd : null;
            if (state.Finishing || state.Gone || state.Held) return null;

            // At its next step: only into a turn already on the wire, never one the person stopped, and never
            // ahead of an urgent word still waiting, which would make the agent read them out of the order said. An ordinary word
            // never enters a turn in flight, and an urgent one goes past it, since its sender chose sooner.
            // A helper's answer is a turn of its own, never handed to the running one, unless its ask is
            // urgent. A kept word goes to the door to be attempted before the wire and taken off the record once written.
            // Never while a transient episode waits or settles on the turn (state.Retrying); the word waits, and the wait ends.
            if (steer is not null && !TurnState.Answer(message) && message.Level != MessageLevel.Ordinary && state.InFlight && state.OnWire && !state.Retrying
                && !state.Stopped && !state.StopPending && state.Holding is null && !state.UrgentWaiting() && steer(state.Dispatched(message)) is { } answered)
            {
                state.Steered.Add(answered);
                state.OnWay.Add(answered);
                state.TheirsSteered |= message.Theirs;
                return TurnReach.NextStep;
            }

            state.Waiting.Add(state.Stamped(message));
            if (state.Retrying && TurnState.Carries(message)) state.Woke.TrySetResult();
            // Waiting for the limit, a word waits with the rest and starts nothing; so does one said while the person's
            // press holds the conversation, until it answers, and one said while a send-back is reserved, which
            // then waits behind the findings' turn.
            start = !state.Pumping && !state.Limited && !state.Pressing && !state.SendingBack;
            if (start)
            {
                state.Pumping = true;
                state.Drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        // Started outside the lock: a door that takes a turn at once runs its send before this returns,
        // so the message it took is never announced as waiting.
        if (start) _ = pump.PumpAsync();
        lock (state.Gate) state.Publish();
        return start ? TurnReach.Now : TurnReach.TurnEnd;
    }

    /// <summary>
    /// A word a keyed open moved onto this conversation's record, taken as
    /// <see cref="Take"/> takes it, as the record's own word under its id there: through <paramref name="door"/>, the record's, it is marked
    /// attempted before its turn is written and taken off once on the wire, and its id is known, so a producer's retry the record answers with
    /// that same word is acknowledged with no second turn (<see cref="KeepAsync"/>). A word whose id a keep took already is that word, and is
    /// not taken twice. Null once nothing more is heard: the word then waits on the record for its go-on.
    /// </summary>
    public TurnReach? TakeMoved(ISaidDoor door, ChatMessage message)
    {
        lock (state.Gate)
        {
            state.Door ??= door;
            // A retry the record answered with this word, kept here before the move's hand reached the turns: it is taken already.
            if (message.Id is { } id && !state.Known.Add(id)) return TurnReach.TurnEnd;
        }

        return Take(message with { Kept = true });
    }

    /// <summary>
    /// Words the agent answered <c>promptRequired</c>: the turn ended between the press and the steer, and the adapter
    /// left them to the client. They go first among what waits, the person's choice, and are the next turn, taken there under
    /// their own id since the record showed them said already. False once the conversation is finishing or gone: the caller says
    /// they never reached the agent. The turn lasts until the steer is answered, so its pump is still running to take them.
    /// </summary>
    public bool HandBack(ChatMessage message)
    {
        bool start;
        lock (state.Gate)
        {
            if (state.Finishing || state.Gone) return false;
            // Off the record once steered: waiting again in this process alone.
            state.Waiting.Insert(0, message with { Kept = false, Attempting = null, Went = null });
            start = !state.Pumping;
            if (start)
            {
                state.Pumping = true;
                state.Drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        if (start) _ = pump.PumpAsync();
        lock (state.Gate) state.Publish();
        return true;
    }

    /// <summary>
    /// The person's words at <c>now</c>, carried out by the door it has: with no turn running the words are the
    /// next turn at once; into a turn on the wire whose agent steers, they are steered at once (<c>now</c>) and the turn lasts until
    /// the steer is answered; on a door that takes words only at its next step they go there, said so; on a door that takes none
    /// during a turn they wait as <see cref="Take"/> queues them, said <c>no-interrupt</c>, and are the next turn when it ends.
    /// Null once the conversation is finishing, held or gone.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Nothing is stopped, on any door</b>: the stop that keeps the waiting words is the person's
    /// own press (<see cref="StopKeepingAsync"/>, <c>CANCEL_TURN</c> with <c>keep</c>), never a word's. On a next-step door a stop
    /// would besides answer a word on its way cancelled while the agent may still act on it. A steer is
    /// no stop: it goes past words on their way and waiting, the person's choice to go first, and they keep their places. Where the
    /// agent cannot steer, the words go behind what is ahead, said <see cref="SendNowWhy.Behind"/>.</para>
    /// </remarks>
    public Task<SentNow?> TakeNowAsync(ChatMessage message)
    {
        message = message with { Level = MessageLevel.Now };
        lock (state.Gate)
        {
            if (state.Finishing || state.Gone || state.Held) return Task.FromResult<SentNow?>(null);
            if (state.InFlight && message.Helped is null)
            {
                // Words before these: handed to this turn at its next step and not yet answered, or urgent words waiting as turns
                // of their own. Ordinary words and a helper's answer, which is ordinary, wait for the turn's end, and these go
                // past them. A helper's answer is never the person's press.
                var onTheirWay = state.OnTheirWay();
                var behind = onTheirWay || state.Holding is not null || state.UrgentWaiting();
                // Nothing is steered into a turn a transient episode waits on; the word waits and ends the wait.
                var running = state.OnWire && !state.Stopped && !state.StopPending && !state.Retrying;
                // 🔴 Steered past whatever is ahead (the owner's case): the steer cuts the step in flight, not the prompt a word on
                // its way is queued as, which is still taken at its next step; what waits keeps its place. The person chose now.
                if (running && steerNow?.Invoke(state.Dispatched(message)) is { } steered)
                {
                    state.Steered.Add(steered);
                    state.TheirsSteered |= message.Theirs;
                    return Task.FromResult<SentNow?>(new SentNow(SendNowWhy.Spelled(TurnReach.Steered), Now: true, Why: null));
                }

                // At its next step, behind any word on its way, as a word said then goes; never ahead of an urgent one still waiting.
                if (running && state.Holding is null && !state.UrgentWaiting() && steer?.Invoke(state.Dispatched(message)) is { } stepped)
                {
                    state.Steered.Add(stepped);
                    state.OnWay.Add(stepped);
                    state.TheirsSteered |= message.Theirs;
                    return Task.FromResult<SentNow?>(new SentNow(
                        SendNowWhy.Spelled(TurnReach.NextStep), Now: false, onTheirWay ? SendNowWhy.Behind : SendNowWhy.NoInterrupt));
                }

                state.Waiting.Add(state.Stamped(message));
                if (state.Retrying && TurnState.Carries(message)) state.Woke.TrySetResult();
                // Held for the turn's end: behind what is ahead on a door that takes words at its next step, and on any other, or
                // behind the person's own stop, because nothing interrupts a turn for them.
                var why = !state.Stopped && !state.StopPending && (onTheirWay || nextStep?.Invoke() == true)
                    ? behind ? SendNowWhy.Behind : SendNowWhy.Opening
                    : SendNowWhy.NoInterrupt;
                state.Publish();
                return Task.FromResult<SentNow?>(new SentNow(SendNowWhy.Spelled(TurnReach.TurnEnd), Now: false, why));
            }
        }

        // No turn in flight: the next turn at once, or behind the one the door is still opening for.
        return Task.FromResult<SentNow?>(Take(message) switch
        {
            null => null,
            TurnReach.Now => new SentNow(null, Now: true, Why: null),
            var reach => new SentNow(SendNowWhy.Spelled(reach), Now: false, SendNowWhy.Opening),
        });
    }
}
