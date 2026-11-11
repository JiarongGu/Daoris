using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The intake half of the loop: an ask the declarations left to a person is answered by a
/// SESSION this driver opens in the circle's room — the driver's brain is a session, not a model.
/// </summary>
/// <remarks>
/// <para><b>The same spawn as a quest's, on the driven capture path</b>: an intake is one
/// turn — the ask is its target — so it is framed as one prompt on either door, never fed raw lines the
/// way a chat's stdin is.</para>
///
/// <para><b>Observed, never self-reported</b>: the record moves on the exit code and on what
/// became of the ASK. Published onto it by the intake is done; a clean exit that published nothing is
/// the intake asking the person, which parks the record until the person answers the ask.</para>
///
/// <para><b>It publishes; it never edits</b>. Its room is Daoris's own directory, its allow-list
/// reads the family and publishes, and it holds no repository's tree.</para>
/// </remarks>
/// <param name="driver">
/// The driver whose run machinery an intake goes through: the spawn's preparation, the connector, the rules handed,
/// the hold of the process and the account sayings, as <see cref="DriverOpinions"/> does.
/// </param>
internal sealed class DriverIntakes(LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    /// <summary>
    /// What an intake with no connector can and cannot do, said on its transcript, and the two commands that lay one
    /// down in the words a session is told: the install's first, as <see cref="KnowledgeConnector.Candidates"/>
    /// ranks them.
    /// </summary>
    internal static string NoConnectorForIntake =>
        $"— no {KnowledgeConnector.ExecutableName} on this machine, so this intake has no connector: it can decide, but "
        + "cannot publish what it decides, and will end by asking you. "
        + "`npm run publish:desktop -- --to <install> --service` lays one beside the application, and "
        + "`npm run publish:service -- --install` lands one in the home's `bin/`.";

    /// <summary>
    /// Why a person's line is refused for a running intake, and where their answer goes instead — the
    /// sentence the bridge carries verbatim, so a stale page is told the truth rather than "it ended".
    /// </summary>
    internal static string TakesNoMessages(string ask) =>
        $"the intake for ask #{ask} takes no messages: an intake is one turn, and where the "
        + "declarations do not settle it, it asks you by parking. The answer is on the ask — publish it "
        + "to a repository, or close it.";

    /// <summary>
    /// The asks due an intake this tick: none unless a harness is named for it; otherwise the oldest
    /// unserved ask in each circle, as many as the slots allow.
    /// </summary>
    public async Task<IReadOnlyList<AskView>> IntakesDueAsync(int slots, List<string> events, CancellationToken ct)
    {
        var config = _context.Config;
        // Intake is read through its actor, so the drain's *off* reaches an order the person set; an order that
        // does not read holds every intake with its sentence.
        string? agent;
        try
        {
            agent = ActorChoices.FirstAgent("intake", config);
        }
        catch (ActorUnreadableException unreadable)
        {
            events.Add(_context.Said.Retry("intake", $"intake  {unreadable.Message}"));
            return [];
        }

        if (agent is not { Length: > 0 }) return [];

        IReadOnlyList<AskView> asks;
        try
        {
            asks = await _context.Service.AsksAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            // A host older than the asks answers no ask door; the quests still drive, and this says why
            // no ask is being answered rather than looking like a machine with none.
            events.Add(_context.Said.Retry("intake", $"intake  the asks could not be read, so none is answered this tick: {error.Message}"));
            return [];
        }

        // Proposed and never served: what the declarations left to a person and no intake has had.
        // One per circle, because the room runs one intake at a time — the OLDEST in each, which the
        // service's newest-first order puts last.
        // A paused ask's intake does not start: nothing of a paused ask starts, and the next ask in its
        // circle is due in its place. Said by the ask's page, never a line each look.
        // Never an ask whose receiver the person named — its quest deleted leaves it standing with none, and an
        // intake would route it by its proposal to repositories nobody named. The service's own refusal says the same.
        var due = asks
            .Where(ask => ask.State == "Proposed" && ask.Intake is null && IntakePrompt.Answers(ask) && config.PausedAsk(ask.Id) is null)
            .GroupBy(ask => ask.Workspace, StringComparer.OrdinalIgnoreCase)
            .Select(circle => circle.Last())
            .ToList();
        if (due.Count == 0) return [];

        if (slots <= 0)
        {
            // Sitting must say why — for an ask as for a quest.
            events.Add(_context.Said.Progress(
                "intake",
                $"intake  {string.Join(", ", due.Select(ask => $"ask #{ask.Id}"))} waiting: the concurrency cap "
                + $"({config.Cap}) is spent — it frees as sessions finish."));
            return [];
        }

        return [.. due.Take(slots)];
    }

    /// <summary>
    /// An intake's start plan: Intake's effective order, read once for this start, walked as a
    /// quest's first start walks Build's, choice by choice, each choice's accounts by the existing walk before the next, judged for its model,
    /// on accounts not kept for conversations. An agent this machine does not know is passed <c>unavailable</c> before any account
    /// is asked; with none that can run, the start holds on Intake's wait sentence, the soonest cool-off beside it. Its re-ask at the open
    /// walks the same order as a probe, a later entry of the same agent on the same account set on the open session and
    /// anything else ending the record <c>choice-not-offered</c>, its ask let go.
    /// </summary>
    private StartPlan PlanOf(AskView ask, DriverConfig config) =>
        new(ParticipantWork.Intake, config, _context.Harnesses, _context.Adapters, _driver.Passed, catalogue: RoleCatalog.ReadHome(_context.Harnesses.Home), place: new RolePlace(ask.Workspace))
        {
            Workspace = ask.Workspace,
            Kind = StartKind.Driven,
            Subject = $"ask:{ask.Id}",
            Named = ("ask", ask.Id),
            Log = _driver.Log,
            Staff = _context.Harnesses.StaffFor(_context.Home),
        };

    /// <returns>
    /// The console line, whether a session actually opened, and — when one ENDED here — what it ended
    /// as. A park is not an ending: the record waits for the person, and the tick that finds it parked
    /// is what tells them, once, from the one place parks are seen.
    /// </returns>
    /// <param name="onOpened">Called once the intake's record is open, and never for one that held or met a refusal.</param>
    public async Task<StartRun> RunIntakeAsync(
        AskView ask, List<TrustHold> untrusted, Action onOpened, CancellationToken ct)
    {
        var service = _context.Service;
        var config = _context.Config;
        var home = _context.Home;
        var output = _context.Output;
        // Intake's effective order, read once for this start by its plan. The gate above found it not off.
        var named = $"ask #{ask.Id} in {ask.Workspace}";
        // The hold's own sentence beside its line, as a quest's hold carries it: what a wait the intake is among says.
        StartRun Hold(string why) => new($"held  intake for {named}: {why}", false, Held: why);
        StartPlan plan;
        try
        {
            plan = PlanOf(ask, config);
        }
        catch (DriverException held)
        {
            // The catalogue holds the start where no role takes intake here, its role removed, or a record does not read.
            return Hold(held.Message);
        }

        var order = plan.Order;
        var adapterName = order[0].Agent;

        // Asked BEFORE the record, like every hold a driven spawn asks: a record for a spawn that could
        // never happen would mark the ask served and explain nothing.
        AgentDoor? one = null;
        SettledSelection settled;
        try
        {
            // An order of one resolves its agent before any account is asked, a name this machine has no agent of
            // held on its sentence, as is anything else its one account walk throws.
            if (order.Count == 1) one = _context.Adapters.Door(adapterName);
            settled = await plan.ResolveAsync(ct).ConfigureAwait(false);
        }
        catch (DriverException error) when (order.Count == 1)
        {
            return Hold(error.Message);
        }

        // A cooling account's hold carries its cool-off, and the accounts it passed not signed in, as a quest's does; a
        // walked order's carries Intake's wait sentence and the soonest cool-off of any choice.
        var selection = settled.Selection;
        if (!selection.Allowed) return Hold(selection.Refusal!) with { Cooling = selection.Cooling, SignedOut = selection.SignedOut };

        var chosen = settled.Chosen!;
        var passes = settled.Passes;
        var adapter = settled.Adapter ?? one!;
        if (settled.Adapter is { } taken) adapterName = taken.Id.Value;

        // The room, written from the circle's declarations as they stand now.
        string room;
        IReadOnlyList<string> routedByIntake;
        try
        {
            var declarations = await service.DeclarationsAsync(ask.Workspace, ct).ConfigureAwait(false);
            // The repositories whose setup takes the intake's route without the person's accept.
            routedByIntake = IntakePrompt.RoutedByIntake(config, ask.Workspace, declarations.Select(declaration => declaration.Repository));
            // The workspace's kinds per repository, read from the choices as they stand now, like the declarations.
            room = IntakeRoom.Prepare(
                home, ask.Workspace, declarations,
                IntakeKinds.Read(home, config, ask.Workspace, declarations.Select(declaration => declaration.Repository)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException
                                          or HttpRequestException or System.Text.Json.JsonException)
        {
            return Hold($"its room could not be written — {error.Message}");
        }

        // The same trust question a driven spawn asks: in a room the harness has not been
        // told to trust, the room's allow-list is inert — so it is asked only where the rules handed
        // over at spawn would not let the intake publish, exactly as for a quest's session.
        if (adapter.Home is { TrustFile: { Length: > 0 } trustFile } doorHome
            &&!_driver.HandedConnector(adapter, ask.Workspace, repository: null, "quest_publish"))
        {
            var trustPath = ClaudeTrust.PathFor(trustFile, selection.ProfileHome, doorHome.Variable);
            if (ClaudeTrust.Accepted(trustPath, room) == false)
            {
                // The room is Daoris's own folder under the home, and trusting it is still the person's
                // grant: the fact goes to the screen beside the sentence, as a quest's does.
                lock (untrusted) untrusted.Add(new TrustHold(room, trustPath, Ask: ask.Id, Own: selection.ProfileHome is null));
                return Hold(ClaudeTrust.Refusal(room, selection.ProfileHome is null ? null : selection.Profile));
            }
        }

        var (sessionId, message) = await service
            .OpenIntakeAsync(ask.Id, adapterName, room, selection.Version, selection.Profile, ct)
            .ConfigureAwait(false);
        if (sessionId is null)
        {
            return new StartRun($"refused  intake for {named}: {message}", false);
        }

        // Marked as this process's start until its spawn's marker replaces it.
        _context.Processes.MarkOpening(sessionId);
        onOpened();

        // An intake on another account of the order says so, as a quest's start does.
        RotatedOpening.Say(service, _context.Events, sessionId, adapterName, selection, carried: null);
        // Where a later choice of Intake's order ran, its actor line says which and why.
        RotatedOpening.SayActor(_context.Events, sessionId, "intake", chosen, passes);
        await RotatedOpening.SayStaffAsync(_context.Events, _context.Harnesses.Staff(), service, null, sessionId, selection, "intake").ConfigureAwait(false);

        SessionStop? stoppedFirst = null;

        try
        {
            var scope = IntakeRoom.Scope(ask.Id, sessionId);
            // The plugins' servers — a ticket behind a sign-in is read through Daoris's own browser, brought up here for a server
            // that drives it, or that server left out and the transcript told why. Handed before the instruction is
            // composed, which tells the intake the browser it holds.
            var (servers, browserNoted, drivesBrowser, browserDrivers) =
                await SessionServers.HandNotedAsync(
                    _context.Home, _context.Servers, _context.Browser, _context.Log, ct,
                    PluginToolsHand.Entry(
                        _context.Home,
                        new PluginToolsAt(sessionId, new PluginPlace(null, ask.Workspace), SessionLanguages.OfWorkspace(config, ask.Workspace)?.Code is "zh" ? "zh" : "en"),
                        PluginToolsHand.Program(_context.Home))).ConfigureAwait(false);
            var browserNotice = browserNoted?.Note;
            var target = new SessionTarget(
                QuestId: "", ask.Sentence.Split('\n', 2)[0].Trim(), ask.Sentence, ask.Asker ?? "the person",
                $"ask #{ask.Id}", room, service.BaseUrl)
            {
                Links = ask.Links,
                Attachments = ask.Attachments,
                Ask = ask.Id,
                Session = sessionId,
                // The entry's model and effort, carried on either door, and the choice its record keeps.
                Asked = chosen.Asked,
                Choice = chosen.From,
                // Its workspace's session language: an intake answers a workspace's ask, in no repository.
                Prompt = IntakePrompt.Compose(
                    ask, SessionLanguages.OfWorkspace(config, ask.Workspace), InAppBrowserServers.Told(browserDrivers), routedByIntake),
                // In no repository, its standing block is its workspace's part alone.
                Block = new StandingFacts { Workspace = ask.Workspace },
            };
            var (info, harnessNotice) = _driver.Prepare(adapter, target, selection);

            await service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);

            var transcript = DaorisHome.Entry(home, HomeEntry.Sessions, $"{sessionId}.log");
            Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

            // 🔴 The connector is handed, never assumed: a chat is given none, and
            // the room has no `.mcp.json` of its own. The protocol door carries it on the wire below;
            // the pipe door takes a file under the home, the knowledge host first, with the ask and
            // this session in its environment so what it publishes is asked by the ask.
            string? handed = null;
            string? preamble = null;
            _context.Hooks?.Log.Served(_context.Catalog, sessionId, servers);
            if (adapter.ServerHanding() is { } handing)
            {
                var connector = _driver.Connector(sessionId, scope);
                if (connector is null) preamble = NoConnectorForIntake;
                handed = handing.Hand(info, home, ConnectorPlans.For(sessionId, ask.Workspace, connector is null ? servers : [connector, .. servers]));
            }

            // The skills plugins hand sessions, composed under the home, gone with the session.
            var (skills, skillPlugins) = SpawnSkills.Hand(
                _context.Adapters, adapter, info, home, sessionId, _context.Catalog, PluginServerTo.Sessions, OwnTools.Machine(home, info));
            using var skilled = new Disposer(() => SpawnFiles.RemoveFolder(skills));

            // What the intake may do: its circle's rules and the machine's — it serves an
            // ask, and belongs to no repository. Its tree is its room, which the guard holds it to.
            var rules = _driver.HandRules(
                adapter, info, sessionId, ask.Workspace, repository: null, tree: room, kept: target.AttachmentsDirectory,
                job: IntakeRoom.Allowed, plugins: skillPlugins);

            return await _driver.Holds.HoldAsync(
                "intake", adapter, info, target, sessionId, transcript, room, Driver.JoinNotices(harnessNotice, browserNotice), rules, handed,
                // 🔴 One turn takes no messages, on either door: the pipe door gives it no stdin,
                // and the protocol door's stdin is the driver's own frames — a person's line written
                // there would land in the middle of the JSON-RPC stream.
                refusesInput: TakesNoMessages(ask.Id),
                ct: ct,
                scope: scope,
                preamble: Driver.JoinNotices(preamble, browserNotice),
                handedPlan: ConnectorPlans.For(sessionId, ask.Workspace, servers),
                drivesBrowser: drivesBrowser,
                // The intake's own authority: no try once the person stopped it, or its ask closed, got deleted or gained quests.
                transientWanted: TransientOwners.Intake(
                    _context.Processes, sessionId, retryCt => service.FindAskAsync(ask.Id, retryCt)!, ask.Quests.Count),
                said: _driver.Said(adapter, selection, sessionId), limits: _driver.NativeLimit(adapter),
                stopMade: stop => stoppedFirst = stop,
                // Its agent's refusal of the entry's model at the open is kept as the account's reading and
                // re-asked within this start, the order read once: a later entry of the same agent on the same account is set on this
                // session; anything else ends the record `choice-not-offered` below, its ask let go in the same write.
                offers: new OpenOffers(
                    selection.Version, selection.Profile,
                    order.Count > 1 ? _ => plan.ReaskAsync(chosen, selection, ct)
                        : _ => Task.FromResult<(ActorAsked Asked, string Choice)?>(null)),
                conclude: async (exitCode, used, turnFailed, ended) =>
                {
                    if (used is not null)
                    {
                        _context.Usage?.Record(new UsageEntry(
                            sessionId, $"ask #{ask.Id}", adapter.Id.Value, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
                    }

                    // Refused at the open, before any prompt. One write ends the record `choice-not-offered` and lets the
                    // ask go, only where its binding names this session, so the next look picks it as an ask never served and its walk
                    // passes the refusing account by the reading kept; its process has stopped, so its one count is given back.
                    if (!_context.Processes.WasStopRequested(sessionId) && ended.NotOffered is { } notOffered)
                    {
                        var refusedEnd = Observation.ChoiceNotOffered(adapter.Id.Value, notOffered);
                        await service.EndRefusedIntakeAsync(sessionId, refusedEnd, ct).ConfigureAwait(false);
                        _context.Harnesses.Release(selection.Number, sessionId);
                        return new StartRun(
                            $"{refusedEnd.State}  intake {sessionId} ({named}): {refusedEnd.Note}", true,
                            new SessionEnded(sessionId, $"ask #{ask.Id}", refusedEnd.State, false, refusedEnd.Note,
                                Adapter: adapter.Id.Value, Account: selection.Profile));
                    }

                    // What became of the ask is the observation — the intake's own account of it is not.
                    var after = await service.FindAskAsync(ask.Id, ct).ConfigureAwait(false);
                    var byPerson = _context.Processes.WasStopRequested(sessionId);
                    var conclusion = byPerson
                        ? SessionConclusion.Of("stopped", _context.Processes.StopNote(sessionId) ?? Observation.Stopped)
                        : exitCode is int code
                            ? IntakeObservation.Conclude(code, ask.Quests.Count, after, turnFailed)
                            : SessionConclusion.Of("failed", _driver.Watch.EndedMinutes(sessionId) is { } quietMinutes
                                ? Observation.Quiet(quietMinutes)
                                : Observation.TimedOut(config.TestBound ?? 0));
                    conclusion = _driver.AccountRefused(conclusion, adapter, selection, ended);
                    // A refused sign-in reads its account signed out, as a driven session's does.
                    conclusion = _driver.AccountSignedOut(conclusion, adapter, selection, turnFailed, ended);
                    // An account's limit cools the account the intake ran as, as a driven session's does.
                    (conclusion, var limited) = _driver.AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used);
                    // Past its transient bound the intake ends `intake.transient` and lets the ask go, as a limit's end does.
                    var transient = !byPerson && !limited && TransientEnded.Of(ended) is not null;
                    if (transient) conclusion = TransientEnded.Over(conclusion, ended, TransientEnded.Intake, adapter.Id.Value);

                    // A limit's end lets the ask go in the same step, so the look picks it again and its
                    // start is held by the cool-off as any start's is, never a strike; its record stays, naming the ask.
                    await service.AdvanceAsync(
                        sessionId, conclusion.State, note: conclusion.Note, ct: ct, limit: limited, parts: conclusion.Parts,
                        unbindAsk: limited || transient).ConfigureAwait(false);

                    return new StartRun(
                        $"{conclusion.State}  intake {sessionId} ({named}): {conclusion.Note}",
                        true,
                        SessionStates.IsParked(conclusion.State)
                            ? null
                            : new SessionEnded(
                                sessionId, $"ask #{ask.Id}", conclusion.State, byPerson, conclusion.Note,
                                Adapter: adapter.Id.Value, Account: selection.Profile) { Limit = limited });
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The person is closing the driver; the record says so rather than sit at "working".
            try
            {
                await service.AdvanceAsync(sessionId, "stopped", Observation.DriverClosed, ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort by construction: the host may already be gone on the same shutdown.
            }

            return new StartRun(
                $"stopped  intake {sessionId} ({named}): the driver was stopped.",
                true,
                new SessionEnded(sessionId, $"ask #{ask.Id}", "stopped", ByPerson: true, Adapter: adapterName));
        }
        catch (Exception) when (stoppedFirst is not null)
        {
            // A wire that broke after a stop is that stop's end, not a failure, as a quest's session reads it.
            var stopped = await _driver.EndStoppedAsync(sessionId, stoppedFirst).ConfigureAwait(false);
            return new StartRun(
                $"{stopped.State}  intake {sessionId} ({named}): {stopped.Note}",
                true,
                new SessionEnded(sessionId, $"ask #{ask.Id}", stopped.State, stoppedFirst.ByPerson, stopped.Note, Adapter: adapterName));
        }
        catch (Exception error)
        {
            // Everything but the shutdown above, a client timeout included — see the quest's twin.
            try
            {
                await service.AdvanceAsync(
                    sessionId, "failed", Observation.Failure(error.Message), ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The terminal write is best-effort by construction: the first failure is the report.
            }

            return new StartRun(
                $"failed  intake {sessionId} ({named}): {error.Message}",
                true,
                new SessionEnded(sessionId, $"ask #{ask.Id}", "failed", ByPerson: false, error.Message, Adapter: adapterName));
        }
        finally
        {
            output?.Close(sessionId);
        }
    }

    /// <summary>
    /// End every parked intake whose ask the person has since answered — published, closed or deleted. A
    /// parked intake has no process left: it asked and ended, so the answer to its question is the
    /// only thing that can close its record. The person's own act, so nothing interrupts them for it.
    /// </summary>
    public async Task ConcludeAnsweredAsync(
        IReadOnlyList<SessionView> active, List<string> events, List<SessionEnded> concluded, CancellationToken ct)
    {
        var service = _context.Service;
        foreach (var parked in active.Where(session => session.Ask is not null && SessionStates.IsParked(session.State)))
        {
            AskView? ask;
            try
            {
                ask = await service.FindAskAsync(parked.Ask!, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
            {
                continue; // The host did not answer; the next tick asks again.
            }

            // An ask the service does not have is one the person deleted, which settles it too.
            var answered = ask is null ? IntakeObservation.Deleted(parked.Ask!) : IntakeObservation.Answered(ask);
            if (answered is null) continue;

            try
            {
                await service.AdvanceAsync(parked.Id, answered.State, answered.AsNoted(), ct: ct).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                events.Add(_context.Said.Retry("intake", $"held  intake {parked.Id} could not be ended: {error.Message}"));
                continue;
            }

            events.Add(_context.Said.Progress(
                "intake", $"{answered.State}  intake {parked.Id} (ask #{parked.Ask}{(ask is null ? "" : $" in {ask.Workspace}")}): {answered.Note}"));
            concluded.Add(new SessionEnded(parked.Id, parked.Repository, answered.State, ByPerson: true, answered.Note));
        }
    }
}
