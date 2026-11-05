using static Daoris.Driver.ChatGoOn;

namespace Daoris.Driver;

/// <summary>One ended conversation's go-on, judged and started: a chat's in its repository, Ask Daoris's in its room. Built per call over the executor.</summary>
internal sealed class GoingOnRun(IExecutorWiring wiring, ChatGoOn owner)
{
    private ChatContext _context => wiring.Context;
    private IChatRunning _runner => wiring;
    private IChatHelping _help => wiring.Of<IChatHelping>();
    private BriefedMove _move => wiring.Of<BriefedMove>();
    private LimitMove _limit => wiring.Of<LimitMove>();

    internal async Task<ChatStart> GoingOnAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, DateTimeOffset? woke, CancellationToken ct)
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

        // 🔴 Answers alone that would take its line on past its number in a row with nobody between wait for the person, marked before any
        // mark is cleared; a word of theirs, or their *Go on with this answer*, lets them go.
        var lines = ServiceClient.ReadRecords(json);
        var hold = AnswersAloneHold.For(_context.Events, _context.Marks, _context.Home);
        if (hold.Holds(record, lines, DateTimeOffset.UtcNow))
        {
            owner.Decide(record, alone: hold.HeldAt(record.Session, lines));
            return new(null, $"conversation `{sessionId}`'s answer waits for you: it would take its work on a third time with nobody between.");
        }

        // The person wrote to it, so it stays in view whatever comes of it (D137 §2.3), and an earlier mark of words it could
        // not take is past: these are taken up now.
        new SessionArchive(_context.Home).Unarchive([sessionId]);
        _context.Marks.Clear(sessionId);

        // 🔴 MODELROLE1d2 (D205 §3.5(b)): one its agent refused at the open began no conversation there, so it is never resumed: its go-on
        // is a fresh start of its actor, its words the first message, a pick trying only its own agent's entries on its own account, and a
        // refused continuation handed the brief of its line first. The come-up's go-on takes the same path.
        if (record.NotOffered)
        {
            var fresh = await _move.NotOfferedAsync(record, json, config, onEnded, ct).ConfigureAwait(false);
            if (fresh.SessionId is null) _context.Note(sessionId, LimitNotes.NotMoved(fresh.Message));
            return new(fresh.SessionId, fresh.Message);
        }

        // Its conversation is the adapter's that opened it, so it goes on on that one or not at all. A record naming none (one from before
        // the adapter was kept) goes on with its role's choice, Chat's order, never the machine's `adapter` key (FOUNDATION1j part iii,
        // the foundation design §2.4's first row); Chat off, or its entry unread, holds the words with the sentence.
        string ranOn;
        try
        {
            // FOUNDATION1x: its binding's role first, as a quest's go-on reads it (§2.4).
            ranOn = record.Adapter is { Length: > 0 } opened
                ? opened
                : StartPlan.ContinuedOn(
                    record, config, "chat", _context.Conversations.RoleOf(sessionId)?.Role, RoleCatalog.ReadHome(_context.Home), new RolePlace());
        }
        catch (DriverException unchosen)
        {
            return owner.Held(sessionId, HeldWords.Program(unchosen.Message));
        }

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
        if (why is not null || adapter is null || kept is null)
        {
            var reason = why ?? ContinueWhy.Of(ContinueWhy.Unkept);
            // 🔴 SESSCTX1c part B (D189 point 3): no id kept, or a door that cannot resume, proves its conversation lost, so it goes on by
            // itself in a new part handed the brief; anything else waits for the person, the brief ready. A lost conversation is decided by
            // the move it goes on in (ConversationLost, which knows the walk's answer); what cannot go on is the course's hold here.
            if (adapter is not null && reason.Lost) return await owner.LostAsync(record, reason, adapter.Name, config, onEnded, held: true, ct).ConfigureAwait(false);
            owner.Decide(record, judged: reason.Code, agentGone: adapter is null);
            return owner.CannotGoOn(record, reason, ranOn);
        }

        if (string.Equals(record.Repository, KnowledgeRoomIdentity.Repository, StringComparison.OrdinalIgnoreCase))
        {
            return await HelpGoingOnAsync(record, adapter, kept, config, onEnded, woke, ct).ConfigureAwait(false);
        }

        var registry = await _context.Service.RegistryAsync(ct).ConfigureAwait(false);
        var known = registry
            .FirstOrDefault(r => string.Equals(r.Repository, record.Repository, StringComparison.OrdinalIgnoreCase));
        if (known is null || string.IsNullOrWhiteSpace(known.Root))
        {
            return owner.Held(sessionId, HeldWords.NoCheckout(record.Repository));
        }

        // The account it ran on, asked for by name, as a chat's picker names one (D137 §2.2, MSG1g): its conversation lives in
        // that account's home. Cooling, the words wait for its reset, said with the door out of the wait; unable to run there
        // at all, it cannot go on, at once and saying why, since nothing carries a chat's words on by itself; anything else the
        // selection will not start holds the words.
        // LIMITSWITCH2 (D137's note of 2026-10-25): cooling, and another account of its agent ready, the person's words go on now in a new part
        // there, through the ended limit move's body, which walks once and opens on what it judged; otherwise they wait for the earliest
        // reset of any account of its agent, never its own alone.
        var resume = await owner.SwitchableAsync(record, adapter.Name, config, known.Workspace, ct).ConfigureAwait(false);
        var course = owner.Decide(record, answer: resume.Answer);
        if (resume.Switched || course is LineCourse.Move { Why: CourseFromWhy.Retry })
        {
            StartedFrom? moved;
            try
            {
                moved = await _limit.MoveEndedAsync(sessionId, lost: null, config, onEnded, ct, holding: true, switching: true).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or IOException or System.Text.Json.JsonException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                moved = new StartedFrom(null, false, null, error.Message);
            }

            // Where nothing opened its words stay on the record, said why where the move said it, for the person's next word.
            return moved is { SessionId: { } onTo } ? new(onTo, moved.Message) : new(null, moved?.Message ?? $"conversation `{sessionId}` did not go on in a new part now.");
        }

        if (resume.Waits || course is LineCourse.Wait)
        {
            _context.Note(sessionId, ResumeWords.NotYetOn(resume, ResumeWords.ChatDoorOf(sessionId)));
            // LIMITRESUME1c (D206 §3.5, D137 point 7): the look's limits stage wakes it at the reset.
            owner.Waits(sessionId);
            return new(null, resume.Selection.Refusal);
        }

        if (resume.Elsewhere is { } elsewhere) return owner.CannotGoOn(record, elsewhere, adapter.Name);
        var selection = resume.Selection;
        if (!selection.Allowed) return owner.Held(sessionId, HeldWords.Refused(selection));
        if (Continuations.Judge(record, adapter.Name, adapter.Resumes, selection.Profile, kept) is { } account)
        {
            return owner.CannotGoOn(record, account, adapter.Name);
        }

        // LIMITRESUME1c (D206 §3.5): the words its account's limit refused, handed again first, before the words that waited.
        var limited = LimitHeld.Waiting(_context.Events.After(sessionId, 0).Events, record.Limit);

        // A tree of its own is taken again: its repository's trees are held until the ledger holds this one once more (LEFT2),
        // so bringing the repository up to date does not rebase it under the conversation.
        var own = !SamePath(record.Tree!, known.Root!);

        // LANDSYNC2b (D87's 2026-10-15 amendment point 7): a conversation going on in its own tree starts from the line where the line
        // came to hold some of its commits, as a driven session's does (LANDSYNC2), and is told so after the words. Asked before this
        // start's own hold below, since bringing a tree up to date holds the repository's trees alone. The person's checkout is never
        // moved: a conversation in it asks nothing.
        var inSync = own ? await owner.StartInSyncAsync(record.Tree!, ct).ConfigureAwait(false) : null;
        // GITLOCK1: one in the checkout itself holds it too, until the ledger holds it again below.
        using var starting = TreeLock.TryStarting(_context.Home, known.Root!);
        if (starting is null) return owner.Held(sessionId, HeldWords.Holding(known.Repository, TreeLock.HolderOf(_context.Home, known.Root!)));

        // 🔴 The ledger's one move out of an ended state, with the person's words waiting (MSG1a, D137 §2.3), before anything
        // is spawned: it holds the tree again, and refuses where another session holds it (D51).
        try
        {
            await _context.Service.AdvanceAsync(sessionId, "working", GoingOnNote(record.Waiting), ct: ct).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            return owner.Held(sessionId, HeldWords.Program(refused.Message));
        }

        starting?.Dispose();
        // COWORKVIEW1a: its process's token and `start` as its move returns, let go wherever it never reaches the runner.
        var proc = ChildLineage.Opened(_context.Home, sessionId);
        var handed = false;
        using var unhanded = new Disposer(() =>
        {
            if (!handed) LineageTokens.Current.Unspawned(proc);
        });

        // Every word waiting once it moved: none can be kept on a record that works (the say door answers `running`), so the
        // list read now is whole, a word said while it was judged included.
        var now = await _context.ReadAgainAsync(sessionId, ct).ConfigureAwait(false);
        var words = Continuations.HandedAfterMove(record.Waiting, now);
        if (words.Count == 0) return await owner.TakenBackAsync(record, ct).ConfigureAwait(false);
        // Each word's files where they were kept as it was said (MSG1d3, D137 §2.4), handed with the words; the conversation's
        // rules already read its own files folder (CONV4c). What its start did with its tree against the line after them (LANDSYNC2b).
        // CONTEXTDRIVE1b (§3 point 2): and Daoris's count for its line, where one moved or stands near what Daoris does on it.
        var counted = CountTexts.GoOn(LineCountsRead.FactsOf(_context.Home, sessionId, json).Counts);
        var ask = new ResumeAsk(
            kept.Conversation, words,
            woke is { } until ? LimitNotes.Resumed(until) : OpeningNote(words, adapter.Name, selection.Version, record.HarnessVersion),
            Continuations.AfterWords(inSync, counted),
            files: word => ChatFiles.Kept(_context.Home, sessionId, word.Files))
        {
            Resent = owner.Resent(sessionId, limited),
        };
        // SESSCTX1b: a part that continues another keeps reading its line's kept folders, whose files its brief named.
        var continued = record.From is { Length: > 0 } from ? await ContextBriefs.LineIdsAsync(_context.Service, from, ct).ConfigureAwait(false) : [];

        // CHATFLOW1e (§6.1 (c), (f)): a go-on with another agent's findings answers them in its first turn, whose reading is frozen at its end; a
        // word of the person's that went with them puts their work inside that turn, as a word steered into it does.
        Func<bool, Task>? answering = null;
        if (words.Any(word => word.By is not null) && owner._answering.TryGetValue(sessionId, out var answers))
        {
            var persons = words.Any(word => word.Persons);
            answering = steered => answers(steered || persons);
        }

        var placed = _runner.RepositoryPlace(
            config, registry, known, record.Repository!, record.Tree!, continued,
            answers: await _runner.AnswersFindingsAsync(sessionId, record.Tree!, config).ConfigureAwait(false));
        handed = true;
        return await _runner.RunAsync(
            record.Actor ?? "chat", sessionId,
            words.Any(word => word.Persons)
                ? $"conversation `{sessionId}` goes on with your words in its own conversation."
                : words.All(word => word.Daoris)
                    // COWORK1b: Daoris's own words alone are no agent's answer.
                    ? $"conversation `{sessionId}` goes on with Daoris's words in its own conversation."
                    : $"conversation `{sessionId}` goes on with another agent's answer in its own conversation.",
            new ChatAgent(adapter), selection, config, placed, onEnded, ct, new GoingOn(record, ask) { Answering = answering, Proc = proc }).ConfigureAwait(false);
    }

    /// <summary>
    /// Ask Daoris's ended conversation goes on with the person's words (ASKHIST1): judged as a chat's is up to its account, then
    /// the room's one running conversation set aside, the ledger's move, and its own conversation resumed in the room. It reads
    /// the help chat's set-aside and room; the help chat calls nothing here.
    /// </summary>
    private async Task<ChatStart> HelpGoingOnAsync(
        PriorSession record, ISessionAdapter adapter, HarnessConversation kept, DriverConfig config,
        Func<string, string, Task>? onEnded, DateTimeOffset? woke, CancellationToken ct)
    {
        var sessionId = record.Session;

        // Its account by name, as a chat's, among the machine's accounts (D89): going on keeps the account it ran on.
        var resume = await _context.Harnesses
            .ResumeAsync(adapter.Name, config, workspace: null, record.Profile, StartKind.Conversation, line: LineKind.AskDaoris, ct: ct)
            .ConfigureAwait(false);
        var course = owner.Decide(record, answer: resume.Answer);
        if (resume.Waits || course is LineCourse.Wait)
        {
            _context.Note(sessionId, ResumeWords.NotYetOn(resume, ResumeWords.HelpDoorOf()));
            // LIMITRESUME1c (D206 §3.5, D137 point 7): the look's limits stage wakes it on its own account at the reset.
            owner.Waits(sessionId);
            return new(null, resume.Selection.Refusal ?? ResumeWords.HelpDoor);
        }

        if (resume.Elsewhere is { } elsewhere) return owner.CannotGoOn(record, elsewhere, adapter.Name);
        var selection = resume.Selection;
        if (!selection.Allowed) return owner.Held(sessionId, HeldWords.Refused(selection));
        if (Continuations.Judge(record, adapter.Name, adapter.Resumes, selection.Profile, kept) is { } account)
        {
            return owner.CannotGoOn(record, account, adapter.Name);
        }

        // Its own workspace's running one alone (ASKSCOPE1a): another workspace's runs on beside it.
        if (await _help.SetAsideHelpAsync(sessionId, record.Workspace, ct).ConfigureAwait(false) is { } answering) return owner.Held(sessionId, HeldWords.Answering(answering));

        // 🔴 The ledger's one move out of an ended state (MSG1a, D137 §2.3), before anything is spawned: it holds the room again,
        // and refuses where another conversation holds it, one another process here runs among them.
        try
        {
            await _context.Service.AdvanceAsync(sessionId, "working", Continuations.GoingOnNoted, ct: ct).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            return owner.Held(sessionId, HeldWords.Program(refused.Message));
        }

        // COWORKVIEW1a: its process's token and `start` as its move returns, let go wherever it never reaches the runner.
        var proc = ChildLineage.Opened(_context.Home, sessionId);
        var handed = false;
        using var unhanded = new Disposer(() =>
        {
            if (!handed) LineageTokens.Current.Unspawned(proc);
        });

        var now = await _context.ReadAgainAsync(sessionId, ct).ConfigureAwait(false);
        var words = Continuations.HandedAfterMove(record.Waiting, now);
        if (words.Count == 0) return await owner.TakenBackAsync(record, ct).ConfigureAwait(false);
        var ask = new ResumeAsk(
            kept.Conversation, words,
            woke is { } until
                ? LimitNotes.Resumed(until)
                : Continuations.OpeningNoted(adapter.Name, selection.Version, record.HarnessVersion, answer: false),
            files: word => ChatFiles.Kept(_context.Home, sessionId, word.Files))
        {
            // LIMITRESUME1c (D206 §3.5): the words its account's limit refused, handed again first.
            Resent = owner.Resent(sessionId, LimitHeld.Waiting(_context.Events.After(sessionId, 0).Events, record.Limit)),
        };

        // ASKSCOPE1b: the room it ran in, its workspace's, or the machine's where it started there before each workspace had its own.
        var (room, reads) = await _help.HelpRoomNowAsync(sessionId, KnowledgeRoomPaths.GoesOnIn(_context.Home, record.Workspace, record.Tree), ct)
            .ConfigureAwait(false);
        handed = true;
        return await _runner.RunAsync(
            record.Actor ?? "ask-daoris", sessionId, $"Ask Daoris `{sessionId}` goes on with your words in its own conversation.", new ChatAgent(adapter), selection, config,
            _runner.HelpPlace(room, reads, record.Workspace), onEnded, ct, new GoingOn(record, ask) { Proc = proc }).ConfigureAwait(false);
    }
}
