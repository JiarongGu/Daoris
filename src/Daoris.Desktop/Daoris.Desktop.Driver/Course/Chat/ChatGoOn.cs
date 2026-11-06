namespace Daoris.Driver;

/// <summary>
/// An ended chat goes on (MSG1c, D137 §2.2, §4.2): the person's words, kept on its record while it had ended (MSG1a) and
/// shown in its conversation by whichever door they were said at (MSG1d), are taken up here at once. Its record takes the
/// ledger's one move out of an ended state, and its own harness conversation is resumed by the id kept for it, with every
/// word waiting its first turn; or, where it cannot go on, the words stay waiting, marked, and its conversation says why.
/// A class over the runner's context (PART10h, D166).
/// </summary>
/// <remarks>
/// <para><b>One record per harness conversation; Daoris resumes only by the id it kept</b> (D137 §8): the protocol door's
/// <c>session/resume</c> or <c>session/load</c>, the native door's <c>--resume &lt;id&gt;</c>. Never <c>--continue</c> nor
/// a name, and never a new conversation under the same record.</para>
///
/// <para><b>Judged as a driven record is</b> (<see cref="Continuations.Judge"/>): what never goes on, no words, the adapter
/// it ran on, the account, its tree, a kept id and a door that resumes. A chat holds no quest, so nothing carries its words
/// on by itself: where it cannot go on, *Start a conversation with these words* is the person's press (MSG1f).</para>
///
/// <para><b>What holds it rather than refusing it</b> leaves the words waiting and unmarked, said in its conversation: an
/// account the selection will not start, a replay holding its repository's trees, the ledger refusing the move (another
/// session holds its tree, D51). The next word said to it tries again.</para>
///
/// <para><b>Words nobody heard</b> (MSG1c2): words kept on an ended chat while no shell ran are taken up as the shell comes
/// up (<see cref="TakeUpAsync"/>), and a chat taken up by itself announces its end (<see cref="TakenUpEnded"/>), since no
/// route that started it is there to hear it.</para>
///
/// <para><b>A registered executor</b>: a conversation going on runs through the runner's own run, in the place the runner makes. It
/// hears the record's events from <see cref="Hear"/>, which the runner calls once it is made, until <see cref="Stop"/>, which
/// <see cref="ChatRunner.StopAll"/> calls first.</para>
/// </remarks>
[Executor("go-on", ExecutorPhase.Move)]
internal sealed class ChatGoOn(IExecutorWiring wiring) : IExecutor
{
    /// <summary>The endings a chat goes on from: every one but a stand-down, which never goes on (D137 §2.2).</summary>
    internal static readonly HashSet<string> Ended = new(StringComparer.OrdinalIgnoreCase) { "completed", "declined", "failed", "stopped" };

    private ChatContext _context => wiring.Context;
    private IChatRunning _runner => wiring;
    private ChatHandOver _handOver => wiring.Of<ChatHandOver>();
    private IChatHelping _help => wiring.Of<IChatHelping>();
    private ChatHelperWords _words => wiring.Of<ChatHelperWords>();

    private BriefedMove _move => wiring.Of<BriefedMove>();

    // LIMITRESUME1c: a conversation held by its limit, proven lost at its wake, moves by the limit's own ended-record body (D206 §3.5).
    private LimitMove _limit => wiring.Of<LimitMove>();

    /// <summary>
    /// An ended chat whose words wait for its own cooling account, or one held by its limit (LIMITRESUME1c, D206 §3.5): told to the look's
    /// <c>limits</c> stage, which wakes it on its own account at the reset.
    /// </summary>
    internal void Waits(string session) => wiring.Of<LimitWaits>().Add(session);

    /// <summary>What the course answered each conversation's go-on, by session (FOUNDATION1h branch 7): a test hears it; no person reads it.</summary>
    internal event Action<string, LineCourse>? Decided;

