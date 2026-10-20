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
/// </remarks>
/// <param name="changed">Where it stands, each time that changes — the page shows what is waiting.</param>
public sealed class DrivenInbox(Action<ChatQueue> changed)
{
    private readonly object _gate = new();
    private readonly List<ChatMessage> _held = [];
    private readonly Queue<Task<string>> _sent = new();
    // The steers *Send now* sent (STEER2a), each until the agent answered it: where it answered `injected` the words were taken.
    private readonly Queue<Task<string>> _steered = new();
    // Each turn the run takes a held word for, counted (STEER2a): a turn is asked to stop once, and the run takes no held word
    // while a stop decided for the turn before is still on its way, which landing late would cut that word's own turn.
    private int _turn;
    private int _stoppedTurn = -1;
    private Task _stopping = Task.CompletedTask;
    private bool _closed;
    private bool _flowing;
    private int _said;
    private DrivenReach? _reach;
    private Func<Task>? _interrupt;
    private Func<ChatMessage, Task<string>>? _deliver;
    private Func<ChatMessage, Task<string>>? _steer;
    private Action<ChatMessage, DrivenReach>? _told;
    private Action? _closing;
    private Action<ChatMessage>? _withdrawn;

    // COWORK1c (the message-urgency design §6): the session's one dispatcher. Words are kept on the record one at a time, so the order
    // they are held in is the record's; each keep on its way holds the inbox open, and each kept word leaves the record by its id, in
    // order, once on its way to the wire, before the inbox closes.
    private readonly SemaphoreSlim _keeping = new(1, 1);
    private int _keepsOnTheirWay;
    private TaskCompletionSource _keepsLanded = Landed();
    private ISaidDoor? _door;
    private Task _takingOff = Task.CompletedTask;
    // Ids a take-off could not take, tried again with the next one, and once more before the inbox closes.
    private readonly List<string> _untaken = [];
    private bool _triedAgain;
    // The record's ids this inbox held, so a producer's retry under a held key holds nothing twice.
    private readonly HashSet<string> _known = new(StringComparer.Ordinal);
    private Func<bool>? _personStopped;
    private Action<IReadOnlyList<ChatMessage>>? _left;

    /// <summary>Where it stands: a turn is running while it is open, and what is held waits for its end.</summary>
    public ChatQueue State
    {
        get
        {
            lock (_gate) return Now();
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
            lock (_gate) return !_closed && _held.Count > 0;
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
            lock (_gate) return _closed ? null : _reach;
        }
    }

    /// <summary>
    /// When a word said now at <paramref name="level"/> reaches the session (COWORK1a, the design §3.0): an ordinary one at the
    /// turn's end on every door, any other at <see cref="Reach"/>. Null as <see cref="Reach"/> is.
    /// </summary>
    public DrivenReach? ReachAt(MessageLevel level)
    {
        lock (_gate) return _closed || _reach is null ? null : ReachOf(level);
    }

    /// <summary>
    /// Whether the agent steers (STEER2a): a word at <c>now</c> cuts its step in flight, where anywhere else it is told why not
    /// (COWORK1a, the design §8.1). False until the door is known, and once it has closed.
    /// </summary>
    public bool Steers
    {
        get
        {
            lock (_gate) return !_closed && _steer is not null;
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
            lock (_gate) return !_closed && _interrupt is not null;
        }
    }

    /// <summary>
    /// The door, once the session is open: its way of stopping the running turn, and — where the agent takes a prompt
    /// during its turn — its way of sending one (STEER1). What is held then is told with when it reaches the session; a
    /// word goes only once the door says the turn has begun (<see cref="Flow"/>), so none overtakes the prompt it follows.
    /// </summary>
    /// <param name="interrupt">
    /// Stops the running turn and keeps the session; null where nothing can stop a turn and keep the session (the native
    /// door's run, MSG1b), and *Send now* then stops nothing.
    /// </param>
    /// <param name="deliver">
    /// Sends one word as a prompt now and completes with its answer; null where the agent takes no prompt during a turn,
    /// and every word waits for the turn's end.
    /// </param>
    /// <param name="steer">
    /// The person's *Send now* on that door (STEER2a): steers one word into the running turn and completes with the agent's
    /// outcome, the words taken where it is <c>injected</c>. Null where the agent never said it steers.
    /// </param>
    public void Attach(
        Func<Task>? interrupt, Func<ChatMessage, Task<string>>? deliver = null, Func<ChatMessage, Task<string>>? steer = null)
    {
        lock (_gate)
        {
            _interrupt = interrupt;
            _deliver = deliver;
            _steer = deliver is null ? null : steer;
            _reach = deliver is null ? DrivenReach.TurnEnd : DrivenReach.NextStep;
            if (_closed) return;

            foreach (var message in _held) Tell(message, ReachOf(message.Level));
            Publish();
        }
    }

