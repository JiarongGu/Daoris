namespace Daoris.Driver;

/// <summary>
/// A conversation goes on with another agent (MIXAGENT1a-i, D163, the mixed-agents design §2 and §5): conversations live in the
/// shell's chat runner, so the ask, the box and the take are here, over the runner's context (PART10g, D166).
/// <see cref="HandOver"/> judges and keeps the box; <see cref="HandOverTake"/> runs the sequence with the runner's own end, start
/// and say.
/// </summary>
/// <param name="runner">
/// The runner whose conversations these are: its start begins the new conversation, as every door's does, and its turns say
/// whether one is talking.
/// </param>
internal sealed class ChatHandOver(ChatContext context, IChatRunning runner)
{
    /// <summary>How long the old conversation's end is waited for before the new one starts: a stop, not a wind-up.</summary>
    private static readonly TimeSpan HandOverEnd = TimeSpan.FromSeconds(15);

    /// <summary>How long a take waits, once its prompt was handed to a turned door, for the door to say it wrote before it tells its end (TAKEWAITS1).</summary>
    internal static readonly TimeSpan WireBound = TimeSpan.FromSeconds(15);

    /// <summary>
    /// The most the closing note's one turn is waited for (SESSCTX1c part B, §2.2): a move never waits more than that turn, and a turn
    /// that hangs past this is the conversation's end's to stop; its note is then said absent.
    /// </summary>
    internal static TimeSpan ClosingBound { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>The states a record is live in, where nothing here may end it (it runs in another process).</summary>
    private static readonly HashSet<string> LiveStates = new(StringComparer.OrdinalIgnoreCase) { "queued", "starting", "working", "awaiting-person" };

    private readonly ChatContext _context = context;

    private readonly IChatRunning _runner = runner;

    /// <summary>
    /// The conversations' move record the hand-over is decided in and taken from (FOUNDATION1f-v part a; the foundation design §3.3), over the
    /// home's one store file the limit move and the curve share: a hand-over keeps its move there and writes no box. A test hands one of its
    /// own run; the runner hands every move the one holder it comes up through.
    /// </summary>
    internal ConversationMoves Moves { get; set; } = new(context.Home, context.Service, context.Events, context.Output);

    /// <summary>
    /// A move's take is over, however it came out, its move ended in the store (HANDWORDS1, TAKEWAITS1): the person's words held while it was
    /// taken go on now, to the conversation that continues it where one opened. Told on the take's own thread, after its end, by the
    /// hand-over, the curve's move and the limit move alike.
    /// </summary>
    public event Action<string>? TakeEnded;

    /// <summary>What the course answered each hand-over of a conversation, by session (FOUNDATION1h branch 3): a test hears it; no person reads it.</summary>
    internal event Action<string, LineCourse>? Decided;

    /// <summary>
    /// Whether a hand-over holds the record from every go-on, sweep and take-up (FOUNDATION1f-v part a, §13.5): a box an older build left for
    /// a conversation and not yet imported, or imported and waiting behind a standing move, or a move the store carries that waits to be taken.
    /// </summary>
    public bool Holds(string sessionId) => new HandOverBox(_context.Home).Read(sessionId) is not null || Moves.HandOverStanding(sessionId) is not null;

    /// <summary>
    /// *Hand a conversation over* (MIXAGENT1a-i): <see cref="HandOver.AskAsync"/> judges and decides the move in the store; a conversation
    /// that is not talking is taken at once, and one that is talking keeps the move, at <c>quiescing</c>, until its turn ends
    /// (<see cref="Due"/>), or the shell comes up (<see cref="ChatRunner.TakeUpAsync"/>).
    /// </summary>
    /// <param name="why">One of <see cref="HandOverWhy"/>.</param>
    /// <param name="askedBy">One of <see cref="HandOverBy"/>.</param>
    /// <param name="onEnded">Told when the new conversation ends, as a start's is.</param>
    public async Task<HandedOver> HandOverAsync(
        string sessionId, string agent, string? account, string why, string askedBy, DriverConfig config,
        Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        if (_context.Closing) return new HandedOver(null, false, null, "the application is closing, so no conversation is handed over now.");

        // FOUNDATION1h branch 3 (the foundation design §3.1): this is the course's adapter for a conversation's hand-over. The boundary is the
        // runner's turn; the hand-over rule's consent is the door's, which calls the runner only for an ask it let go (`go`), so an agent's ask
        // reaches the course admitted and the person's press needs none. The course decides inside the ask, and what the executor alone
        // knows, a conversation another Daoris process runs, is refused once the course has allowed the move, before it is kept.
        var consent = askedBy == HandOverBy.Agent ? HandOver.ConsentOf(HandOverAskVerdict.Go(agent)) : null;
        var asked = await HandOver.AskAsync(
                _context.Service, Moves, AccountsOf, sessionId, agent, account, why, askedBy, DateTimeOffset.UtcNow, ct, consent,
                _runner.Taking(sessionId) ? CourseBoundary.TurnRunning : CourseBoundary.BetweenTurns,
                // A conversation another Daoris process runs cannot be ended from here: its own shell takes the ask, or it is stopped there.
                prior => LiveStates.Contains(prior.State) && !_context.Talking.ContainsKey(sessionId)
                    ? HandOver.Refused(
                        HandOver.Running,
                        $"{sessionId} is {prior.State}, running in another Daoris process on this machine: hand it over there, or stop it first.")
                    : null,
                course => Decided?.Invoke(sessionId, course))
            .ConfigureAwait(false);
        if (asked is not { Move: { } move, Request: { } request }) return new HandedOver(null, false, asked.Answer.Why, asked.Answer.Message);

        // Talking, it goes on with its turn and is taken at that turn's end: the move waits for the old part's own turn (§3.3).
        if (_runner.Taking(sessionId))
        {
            move.Quiescing();
            return new HandedOver(null, true, null, asked.Answer.Message);
        }

        return await TakeCoreAsync(sessionId, request, config, onEnded, moved: null, move, keeps: true, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The hand-over of a conversation whose turn just ended is taken (MIXAGENT1a-i): told as the turns move, and read only where a turn
    /// is neither in flight nor waiting. A hand-over left from before is taken as the shell comes up.
    /// </summary>
    public void Due(string sessionId, ChatQueue queue, DriverConfig config, Func<string, string, Task>? onEnded)
    {
        if (_context.Closing || queue.Taking || queue.Opening || queue.Queued.Count > 0) return;
        if (Moves.HandOverStanding(sessionId) is null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await TakeAsync(sessionId, config, onEnded).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Said wherever it could be; the move is ended by its take, and the asker asks again.
            }
        });
    }

    /// <summary>
    /// Every hand-over this run holds that waits to be taken is taken (MIXAGENT1a-i), as the shell comes up and at each look: an ask decided while
    /// no runner heard it, or while its conversation talked and the shell closed, a dead run's the take-up left standing, or an older build's box
    /// the come-up imported (FOUNDATION1f-v part a). Each is judged again as it is taken; one whose conversation is talking or being taken is
    /// left for its turn's end.
    /// </summary>
    /// <param name="onEnded">Told as each new conversation ends: the runner's own announcement, since no route started it.</param>
    public async Task<IReadOnlyList<ChatStart>> TakeAllAsync(Func<string, string, Task> onEnded, CancellationToken ct)
    {
        var taken = new List<ChatStart>();
        foreach (var waiting in Moves.HandOversWaiting())
        {
            if (_context.Closing || ct.IsCancellationRequested) break;
            if (_context.GoingOn.ContainsKey(waiting.Left) || _runner.Taking(waiting.Left)) continue;
            try
            {
                var handed = await TakeAsync(waiting.Left, _context.MachineConfig(), onEnded, ct).ConfigureAwait(false);
                taken.Add(new ChatStart(handed.SessionId, handed.Message));
            }
            catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // The take ended its move by its phase; the asker is told wherever it could be, and asks again.
            }
        }

        return taken;
    }

