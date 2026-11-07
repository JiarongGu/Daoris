using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The opened record handed to its session: the words the open moved to it, the instruction composed from everything the run found
/// (the quest, the tree, the line it carries on, the process its work follows), the rules and servers it is handed, and the hold that
/// runs its process to the conclusion. The one stage of the run that does not go on: it ends in the run's own start, however the
/// session ended.
/// </summary>
[RunStage("hand", RunPhase.Handed, "open")]
internal sealed class HandToSession(IRunWiring wiring) : IRunStage
{
    public async ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct)
    {
        var context = wiring.Context;
        var service = context.Service;
        var home = context.Home;
        var config = context.Config;
        var state = run.State;
        var quest = run.Quest;
        var registry = run.Registry;
        var (start, fellBack, answerKept, written, switched) = (state.GoneOn.Start, state.GoneOn.FellBack, state.GoneOn.AnswerKept, state.GoneOn.Written, state.GoneOn.Switched);
        var (agent, selection, access, permissions, chosen) = (state.Agent, state.Selection, state.Access, state.Permissions, state.Chosen);
        var (handedQuest, stepRole, handedFrom, limitWalks) = (state.HandedQuest, state.StepRole, state.HandedFrom, state.LimitWalks);
        var tree = state.Tree;
        // The look the tree's stage opened does not follow the flow out of it: taken in again, git's guess at the line stays asked once.
        tree.OneLook.Reenter();
        var (opened, joinedIn, entryTree, carryingOn, answered, handsOn, inSync) = (tree.Opened, tree.JoinedIn, tree.EntryTree, tree.CarryingOn, tree.Answered, tree.HandsOn, tree.InSync);
        var (sessionId, keyed, fenced, continues) = (state.Opened.SessionId, state.Opened.Keyed, state.Opened.Fenced, state.Opened.Continues);
        var (carry, workTree, runBound, setUp) = (state.Carry, state.WorkTree, state.Bound, state.SetUp);
        var prior = carryingOn ? start.Resumes : null;
        var refusedHanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var endings = new RunEndings(wiring, run, sessionId, agent, workTree, state.Opened.LocksTree);
        try
        {
            // The words a session could not go on with went to this one: off that record by their ids, kept on the ask by the service, so
            // this start's instruction reads them among the person's words; and said in that session's conversation, where the person
            // wrote them. Where the open moved them, off this one by theirs, each shown here under its id and its files copied here first.
            var movedBy = MovedWords.Moving(keyed) ? keyed : null;
            IReadOnlyList<SaidWordView>? movedOn = null;
            // The record's delivery is written where each door's write of the first prompt returns, the moved words' `sent` first and
            // their take after it, so it goes with the take only where the fence is written; where the store refused it, the moved
            // words are not taken here and wait on the new session.
            var delivering = fenced is null ? carry : null;
            if (fenced is not null && movedBy is not null)
            {
                context.Output?.Append(sessionId, $"— the move's record could not be written ({fenced}), so the words moved to this session wait on it.");
            }
            else if (fellBack is not null)
            {
                movedOn = await wiring.GoOns.HandedOnAsync(written!, sessionId, fellBack, ct, switched: switched, opened: movedBy, delivery: delivering)
                    .ConfigureAwait(false);
            }
            // And those said to the session that asked while its quest waited go to its resume, the same way.
            else if (handsOn is not null)
            {
                movedOn = await wiring.GoOns.HandedOnAsync(
                        handsOn, sessionId, ContinueWhy.Of(ContinueWhy.Resumed), ct, resumed: true, opened: movedBy, delivery: delivering)
                    .ConfigureAwait(false);
            }

            // The record that is not fenced is ended by its phase here, not at the end of a run that lasts as long as its session does.
            // One that is fenced goes on to its door's write, which is where it delivers; a run that never writes ends it as it is let go.
            if (carry is not null && delivering is null) await carry.EndAsync().ConfigureAwait(false);

            var adapter = context.Adapters.Door(agent);
            // What it may read and write outside its tree: the rules it is handed, and the sentence every harness's instruction carries.
            var across = AcrossRules.Reach(config, registry, quest.To, start.Workspace);
            // What the cut-off session's line left in Daoris's own record: Daoris's brief of the whole line, every word it took and each
            // turn's outcome, read in the tree this start works in, which every carry-on is handed, since a cut-off for any reason loses
            // the same thread. And the note its agent left where it asked to hand over, the brief's own section as that agent's claim.
            var handedNote = carryingOn && handedFrom ? handedQuest?.Note : null;
            // A limit's move onto another account of the same agent carries the conversation itself where its door can, and is briefed only
            // where it cannot or the copy fails.
            var carryPlan = LimitCarried.Plan(context, adapter, prior, continues?.Why, selection, workTree);
            var conversation = carryPlan as CarryPlan.Carried;
            var brief = prior is null || conversation is not null
                ? null
                : await ContextBriefs.ComposeAsync(
                    service, context.Events, home, prior.Session, continues?.Why ?? HandOverWhy.CarryOn, conversation: false, note: handedNote,
                    config: config, tree: workTree, ct: ct).ConfigureAwait(false);
            // Bound to the part's record before it runs, so a restart that cuts it off before its first prompt resumes that conversation.
            if (conversation is not null) context.Conversations.Keep(sessionId, adapter.Id.Value, conversation.Conversation);
            if (prior is not null && continues is not null && conversation is not null) wiring.SayCarried(prior, sessionId, agent, selection.Profile, continues.Why, conversation);
            if (prior is not null && continues is not null && brief is not null)
            {
                wiring.SayMoved(prior, sessionId, agent, selection.Profile, continues.Why, brief, (carryPlan as CarryPlan.Briefed)?.Why);
            }
            // A start with no brief carries its line's course where any count is not zero, a quest that failed here before among them.
            var course = brief is null ? ContextBrief.Counted((await LineCountsRead.FactsAsync(service, home, sessionId, ct).ConfigureAwait(false)).Counts) : null;
            var elsewhere = prior is not null && AccountIdentity.OnAnotherAccount(prior, selection.Profile, limitWalks ? agent : null);
            // The servers the plugins hand this session, handed before its instruction is composed, which tells it the browser it is
            // handed. With Daoris's own browser brought up for a server that drives it, or that server left out and the transcript told why.
            var (servers, browserNoted, drivesBrowser, browserDrivers) =
                await SessionServers.HandNotedAsync(home, context.Servers, context.Browser, context.Log, ct).ConfigureAwait(false);
            var browserNotice = browserNoted?.Note;
            // What waited on the ask's channel for this part's line of work: read for its instruction, never holding the start, and its
            // entry's mark moved once the record works, not before, so a start that fails hands it on.
            var channelDoors = new ServiceChannelDoors(service);
            var channelHeld = start.Channel is { } line ? await ChannelReach.HandAsync(channelDoors, line, sessionId, ct).ConfigureAwait(false) : null;
            // A quest whose work cycle holds `quest.close` names its graph for the take as its first session opens, so the take binds it
            // in its own write, and a bound quest's session says `ready`, never `done`. Named for this session, whose take alone binds it,
            // the service judging it again at the take: the graph its run is bound to, as the bind kept it.
            var closure = await ClosureBinding.AtStartAsync(
                service, config, quest, start.Workspace ?? RemoteTarget.DefaultWorkspace,
                RemoteTarget.Effective(wiring.Variable, home).ContainsKey(RemoteTarget.Workspace(start.Workspace)), home, ct,
                session: sessionId, run: runBound.Binding)
                .ConfigureAwait(false);
            // The process its work follows: read for the landing a session in a tree of its own is told, and for the checks its run takes
            // wherever its repository sets them or its run is bound to a named workflow. Read from the binding the bind came to, so the
            // first session takes its version's, and neither the service's quests nor the run's file is read again.
            var process = context.Trees.Holds(workTree) || SessionChecks.For(config, quest.To) is not null || runBound.Binding is { IsCurrent: false }
                ? WorkflowProcesses.Bound(home, config, quest.To, start.Workspace, runBound.Run, runBound.Binding)
                : null;
            // What its run's checks tell and hand it, its words composed from the declaration, its deny rules where its agent takes
            // Daoris's rules; and those checks kept by its id for `run-heavy`, let go as its run ends, a graph version's `callMinutes`
            // beside them for its long-call watch.
            var checks = SessionChecks.Of(config, quest.To, process);
            var heavy = HeavyTold.Hand(workTree, home, quest.To, checks, adapter.Can.TakesSettings);
            var checksKept = SessionChecks.Hold(home, sessionId, checks, CallBound.MinutesOf(process));
            using var checksHeld = new Disposer(() => SessionChecks.LetGo(checksKept));
            var asking = await StartTargets.WithAskWordsAsync(SessionTarget.ForQuest(quest, workTree, service.BaseUrl), sessionId, service, ct).ConfigureAwait(false);
            // A carry-on's moved words are taken once its first prompt is written, and the take is what keeps the person's among them on
            // the ask this instruction reads, so the instruction is handed them here, as the take will have kept them.
            if (delivering is not null && movedBy is not null && movedOn is { Count: > 0 } && asking.Words is AskWords askRead)
            {
                asking = asking with { Words = askRead.WithMoved(movedOn, sessionId, quest.Id) };
            }

            var target = StartTargets.WithAccess(StartTargets.WithLanguage(asking, config, start.Workspace), access) with
            {
                // Its standing block, its tree's facts with its repository's routing and the person's choices on this machine, the
                // memory a carry-on's line read, and the file it goes through where the whole first prompt cannot carry it.
                Block = StandingBlocks.OnMachine(asking.Block ?? new StandingFacts { Repository = quest.To }, ParticipantWork.Build, config, registry, start.Workspace) with
                {
                    Checkouts = across.Reads.Count > 0,
                    Memory = brief?.Memory ?? [],
                    File = StandingBlocks.FileOf(home, sessionId),
                },
                // The mode its repository's permission prompts name on its harness, on either door.
                Access = permissions,
                // What the entry the walk took names, carried on every run; a continuation keeps its record's, and its choice with it,
                // so a part carried on from Build's second choice says it is that one.
                Asked = chosen?.Asked ?? (start.Resumes is { } carried ? carried.Asked : ActorAsked.Before(adapter.Id.Value)),
                Choice = chosen?.From ?? start.Resumes?.Choice,
                Browser = InAppBrowserServers.Told(browserDrivers),
                ReadsAcross = across.Reads,
                WritesAcross = across.Writes,
                // Named on its spawn as on its connector, so what it publishes says it did.
                Session = sessionId,
                Ready = closure?.Bound == true,
                Answered = answered,
                // What cut the last session off, and what it left uncommitted, read by the driver, because the session may not run
                // `git status` itself. An open quest's start, handed the words its session could not go on with, takes it as any first
                // start does: it is not yet anyone's. A stand-down brought forward is told the quest is this machine's again, never its
                // stop's words that its work would double someone else's.
                CutOff = carryingOn && quest.Status != "Open"
                    ? start.BroughtForward is { } forward ? Driver.BroughtForwardTold(forward) : start.Resumes!.Note ?? "it ended before closing the quest."
                    : null,
                // An open quest carried on in the tree its failed session committed work in is told that record's words, to take the
                // quest and finish the work rather than build it again.
                Retried = carryingOn && start.CarriesWork is > 0 ? start.Resumes!.Note ?? "it ended before closing the quest." : null,
                // A further part gone into its session's tree is told what the part before it left uncommitted there.
                InFlight = carryingOn || joinedIn is not null ? await WorkingTree.UncommittedAsync(workTree, ct: ct).ConfigureAwait(false) : [],
                // The harness the part before it ran on and the models its door reported, so a carry-on of another maker credits what that
                // part left uncommitted.
                LeftBy = carryingOn ? wiring.LeftBy(start.Resumes!) : null,
                // What its start did against the line in the tree it goes on in.
                InSync = inSync?.Told,
                // Its repository's remotes Daoris holds, so it does not try them: read after its start's own fetch. And, where the remote
                // helper is laid out for it, what `git remote -v` shows under the rewrite; or, on a git older than the doors need, that its
                // remote acts are not Daoris's.
                Git = GitHolds.Told(
                    GitHolds.OfRepository(home, quest.To, RemoteTarget.Workspace(start.Workspace)),
                    rewritten: GitWiring.DoorPath(home, GitWiring.RemoteHelper) is not null,
                    origin: GitWiring.DoorPath(home, GitWiring.RemoteHelper) is null
                        ? null
                        : await GitRemotes.AddressOf(workTree, "origin", ct: ct).ConfigureAwait(false),
                    older: await GitWiring.OlderAsync(home, ct).ConfigureAwait(false)),
                // What its tree's open copied of the checkout's dependencies, so it skips the `npm ci` that would delete them. And what a
                // tree made again copied the same way, where the session goes on in one.
                Dependencies = opened?.Dependencies ?? (tree.PriorStands?.Told, tree.EntryStands?.Told) switch
                {
                    ({ } remadePrior, { } remadeEntry) => $"{remadePrior} {remadeEntry}",
                    ({ } remadePrior, _) => remadePrior,
                    (_, { } remadeEntry) => remadeEntry,
                    _ => null,
                },
                // Its repository's heavy gates, once at its end through `run-heavy`, where its PATH finds the verb, under its run's checks.
                Heavy = heavy.Words,
                GrewFrom = opened?.GrewFrom,
                // The parts before it on its session's line, and how its tree stands to theirs.
                Parts = start.Entry is { } entry ? await wiring.PartsAsync(entry, joinedIn, entryTree, opened, quest.To, ct).ConfigureAwait(false) : null,
                // What waited on the ask's channel for its line of work, its mark moved once it works.
                Channel = channelHeld,
                // How its work will land, for a session in a tree of its own, the only kind the review's press reaches. A chain's step is
                // told its chain's branch, which its done moves on. Under a named workflow its run's bound version's landing step says it.
                LandsOn = context.Trees.Holds(workTree)
                    ? await context.Trees.PlanAsync(
                        workTree, await StartLanding.SubjectAsync(sessionId, quest, id => service.FindQuestAsync(id, ct), ct).ConfigureAwait(false), ct,
                        process!)
                        .ConfigureAwait(false)
                    : null,
                // How it commits: the rule standing for its repository, told with the landing to a session in a tree of its own on every
                // form, so its agent and the repository's own rules do not stop each other; the person's checkout is told nothing.
                Commits = context.Trees.Holds(workTree) ? CommitRules.Resolve(config, quest.To, start.Workspace) : null,
                // The person's answer, when the session before parked to ask them.
                PersonSaid = carryingOn ? SaidWordView.PersonsJoined(start.Resumes!.Waiting) : null,
                // A resume's words said to the session that asked while it waited, each under its id; a carry-on a cooling account's
                // switch made, Daoris's own words it carries; and the words the open moved here, as this record holds them.
                Handed = handsOn is not null
                    ? [.. (movedOn ?? handsOn.Waiting).Where(DriverContinuations.HandedToResume)]
                    : switched ? [.. (movedOn ?? written!.Waiting).Where(word => word.Daoris && DaorisWords.NewPartCarries(word))] : [],
                // The person's stop, which they released with Try again: told so, never that it is cut off.
                Released = carryingOn && start.Resumes!.PersonStopped && !handedFrom,
                // The quest is handed over from that session: told so, with the agent it ran on.
                BroughtForward = carryingOn && quest.Status != "Open" && start.BroughtForward is not null,
                HandedOver = carryingOn && handedFrom ? start.Resumes!.Adapter ?? "another agent" : null,
                Brief = brief,
                Course = course,
                // The note its agent left where it asked to hand over, told as that agent's claim; the brief carries it.
                HandedNote = handedNote,
                // Another agent's accounts are not this agent's: a hand-over says no account is cooling for it to be told of.
                AccountChanged = elsewhere && !handedFrom && prior!.Limit
                    && LineAccount.OwnHold(context.Harnesses.CoolingOf(agent, prior.Profile, prior.ModelOn(agent))) == NextHold.Cooling,
                // A set-up step is told its environment as the rule declares it now, and its account says where that rule is set.
                SetUp = setUp,
                SetUpFrom = setUp is null ? null : ReviewRules.Resolve(config, quest.To, start.Workspace)?.Source,
            };
            tree.OneLook.Dispose();
            var carriedAsk = conversation is null || prior is null || continues is null
                ? null
                : LimitCarried.Ask(conversation, CarriedNotes.From(prior.Session, prior.Adapter ?? agent, prior.Profile, continues.Why, conversation), movedOn);

            // The creating sentence, on the record while the session runs: where it grew from and what a fresh tree does not hold. The
            // conclusion's note replaces it.
            var startingNote = StartSentence.Of(
                opened, quest, answered, tree.ResumedIn, carryingOn, start.Resumes, written, elsewhere, fellBack,
                handedOver: carryingOn && handedFrom, walked: carryingOn && handedFrom && state.PassedAccount is not null,
                joinedSession: joinedIn is not null ? start.Entry?.Into?.Session : null, broughtForward: start.BroughtForward is not null,
                carriesWork: carryingOn && start.CarriesWork is > 0);
            await service.AdvanceAsync(sessionId, "starting", note: startingNote?.Note, ct: ct, parts: startingNote?.Parts).ConfigureAwait(false);

            // The answer beneath the question it answers: the session that asked ends with the person's words in its own record, where
            // they read the question. A resume tried already put them there; words to a session that had ended are in its
            // conversation already, where the person wrote them.
            if (carryingOn && !answerKept && SaidWordView.PersonsJoined(start.Resumes!.Waiting) is { Length: > 0 } answeredWith && written is { Parked: true })
            {
                context.Events.Keep(start.Resumes.Session, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = answeredWith }, say: null);
            }

            var transcript = DaorisHome.Entry(home, HomeEntry.Sessions, $"{sessionId}.log");
            Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

            // The protocol door carries the servers on the wire below; a pipe-door harness that takes a file at spawn is handed one under
            // Daoris's home, and the file goes when the session does.
            context.Hooks?.Log.Served(context.Catalog, sessionId, servers);
            var connectors = ConnectorPlans.For(sessionId, start.Workspace, servers);
            context.Runs.Live[quest.Id] = sessionId;
            using var live = new Disposer(() => context.Runs.Live.TryRemove(quest.Id, out var _));

            var conclusion = new RunConclusion(wiring, run, endings, adapter, target, transcript);

            // A carried resume the door refuses before any prompt is the brief's start instead, the copy taken out of the account's home.
            var arranged = Arrange(target, conversation, brief);
            try
            {
                return await HoldAsync(arranged, target, carriedAsk);
            }
            catch (CarriedResumeRefused) when (conversation is not null && prior is not null && continues is not null)
            {
            }
            finally
            {
                arranged.Skilled.Dispose();
            }

            LimitCarried.Withdraw(context, adapter, selection, workTree, conversation!);
            var fallback = await ContextBriefs.ComposeAsync(
                service, context.Events, home, prior!.Session, continues!.Why, conversation: false, note: handedNote, config: config, tree: workTree, ct: ct)
                .ConfigureAwait(false);
            wiring.SayMoved(prior, sessionId, agent, selection.Profile, continues.Why, fallback, LimitCarry.Refused);
            var briefed = target with { Brief = fallback, Course = null, Block = target.Block! with { Memory = fallback.Memory } };
            arranged = Arrange(briefed, null, fallback);
            try
            {
                return await HoldAsync(arranged, briefed, null, again: true);
            }
            finally
            {
                arranged.Skilled.Dispose();
            }

            (ProcessStartInfo Info, string? Notice, string? Served, (string? File, object? Meta, HandedSection? Handed) Rules, IDisposable Skilled) Arrange(
                SessionTarget held, CarryPlan.Carried? carried, ComposedBrief? composed)
            {
                var (spawn, notice) = wiring.Prepare(adapter, held, selection, carried is null ? null : LimitCarried.Prepared(context, adapter, held, carried));
                if (carried is not null) LimitCarried.Compact(spawn, carried);
                var served = adapter.ServerHanding()?.Hand(spawn, home, connectors);
                // The skills plugins hand sessions, composed under the home, gone with the session.
                var (skills, skillPlugins) = SpawnSkills.Hand(context.Adapters, adapter, spawn, home, sessionId, context.Catalog, PluginServerTo.Sessions);

                // What this session may do: the rules composed for its circle and repository, handed over as the harness's own settings
                // tier. A carry-on reads the kept folders of its line's parts, whose files its brief names, and the folder its standing block
                // went through, where it went through one.
                var blockFolder = TargetPrompt.Filed(TargetPrompt.ForStart(held, context.Adapters, adapter)) ? StandingBlocks.FolderOf(home, sessionId) : null;
                var ruled = wiring.HandRules(
                    adapter, spawn, sessionId, start.Workspace, quest.To, workTree, held.AttachmentsDirectory, across: across, plugins: skillPlugins,
                    continued: blockFolder is null ? composed?.Kept : [.. composed?.Kept ?? [], blockFolder], heavy: heavy);
                return (spawn, notice, served, ruled, new Disposer(() => SpawnFiles.RemoveFolder(skills)));
            }

            async Task<StartRun> HoldAsync(
                (ProcessStartInfo Info, string? Notice, string? Served, (string? File, object? Meta, HandedSection? Handed) Rules, IDisposable Skilled) laid,
                SessionTarget held, ResumeAsk? ask, bool again = false) =>
            await wiring.Holds.HoldAsync(
                "build", adapter, laid.Info, held, sessionId, transcript, workTree, Driver.JoinNotices(laid.Notice, browserNotice), laid.Rules, laid.Served,
                // `actor` stays the seeded id; the role the start took and its revision go on the binding. A continuation keeps its record's.
                taken: start.Resumes is null ? wiring.RoleTaken(start, stepRole) : null,
                // A driven session takes no person's line, on either door: it is handed its whole quest at once, the pipe door gives it
                // no stdin, and the protocol door's stdin carries the driver's own frames.
                refusesInput: Driver.TakesNoMessages(quest),
                ct: ct,
                preamble: browserNotice,
                handedPlan: connectors,
                drivesBrowser: drivesBrowser,
                said: wiring.Said(adapter, selection, sessionId), limits: wiring.NativeLimit(adapter),
                // A carried conversation is resumed on the new account, its first prompt Daoris's word.
                resume: ask,
                // The refused try moved the record to working already.
                movedFirst: again,
                // A native run holds what the person says while it works, and goes on with it in its own conversation.
                goesOnAs: selection,
                stopMade: stop => endings.StoppedFirst = stop,
                // The part works: what it is handed from the ask's channel is acknowledged by its id.
                working: channelHeld is null ? null : () => _ = ChannelReach.HandedAsync(channelDoors, channelHeld, CancellationToken.None),
                // A turn that ends on the session's own background work, holding its quest as a park would, is not a question. Read from
                // the quest as the conclusion reads it, by the conclusion's own rule.
                waitsOnBackground: SessionWaits.OnBackground(service, quest, sessionId, resumed: start.Resumes is not null),
                // A turn that ends with the quest done and the tree uncommitted is carried on, not concluded.
                carriesOn: SessionWaits.CarriesOn(service, quest, workTree, resumed: start.Resumes is not null, () => context.Processes.StopMade(sessionId) is not null),
                // Where the first prompt's write returns, the carry-on's moved words are `sent`, then taken.
                written: delivering is null ? null : delivering.Written,
                // Its agent's refusal of the entry's model at the open is re-asked; a new start's walk may answer a later entry on the same
                // agent and account, set on this session. A continuation keeps its asked choice, so its record ends. A handed quest's start
                // asks its named agent's own choice, so a refusal of that entry's model is re-asked on that agent's later entries and its
                // default, on the same account.
                offers: new OpenOffers(
                    selection.Version, selection.Profile,
                    chosen is { } walkedEntry && handedQuest is null && (start.Resumes is null || limitWalks)
                        ? _ => wiring.ReaskAsync(start, registry, walkedEntry, selection, ct, stepRole)
                        : chosen is not null && handedQuest is not null
                            ? refusal => wiring.ReaskHandedAsync(start, agent, selection, refusal, refusedHanded, ct, stepRole)
                            : _ => Task.FromResult<(ActorAsked Asked, string Choice)?>(null)),
                // A driven quest session is tried again in the same conversation on a transient failure, asking before each try whether
                // its quest still wants a turn, against the quest as this start is planned.
                transientWanted: retryCt => TransientRetries.WantedAsync(service, quest.Id, sessionId, quest.Status, quest.Awaits, retryCt),
                conclude: (exitCode, used, turnFailed, ended) =>
                    ask is not null && endings.StoppedFirst is null && CarriedResumeRefused.Of(ask, adapter, exitCode) is { } refusal
                        ? Task.FromException<StartRun>(new CarriedResumeRefused(refusal))
                        : conclusion.ConcludeAsync(exitCode, used, turnFailed, ended, ct)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return await endings.ClosedAsync().ConfigureAwait(false);
        }
        catch (Exception) when (endings.StoppedFirst is not null)
        {
            return await endings.StoppedAsync().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            return await endings.FailedAsync(error).ConfigureAwait(false);
        }
        finally
        {
            await endings.LetGoAsync().ConfigureAwait(false);
        }
    }
}
