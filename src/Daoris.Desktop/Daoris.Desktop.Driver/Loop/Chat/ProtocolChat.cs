namespace Daoris.Driver;

/// <summary>
/// One conversation on the protocol door: its session once opened, and its turns one at a time, in the order the person sent them
/// (<see cref="ChatTurns"/>).
/// </summary>
/// <remarks>
/// <para><b>A message is recorded as it is sent, not as it is typed</b>: the queue's rule, and the reason it exists.</para>
///
/// <para><b>A stop is <c>session/cancel</c></b>, which keeps the session: the agent winds its turn up and answers the prompt
/// <c>cancelled</c>, and that answer is the turn's end in the record.</para>
///
/// <para><b>A turn that could not be sent is said</b>, on the transcript and in the record, and the conversation goes on: the process is
/// still there, and so is the person.</para>
///
/// <para><b>On the next-step door</b>, where the agent said it takes a prompt during a turn, a word said while a turn runs goes at once,
/// shown waiting with its reach and taken where the agent hands the turn off to it. A stop with such a word on its way says the agent may
/// have read it.</para>
/// </remarks>
internal sealed class ProtocolChat : ITurnedChat, IEpisodeWords
{
    IChatTurns ITurnedChat.Turns => Turns;

    // Its run once its session opened, or null where it could not: its session is the run's.
    private readonly TaskCompletionSource<TransportRun?> _open = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Func<bool> _stopped;
    private readonly Func<string, LimitSeen?, Noted?>? _limited;
    private readonly Func<string, Noted?>? _signedOut;
    // The retry its session is handed, whose last reading says the failure's code; null before the open or with none.
    private volatile TransientRetry? _retried;
    private readonly Action? _wentOn;
    private Action<string> _line = _ => { };
    private Action<SessionEvent> _record = _ => { };

    // The words handed to the running turn and not yet answered, and those a stop is asked while on their way.
    private readonly object _gate = new();
    private readonly HashSet<string> _onTheirWay = new(StringComparer.Ordinal);
    private readonly HashSet<string> _stoppedUnder = new(StringComparer.Ordinal);

    // The answers of the words steered into the turn on the wire, for a transient episode to settle (the turns those words opened are the
    // episode's to await); and the turn's own cut: the person's stop ends a wait between tries, which no prompt of the session's is open to
    // answer.
    private readonly Queue<Task<string>> _sentAnswers = new();
    private CancellationTokenSource _cut = new();
    private volatile bool _stopAsked;
    private volatile bool _unwanted;

    /// <param name="limited">
    /// A refused turn's failure, read for an account's limit, with the reader's limit by its code where it read one: the line its record
    /// takes, with its code, or null.
    /// </param>
    /// <param name="signedOut">A refused call's failure, read for the account's sign-in: the line its record takes, or null.</param>
    /// <param name="goOn">The ended chat's conversation this one goes on in, resumed rather than opened; null for a new one.</param>
    /// <param name="wentOn">Told once the words it goes on with are on the wire.</param>
    public ProtocolChat(
        Action<ChatQueue> changed, Func<bool> stopped, Func<string, LimitSeen?, Noted?>? limited = null,
        Func<string, Noted?>? signedOut = null, ResumeAsk? goOn = null, Action? wentOn = null)
    {
        _stopped = stopped;
        _limited = limited;
        _signedOut = signedOut;
        _wentOn = wentOn;
        GoOn = goOn;
        Turns = new ChatTurns(
            ready: async () => await _open.Task.ConfigureAwait(false) is not null,
            take: TurnAsync,
            interrupt: CancelAsync,
            changed,
            steer: Steer,
            steerNow: SteerNow,
            nextStep: () => Words is { NextStep: true },
            settled: SettleSteeredAsync,
            cutsIn: true);
    }

    /// <summary>
    /// What a transient failure of a turn is tried again with: handed to its session once the session is open, so a failure at the open
    /// keeps its end. Null keeps that end for every failure.
    /// </summary>
    public ChatRetry? Retry { get; init; }