    /// <summary>
    /// The course's answer for an ended conversation's go-on (FOUNDATION1h branch 7, the foundation design §3.1): the record, the words waiting
    /// and what the go-on has read so far go in as facts, the conversation stands between its parts, and <paramref name="alone"/> is the answers-alone
    /// reading <see cref="AnswersAloneHold"/> made (the course never reads an event file). Where the course would answer another thing than the go-on
    /// does it keeps today's answer (D212's differences 6 and 9), so what it says is heard and the go-on's reading stays its own.
    /// </summary>
    internal LineCourse Decide(PriorSession record, string? judged = null, bool agentGone = false, int? alone = null, LineAnswer? answer = null)
    {
        var course = CourseDecision.Decide(
            new CourseFacts(
                new CourseHead(
                    record.State, Role: record.Actor ?? "chat", Choice: record.Choice, Adapter: record.Adapter, Profile: record.Profile,
                    Tree: record.Tree, Conversation: true,
                    AskDaoris: string.Equals(record.Repository, KnowledgeRoomIdentity.Repository, StringComparison.OrdinalIgnoreCase),
                    AgentGone: agentGone, Judged: judged,
                    Waiting: [.. record.Waiting.Select(word => new CourseWord(word.Id ?? "", BriefedMove.AuthorOf(word), word.Key))],
                    Session: record.Session),
                CourseBoundary.ProcessExited,
                Account: new CourseAccount(Answer: answer, Picked: _context.Conversations.PickedOn(record.Session, record.Profile)),
                Alone: alone is { } at ? new CourseAlone(at, at) : null, Rules: new CourseRules(Runner: true)),
            CourseTrigger.WordsKept);
        Decided?.Invoke(record.Session, course);
        return course;
    }

    /// <summary>The runner is made: the record's events are heard from now.</summary>
    public void Hear()
    {
        // The person's words to an ended chat are kept on its record and shown in its conversation by whichever door they
        // were said at (MSG1d); hearing them there is what takes them up at once (MSG1c, D137 §2.2).
        _context.Events.Evented += Heard;
        // COWORK1c: a conversation the orphan sweep ended under words kept for its turns goes on with them as the sweep ends it.
        _context.Service.Moved += Swept;
    }

    /// <summary>The runner is closing: the record's events are heard no more.</summary>
    public void Stop()
    {
        _context.Events.Evented -= Heard;
        _context.Service.Moved -= Swept;
    }

