namespace Daoris.Driver;

/// <summary>
/// Where a driven session's inbox stands (<see cref="DrivenInbox"/>): the words held for the run, the answers and steers on their way, the
/// door once known and whether its first prompt flowed, the stop decided for a turn, the keeps on their way to the record and the kept words
/// on their way off it, and a transient episode's wait. The one state that the person's sends, another agent's claims, the run's takes and
/// the reader's thread all change, each under one gate.
/// </summary>
/// <remarks>
/// <para><b>What the gate guards.</b> Every field here but <see cref="Keeping"/> is read and written under <see cref="Gate"/>, and every
/// member is called under it but <see cref="ReachAt"/>, which takes it, and <see cref="Released"/>, called with it released. Called out
/// while it is held, and so told in order: the listener (<see cref="Publish"/>), the record's told and withdrawn lines
/// (<see cref="Tell"/>), the door's deliver and steer (<see cref="Send"/>, <see cref="Steer"/>), which return the answer's task without
/// waiting, and the registry's ask whether the person stopped the session. Called out with it released: the record's keeps and
/// take-offs, the registry's closing and the words left, a claim's release, and the door's interrupt.</para>
///
/// <para><b>The order of the locks.</b> <see cref="Keeping"/> is taken before the gate, never inside it: one keep at a time, so two words
/// said at once are held in the order the record kept them. A keep counts itself on its way (<see cref="KeepsOnWay"/>) under the gate
/// before it waits for <see cref="Keeping"/>, and uncounts itself after.</para>
///
/// <para><b>What a race would break.</b> The run's next step and the close are one step under the gate (<see cref="InboxRun"/>): a word
/// held between "nothing on its way" and "closed" would be answered to nobody, and a keep on its way holds the inbox open, so a word said
/// as the turn ends is this run's, never left on a record that ends under it. A word goes to the wire under the gate, so words leave in the
/// order said. A stop is decided once per turn (<see cref="StoppedTurn"/>), and the run takes no held word while <see cref="Stopping"/>
/// is on its way, which landing late would cut that word's own turn. Take-offs chain under the gate (<see cref="TakingOffChain"/>), so
/// kept words leave the record in order, and the ones that failed are tried once more before the inbox closes.</para>
/// </remarks>
internal sealed class InboxState(Action<ChatQueue> changed)
{
    internal readonly object Gate = new();
    internal readonly List<ChatMessage> Held = [];
    internal readonly Queue<Task<string>> Answers = new();
    // The steers *Send now* sent, each until the agent answers it: where it answers `injected` the words are taken.
    internal readonly Queue<Task<string>> Steers = new();
    // Each turn the run takes a held word for, counted: a turn is asked to stop once, and the run takes no held word
    // while a stop decided for the turn before is still on its way, which landing late would cut that word's own turn.
    internal int Turn;
    internal int StoppedTurn = -1;
    internal Task Stopping = Task.CompletedTask;
    internal bool Closed;
    internal bool Flowing;
    // The answer to the last prompt open is read, so the turn is over and a claim written
    // now would start one of its own. A prompt sent again opens it.
    internal bool Quiet;
    internal int IdsGiven;
    internal DrivenReach? Reach;
    internal Func<Task>? Interrupt;
    internal Func<ChatMessage, Task<string>>? Deliver;
    internal Func<ChatMessage, Task<string>>? SteerWith;
    internal Action<ChatMessage, DrivenReach>? Told;
    internal Action? Closing;
    internal Action<ChatMessage>? Withdrawn;

    // The session's one dispatcher. Words are kept on the record one at a time, so the order
    // they are held in is the record's; each keep on its way holds the inbox open, and each kept word leaves the record by its id, in
    // order, once on its way to the wire, before the inbox closes.
    internal readonly SemaphoreSlim Keeping = new(1, 1);
    internal int KeepsOnWay;
    internal TaskCompletionSource KeepsLanded = Landed();
    internal ISaidDoor? Door;
    internal Task TakingOffChain = Task.CompletedTask;
    // Ids a take-off could not take, tried again with the next one, and once more before the inbox closes.
    internal readonly List<string> Untaken = [];
    internal bool TriedAgain;
    // The record's ids this inbox held, so a producer's retry under a held key holds nothing twice.
    internal readonly HashSet<string> Known = new(StringComparer.Ordinal);
    internal Func<bool>? PersonStoppedAsk;
    internal Action<IReadOnlyList<ChatMessage>>? Left;