    /// <summary>
    /// The session's first prompt is on the wire (STEER1): where words go during a turn, the urgent words held go now, and every
    /// urgent word after it the moment it is said. Nothing on a door that holds words for the turn's end.
    /// </summary>
    /// <remarks>
    /// 🔴 An ordinary word stays held for the turn's end (COWORK1a, the design §14's trap): it never enters a turn in flight, and
    /// sending every held word here sent it with the urgent ones.
    /// </remarks>
    public void Flow()
    {
        lock (_gate)
        {
            _flowing = true;
            if (_closed || _reach != DrivenReach.NextStep) return;

            foreach (var message in _held.Where(GoesAtNextStep).ToList()) Send(message);
            _held.RemoveAll(GoesAtNextStep);
            Publish();
        }
    }

    /// <summary>
    /// Told each word with when it reaches the session, the moment it is said (STEER1) — or, said before the door is
    /// known, when it is. Called under the inbox's lock, so told in order; a listener's failure is its own.
    /// </summary>
    public void OnSaid(Action<ChatMessage, DrivenReach> said)
    {
        lock (_gate) _told = said;
    }

    /// <summary>
    /// Told each held word the person took back or reworded (QUEUEEDIT1a), after it was told as said: the record settles it
    /// as withdrawn. Called under the inbox's lock, in order; a listener's failure is its own.
    /// </summary>
    public void OnWithdrawn(Action<ChatMessage> withdrawn)
    {
        lock (_gate) _withdrawn = withdrawn;
    }

    /// <summary>
    /// Take back, reword, or change the level of one held word (QUEUEEDIT1a, COWORK1a): with neither <paramref name="text"/> nor
    /// <paramref name="level"/> it is removed; with words, they replace its words where it is held, its place and its files kept,
    /// under a new id the record is told as said; with a level, it asks to reach the session then, under the same id, told again
    /// with where it reaches now. Raised where the door takes words at its next step, it goes there at once, and at <c>now</c> where
    /// the agent steers, steered past what waits (the design §8.2). Only a word still held (<see cref="QueueEdit.Taken"/>
    /// otherwise): one sent to the turn on the wire, or taken for the next, is the session's. The record, which showed it waiting
    /// once the door was known, settles a reworded or removed word's old id as withdrawn.
    /// </summary>
    public QueueEdit Edit(string id, string? text, MessageLevel? level = null)
    {
        lock (_gate)
        {
            var at = HeldAt(id);
            if (at < 0) return QueueEdit.Taken;
            return Edited(at, text, level, text is null ? null : $"said-{++_said}", kept: false);
        }
    }

    /// <summary>
    /// <see cref="Edit"/>, and a word kept on the record reworded as a word said is kept (COWORK1c): the new words on the record first,
    /// under their own id, then in the old one's place, which leaves the record. One taken while the new words were kept is the
    /// session's, and the new words leave the record again. Where the record will not keep them, they are held in this process alone.
    /// </summary>
    public async Task<QueueEdit> EditAsync(string id, string? text, MessageLevel? level = null, CancellationToken ct = default)
    {
        ISaidDoor? door;
        lock (_gate)
        {
            var at = HeldAt(id);
            door = _door;
            if (at < 0 || text is null || !_held[at].Kept || door is null) return Edit(id, text, level);
        }

        await _keeping.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ChatMessage old;
            lock (_gate)
            {
                var at = HeldAt(id);
                if (at < 0) return QueueEdit.Taken;
                old = _held[at];
            }

            SayAnswer said;
            try
            {
                said = await door.KeepAsync(old with { Text = text, Level = level ?? old.Level, Key = null, Id = null, Kept = false }, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                said = new SayAnswer(false, error.Message);
            }

            lock (_gate)
            {
                var at = HeldAt(id);
                var kept = said is { Kept: true, Word.Id: { } newId } ? newId : null;
                if (at < 0)
                {
                    if (kept is not null) TakeOff([new ChatMessage(text, []) { Id = kept, Kept = true }]);
                    return QueueEdit.Taken;
                }

                if (kept is not null) _known.Add(kept);
                return Edited(at, text, level, kept ?? $"said-{++_said}", kept: kept is not null);
            }
        }
        finally
        {
            _keeping.Release();
        }
    }

