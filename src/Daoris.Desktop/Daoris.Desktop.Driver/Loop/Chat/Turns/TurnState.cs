namespace Daoris.Driver;

/// <summary>
/// Where one conversation's turns stand (<see cref="ChatTurns"/>): the words that wait and the one the door opens for, the turn in flight
/// and the words handed to it, the holds a hand-over, a limit, a press or a send-back put on the turns, the stop decided for a turn, and the
/// kept words on their way off the record. The one state that the person's sends, the pump, the door's callbacks and the shell's moves all
/// change, each under one gate.
/// </summary>
/// <remarks>
/// <para><b>What the gate guards.</b> Every field here but <see cref="Keeping"/> is read and written under <see cref="Gate"/>, and every
/// member is called under it but <see cref="TakingOff"/>, which takes it. Called out while it is held, and so never calling back into the
/// turns: the listener (<see cref="Publish"/>, so where the turns stand is told in the order it moved), the door's next step and its steer
/// (<see cref="TurnTakes"/>, <see cref="TurnKeeps"/>), a limit's wake's composer and a moved-out conversation's onward take
/// (<see cref="TurnHolds"/>). Called out with it released: the door's open, its take of a turn and its interrupt, the settling of a turn's
/// words, the record's keeps and take-offs, the answers-alone judge, a send-back's hand and history, and a turn's answering.</para>
///
/// <para><b>The order of the locks.</b> <see cref="Keeping"/> is taken before the gate, never inside it: one keep at a time
/// (<see cref="TurnKeeps"/>), so two words said at once wait in the order the record kept them, and a press or a send-back is taken after
/// any word whose keeping is in flight has reached the queue.</para>
///
/// <para><b>What a race would break.</b> The pump is started only by whoever set <see cref="Pumping"/> under the gate, outside it, and it
/// clears the flag under the gate only when it finds nothing waiting: so one pump runs at a time, and a word added as it stops is either
/// taken by it or starts the next. A stop is decided for one turn (<see cref="Turn"/>), and the next turn waits on
/// <see cref="Stopping"/> before it goes, so a late stop never cuts the turn after the one it meant. The words handed to a turn at its next
/// step (<see cref="Steered"/>) are found empty under the gate that ends the turn, so none is handed to a turn that has ended. Take-offs
/// chain under the gate (<see cref="TakingOffChain"/>), so kept words leave the record in the order they left the queue, and one that
/// failed is tried again with the next.</para>
/// </remarks>
internal sealed class TurnState(Action<ChatQueue> changed)
{
    internal readonly object Gate = new();
    internal readonly List<ChatMessage> Waiting = [];
    // The words handed to the turn in flight at its next step, each until the agent answers it: the turn lasts as long.
    internal readonly List<Task> Steered = [];
    // Of those, the words sent at its next step and not yet answered: on their way, which no stop and no steer may overtake.
    // Pruned as they are answered.
    internal readonly List<Task> OnWay = [];
    // Each turn put in flight, counted: a stop is decided for one turn, and the next turn waits until that stop is
    // on the wire or dropped, so a stop that lands late never cuts the turn after the one it is meant for.
    internal int Turn;
    internal Task Stopping = Task.CompletedTask;
    internal ChatMessage? Holding;
    // The ids this queue gave the words that wait: the page's handle on one, never carried into the turn it
    // becomes, which a conversation's record has always shown without.
    internal readonly HashSet<string> Stamps = [];
    internal int IdsGiven;
    internal bool Pumping;
    internal bool Opening;
    internal bool InFlight;
    internal bool OnWire;
    internal bool StopPending;
    // The person stopped the turn in flight: what they say next waits for it to end, never joins a turn winding up.
    internal bool Stopped;
    internal bool Finishing;
    // Held for a hand-over's take: no word starts a turn the take's end would cut.
    internal bool Held;
    // Held for a move whose open moves the old part's words, so a word said meanwhile is kept on the record and waits here, behind
    // the hold, for a release that did not end the conversation; gone, it stays on the record for the open.
    internal bool KeepsHeld;
    internal readonly List<ChatMessage> HeldBack = [];
    internal bool CloseHeld;
    // Waiting for its account's limit to reset: a word is kept and waits, and starts no turn on a process
    // whose account would refuse it, until the wake hands them all over in one (WakeFromLimit) or a move takes them (HoldLimited).
    internal bool Limited;
    // Reserved for the person's press on a chat run's work: a word is kept and waits, shown
    // queued, and starts no turn, as under Limited, until the press answers, landed or refused.
    internal bool Pressing;
    // Its open refused before any prompt, its words moved to the replacement opening for it: every word
    // said to it since joins them, never this queue.
    internal Func<ChatMessage, bool>? Onward;
    // Reserved for a second opinion's send-back: from the reservation until the service's hand
    // answers, a word is kept and waits, starting no turn, so the findings word the hand kept becomes the next turn ahead of it.
    internal bool SendingBack;
    // The turn in flight's message: a word of the person's on the wire counts in their turn count before its event is read.
    internal ChatMessage? Current;
    // A word of the person's handed to the turn in flight at its next step or by *Send now*: the answering turn's
    // reading then holds their work too.
    internal bool TheirsSteered;
    internal bool Gone;
    // A transient episode is waiting, or settling its wire, on the turn in flight: every word
    // is kept and waits, handed to no turn alongside it, and a word of the person's that waits ends the wait at once as the next try.
    internal bool Retrying;
    internal TaskCompletionSource Woke = Completed();
    internal ChatQueue Published = ChatQueue.Idle;
    internal TaskCompletionSource Drained = Completed();
    internal DateTimeOffset? LastEnded;

