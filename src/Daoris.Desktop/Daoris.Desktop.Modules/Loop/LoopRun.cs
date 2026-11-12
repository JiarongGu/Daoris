using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// One run of the shell's loop (<see cref="DriverLoop.Start"/>), from the host's answer to the stop. <see cref="RunAsync"/> declares
/// what it brings up in the order it is let go in reverse; each part's making is a method of its own, and what a look tells is
/// <see cref="LookTold"/>'s. What the loop's doors read of the run is in the shared state (<see cref="LoopState"/>).
/// </summary>
/// <remarks>
/// Everything before the home's lease is held by every loop on the home, whoever holds the lease, since the host, the page and the
/// conversations are this process's either way; what drives the home waits for the lease.
/// </remarks>
internal sealed class LoopRun(
    DriverLoop loop, LoopState shared, IEventBus eventBus, HostSupervisor supervisor, string serviceUrl, HomeEstablished? home,
    IInAppBrowser? browser, MachineLog? log)
{
    private Task? _firstScreen;

    public async Task RunAsync()
    {
        var ct = shared.Stopping.Token;
        if (!await HostUpAsync(ct).ConfigureAwait(false)) return;

        var homeDirectory = loop.Home;
        var key = Environment.GetEnvironmentVariable(ServiceClient.KeyVariable);
        var person = supervisor.ProvedKey;
        // A session's end hands the service its usage history's turn lines, which its memory rows' models are read from, and in its
        // place the uncommitted paths of its own its end's manifest found.
        using var service = new ServiceClient(serviceUrl, key, personKey: person)
        {
            TurnLines = sessions => ServiceClient.LinesOf(UsageHistory.TurnLinesOf(homeDirectory, sessions), turn => new ServiceClient.TurnLine(turn.Seq, turn.Started, turn.Ended, turn.Model)),
            PlaceEnds = (id, ct) => PlaceTurns.EndAsync(homeDirectory, id, ct),
        };
        // The service's doors for the one read the client does not carry: the repositories' index sizes.
        using var doors = new ServiceHttp(service.BaseUrl, key, personKey: person);
        // After the client and before the chat runner, so it hears the runner's closing moves and lets go before the client does.
        using var sessionLog = log is null ? null : new SessionLog(log, service, loop.Events);
        // The machine's remotes, one per workspace that has one, riding the looks as in the headless host. Absence is silent and local.
        using var sync = RemoteSyncSet.FromEnvironment(service.BaseUrl, key, person);
        shared.Sync = sync;
        // Each pass's end, a look's or *Sync now*'s, asks the sync slice to read where each circle stands again.
        sync.Passed += loop.HeardPass;
        // The page reads each backlog once and takes everything after it from these relays' batches.
        using var console = new ConsoleRelay(loop.Output, (session, lines) => OutputTold.TellAsync(eventBus, session, lines, loop.Output.IsLive(session)));
        using var conversation = new EventRelay(loop.Events, (session, events) =>
            eventBus.EmitAsync("DAORIS", "SESSION_EVENTS", new { Session = session, Events = events.ToArray() }));
        // After the client, so it is disposed before it: disposing the runner ends each conversation and writes its record through this
        // client, and a chat open at the close would otherwise be recorded nowhere. It shares the loop's registry, console, roster and usage.
        using var chat = new ChatRunner(
            service, loop.Harnesses.Adapters, homeDirectory, loop.Processes, loop.Output, loop.Harnesses, loop.Events, loop.Usage, browser,
            plugins: new PluginLog(log, loop.Health));
        TellChatMoves(chat);
        using var secondCheck = SecondCheckHookUp.Start(
            homeDirectory, loop.Events, loop.Pipe?.StandingConfirmation.HelpPresses, HeldPermissions.Of(loop.Events), log,
            session => StatePublisher.Publish(eventBus, StateScopes.Help, session));
        await loop.ComeUpAsync(service, chat).ConfigureAwait(false);
        var news = StartBeside(service, sessionLog, homeDirectory, ct);

        // A terminal's `sessions` requests, acted on as the screen's routes would act; before the home's lease, so a conversation is
        // reached while a terminal's loop drives the home.
        await using var requests = new SessionRequestWatch(homeDirectory, loop.Processes, () => loop.Service)
        {
            // A terminal's say is judged as the box's words are, and its take-back writes the withdrawn note here.
            Say = loop.Words.HoldAsync,
            TakeBack = loop.Words.HoldTakeBackAsync,
            Permit = asked => NativePermits.HoldAsync(new SessionRequests(homeDirectory), HeldPermissions.Of(loop.Events), asked),
            Told = press => StatePublisher.Publish(eventBus, press),
            Log = log,
        };
        // The shell's power event reopens each watch's roots at a resume; their own overrun rule is the fallback.
        using var resumeHeard = loop.Power?.Hear(requests.Resumed);

        // The notices' record, which any process on the home writes: another's write is heard, or found at the topic's deadline, and
        // this one's wakes nothing, since its writer pushes it.
        global::Daoris.Driver.Notices.ImportFile(homeDirectory);
        await using var notices = HomeWatch.ForNotices(homeDirectory, (_, _) => loop.Notices.Slice.Wake(), log: log);
        notices.Start();
        using var noticesResumed = loop.Power?.Hear(notices.Resumed);

        await using var lineage = HomeWatch.ForWork(homeDirectory, ChildLineage.FileName, loop.AtWork.JudgeFiles, log: log);
        lineage.Start();
        using var lineageResumed = loop.Power?.Hear(lineage.Resumed);

        // A plugin's word goes to the console under `plugin:<id>`; what the set does, without its words, to the log and the health record.
        shared.Hooks = PluginsAtStart.Set(homeDirectory, say: null, loop.Output, log, loop.Health);
        var adoptions = new PluginTokenAdoptions(homeDirectory, log);
        _ = adoptions.Ask();
        // Each change the publisher tells the windows reaches the plugins at `state/changed` too, queued by the set, so no press's answer
        // waits on a plugin; while another loop drives the home this set runs no plugin, and a change is told to nobody. A change to the
        // plugins asks the token's adoption again.
        using var observing = StatePublisher.Observe(eventBus, change =>
        {
            shared.Hooks?.Tell(change);
            adoptions.Told(change);
        });

        // One live driver per home: everything above carries on while another holds it.
        using var held = await loop.HoldHomeAsync(ct).ConfigureAwait(false);
        if (held is null) return;

        // The driven work's news is the lease holder's, told from its own cursor on a wait of its own.
        _ = Task.Run(
            () => JournalFeed.RunAsync(
                service,
                [
                    // The quests' moves first, its sink never waiting, so no wake waits on the news teller's reads of the service.
                    WorkEventNews.Gapped(new WorkEventTeller(told => shared.Hooks?.Tell(told)).Teller, told => shared.Hooks?.Tell(told), () => DateTimeOffset.UtcNow),
                    // The driven work's parks and ends told at `work/event` as they are claimed, and the teller's gaps.
                    WorkEventNews.Gapped(
                        NewsFeed("lease", news, service, row => !row.Marks.Contains("chat", StringComparer.Ordinal), ct, told => shared.Hooks?.Tell(told)),
                        told => shared.Hooks?.Tell(told), () => DateTimeOffset.UtcNow),
                ],
                log, ct),
            ct);

        ServePipe();

        var told = new LookTold(loop, shared, eventBus, service, doors, news, ct);
        // The conversations' take-up, run beside each look before the limits' wake: a dead shell's move is taken up, and a live one
        // quiet ten minutes said once.
        var moves = new ConversationMoves(homeDirectory, service, loop.Events, loop.Output);
        // A fresh home is seeded once with the copy of the merge workflow, which a place then follows.
        HomeSeed.Once(loop.ConfigPath, homeDirectory);
        var watch = NewWatch(service, homeDirectory, sync, chat, moves);
        shared.Watch = watch;
        // A word said while a conversation move stands is held until it ends, read from the move record.
        loop.Words.TakeStanding = moves.Stands;
        shared.Focus.Ready(watch.Focused);
        using var filesResumed = loop.Power?.Hear(watch.Resumed);
        await watch.RunAsync(told.ToldAsync, told.FailedAsync, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The host up, held or adopted, and what the person acts on said once on the channel they act on; false where it is not up.
    /// <see cref="LoopState.HostReady"/> completes whatever happens: the splash awaits it, and a fault that skipped it would leave the
    /// window dark.
    /// </summary>
    private async Task<bool> HostUpAsync(CancellationToken ct)
    {
        var up = false;
        string? trouble = null;
        try
        {
            // The host the shell began before the window's engine, where it began one.
            up = await supervisor.UpAsync(ct).ConfigureAwait(false);
            trouble = supervisor.Trouble;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            trouble = error.Message;
        }
        finally
        {
            shared.HostReady.TrySetResult(up);
        }

        if (!up)
        {
            await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { Message = trouble })
                .ConfigureAwait(false);
            return false;
        }

        // The service answers: its address goes into the home for a terminal to find.
        loop.Announce();

        // A host that is somebody else's, serving a page that is not this install's, waits on the person as a parked session does; the
        // loop goes on, because the host does work.
        if (supervisor.Notice is { } notice)
        {
            await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { Message = notice })
                .ConfigureAwait(false);
        }

        // The home's own news, which the person acts on once, and which a terminal opened before it is set does not see.
        if (home is { Worth: true })
        {
            await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { Message = home.Notice })
                .ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// What a conversation's runner tells the windows as it moves: its model and effort, in the shape <c>SESSION_OPTIONS</c> answers
    /// with; a start refused at its open going on in its replacement, so a window showing the refused one follows it; and its start
    /// receipt written, so an open <i>What it has</i> reads a resume's new one.
    /// </summary>
    private void TellChatMoves(ChatRunner chat)
    {
        chat.OptionsChanged += (session, options) =>
            _ = eventBus.EmitAsync("DAORIS", "SESSION_OPTIONS_CHANGED", ConversationRoutes.OptionsAnswer(session, options));
        chat.Replaced += (from, to) =>
        {
            _ = eventBus.EmitAsync("DAORIS", DriverLoop.ReplacedType, new { From = from, To = to });
            StatePublisher.Publish(eventBus, StateScopes.Sessions | StateScopes.Help, to);
        };
        chat.WiredKept += DriverLoop.WiredKeptTold(eventBus);
    }

    /// <summary>
    /// What runs beside the looks on tasks of its own until the stop, held by every loop on the home, since a person presses here
    /// either way; the news it makes is returned for the lease holder's feed and the looks.
    /// </summary>
    private News StartBeside(ServiceClient service, SessionLog? sessionLog, string homeDirectory, CancellationToken ct)
    {
        // The machine's facts, once the first screen has its data: their sessions judge is a third look at the sessions beside the
        // page's two. The watch tells them its wakes; a wake before is kept for the start.
        _ = StartFactsAsync(ct);
        // The presses waiting at the service's door, told to the windows from one wait on the service.
        _ = Task.Run(() => loop.ServiceConfirmations.RunAsync(service.Confirmations, ct), ct);
        // The memory changes that wait on the person, read on a poll.
        _ = Task.Run(() => loop.MemoryWaits.RunAsync(service, ct), ct);

        // The service's journal, so an end a headless loop drove reaches this window's slices as a conversation's own does; and the
        // conversations' news, a park among them, told from a cursor this shell keeps, so a park made just before a restart is told
        // once after it.
        var news = new News(homeDirectory);
        _ = Task.Run(
            () => JournalFeed.RunAsync(
                service,
                [
                    new JournalTeller(async batch =>
                    {
                        await PartEndRelay.TellAsync(eventBus, batch).ConfigureAwait(false);
                        if (sessionLog is not null) await sessionLog.TellAsync(batch).ConfigureAwait(false);
                    }),
                    NewsFeed("shell", news, service, row => row.Marks.Contains("chat", StringComparer.Ordinal), ct),
                ],
                log, ct, heard: present => shared.HearsJournal = present),
            ct);
        return news;
    }

    /// <summary>
    /// The terminal's pipe, served once this loop holds the home, the one writer, and only then named in the address, so a terminal
    /// that reads it finds it served. A name already held serves none, said in the log.
    /// </summary>
    private void ServePipe()
    {
        if (loop.Pipe is { } served)
        {
            // A press taken back after being shown, or lost at the close, is kept in Done and failed.
            served.Confirmations.Noticed = loop.Notices.Ended;
            // A press no terminal waits on, told at `work/event` as it settles, after its record says how.
            var toldHooks = SettledTelling.Of(() => shared.Hooks, () => DateTimeOffset.UtcNow);
            // One of Ask Daoris's is also said in its conversation.
            Func<Func<string, string, string, bool>?> tellChat = () => loop.Chat is { } chat ? chat.Tell : null;
            Action<string, HelpPublishedEntry> keepPublished = (session, entry) =>
            {
                try
                {
                    new HelpPublished(loop.Home).Keep(session, entry);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
                {
                    // Only this conversation's list is short by one: the ask and the quest stand, and their lists name them.
                }
            };
            var saidChat = SettledSaid.Of(tellChat, loop.PressedEnds, keepPublished);
            served.Confirmations.Settled = (shown, ending) =>
            {
                toldHooks(shown, ending);
                saidChat(shown, ending);
            };
            served.Confirmations.Answered = (id, answer) => loop.PressedEnds.Answered(id, answer, tellChat(), keepPublished);
            served.Grounds = new LoopGrounds(loop, served);
            served.Delegates = session => DelegateTiers.Of(loop, session);
            served.TerminalTier = () => DelegateTiers.Terminal(loop);
            served.TerminalActs = loop.TerminalActs;
            // A restart forgets every steward's release, so what an earlier run left in the file is withdrawn before the first look;
            // the owner's own releases are kept whole.
            RoleStarts.Sweep(served.StandingConfirmation, loop.ConfigPath);
            // The presses that waited when Daoris last stopped come back before the pipe is served, on this home's lease, which makes
            // this loop their records' one writer; what could not come back, and what a newer Daoris kept, is said once in Done and failed.
            served.Confirmations.Restarted = loop.Notices.Restarted;
            served.Restore();
        }

        if (loop.Pipe?.Serve() is { } pipe)
        {
            loop.AnnouncePipe(pipe);
            loop.Stages?.Mark(StartStages.Pipe);
        }
    }

    /// <summary>
    /// The watch every look runs in, handed what the shell adds to the headless host's: the conversations this shell's chat runner
    /// carries on among them (a hand-over its agent asked for, a chat run's second-opinion findings, a lost session going on as a
    /// conversation in its tree, and the conversations waiting for an account, woken beside each look after the take-up).
    /// </summary>
    private DriverWatch NewWatch(ServiceClient service, string homeDirectory, RemoteSyncSet sync, ChatRunner chat, ConversationMoves moves) =>
        new DriverWatch(service, loop.ConfigPath, homeDirectory, loop.Processes, sync, loop.Output, loop.Harnesses, loop.Usage, shared.Hooks, loop.Events, browser)
        {
            // Asked at every look, so an update staged or put off between looks holds or frees the next one.
            Draining = () => loop.Draining?.Invoke() == true,
            Noticed = () => loop.Notices.Slice.Wake(),
            // Where a run's failure nothing else awaits is written with its place, not left to the finalizer.
            Log = log,
            Supervisor = loop.Supervisor,
            Roles = () => loop.Pipe?.StandingConfirmation is { } judge ? new RoleStarts(judge, loop.ConfigPath) : null,
            Supervising = () => loop.Pipe?.StandingConfirmation is null ? null : new AgentSupervisorDesk(loop),
            // The first look's line-following and tidy wait for the first screen's data, so the page's git-backed pulls and harness
            // probes are not queued behind them.
            FirstScreen = FirstScreenAsync,
            Reviews = loop.Reviews,
            Heard = loop.HeardMove,
            HandsOver = (session, agent, step, token) => chat.HandOverAsync(
                session, agent, null, HandOverWhy.Agent, HandOverBy.Agent, DriverConfig.Load(loop.ConfigPath),
                onEnded: (ended, state) => DriverLoop.Ended(eventBus, ended, state), token, step?.Model, step?.Effort),
            SendsBack = chat,
            GoesOnLost = (session, why, token) => chat.GoOnLostAsync(
                session, why, DriverConfig.Load(loop.ConfigPath), onEnded: (ended, state) => DriverLoop.Ended(eventBus, ended, state), token),
            Limits = async (looked, token) =>
            {
                await moves.LookAsync(token).ConfigureAwait(false);
                await chat.WakeLimitsAsync(looked, onEnded: (ended, state) => DriverLoop.Ended(eventBus, ended, state), token).ConfigureAwait(false);
                // A hand-over the take-up left standing under this run is taken beside the look, as the come-up takes the ones it finds.
                await chat.TakeHandOversAsync((ended, state) => DriverLoop.Ended(eventBus, ended, state), token).ConfigureAwait(false);
            },
        };

    /// <summary>
    /// One news teller as a journal feed's teller: its rows claimed from its own cursor, each claimed piece made the attention event
    /// the notifier reads, kept in Notifications whatever the switch says, since recording is not telling, and told while it is on.
    /// </summary>
    /// <param name="told">Where the lease's teller tells each piece at <c>work/event</c>; null for the conversations' teller, which tells none.</param>
    private JournalTeller NewsFeed(
        string name, News news, ServiceClient service, Func<JournalEntry, bool> takes, CancellationToken ct, Action<WorkEvent>? told = null)
    {
        var teller = new NewsTeller(
            news, name, takes, () => DateTimeOffset.UtcNow,
            async pieces =>
            {
                foreach (var piece in pieces)
                {
                    var record = await NewsAttention.ReadAsync(service, piece, ct).ConfigureAwait(false);
                    if (told is not null && WorkEventNews.Of(piece, record) is { } happened) told(happened);
                    if (await LineGoesOn.OfNewsAsync(service, piece, ct).ConfigureAwait(false)) continue;
                    var item = NewsAttention.Of(piece, record);
                    loop.Notices.Keep([item]);
                    if (NotifyRule.Tells(item, loop.Notifying())) await AttentionTold.TellAsync(eventBus, item).ConfigureAwait(false);
                }
            })
        {
            Incomplete = batch => log?.Warn("news.incomplete", ("teller", name), ("store", batch.Store), ("floor", batch.Floor)),
            Refused = why => log?.Warn("news.refused", ("teller", name), ("why", why)),
        };
        return new JournalTeller(teller.TellAsync, () => news.Cursor(name), position => news.Advance(name, position));
    }

    /// <summary>
    /// The first screen's data in, or <see cref="DriverLoop.FirstScreenBound"/> from the first ask: the work held until then runs
    /// beside the looks, so nothing the page asks for waits on it. A loop with no start's timer (headless, a test) waits for nothing.
    /// </summary>
    private Task FirstScreenAsync(CancellationToken ct) =>
        LazyInitializer.EnsureInitialized(ref _firstScreen, () => loop.Stages?.FirstScreenAsync(DriverLoop.FirstScreenBound) ?? Task.CompletedTask)
            .WaitAsync(ct);

    /// <summary>The machine's facts started once the first screen has its data, or never where the loop stops first.</summary>
    private async Task StartFactsAsync(CancellationToken ct)
    {
        try
        {
            await FirstScreenAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        loop.Facts.Start(ct);
    }
}