    /// <summary>What its actor asks, set on the session before its first prompt; null sets nothing.</summary>
    public ActorAsked? Asked { get; init; }

    /// <summary>Told what is carried where it differs from <see cref="Asked"/>.</summary>
    public Action<ActorAsked>? Carried { get; init; }

    /// <summary>Told what the open listed, its agent's offer reading on its account.</summary>
    public Action<IReadOnlyList<AcpConfigOption>>? OffersRead { get; init; }

    /// <summary>Its pending start, which a refusal at its open moves its words to; null for none.</summary>
    public ChatPending? Pending { get; init; }

    /// <summary>
    /// What a message shows of the words a keyed open moved to this part (<see cref="ChatContext.TakeShown"/>): each moved word's own
    /// event, recorded before the write in place of the message's one joined event; null for every other message.
    /// </summary>
    public Func<ChatMessage, IReadOnlyList<SessionEvent>?>? Shown { get; init; }

    /// <summary>
    /// Asked where its agent refused <see cref="Asked"/>'s model at the open, before any prompt: a later entry to set on this same session,
    /// or null, and the open ends <c>choice-not-offered</c>. Null reads the model as not carried.
    /// </summary>
    public IChoiceReask? ModelRefused { get; init; }

    /// <summary>The refusal that ended its open before any prompt, or null.</summary>
    public ModelRefusal? NotOffered { get; set; }

    public ChatTurns Turns { get; }

    /// <summary>The ended chat's conversation this one goes on in, or null for a new conversation.</summary>
    public ResumeAsk? GoOn { get; }

    /// <summary>The session, once it opened, as its run holds it; null while it opens, or when it could not. Read for its options alone.</summary>
    public AcpSession? Session => _open.Task.IsCompletedSuccessfully ? _open.Task.Result?.Session : null;

    /// <summary>Its words and steer on the run, once its session opened; null while it opens, or when it could not.</summary>
    public ProtocolWords? Words => _open.Task.IsCompletedSuccessfully ? _open.Task.Result?.Words : null;

    public PermissionHolds? Permissions { get; init; }

    /// <summary>What its actor asked, and where its permission requests are held, set on the session the run made before its open.</summary>
    public void Configure(AcpSession session)
    {
        session.Permissions = Permissions;
        session.Carry = Asked;
        session.Carried = Carried;
        session.OffersRead = OffersRead;
        session.ModelRefused = ModelRefused;
    }

    /// <summary>The session is open on its run — or could not be, and then every turn asked for is dropped.</summary>
    public void Opened(TransportRun? run, Action<string> line, Action<SessionEvent> record)
    {
        _line = line;
        _record = record;
        if (run?.Words is { } wire && Retry is { } retry)
        {
            _retried = Retried(retry);
            wire.Retries(_retried, this);
        }

        _open.TrySetResult(run);
    }