    // While a transient episode waits, every word is held, none goes alone, and one that
    // makes a turn ends the wait at once.
    internal bool Waiting;
    internal TaskCompletionSource Woke = Landed();

    /// <summary>When a word said now at <paramref name="level"/> reaches the session; null before the door is known and once closed. Takes the gate.</summary>
    internal DrivenReach? ReachAt(MessageLevel level)
    {
        lock (Gate) return Closed || Reach is null ? null : ReachOf(level);
    }

    /// <summary>Where a word of the person's is held, by its id; -1 once closed. Daoris's own words are no word of theirs to edit.</summary>
    internal int HeldAt(string id) => Closed ? -1 : Held.FindIndex(message => message.Id == id && message.Daoris is null && !IsClaim(message));

    /// <summary>Told with where it reaches, then sent at the next step or held for the turn's end, under its own id. Called under the gate.</summary>
    internal void HoldHere(ChatMessage said)
    {
        if (Reach is not null) Tell(said, ReachOf(said.Level));
        if (GoesAtNextStep(said)) Send(said);
        else Held.Add(said);
        // A word that makes a turn ends a transient episode's wait.
        if (Waiting && !said.Rides) Woke.TrySetResult();
        Publish();
    }

    /// <summary>A word at <c>now</c> under its own id, steered where the agent steers, held as an urgent word anywhere else. Called under the gate.</summary>
    internal SentNow NowHeld(ChatMessage said)
    {
        var onTheirWay = Answers.Any(answer => !answer.IsCompleted);
        if (Reach == DrivenReach.NextStep && Flowing && !Waiting && SteerWith is { } steer)
        {
            Tell(said, DrivenReach.Steered);
            Steer(steer, said);
            Publish();
            return new SentNow(SendNowWhy.Spelled(DrivenReach.Steered), Now: true, Why: null);
        }

        var reach = Reach;
        HoldHere(said);
        var why = reach is null || reach == DrivenReach.NextStep && !Flowing ? SendNowWhy.Opening
            : reach == DrivenReach.NextStep && onTheirWay ? SendNowWhy.Behind
            : SendNowWhy.NoInterrupt;
        return new SentNow(SendNowWhy.Spelled(reach), Now: false, why);
    }

