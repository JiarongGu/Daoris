namespace Daoris.Driver;

/// <summary>
/// How a driven session's inbox holds what is said to it (<see cref="DrivenInbox"/>): the door known and its first
/// prompt flowing, the person's words at their level or at <c>now</c>, words handed back, another agent's claims held to ride or passed on,
/// and a word another producer kept. Each under <see cref="InboxState"/>'s gate.
/// </summary>
internal sealed class InboxHolds(InboxState state)
{
    /// <summary>
    /// The door, once the session is open: its way of stopping the running turn, and — where the agent takes a prompt
    /// during its turn — its way of sending one. What is held then is told with when it reaches the session; a
    /// word goes only once the door says the turn has begun (<see cref="Flow"/>), so none overtakes the prompt it follows.
    /// </summary>
    /// <param name="interrupt">
    /// Stops the running turn and keeps the session; null where nothing can stop a turn and keep the session (the native
    /// door's run), and *Send now* then stops nothing.
    /// </param>
    /// <param name="deliver">
    /// Sends one word as a prompt now and completes with its answer; null where the agent takes no prompt during a turn,
    /// and every word waits for the turn's end.
    /// </param>
    /// <param name="steer">
    /// The person's *Send now* on that door: steers one word into the running turn and completes with the agent's
    /// outcome, the words taken where it is <c>injected</c>. Null where the agent never said it steers.
    /// </param>
    public void Attach(
        Func<Task>? interrupt, Func<ChatMessage, Task<string>>? deliver = null, Func<ChatMessage, Task<string>>? steer = null)
    {
        lock (state.Gate)
        {
            state.Interrupt = interrupt;
            state.Deliver = deliver;
            state.SteerWith = deliver is null ? null : steer;
            state.Reach = deliver is null ? DrivenReach.TurnEnd : DrivenReach.NextStep;
            if (state.Closed) return;

            foreach (var message in state.Held) state.Tell(message, state.ReachOf(message.Level));
            state.Publish();
        }
    }

    /// <summary>
    /// The session's first prompt is on the wire: where words go during a turn, the urgent words held go now, and every
    /// urgent word after it the moment it is said. Nothing on a door that holds words for the turn's end.
    /// </summary>
    /// <remarks>
    /// 🔴 An ordinary word stays held for the turn's end: it never enters a turn in flight, and
    /// sending every held word here sent it with the urgent ones.
    /// </remarks>
    public void Flow()
    {
        lock (state.Gate)
        {
            state.Flowing = true;
            state.Quiet = false;
            if (state.Closed || state.Reach != DrivenReach.NextStep) return;

            foreach (var message in state.Held.Where(state.GoesAtNextStep).ToList())
            {
                // A claim held until the first prompt flows is not yet told: it is, as it goes.
                if (message.Rides) state.Tell(message, DrivenReach.NextStep);
                state.Send(message);
            }

            state.Held.RemoveAll(state.GoesAtNextStep);
            state.Publish();
        }
    }

    /// <summary>
    /// Take the person's words at the level they carry: told at once with where they reach the session, then sent where
    /// the agent takes them during its turn and they are urgent, or held for the turn's end. False once the session has stopped
    /// taking any: it is ending. Held in this process alone: what the record keeps first goes through <see cref="KeepAsync"/>.
    /// </summary>
    public bool Hold(ChatMessage message)
    {
        lock (state.Gate)
        {
            if (state.Closed) return false;
            state.HoldHere(message with { Id = $"said-{++state.IdsGiven}" });
            return true;
        }
    }

    /// <summary>
    /// The person's words at <c>now</c>, carried out by the door it has. Where the agent steers and its first
    /// prompt is on the wire, the words are steered into the running turn at once, told with the reach <c>now</c>. Anywhere else
    /// they go as <see cref="Hold"/> takes an urgent word, and the answer says why not now by its code: <c>opening</c> before the
    /// first prompt left, <c>behind</c> or <c>no-interrupt</c> at a next step, and <c>no-interrupt</c> held for the turn's end.
    /// Null once the session has stopped taking any.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>It never stops the turn</b>: on a door that holds words for the turn's end the words are
    /// held and the turn runs on. The keeping stop is the person's own press, <see cref="SendNowAsync"/>, offered beside the held
    /// words where the door has an interrupt that keeps the session (<see cref="KeepingStop"/>).</para>
    ///
    /// <para>🔴 <b>A steer goes past a word on its way</b>: the steer cuts the step in flight, not the prompt the
    /// word is queued as, which is still taken at its next step, and the person chose to go first. Where the agent cannot steer,
    /// these go at its next step behind it, said <see cref="SendNowWhy.Behind"/>. The steer is answered by the run
    /// (<see cref="NextOrClose"/>), which closes nothing while one is on its way.</para>
    /// </remarks>
    public Task<SentNow?> HoldNowAsync(ChatMessage message)
    {
        lock (state.Gate)
        {
            if (state.Closed) return Task.FromResult<SentNow?>(null);
            return Task.FromResult<SentNow?>(state.NowHeld(message with { Level = MessageLevel.Now, Id = $"said-{++state.IdsGiven}" }));
        }
    }

