namespace Daoris.Driver;

/// <summary>
/// An ended chat going on (MSG1c): its record as it ended, what its resumed run carries and learns (<see cref="ResumeAsk"/>),
/// and whether its words went, which on the native door needs its line on stdin and its harness naming the conversation. The
/// runner's run takes one in and its watch tells it when the words went (<see cref="ChatGoOn.WentOnAsync"/>).
/// </summary>
internal sealed class GoingOn(PriorSession record, ResumeAsk ask)
{
    private readonly object _gate = new();
    private bool _sent;
    private bool _taken;

    public PriorSession Record => record;

    public ResumeAsk Ask => ask;

    /// <summary>The words are on the wire. True once, where they went now: at once, or where the harness already named it.</summary>
    /// <param name="named">Whether they went only once the harness has named its conversation too (the native door).</param>
    public bool Sent(bool named)
    {
        lock (_gate)
        {
            _sent = true;
            return Take(named);
        }
    }

    /// <summary>The native door's harness named its conversation. True once, where the words went now.</summary>
    public bool Named()
    {
        lock (_gate)
        {
            ask.Named = true;
            return Take(named: true);
        }
    }

    private bool Take(bool named)
    {
        if (_taken || !_sent || (named && !ask.Named)) return false;
        _taken = true;
        ask.Prompted = true;
        return true;
    }
}

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
/// <para><b>It holds the runner</b> as <see cref="ChatHandOver"/> does: a conversation going on runs through the runner's own
/// run, in the place the runner makes. It hears the record's events from its construction until <see cref="Stop"/>, which
/// <see cref="ChatRunner.StopAll"/> calls first.</para>
/// </remarks>
internal sealed class ChatGoOn
{
    /// <summary>The endings a chat goes on from: every one but a stand-down, which never goes on (D137 §2.2).</summary>
    private static readonly HashSet<string> Ended = new(StringComparer.OrdinalIgnoreCase) { "completed", "declined", "failed", "stopped" };

    private readonly ChatContext _context;
    private readonly ChatRunner _runner;
    private readonly ChatHandOver _handOver;
    private readonly ChatHelp _help;
    private readonly ChatHelperWords _words;

    public ChatGoOn(ChatContext context, ChatRunner runner, ChatHandOver handOver, ChatHelp help, ChatHelperWords words)
    {
        _context = context;
        _runner = runner;
        _handOver = handOver;
        _help = help;
        _words = words;

        // The person's words to an ended chat are kept on its record and shown in its conversation by whichever door they
        // were said at (MSG1d); hearing them there is what takes them up at once (MSG1c, D137 §2.2).
        _context.Events.Evented += Heard;
    }

    /// <summary>The runner is closing: the record's events are heard no more.</summary>
    public void Stop() => _context.Events.Evented -= Heard;

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
    /// The ended chats whose words wait on their record from while no runner heard them (MSG1c2): words kept while no shell
    /// ran, or before this runner listened, are taken up as words shown now would be, one chat at a time, each judged as
    /// MSG1c judges it and its end announced. A record whose every waiting word was already judged unable to go on (its
    /// marks) waits for a word said since, as a look leaves it (MSG1b). Called by the shell once, as its loop comes up.
    /// </summary>
    /// <returns>What became of each chat it took up, in the records' order; none where the service did not answer.</returns>
    public async Task<IReadOnlyList<ChatStart>> TakeUpAsync(CancellationToken ct = default)
    {
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

            if (ServiceClient.ReadRecord(json, each.Id) is not { WordsWaiting: true } record) continue;
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

        if (_context.Talking.ContainsKey(session) || _context.Turned.ContainsKey(session) || _context.GoingOn.ContainsKey(session)) return;
        _ = Task.Run(async () =>
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
    }

    /// <summary>
    /// Go on with an ended chat the person wrote to (MSG1c, D137 §2.2): its record reopened, its own conversation resumed by
    /// the id kept for it with every word waiting its first turn, or why not said in its conversation. Nothing for a record
    /// that is not an ended chat of this machine's with words waiting.
    /// </summary>
    /// <param name="config">The machine's choices; read from the home's <c>driver.json</c> where none is handed.</param>
    /// <param name="onEnded">Told when the conversation it goes on in ends, or when its agent would not resume it.</param>
    /// <returns>The session where it went on; else null and why.</returns>
    public async Task<ChatStart> GoOnAsync(
        string sessionId, DriverConfig? config = null, Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        if (_context.Closing) return new(null, "the application is closing, so no conversation goes on now.");
        if (_context.Talking.ContainsKey(sessionId)) return new(null, $"conversation `{sessionId}` runs here: its words reach it at its door.");

        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_context.GoingOn.TryAdd(sessionId, going.Task)) return new(null, $"conversation `{sessionId}` is already going on.");
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
                return Held(sessionId, HeldWords.ConfigUnread(unreadable.Message));
            }

