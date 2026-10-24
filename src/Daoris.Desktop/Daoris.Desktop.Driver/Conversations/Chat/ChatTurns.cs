namespace Daoris.Driver;

/// <summary>
/// One conversation's turns: one at a time, in the order the person sent them — the same on both doors
/// (CONV3b, CONV4a).
/// </summary>
/// <remarks>
/// <para><b>A message sent while a turn runs waits for it</b>, and the door records it when it is sent,
/// not when it is typed. Recorded at once, it would sit inside the turn before it, and the conversation
/// would read as though the agent answered a question it had not been asked. The native door used to
/// write it to the harness at once, where Claude Code may fold it into the running turn — so both doors
/// hold it here instead, and neither depends on what a harness does with a line mid-turn.</para>
///
/// <para><b>Where the turns stand is told as it moves</b> (<paramref name="changed"/>): whether a turn is
/// in flight, which the page's stop follows — the record lags it — and what is waiting, which the page
/// shows as queued because nothing else shows it. The one the door is still opening for counts as
/// waiting; one the door takes at once is never announced.</para>
///
/// <para><b>A stop withdraws what was waiting and stops the turn in flight</b>, in that order: stopped
/// the other way round, the next message would start the moment the stopped turn ended. A stop that
/// lands after a turn was taken but before it reached the harness waits for it to be sent, so its
/// interrupt never overtakes the turn it means to stop.</para>
/// </remarks>
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
/// The door's next step (MSG1c, D137 §2.1): hand a word to the turn on the wire at once, returning what completes when the
/// agent has answered it, or null where the door takes no word during a turn now. Called under the turns' own lock, so it
/// must not call back into them. Null for a door that never does (the native door, a text pipe).
/// </param>
/// <param name="steerNow">
/// The person's *Send now* on that door (STEER2a, D136's 2026-10-16 note): steer a word into the turn on the wire at once,
/// cutting the step in flight, returning what completes when the agent has answered the steer, or null where the agent never
/// said it steers. Called under the turns' own lock, as <paramref name="steer"/> is. Null for a door that never does.
/// </param>
/// <param name="nextStep">
/// Whether the door takes words at the turn's next step now (MSG1c), asked without sending anything: on such a door *Send now*
/// never stops a turn, since a word may be on its way (D136 point 4). Called under the turns' own lock. Null for a door that
/// never does.
/// </param>
internal sealed class ChatTurns(
    Func<Task<bool>> ready,
    Func<ChatMessage, Action, Task> take,
    Func<Task> interrupt,
    Action<ChatQueue> changed,
    Func<ChatMessage, Task?>? steer = null,
    Func<ChatMessage, Task?>? steerNow = null,
    Func<bool>? nextStep = null)
{
    private readonly object _gate = new();
    private readonly List<ChatMessage> _waiting = [];
    // The words handed to the turn in flight at its next step (MSG1c), each until the agent answered it: the turn lasts as long.
    private readonly List<Task> _steered = [];
    // Of those, the words sent at its next step and not yet answered: on their way, which no stop and no steer may overtake
    // (D136 point 4, STEER2a). Pruned as they are answered.
    private readonly List<Task> _onTheirWay = [];
    // Each turn put in flight, counted (STEER2a): a stop is decided for one turn, and the next turn waits until that stop is
    // on the wire or dropped, so a stop that lands late never cuts the turn after the one it was meant for.
    private int _turn;
    private Task _stopping = Task.CompletedTask;
    private ChatMessage? _holding;
    // The ids this queue gave the words that wait (QUEUEEDIT1a): the page's handle on one, never carried into the turn it
    // becomes, which a conversation's record has always shown without.
    private readonly HashSet<string> _stamped = [];
    private int _said;
    private bool _pumping;
    private bool _opening;
    private bool _inFlight;
    private bool _sent;
    private bool _stopPending;
    // The person stopped the turn in flight: what they say next waits for it to end, never joins a turn winding up (MSG1c).
    private bool _stopped;
    private bool _finishing;
    // Held for a hand-over's take (SWITCH1c-ii): no word starts a turn the take's end would cut.
    private bool _held;
    private bool _closeHeld;
    // Waiting for its account's limit to reset (LIMITRESUME1c, D206 §3.5): a word is kept and waits, and starts no turn on a process
    // whose account would refuse it, until the wake hands them all over in one (WakeFromLimit) or a move takes them (HoldLimited).
    private bool _limited;
    // Reserved for the person's Accept on a chat run (CHATFLOW1d, the chat-flow design §5.2 point 1): a word is kept and waits, shown
    // queued, but starts no turn, from the press's capture through its landing hold, until the press answers (Pressed).
    private bool _pressing;
    private bool _gone;
    private ChatQueue _published = ChatQueue.Idle;
    private TaskCompletionSource _drained = Completed();
    private DateTimeOffset? _lastEnded;

    // COWORK1c (the message-urgency design §6): the conversation's one dispatcher. Words are kept on its record one at a time, so the
    // order they wait in is the record's; each kept word leaves the record by its id, in order, once its turn is on the wire or it is
    // handed to the turn there, and before the turns finish.
    private readonly SemaphoreSlim _keeping = new(1, 1);
    private ISaidDoor? _door;
    private Task _takingOff = Task.CompletedTask;
    private readonly List<string> _untaken = [];
    private readonly HashSet<string> _known = new(StringComparer.Ordinal);

    /// <summary>
    /// The record's door for words kept there before this queue took them (COWORK1c): a helper's answer, kept by the driver, taken off by
    /// its id once its turn is on the wire, in the same order as the person's words. The first door named stays.
    /// </summary>
    internal void Keeps(ISaidDoor door)
    {
        lock (_gate) _door ??= door;
    }

    // COWORK1e (D188 point 4; the message-urgency design §2.3): the answers-alone bound's judge, named by whoever says a helper's answer.
    private Func<ChatMessage, Task<bool>>? _alone;

    /// <summary>
    /// The answers-alone bound's judge of a helper's answer as its turn comes (COWORK1e): true where the bound holds it for the person,
    /// and the answer then goes no further, kept on its record. Named by whoever says a helper's answer, as <see cref="Keeps"/> names the
    /// door; the first named stays.
    /// </summary>
    internal void Bounds(Func<ChatMessage, Task<bool>> alone)
    {
        lock (_gate) _alone ??= alone;
    }

    /// <summary>
    /// Whether the bound holds this helper's answer (COWORK1e). A judge that cannot judge holds nothing, as a line whose events do not
    /// read counts nothing (the steward's choice 2).
    /// </summary>
    private async Task<bool> HeldAloneAsync(ChatMessage message)
    {
        Func<ChatMessage, Task<bool>>? alone;
        lock (_gate) alone = _alone;
        if (message.Helped is null || alone is null) return false;
        try
        {
            return await alone(message).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The take-offs on their way (COWORK1c), in order: what completes once every kept word that left so far is off the record. Never faults.</summary>
    public Task TakingOff
    {
        get
        {
            lock (_gate) return _takingOff;
        }
    }

    /// <summary>
    /// Keep a word on the conversation's record first, then take it as <see cref="Take"/> or, at <c>now</c>,
    /// <see cref="TakeNowAsync"/> takes it, under the record's id (COWORK1c, D188 point 6; the message-urgency design §6). One keep at
    /// a time: two said at once wait in the order the record kept them (trap 20). A record that will not keep it, or does not answer,
    /// leaves it waiting here alone, as before. Null, keeping nothing, once nothing more is heard (finishing, held for a hand-over's
    /// take, or gone): such words must not land on a record that is ending (HANDWORDS1).
    /// </summary>
    public async Task<TurnKept?> KeepAsync(ISaidDoor door, ChatMessage message, CancellationToken ct)
    {
        await _keeping.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            lock (_gate)
            {
                if (_finishing || _gone || _held) return null;
            }

            SayAnswer? said;
            try
            {
                said = await door.KeepAsync(message, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or ObjectDisposedException
                                              or System.Text.Json.JsonException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                said = null;
            }

            var id = said is { Kept: true, Word.Id: { } kept } ? kept : null;
            var word = id is null ? message : message with { Id = id, Kept = true };
            if (id is not null)
            {
                lock (_gate)
                {
                    _door ??= door;
                    // A producer's retry under the key of a word that waits already: taken once.
                    if (!_known.Add(id)) return new TurnKept(TurnReach.TurnEnd, id);
                }
            }

            if (word.Level == MessageLevel.Now)
            {
                if (await TakeNowAsync(word).ConfigureAwait(false) is { } now) return new TurnKept(TurnReach.Steered, id) { Now = now };
            }
            else if (Take(word) is { } reach)
            {
                return new TurnKept(reach, id);
            }

            // Nothing more is heard since it was kept: off the record again, which is ending.
            lock (_gate) TakeOff([word]);
            return null;
        }
        finally
        {
            _keeping.Release();
        }
    }

    /// <summary>Where the turns stand now — what a page that just opened the conversation is told.</summary>
    public ChatQueue State
    {
        get
        {
            lock (_gate) return Now();
        }
    }

    /// <summary>Whether a turn is on its way to the harness or running there.</summary>
    public bool Running
    {
        get
        {
            lock (_gate) return _pumping;
        }
    }

    /// <summary>Whether the person stopped the turn in flight, which a word said now then waits for (MSG1c).</summary>
    public bool Stopped
    {
        get
        {
            lock (_gate) return _inFlight && _stopped;
        }
    }

    /// <summary>
    /// The line a conversation keeps of a word handed to its turn at the next step (MSG1c, D137 §2.1), once the agent answered
    /// it: where the person's stop was asked while the word was on its way and the agent answered it <c>cancelled</c>, the
    /// agent may have read it and its answer was not kept (STEER1 §3). Null for any other answer, which is in the turn.
    /// </summary>
    /// <remarks>
    /// Its code (MSG1c3), which the page words as <c>work.conversation.lost</c> in the reader's language, and the word's id, so
    /// the page settles the word it still shows waiting (MSG1f). The English beside them is the console's and the terminal's.
    /// </remarks>
    /// <param name="word">The word's id, as the record showed it waiting.</param>
    /// <param name="stoppedUnder">The person's stop was asked while the word was on its way.</param>
    /// <param name="stopReason">The agent's answer to the word.</param>
    public static SessionEvent? Lost(string word, bool stoppedUnder, string stopReason) =>
        stoppedUnder && stopReason == "cancelled"
            ? new SessionEvent { Kind = SessionEventKind.Note, Text = LostLine, Words = [word], Code = SessionEventCodes.Lost }
            : null;

    /// <summary>The driver's English for <see cref="Lost"/>: what its console says and a page that does not know the code shows.</summary>
    public const string LostLine = "— it may have read what you added; its answer was not kept.";

    /// <summary>
    /// The line a driven session's record keeps of a held word the person took back or reworded (QUEUEEDIT1a): its code, which
    /// the page words as <c>work.conversation.withdrawn</c>, and the word's id, so the page settles the word it showed waiting.
    /// </summary>
    public static SessionEvent Withdrawn(string word) => SessionNotes.Withdrawn(word);

    /// <summary>The driver's English for <see cref="Withdrawn"/>.</summary>
    public const string WithdrawnLine = SessionNotes.WithdrawnLine;

    /// <summary>
    /// Take back, reword, or change the level of one word that still waits (QUEUEEDIT1a, COWORK1a): with neither
    /// <paramref name="text"/> nor <paramref name="level"/> it is removed; with words, they replace its words where it waits, its
    /// place and its files kept, under a new id; with a level, it keeps its id and asks to reach the agent then. Raised while a turn
    /// is on the wire whose door takes words at its next step, it goes into that turn as a word said now would (the design §8.2),
    /// steered past what waits where it is <c>now</c> and the agent steers. The word taken while the door opens is still the
    /// queue's. <see cref="QueueEdit.Taken"/> where a turn took it already. A conversation's queued words are in no record until
    /// taken (D76), so nothing is written.
    /// </summary>
    public QueueEdit EditQueued(string id, string? text, MessageLevel? level = null)
    {
        lock (_gate)
        {
            return EditedHere(id, text, level, newId: null);
        }
    }

    /// <summary>
    /// <see cref="EditQueued"/>, and a word kept on the record reworded as a word said is kept (COWORK1c): the new words on the record
    /// first, under their own id, then in the old one's place, which leaves the record. One a turn took meanwhile is the turn's, and the
    /// new words leave the record again. Where the record will not keep them, they wait here alone.
    /// </summary>
    public async Task<QueueEdit> EditQueuedAsync(string id, string? text, MessageLevel? level = null, CancellationToken ct = default)
    {
        ChatMessage? old;
        ISaidDoor? door;
        lock (_gate)
        {
            old = Waiting(id);
            door = _door;
        }

        if (old is not { Kept: true } || text is null || door is null) return EditQueued(id, text, level);

        await _keeping.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            SayAnswer? said;
            try
            {
                said = await door.KeepAsync(old with { Text = text, Level = level ?? old.Level, Key = null, Id = null, Kept = false }, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                said = null;
            }

            var kept = said is { Kept: true, Word.Id: { } newId } ? newId : null;
            lock (_gate)
            {
                if (Waiting(id) is null)
                {
                    if (kept is not null) TakeOff([new ChatMessage(text, []) { Id = kept, Kept = true }]);
                    return QueueEdit.Taken;
                }

                if (kept is not null) _known.Add(kept);
                return EditedHere(id, text, level, kept);
            }
        }
        finally
        {
            _keeping.Release();
        }
    }

    /// <summary>The person's word waiting, the one the door opens for among them, by its id; null where none is. Called under the lock.</summary>
    private ChatMessage? Waiting(string id) =>
        _holding is { Theirs: true } holding && holding.Id == id ? holding
        : _waiting.FirstOrDefault(message => message.Theirs && message.Id == id);

    /// <summary>
    /// One waiting word taken back, reworded (under <paramref name="newId"/>, the record's, where it kept the new words), or at a new
    /// level. A kept word taken back or reworded leaves the record (COWORK1c), so a restart never brings back what the person changed.
    /// Called under the lock.
    /// </summary>
    private QueueEdit EditedHere(string id, string? text, MessageLevel? level, string? newId)
    {
        var withdrawn = text is not null || level is null;
        if (_holding is { Theirs: true } holding && holding.Id == id)
        {
            _holding = text is null && level is null ? null : Changed(holding, text, level, newId);
            if (withdrawn) TakeOff([holding]);
        }
        else
        {
            var at = _waiting.FindIndex(message => message.Theirs && message.Id == id);
            if (at < 0) return QueueEdit.Taken;
            var old = _waiting[at];
            _waiting.RemoveAt(at);
            if (withdrawn) TakeOff([old]);
            if (text is not null || level is not null)
            {
                var word = Changed(old, text, level, newId);
                if (!IntoTheTurn(word, at)) _waiting.Insert(at, word);
            }
        }

        Publish();
        return QueueEdit.Done;
    }

    /// <summary>A waiting word reworded under a new id, or at a new level under its own. Called under the lock.</summary>
    private ChatMessage Changed(ChatMessage message, string? text, MessageLevel? level, string? kept = null) =>
        (text is null ? message : Reworded(message, text, kept)) with { Level = level ?? message.Level };

    /// <summary>A waiting word reworded: under the record's new id where it kept the new words (COWORK1c), else under the queue's own.</summary>
    private ChatMessage Reworded(ChatMessage message, string text, string? kept)
    {
        if (kept is not null) return message with { Text = text, Id = kept, Kept = true };
        var id = $"said-{++_said}";
        _stamped.Add(id);
        return message with { Text = text, Id = id, Kept = false };
    }

    /// <summary>
    /// A waiting word raised (COWORK1a): handed to the turn on the wire where its level and the door allow, as a word said now goes
    /// there; at <c>now</c> steered past what waits where the agent steers, and at its next step only with no urgent word waiting
    /// before it. False where it waits on. Called under the lock, with the word out of the queue.
    /// </summary>
    private bool IntoTheTurn(ChatMessage word, int at)
    {
        if (word.Level == MessageLevel.Ordinary || Answer(word) || !_inFlight || !_sent || _stopped || _stopPending
            || _holding is not null) return false;

        // Without the queue's id, as a turn is sent (QUEUEEDIT1a): the door gives a word handed to the turn its own.
        var stamped = word.Id is { } queued && _stamped.Remove(queued) ? queued : null;
        var handed = stamped is null ? word : word with { Id = null };
        if (word.Level == MessageLevel.Now && steerNow?.Invoke(Dispatched(handed)) is { } steered)
        {
            _steered.Add(steered);
            return true;
        }

        if (!UrgentWaiting(before: at) && steer?.Invoke(Dispatched(handed)) is { } stepped)
        {
            _steered.Add(stepped);
            _onTheirWay.Add(stepped);
            return true;
        }

        if (stamped is not null) _stamped.Add(stamped);
        return false;
    }

    /// <summary>
    /// Whether a word that asks for the turn's next step waits (COWORK1a): the person's urgent or now words, never an ordinary one
    /// nor a helper's answer, which is ordinary (the design §5). An urgent word goes past ordinary ones, never past an urgent one
    /// said before it. Called under the lock.
    /// </summary>
    private bool UrgentWaiting(int? before = null) =>
        _waiting.Take(before ?? _waiting.Count).Any(message => !Answer(message) && message.Level != MessageLevel.Ordinary);

    /// <summary>
    /// A helper's answer that waits for the turn's end as a turn of its own (MIXAGENT2a-iii): every one but an urgent ask's, which
    /// goes at the next step where the door takes words then (COWORK1d, the message-urgency design §5).
    /// </summary>
    private static bool Answer(ChatMessage message) => message.Helped is { } helped && helped.Level != MessageLevel.Urgent;

    /// <summary>Queue a turn. False once the conversation is finishing, held or gone: nothing more will be heard.</summary>
    public bool Say(ChatMessage message) => Take(message) is not null;

    /// <summary>
    /// Hold the turns for a hand-over's take (SWITCH1c-ii; the switch design §4, never mid-turn): true where no turn runs and no
    /// word waits, and from then on a word takes nothing (<see cref="Take"/> is null) until <see cref="Release"/>. False where a
    /// turn runs or a word waits, whose turn ends first; or where the conversation is finishing or gone.
    /// </summary>
    /// <remarks>
    /// Asked and set under the turns' own lock, so a word said as a move begins either started its turn first, and the move waits
    /// for that turn's end, or finds the turns held. Checked first and held after, a word between the two started a turn the take's
    /// end then cut.
    /// </remarks>
    public bool Hold()
    {
        lock (_gate)
        {
            if (_pumping || _holding is not null || _waiting.Count > 0 || _finishing || _gone || _held) return false;
            _held = true;
            return true;
        }
    }

    /// <summary>Hold even a running turn at the shell's closing boundary; nothing queued starts after it.</summary>
    public bool HoldForClose()
    {
        lock (_gate)
        {
            var cut = _pumping || _holding is not null || _waiting.Count > 0;
            _held = true;
            _closeHeld = true;
            return cut;
        }
    }

    internal Func<bool>? Closing { get; set; }

    /// <summary>Take words again after <see cref="Hold"/>: the take did not end the conversation, or it has ended and nothing listens.</summary>
    public void Release()
    {
        lock (_gate) _held = false;
    }

    /// <summary>
    /// Wait for the account's limit to reset (LIMITRESUME1c, D206 §3.5): from now on a word is kept as any is and waits, shown queued,
    /// but starts no turn, since the account would refuse it. False where the conversation is finishing, held for a take, or gone.
    /// </summary>
    public bool WaitForLimit()
    {
        lock (_gate)
        {
            if (_finishing || _gone || _held) return false;
            _limited = true;
            return true;
        }
    }

    /// <summary>
    /// Reserve the turns for the person's Accept (CHATFLOW1d, the chat-flow design §5.2 point 1): the dispatcher's keep first, so a word
    /// whose keeping is in flight finishes being kept and is taken before anything is tested; then, under the turns' own lock, true where
    /// nothing is taking, opening, held or waiting, and from then on a word is kept and waits, starting no turn, until
    /// <see cref="Pressed"/>. False where a turn runs or a word waits, or the conversation is finishing, held, gone or reserved already.
    /// </summary>
    public async Task<bool> PressAsync(CancellationToken ct = default)
    {
        await _keeping.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            lock (_gate)
            {
                if (_pumping || _opening || _holding is not null || _waiting.Count > 0 || _finishing || _gone || _held || _pressing) return false;
                _pressing = true;
                return true;
            }
        }
        finally
        {
            _keeping.Release();
        }
    }

    /// <summary>Whether the turns are reserved for the person's Accept (<see cref="PressAsync"/>).</summary>
    public bool Pressing
    {
        get
        {
            lock (_gate) return _pressing;
        }
    }

    /// <summary>
    /// The press answered, landed or refused (CHATFLOW1d): the reservation let go, and a word that waited under it starts at once, as the
    /// next turn. Nothing where nothing was reserved.
    /// </summary>
    public void Pressed()
    {
        bool start;
        lock (_gate)
        {
            if (!_pressing) return;
            _pressing = false;
            start = _waiting.Count > 0 && !_pumping && !_limited && !_gone && !_finishing && !_held;
            if (start)
            {
                _pumping = true;
                _drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        if (start) _ = PumpAsync();
        lock (_gate) Publish();
    }

    /// <summary>Whether its words wait for its account's limit to reset (<see cref="WaitForLimit"/>).</summary>
    public bool Limited
    {
        get
        {
            lock (_gate) return _limited;
        }
    }

    /// <summary>
    /// The wake (LIMITRESUME1c, D206 §3.5): every word that waited, handed to <paramref name="compose"/> in the order said, becomes one
    /// turn, the next at once, and the turns take words again. Under the turns' own lock, so a word said as it wakes either waited and is
    /// among them or comes after it. False, composing nothing, where it was not waiting, or is finishing, held or gone.
    /// </summary>
    /// <param name="compose">The one turn the waiting words become; called under the lock, so it must not call back into the turns.</param>
    public bool WakeFromLimit(Func<IReadOnlyList<ChatMessage>, ChatMessage> compose)
    {
        bool start;
        lock (_gate)
        {
            if (!_limited || _finishing || _gone || _held) return false;
            List<ChatMessage> waited = [.. _waiting.Select(Unstamped)];
            _waiting.Clear();
            _waiting.Add(compose(waited));
            _limited = false;
            start = !_pumping && !_pressing;
            if (start)
            {
                _pumping = true;
                _drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        if (start) _ = PumpAsync();
        lock (_gate) Publish();
        return true;
    }

    /// <summary>
    /// Hold the turns for a move while they wait for the limit (LIMITRESUME1c, as <see cref="Hold"/> holds them for a take): the words
    /// that waited are handed out, in the order said, for the move to carry, and nothing more is taken until <see cref="Release"/>.
    /// Null where it was not waiting, a turn runs, or it is finishing, held or gone.
    /// </summary>
    public IReadOnlyList<ChatMessage>? HoldLimited()
    {
        lock (_gate)
        {
            if (!_limited || _pumping || _holding is not null || _finishing || _gone || _held) return null;
            List<ChatMessage> waited = [.. _waiting.Select(Unstamped)];
            _waiting.Clear();
            _limited = false;
            _held = true;
            Publish();
            return waited;
        }
    }

    /// <summary>A waiting word without the queue's own id, which no record knows: its record's id stays. Called under the lock.</summary>
    private ChatMessage Unstamped(ChatMessage message) =>
        message.Id is { } stamped && _stamped.Remove(stamped) ? message with { Id = null } : message;

    /// <summary>
    /// The one closing turn a hand-over's take asks of the outgoing agent while its turns are held (SESSCTX1c part B, the session-context
    /// design §2.2): taken as a turn of its own though held, every other word still taking nothing, and what completes once that turn has
    /// ended. Null where the turns are not held by <see cref="Hold"/>, or something runs or waits, or the conversation is finishing or gone.
    /// </summary>
    public Task? TakeHeld(ChatMessage message)
    {
        Task drained;
        lock (_gate)
        {
            if (!_held || _closeHeld || Closing?.Invoke() == true || _pumping || _holding is not null || _waiting.Count > 0 || _finishing || _gone) return null;
            _waiting.Add(Stamped(message));
            _pumping = true;
            _drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            drained = _drained.Task;
        }

        _ = PumpAsync();
        lock (_gate) Publish();
        return drained;
    }

    /// <summary>
    /// Take a word at the level it carries (COWORK1a): an urgent one handed to the turn on the wire where the door takes words at
    /// its next step and no urgent word waits before it (MSG1c); an ordinary one, and any other, queued as a turn of its own. Null
    /// once the conversation is finishing, held for a hand-over's take, or gone: nothing more is heard.
    /// </summary>
    public TurnReach? Take(ChatMessage message)
    {
        bool start;
        lock (_gate)
        {
            if (_finishing || _gone || _held) return null;

            // At its next step (D137 §2.1): only into a turn already on the wire, never one the person stopped, and never
            // ahead of an urgent word still waiting, which would make the agent read them out of the order said. An ordinary word
            // never enters a turn in flight, and an urgent one goes past it, since its sender chose sooner (COWORK1a, the design
            // §2.1). A helper's answer is a turn of its own, never handed to the running one (MIXAGENT2a-iii), unless its ask was
            // urgent (COWORK1d). COWORK1c: a kept word goes to the door to be attempted before the wire and taken off the record once written.
            if (steer is not null && !Answer(message) && message.Level != MessageLevel.Ordinary && _inFlight && _sent
                && !_stopped && !_stopPending && _holding is null && !UrgentWaiting() && steer(Dispatched(message)) is { } answered)
            {
                _steered.Add(answered);
                _onTheirWay.Add(answered);
                return TurnReach.NextStep;
            }

            _waiting.Add(Stamped(message));
            // LIMITRESUME1c: waiting for the limit, a word waits with the rest and starts nothing; CHATFLOW1d: so under the person's press.
            start = !_pumping && !_limited && !_pressing;
            if (start)
            {
                _pumping = true;
                _drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        // Started outside the lock: a door that takes a turn at once runs its send before this returns,
        // so the message it took is never announced as waiting.
        if (start) _ = PumpAsync();
        lock (_gate) Publish();
        return start ? TurnReach.Now : TurnReach.TurnEnd;
    }

    /// <summary>
    /// Words the agent answered <c>promptRequired</c> (STEER2d): the turn ended between the press and the steer, and the adapter
    /// left them to the client. They go first among what waits, the person's choice, and are the next turn, taken there under
    /// their own id since the record showed them said already. False once the conversation is finishing or gone: the caller says
    /// they never reached the agent. The turn lasts until the steer is answered, so its pump is still running to take them.
    /// </summary>
    public bool HandBack(ChatMessage message)
    {
        bool start;
        lock (_gate)
        {
            if (_finishing || _gone) return false;
            // Off the record once steered (COWORK1c): waiting again in this process alone.
            _waiting.Insert(0, message with { Kept = false, Attempting = null, Marking = null, Went = null });
            start = !_pumping;
            if (start)
            {
                _pumping = true;
                _drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        if (start) _ = PumpAsync();
        lock (_gate) Publish();
        return true;
    }

    /// <summary>
    /// The person's words at <c>now</c> (STEER2a, COWORK1a), carried out by the door it has: with no turn running the words are the
    /// next turn at once; into a turn on the wire whose agent steers, they are steered at once (<c>now</c>) and the turn lasts until
    /// the steer is answered; on a door that takes words only at its next step they go there, said so; on a door that takes none
    /// during a turn they wait as <see cref="Take"/> queues them, said <c>no-interrupt</c>, and are the next turn when it ends (D90).
    /// Null once the conversation is finishing, held or gone.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Nothing is stopped, on any door</b> (D188 points 1 and 3): the stop that keeps the waiting words is the person's
    /// own press (<see cref="StopKeepingAsync"/>, <c>CANCEL_TURN</c> with <c>keep</c>), never a word's. On a next-step door a stop
    /// would besides answer a word on its way cancelled while the agent may still act on it (D136 point 4, evidence §3). A steer is
    /// no stop: it goes past words on their way and waiting, the person's choice to go first, and they keep their places. Where the
    /// agent cannot steer, the words go behind what is ahead, said <see cref="SendNowWhy.Behind"/>.</para>
    /// </remarks>
    public Task<SentNow?> TakeNowAsync(ChatMessage message)
    {
        message = message with { Level = MessageLevel.Now };
        lock (_gate)
        {
            if (_finishing || _gone || _held) return Task.FromResult<SentNow?>(null);
            if (_inFlight && message.Helped is null)
            {
                // Words before these: handed to this turn at its next step and not yet answered, or urgent words waiting as turns
                // of their own. Ordinary words and a helper's answer, which is ordinary, wait for the turn's end, and these go
                // past them (COWORK1a). A helper's answer is never the person's press (MIXAGENT2a-iii).
                var onTheirWay = OnTheirWay();
                var behind = onTheirWay || _holding is not null || UrgentWaiting();
                var running = _sent && !_stopped && !_stopPending;
                // 🔴 Steered past whatever is ahead (the owner's case): the steer cuts the step in flight, not the prompt a word on
                // its way is queued as, which is still taken at its next step; what waits keeps its place. The person chose now.
                if (running && steerNow?.Invoke(Dispatched(message)) is { } steered)
                {
                    _steered.Add(steered);
                    return Task.FromResult<SentNow?>(new SentNow(SendNowWhy.Spelled(TurnReach.Steered), Now: true, Why: null));
                }

                // At its next step, behind any word on its way, as a word said then goes; never ahead of an urgent one still waiting.
                if (running && _holding is null && !UrgentWaiting() && steer?.Invoke(Dispatched(message)) is { } stepped)
                {
                    _steered.Add(stepped);
                    _onTheirWay.Add(stepped);
                    return Task.FromResult<SentNow?>(new SentNow(
                        SendNowWhy.Spelled(TurnReach.NextStep), Now: false, onTheirWay ? SendNowWhy.Behind : SendNowWhy.NoInterrupt));
                }

                _waiting.Add(Stamped(message));
                // Held for the turn's end: behind what is ahead on a door that takes words at its next step, and on any other, or
                // behind the person's own stop, because nothing interrupts a turn for them.
                var why = !_stopped && !_stopPending && (onTheirWay || nextStep?.Invoke() == true)
                    ? behind ? SendNowWhy.Behind : SendNowWhy.Opening
                    : SendNowWhy.NoInterrupt;
                Publish();
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

    /// <summary>
    /// Stop the turn in flight and keep what waits (STEER2a, <c>CANCEL_TURN</c> with <c>keep</c>): nothing is withdrawn, and the
    /// words waiting are the next turn once it ends (D90), where <see cref="StopAsync"/> hands them back (CONV4a). A stop already
    /// asked is not asked again. <see cref="TurnStop.Nothing"/> when no turn is in flight.
    /// </summary>
    public async Task<TurnStop> StopKeepingAsync()
    {
        Stop? stop;
        lock (_gate)
        {
            if (!_inFlight) return TurnStop.Nothing;
            stop = StopKeeping();
            Publish();
        }

        if (stop is not null) await SendStopAsync(stop).ConfigureAwait(false);
        return new TurnStop(Cancelled: true, Withdrawn: []);
    }

    /// <summary>
    /// Mark the turn in flight stopped, keeping what waits: the stop to send now, or null. Taken but not yet on the wire, the
    /// interrupt goes the moment it is (see <c>take</c>); a stop already asked is not asked again. Called under the lock.
    /// </summary>
    private Stop? StopKeeping()
    {
        var asked = _stopped || _stopPending;
        _stopped = true;
        if (!_sent) _stopPending = true;
        return _sent && !asked ? Decide() : null;
    }

    /// <summary>A stop decided for one turn (STEER2a), and what tells the next turn it is on the wire or dropped.</summary>
    private sealed record Stop(int Turn, TaskCompletionSource Sent);

    /// <summary>Decide a stop for the turn in flight, which the next turn waits for (<see cref="PumpAsync"/>). Called under the lock.</summary>
    private Stop Decide()
    {
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _stopping = _stopping.IsCompleted ? sent.Task : Task.WhenAll(_stopping, sent.Task);
        return new Stop(_turn, sent);
    }

    /// <summary>
    /// Send a stop to the turn it was decided for, or drop it where that turn has ended (STEER2a); either way the next turn may
    /// then go, never before. Called outside the lock: the door's interrupt is its own.
    /// </summary>
    private async Task SendStopAsync(Stop stop)
    {
        try
        {
            bool current;
            lock (_gate) current = _inFlight && _turn == stop.Turn;
            if (current) await interrupt().ConfigureAwait(false);
        }
        finally
        {
            stop.Sent.TrySetResult();
        }
    }

    /// <summary>Whether a word handed to the turn at its next step is still unanswered: on its way. Called under the lock.</summary>
    private bool OnTheirWay()
    {
        _onTheirWay.RemoveAll(word => word.IsCompleted);
        return _onTheirWay.Count > 0;
    }

    /// <summary>The queue's id for a word that has none (QUEUEEDIT1a): the page's handle on it while it waits. Called under the lock.</summary>
    private ChatMessage Stamped(ChatMessage message)
    {
        if (message.Id is not null || message.OnRecord) return message;
        var id = $"said-{++_said}";
        _stamped.Add(id);
        return message with { Id = id };
    }

    /// <summary>
    /// Withdraw what is waiting, then stop the turn in flight. <see cref="TurnStop.Nothing"/> when there
    /// was neither.
    /// </summary>
    public async Task<TurnStop> StopAsync()
    {
        List<ChatMessage> withdrawn = [];
        bool cancelled;
        Stop? stop;
        lock (_gate)
        {
            if (_holding is not null)
            {
                withdrawn.Add(_holding);
                _holding = null;
            }

            withdrawn.AddRange(_waiting);
            _waiting.Clear();
            // COWORK1c: the person's words handed back to them, so off the record too; a helper's answer stays there (below).
            TakeOff([.. withdrawn.Where(message => !message.OnRecord)]);
            cancelled = _inFlight;
            if (_inFlight) _stopped = true;
            // Taken but not yet on the wire: the interrupt goes the moment it is (see `take`). A word said after the stop waits
            // for it, so it too is sent for this turn alone (STEER2a).
            stop = _inFlight && _sent ? Decide() : null;
            if (_inFlight && !_sent) _stopPending = true;
            Publish();
        }

        if (stop is not null) await SendStopAsync(stop).ConfigureAwait(false);
        // The words a conversation goes on with are not handed back (MSG1c): the page never listed them, and they stay on
        // the record, which takes them off only once they went. Nor is a helper's answer (MIXAGENT2a-iii): it is no word of the
        // person's to put back in the composer, and it stays on the record for a later turn. Nor is Daoris's own word (ASKQUESTS1):
        // the stop takes it with the turn.
        withdrawn.RemoveAll(message => !message.Theirs);
        return !cancelled && withdrawn.Count == 0 ? TurnStop.Nothing : new TurnStop(cancelled, withdrawn);
    }

    /// <summary>
    /// Take no more, and complete once every turn already asked for has ended — what finishing waits on
    /// before it ends the input, on either door — and every kept word that left is off the record (COWORK1c).
    /// </summary>
    public Task FinishAsync()
    {
        Task drained;
        lock (_gate)
        {
            _finishing = true;
            drained = _drained.Task;
        }

        return TakenOffAfterAsync(drained);

        async Task TakenOffAfterAsync(Task turns)
        {
            await turns.ConfigureAwait(false);
            await TakingOff.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The harness has gone: nothing waiting will be sent, and nothing more is taken. Words kept on the record stay there for the
    /// conversation to go on with (COWORK1c: nothing undelivered vanishes), unless <paramref name="personStopped"/>: the person's stop
    /// ends them with the conversation, as it always did, rather than start it again.
    /// </summary>
    public void Gone(bool personStopped = false)
    {
        lock (_gate)
        {
            _gone = true;
            if (personStopped) TakeOff([.. (_holding is null ? [] : new[] { _holding }).Concat(_waiting).Where(message => !message.OnRecord)]);
            _holding = null;
            _waiting.Clear();
            Publish();
        }
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            ChatMessage message;
            lock (_gate)
            {
                // LIMITRESUME1c: a conversation waiting for its account's limit starts no turn; the wake starts the next.
                if (_waiting.Count == 0 || _gone || _limited || _closeHeld || Closing?.Invoke() == true)
                {
                    _pumping = false;
                    _drained.TrySetResult();
                    Publish();
                    return;
                }

                message = _waiting[0];
                _waiting.RemoveAt(0);
                _holding = message;
            }

            // 🔴 A stop decided for the turn before is on the wire, or dropped, before this one goes (STEER2a): sent after it, the
            // stop would cut this turn, whose words have left the queue and would be lost. No stop is decided meanwhile, since
            // none is decided with no turn in flight.
            Task stopping;
            lock (_gate) stopping = _stopping;
            await stopping.ConfigureAwait(false);

            // 🔴 COWORK1e (D188 point 4; the message-urgency design §2.3): the conversation's one dispatcher judges a helper's answer as its
            // turn comes, once every turn before it was recorded, so two answers queued at once under separate asks never both pass as the
            // second. One the bound holds goes no further: it stays on its record, marked for the person, and the queue goes on.
            if (await HeldAloneAsync(message).ConfigureAwait(false))
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_holding, message)) _holding = null;
                    Publish();
                }

                continue;
            }

            var readying = ready();
            if (!readying.IsCompleted)
            {
                // The door is still opening: the message is waiting on it, and says so.
                lock (_gate)
                {
                    _opening = true;
                    Publish();
                }
            }

            bool open;
            try
            {
                open = await readying.ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or IOException or InvalidOperationException
                                              or OperationCanceledException)
            {
                open = false;
            }

            lock (_gate)
            {
                _opening = false;
                if (_closeHeld || Closing?.Invoke() == true)
                {
                    _pumping = false;
                    _drained.TrySetResult();
                    Publish();
                    return;
                }
                if (!open)
                {
                    _gone = true;
                    _holding = null;
                    _waiting.Clear();
                    continue;
                }

                // Withdrawn by a stop while the door opened: never sent.
                if (_holding is null) continue;
                // As it stands now: reworded while the door opened (QUEUEEDIT1a), and without the queue's id.
                message = _holding;
                if (message.Id is { } stamped && _stamped.Remove(stamped)) message = message with { Id = null };
                _holding = null;
                _inFlight = true;
                _turn++;
                _sent = false;
                _stopPending = false;
                _stopped = false;
                Publish();
            }

            try
            {
                // COWORK1c: a kept word's attempt is marked on its record before its turn is on the wire.
                if (Dispatched(message).Attempting is { } attempting) await attempting().ConfigureAwait(false);
                await take(message, () =>
                {
                    Sent();
                    // A helper's answer comes off the record once its turn is on the wire (MIXAGENT2a-iii), and a kept word by its id
                    // (COWORK1c), after every take-off before it.
                    message.Delivered?.Invoke();
                    lock (_gate) TakeOff([message]);
                }).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The door says what failed in its own words; the queue outlives one bad turn, or every
                // later message would wait on a pump that is gone.
            }

            // 🔴 The turn lasts until every word handed to it at its next step is answered (MSG1c, D137 §2.1): the agent
            // answers the prompt before them when it takes them, and works on. Found empty under the lock that ends the
            // turn, so no word is handed to a turn that has already ended.
            while (true)
            {
                Task[] steered;
                lock (_gate)
                {
                    steered = [.. _steered];
                    _steered.Clear();
                    if (steered.Length == 0)
                    {
                        // The end of a turn that reached the harness is the chat's last move (RAIL2), told
                        // at once: the next message may start straight away and leave nothing else changed
                        // for the page to hear. One that never went out moved nothing.
                        if (_sent)
                        {
                            _lastEnded = DateTimeOffset.UtcNow;
                            Publish();
                        }

                        _inFlight = false;
                        _sent = false;
                        _stopPending = false;
                        break;
                    }
                }

                try
                {
                    await Task.WhenAll(steered).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Each word's door says what became of it; the turn ends all the same.
                }
            }
        }
    }

    /// <summary>The turn is on the wire — and a stop that arrived while it was on its way goes now.</summary>
    private void Sent()
    {
        Stop? late;
        lock (_gate)
        {
            _sent = true;
            late = _stopPending ? Decide() : null;
            _stopPending = false;
        }

        if (late is not null) _ = SendStopAsync(late);
    }

    private ChatQueue Now() => new(_pumping, Snapshot(), _lastEnded, _opening);

    // The words a conversation goes on with are left out (MSG1c): its record shows them waiting already, at its foot. So is Daoris's
    // own word (ASKQUESTS1): the queue is the person's, which the composer draws with its edits.
    private List<ChatMessage> Snapshot()
    {
        List<ChatMessage> now = _holding is { Theirs: true } ? [_holding] : [];
        now.AddRange(_waiting.Where(message => message.Theirs));
        return now;
    }

    /// <summary>Tell where the turns stand when it differs from what was last told. Called under the gate, so told in order.</summary>
    private void Publish()
    {
        var now = Now();
        if (now.Taking == _published.Taking && now.Queued.SequenceEqual(_published.Queued)
            && now.LastTurnEnded == _published.LastTurnEnded && now.Opening == _published.Opening) return;
        _published = now;
        try
        {
            changed(now);
        }
        catch (Exception)
        {
            // A listener's failure is its own; the queue has moved either way.
        }
    }

    /// <summary>A word as these turns hand it to the door at its next step (COWORK1c): a kept one to be attempted before the wire and taken off once written.</summary>
    private ChatMessage Dispatched(ChatMessage message) =>
        SaidDoor.Dispatched(message, _door, word =>
        {
            lock (_gate) TakeOff([word]);
        });

    /// <summary>
    /// Kept words on their way to the wire, handed back to the person or taken back (COWORK1c): off the record by their ids, after
    /// every take-off before them, awaited in order and never fired and forgotten (the design §6). One that fails is tried again with
    /// the next. Called under the lock.
    /// </summary>
    private void TakeOff(IReadOnlyList<ChatMessage> words)
    {
        if (_door is not { } door) return;
        List<string> ids = [.. words.Where(word => word.Kept && word.Id is not null).Select(word => word.Id!)];
        if (ids.Count == 0) return;
        _takingOff = TakeOffAfterAsync(_takingOff, door, ids);
    }

    /// <summary>One take-off, after the one before it; never faults, and what it could not take waits for the next.</summary>
    private async Task TakeOffAfterAsync(Task before, ISaidDoor door, IReadOnlyList<string> ids)
    {
        await before.ConfigureAwait(false);
        List<string> taking;
        lock (_gate)
        {
            taking = [.. _untaken, .. ids];
            _untaken.Clear();
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
            lock (_gate) _untaken.InsertRange(0, taking);
        }
    }

    private static TaskCompletionSource Completed()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        done.SetResult();
        return done;
    }
}