    // The conversation's one dispatcher. Words are kept on its record one at a time, so the
    // order they wait in is the record's; each kept word leaves the record by its id, in order, once its turn is on the wire or it is
    // handed to the turn there, and before the turns finish.
    internal readonly SemaphoreSlim Keeping = new(1, 1);
    internal ISaidDoor? Door;
    internal Task TakingOffChain = Task.CompletedTask;
    internal readonly List<string> Untaken = [];
    internal readonly HashSet<string> Known = new(StringComparer.Ordinal);

    // The answers-alone bound's judge, named by whoever says a helper's answer.
    internal Func<ChatMessage, Task<bool>>? Alone;

    /// <summary>
    /// Told how many words a turn left waiting on their record because the record did not name them marked attempted:
    /// nothing is written for them, and the conversation says so. Null says nothing.
    /// </summary>
    internal Action<int>? Unsent { get; set; }

    /// <summary>Whether the shell is closing: asked as a held take is asked for, and before each turn is taken.</summary>
    internal Func<bool>? Closing { get; set; }

    /// <summary>The take-offs on their way, in order: what completes once every kept word that left so far is off the record. Never faults.</summary>
    internal Task TakingOff
    {
        get
        {
            lock (Gate) return TakingOffChain;
        }
    }

    /// <summary>The person's word waiting, the one the door opens for among them, by its id; null where none is. Called under the lock.</summary>
    internal ChatMessage? WaitingWord(string id) =>
        Holding is { Theirs: true } holding && holding.Id == id ? holding
        : Waiting.FirstOrDefault(message => message.Theirs && message.Id == id);

    /// <summary>
    /// Whether a word that asks for the turn's next step waits: the person's urgent or now words, never an ordinary one
    /// nor a helper's answer, which is ordinary. An urgent word goes past ordinary ones, never past an urgent one
    /// said before it. Called under the lock.
    /// </summary>
    internal bool UrgentWaiting(int? before = null) =>
        Waiting.Take(before ?? Waiting.Count).Any(message => !Answer(message) && message.Level != MessageLevel.Ordinary);

    /// <summary>
    /// A helper's answer that waits for the turn's end as a turn of its own: every one but an urgent ask's, which
    /// goes at the next step where the door takes words then.
    /// </summary>
    internal static bool Answer(ChatMessage message) => message.Helped is { } helped && helped.Level != MessageLevel.Urgent;

    /// <summary>A waiting word without the queue's own id, which no record knows: its record's id stays. Called under the lock.</summary>
    internal ChatMessage Unstamped(ChatMessage message) =>
        message.Id is { } stamped && Stamps.Remove(stamped) ? message with { Id = null } : message;

    /// <summary>
    /// Whether a word waiting is one a transient try carries: the person's own, which the
    /// record has not shown waiting already. A helper's answer, Daoris's word and the words a conversation goes on with are turns of their
    /// own, kept for after the turn that failed.
    /// </summary>
    internal static bool Carries(ChatMessage message) => message.Theirs && message.GoesOn is null && message.Answering is null;

    /// <summary>Whether a word handed to the turn at its next step is still unanswered: on its way. Called under the lock.</summary>
    internal bool OnTheirWay()
    {
        OnWay.RemoveAll(word => word.IsCompleted);
        return OnWay.Count > 0;
    }

    /// <summary>The queue's id for a word that has none: the page's handle on it while it waits. Called under the lock.</summary>
    internal ChatMessage Stamped(ChatMessage message)
    {
        if (message.Id is not null || message.OnRecord) return message;
        var id = $"said-{++IdsGiven}";
        Stamps.Add(id);
        return message with { Id = id };
    }

    internal ChatQueue Now() => new(Pumping, Snapshot(), LastEnded, Opening, Pressing);

    // The words a conversation goes on with are left out: its record shows them waiting already, at its foot. So is Daoris's
    // own word: the queue is the person's, which the composer draws with its edits.
    internal List<ChatMessage> Snapshot()
    {
        List<ChatMessage> now = Holding is { Theirs: true } ? [Holding] : [];
        now.AddRange(Waiting.Where(message => message.Theirs));
        return now;
    }

    /// <summary>Tell where the turns stand when it differs from what is last told. Called under the gate, so told in order.</summary>
    internal void Publish()
    {
        var now = Now();
        if (now.Taking == Published.Taking && now.Queued.SequenceEqual(Published.Queued)
            && now.LastTurnEnded == Published.LastTurnEnded && now.Opening == Published.Opening && now.Pressing == Published.Pressing) return;
        Published = now;
        try
        {
            changed(now);
        }
        catch (Exception)
        {
            // A listener's failure is its own; the queue has moved either way.
        }
    }

    /// <summary>A word as these turns hand it to the door at its next step: a kept one to be attempted before the wire and taken off once written.</summary>
    internal ChatMessage Dispatched(ChatMessage message) =>
        SaidDoor.Dispatched(message, Door, word =>
        {
            lock (Gate) TakeOff([word]);
        });

    /// <summary>
    /// Kept words on their way to the wire, handed back to the person or taken back: off the record by their ids, after
    /// every take-off before them, awaited in order and never fired and forgotten. One that fails is tried again with
    /// the next. Called under the lock.
    /// </summary>
    internal void TakeOff(IReadOnlyList<ChatMessage> words)
    {
        if (Door is not { } door) return;
        List<string> ids = [.. words.Where(word => word.Kept && word.Id is not null).Select(word => word.Id!)];
        if (ids.Count == 0) return;
        TakingOffChain = TakeOffAfterAsync(TakingOffChain, door, ids);
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

    internal static TaskCompletionSource Completed()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        done.SetResult();
        return done;
    }
}