    /// <summary>Take one hand-over from the store: the sequence of <see cref="HandOverTake"/> through the runner, one at a time per conversation.</summary>
    public Task<HandedOver> TakeAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct = default)
    {
        if (Moves.HandOverStanding(sessionId) is not { } waiting || ConversationMoves.AskOf(waiting) is not { } ask || Moves.Claimed(waiting) is not { } record)
        {
            return Task.FromResult(new HandedOver(null, false, null, $"nothing waits to be handed over from `{sessionId}`."));
        }

        return TakeCoreAsync(sessionId, ask, config, onEnded, moved: null, record, keeps: true, ct);
    }

    /// <summary>
    /// The curve's move taken (FOUNDATION1f-iii part a, the foundation design §3.3, §3.4 rule 5, §13.4): handed the request its decision made,
    /// since its move is in the store and no box is written. The sequence is <see cref="HandOverTake"/>'s, shared with the person's and the
    /// agent's hand-overs, whose move waits for the turn's end (<c>keeps</c>); the move's phases are written around it: <c>old-ended</c> as
    /// the old part ends, then <c>opening</c> under the key the open is sent with, the answer, <c>opened</c>, <c>delivering</c> with the prompt
    /// marked before the hand, the prompt's outcome as the door's write returns (the curve's message carries no word id, so no word's
    /// <c>sent</c>), then the take, and the move ended by its phase where it goes no further before its hand. A write the store refuses stops
    /// the move before the effect it fences.
    /// </summary>
    /// <param name="ask">The curve's decision, as a hand-over's ask: the conversation's own agent, the account it named, <c>curve</c>, <c>daoris</c>.</param>
    /// <param name="moved">What the usage curve's decision said, for the two lines the take writes.</param>
    /// <param name="record">The move's record, decided under this run's claim by the caller.</param>
    internal Task<HandedOver> TakeAsync(
        string sessionId, HandOverRequest ask, SwitchChoice.MoveTo moved, ConversationMove record, DriverConfig config,
        Func<string, string, Task>? onEnded, CancellationToken ct = default) =>
        TakeCoreAsync(sessionId, ask, config, onEnded, moved, record, keeps: false, ct);

    /// <param name="keeps">
    /// A hand-over's move (FOUNDATION1f-v part a) stands where the take cannot begin, the conversation being taken or talking, and goes on at
    /// its turn's end; the curve's is closed <c>superseded</c> and judged again at the next turn's end.
    /// </param>
    private async Task<HandedOver> TakeCoreAsync(
        string sessionId, HandOverRequest ask, DriverConfig config, Func<string, string, Task>? onEnded, SwitchChoice.MoveTo? moved,
        ConversationMove record, bool keeps, CancellationToken ct)
    {
        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_context.GoingOn.TryAdd(sessionId, going.Task))
        {
            if (!keeps) record.Superseded();
            return new HandedOver(null, true, null, $"`{sessionId}` is already being taken.");
        }

        IChatTurns? held = null;
        // The move's own ends: the key its open is sent with, a write the store refused (which stops the move before what it fences), and
        // whether its prompt's write returned and was recorded, so the move is ended by its phase in the finally where it did not; a move
        // kept for the turn's end is not ended at all.
        var key = MoveWords.ChatKey();
        string? unasked = null;
        string? fenced = null;
        var wired = false;
        var waits = false;
        var wrote = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Wire() => wrote.TrySetResult(Wired(record));
        try
        {
            ClosingNote? closing = null;
            // Never mid-turn (SWITCH1c-ii, the switch design §4): its turns are held while it is taken, so a word said as the take
            // begins either started its turn first, and the move waits for that turn's end (Due), or takes nothing.
            if (_context.Turned.TryGetValue(sessionId, out var chat))
            {
                // FOUNDATION1o: where the host's open moves the old part's words, one said while held is kept on the record for that open.
                if (!chat.Turns.Hold(await _context.Service.Opens.KeepsAsync(ct).ConfigureAwait(false)))
                {
                    if (keeps) waits = true;
                    else record.Superseded();
                    return new HandedOver(null, true, null, $"`{sessionId}` is talking: it goes on as its turn ends.");
                }

                held = chat.Turns;

                // 🔴 SESSCTX1c part B (D189's amendment, the session-context design §2.2): the person's press, or a change of agent its own
                // agent asked for and left no note with, asks the outgoing agent for one closing note in one turn of Daoris's, before the
                // conversation ends; a move the account forces asks none. Asked once, under the hold, so no word of the person's goes first.
                var own = ask.AskedBy == HandOverBy.Agent ? new HandOverAskFiles(_context.Home).Ask(sessionId)?.Note : null;
                if (HandOverNote.Asks(ask, own)) closing = await AskClosingAsync(sessionId, chat.Turns, ask.Agent).ConfigureAwait(false);
            }

            // A move by the usage curve says which accounts it moved between: its decision, which the caller holds (SWITCH1c-ii). A hand-over
            // taken without it (an older build's box imported, or a shell come up since) says the hand-over's lines, its reason curve.
            return await HandOverTake.RunAsync(
                _context.Service, _context.Home, _context.Events, _context.Output, sessionId, ask, AccountsOf,
                async old =>
                {
                    await EndForHandOverAsync(old).ConfigureAwait(false);
                    // FOUNDATION1f-iii part a (§13.4 steps 1 and 3): the old part's end, then the open asked under the key it is sent with,
                    // each before what it fences; a write the store refuses opens nothing, and the line stays on the old record.
                    unasked = record.OldEnded() ?? record.Opening(key);
                },
                // The person's press with an account named is their pick of it, which the new conversation waits for (FOUNDATION1a-ii); an
                // agent's ask or the curve's move names an account that keeps today's reading. The keyed open (FOUNDATION1d-ii part 4) moves
                // every word of the person's kept on the old part meanwhile, none named (HandOverTake asks it under the move's key).
                async (prior, request, from, keyed, token) =>
                {
                    if (unasked is not null) return new ChatStart(null, unasked);

                    var started = await _runner.StartAsync(
                            prior.Repository!, request.Agent, config, onEnded, request.Account, ct: token, tree: prior.Tree, from: from,
                            picked: keeps && request.AskedBy == HandOverBy.Person && !string.IsNullOrWhiteSpace(request.Account), moves: keyed)
                        .ConfigureAwait(false);
                    if (started.SessionId is not { } to) return started;

                    // §13.4 steps 2 and 3: the open's answer, then `delivering` and the prompt's mark, before any hand is made. Where the
                    // line's head has gone on past the new part (an older build), nothing is handed there.
                    record.Answered();
                    fenced = record.Opened(to, started.Moved);
                    if (fenced is null && await record.SupersededAsync().ConfigureAwait(false)) fenced = "the line went on past its new part";
                    fenced ??= record.Delivering();
                    return started;
                },
                async (id, message) =>
                {
                    if (fenced is not null) return false;

                    // §3.4 rule 5: the prompt's outcome as the door's write returns, before the take; the message carries no word id, so no
                    // word's `sent`. A text door writes as it is handed and tells no message that it wrote: its return is the write's.
                    var turned = _context.Turned.ContainsKey(id);
                    var taken = await SayHandedOver(id, message with { Delivered = Wire }).ConfigureAwait(false);
                    wired = taken;
                    if (taken && !turned) Wire();
                    return taken;
                },
                ct, moved, closing, opens: key).ConfigureAwait(false);
        }
        finally
        {
            // A move that went no further than its hand is ended by its phase, its prompt known unwritten; one kept for the turn's end stands.
            if (!wired && !waits) await record.EndAsync().ConfigureAwait(false);

            // The move's own reading ends it in the store before the take is told over (TAKEWAITS1), so a word the person said meanwhile and
            // held waits no longer. A turned door tells that it wrote as its turn does, which a door that never writes does not: bounded.
            if (wired && await Task.WhenAny(wrote.Task, Task.Delay(WireBound)).ConfigureAwait(false) == wrote.Task) await (await wrote.Task).ConfigureAwait(false);

            // Ended, nothing listens to them now; refused before its end, the conversation takes words again.
            held?.Release();
            _context.GoingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
            if (!waits) Ended(sessionId);
        }
    }

    /// <summary>The message is on the wire: its prompt's outcome is written now, before the take, then the one reading is made on a task of its own.</summary>
    private static Task Wired(ConversationMove record)
    {
        record.Wire([]);
        return Task.Run(record.ReadAsync);
    }

    /// <summary>
    /// The outgoing agent's one closing turn (SESSCTX1c part B, the session-context design §2.2), under the take's hold: Daoris's ask,
    /// then its last message of that turn as its note, read from the events the turn added. The record keeps the ask as Daoris's and the
    /// reply as the agent's; the brief is composed from the events before the ask, so the note is told once, in its own section.
    /// </summary>
    private async Task<ClosingNote> AskClosingAsync(string sessionId, IChatTurns turns, string agent)
    {
        var before = _context.Events.After(sessionId, 0).Events.Count;
        // Daoris's word, by a key that names no fact of Daoris's, so nothing judges it again: it is the take's own, said once.
        var turn = turns.TakeHeld(new ChatMessage(HandOverNote.Ask(agent), []) { Daoris = new DaorisWord($"closing-note/{sessionId}") });
        if (turn is null) return new ClosingNote(null, before);
        await Task.WhenAny(turn, Task.Delay(ClosingBound)).ConfigureAwait(false);
        var added = _context.Events.After(sessionId, 0).Events;
        return new ClosingNote(HandOverNote.Written([.. added.Skip(before)]), before);
    }

    /// <summary>Tell <see cref="TakeEnded"/>: a listener's failure is its own, and the take is over either way.</summary>
    internal void Ended(string sessionId)
    {
        try
        {
            TakeEnded?.Invoke(sessionId);
        }
        catch (Exception)
        {
            // The words it holds are tried again at its next look (SessionWords' poll), so nothing is lost by a listener that threw.
        }
    }

    /// <summary>
    /// The accounts the roster holds for an agent, or null where it has no such agent or one that holds no conversations: what a
    /// hand-over's agent and account are judged by, the curve's move's too.
    /// </summary>
    public IReadOnlyList<string>? AccountsOf(string agent)
    {
        try
        {
            var resolved = _context.Harnesses.Adapters.Resolve(agent);
            if (!resolved.Interactive) return null;
            return resolved.Toolchain is { } toolchain
                ? HarnessSettings.Profiles(_context.Harnesses.Home, toolchain.Owner(resolved.Name))
                : [];
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>
    /// The old conversation ends by the runner's own end: its process is stopped and its record says so, returning once its watch
    /// has written it, or after <see cref="HandOverEnd"/>. One that already ended has nothing to stop. A limit's move ends the old part
    /// the same way (<see cref="LimitMove"/>).
    /// </summary>
    internal async Task EndForHandOverAsync(string sessionId)
    {
        if (!_context.Watching.TryGetValue(sessionId, out var watch)) return;

        _context.Processes.Stop(sessionId, ChatEnded.EndedNote(stopped: false, notKept: false));
        await Task.WhenAny(watch, Task.Delay(HandOverEnd)).ConfigureAwait(false);
    }

    /// <summary>
    /// The new conversation's first message (MIXAGENT1a-i): Daoris's briefing, with the browser's line after it where it was handed
    /// one, and recorded as the note of where it came from (<see cref="ChatMessage.TakenOver"/>). A hand-over's carries none of the
    /// person's words; a part that goes on from a lost conversation carries those that waited there after the brief (SESSCTX1c part B,
    /// <see cref="BriefedMove"/>).
    /// </summary>
    /// <param name="shown">
    /// The words a keyed open moved to the part (FOUNDATION1d-ii part 4, <see cref="MovedWords.Shown"/>): each recorded by its own event under its
    /// id, before the write, in place of the message's one joined event; null for every other message.
    /// </param>
    internal async Task<bool> SayHandedOver(string sessionId, ChatMessage message, IReadOnlyList<SessionEvent>? shown = null)
    {
        if (!_context.Turned.ContainsKey(sessionId) && !_context.Talking.ContainsKey(sessionId)) return false;

        message = _runner.WithProcess(sessionId, message);
        // The browser's line after Daoris's brief (SESSCTX1b keeps the brief a field of its own, so the line is the briefing's alone).
        if (_context.Browsers.TryRemove(sessionId, out var browser))
        {
            message = message with { Briefing = message.Briefing is { Length: > 0 } told ? $"{told}\n\n{browser}" : browser };
        }
        if (_context.Turned.TryGetValue(sessionId, out var chat))
        {
            // Held for the turn that writes the message, which records them before its write (ChatContext.Shown).
            if (shown is not null) _context.Shown[sessionId] = (message.Text, shown);
            if (chat.Turns.Take(message) is not null) return true;
            if (shown is not null) _context.Shown.TryRemove(sessionId, out _);
            return false;
        }

        // A text door has no turns to record it as they are sent: the note is kept here, with what is sent, so no path says nothing.
        // Its process is tracked a moment after the start returns, and a send before that reaches nothing: tried again, bounded.
        // FOUNDATION1d-ii part 4: moved words are recorded before the write, as the turned doors record them, the agent having gone said after.
        if (shown is not null)
        {
            foreach (var recorded in ChatRecorded.Recorded(message, shown)) _context.Record(sessionId, recorded);
        }

        var framed = _context.Talking.TryGetValue(sessionId, out var adapter) ? adapter.FrameMessage(message.Prompt) : message.Prompt;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (_context.Processes.Send(sessionId, framed))
            {
                if (shown is null)
                {
                    foreach (var recorded in ChatRecorded.Recorded(message)) _context.Record(sessionId, recorded);
                    return true;
                }

                // On the wire: a limit's move takes the moved words off the new part now, as a turned door tells it once it wrote them.
                message.Delivered?.Invoke();
                return true;
            }

            if (!_context.Talking.ContainsKey(sessionId)) break;
            await Task.Delay(100).ConfigureAwait(false);
        }

        if (shown is not null)
        {
            var gone = WordsNotes.NotSent();
            _context.Record(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = gone.Note, Parts = gone.Parts });
        }

        return false;
    }
}
