namespace Daoris.Driver;

/// <summary>
/// The run's side of a driven session's inbox (<see cref="DrivenInbox"/>, STEER1, MSG1b): its next step taken and the inbox closed in the
/// same step under <see cref="InboxState"/>'s gate, the close that hands back what never left, and a transient episode's wait, its settling
/// and its try's words. The registry's closing and a claim's release are told with the gate released.
/// </summary>
internal sealed class InboxRun(InboxState state)
{
    /// <summary>
    /// What the run does next (STEER1): wait for a word already sent, oldest first; else prompt a word held; else, with
    /// nothing held and nothing on its way, null, and the inbox closed in the same step.
    /// </summary>
    public DrivenNext? NextOrClose()
    {
        Action? closing = null;
        DrivenNext? next = null;
        List<ChatMessage> released = [];
        // One step under the gate: a word sent between "nothing on its way" and "closed" would be answered to nobody.
        lock (state.Gate)
        {
            if (state.Closed) return null;
            if (state.Steers.TryDequeue(out var steered))
            {
                next = new DrivenNext(null, null) { Waits = steered };
            }
            else if (state.Answers.TryDequeue(out var answer))
            {
                next = new DrivenNext(null, answer);
            }
            else if (state.Turns() && !state.Stopping.IsCompleted)
            {
                // 🔴 The stop decided for the turn that ended is on its way: the held word's own turn goes after it (STEER2a).
                next = new DrivenNext(null, null) { Waits = state.Stopping };
            }
            else if (state.Turns())
            {
                // COWORK1d: the claims held to ride go with what the run sends anyway, each in the order said.
                state.Ride();
                // COWORK1c: its door marks its attempt before the wire and tells it went once written, when it leaves the record.
                next = new DrivenNext(state.Dispatched(state.Held[0]), null);
                state.Held.RemoveAt(0);
                state.Turn++;
                state.Quiet = false;
            }
            else if (state.KeepsOnWay > 0)
            {
                // 🔴 COWORK1c: a word on its way to the record is this run's, so the inbox stays open for it rather than leave it on a
                // record that concludes under it.
                next = new DrivenNext(null, null) { Waits = state.KeepsLanded.Task };
            }
            else if (!state.TakingOffChain.IsCompleted || (state.Untaken.Count > 0 && !state.TriedAgain))
            {
                // 🔴 COWORK1c: every word the session took is off the record before it can end, or a go-on would say it again.
                if (state.TakingOffChain.IsCompleted) state.TryAgain();
                next = new DrivenNext(null, null) { Waits = state.TakingOffChain };
            }
            else
            {
                // 🔴 COWORK1d (trap 3): claims alone make no turn, so the run ends and each is let go for the entry's next part.
                released = state.LetGo();
                state.Closed = true;
                closing = state.Closing;
            }

            state.Publish();
        }

        closing?.Invoke();
        InboxState.Released(released);
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
        List<ChatMessage> released = [];
        lock (state.Gate)
        {
            if (state.Closed) return null;
            if (state.Turns())
            {
                state.Ride();
                next = state.Held[0];
                state.Held.RemoveAt(0);
                state.Turn++;
                state.TakeOff([next]);
            }
            else if (state.Answers.Count > 0 || state.Steers.Count > 0)
            {
                return null;
            }
            else
            {
                next = null;
                released = state.LetGo();
                state.Closed = true;
                closing = state.Closing;
            }

            state.Publish();
        }

        closing?.Invoke();
        InboxState.Released(released);
        return next;
    }

    /// <summary>
    /// Every word held for the turn's end, in the order said — or, with none held and none on its way, none, and the inbox
    /// closed in the same step (MSG1b): the native door goes on with all of them in one resumed run, its words joined.
    /// </summary>
    public IReadOnlyList<ChatMessage> TakeAllOrClose()
    {
        Action? closing = null;
        List<ChatMessage> taken = [];
        List<ChatMessage> released = [];
        lock (state.Gate)
        {
            if (state.Closed) return [];
            // COWORK1d: claims held to ride go only beside a word that makes the resumed run anyway.
            if (state.Turns())
            {
                state.Ride();
                // COWORK1c: the resumed run marks their attempts before it starts and tells each went once it has, when they leave the record.
                taken = [.. state.Held.Select(state.Dispatched)];
                state.Held.Clear();
                state.Turn++;
            }

            if (taken.Count == 0 && state.Answers.Count == 0 && state.Steers.Count == 0)
            {
                released = state.LetGo();
                state.Closed = true;
                closing = state.Closing;
            }

            state.Publish();
        }

        closing?.Invoke();
        InboxState.Released(released);
        return taken;
    }