    /// <summary>Where a word of the person's is held, by its id; -1 once closed. Daoris's own words are no word of theirs to edit (COWORK1b).</summary>
    private int HeldAt(string id) => _closed ? -1 : _held.FindIndex(message => message.Id == id && message.Daoris is null);

    /// <summary>
    /// The held word at <paramref name="at"/> taken back, reworded under <paramref name="newId"/>, or at a new level. A kept word
    /// taken back or reworded leaves the record (COWORK1c), so a restart never brings back what the person changed. Called under the gate.
    /// </summary>
    private QueueEdit Edited(int at, string? text, MessageLevel? level, string? newId, bool kept)
    {
        var old = _held[at];
        // A word said before the door was known was never told: the record has nothing of it to settle.
        var told = _reach is not null;
        ChatMessage? word = text is null && level is null
            ? null
            : old with
            {
                Text = text ?? old.Text, Id = text is null ? old.Id : newId, Level = level ?? old.Level, Kept = text is null ? old.Kept : kept,
            };
        _held.RemoveAt(at);
        if (word is null || text is not null) TakeOff([old]);

        if (told && (word is null || text is not null))
        {
            try
            {
                _withdrawn?.Invoke(old);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // The record misses it; the queue has moved either way.
            }
        }

        if (word is not null)
        {
            if (word.Level == MessageLevel.Now && _reach == DrivenReach.NextStep && _flowing && _steer is { } steer)
            {
                Tell(word, DrivenReach.Steered);
                Steer(steer, word);
            }
            else if (GoesAtNextStep(word))
            {
                Tell(word, DrivenReach.NextStep);
                Send(word);
            }
            else
            {
                _held.Insert(at, word);
                if (told) Tell(word, ReachOf(word.Level));
            }
        }

        Publish();
        return QueueEdit.Done;
    }

    /// <summary>Called once, when it closes — how the registry forgets it.</summary>
    /// <param name="personStopped">
    /// Whether the person stopped the session (COWORK1c), asked as it closes: then the words kept for its run leave the record with
    /// it, as they always left the session; closed by anything else, they stay on the record for the session's go-on.
    /// </param>
    /// <param name="left">Told the words kept for the run that stay on the record as it closes under them (COWORK1c).</param>
    internal void OnClosing(Action closing, Func<bool>? personStopped = null, Action<IReadOnlyList<ChatMessage>>? left = null)
    {
        lock (_gate)
        {
            _closing = closing;
            _personStopped = personStopped;
            _left = left;
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
            lock (_gate) return _takingOff;
        }
    }

