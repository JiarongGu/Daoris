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
        // Only an ended chat of this machine's with words waiting: a driven record goes on through the planner, and a
        // live one hears words at its door. Ask Daoris's conversation goes on too, in its room.
        var json = await _context.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var record = ServiceClient.ReadRecord(json, sessionId);
        if (record is not { Kind: "chat", WordsWaiting: true } || record.Teammate
            || !(Ended.Contains(record.State) || string.Equals(record.State, "stood-down", StringComparison.OrdinalIgnoreCase)))
        {
            return new(null, $"session `{sessionId}` is no ended conversation of this machine's with words waiting.");
        }

        // 🔴 One handed over or moved went on in another, which holds its tree now and takes
        // its words. Restarted, it would open a second conversation in that tree: it stays as it ended, its words unmarked.
        if (WordsNever.ContinuedIn(json, sessionId) is { } next)
        {
            return new(null, $"conversation `{sessionId}` went on in conversation `{next}`, which takes its words.");
        }

        // 🔴 Answers alone that would take its line on past its number in a row with nobody between wait for the person, marked before any
        // mark is cleared; a word of theirs, or their *Go on with this answer*, lets them go.
        var lines = ServiceClient.ReadRecords(json);
        var hold = AnswersAloneHold.For(_context.Events, _context.Marks, _context.Home);
        int? heldAt = null;
        if (hold.Holds(record, lines, DateTimeOffset.UtcNow, heldAt: at => { heldAt = at; owner.Decide(record, alone: at); }))
        {
            return new(null, AnswersAloneNotes.Refusal(sessionId, heldAt));
        }

        // The person wrote to it, so it stays in view whatever comes of it, and an earlier mark of words it could
        // not take is past: these are taken up now.
        new SessionArchive(_context.Home).Unarchive([sessionId]);
        _context.Marks.Clear(sessionId);

        // 🔴 One its agent refused at the open, or whose opening passed its bound unopened, began no conversation, so it is never resumed: its go-on
        // is a fresh start of its actor, its words the first message, a pick trying only its own agent's entries on its own account, and a
        // refused continuation handed the brief of its line first. The come-up's go-on takes the same path.
        if (record.NotOffered || (record.NotUp && _context.Conversations.Read(sessionId) is null))
        {
            var fresh = await _move.NotOfferedAsync(record, json, config, onEnded, ct).ConfigureAwait(false);
            if (fresh.SessionId is null) _context.Note(sessionId, LimitNotes.NotMoved(fresh.Message));
            return new(fresh.SessionId, fresh.Message);
        }

        // Its conversation is the adapter's that opened it, so it goes on on that one or not at all. A record naming none (one from before
        // adapters were kept) goes on with its role's choice, Chat's order, never the machine's `adapter` key; Chat off, or its entry unread, holds the words with the sentence.
        string ranOn;
        try
        {
            // Its binding's role first, as a quest's go-on reads it.
            ranOn = record.Adapter is { Length: > 0 } opened
                ? opened
                : StartPlan.ContinuedOn(
                    record, config, "chat", _context.Conversations.RoleOf(sessionId)?.Role, RoleCatalog.ReadHome(_context.Home), new RolePlace());
        }
        catch (DriverException unchosen)
        {
            return owner.Held(sessionId, HeldWords.Program(unchosen.Message));
        }

        AgentDoor? adapter = null;
        try
        {
            adapter = _context.Harnesses.Adapters.Door(ranOn);
        }
        catch (DriverException)
        {
            // An adapter not here (a plugin's, removed): its conversation cannot be resumed.
        }

        // What can be known before anything is asked of the toolchain, on the account it ran on: the nevers, no words, its
        // tree, a kept id and a door that resumes. Where the selection lands on another account is judged after it.
        var kept = _context.Conversations.Read(sessionId);
        var why = adapter is null
            ? ContinueWhy.Of(ContinueWhy.Refused)
            : Continuations.Judge(record, adapter.Id.Value, adapter.Can.Resumes, record.Profile, kept, home: _context.Home);
        if (why is not null || adapter is null || kept is null)
        {
            var reason = why ?? ContinueWhy.Of(ContinueWhy.Unkept);
            // 🔴 No id kept, or a door that cannot resume, proves its conversation lost, so it goes on by
            // itself in a new part handed the brief; anything else waits for the person, the brief ready. A lost conversation is decided by
            // the move it goes on in (ConversationLost, which knows the walk's answer); what cannot go on is the course's hold here.
            if (adapter is not null && reason.Lost) return await owner.LostAsync(record, reason, adapter.Id.Value, config, onEnded, held: true, ct).ConfigureAwait(false);
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

        // The account it ran on, asked for by name, as a chat's picker names one: its conversation lives in
        // that account's home. Cooling, the words wait for its reset, said with the door out of the wait; unable to run there
        // at all, it cannot go on, at once and saying why, since nothing carries a chat's words on by itself; anything else the
        // selection will not start holds the words.
        // Cooling, and another account of its agent ready, the person's words go on now in a new part
        // there, through the ended limit move's body, which walks once and opens on what it judged; otherwise they wait for the earliest
        // reset of any account of its agent, never its own alone.
        var resume = await owner.SwitchableAsync(record, adapter.Id.Value, config, known.Workspace, ct).ConfigureAwait(false);
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
            // The look's limits stage wakes it at the reset.
            owner.Waits(sessionId);
            return new(null, resume.Selection.Refusal);
        }

        if (resume.Elsewhere is { } elsewhere) return owner.CannotGoOn(record, elsewhere, adapter.Id.Value);
        var selection = resume.Selection;
        if (!selection.Allowed) return owner.Held(sessionId, HeldWords.Refused(selection));
        if (Continuations.Judge(record, adapter.Id.Value, adapter.Can.Resumes, selection.Profile, kept, home: _context.Home) is { } account)
        {
            return owner.CannotGoOn(record, account, adapter.Id.Value);
        }

        // The words its account's limit refused, handed again first, before the words that waited.
        var limited = LimitHeld.Waiting(_context.Events.After(sessionId, 0).Events, record.Limit);

        // A tree of its own is taken again: its repository's trees are held until the ledger holds this one once more,
        // so bringing the repository up to date does not rebase it under the conversation.
        var own = !SamePath(record.Tree!, known.Root!);

        // A conversation going on in its own tree starts from the line where the line
        // came to hold some of its commits, as a driven session's does, and is told so after the words. Asked before this
        // start's own hold below, since bringing a tree up to date holds the repository's trees alone. The person's checkout is never
        // moved: a conversation in it asks nothing.
        var inSync = own ? await owner.StartInSyncAsync(record.Tree!, ct).ConfigureAwait(false) : null;
        // One in the checkout itself holds it too, until the ledger holds it again below; a resolver's own going on carries its ask key.
        var startIn = StartIn.In(record.Tree!, new TreeHandOffs(_context.Home).AskOf(record.Tree!, sessionId));
        using var starting = TreeLock.TryStarting(_context.Home, known.Root!, startIn);
        if (starting is null) return owner.Held(sessionId, TreeLock.StartHeld(_context.Home, known.Root!, startIn, known.Repository));

        // 🔴 The ledger's one move out of an ended state, with the person's words waiting, before anything
        // is spawned: it holds the tree again, and refuses where another session holds it.
        try
        {
            await _context.Service.AdvanceAsync(sessionId, "working", GoingOnNote(record.Waiting), ct: ct).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            return owner.Held(sessionId, HeldWords.Program(refused.Message));
        }

        starting?.Dispose();
        // Its process's token and `start` as its move returns, let go wherever it never reaches the runner.
        var proc = ChildLineage.Opened(_context.Home, sessionId);
        var handed = false;
        using var unhanded = new Disposer(() =>
        {
            if (!handed) LineageTokens.Current.Unspawned(proc);
        });

        // Every word waiting once it moved: none can be kept on a record that works (the say door answers `running`), so the
        // list read now is whole, a word said while it is judged included.
        var now = await _context.ReadAgainAsync(sessionId, ct).ConfigureAwait(false);
        var words = Continuations.HandedAfterMove(record.Waiting, now);
        if (words.Count == 0) return await owner.TakenBackAsync(record, ct).ConfigureAwait(false);
        // Each word's files where they were kept when said, handed with the words; the conversation's
        // rules already read its own files folder. What its start did with its tree against the line after them.
        // And Daoris's count for its line, where one moved or stands near what Daoris does on it.
        var counted = CountTexts.GoOn(LineCountsRead.FactsOf(_context.Home, sessionId, json).Counts);
        var ask = new ResumeAsk(
            kept.Conversation, words,
            woke is { } until ? LimitNotes.Resumed(until) : OpeningNote(words, adapter.Id.Value, selection.Version, record.DoorVersion),
            Continuations.AfterWords(inSync, counted),
            files: word => ChatFiles.Kept(_context.Home, sessionId, word.Files))
        {
            Resent = owner.Resent(sessionId, limited),
        };
        // A part that continues another keeps reading its line's kept folders, whose files its brief named.
        var continued = record.From is { Length: > 0 } from ? await ContextBriefs.LineIdsAsync(_context.Service, from, ct).ConfigureAwait(false) : [];

        // A go-on with another agent's findings answers them in its first turn, whose reading is frozen at its end; a
        // word of the person's that went with them puts their work inside that turn, as a word steered into it does.
        Func<bool, Task>? answering = null;
        if (words.Any(word => word.By is not null) && owner._answering.TryGetValue(sessionId, out var answers))
        {
            var persons = words.Any(word => word.Persons);
            answering = steered => answers(steered || persons);
        }

        // A tree Daoris marked as it brought it back to this line gets its files step, then the go-on's own step takes the mark off
        // before its resumed turn; a tree holding none is the conversation's own, and nothing is done to it.
        if (TreeFilesRecord.Read(_context.Home, record.Tree!) is { Unworked: true })
        {
            await TreeFilesStep.RunAsync(
                _context.Home, known.Root!, record.Tree!, record.Repository!,
                line => _context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts.Count > 0 ? line.Parts : null }, say: null),
                ct).ConfigureAwait(false);
            TreeFilesStep.Hand(_context.Home, record.Tree!);
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
                    // Daoris's own words alone are no agent's answer.
                    ? $"conversation `{sessionId}` goes on with Daoris's words in its own conversation."
                    : $"conversation `{sessionId}` goes on with another agent's answer in its own conversation.",
            new ChatAgent(_context.Harnesses.Adapters, adapter), selection, config, placed, onEnded, ct, new GoingOn(record, ask) { Answering = answering, Proc = proc }).ConfigureAwait(false);
    }

    /// <summary>
    /// Ask Daoris's ended conversation goes on with the person's words: judged as a chat's is up to its account, then
    /// the ledger's move, and its own conversation resumed in the room, beside any other running. It reads the help chat's room;
    /// the help chat calls nothing here.
    /// </summary>
    private async Task<ChatStart> HelpGoingOnAsync(
        PriorSession record, AgentDoor adapter, HarnessConversation kept, DriverConfig config,
        Func<string, string, Task>? onEnded, DateTimeOffset? woke, CancellationToken ct)
    {
        var sessionId = record.Session;

        // Its account by name, as a chat's, among the machine's accounts: going on keeps the account it ran on.
        var resume = await _context.Harnesses
            .ResumeAsync(adapter.Id.Value, config, workspace: null, record.Profile, StartKind.Conversation, line: LineKind.AskDaoris, ct: ct)
            .ConfigureAwait(false);
        var course = owner.Decide(record, answer: resume.Answer);
        if (resume.Waits || course is LineCourse.Wait)
        {
            _context.Note(sessionId, ResumeWords.NotYetOn(resume, ResumeWords.HelpDoorOf()));
            // The look's limits stage wakes it on its own account at the reset.
            owner.Waits(sessionId);
            return new(null, resume.Selection.Refusal ?? ResumeWords.HelpDoor);
        }

        if (resume.Elsewhere is { } elsewhere) return owner.CannotGoOn(record, elsewhere, adapter.Id.Value);
        var selection = resume.Selection;
        if (!selection.Allowed) return owner.Held(sessionId, HeldWords.Refused(selection));
        if (Continuations.Judge(record, adapter.Id.Value, adapter.Can.Resumes, selection.Profile, kept, home: _context.Home) is { } account)
        {
            return owner.CannotGoOn(record, account, adapter.Id.Value);
        }

        // 🔴 The ledger's one move out of an ended state, before anything is spawned; another conversation running, in its room or
        // any other, holds nothing of it.
        try
        {
            await _context.Service.AdvanceAsync(sessionId, "working", Continuations.GoingOnNoted, ct: ct).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            return owner.Held(sessionId, HeldWords.Program(refused.Message));
        }

        // Its process's token and `start` as its move returns, let go wherever it never reaches the runner.
        var proc = ChildLineage.Opened(_context.Home, sessionId);
        var handed = false;
        using var unhanded = new Disposer(() =>
        {
            if (!handed) LineageTokens.Current.Unspawned(proc);
        });

        var now = await _context.ReadAgainAsync(sessionId, ct).ConfigureAwait(false);
        var words = Continuations.HandedAfterMove(record.Waiting, now);
        if (words.Count == 0) return await owner.TakenBackAsync(record, ct).ConfigureAwait(false);
        // The room it ran in, its workspace's, or the machine's where it started there before each workspace had its own; the focus
        // these words take, its room's own where none is chosen, a change told in one line ahead of them.
        var roomIn = KnowledgeRoomPaths.GoesOnIn(_context.Home, record.Workspace, record.Tree);
        var ask = new ResumeAsk(
            kept.Conversation, _help.TakeFocus(sessionId, roomIn, words, await FocusRegistryAsync(ct).ConfigureAwait(false)),
            woke is { } until
                ? LimitNotes.Resumed(until)
                : Continuations.OpeningNoted(adapter.Id.Value, selection.Version, record.DoorVersion, answer: false,
                    restarted: words.Any(word => word.Daoris && DaorisKeys.IsRestart(word.Key))),
            files: word => ChatFiles.Kept(_context.Home, sessionId, word.Files))
        {
            // The words its account's limit refused, handed again first.
            Resent = owner.Resent(sessionId, LimitHeld.Waiting(_context.Events.After(sessionId, 0).Events, record.Limit)),
        };

        var (room, reads) = await _help.HelpRoomNowAsync(sessionId, roomIn, ct).ConfigureAwait(false);
        handed = true;
        return await _runner.RunAsync(
            record.Actor ?? "ask-daoris", sessionId, $"Ask Daoris `{sessionId}` goes on with your words in its own conversation.", new ChatAgent(_context.Harnesses.Adapters, adapter), selection, config,
            _runner.HelpPlace(room, reads, record.Workspace, _help.FocusOf(sessionId, roomIn)), onEnded, ct, new GoingOn(record, ask) { Proc = proc }).ConfigureAwait(false);
    }

    /// <summary>The registry a focus is resolved against; null where the service did not answer, which widens nothing.</summary>
    private async Task<IReadOnlyList<RepoView>?> FocusRegistryAsync(CancellationToken ct)
    {
        try
        {
            return await _context.Service.RegistryAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