    /// <summary>
    /// The retry its session is handed: the reader and the clock it is built with, the owner's check, and what the episode says in this
    /// conversation, the one line, a quiet event each wait and try, and Daoris's go-on.
    /// </summary>
    private TransientRetry Retried(ChatRetry retry) => new()
    {
        Read = retry.Read,
        Clock = retry.Clock,
        Limit = retry.Limit,
        // The owner's check: the conversation wants another turn unless its session or the turn is stopped by the person.
        Wanted = _ =>
        {
            if (!_stopAsked && !_stopped()) return Task.FromResult(true);
            _unwanted = true;
            return Task.FromResult(false);
        },
        // Daoris's go-on is shown once, as the conversation's own word beside the person's (the first try's alone); it is kept on no
        // record: a conversation's process is gone with a restart, and nothing of an episode outlives it.
        KeepGoOn = (episode, text, _) => Task.FromResult<IAgentWord?>(
            new ChatMessage(text, []) { Daoris = new DaorisWord(DaorisKeys.TransientRetry(retry.Session, episode.Began)) }),
        Began = (episode, next) =>
        {
            var noted = TransientRetry.Retrying(retry.Agent, episode.Words, next, retry.Limit);
            _line(noted.Note);
            _record(new SessionEvent { Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts });
        },
        Tried = (episode, number, next) =>
        {
            retry.Tried?.Invoke(episode, number, next);
            _record(new SessionEvent { Kind = SessionEventKind.Transient, Try = number });
        },
        WaitsUntil = (_, until) => _record(new SessionEvent { Kind = SessionEventKind.Transient, Next = until }),
        // A limit refused the try before its agent took the person's words it carried. They are recorded as a refused turn's words are,
        // after the try's quiet event, so `LimitHeld` holds them for the wake or the move, which receive them once; never the failed turn's
        // own, recorded before the episode's first event, nor Daoris's go-on.
        LimitRefused = words =>
        {
            foreach (var word in words.Where(word => !word.IsDaoris))
            {
                _record(ChatMessage.Words(ChatMessage.FromAgent(word)));
            }
        },
    };

    // The words an episode holds, waits on and carries are this conversation's turns'; the answers it settles are the steered words' own.
    void IEpisodeWords.Settling() => Turns.Settling();

    Task IEpisodeWords.Waiting() => Turns.Waiting();

    void IEpisodeWords.Waited() => Turns.Waited();

    IReadOnlyList<IAgentWord> IEpisodeWords.TakeHeld() => Turns.TakeHeld();

    Task<string>? IEpisodeWords.TakeSent()
    {
        lock (_gate) return _sentAnswers.TryDequeue(out var answer) ? answer : null;
    }

    /// <summary>
    /// A call the door refused, opening the session or taking a turn, read for the account's sign-in: where it is, the account reads
    /// signed out and the record says so, its English beside the line's parts as a session record's note carries them. Only the door's
    /// failure is read, as a driven start's conclusion reads it.
    /// </summary>
    public void ReadSignIn(Exception error)
    {
        if (error is not DriverException || _signedOut?.Invoke(error.Message) is not { } signedOut) return;
        _line($"— {signedOut.Note}");
        _record(new SessionEvent { Kind = SessionEventKind.Note, Text = signedOut.Note, Parts = signedOut.Parts });
    }

    /// <summary>
    /// A word said while a turn is on the wire, handed to it at once where the agent said it takes words then: shown waiting with its reach
    /// under a fresh id, then sent as a prompt of the same session. What completes when the agent has answered it; null where the door takes
    /// no word during a turn.
    /// </summary>
    /// <remarks>Called under the turns' lock: nothing here calls back into them.</remarks>
    private Task? Steer(ChatMessage message)
    {
        if (Words is not { NextStep: true } wire) return null;

        // Its own id, so the words shown while they wait pair with the same words where the session took them: the record's, where it kept
        // them first, never one of the door's own.
        var words = Identified(message);
        if (ChatRecorded.Told(words) is { } told) _record(told);
        _line($"— the person added, which reaches the conversation at its next step: {words.Text}");
        _record(ChatMessage.Words(words) with { Reaches = "next-step", Level = words.LevelSaid });
        // The session's own answer, kept for a transient episode to settle: the word's turn is the episode's to await.
        var answer = wire.SayAsync(words);
        lock (_gate)
        {
            _onTheirWay.Add(words.Id!);
            _sentAnswers.Enqueue(answer);
        }

        return AnsweredAsync(words, answer);
    }

