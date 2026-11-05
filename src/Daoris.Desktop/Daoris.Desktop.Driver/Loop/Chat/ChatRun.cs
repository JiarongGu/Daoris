using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The run of a conversation whose record is open: its process spawned with its rules and servers, its door's turns held, and its watch
/// started, or, when the spawn fails, its record concluded. It is one run for every kind of conversation: the kinds differ in what they hand
/// it, a place, a selection and the entry of the order they run.
/// </summary>
/// <remarks>
/// It stays one method: the steps share the process, the rules and the servers each failure lets go of, and the stages they would make are
/// not yet drawn.
/// </remarks>
internal sealed class ChatRun(
    IExecutorWiring wiring, ChatTold told, AttentionWatch answers, ChatOptions options, ChatAccountReads accounts, ChatServers chatServers,
    ChatWatch watching, ProtocolHeld held, ChatConclusion conclusion, ChatClosing closing, SessionUsage usage, IInAppBrowser? browser,
    PluginLog plugins, Func<TimeProvider> clock, Action<string, ChatQueue> queueChanged, Action<AttentionEvent> onAnswered)
{
    private ChatContext Context => wiring.Context;

    /// <param name="goOn">
    /// An ended chat going on: its record already moved to working by the ledger's one move out of an ended state, its own conversation
    /// resumed rather than opened, and the person's words waiting its first turn. Null for a new conversation.
    /// </param>
    /// <param name="chosen">
    /// The entry of its actor's order this new conversation runs: its <c>choice</c> and what it asks. Null for one going on, which keeps its
    /// record's, and for a start that walked nothing, which runs as before.
    /// </param>
    /// <param name="pending">
    /// Its pending start: refused at the open, it is re-asked, and where the answer is not this same session the record ends
    /// <c>choice-not-offered</c> and the replacement opens with its words. Null for one going on, and for a start that asks nothing.
    /// </param>
    public async Task<ChatStart> RunAsync(
        string actor, string sessionId, string message, ChatAgent agent, HarnessSelection selection, DriverConfig config,
        ChatPlace place, Func<string, string, Task>? onEnded, CancellationToken ct, GoingOn? goOn = null, ActorEffective? chosen = null,
        ChatPending? pending = null)
    {
        var home = Context.Home;
        var service = Context.Service;
        var processes = Context.Processes;
        var harnesses = Context.Harnesses;
        var events = Context.Events;
        var goOnDoor = wiring.Of<ChatGoOn>();

        if (goOn is not null) pending = null;
        var workTree = place.Tree;
        var transcript = DaorisHome.Entry(home, HomeEntry.Sessions, $"{sessionId}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);
        // The place as asked, before its mode is applied below: what the one retry of a native go-on runs in again.
        var asked = place;
        actor = goOn?.Record.Actor ?? actor;
        var actorAsked = goOn?.Record.Asked ?? chosen?.Asked ?? ActorAsked.Before(agent.Name);
        var choice = goOn?.Record.Choice ?? chosen?.From ?? "before";
        // A conversation going on re-applies the person's last composer change over its asked choice, so its resume sets what they last left;
        // the record's asked stays as its open set it.
        var overriding = goOn is null ? null : ActorOverride.Read(goOn.Record.Override);
        options.Kept(sessionId, overriding);
        var carrying = actorAsked.Overlaid(overriding);

        // The run over today's transports holds the conversation's process, its capture and its end, its stop and its close; its words stay
        // beside it, on the session and the process the run holds.
        TransportRun? run = null;
        TransportStart? start = null;
        string? rules = null;
        string? servers = null;
        string? skills = null;
        object? meta = null;
        IReadOnlyList<AcpMcpServer> pluginServers = [];
        // What its door is handed of the servers: the knowledge connector and the plugins', composed once for both doors, so the receipt
        // names the list the spawn got.
        IReadOnlyList<AcpMcpServer> offered = [];
        var connectors = ConnectorPlans.For(sessionId, null, []);
        Noted? browserNotice = null;
        var drivesBrowser = false;
        string? modeSaid = null;
        string? helpMode = null;
        try
        {
            // A conversation going on is working already: the ledger moved it there before anything was spawned.
            if (goOn is null) await service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);

            // Ask Daoris's place under the mode the person chose, refused where its agent has none such.
            var ran = goOn is null ? null : HelpModes.Recorded(goOn.Record.HelpMode);
            (place, modeSaid, helpMode) = HelpPlacement.UnderMode(place, agent, config, ran);

            // One going on under another mode than it last ran under runs under the new one, and its record says so before anything is spawned,
            // as its transcript's first line does.
            if (ran is not null && helpMode is not null && helpMode != ran)
            {
                await service.KeepHelpModeAsync(sessionId, helpMode, ct).ConfigureAwait(false);
            }

            // Its process as its door makes it, with its toolchain's environment and its tools at the start, as every driven session's.
            var info = agent.Prepare(
                // The native door resumes its own conversation by the id kept for it; the protocol door on its wire.
                new ChatTarget(place.Name, workTree, service.BaseUrl)
                {
                    Asked = carrying,
                    Resume = agent.Abilities.ResumesByRestart ? goOn?.Ask.Conversation : null,
                    // Its harness's mode that asks nothing, where Ask Daoris runs with full access on the native door.
                    PermissionMode = place.PermissionMode,
                    // Its own record, so a take through the connector its harness starts marks it.
                    Session = sessionId,
                },
                config.Commands.GetValueOrDefault(agent.Name),
                selection);

            // Its standing block, on its harness, told with a new conversation's first message and never at a go-on, which resumes the same
            // conversation; a hand-over's adds the memory its brief hands on.
            if (place.Block is { } block && goOn is null) told.HoldBlock(sessionId, agent.OnHarness(block, workTree));

            // The run's bracket, before what names its process is composed: what this start is handed of the downloads kept on its record, a
            // go-on as a first start, and its process's lineage token issued, a go-on's the one issued at its move, its retry's its own.
            start = new TransportStart(info, StartClass.Person) { Moved = goOn?.Proc };
            run = agent.Run(new ChatTransportHost(home, processes, service, Context.Conversations, config, Context.Output), start);
            var proc = await run.BracketAsync(sessionId, ct).ConfigureAwait(false);

            // The skills plugins hand this conversation's destination, composed under the home and handed by the harness's own session-only
            // door; the protocol door carries them beside its rules below.
            IReadOnlyList<string> skillPlugins = [];
            if (place.Plugins)
            {
                (skills, skillPlugins) = agent.HandSkills(
                    info, home, sessionId, PluginCatalog.Load(home, AdapterSet.Built().Names), place.ServersTo);
            }

            // What the conversation's agent may do, handed by each door's own way: a flag on the pipe, `session/new`'s `_meta` on the protocol
            // door, now that a conversation there opens a session.
            RuleLists? composed = null;
            var guarded = false;
            // A conversation in a tree of its own is held to its run's checks at every execution, its open, a hand-over's next record and a
            // go-on alike: the tree locked in its id for `run-heavy` to find, the checks kept by it, each heavy gate's command denied under
            // `light`, and the conversation told only then.
            var heavy = await told.HeldChecksAsync(sessionId, workTree, place, config, agent, ct).ConfigureAwait(false);
            if (agent.Rules is { } language)
            {
                var file = PermissionRules.Load(home);
                composed = place.Rules(file, sessionId);
                if (heavy is { Deny.Count: > 0 }) composed = composed with { Deny = [.. composed.Deny.Union(heavy.Deny, StringComparer.Ordinal)] };
                var guard = PermissionRules.GuardsTree(file) && place.Guarded ? TreeGuard.For(home, workTree, place.Also) : null;
                guarded = guard is not null;
                rules = language.Write(home, sessionId, new HandedRules(
                    composed, [.. PermissionRules.HardDeny(file), .. heavy.HardDeny is { } sentence ? [sentence] : (IEnumerable<string>)[]], guard,
                    // Which agent made each call, and each subagent's start and stop.
                    ChildLineage.PathOf(home, sessionId)));
                if (!agent.Abilities.RulesOnWire) agent.HandSettings(info, rules);
                else meta = agent.Meta(rules, skillPlugins);
            }

            // The plugins' servers, resolved once for both doors, with Daoris's own browser brought up for one that drives it, or that one
            // left out and the conversation told why.
            if (place.Plugins)
            {
                IReadOnlyList<string> drivers;
                (pluginServers, browserNotice, drivesBrowser, drivers) = await ChatServers.HandAsync(
                    home, browser, plugins, sessionId, ct, place.ServersTo, place.EveryServer).ConfigureAwait(false);
                // A new conversation's agent is told of the browser with its first message; one going on resumes a conversation already told
                // when it opened, so it is told nothing again.
                if (goOn is null && InAppBrowserServers.Told(drivers) is { } line) Context.Browsers[sessionId] = line;
                if (browserNotice is not null)
                {
                    Context.Record(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = browserNotice.Note, Parts = browserNotice.Parts });
                }
            }

            // The servers the plugins hand every session, on this door too. The protocol door carries them on `session/new`; a driven or
            // intake session on the pipe is handed a file, and a conversation on the pipe was handed nothing at all. A room has no `.mcp.json`
            // of its own, so its connector goes in the same file.
            var onWire = agent.Abilities.ServersOnWire;
            offered = onWire || place.ConnectorOnPipe
                ? chatServers.For(sessionId, place.Circle, pluginServers, onWire ? proc : null)
                : pluginServers;
            connectors = ConnectorPlans.For(sessionId, place.Circle, offered);
            if (agent.Servers is { } handing) servers = handing.Hand(info, home, connectors);

            // What Ask Daoris's start handed, kept now its every input is composed and before its process starts, read from the spawn itself:
            // whether its command line or its protocol door's meta names each file.
            if (helpMode is not null)
            {
                var pipe = !agent.Abilities.SetAtOpen;
                wiring.Of<HelpAccessSwap>().KeepWired(sessionId, new HelpHanded(helpMode, goOn is not null, agent.Name, agent.Abilities.Transport, workTree)
                {
                    Version = selection.Version,
                    Account = selection.Profile,
                    Posture = pipe ? null : place.Posture ?? agent.Posture,
                    PermissionMode = pipe ? place.PermissionMode : null,
                    Rules = composed,
                    RulesHanded = rules is not null && (pipe ? HelpWired.Names(info, rules) : HelpWired.Carries(meta, rules)),
                    Guarded = guarded,
                    Reads = place.Reads,
                    SkillPlugins = skillPlugins,
                    SkillsHanded = skillPlugins.Count > 0
                        && skillPlugins.All(plugin => pipe ? HelpWired.Names(info, plugin) : HelpWired.Carries(meta, plugin)),
                    Servers = offered,
                    ServersHanded = !pipe || (servers is not null && HelpWired.Names(info, servers)),
                    Withheld = browserNotice,
                    Housekeeping = place.Housekeeping,
                    Settings = place.Settings,
                });
            }

            // Reported alone, so a refusal here costs the record its provenance, never the conversation. An override re-applied is the person's
            // change, never the open's asked, so the record's asked is left as it was.
            if (overriding is null)
            {
                using var startLog = new MachineLog(home, "driver");
                await service.TryKeepActorAsync(sessionId, startLog, choice, ActorAsked.ForSpawn(agent.Name, info, actorAsked), ct: ct)
                    .ConfigureAwait(false);
            }
            // Never once the driver is closing. The run's spawn passes the start gate, which a conversation's press may reach before its first
            // wait, through the launcher: its harness born in this process's agents' job, the launcher read as the conversation's process.
            if (Context.Closing)
            {
                // Let go before its spawn, the run lets its token go.
                await run.DisposeAsync().ConfigureAwait(false);
                SpawnFiles.Remove(rules);
                SpawnFiles.Remove(servers);
                SpawnFiles.RemoveFolder(skills);
                await conclusion.ConcludeAsync(sessionId, "stopped", ChatRunner.ClosedNoted, onEnded).ConfigureAwait(false);
                if (processes.Close.Build is { } build) closing.KeepRestart(sessionId, build);
                return new(sessionId, message);
            }

            // Tracked from its spawn, so who is driving Daoris's browser lasts exactly as long as it, and reaped; a conversation is not ended
            // by the request that started it, so its spawn is on no request's token.
            start = start with { DrivesBrowser = drivesBrowser };
            run.HandStart(start);
            await run.SpawnAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // What the run was handed goes with it: a token never spawned, a go-on's among them, or one its spawn let go.
            if (run is not null) await run.DisposeAsync().ConfigureAwait(false);
            else if (goOn?.Proc is { } moved) LineageTokens.Current.Unspawned(moved);
            // The record exists and must say what happened, or it sits at `queued` or `starting` forever holding the repository, the same rule
            // the driven path follows. Every failure: a client timeout is an OperationCanceledException too, and filtering those out left the
            // record stranded where no stop and no sweep reaches it.
            SpawnFiles.Remove(rules);
            SpawnFiles.Remove(servers);
            SpawnFiles.RemoveFolder(skills);
            told.LetGoChecks(sessionId);
            var cancelled = error is OperationCanceledException && ct.IsCancellationRequested;
            await conclusion.ConcludeAsync(
                sessionId, "failed",
                cancelled
                    ? Noted.Of(ChatNoteCodes.ChatCancelled, "the request that started it was cancelled before its process started.")
                    : Observation.Failure(error.Message),
                onEnded).ConfigureAwait(false);
            if (cancelled) throw;
            return new(null, error.Message);
        }

        // Its turns are kept in the usage history under the account it runs as, as a driven session's are: bound before any word is queued to it.
        UsageTurns.Bound(home, events, sessionId, agent.Binding(Context.Harnesses.Adapters, selection.Profile) with { Actor = actor });
        var actorLog = new MachineLog(home, "driver");
        var actorRun = new ActorRun(events, sessionId, actor, goOn?.Record.Ran, ran => service.KeepActorAsync(sessionId, ran: ran), actorLog);
        Context.ActorRuns[sessionId] = actorRun;

        // The conversation outlives this call: the person types, the harness answers, and the record moves when the process does. Watched on
        // an unbound token deliberately, a chat is not ended by the request that started it.
        Context.Talking[sessionId] = agent;
        Context.Runs[sessionId] = run!;
        // One at a time in each workspace: going back to an earlier conversation of its workspace sets this one aside first, and one of
        // another workspace's leaves it be.
        if (string.Equals(place.Name, HelpRoom.Repository, StringComparison.Ordinal)) Context.Rooms[sessionId] = RemoteTarget.Workspace(place.Workspace);
        // The access it got, its mode and its configured access, which a change of the person's is compared with while it runs.
        if (helpMode is not null)
        {
            Context.AccessHanded[sessionId] = new ConfiguredAccess(place.Housekeeping) { Settings = place.Settings, CheckoutsOff = place.CheckoutsOff };
            Context.HelpModes[sessionId] = helpMode;
        }

        // On the protocol door a conversation is one session held over the wire: opened once, each message a turn on it. It used to be spawned
        // as a pipe, and a person's words went into the JSON-RPC stream as raw text, so the harness declared itself interactive and could not
        // hold a conversation at all.
        ProtocolChat? chat = null;
        NativeChat? native = null;
        var mapper = agent.StructuredOutput();
        // The turn's end it was last judged at by its account's usage curve: told under its turns' own lock, in order.
        DateTimeOffset? judged = null;
        // Whether a turn's refusal was read for its account's limit since the last turn's end was judged: set on the turn's own thread, taken
        // at its end.
        var limitedTurn = 0;
        // Whether a turn ended past its transient episode since the last turn's end was judged: its line's rules judge that end.
        var transientTurn = 0;
        // Only a repository's chat has a chat run; Ask Daoris, a helper and a reviewer have none, which the bind reads too.
        var bindsRun = actor == "chat";
        void Changed(ChatQueue queue)
        {
            queueChanged(sessionId, queue);
            // A full-access conversation the person narrowed mid-turn ends as its turn ends.
            wiring.Of<HelpAccessSwap>().TurnChanged(sessionId, queue);
            // A conversation asked to go on with another agent while it talked is handed over as its turn ends.
            wiring.Of<ChatHandOver>().Due(sessionId, queue, config, onEnded);
            // One over its account's usage curve goes on with another account of its agent as its turn ends, judged once; one whose turn its
            // account's limit refused goes on, in the same conversation, on the next account, and only that.
            if (CurveMove.TurnJustEnded(queue, judged))
            {
                judged = queue.LastTurnEnded;
                // A chat whose run is not bound yet (its tree grew before this build, or its bind failed) binds now.
                if (bindsRun) _ = Context.ChatRuns.TurnEndedAsync(sessionId, workTree, config);
                if (Interlocked.Exchange(ref limitedTurn, 0) == 1) wiring.Of<LimitMove>().Due(sessionId, config, onEnded);
                else if (Interlocked.Exchange(ref transientTurn, 0) == 1) wiring.Of<TransientMove>().Due(sessionId, config, onEnded);
                else wiring.Of<CurveMove>().Due(sessionId, config, onEnded);
            }

            // A turn the person's words started has ended: judged last, so a listener's failure moves nothing above.
            if (answers.Turned(sessionId, place.Name, queue) is { } answered) onAnswered(answered);
        }
        // A conversation going on hands its words to the native door only once its watch began, so a word the person says meanwhile goes after
        // them; the protocol door waits for its session to open anyway.
        var tracked = goOn is null ? null : new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (agent.Abilities.HeldOnWire)
        {
            chat = new ProtocolChat(
                Changed,
                stopped: () => processes.WasStopRequested(sessionId),
                limited: (failure, byCode) =>
                {
                    var cooling = accounts.Limited(sessionId, agent, selection.Profile, failure, config.CoolOff, byCode);
                    if (cooling is not null) Interlocked.Exchange(ref limitedTurn, 1);
                    return cooling;
                },
                signedOut: failure => accounts.SignedOut(agent, selection.Profile, failure),
                goOn: goOn?.Ask,
                // On this door the words went once their prompt is on the wire.
                wentOn: goOn is null ? null : () =>
                {
                    if (goOn.Sent(named: false)) _ = goOnDoor.WentOnAsync(sessionId, goOn, agent.Name);
                })
            {
                // A transient failure of one of its turns is tried again in this same conversation, on this process, with the one reader over
                // the adapter set; handed to its session once it is open.
                Retry = new ChatRetry(
                    sessionId, agent.Name, clock(),
                    refused =>
                    {
                        var read = agent.Read(harnesses.Adapters, DoorFailures.OfPrompt(refused), clock().GetUtcNow(), harnesses.Zone);
                        using var log = new MachineLog(home, "driver");
                        log.Info("agent.error", ("session", sessionId), ("adapter", agent.Name), ("account", selection.Profile), ("kind", read.Spelled), ("by", read.By));
                        return read;
                    })
                {
                    Limit = ConversationRules.Of(Context).Tries(
                        actor is { Length: > 0 } ? actor : helpMode is not null ? RoleWork.AskDaoris : RoleWork.Chat, place.Name, place.Workspace, sessionId),
                    PastBound = () => Interlocked.Exchange(ref transientTurn, 1),
                    Tried = (episode, number, next) =>
                    {
                        using var log = new MachineLog(home, "driver");
                        log.Info(
                            "agent.transient",
                            ("session", sessionId), ("adapter", agent.Name), ("account", selection.Profile), ("by", episode.First.By), ("try", number),
                            ("next", next));
                    },
                },
                // Its actor's model and effort set before its first prompt, the composer opening on them; what the agent would not take kept as
                // asked and not carried, and what its open listed kept as its agent's reading.
                Asked = carrying,
                Carried = overriding is not null ? null
                    : notCarried => _ = service.TryKeepActorAsync(sessionId, null, choice, notCarried, ct: CancellationToken.None),
                OffersRead = offers => options.KeepOffers(agent.Name, selection, sessionId, offers),
                Pending = pending,
                Shown = said => Context.TakeShown(sessionId, said),
                ModelRefused = ChoiceReask.Of(pending is null ? null : async refusal =>
                {
                    // Kept as the account's reading first, then the walk asked again as if it had been known.
                    var later = await wiring.Of<ChatReplacement>().ReaskAsync(pending, sessionId, agent, selection, config, refusal).ConfigureAwait(false);
                    if (later is null) return null;
                    choice = later.From;
                    await service.TryKeepActorAsync(sessionId, null, choice, later.Asked, ct: CancellationToken.None).ConfigureAwait(false);
                    return later.Asked;
                }),
            };
            chat.Turns.Closing = () => Context.Closing;
            chat.Turns.Unsent = count => closing.Unsent(sessionId, count);
            Context.Turned[sessionId] = chat;
        }
        else if (mapper is not null)
        {
            // Turns are only visible where the wire says where one ends. A text-only pipe takes each line at once, as it always did.
            native = new NativeChat(
                sessionId, agent, processes, e => Context.Record(sessionId, e), Changed, tracked?.Task,
                // On this door the words went once their line is on stdin and the harness has opened the conversation.
                wentOn: goOn is null ? null : () =>
                {
                    if (goOn.Sent(named: true)) _ = goOnDoor.WentOnAsync(sessionId, goOn, agent.Name);
                })
            {
                Shown = said => Context.TakeShown(sessionId, said),
            };
            native.Turns.Closing = () => Context.Closing;
            native.Turns.Unsent = count => closing.Unsent(sessionId, count);
            Context.Turned[sessionId] = native;
        }

        if (Context.Closing)
        {
            (chat?.Turns ?? native?.Turns)?.HoldForClose();
            if (processes.Close.Build is not null) closing.UpdateCuts.TryAdd(sessionId, 0);
        }

        // The person's words a conversation goes on with are its first turn, queued before any word said to it once it runs. Marked attempted
        // on the record before the turn carrying them is on the wire, so a shell cut off under it never sends them again.
        var first = goOn is null
            ? null
            : new ChatMessage(goOn.Ask.Prompt, [])
            {
                GoesOn = goOn.Ask, Attempting = () => SaidDoor.AttemptedAsync(service, sessionId, goOn.Ask.Words),
                // A go-on with another agent's findings has the answering turn's completion of its own.
                Answering = goOn.Answering,
            };
        if (first is not null)
        {
            // Their words, so the turn's end they start is an answer they wait on.
            answers.Asked(sessionId);
            (chat?.Turns ?? native?.Turns)?.Say(first);
        }

        // What the conversation consumed counts toward the account it ran as, as a driven session's does: at its end, at its high-water mark,
        // where the door reported one.
        void Measured(AcpUsage used) => usage.Record(new UsageEntry(
            sessionId, place.Name, agent.Name, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));

        // What its door says about its account's windows is kept for the next start's walk, as a driven session's is.
        void Said(JsonElement info)
        {
            try
            {
                harnesses.Said(agent.Name, selection.Profile, info, sessionId);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Nothing kept is nothing said: the account reads as unknown, as it did before the door spoke.
            }
        }

        // What the run is handed before its open: a native door's events and its conversation named, the protocol door's session held open.
        run!.HandStart(start! with
        {
            // The turn's end, once the record holds it, lets the next message go.
            Observed = native is null ? null : e =>
            {
                if (e.Kind == SessionEventKind.Turn) native.TurnEnded();
            },
            // Its harness opened the conversation its words went to, so they went: off the record by their ids.
            Named = goOn is null ? null : () =>
            {
                if (goOn.Named()) _ = goOnDoor.WentOnAsync(sessionId, goOn, agent.Name);
            },
            Protocol = chat is null ? null : held.Of(run, chat, sessionId, workTree, place.Posture ?? agent.Posture, meta, connectors, agent),
        });

        var watch = watching.WatchAsync(
            sessionId, run, transcript, onEnded, Measured, rules, mapper, chat, native, servers, Said,
            new ChatWatched(agent.Name, place.Keeps, goOn) { Skills = skills, Preamble = modeSaid }, actorRun, actorLog,
            // The native door's failed result read for the account's limit, cooling it as a protocol conversation's refused turn does.
            new NativeLimits(
                failure => agent.Read(harnesses.Adapters, failure, clock().GetUtcNow(), harnesses.Zone),
                (failure, error) => accounts.Limited(sessionId, agent, selection.Profile, failure.Words ?? "", config.CoolOff, error.Limit) is not null));
        Context.Watching[sessionId] = watch;
        _ = watch.ContinueWith(
            _ => Context.Watching.TryRemove(new KeyValuePair<string, Task>(sessionId, watch)), TaskScheduler.Default);
        // Its watch began: the words it goes on with may reach it.
        tracked?.TrySetResult(true);
        // Where its open was refused and the re-ask answered another session, the replacement opens once this run let its process, its files
        // and its turns go, in the same call as far as the person is concerned.
        if (pending is not null) _ = wiring.Of<ChatReplacement>().ReplaceAsync(watch, pending, sessionId, selection);
        // A native go-on whose harness ended before naming its conversation is tried once more as the same resume, its record still working,
        // once its watch let its process, its files and its turns go; never while closing.
        if (goOn is not null)
        {
            _ = AgainAsync(
                watch,
                () => RunAsync(
                    actor, sessionId, message, agent, selection, config, asked, onEnded, CancellationToken.None, new GoingOn(goOn.Record, goOn.Ask)),
                () => goOn.Ask.Refused is { Lost: true } lost
                    ? goOnDoor.LostAsync(goOn.Record, lost, agent.Name, config, onEnded, held: false, CancellationToken.None)
                    : Task.FromResult(new ChatStart(null, "")));
        }

        return new(sessionId, message);
    }

    /// <summary>
    /// The one retry of a native go-on, where its watch asked for it and the runner is not closing; else, where the wire refused the resume as
    /// lost, the new part it goes on in, once this run let its process, files and turns go, since that part starts in the same tree.
    /// </summary>
    private async Task AgainAsync(Task<bool> watch, Func<Task<ChatStart>> again, Func<Task<ChatStart>> lost)
    {
        try
        {
            var retry = await watch.ConfigureAwait(false);
            if (Context.Closing) return;
            await (retry ? again() : lost()).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Its own run concludes the record whatever fails there; a watch that threw has said its own failure.
        }
    }
}