    /// <summary>
    /// A record moved through this machine's client to <c>stopped</c> (COWORK1c, the message-urgency design §6): where it is a
    /// conversation nothing here holds, ended by the orphan sweep (<c>interrupted</c>) with words waiting, the words kept for its turns
    /// before the shell that ran it was cut off go on now, as words an ended chat was written to go on. The shell's own come-up
    /// (<see cref="TakeUpAsync"/>) reads the records before the sweep has ended them; this hears the sweep. The person's stop is never
    /// <c>interrupted</c>, so it starts nothing again.
    /// </summary>
    private void Swept(SessionMoved moved)
    {
        if (_context.Closing || !string.Equals(moved.State, "stopped", StringComparison.OrdinalIgnoreCase)) return;
        var session = moved.Session;
        if (_context.Talking.ContainsKey(session) || _context.Turned.ContainsKey(session) || _context.GoingOn.ContainsKey(session) || Boxed(session)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                if (await _context.ReadAgainAsync(session, CancellationToken.None).ConfigureAwait(false)
                        is not { Kind: "chat", Teammate: false, Interrupted: true, WordsWaiting: true } record)
                {
                    return;
                }

                // Words the shell cut off had written to its agent leave the record first, never sent again (trap 8).
                if (await UncertainWords.SettleOneAsync(_context.Service, _context.Events, record, CancellationToken.None).ConfigureAwait(false) > 0)
                {
                    record = await _context.ReadAgainAsync(session, CancellationToken.None).ConfigureAwait(false);
                }

                if (record is not { WordsWaiting: true } || GoOnMarks.Judged(record, _context.Marks.Read(session))) return;

                // No route started it, so its end is announced (MSG1c2).
                await GoOnAsync(session, onEnded: Announce).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Said in its conversation wherever it could be; the words wait on the record for the next come-up.
            }
        });
    }

    /// <summary>
    /// A conversation this runner took up by itself ended (MSG1c2): the session and the state its record took, as a start's
    /// <c>onEnded</c> is told, or the state it went back to where its agent would not resume it. No route started it, so no
    /// route hears its end: the shell announces it as the page's <c>SESSION_ENDED</c>.
    /// </summary>
    public event Action<string, string>? TakenUpEnded;

    /// <summary>A chat taken up by itself tells its end to whoever listens (MSG1c2), as the <c>onEnded</c> its run is handed.</summary>
    private Task Announce(string session, string state)
    {
        TakenUpEnded?.Invoke(session, state);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether an older build's box for the record's ended limit move or its hand-over is still in place, or a hand-over the store carries waits
    /// to be taken (FOUNDATION1f-vi part a, f-v part a; §3.3, §13.5): the record is the course's <c>Never</c> until the come-up's import has set
    /// the box aside and the hand-over has been taken, so no go-on, sweep or take-up resumes its old part meanwhile. The box's presence is the
    /// reading, which holds between come-ups and for a box held behind a standing move or for the start's confirmation alike.
    /// </summary>
    private bool Boxed(string session) => new LimitBoxes(_context.Home).Read(session) is not null || _handOver.Holds(session);

    /// <summary>
    /// The ended chats whose words wait on their record from while no runner heard them (MSG1c2): words kept while no shell
    /// ran, or before this runner listened, are taken up as words shown now would be, one chat at a time, each judged as
    /// MSG1c judges it and its end announced. A record whose every waiting word was already judged unable to go on (its
    /// marks) waits for a word said since, as a look leaves it (MSG1b). Called by the shell once, as its loop comes up.
    /// </summary>
    /// <returns>What became of each chat it took up, in the records' order; none where the service did not answer.</returns>
    public async Task<IReadOnlyList<ChatStart>> TakeUpAsync(CancellationToken ct = default)
    {
        // FOUNDATION1f-ii part a (the foundation design §3.3): a dead shell's conversation moves are taken up first, before anything else that
        // could act on their records, each ended by its phase so its records go on below by their own come-up; nothing is sent there.
        // FOUNDATION1f-vi part a (§3.3, §13.5): a box an older build wrote for an ended limit move is imported there first, once this start's
        // confirmation is settled, and ended by its phase like a dead shell's; until it is, the box in place is its record's `Never`, below.
        await _limit.Moves.ComeUpAsync(say: null, ct).ConfigureAwait(false);

        string json;
        IReadOnlyList<SessionRecord> records;
        try
        {
            json = await _context.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
            records = SessionRecords.Parse(json);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException
                                          or ObjectDisposedException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            // Nothing is taken up now: the words stay waiting as said, and the next word said to each tries again.
            return [];
        }

        var taken = new List<ChatStart>();
        foreach (var each in records)
        {
            if (_context.Closing || ct.IsCancellationRequested) break;

            // A helper's answer left on a conversation this runner holds open is said as its next turn (MIXAGENT2a-iii).
            if (each.Kind == "chat" && !each.Teammate && _context.Holds(each.Id) && ServiceClient.ReadRecord(json, each.Id) is { } open)
            {
                await _words.SayHelperWordsAsync(each.Id, open).ConfigureAwait(false);
                continue;
            }

            // Ask Daoris's conversations among them (ASKHIST1): each goes on in its own conversation as a repository's chat does.
            if (each.Kind != "chat" || each.Teammate || !Ended.Contains(each.State)) continue;

            // HANDWORDS1: one handed over or moved went on in another, which holds its tree: words left on it never restart it.
            if (WordsNever.ContinuedIn(records, each.Id) is not null) continue;

            // FOUNDATION1f-vi part a (§3.3, §13.5): an older build's box for an ended limit move still in place, held for the start's
            // confirmation or behind a standing move, is the record's `Never`: no go-on, sweep or take-up acts on it meanwhile.
            if (Boxed(each.Id)) continue;

            if (ServiceClient.ReadRecord(json, each.Id) is not { WordsWaiting: true } record) continue;

            // LIMITRESUME1c: one held by its limit is the look's limits stage's, which wakes it, its own account first, with Daoris's word.
            // FOUNDATION1g part c: held by the record's flag, its events read alone for a record an older build ended.
            if (LimitHeld.Waiting(_context.Events.After(each.Id, 0).Events, record.Limit) is not null)
            {
                Waits(each.Id);
                continue;
            }

            // COWORK1c (the message-urgency design §6, trap 8): words a shell before this one wrote to its agent leave the record first,
            // never sent again, since this may run before the loop's first look settles them.
            if (await UncertainWords.SettleOneAsync(_context.Service, _context.Events, record, ct).ConfigureAwait(false) > 0
                && await _context.ReadAgainAsync(each.Id, ct).ConfigureAwait(false) is not { WordsWaiting: true })
            {
                continue;
            }

            if (GoOnMarks.Judged(record, _context.Marks.Read(each.Id))) continue;

            try
            {
                taken.Add(await GoOnAsync(each.Id, onEnded: Announce, ct: ct).ConfigureAwait(false));
            }
            catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Said in its conversation wherever it could be, as a word taken up says it; the next chat is still taken up.
            }
        }

        // Every hand-over asked while no runner heard it (MIXAGENT1a-i) is taken too.
        taken.AddRange(await _handOver.TakeAllAsync(Announce, ct).ConfigureAwait(false));
        return taken;
    }

    /// <summary>
    /// A record event (MSG1d's shown words): the person's words with the reach <c>resume</c>, said to a session nothing here
    /// runs, are taken up at once where it is an ended chat of this machine's. A driven record is the planner's (MSG1b), and
    /// its record says so: reading it here costs one look and starts nothing.
    /// </summary>
    private void Heard(string session, SessionEvent e)
    {
        if (_context.Closing || e.Kind != SessionEventKind.User || e.Reaches != "resume") return;

        // A helper's answer for a conversation open here (MIXAGENT2a-iii): Daoris's words, never the person's, which a person's say
        // door never shows for a conversation that runs (its words reach it at its door). Said as its next turn.
        if (_context.Holds(session) && !string.Equals(e.Origin, "person", StringComparison.Ordinal))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _words.SayHelperWordsAsync(session).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // It waits on the record; the next word heard, or the next come-up, says it.
                }
            });
            return;
        }

        // FOUNDATION1o (§3.4 rule 4): a word shown while a conversation move stands is the move's, which its open carries. The come-up and the
        // sweep take up dead runs' moves first, and keep their own guards, so an older build's come-up reads as it did.
        if (_limit.Moves.Stands(session)) return;

        // 🔴 CHATGOONWAKE1: a run ends its record before it lets the conversation go, so a word shown between is owed to the run, which takes
        // it up as it lets go (LetGo). Owed before the hold is read, so a let-go between the two still finds it; whoever removes it wakes.
        _owed[session] = 0;
        if (_context.Holds(session) || !_owed.TryRemove(session, out _) || _context.GoingOn.ContainsKey(session)) return;
        Wake(session);
    }

    // CHATGOONWAKE1: the conversations a word was shown to while their run held them, each taken up as that run lets go.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _owed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The run that held the conversation has let it go, its last disposal done (CHATGOONWAKE1): a word shown to its ended record meanwhile,
    /// which <see cref="Heard"/> left owed, goes on now, as one shown a moment later would. Nothing is read where none was shown.
    /// </summary>
    internal void LetGo(string session)
    {
        if (_owed.TryRemove(session, out _) && !_context.Closing) Wake(session);
    }

    private void Wake(string session) => _ = Task.Run(async () =>
    {
        try
        {
            // No route started it, so its end is announced (MSG1c2).
            await GoOnAsync(session, onEnded: Announce).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Said in its conversation wherever it could be; a word said later tries again.
        }
    });

    /// <summary>
    /// The person let an answer the answers-alone bound held go on (COWORK1e, §8.3; <see cref="AnswersAloneWord.GoOn"/>, which already
    /// kept their press and took the mark away): a conversation open here says it as its next turn, and one that ended goes on with it,
    /// its end announced, as words shown to it would. On its own thread: the press is answered at once.
    /// </summary>
    public void AnswerGoesOn(string session)
    {
        if (_context.Closing) return;
        _ = Task.Run(async () =>
        {
            try
            {
                if (_context.Holds(session)) await _words.SayHelperWordsAsync(session).ConfigureAwait(false);
                else await GoOnAsync(session, onEnded: Announce).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Said in its conversation wherever it could be; the answer waits on the record, unmarked, for the next word or come-up.
            }
        });
    }

    // CHATFLOW1e: the answering turn's completion an ended chat goes on with another agent's findings under, by record, until its go-on takes it.
    internal readonly System.Collections.Concurrent.ConcurrentDictionary<string, Func<bool, Task>> _answering = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether an ended conversation can go on with another agent's findings (CHATFLOW1e, the chat-flow design §6.1 (f)), asked before they are
    /// kept on it: null where it can; else why it never can, as its go-on would judge it with them waiting (a teammate's, a stand-down, the
    /// adapter or account changed, its tree gone, no conversation kept, a door that cannot resume), which sends them to the person.
    /// </summary>
    internal ContinueReason? CannotGoOnWithFindings(PriorSession record)
    {
        AgentDoor? adapter = null;
        try
        {
            if (record.Adapter is { Length: > 0 } ranOn) adapter = _context.Harnesses.Adapters.Door(ranOn);
        }
        catch (DriverException)
        {
            // An adapter no longer here: its conversation cannot be resumed.
        }

        if (adapter is null) return ContinueWhy.Of(ContinueWhy.Refused);
        // Judged with the findings counted as the word that will wait, so an ended record with nothing waiting yet is not refused for it.
        var waiting = record with { Said = [.. record.Said ?? [], new SaidWordView(null, "", DateTimeOffset.UtcNow, [], Reopens: true) { By = "findings" }] };
        return Continuations.Judge(waiting, adapter.Id.Value, adapter.Can.Resumes, record.Profile, _context.Conversations.Read(record.Session));
    }

    /// <summary>
    /// An ended chat goes on with another agent's findings kept on its record (CHATFLOW1e, §6.1 (f)): its own go-on, the first turn's completion
    /// <paramref name="answering"/>, its end announced as a go-on no route started is. Why it did not, where it did not.
    /// </summary>
    internal async Task<ChatStart> GoOnWithFindingsAsync(string sessionId, Func<bool, Task> answering, CancellationToken ct)
    {
        _answering[sessionId] = answering;
        try
        {
            return await GoOnAsync(sessionId, onEnded: Announce, ct: ct).ConfigureAwait(false);
        }
        finally
        {
            _answering.TryRemove(sessionId, out _);
        }
    }

    /// <summary>
    /// Go on with an ended chat the person wrote to (MSG1c, D137 §2.2): its record reopened, its own conversation resumed by
    /// the id kept for it with every word waiting its first turn, or why not said in its conversation. Nothing for a record
    /// that is not an ended chat of this machine's with words waiting.
    /// </summary>
    /// <param name="config">The machine's choices; read from the home's <c>driver.json</c> where none is handed.</param>
    /// <param name="onEnded">Told when the conversation it goes on in ends, or when its agent would not resume it.</param>
    /// <returns>The session where it went on; else null and why.</returns>
    /// <param name="woke">
    /// The moment the limits stage found its own account ready again (LIMITRESUME1c, D206 §3.5): the run's first line says so. Null for
    /// every other go-on.
    /// </param>
    public async Task<ChatStart> GoOnAsync(
        string sessionId, DriverConfig? config = null, Func<string, string, Task>? onEnded = null, CancellationToken ct = default,
        DateTimeOffset? woke = null)
    {
        if (_context.Closing) return new(null, "the application is closing, so no conversation goes on now.");
        if (_context.Talking.ContainsKey(sessionId)) return new(null, $"conversation `{sessionId}` runs here: its words reach it at its door.");
        if (Boxed(sessionId)) return new(null, $"conversation `{sessionId}` has a move an older Daoris began that is not taken up yet, so it goes on once it is.");

        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_context.GoingOn.TryAdd(sessionId, going.Task)) return new(null, $"conversation `{sessionId}` is already going on.");
        ChatStart? started = null;
        try
        {
            DriverConfig choices;
            try
            {
                choices = config ?? _context.MachineConfig();
            }
            catch (Exception unreadable) when (unreadable is DriverException or System.Text.Json.JsonException or IOException
                                                   or UnauthorizedAccessException)
            {
                // The words wait, said: the next word tries again, and a torn driver.json is the person's to mend.
                return started = Held(sessionId, HeldWords.ConfigUnread(unreadable.Message));
            }

            return started = await GoingOnAsync(sessionId, choices, onEnded, woke, ct).ConfigureAwait(false);
        }
        finally
        {
            // 🔴 LIMITRESUME1c (D206 §3.5, B9): nothing went on, whatever held or refused it, so Daoris's limit word leaves its record by its
            // id, never left to be said alone; the next wake keeps one of its own. A run that started takes it with the words it went with.
            if (started is not { SessionId: not null } && woke is not null)
            {
                await LimitWordOffAsync(sessionId).ConfigureAwait(false);
            }

            _context.GoingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
        }
    }

    /// <summary>
    /// The limits stage's wake of an ended chat on its own account (LIMITRESUME1c, D206 §3.5): where its limit held it, Daoris's word is
    /// kept on its record through the say door first, under <see cref="DaorisKeys.LimitReset"/>, so it goes last after the person's
    /// words; then it goes on as any ended chat does, its own conversation resumed with the refused turns' words handed again first. An
    /// ended chat whose words waited for its account never met a limit, and goes on with its words alone.
    /// </summary>
    internal async Task<ChatStart> WakeAsync(
        string sessionId, bool limited, DateTimeOffset until, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        if (limited)
        {
            try
            {
                var said = await _context.Service
                    .SayAsync(sessionId, LimitResetWord.Text, ct: ct, from: DaorisWords.From, key: DaorisKeys.LimitReset(sessionId, until))
                    .ConfigureAwait(false);
                if (!said.Kept) return new(null, said.Message);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException)
            {
                return new(null, error.Message);
            }
        }

        return await GoOnAsync(sessionId, config, onEnded ?? Announce, ct, limited ? until : null).ConfigureAwait(false);
    }

    /// <summary>
    /// Daoris's limit word off an ended record by its id (LIMITRESUME1c, D206 §3.5, N24): the record's own session named as taking it, as
    /// the say door's taken door has it. The record as it stands after, or as it was where it could not be read.
    /// </summary>
    internal async Task<PriorSession?> LimitWordOffAsync(string sessionId, PriorSession? record = null)
    {
        record ??= await _context.ReadAgainAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        if (record is null) return null;
        List<string> ids = [.. LimitWordsOf(record).Select(word => word.Id!)];
        if (ids.Count == 0) return record;
        try
        {
            await _context.Service.TakenAsync(sessionId, ids, by: sessionId).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException or ObjectDisposedException)
        {
            // Left, it is uncertain at the next come-up or taken with the next wake's.
        }

        return WithoutLimitWords(record);
    }

    /// <summary>Daoris's limit words waiting on a record (<see cref="DaorisKeys.IsLimitReset"/>).</summary>
    private static IEnumerable<SaidWordView> LimitWordsOf(PriorSession record) =>
        record.Waiting.Where(word => word.Daoris && DaorisKeys.IsResume(word.Key) && word.Id is not null);

    /// <summary>The record as its words stand without Daoris's limit word: what a refusal marks, names and hands on.</summary>
    internal static PriorSession WithoutLimitWords(PriorSession record) =>
        record.Said is { } said && LimitWordsOf(record).Any()
            ? record with { Said = [.. said.Where(word => !(word.Daoris && DaorisKeys.IsResume(word.Key)))] }
            : record;

    /// <summary>
    /// The roster's reading of an ended conversation's own account (LIMITSWITCH2, D137's note of 2026-10-25): asked for by name as a chat's
    /// picker names one, and, where it cools while the person's words wait, the walk of its agent's accounts judged beside it, never counting
    /// a start: the ended limit move that goes on walks and counts once. A conversation the person picked an account for keeps waiting for
    /// that account (D205 §3.5(d)): its binding in the desktop store says so (FOUNDATION1a-ii), and the account rule holds it to that account as
    /// a pick; every other conversation, an agent's pick and one from before the pick was kept among them, walks its agent's accounts. Ask
    /// Daoris's room never asks it (<see cref="HelpGoingOnAsync"/>).
    /// </summary>
    internal Task<ResumeChoice> SwitchableAsync(PriorSession record, string adapter, DriverConfig config, string? workspace, CancellationToken ct) =>
        _context.Harnesses.ResumeAsync(
            adapter, config, workspace, record.Profile, StartKind.Conversation, ct: ct,
            line: LineKind.Conversation(carries: record.Waiting.Any(word => word.Persons)),
            picked: _context.Conversations.PickedOn(record.Session, record.Profile));

    /// <summary>
    /// What the limits stage's wake of an ended conversation whose words wait for a cooling account reads (LIMITSWITCH2): where another
    /// account of its agent is ready it goes on there now (<see cref="ResumeChoice.Switched"/>); else its wait is the earliest reset of any
    /// account of the walk, which <see cref="ResumeChoice.Selection"/> carries (D206 point 2). Null for Ask Daoris's room, a conversation
    /// its agent refused at the open and one with no checkout or agent here, whose wait is the own account's as before.
    /// </summary>
    internal async Task<ResumeChoice?> JudgeWaitAsync(PriorSession record, DriverConfig config, CancellationToken ct)
    {
        if (record.NotOffered || record.Adapter is not { Length: > 0 } ranOn
            || string.Equals(record.Repository, KnowledgeRoomIdentity.Repository, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        AgentDoor adapter;
        try
        {
            adapter = _context.Harnesses.Adapters.Door(ranOn);
        }
        catch (DriverException)
        {
            return null;
        }

        var registry = await _context.Service.RegistryAsync(ct).ConfigureAwait(false);
        var known = registry.FirstOrDefault(r => string.Equals(r.Repository, record.Repository, StringComparison.OrdinalIgnoreCase));
        return known is null ? null : await SwitchableAsync(record, adapter.Id.Value, config, known.Workspace, ct).ConfigureAwait(false);
    }

    private Task<ChatStart> GoingOnAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, DateTimeOffset? woke, CancellationToken ct) =>
        new GoingOnRun(wiring, this).GoingOnAsync(sessionId, config, onEnded, woke, ct);


    /// <summary>
    /// What the ledger's note gains as an ended chat goes on (MSG1b), by what waits: another agent's claims alone are never "your
    /// words" (XAGENT1e, MIXAGENT2a-iii), as the driven path's note is (<c>DriverContinuations</c>).
    /// </summary>
    internal static Noted GoingOnNote(IReadOnlyList<SaidWordView> waiting)
    {
        var persons = waiting.Any(word => word.Persons);
        if (persons) return Continuations.GoingOnNoted;
        return waiting.Any(word => word.By is not null) ? Continuations.GoingOnWithFindingsNoted
            : waiting.Any(word => word.Helper is not null) ? Continuations.GoingOnWithHelperNoted
            // COWORK1b: Daoris's own words alone.
            : waiting.Any(word => word.Daoris) ? Continuations.GoingOnWithDaorisNoted
            : Continuations.GoingOnNoted;
    }

    /// <summary>
    /// The resumed conversation's first line (D131 §1), by what it goes on with: the same arguments the driven path passes
    /// (<c>DriverContinuations</c>), so a helper's answer is said as that agent's claim and Daoris's own words as Daoris's, never
    /// as "your words" (MIXAGENT2a-iii, COWORK1b).
    /// </summary>
    internal static Noted OpeningNote(IReadOnlyList<SaidWordView> words, string adapter, string? now, string? then) =>
        Continuations.OpeningNoted(
            adapter, now, then, answer: false, findings: words.Any(word => word.By is not null),
            persons: words.Any(word => word.Persons), helper: words.Any(word => word.Helper is not null),
            daoris: words.Any(word => word.Daoris));

    /// <summary>
    /// The words went to the conversation (MSG1c): off its record by their ids (MSG1a's taken door), so the service keeps
    /// them on the ask as <c>reopened</c>, and the machine log's <c>session.reopened</c> says it resumed. A refusal costs a
    /// line in its conversation, never the conversation.
    /// </summary>
    public async Task WentOnAsync(string sessionId, GoingOn goOn, string adapter)
    {
        var ids = goOn.Ask.Ids;
        if (ids.Count > 0)
        {
            string? failed;
            try
            {
                var (taken, message) = await _context.Service.TakenAsync(sessionId, ids, by: null).ConfigureAwait(false);
                failed = taken ? null : message;
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException)
            {
                failed = error.Message;
            }

            if (failed is not null) _context.Note(sessionId, $"— your words could not be taken off its record: {failed}");
        }

        _context.Service.AccountSaid(Continuations.Reopened(
            sessionId, adapter, goOn.Record.State, why: null, _context.Events.DoorOf(sessionId, ids), kind: "chat"));
    }

    /// <summary>
    /// The person's words of the turns its account's limit refused (LIMITRESUME1c, <see cref="LimitHeld"/>), each with its kept files:
    /// handed again first where its own conversation goes on. None where its limit holds it no more.
    /// </summary>
    internal IReadOnlyList<ChatMessage> Resent(string sessionId, LimitHeld? held) =>
        held is null
            ? []
            : [.. held.Words.Select(word => new ChatMessage(word.Text!, ChatFiles.Kept(_context.Home, sessionId, word.Files ?? [])))];

    private GoOnRefusals Refusals => new(wiring, this);

    /// <summary>The agent would not resume the conversation (MSG1c): the record goes back as it ended and its words wait, marked.</summary>
    public Task WentBackAsync(string sessionId, GoingOn goOn, ContinueReason why, string adapter, Func<string, string, Task>? onEnded) =>
        Refusals.WentBackAsync(sessionId, goOn, why, adapter, onEnded);

    /// <summary>Where a chat's words cannot go on in it (MSG1c, D137 §2.2): they stay waiting, marked, and its conversation says why.</summary>
    internal ChatStart CannotGoOn(PriorSession record, ContinueReason why, string adapter, bool said = false) => Refusals.CannotGoOn(record, why, adapter, said);

    /// <summary>A conversation whose own conversation is proven lost goes on in a new part handed Daoris's brief (SESSCTX1c part B).</summary>
    internal Task<ChatStart> LostAsync(
        PriorSession record, ContinueReason why, string adapter, DriverConfig choices, Func<string, string, Task>? onEnded, bool held, CancellationToken ct) =>
        Refusals.LostAsync(record, why, adapter, choices, onEnded, held, ct);


    /// <summary>
    /// What holds a chat's words rather than refusing them (MSG1c): said in its conversation, the words left waiting and
    /// unmarked, so the next word said to it tries again.
    /// </summary>
    internal ChatStart Held(string sessionId, (string Why, Noted Line) held)
    {
        _context.Note(sessionId, held.Line);
        return new(null, held.Why);
    }

    /// <summary>
    /// Every word waiting was taken back between the look and the ledger's move (SAIDBACK1d): nothing goes on, so the record is
    /// put back as it ended, not left working for a run that will never start, and its conversation says so. Where the ledger
    /// will not move it back, the refusal is said and the next look reads it as it now is.
    /// </summary>
    internal async Task<ChatStart> TakenBackAsync(PriorSession record, CancellationToken ct)
    {
        var line = new Noted(TakenBackBeforeGoingOn, []);
        // As it ended (SAIDBACK1g): its own note, parts and flags, which the move to working cleared; the line is said in the
        // conversation beside, never in place of what ended it.
        var (state, note, interrupted, limit) = Continuations.PutBack(record);
        try
        {
            await _context.Service.AdvanceAsync(record.Session, state, note, ct: ct, interrupted: interrupted, limit: limit).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            _context.Note(record.Session, $"{TakenBackBeforeGoingOn} {refused.Message}");
            return new(null, refused.Message);
        }

        _context.Note(record.Session, line);
        return new(null, TakenBackBeforeGoingOn);
    }

    /// <summary>The line a conversation keeps where the words it was to go on with were all taken back first (SAIDBACK1d).</summary>
    public const string TakenBackBeforeGoingOn = "— the words waiting on it were taken back before it went on, so it stays as it ended.";

    /// <summary>
    /// A conversation's own tree brought up to date where the line holds some of its commits (LANDSYNC2b, as the driver's start asks
    /// <see cref="SessionTrees.StartInSyncAsync"/>): what it is told, or null. Git or a disk that fails here is no reason to hold its
    /// words: it goes on in the tree as it stands, told nothing.
    /// </summary>
    internal async Task<string?> StartInSyncAsync(string tree, CancellationToken ct)
    {
        try
        {
            return (await new SessionTrees(_context.Home).StartInSyncAsync(tree, ct).ConfigureAwait(false)).Told;
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Whether two paths name one folder, whatever their separators or a trailing one.</summary>
    internal static bool SamePath(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