    /// <summary>
    /// The person's *Send now* into the turn on the wire, where the agent steers: shown with the reach <c>now</c> under a fresh id, then
    /// steered into the running turn, cutting the step in flight. The session takes them where the agent answers <c>injected</c>, through its
    /// taken listener; any other answer costs only them, said by the session in the record. What completes when the agent has answered; null
    /// where it never said it steers.
    /// </summary>
    /// <remarks>Called under the turns' lock: nothing here calls back into them.</remarks>
    private Task? SteerNow(ChatMessage message)
    {
        if (Words is not { Steers: true } wire) return null;

        var words = Identified(message);
        if (ChatRecorded.Told(words) is { } told) _record(told);
        _line($"— the person sent now, which the conversation takes at once: {words.Text}");
        _record(ChatMessage.Words(words) with { Reaches = SendNowWhy.Spelled(TurnReach.Steered), Level = MessageLevels.Now });
        return wire.SteeredAsync(words, back => Turns.HandBack(ChatMessage.FromAgent(back) with { Said = true }));
    }

    /// <summary>A word handed to the turn on the wire under the record's id where it is kept there, or a fresh one of its own.</summary>
    internal static ChatMessage Identified(ChatMessage message) =>
        message is { Kept: true, Id: not null } ? message : message with { Id = $"said-{Guid.NewGuid():N}"[..13] };

    /// <summary>
    /// A word handed to the running turn, answered: said where the agent refused it, where it never reached the agent, or where the person's
    /// stop is asked while it is on its way and the agent answers it cancelled, since it may have read it and its answer is not kept.
    /// </summary>
    private async Task AnsweredAsync(ChatMessage words, Task<string> answered)
    {
        // The words' id beside each line, so the page settles any it still shows waiting.
        SessionEvent Said(Noted why) => new()
        {
            Kind = SessionEventKind.Note, Text = why.Note, Parts = why.Parts.Count > 0 ? why.Parts : null, Words = [words.Id!],
        };

        SessionEvent? said = null;
        try
        {
            var reason = await answered.ConfigureAwait(false);
            bool stoppedUnder;
            lock (_gate) stoppedUnder = _stoppedUnder.Contains(words.Id!);
            // With its code, which the page words in the reader's language.
            said = ChatTurns.Lost(words.Id!, stoppedUnder, reason);
        }
        catch (AcpRefusal refused)
        {
            // A word the agent would not take costs that word, in its own words, never the conversation.
            said = Said(WordsNotes.Refused(refused.Words, "conversation"));
        }
        catch (AcpWordTurnFailed)
        {
            // The word is taken, so the turn that failed is the one it opened: the turn's failure, settled where the turn's words are
            // (`SettleSteeredAsync`), never a note of this word's.
        }
        catch (Exception error) when (error is DriverException or IOException or ObjectDisposedException or InvalidOperationException)
        {
            said = Said(WordsNotes.NeverReached(error.Message));
        }
        finally
        {
            lock (_gate)
            {
                _onTheirWay.Remove(words.Id!);
                _stoppedUnder.Remove(words.Id!);
            }
        }

        if (said is null) return;
        _line(said.Text!);
        _record(said);
    }

    private async Task TurnAsync(ChatMessage message, Action sent)
    {
        if (await _open.Task.ConfigureAwait(false) is not { Words: { } wire }) return;

        // This turn's own cut and stop, and the answers of words steered into an earlier turn, all over.
        CancellationToken cut;
        lock (_gate)
        {
            _cut.Dispose();
            _cut = new CancellationTokenSource();
            cut = _cut.Token;
            _sentAnswers.Clear();
        }

        _stopAsked = false;
        _unwanted = false;
        try
        {
            if (message.GoesOn is { } goOn)
            {
                // The words an ended chat goes on with: the driver's first line, then each word under the id its record gave it, then one
                // prompt of one block each; off the record once on the wire.
                foreach (var opening in goOn.Opening())
                {
                    if (opening.Kind == SessionEventKind.Note && opening.Text is { } note) _line(note);
                    _record(opening);
                }

                // The files said with the words, a link each after their blocks.
                await PromptedAsync(wire, cut, () => wire.PromptAsync(goOn.Blocks, () =>
                {
                    sent();
                    _wentOn?.Invoke();
                }, goOn.Files)).ConfigureAwait(false);
                return;
            }

            foreach (var recorded in ChatRecorded.Recorded(message, Shown?.Invoke(message))) _record(recorded);
            // Each attached file a `resource_link` to where it is kept.
            await PromptedAsync(wire, cut, () => wire.PromptAsync(message.Prompt, sent, message.Files))
                .ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // Ended in the record by `Failed`: the turn's end is in it. Anything that is no failure of the door's is not ours.
            if (!Failed(error, cut)) throw;
        }
        finally
        {
            // The episode is over, whatever ended it: a word said from now goes as it did.
            Turns.Waited();
        }
    }