    /// <summary>Whether the person stopped the session, as the registry knows it. Called under the gate; a failed answer is no stop.</summary>
    internal bool PersonStopped()
    {
        try
        {
            return PersonStoppedAsk?.Invoke() == true;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Kept words on their way to the wire, or taken back: off the record by their ids, after every take-off before them,
    /// awaited in order and never fired and forgotten. One that fails is tried again with the next, and once more
    /// before the inbox closes. Called under the gate.
    /// </summary>
    internal void TakeOff(IReadOnlyList<ChatMessage> words)
    {
        if (Door is not { } door) return;
        List<string> ids = [.. words.Where(word => word.Kept && word.Id is not null).Select(word => word.Id!)];
        if (ids.Count == 0) return;
        TakingOffChain = TakeOffAfterAsync(TakingOffChain, door, ids);
    }

    /// <summary>The take-offs that failed, tried once more as the inbox would close. Called under the gate.</summary>
    internal void TryAgain()
    {
        TriedAgain = true;
        if (Door is { } door) TakingOffChain = TakeOffAfterAsync(TakingOffChain, door, []);
    }

    /// <summary>One take-off, after the one before it; never faults, and what it could not take waits for the next.</summary>
    private async Task TakeOffAfterAsync(Task before, ISaidDoor door, IReadOnlyList<string> ids)
    {
        await before.ConfigureAwait(false);
        List<string> taking;
        lock (Gate)
        {
            taking = [.. Untaken, .. ids];
            Untaken.Clear();
        }

        if (taking.Count == 0) return;
        bool taken;
        try
        {
            taken = await door.TakeOffAsync(taking, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException
                                          or ObjectDisposedException or System.Text.Json.JsonException)
        {
            taken = false;
        }

        if (!taken)
        {
            lock (Gate) Untaken.InsertRange(0, taking);
        }
    }

    internal static TaskCompletionSource Landed()
    {
        var landed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        landed.SetResult();
        return landed;
    }

    /// <summary>
    /// Where it stands for the page: the person's words held, never Daoris's own, which the record shows as Daoris's, nor
    /// another agent's claim, which is never the person's.
    /// </summary>
    internal ChatQueue Now() => Closed
        ? ChatQueue.Idle
        : new ChatQueue(Taking: true, [.. Held.Where(message => message.Daoris is null && message.Helped is null && !IsClaim(message))]);

    /// <summary>Another agent's claim on the ask's channel, as this inbox carries it.</summary>
    internal static bool IsClaim(ChatMessage message) => message.Channel is { ByThisPerson: false };

    /// <summary>Whether a word held makes a turn of its own: any but a claim held only to ride. Called under the gate.</summary>
    internal bool Turns() => Held.Any(word => !word.Rides);

    /// <summary>
    /// The claims held to ride go with what the run sends anyway: each in its place, told now as reaching at
    /// the turn's end, a rider no longer. Called under the gate, once a word that makes a turn is taken.
    /// </summary>
    internal void Ride()
    {
        for (var at = 0; at < Held.Count; at++)
        {
            if (!Held[at].Rides) continue;
            Held[at] = Held[at] with { Rides = false, Level = MessageLevel.Ordinary };
            Tell(Held[at], DrivenReach.TurnEnd);
        }
    }

    /// <summary>The claims held to ride, out of the inbox: nothing took them. Called under the gate.</summary>
    internal List<ChatMessage> LetGo()
    {
        List<ChatMessage> riders = [.. Held.Where(word => word.Rides || IsClaim(word))];
        Held.RemoveAll(word => word.Rides || IsClaim(word));
        return riders;
    }

    /// <summary>Each claim let go told so: its entry's next part is handed it. Called outside the gate; a listener's failure is its own.</summary>
    internal static void Released(IReadOnlyList<ChatMessage> claims)
    {
        foreach (var claim in claims)
        {
            try
            {
                claim.Acknowledge?.Invoke(ChannelAcks.Released);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // The claim waits on the channel either way, for the entry's next part or the agent's own read.
            }
        }
    }

    /// <summary>
    /// When a word reaches the session on the door known, by its level: an ordinary word at the turn's
    /// end on every door; any other at the door's own reach. Called under the gate, once the door is known.
    /// </summary>
    internal DrivenReach ReachOf(MessageLevel level) =>
        level == MessageLevel.Ordinary ? DrivenReach.TurnEnd : Reach ?? DrivenReach.TurnEnd;

    /// <summary>Whether a word goes to the running turn now: urgent or now, on a door that takes words at its next step, its first prompt on the wire.</summary>
    internal bool GoesAtNextStep(ChatMessage message) =>
        Reach == DrivenReach.NextStep && Flowing && !Waiting && message.Level != MessageLevel.Ordinary;

    /// <summary>Send one word now, under the gate so words leave in the order said; its answer waits for the run.</summary>
    internal void Send(ChatMessage message)
    {
        Task<string> answer;
        try
        {
            // Its door marks its attempt before the wire and tells it went once written, when it leaves the record.
            answer = Deliver!(Dispatched(message));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            answer = Task.FromException<string>(error);
        }

        // Observed here as well as by the run, which may have ended before it reads this one.
        _ = answer.ContinueWith(static sent => sent.Exception, TaskScheduler.Default);
        Answers.Enqueue(answer);
        // A prompt is open again, so the turn goes on and may take a claim.
        Quiet = false;
        // Daoris's word at the next step goes now, as the look that held it judged it.
        DaorisWords.Delivered(message);
    }

    /// <summary>Steer one word into the running turn now, under the gate as <see cref="Send"/> sends; the run waits for its answer.</summary>
    internal void Steer(Func<ChatMessage, Task<string>> steer, ChatMessage message)
    {
        Task<string> answer;
        try
        {
            answer = steer(Dispatched(message));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            answer = Task.FromException<string>(error);
        }

        _ = answer.ContinueWith(static steered => steered.Exception, TaskScheduler.Default);
        Steers.Enqueue(answer);
        Quiet = false;
    }

    /// <summary>A word as this inbox hands it to its door: a kept one to be attempted before the wire and taken off once written.</summary>
    internal ChatMessage Dispatched(ChatMessage message) =>
        SaidDoor.Dispatched(message, Door, word =>
        {
            lock (Gate) TakeOff([word]);
        });

    /// <summary>Tell the record what is said. Called under the gate, so told in order; a listener's failure is its own.</summary>
    internal void Tell(ChatMessage message, DrivenReach reach)
    {
        try
        {
            Told?.Invoke(message, reach);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The words still go: the record misses them, and the session does not.
        }
    }

    /// <summary>Tell where it stands. Called under the gate, so told in order; a listener's failure is its own.</summary>
    internal void Publish()
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