            return await GoingOnAsync(sessionId, choices, onEnded, ct).ConfigureAwait(false);
        }
        finally
        {
            _context.GoingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
        }
    }

    private async Task<ChatStart> GoingOnAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        // Only an ended chat of this machine's with words waiting: a driven record goes on through the planner (MSG1b), and a
        // live one hears words at its door. Ask Daoris's conversation goes on too, in its room (ASKHIST1).
        var json = await _context.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var record = ServiceClient.ReadRecord(json, sessionId);
        if (record is not { Kind: "chat", WordsWaiting: true } || record.Teammate
            || !(Ended.Contains(record.State) || string.Equals(record.State, "stood-down", StringComparison.OrdinalIgnoreCase)))
        {
            return new(null, $"session `{sessionId}` is no ended conversation of this machine's with words waiting.");
        }

        // 🔴 HANDWORDS1 (D163 point 1, D165 point 3): one handed over or moved went on in another, which holds its tree now and takes
        // its words. Restarted, it would open a second conversation in that tree: it stays as it ended, its words unmarked.
        if (WordsNever.ContinuedIn(json, sessionId) is { } next)
        {
            return new(null, $"conversation `{sessionId}` went on in conversation `{next}`, which takes its words.");
        }

        // The person wrote to it, so it stays in view whatever comes of it (D137 §2.3), and an earlier mark of words it could
        // not take is past: these are taken up now.
        new SessionArchive(_context.Home).Unarchive([sessionId]);
        _context.Marks.Clear(sessionId);

        // Its conversation is the adapter's that opened it, so it goes on on that one or not at all.
        var ranOn = record.Adapter ?? config.Adapter;
        ISessionAdapter? adapter = null;
        try
        {
            adapter = _context.Harnesses.Adapters.Resolve(ranOn);
        }
        catch (DriverException)
        {
            // An adapter no longer here (a plugin's, removed): its conversation cannot be resumed.
        }

        // What can be known before anything is asked of the toolchain, on the account it ran on: the nevers, no words, its
        // tree, a kept id and a door that resumes. Where the selection lands on another account is judged after it.
        var kept = _context.Conversations.Read(sessionId);
        var why = adapter is null
            ? ContinueWhy.Of(ContinueWhy.Refused)
            : Continuations.Judge(record, adapter.Name, adapter.Resumes, record.Profile, kept);
        if (why is not null || adapter is null || kept is null) return CannotGoOn(record, why ?? ContinueWhy.Of(ContinueWhy.Unkept), ranOn);

        if (string.Equals(record.Repository, HelpRoom.Repository, StringComparison.OrdinalIgnoreCase))
        {
            return await HelpGoingOnAsync(record, adapter, kept, config, onEnded, ct).ConfigureAwait(false);
        }

        var registry = await _context.Service.RegistryAsync(ct).ConfigureAwait(false);
        var known = registry
            .FirstOrDefault(r => string.Equals(r.Repository, record.Repository, StringComparison.OrdinalIgnoreCase));
        if (known is null || string.IsNullOrWhiteSpace(known.Root))
        {
            return Held(sessionId, HeldWords.NoCheckout(record.Repository));
        }

        // The account it ran on, asked for by name, as a chat's picker names one (D137 §2.2, MSG1g): its conversation lives in
        // that account's home. Cooling, the words wait for its reset, said with the door out of the wait; unable to run there
        // at all, it cannot go on, at once and saying why, since nothing carries a chat's words on by itself; anything else the
        // selection will not start holds the words.
        var resume = await _context.Harnesses
            .ResumeAsync(adapter.Name, config, known.Workspace, record.Profile, StartKind.Conversation, walks: false, ct: ct)
            .ConfigureAwait(false);
        if (resume.Waits)
        {
            _context.Note(sessionId, ResumeWords.NotYetOn(resume, ResumeWords.ChatDoorOf(sessionId)));
            return new(null, resume.Selection.Refusal);
        }

        if (resume.Elsewhere is { } elsewhere) return CannotGoOn(record, elsewhere, adapter.Name);
        var selection = resume.Selection;
        if (!selection.Allowed) return Held(sessionId, HeldWords.Refused(selection));
        if (Continuations.Judge(record, adapter.Name, adapter.Resumes, selection.Profile, kept) is { } account)
        {
            return CannotGoOn(record, account, adapter.Name);
        }

        // A tree of its own is taken again: its repository's trees are held until the ledger holds this one once more (LEFT2),
        // so bringing the repository up to date does not rebase it under the conversation.
        var own = !SamePath(record.Tree!, known.Root!);

        // LANDSYNC2b (D87's 2026-10-15 amendment point 7): a conversation going on in its own tree starts from the line where the line
        // came to hold some of its commits, as a driven session's does (LANDSYNC2), and is told so after the words. Asked before this
        // start's own hold below, since bringing a tree up to date holds the repository's trees alone. The person's checkout is never
        // moved: a conversation in it asks nothing.
        var inSync = own ? await StartInSyncAsync(record.Tree!, ct).ConfigureAwait(false) : null;
        using var starting = own ? TreeLock.TryStarting(_context.Home, known.Workspace, known.Repository) : null;
        if (own && starting is null) return Held(sessionId, HeldWords.Replaying(known.Repository));

        // 🔴 The ledger's one move out of an ended state, with the person's words waiting (MSG1a, D137 §2.3), before anything
        // is spawned: it holds the tree again, and refuses where another session holds it (D51).
        try
        {
            await _context.Service.AdvanceAsync(sessionId, "working", GoingOnNote(record.Waiting), ct: ct).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            return Held(sessionId, HeldWords.Program(refused.Message));
        }

        starting?.Dispose();

        // Every word waiting once it moved: none can be kept on a record that works (the say door answers `running`), so the
        // list read now is whole, a word said while it was judged included.
        var now = await _context.ReadAgainAsync(sessionId, ct).ConfigureAwait(false);
        var words = Continuations.HandedAfterMove(record.Waiting, now);
        if (words.Count == 0) return await TakenBackAsync(record, ct).ConfigureAwait(false);
        // Each word's files where they were kept as it was said (MSG1d3, D137 §2.4), handed with the words; the conversation's
        // rules already read its own files folder (CONV4c). What its start did with its tree against the line after them (LANDSYNC2b).
        var ask = new ResumeAsk(
            kept.Conversation, words,
            OpeningNote(words, adapter.Name, selection.Version, record.HarnessVersion),
            Continuations.AfterWords(inSync, after: null),
            files: word => ChatFiles.Kept(_context.Home, sessionId, word.Files));

        return await _runner.RunAsync(
            sessionId,
            words.Any(word => word.Persons)
                ? $"conversation `{sessionId}` goes on with your words in its own conversation."
                : words.All(word => word.Daoris)
                    // COWORK1b: Daoris's own words alone are no agent's answer.
                    ? $"conversation `{sessionId}` goes on with Daoris's words in its own conversation."
                    : $"conversation `{sessionId}` goes on with another agent's answer in its own conversation.",
            adapter, selection, config,
            _runner.RepositoryPlace(config, registry, known, record.Repository!, record.Tree!), onEnded, ct,
            new GoingOn(record, ask)).ConfigureAwait(false);
    }

    /// <summary>
    /// Ask Daoris's ended conversation goes on with the person's words (ASKHIST1): judged as a chat's is up to its account, then
    /// the room's one running conversation set aside, the ledger's move, and its own conversation resumed in the room. It reads
    /// the help chat's set-aside and room; the help chat calls nothing here.
    /// </summary>
    private async Task<ChatStart> HelpGoingOnAsync(
        PriorSession record, ISessionAdapter adapter, HarnessConversation kept, DriverConfig config,
        Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        var sessionId = record.Session;

        // Its account by name, as a chat's, among the machine's accounts (D89): going on keeps the account it ran on.
        var resume = await _context.Harnesses
            .ResumeAsync(adapter.Name, config, workspace: null, record.Profile, StartKind.Conversation, walks: false, ct: ct)
            .ConfigureAwait(false);
        if (resume.Waits)
        {
            _context.Note(sessionId, ResumeWords.NotYetOn(resume, ResumeWords.HelpDoorOf()));
            return new(null, resume.Selection.Refusal ?? ResumeWords.HelpDoor);
        }

        if (resume.Elsewhere is { } elsewhere) return CannotGoOn(record, elsewhere, adapter.Name);
        var selection = resume.Selection;
        if (!selection.Allowed) return Held(sessionId, HeldWords.Refused(selection));
        if (Continuations.Judge(record, adapter.Name, adapter.Resumes, selection.Profile, kept) is { } account)
        {
            return CannotGoOn(record, account, adapter.Name);
        }

        // Its own workspace's running one alone (ASKSCOPE1a): another workspace's runs on beside it.
        if (await _help.SetAsideHelpAsync(sessionId, record.Workspace, ct).ConfigureAwait(false) is { } answering) return Held(sessionId, HeldWords.Answering(answering));

        // 🔴 The ledger's one move out of an ended state (MSG1a, D137 §2.3), before anything is spawned: it holds the room again,
        // and refuses where another conversation holds it, one another process here runs among them.
        try
        {
            await _context.Service.AdvanceAsync(sessionId, "working", Continuations.GoingOnNoted, ct: ct).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            return Held(sessionId, HeldWords.Program(refused.Message));
        }

        var now = await _context.ReadAgainAsync(sessionId, ct).ConfigureAwait(false);
        var words = Continuations.HandedAfterMove(record.Waiting, now);
        if (words.Count == 0) return await TakenBackAsync(record, ct).ConfigureAwait(false);
        var ask = new ResumeAsk(
            kept.Conversation, words,
            Continuations.OpeningNoted(adapter.Name, selection.Version, record.HarnessVersion, answer: false),
            files: word => ChatFiles.Kept(_context.Home, sessionId, word.Files));

        // ASKSCOPE1b: the room it ran in, its workspace's, or the machine's where it started there before each workspace had its own.
        var (room, reads) = await _help.HelpRoomNowAsync(sessionId, HelpRoom.GoesOnIn(_context.Home, record.Workspace, record.Tree), ct)
            .ConfigureAwait(false);
        return await _runner.RunAsync(
            sessionId, $"Ask Daoris `{sessionId}` goes on with your words in its own conversation.", adapter, selection, config,
            _runner.HelpPlace(room, reads, record.Workspace), onEnded, ct, new GoingOn(record, ask)).ConfigureAwait(false);
    }

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
    /// The agent would not resume the conversation (MSG1c): nothing went, so the record goes back to how it ended, its flags
    /// kept and its note saying why, and its words stay waiting, marked, as where it could not go on.
    /// </summary>
    public async Task WentBackAsync(
        string sessionId, GoingOn goOn, ContinueReason why, string adapter, Func<string, string, Task>? onEnded)
    {
        var record = goOn.Record;
        try
        {
            await _context.Service.AdvanceAsync(
                    sessionId, record.State, Continuations.CannotNote(record, why), interrupted: record.Interrupted,
                    limit: record.Limit)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best-effort, as every conclusion here is: the host may be gone on the same shutdown.
        }

        CannotGoOn(record, why, adapter);
        if (onEnded is not null)
        {
            try
            {
                await onEnded(sessionId, record.State).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A listener's failure is its own; the record is already written.
            }
        }
    }

    /// <summary>
    /// Where a chat's words cannot go on in it (MSG1c, D137 §2.2): nothing carries a chat's words on by itself, so they stay
    /// waiting as said and are marked by their ids; its conversation says why with their ids and the reason's code, which the
    /// page words with its one press (MSG1f); and the machine log says it did not resume.
    /// </summary>
    private ChatStart CannotGoOn(PriorSession record, ContinueReason why, string adapter)
    {
        var words = record.Waiting.Select(word => word.Id).OfType<string>().ToList();
        _context.Marks.Mark(record.Session, words, why, DateTimeOffset.UtcNow);
        var note = Continuations.Cannot(words, why);
        _context.Output?.Append(record.Session, note.Text!);
        _context.Events.Keep(record.Session, note, say: null);
        _context.Service.AccountSaid(Continuations.Reopened(
            record.Session, adapter, record.State, why, _context.Events.DoorOf(record.Session, words), kind: "chat"));
        return new(null, $"it cannot go on in this conversation, because {why.Said}.");
    }

    /// <summary>
    /// What holds a chat's words rather than refusing them (MSG1c): said in its conversation, the words left waiting and
    /// unmarked, so the next word said to it tries again.
    /// </summary>
    private ChatStart Held(string sessionId, (string Why, Noted Line) held)
    {
        _context.Note(sessionId, held.Line);
        return new(null, held.Why);
    }

    /// <summary>
    /// Every word waiting was taken back between the look and the ledger's move (SAIDBACK1d): nothing goes on, so the record is
    /// put back as it ended, not left working for a run that will never start, and its conversation says so. Where the ledger
    /// will not move it back, the refusal is said and the next look reads it as it now is.
    /// </summary>
    private async Task<ChatStart> TakenBackAsync(PriorSession record, CancellationToken ct)
    {
        var line = new Noted(TakenBackBeforeGoingOn, []);
        // As it ended (SAIDBACK1g): its own note, parts and flags, which the move to working cleared; the line is said in the
        // conversation beside, never in place of what ended it.
        var (state, note, interrupted, limit) = Continuations.PutBack(record, why: null);
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
    private async Task<string?> StartInSyncAsync(string tree, CancellationToken ct)
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
    private static bool SamePath(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