    /// <summary>
    /// Words the agent answered <c>promptRequired</c>: the turn ended between the press and the steer, and the adapter
    /// left them to the client. They go to the front of what is held, since the person chose to go first, and the run prompts
    /// them next. Not told again, since they were said once; the run's prompt takes them. Called while the run waits on the
    /// steer, so nothing has closed; false once it has, and the caller says they never reached the agent.
    /// </summary>
    public bool HandBack(ChatMessage message)
    {
        lock (state.Gate)
        {
            if (state.Closed) return false;
            // Off the record once steered: held again in this process alone.
            state.Held.Insert(0, message with { Kept = false, Attempting = null, Went = null });
            state.Publish();
            return true;
        }
    }

    /// <summary>
    /// Take another agent's claim at its level:
    /// urgent, at the next step where the door takes words then, told and sent as <see cref="Hold"/> sends; ordinary, and any claim on a
    /// door that takes words only at its turn's end, held only to ride (<see cref="ChatMessage.Rides"/>): it goes beside a word that
    /// makes a turn anyway, and alone at the turn's end it makes none and is let go. False before the door is known, once the session
    /// stopped taking any, and once the answer to its last open prompt is read (<see cref="TakesClaims"/>).
    /// </summary>
    public bool HoldClaim(ChatMessage message)
    {
        lock (state.Gate)
        {
            if (state.Closed || state.Reach is null || state.Quiet) return false;
            var said = message with { Id = $"said-{++state.IdsGiven}" };
            if (said.Level != MessageLevel.Ordinary && state.Reach == DrivenReach.NextStep && state.Flowing && !state.Waiting)
            {
                state.Tell(said, DrivenReach.NextStep);
                state.Send(said);
            }
            else
            {
                // 🔴 Never told while it only rides: the record shows it once it goes. Urgent on a turn-end door earns nothing sooner.
                state.Held.Add(said with { Rides = true, Level = state.Reach == DrivenReach.NextStep ? said.Level : MessageLevel.Ordinary });
            }

            state.Publish();
            return true;
        }
    }

    /// <summary>
    /// A word another producer already kept on the session's record under <see cref="ChatMessage.Id"/> (an urgent ask's
    /// helper answer, handed for this run), held as a kept word is: told, sent at the next step at its level or held for the turn's
    /// end, and taken off the record by its id through <paramref name="door"/> once on the wire. Held once by its id. False once the
    /// session stopped taking words, and the word stays on the record for its go-on. A solicited answer may make a turn happen, so the
    /// turn's end refuses it nothing.
    /// </summary>
    public bool HoldKept(ChatMessage word, ISaidDoor door)
    {
        lock (state.Gate)
        {
            if (state.Closed || word.Id is not { } id) return false;
            state.Door ??= door;
            if (!state.Known.Add(id)) return true;
            state.HoldHere(word with { Kept = true });
            return true;
        }
    }

    /// <summary>
    /// The person passed on a claim this inbox holds only to ride: by its channel id, it now makes a turn of
    /// its own, at the next step where the door takes words then and its first prompt flowed, else as the turn's end's own prompt.
    /// False where no such claim is held.
    /// </summary>
    public bool PassOn(string channel)
    {
        lock (state.Gate)
        {
            var at = state.Closed ? -1 : state.Held.FindIndex(word => word.Rides && word.Channel?.Id == channel);
            if (at < 0) return false;

            var held = state.Held[at];
            var passed = held with
            {
                Rides = false, Level = MessageLevel.Urgent, Text = held.Channel is { IsMessage: true } message ? ChannelClaims.Claim(message, passedOn: true) : held.Text,
            };
            state.Held.RemoveAt(at);
            state.HoldHere(passed);
            return true;
        }
    }

    /// <summary>
    /// The claims held to ride that the agent read itself with <c>channel_read</c>, by their channel ids: let go, never
    /// handed a second time. Words that make a turn are never dropped here.
    /// </summary>
    public void DropClaims(IReadOnlySet<string> channels)
    {
        lock (state.Gate)
        {
            if (state.Held.RemoveAll(word => word.Rides && word.Channel?.Id is { } id && channels.Contains(id)) > 0) state.Publish();
        }
    }
}