    /// <summary>
    /// Close it whatever is held — the session failed or was stopped — and hand back what never left it, so the driver
    /// can say so rather than drop the person's words without a trace. A word already sent is the session's: the run
    /// says which of those it never took.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Nothing undelivered vanishes</b> (COWORK1c, the message-urgency design §6): a word kept on the record stays there when the
    /// session closes under it (its door failed, the shell shut down), told as left (<see cref="OnClosing"/>), and the session's go-on
    /// takes it; only words held in this process alone come back. Where the person stopped the session, the kept words leave the
    /// record and come back with the rest, as a stop always ended them, rather than restart what the person stopped.
    /// </remarks>
    public IReadOnlyList<ChatMessage> Close()
    {
        Action? closing;
        List<ChatMessage> left;
        List<ChatMessage> stays = [];
        List<ChatMessage> released;
        Action<IReadOnlyList<ChatMessage>>? told = null;
        lock (state.Gate)
        {
            if (state.Closed) return [];
            // COWORK1d: another agent's claim never comes back as the person's words never reached: it is let go for the entry's next part.
            released = state.LetGo();
            left = [.. state.Held];
            state.Held.Clear();
            state.Answers.Clear();
            state.Steers.Clear();
            state.Closed = true;
            closing = state.Closing;
            if (left.Any(word => word.Kept))
            {
                if (state.PersonStopped())
                {
                    state.TakeOff(left);
                }
                else
                {
                    stays = [.. left.Where(word => word.Kept)];
                    left = [.. left.Where(word => !word.Kept)];
                    told = state.Left;
                }
            }

            state.Publish();
        }

        closing?.Invoke();
        InboxState.Released(released);
        if (stays.Count > 0)
        {
            try
            {
                told?.Invoke(stays);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // The words wait on the record either way; whoever shows them misses the line.
            }
        }

        return left;
    }

    /// <summary>
    /// A transient episode's wait begins (AGENTERRORS1a, the design §5.1): from now every word is held, its mid-turn route and the steer
    /// sending nothing, so none goes alone and none is cut by a go-on sent after it. Completes once a word that makes a turn is held, at
    /// once where one already is; never while the inbox is closed.
    /// </summary>
    public Task Waiting()
    {
        lock (state.Gate)
        {
            state.Waiting = true;
            state.Woke = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!state.Closed && state.Turns()) state.Woke.TrySetResult();
            return state.Woke.Task;
        }
    }

    /// <summary>
    /// A transient episode settles its wire (Codex's owed read of D210, finding 2): from its first failure, and from each try's, every word
    /// is held as during a wait, so none joins the turns the episode settles and the turns it must settle are only those already written.
    /// </summary>
    public void Settling()
    {
        lock (state.Gate) state.Waiting = true;
    }

    /// <summary>The episode's wait is over (AGENTERRORS1a): a word said from now goes as it did, at its next step where it is urgent.</summary>
    public void Waited()
    {
        lock (state.Gate) state.Waiting = false;
    }

    /// <summary>
    /// The answer of the oldest word sent to the turn on the wire and not yet waited for (AGENTERRORS1a, the design §5.1's settling
    /// turns), taken without closing anything; null where none is on its way.
    /// </summary>
    public Task<string>? TakeSent()
    {
        lock (state.Gate) return !state.Closed && state.Answers.TryDequeue(out var answer) ? answer : null;
    }

    /// <summary>
    /// Every word held that a try carries (AGENTERRORS1a, the design §5.1), in the order said, with the claims held to ride beside them,
    /// each dispatched as <see cref="NextOrClose"/> dispatches one: never closing the inbox, and none where no word makes a turn.
    /// </summary>
    public IReadOnlyList<ChatMessage> TakeHeld()
    {
        lock (state.Gate)
        {
            if (state.Closed || !state.Turns()) return [];
            state.Ride();
            List<ChatMessage> taken = [.. state.Held.Select(state.Dispatched)];
            state.Held.Clear();
            state.Turn++;
            state.Quiet = false;
            state.Publish();
            return taken;
        }
    }
}