    /// <summary>
    /// One turn's prompt: where the conversation is handed a retry, a transient failure is tried again on this same process, inside the
    /// turn, so the turn is one however many tries it takes: the queue is not released, no turn ends in the record, and nothing is counted,
    /// notified or moved until the turn does. Any other failure comes out as it came, and one past the bound as
    /// <see cref="AcpTransientExhausted"/>.
    /// </summary>
    private async Task PromptedAsync(ProtocolWords wire, CancellationToken cut, Func<Task<string>> prompt)
    {
        if (Retry is null)
        {
            await prompt().ConfigureAwait(false);
            return;
        }

        await wire.RetriedAsync(prompt, cut).ConfigureAwait(false);
        // The person's stop met the episode between tries: no prompt of the session's is open to answer it, so the turn ends here.
        if (_unwanted)
        {
            _line("— the turn ended: it was stopped while it waited to try again");
            _record(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "cancelled" });
        }
    }

    /// <summary>
    /// What became of the words steered into the turn on the wire, settled before the turn is found to have ended: one whose own prompt is
    /// refused after it is taken (<see cref="AcpWordTurnFailed"/>) is the turn's prompt failing, so a transient one is tried again here, in
    /// the turn, which lasts. Any other answer is its word's own, said by its <see cref="AnsweredAsync"/>.
    /// </summary>
    private async Task SettleSteeredAsync()
    {
        if (Retry is null || Words is not { } wire) return;
        CancellationToken cut;
        lock (_gate) cut = _cut.Token;
        while (true)
        {
            Task<string>? answer;
            lock (_gate) answer = _sentAnswers.TryDequeue(out var next) ? next : null;
            if (answer is null) return;

            try
            {
                await answer.ConfigureAwait(false);
            }
            catch (AcpWordTurnFailed failed)
            {
                try
                {
                    await wire.RecoveredAsync(failed.Refusal, cut).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    // Ended in the record by `Failed`; a settling that no failure of the door's ended is the pump's, which ends the turn.
                    if (!Failed(error, cut)) throw;
                }
                finally
                {
                    Turns.Waited();
                }
            }
            catch (Exception)
            {
                // Its word's own note, said by AnsweredAsync.
            }
        }
    }

    /// <summary>
    /// A turn's prompt failed: the turn's end, in the record. False for an exception that is no failure of the door's, left to its catcher.
    /// A past-the-bound episode ends it with <c>chat.transient-stopped</c>; the person's stop is a stopped turn; any other failure is a turn
    /// the door would not take, read for the account's limit and sign-in.
    /// </summary>
    private bool Failed(Exception error, CancellationToken cut)
    {
        if (error is OperationCanceledException && cut.IsCancellationRequested)
        {
            // The person's stop ended the wait between tries: a stopped turn, nobody's failure.
            _line("— the turn ended: it was stopped while it waited to try again");
            _record(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "cancelled" });
            return true;
        }

        if (error is not (DriverException or IOException or ObjectDisposedException or InvalidOperationException)) return false;

        if (_stopped())
        {
            // The person stopped the session, which ended the agent and this turn's prompt with it. That is not the turn failing, and a
            // note saying so would blame the agent for the person's act. The turn ends as a stopped turn does.
            _line("— the turn ended: the session was stopped");
            _record(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "cancelled" });
            return true;
        }

        // Whatever it is, a turn's failure is one reading on the run's sink, as the driven door's is.
        if (_open.Task.IsCompletedSuccessfully && _open.Task.Result is { } failedRun)
        {
            failedRun.Read(new WireReading.Failed(DoorFailures.OfProtocol(error, DoorWhere.Prompt)));
        }

        if (error is AcpTransientExhausted exhausted && Retry is { } retry)
        {
            // Every try failed, so the turn ends `error` and the next word is a turn of this same conversation. No limit, no sign-in and no
            // door note: the provider's outage is not the account's.
            var stopped = Noted.Of(
                ChatNoteCodes.ChatTransientStopped,
                $"{retry.Agent}'s provider kept failing, so this turn ends; your next words go on in this conversation:",
                ("agent", retry.Agent));
            if (exhausted.Episode.Words is { Length: > 0 } words) stopped = stopped.Then(" ", Noted.Said(words, NoteBy.Program));
            retry.PastBound?.Invoke();
            _line($"— {stopped.Note}");
            _record(new SessionEvent { Kind = SessionEventKind.Note, Text = stopped.Note, Parts = stopped.Parts });
            _record(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" });
            return true;
        }

        var notTaken = DoorNotes.TurnNotTaken(error.Message);
        _line($"— {notTaken.Note}");
        _record(new SessionEvent { Kind = SessionEventKind.Note, Text = notTaken.Note, Parts = notTaken.Parts });

        // A refusal the door carried, read for an account's limit before the turn's end is recorded, so the refused turn is counted after
        // the turns that ended. Never the transcript's words. Its English beside the line's parts, as a refused sign-in's is, so the page
        // words it in either language. The reader's own reading of this failure reads a limit by its code where the sentence says none: the
        // last one the session's retry kept, `Acp.ReadFailure` running before a prompt's failure is thrown.
        var byCode = _retried?.Last is { Kind: AgentErrorKind.Limit, Limit: { } coded } ? coded : null;
        if (error is DriverException && _limited?.Invoke(error.Message, byCode) is { } cooling)
        {
            _line($"— {cooling.Note}");
            _record(new SessionEvent { Kind = SessionEventKind.Note, Text = cooling.Note, Parts = cooling.Parts });
        }

        // And for the account's sign-in, so the next start walks past an account its agent refused here.
        ReadSignIn(error);

        // The turn ends: only a turn event closes a turn on the page, which would otherwise show it working under this note while the
        // composer, told by the queue that nothing is taking, offered to send.
        _record(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" });
        return true;
    }

    private async Task CancelAsync()
    {
        if (!_open.Task.IsCompletedSuccessfully || _open.Task.Result is not { Words: { } wire } run) return;

        // A person's stop is never refused, even with a word on its way: the agent may still act on that word with its answer dropped,
        // and its record says so once the word is answered.
        lock (_gate) _stoppedUnder.UnionWith(_onTheirWay);
        // A stop met between tries has no prompt of the session's to answer it: the wait it ends is the turn's, cut here, and the owner's
        // check refuses a try the stop met anywhere else. A try on the wire is stopped as any turn is.
        _stopAsked = true;
        if (wire.WaitsOnTransient)
        {
            CancellationTokenSource cut;
            lock (_gate) cut = _cut;
            try
            {
                await cut.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // The turn it meant to stop ended.
            }
        }

        try
        {
            // The run's stop that keeps the conversation: `session/cancel`.
            await run.StopTurnAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The agent is gone, and its turn with it.
            _line($"— the stop could not reach the agent: {error.Message}");
        }
    }

    /// <summary>
    /// The turns already asked for, then its run's close, once its open settled: <c>session/close</c>, then the end of input — after which
    /// the agent exits on its own, and that exit is what the record concludes from.
    /// </summary>
    public async Task FinishAsync(Func<Task> close)
    {
        await Turns.FinishAsync().ConfigureAwait(false);
        await _open.Task.ConfigureAwait(false);
        await close().ConfigureAwait(false);
    }
}