    /// <summary>
    /// Keep a word for the run on the session's record first, then take it as <see cref="Hold"/> or, at <c>now</c>,
    /// <see cref="HoldNowAsync"/> takes it, under the record's id (COWORK1c, D188 point 6; the message-urgency design §6). One keep
    /// at a time: two said at once are held in the order the record kept them, whichever write answered first (trap 20). A keep on
    /// its way holds the inbox open, so a word said as the turn ends is taken by this run. A word the record answers by a key it
    /// already holds is held once.
    /// </summary>
    /// <remarks>
    /// <para><b>Kept before told.</b> The record keeps the word, then the inbox tells it with where it reaches the session; a word the
    /// record refused is never held, and its caller says so.</para>
    ///
    /// <para><b>Closed</b> (<see cref="InboxKeep.Closed"/>), before the keep or while it was on its way: the run takes no more words,
    /// so nothing stays on the record, which is ending, and the caller holds the word as words said while a session winds up are held
    /// (the design §6, HANDWORDS1).</para>
    /// </remarks>
    /// <exception cref="HttpRequestException">The record did not answer: nothing holds the word.</exception>
    public async Task<InboxKept> KeepAsync(ISaidDoor door, ChatMessage message, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_closed) return InboxKept.Closed;
            if (_keepsOnTheirWay++ == 0) _keepsLanded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        try
        {
            await _keeping.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                lock (_gate)
                {
                    if (_closed) return InboxKept.Closed;
                }

                var said = await door.KeepAsync(message, ct).ConfigureAwait(false);
                if (!said.Kept || said.Word?.Id is not { } id) return new InboxKept(InboxKeep.Refused, said);

                lock (_gate)
                {
                    _door ??= door;
                    var kept = message with { Id = id, Kept = true };
                    if (_closed)
                    {
                        // 🔴 Never left on a record that is ending (the design §6): off it again, and its caller holds it as words said
                        // while a session winds up are held, until the record ends.
                        TakeOff([kept]);
                        return InboxKept.Closed;
                    }

                    var reach = _reach is null ? (DrivenReach?)null : ReachOf(kept.Level);
                    // A producer's retry under the key of a word held already (trap 19): held once.
                    if (!_known.Add(id)) return new InboxKept(InboxKeep.Held, said) { Reach = reach };
                    if (kept.Level == MessageLevel.Now) return new InboxKept(InboxKeep.Held, said) { Reach = _reach, Now = NowHeld(kept) };

                    HoldHere(kept);
                    return new InboxKept(InboxKeep.Held, said) { Reach = reach };
                }
            }
            finally
            {
                _keeping.Release();
            }
        }
        finally
        {
            TaskCompletionSource? landed = null;
            lock (_gate)
            {
                if (--_keepsOnTheirWay == 0) landed = _keepsLanded;
            }

            landed?.TrySetResult();
        }
    }

    /// <summary>
    /// <see cref="KeepAsync"/> where the record can keep the word, and held in this process alone, as before, where it cannot: no
    /// service yet (<paramref name="door"/> null), one from before COWORK1c that refuses a running record, a record that refuses it
    /// for any reason, or one that did not answer. Words said to a session this process runs are never lost to a record that will not
    /// keep them; a restart loses only those. Never <see cref="InboxKeep.Refused"/>.
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

    /// <summary>
    /// Take the person's words at the level they carry (COWORK1a): told at once with where they reach the session, then sent where
    /// the agent takes them during its turn and they are urgent, or held for the turn's end. False once the session has stopped
    /// taking any: it is ending. Held in this process alone: what the record keeps first goes through <see cref="KeepAsync"/>.
    /// </summary>
    public bool Hold(ChatMessage message)
    {
        lock (_gate)
        {
            if (_closed) return false;
            HoldHere(message with { Id = $"said-{++_said}" });
            return true;
        }
    }

    /// <summary>Told with where it reaches, then sent at the next step or held for the turn's end, under its own id. Called under the gate.</summary>
    private void HoldHere(ChatMessage said)
    {
        if (_reach is not null) Tell(said, ReachOf(said.Level));
        if (GoesAtNextStep(said)) Send(said);
        else _held.Add(said);
        Publish();
    }

    /// <summary>
    /// The person's words at <c>now</c> (STEER2a, COWORK1a), carried out by the door it has. Where the agent steers and its first
    /// prompt is on the wire, the words are steered into the running turn at once, told with the reach <c>now</c>. Anywhere else
    /// they go as <see cref="Hold"/> takes an urgent word, and the answer says why not now by its code: <c>opening</c> before the
    /// first prompt left, <c>behind</c> or <c>no-interrupt</c> at a next step, and <c>no-interrupt</c> held for the turn's end.
    /// Null once the session has stopped taking any.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>It never stops the turn</b> (D188 points 1 and 3): on a door that holds words for the turn's end the words are
    /// held and the turn runs on. The keeping stop is the person's own press, <see cref="SendNowAsync"/>, offered beside the held
    /// words where the door has an interrupt that keeps the session (<see cref="KeepingStop"/>).</para>
    ///
    /// <para>🔴 <b>A steer goes past a word on its way</b> (D136 point 4): the steer cuts the step in flight, not the prompt the
    /// word is queued as, which is still taken at its next step, and the person chose to go first. Where the agent cannot steer,
    /// these go at its next step behind it, said <see cref="SendNowWhy.Behind"/>. The steer is answered by the run
    /// (<see cref="NextOrClose"/>), which closes nothing while one is on its way.</para>
    /// </remarks>
    public Task<SentNow?> HoldNowAsync(ChatMessage message)
    {
        lock (_gate)
        {
            if (_closed) return Task.FromResult<SentNow?>(null);
            return Task.FromResult<SentNow?>(NowHeld(message with { Level = MessageLevel.Now, Id = $"said-{++_said}" }));
        }
    }

    /// <summary>A word at <c>now</c> under its own id, steered where the agent steers, held as an urgent word anywhere else. Called under the gate.</summary>
    private SentNow NowHeld(ChatMessage said)
    {
        var onTheirWay = _sent.Any(answer => !answer.IsCompleted);
        if (_reach == DrivenReach.NextStep && _flowing && _steer is { } steer)
        {
            Tell(said, DrivenReach.Steered);
            Steer(steer, said);
            Publish();
            return new SentNow(SendNowWhy.Spelled(DrivenReach.Steered), Now: true, Why: null);
        }

        var reach = _reach;
        HoldHere(said);
        var why = reach is null || reach == DrivenReach.NextStep && !_flowing ? SendNowWhy.Opening
            : reach == DrivenReach.NextStep && onTheirWay ? SendNowWhy.Behind
            : SendNowWhy.NoInterrupt;
        return new SentNow(SendNowWhy.Spelled(reach), Now: false, why);
    }

    /// <summary>
    /// Words the agent answered <c>promptRequired</c> (STEER2d): the turn ended between the press and the steer, and the adapter
    /// left them to the client. They go to the front of what is held, since the person chose to go first, and the run prompts
    /// them next. Not told again, since they were said once; the run's prompt takes them. Called while the run waits on the
    /// steer, so nothing has closed; false once it has, and the caller says they never reached the agent.
    /// </summary>
    public bool HandBack(ChatMessage message)
    {
        lock (_gate)
        {
            if (_closed) return false;
            // Off the record once steered (COWORK1c): held again in this process alone.
            _held.Insert(0, message with { Kept = false });
            Publish();
            return true;
        }
    }

    /// <summary>
    /// Take another agent's words only where they reach the running turn (SESSONE2b, D155): on the door that takes words during
    /// a turn (STEER1), as <see cref="Hold"/> takes them. False on a door that holds words for the turn's end, where they would be
    /// prompted after it and extend the run, before the door is known, and once the session has stopped taking any.
    /// </summary>
    public bool HoldDuringTurn(ChatMessage message)
    {
        lock (_gate)
        {
            return !_closed && _reach == DrivenReach.NextStep && Hold(message);
        }
    }

    /// <summary>
    /// What the run does next (STEER1): wait for a word already sent, oldest first; else prompt a word held; else, with
    /// nothing held and nothing on its way, null, and the inbox closed in the same step.
    /// </summary>
    public DrivenNext? NextOrClose()
    {
        Action? closing = null;
        DrivenNext? next = null;
        // One step under the gate: a word sent between "nothing on its way" and "closed" would be answered to nobody.
        lock (_gate)
        {
            if (_closed) return null;
            if (_steered.TryDequeue(out var steered))
            {
                next = new DrivenNext(null, null) { Waits = steered };
            }
            else if (_sent.TryDequeue(out var answer))
            {
                next = new DrivenNext(null, answer);
            }
            else if (_held.Count > 0 && !_stopping.IsCompleted)
            {
                // 🔴 The stop decided for the turn that ended is on its way: the held word's own turn goes after it (STEER2a).
                next = new DrivenNext(null, null) { Waits = _stopping };
            }
            else if (_held.Count > 0)
            {
                next = new DrivenNext(_held[0], null);
                _held.RemoveAt(0);
                _turn++;
                // COWORK1c: on its way to the wire, so off the record by its id.
                TakeOff([next.Held!]);
            }
            else if (_keepsOnTheirWay > 0)
            {
                // 🔴 COWORK1c: a word on its way to the record is this run's, so the inbox stays open for it rather than leave it on a
                // record that concludes under it.
                next = new DrivenNext(null, null) { Waits = _keepsLanded.Task };
            }
            else if (!_takingOff.IsCompleted || (_untaken.Count > 0 && !_triedAgain))
            {
                // 🔴 COWORK1c: every word the session took is off the record before it can end, or a go-on would say it again.
                if (_takingOff.IsCompleted) TryAgain();
                next = new DrivenNext(null, null) { Waits = _takingOff };
            }
            else
            {
                _closed = true;
                closing = _closing;
            }

            Publish();
        }

        closing?.Invoke();
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
        lock (_gate)
        {
            if (_closed) return null;
            if (_held.Count > 0)
            {
                next = _held[0];
                _held.RemoveAt(0);
                _turn++;
                TakeOff([next]);
            }
            else if (_sent.Count > 0 || _steered.Count > 0)
            {
                return null;
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
    /// Every word held for the turn's end, in the order said — or, with none held and none on its way, none, and the inbox
    /// closed in the same step (MSG1b): the native door goes on with all of them in one resumed run, its words joined.
    /// </summary>
    public IReadOnlyList<ChatMessage> TakeAllOrClose()
    {
        Action? closing = null;
        List<ChatMessage> taken;
        lock (_gate)
        {
            if (_closed) return [];
            taken = [.. _held];
            _held.Clear();
            if (taken.Count > 0) _turn++;
            // COWORK1c: the resumed run carries them, so off the record by their ids.
            TakeOff(taken);
            if (taken.Count == 0 && _sent.Count == 0 && _steered.Count == 0)
            {
                _closed = true;
                closing = _closing;
            }

            Publish();
        }

        closing?.Invoke();
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
        Action<IReadOnlyList<ChatMessage>>? told = null;
        lock (_gate)
        {
            if (_closed) return [];
            left = [.. _held];
            _held.Clear();
            _sent.Clear();
            _steered.Clear();
            _closed = true;
            closing = _closing;
            if (left.Any(word => word.Kept))
            {
                if (PersonStopped())
                {
                    TakeOff(left);
                }
                else
                {
                    stays = [.. left.Where(word => word.Kept)];
                    left = [.. left.Where(word => !word.Kept)];
                    told = _left;
                }
            }

            Publish();
        }

        closing?.Invoke();
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

    /// <summary>Whether the person stopped the session, as the registry knows it. Called under the gate; a failed answer is no stop.</summary>
    private bool PersonStopped()
    {
        try
        {
            return _personStopped?.Invoke() == true;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Kept words on their way to the wire, or taken back (COWORK1c): off the record by their ids, after every take-off before them,
    /// awaited in order and never fired and forgotten (the design §6). One that fails is tried again with the next, and once more
    /// before the inbox closes. Called under the gate.
    /// </summary>
    private void TakeOff(IReadOnlyList<ChatMessage> words)
    {
        if (_door is not { } door) return;
        List<string> ids = [.. words.Where(word => word.Kept && word.Id is not null).Select(word => word.Id!)];
        if (ids.Count == 0) return;
        _takingOff = TakeOffAfterAsync(_takingOff, door, ids);
    }

    /// <summary>The take-offs that failed, tried once more as the inbox would close. Called under the gate.</summary>
    private void TryAgain()
    {
        _triedAgain = true;
        if (_door is { } door) _takingOff = TakeOffAfterAsync(_takingOff, door, []);
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
            lock (_gate) _untaken.InsertRange(0, taking);
        }
    }

    private static TaskCompletionSource Landed()
    {
        var landed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        landed.SetResult();
        return landed;
    }

    /// <summary>
    /// The keeping stop (COWORK1a, the design §3.2; <c>CANCEL_TURN</c>, <c>sessions stop --turn</c>): stop the running turn so what
    /// is held goes next, the person's own press. <see cref="TurnStop.Nothing"/> with nothing held, before the session is open,
    /// where nothing can stop a turn and keep the session (the native door's run), or where words go at once (STEER1): a stop
    /// while one is on its way would answer it cancelled while the agent may still act on it (docs/2026-10-03-steer-evidence.md §3).
    /// </summary>
    /// <remarks>A turn is asked to stop once, however often the person presses (STEER2a): the answer still says it was.</remarks>
    public async Task<TurnStop> SendNowAsync()
    {
        Stop? stop;
        lock (_gate)
        {
            if (_closed || _held.Count == 0 || _interrupt is null || _reach == DrivenReach.NextStep) return TurnStop.Nothing;
            stop = StopThisTurn();
        }

        if (stop is not null) await SendStopAsync(stop).ConfigureAwait(false);
        return new TurnStop(Cancelled: true, Withdrawn: []);
    }

    /// <summary>A stop decided for one turn (STEER2a): the door's interrupt, the turn, and what tells the run it is on the wire or dropped.</summary>
    private sealed record Stop(Func<Task> Interrupt, int Turn, TaskCompletionSource Sent);

    /// <summary>
    /// Decide the stop for the turn running now, once per turn; null where it was asked already or nothing can stop a turn. The
    /// run takes no held word until it is on the wire or dropped (<see cref="NextOrClose"/>). Called under the gate.
    /// </summary>
    private Stop? StopThisTurn()
    {
        if (_interrupt is not { } interrupt || _stoppedTurn == _turn) return null;
        _stoppedTurn = _turn;
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _stopping = _stopping.IsCompleted ? sent.Task : Task.WhenAll(_stopping, sent.Task);
        return new Stop(interrupt, _turn, sent);
    }

    /// <summary>
    /// Send a stop to the turn it was decided for, or drop it where the run took another turn or the inbox closed; the run may
    /// then take the next held word, never before. Called outside the gate: the door's interrupt is its own.
    /// </summary>
    private async Task SendStopAsync(Stop stop)
    {
        try
        {
            bool current;
            lock (_gate) current = !_closed && _turn == stop.Turn;
            if (current) await stop.Interrupt().ConfigureAwait(false);
        }
        finally
        {
            stop.Sent.TrySetResult();
        }
    }

    /// <summary>Where it stands for the page: the person's words held, never Daoris's own (COWORK1b), which the record shows as Daoris's.</summary>
    private ChatQueue Now() => _closed ? ChatQueue.Idle : new ChatQueue(Taking: true, [.. _held.Where(message => message.Daoris is null)]);

    /// <summary>
    /// When a word reaches the session on the door known, by its level (COWORK1a, the design §3.0): an ordinary word at the turn's
    /// end on every door; any other at the door's own reach. Called under the gate, once the door is known.
    /// </summary>
    private DrivenReach ReachOf(MessageLevel level) =>
        level == MessageLevel.Ordinary ? DrivenReach.TurnEnd : _reach ?? DrivenReach.TurnEnd;

    /// <summary>Whether a word goes to the running turn now: urgent or now, on a door that takes words at its next step, its first prompt on the wire.</summary>
    private bool GoesAtNextStep(ChatMessage message) =>
        _reach == DrivenReach.NextStep && _flowing && message.Level != MessageLevel.Ordinary;

    /// <summary>Send one word now, under the gate so words leave in the order said; its answer waits for the run.</summary>
    private void Send(ChatMessage message)
    {
        Task<string> answer;
        try
        {
            answer = _deliver!(message);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            answer = Task.FromException<string>(error);
        }

        // Observed here as well as by the run, which may have ended before it reads this one.
        _ = answer.ContinueWith(static sent => sent.Exception, TaskScheduler.Default);
        _sent.Enqueue(answer);
        // COWORK1c: handed to the wire, so off the record by its id.
        TakeOff([message]);
        // COWORK1b: Daoris's word at the next step went now, as the look that held it judged it.
        DaorisWords.Delivered(message);
    }

    /// <summary>Steer one word into the running turn now (STEER2a), under the gate as <see cref="Send"/> sends; the run waits for its answer.</summary>
    private void Steer(Func<ChatMessage, Task<string>> steer, ChatMessage message)
    {
        Task<string> answer;
        try
        {
            answer = steer(message);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            answer = Task.FromException<string>(error);
        }

        _ = answer.ContinueWith(static steered => steered.Exception, TaskScheduler.Default);
        _steered.Enqueue(answer);
        TakeOff([message]);
    }

    /// <summary>Tell the record what was said. Called under the gate, so told in order; a listener's failure is its own.</summary>
    private void Tell(ChatMessage message, DrivenReach reach)
    {
        try
        {
            _told?.Invoke(message, reach);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The words still go: the record misses them, and the session does not.
        }
    }

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
