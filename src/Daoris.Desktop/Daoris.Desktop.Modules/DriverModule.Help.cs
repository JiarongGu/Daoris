using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Ask Daoris, the page's `bridge/help.ts` (MOD5; HELP1, D89): its conversation in its room, its proposals
/// and the person's Apply or Not now, and the doors an Apply goes through, each a screen's own route (HELP6).
/// </summary>
public sealed partial class DriverModule
{
    // Ask Daoris's conversation (HELP1a, D89): the one this machine is running, carried on — one
    // per machine — or a new one in its room, written from the machine as the driver holds it now.
    [DriverRoute("START_HELP")]
    private async Task<object?> StartHelpAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        // Asked first: no service answer changes it, and it says what to do.
        var config = DriverConfig.Load(_loop.ConfigPath);
        if (config.HelperAdapter is not { Length: > 0 } helper)
        {
            throw new DriverException(
                "Ask Daoris has no agent to run on — name one under Settings → AI features, or "
                + "`daoris driver helper <agent>`. Its starters need none.");
        }

        var chat = _loop.Chat ?? throw NotReady();
        var service = _loop.Service ?? throw NotReady();
        var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var held = _loop.Processes.Running;
        if (snapshot.Active.FirstOrDefault(session =>
                session.Repository == HelpRoom.Repository && held.Contains(session.Id, StringComparer.OrdinalIgnoreCase)) is { } running)
        {
            return new { SessionId = running.Id, Message = $"Ask Daoris `{running.Id}` is carried on.", Running = true };
        }

        var machine = await HelpMachineAsync(service, config, snapshot, cancellationToken).ConfigureAwait(false);
        var start = await chat.StartHelpAsync(
            helper, config, machine,
            onEnded: (session, state) => DriverLoop.Ended(_events, session, state),
            ct: cancellationToken).ConfigureAwait(false);

        _loop.Nudge();
        return new { start.SessionId, start.Message, Running = false };
    }

    /// <summary>
    /// The machine as Ask Daoris's room says it (HELP1a), from the driver's own answers: its file, the registry and each
    /// repository's line, the roster, the asks, the plugins and offers, the landings, what the loop parked and held, and the
    /// browser's files. What every open writes the room from, and since ASKHIST1 a conversation going on too.
    /// </summary>
    private async Task<HelpMachine> HelpMachineAsync(
        ServiceClient service, DriverConfig config, Snapshot snapshot, CancellationToken cancellationToken)
    {
        var lines = await CanonicalLine.OfAsync(
            config,
            snapshot.Repositories
                .OrderBy(known => known.Repository, StringComparer.Ordinal)
                .Select(known => (known.Repository, (string?)known.Workspace, known.Root)),
            cancellationToken).ConfigureAwait(false);
        var roster = await _loop.Harnesses.RosterAsync(config, ct: cancellationToken).ConfigureAwait(false);
        var standing = await service.AsksAsync(cancellationToken).ConfigureAwait(false);
        var asks = standing.Count(ask => ask.State == "Proposed");
        // This machine's sessions as the Sessions list places them (SESSUX1h3), so an archive or a delete names one it was shown;
        // the room still opens where the look cannot be taken, saying the sessions were not read.
        SessionLook? sessions = null;
        var looking = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            sessions = await SessionGroups.LookAsync(service, config, Door(config), _loop.Look.Latest, _loop.Home, null, cancellationToken).ConfigureAwait(false);
            sessions = sessions with { Waits = _loop.Look.Waits };
        }
        catch (DriverException)
        {
            // Left null: the room says `not read`, and a go still names a session the person names.
        }

        HelpRoomLookLog.Said(_loop.Log, looking.Elapsed, sessions is not null);

        // What each workspace keeps of finished work (HIST1f-c), the workspaces `daoris-driver history` reads; null where the
        // service could not be read, and the room says so.
        IReadOnlyList<HistoryReading>? history = null;
        try
        {
            var records = await service.SessionRecordsAsync(cancellationToken).ConfigureAwait(false);
            var quests = await service.EveryQuestAsync(cancellationToken).ConfigureAwait(false);
            var everyAsk = await service.EveryAskAsync(cancellationToken).ConfigureAwait(false);
            var world = HistoryWorldOf();
            var readings = new List<HistoryReading>();
            foreach (var workspace in records.Select(record => record.Workspace)
                         .Concat(quests.Select(quest => quest.Workspace))
                         .Concat(everyAsk.Select(each => each.Workspace))
                         .Select(RemoteTarget.Workspace)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .Order(StringComparer.OrdinalIgnoreCase))
            {
                var plan = await HistoryClearing.PlanAsync(world, HistoryScope.Workspace, workspace, cancellationToken).ConfigureAwait(false);
                if (plan.Reading is { } reading) readings.Add(reading);
            }

            history = readings;
        }
        catch (DriverException)
        {
            // Left null: the room says `not read`.
        }

        // The asks by id too (HELP6), so a delete of one made by mistake can name it.
        // And the branches landings made here (WSR5b), so a hand-off names one the record holds.
        return HelpRoom.Describe(
            config, snapshot, lines, roster, adapter => _loop.Harnesses.Toolchain(adapter)?.Product, asks, standing,
            PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names), new LandedBranches(_loop.Home).All(),
            // The install's own plugins (PLUG9 d), which the helper may propose installing by id.
            PluginOffers.Load(OffersFolder, _loop.Home, AdapterSet.Built().Names),
            // What the last tick parked by its strikes (HELP10), so a retry names a quest the drawer offers Retry on.
            _loop.Parked.Latest,
            // Daoris's browser as its files hold it (HELP10), so a favorite removed is one kept.
            BrowserModule.HelpFacts(BrowserModule.Home),
            // And what its last look held by the person's stop (SESSUX1b), so a retry names a stop Try again releases.
            HeldQuest.From(_loop.Look.Latest),
            sessions, history,
            // The named workflows saved here, so each repository's Workflow column names the version it follows (WORKFLOW1h-c).
            WorkflowStore.List(_loop.Home));
    }

    /// <summary>
    /// The room's reading for a conversation going on (ASKHIST1): the loop's service and this machine's file as they are now;
    /// null before the service answers, when the room as last written serves.
    /// </summary>
    private async Task<HelpMachine?> HelpMachineNowAsync(CancellationToken cancellationToken)
    {
        if (_loop.Service is not { } service) return null;
        var config = DriverConfig.Load(_loop.ConfigPath);
        var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        return await HelpMachineAsync(service, config, snapshot, cancellationToken).ConfigureAwait(false);
    }

    // Ask Daoris's proposals (HELP1c, D89): what one conversation proposed that waits for the
    // person, each judged with the route's own code first. One the route would refuse is never
    // shown: it is settled refused, and the agent hears why in the route's words.
    [DriverRoute("HELP_PROPOSALS")]
    private async Task<object?> HelpProposalsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        var service = _loop.Service ?? throw NotReady();
        var pending = HelpProposals.Pending(_loop.Home, session);
        var (config, facts) = await HelpFactsAsync(service, pending, cancellationToken).ConfigureAwait(false);
        var shown = new List<object>();
        foreach (var proposal in pending)
        {
            var plan = HelpProposals.Plan(proposal, config, facts);
            if (plan.Refusal is { } refused)
            {
                HelpProposals.Settle(_loop.Home, proposal.Id, "refused", refused);
                _loop.Chat?.Say(session, InPersonsWords($"Daoris did not show proposal `#{proposal.Id}` to the person — the route refuses it: {refused}"));
                continue;
            }

            // A clear's card holds the units it listed, once, so its Apply sends exactly those (HIST1f-c, design §5).
            HelpProposals.Drawn(_loop.Home, proposal, plan);
            shown.Add(new
            {
                proposal.Id, proposal.Kind, plan.Describe, plan.Terminal, proposal.Why,
                // What a plugin runs, as its manifest writes it (PLUG9), for the card to show before Apply.
                Plugin = plan.Plugin is { } plugin
                    ? new
                    {
                        plugin.Id, plugin.Name, plugin.Version, plugin.Command, plugin.Points,
                        Harnesses = plugin.Harnesses.Select(part => new { part.Name, part.Command }).ToArray(),
                        Servers = plugin.Servers.Select(part => new { part.Name, part.Command }).ToArray(),
                        plugin.Copied, plugin.Problem,
                        // An offer's requirement lines (PLUG9 d), and an update's changes (PLUG9 c).
                        plugin.Needs, plugin.Replaced,
                        Changes = ChangesOf(plugin.Changes),
                    }
                    : null,
                Sync = SyncShown(plan.Sync),
                // An accept's departures (DRIFT1d2), for the card to show the person's words before the yes.
                Accept = AcceptShown(plan.Accept),
                // A workflow's version, as the card shows it (WORKFLOW1h-c), never the steps or the choice Apply carries.
                Workflow = WorkflowShown(plan.Workflow),
            });
        }

        return new { Session = session, Proposals = shown.ToArray() };
    }

    /// <summary>
    /// What an accept card shows (DRIFT1d2), the page's <c>HelpAcceptShown</c>: the quest, and each departure its done
    /// answered — the requirement's number, the person's words it quotes and its check, the done's reason, and the person's
    /// words the reason relied on, each as the service answered it. Null for every other kind.
    /// </summary>
    public static object? AcceptShown(HelpAcceptPlan? accept) => accept is null ? null : new
    {
        accept.Quest,
        accept.Title,
        Departures = accept.Departures.Select(departure => new
        {
            departure.Requirement,
            Quote = departure.Required,
            departure.Check,
            Departed = departure.Reason,
            departure.Words,
        }).ToArray(),
    };

    /// <summary>
    /// What a bring-up-to-date card shows (HELP10), the page's <c>HelpSyncShown</c>: whether the person has looked, every
    /// row the look listed in the terminal's words, and what the rows do not say (LEFT3 b) — each repository not fetched,
    /// with git's reason, when it last heard from origin and how origin is reached, and the repositories left apart.
    /// Null for every other kind.
    /// </summary>
    public static object? SyncShown(HelpSyncPlan? sync) => sync is null ? null : new
    {
        sync.Looked,
        Rows = sync.Rows.Select(row => new { row.Key, row.Step, row.Moves, row.Says }).ToArray(),
        NotFetched = sync.Besides.NotFetched.Select(line => new { line.Repository, line.Fetch, line.LastFetch, line.Reach }).ToArray(),
        Apart = sync.Besides.Apart.ToArray(),
    };

    // The person's Apply: made through the door the screen's own route uses (HELP6), judged again
    // first, since the machine may have moved since the card was drawn. The result goes back into
    // the conversation as the person's next message, so the agent knows (D89).
    [DriverRoute("HELP_APPLY")]
    private async Task<object?> HelpApplyAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var service = _loop.Service ?? throw NotReady();
        var proposal = HelpProposals.Find(_loop.Home, id)
            ?? throw new DriverException($"there is no proposal `#{id}` on this machine.");
        if (proposal.State != "proposed") throw new DriverException($"proposal `#{id}` is already {proposal.State}.");

        var (config, facts) = await HelpFactsAsync(service, [proposal], cancellationToken).ConfigureAwait(false);
        var plan = HelpProposals.Plan(proposal, config, facts);
        // What it did, and an agent action's end once it comes, into the conversation that proposed it.
        void Say(string text)
        {
            if (proposal.Session is { } session) _loop.Chat?.Say(session, InPersonsWords(text));
        }

        var applied = await HelpProposals.ApplyAsync(
            _loop.Home, proposal, plan, HelpDoors(service), Say, cancellationToken).ConfigureAwait(false);
        // An accept publishes the step a departure held and lets a quest waiting on it go on (DRIFT1d2), as a delete or an ask moves the work.
        if (proposal.Kind is "ask" or "delete" or "accept" or "session" or "pause" or "clear") _loop.Nudge();

        return ApplyAnswer(proposal, applied);
    }

    /// <summary>
    /// What an Apply answers the page (HELP1c, HELP6), the page's <c>HelpSettled</c>: the driver's sentence, whether it was
    /// applied, where a go takes the person, and the agent action the Agents screen follows. Public so the fast half holds
    /// the answer without a service (LEFT3 d), as it holds a conversation's options.
    /// </summary>
    public static object ApplyAnswer(HelpProposal proposal, HelpApplied applied) => new
    {
        Message = applied.Told,
        applied.Applied,
        // Where a go takes the person: the page navigates, as its starters' doors do (HELP6), to the one quest or ask a go
        // names in it, the driver's judged spelling (ENTRY1f1), and opens Add repository or Import a folder with the workspace
        // the driver judged filled (ENTRY1d2a); each null on any other go.
        Go = applied.Go is { } place ? new { place.View, place.Domain, place.Part, place.Item, place.Workspace } : null,
        // The action an update or a pin started, so the Agents screen follows its console and its end. A default
        // (HELP10) is a file edit that starts nothing, so there is nothing to follow.
        HarnessAction = applied.Applied && proposal.Kind == "agent" && proposal.Door is "update" or "pin"
            ? new { Harness = proposal.Target!.Trim(), Action = proposal.Door }
            : null,
        // The card stands for another press (LEFT3 c): a sync card's look settles nothing, so the page logs no settlement.
        applied.Stands,
    };

    // The person's Not now: nothing changes, and the agent is told so.
    [DriverRoute("HELP_DISMISS")]
    private async Task<object?> HelpDismissAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var proposal = HelpProposals.Find(_loop.Home, id)
            ?? throw new DriverException($"there is no proposal `#{id}` on this machine.");
        if (proposal.State != "proposed") throw new DriverException($"proposal `#{id}` is already {proposal.State}.");

        HelpProposals.Settle(_loop.Home, id, "dismissed", null);
        var told = $"Not now: the person did not apply `#{id}`.";
        if (proposal.Session is { } said) _loop.Chat?.Say(said, InPersonsWords(told));
        await Task.CompletedTask.ConfigureAwait(false);
        return new { Message = told };
    }

    /// <summary>
    /// The doors Ask Daoris's Apply goes through (HELP6): each the code a screen's own route runs, so an
    /// Apply is what the screen would have done. <paramref name="service"/> is the loop's, or null before
    /// it is up — when an ask or a delete is the cold-start sentence.
    /// </summary>
    public IHelpDoors HelpDoors(ServiceClient? service) => new ScreenDoors(this, service);

    private sealed class ScreenDoors(DriverModule module, ServiceClient? service) : IHelpDoors
    {
        // SET_DRIVABLE, SET_LINE, SET_LANDING…: an edit to the driver's file, then a nudge.
        public void Change(Func<DriverConfig, DriverConfig> edit) => module.Change(edit);

        // The ask composer's door, the local host's `POST /api/asks`, with its review choice and words (ENTRY1c). A named
        // environment the workspace does not declare is refused before anything is sent, as `daoris-driver ask --review` refuses
        // it: the service holds no rule, and the proposal's plan holds no repositories per workspace.
        public async Task<AskAnswer> AskAsync(string workspace, string sentence, string? review, string? reviewWords, CancellationToken ct)
        {
            var asks = service ?? throw NotReady();
            if (review is not null
                && await AskReviewCommand.UndeclaredAsync(review, workspace, new AskReviewWorld(asks, () => DriverConfig.Load(module._loop.ConfigPath)), ct)
                    .ConfigureAwait(false) is { } undeclared)
            {
                return new AskAnswer(false, undeclared, null, null);
            }

            return await asks.AskAsync(workspace, sentence, [], [], null, ct, review, reviewWords).ConfigureAwait(false);
        }

        // The quest drawer's Delete: the local host's `DELETE /api/quests/{id}`.
        public Task<(bool Ok, string Message)> DeleteQuestAsync(string id, CancellationToken ct) =>
            (service ?? throw NotReady()).DeleteQuestAsync(id, ct);

        // The ask's record's Delete: the local host's `DELETE /api/asks/{id}`.
        public Task<(bool Ok, string Message)> DeleteAskAsync(string id, CancellationToken ct) =>
            (service ?? throw NotReady()).DeleteAskAsync(id, ct);

        // The quest page's Accept the departure (DRIFT1d2): the local host's `POST /api/quests/{id}/accept`, the terminal's too.
        public Task<(bool Ok, string Message)> AcceptDepartureAsync(string id, CancellationToken ct) =>
            (service ?? throw NotReady()).AcceptDepartureAsync(id, ct);

        // SESSION_ARCHIVE's own marks (SESSUX1h2c): each judged again by where the list places it as it goes, counted in the
        // machine log as the screen's. The service is the one this Apply was handed, as the other doors use it.
        public async Task<(bool Ok, string Message)> ArchiveSessionsAsync(IReadOnlyList<string> ids, bool archived, CancellationToken ct)
        {
            var marks = new SessionArchive(module._loop.Home);
            static string Listed(IEnumerable<string> each) => string.Join(", ", each.Select(id => $"`{id}`"));
            static string Count(int n) => $"{n} session{(n == 1 ? "" : "s")}";
            if (!archived)
            {
                var back = marks.Unarchive(ids);
                var undone = back.NotArchived.Count == 0 ? "" : $" Not archived, so left as they were: {Listed(back.NotArchived)}.";
                return (true, $"Unarchived {Count(ids.Count - back.NotArchived.Count)}.{undone}");
            }

            var asked = service ?? throw NotReady();
            var config = DriverConfig.Load(module._loop.ConfigPath);
            var look = await SessionGroups.LookAsync(asked, config, module.Door(config), module._loop.Look.Latest, module._loop.Home, ids, ct)
                .ConfigureAwait(false);
            look = look with { Waits = module._loop.Look.Waits };
            var answer = marks.Archive(ids, SessionGroups.Read(look, ids), [.. look.Records.Select(record => record.Id)], DateTimeOffset.UtcNow);
            var done = answer.Outcomes.Where(outcome => outcome.Verdict == ArchiveVerdict.Archived).Select(outcome => outcome.Session).ToList();
            var kept = answer.Outcomes.Where(outcome => outcome.Verdict != ArchiveVerdict.Archived).Select(outcome => outcome.Session).ToList();
            SessionArchive.Said(module._loop.Log, done.Count, PluginEvents.Screen);
            var left = kept.Count == 0 ? "" : $" Kept in the list, as archive never hides what needs you or stops what runs: {Listed(kept)}.";
            return done.Count == 0
                ? (false, $"Nothing was archived.{left}")
                : (true, $"Archived {Count(done.Count)}: {Listed(done)}.{left}");
        }

        // WORK_PAUSE's own pause as the screen's, then the loop asked to look (PAUSE1f-c): only an id this machine does not
        // have is not applied; a pause that finds nothing to hold is said, as the route says it (D48 §6).
        public async Task<(bool Ok, string Message)> PauseWorkAsync(WorkScope scope, string id, CancellationToken ct)
        {
            var outcome = await WorkPausing.PauseAsync(module.WorkWorldOf(), scope, id, PluginEvents.Screen, ct).ConfigureAwait(false);
            if (outcome.Verdict == PauseVerdict.Unknown) return (false, WorkPausing.Unknown(scope, id));

            module._loop.Nudge();
            return (true, string.Join(' ', WorkPausing.Lines(outcome).Select(line => line.Trim())));
        }

        // WORK_RESUME's own resume, the same way.
        public async Task<(bool Ok, string Message)> ResumeWorkAsync(WorkScope scope, string id, CancellationToken ct)
        {
            var outcome = await WorkPausing.ResumeAsync(module.WorkWorldOf(), scope, id, PluginEvents.Screen, ct).ConfigureAwait(false);
            if (outcome.Verdict == ResumeVerdict.Unknown) return (false, WorkPausing.Unknown(scope, id));

            module._loop.Nudge();
            return (true, string.Join(' ', WorkPausing.Lines(outcome).Select(line => line.Trim())));
        }

        // HISTORY_CLEAR's own clear of exactly the units the card listed (HIST1f-c): the driver library's one owner, each unit judged
        // again as it goes; the outcome comes back whole, and a service that did not answer throws before anything was removed.
        public Task<HistoryOutcome> ClearHistoryAsync(HistoryScope scope, string id, IReadOnlyList<HistoryUnitName> units, CancellationToken ct) =>
            HistoryClearing.ClearAsync(module.HistoryWorldOf(), scope, id, units, PluginEvents.Screen, ct);

        // SESSION_DELETE's own delete: the driver library's one owner, then the loop asked to look (SESSUX1h2c).
        public async Task<(bool Ok, string Message)> DeleteSessionAsync(string id, CancellationToken ct)
        {
            var outcome = await new SessionDeletion(module._loop.Home)
                .DeleteAsync(service ?? throw NotReady(), id, PluginEvents.Screen, module._loop.Log, module._loop.Events, ct)
                .ConfigureAwait(false);
            if (outcome.Verdict != DeleteVerdict.Deleted) return (false, outcome.Message);

            module._loop.Nudge();
            return (true, outcome.Message);
        }

        // The Manage drawer's Move to workspace (ENTRY1d1): the local host's `POST /api/registry/{repository}/workspace`, one
        // field of the row. The loop's registry watch (FG4) tells the page what moved.
        public Task<(bool Ok, string Message)> WireRepositoryAsync(string repository, string workspace, CancellationToken ct) =>
            (service ?? throw NotReady()).WireRepositoryAsync(repository, workspace, ct);

        // HARNESS_ACTION's own start, streamed under the same key and ended with the same news.
        public Task StartAgentActionAsync(string harness, string action, string? version, Action<int, string?> ended, CancellationToken ct)
        {
            var config = DriverConfig.Load(module._loop.ConfigPath);
            var toolchain = module._loop.Harnesses.Toolchain(harness)
                ?? throw new DriverException($"Daoris manages no toolchain for `{harness}` — its accounts are its own tooling's.");
            return module.StartProcessActionAsync(
                harness, action, profile: null, version, toolchain, config.Commands.GetValueOrDefault(harness),
                module.Relay(harness, action), config, ended);
        }

        // HARNESS_ACTION's own profile-default (HELP10): the door's owner (AGT7), the same write, and a person looking again
        // as the route's edit is; the report reads the default fresh, so no account is asked (ROSTER1).
        public async Task SetDefaultAccountAsync(string harness, string account, string? workspace, CancellationToken ct)
        {
            var config = DriverConfig.Load(module._loop.ConfigPath);
            var toolchain = module._loop.Harnesses.Toolchain(harness)
                ?? throw new DriverException($"Daoris manages no toolchain for `{harness}` — its accounts are its own tooling's.");
            try
            {
                module.ProfileDefault(toolchain.Owner(harness), account, workspace);
            }
            catch (ShenoraException refused)
            {
                // The route's refusal in its words, as the driver's, so the card settles refused with them (TOOL4g).
                throw new DriverException(refused.Message);
            }

            await module.ChangedAsync(harness, "profile-default", config, ct).ConfigureAwait(false);
        }

        // ACCOUNT_USE's own `use` (TOOL4g): the same refusals and the same write, then the loop asked to look.
        public Task SetAccountUseAsync(string harness, UseChange change, string? workspace, CancellationToken ct)
        {
            var owner = module.OwnerOf(harness);
            try
            {
                UseEdited(module._loop.Harnesses.Settings, owner, change, workspace).Save(module._loop.Harnesses.SettingsPath);
            }
            catch (ShenoraException refused)
            {
                throw new DriverException(refused.Message);
            }

            module._loop.Nudge();
            return Task.CompletedTask;
        }

        // ACCOUNT_USE's own `order` and `inherit` (TOOL4l-b): the list written whole through the screen's own edit, or a workspace
        // returned to the machine's accounts with its default and list cleared at once (D130 §3.2), then the loop asked to look.
        public Task SetAccountOrderAsync(string harness, IReadOnlyList<string> accounts, bool clear, string? workspace, CancellationToken ct)
        {
            var owner = module.OwnerOf(harness);
            var settings = module._loop.Harnesses.Settings;
            try
            {
                if (clear)
                {
                    var circle = workspace ?? throw new DriverException("returning to this machine's accounts names the workspace that returns.");
                    settings.WithWorkspaceDefault(circle, owner, null).WithRotation(owner, null, circle).Save(module._loop.Harnesses.SettingsPath);
                }
                else
                {
                    OrderEdited(settings, HarnessSettings.Profiles(module._loop.Harnesses.Home, owner), owner, accounts, workspace)
                        .Save(module._loop.Harnesses.SettingsPath);
                }
            }
            catch (ShenoraException refused)
            {
                throw new DriverException(refused.Message);
            }

            module._loop.Nudge();
            return Task.CompletedTask;
        }

        // ACCOUNT_USE's own `ready` (TOOL4l-b): *Try now*, the loop's own end of a cool-off, then the loop asked to look so an account
        // offered again takes a waiting start now, not at the next poll.
        public Task<bool> ReadyAccountAsync(string harness, string? account, CancellationToken ct)
        {
            var ended = module._loop.Harnesses.Ready(harness, account);
            module._loop.Nudge();
            return Task.FromResult(ended);
        }

        // TREES_SYNC_PLAN's own list (HELP10, WSR6): the same checkouts and sessions, and each line fetched as the person,
        // whose press this is — the look on Ask Daoris's card, never its proposal (D109).
        // It takes what the screen's look takes (D112): the repositories holding Daoris's branches, and the one a card names.
        public async Task<SyncPlan> SyncPlanAsync(string? repository, CancellationToken ct)
        {
            var (repositories, inUse) = await module.CheckoutsAndSessionsAsync(repository, ct).ConfigureAwait(false);
            return await new SessionTrees(module._loop.Home).SyncPlanAsync(repositories, inUse, fetch: true, ct, Named(repository))
                .ConfigureAwait(false);
        }

        // TREES_SYNC's own press: only the rows the look listed, fetching nothing, then the loop asked to look. The
        // sessions in use are asked again inside each repository's hold, as the screen's press asks them (LEFT2, WSR7).
        public async Task<SyncDone> SyncAsync(string? repository, IReadOnlySet<string> only, CancellationToken ct)
        {
            var (repositories, inUse) = await module.CheckoutsAndSessionsAsync(repository, ct).ConfigureAwait(false);
            var ledger = module._loop.Service ?? throw NotReady();
            var done = await new SessionTrees(module._loop.Home).SyncAsync(repositories, inUse, only, fetch: false, ct,
                inUseNow: async token => await InUseAsync(ledger, token).ConfigureAwait(false), scope: Named(repository)).ConfigureAwait(false);
            module._loop.Nudge();
            return done;
        }

        /// <summary>A card naming a repository includes it, as `--repository` does (D112); none, and the default.</summary>
        private static SyncScope? Named(string? repository) => repository is null ? null : SyncScope.Named([repository]);

        // BrowserModule's own edits (HELP10): the same check, the same refusal for a file it could not read, the
        // same write. A route's refusal is its words as the driver's, so the card settles refused with them.
        public void ChangeBrowser(string setting, string value, string? address, string? title)
        {
            var home = BrowserModule.Home;
            try
            {
                switch (setting, value)
                {
                    case ("use", _): BrowserModule.SetBrowser(home, value); break;
                    case ("links", _): BrowserModule.SetLinks(home, value); break;
                    case ("extensions", _): BrowserModule.SetExtensions(home, value); break;
                    case ("favorite", "add"): BrowserModule.AddFavorite(home, address ?? "", title); break;
                    case ("favorite", "remove"): BrowserModule.RemoveFavorite(home, address ?? ""); break;
                    default: throw new DriverException($"`{setting} {value}` is not a change the Browser screen makes.");
                }
            }
            catch (ShenoraException refused)
            {
                throw new DriverException(refused.Message);
            }
        }

        // SET_AGENT_SETTINGS's own write.
        public AgentSettingsRead SetAgentSettings(string harness, string account, AgentSettingEdit? model, AgentSettingEdit? effort) =>
            module.WriteAgentSettings(harness, account, () => (model, effort, null)).Read;

        // `daoris plugin add`'s copy, the driver's twin (PLUG9); the loop is asked to look, and it starts
        // what the plugin runs at that look, as it does any plugin. Nothing runs here.
        public void AddPlugin(string folder)
        {
            PluginInstall.Add(module._loop.Home, folder, AdapterSet.Built().Names);
            module._loop.Nudge();
        }

        // PLUGIN_ACTION's own enable|disable: the same lookup, the same row, the same nudge.
        public void SwitchPlugin(string id, bool on)
        {
            module.SwitchPlugin(module.InstalledPlugin(id), on);
            module._loop.Nudge();
        }

        // HANDOFF's own press (WSR5b): the recorded branch, its repository's checkout, the same note kept.
        public async Task<TreeHand> HandAsync(string repository, string branch, string? plugin, CancellationToken ct)
        {
            var trees = new SessionTrees(module._loop.Home, new LandingPlugins(
                module._loop.Home, say: (id, line) => module._loop.Output.Append($"plugin:{id}", line),
                log: module._loop.Log, health: module._loop.Health));
            if (trees.Recorded.Of(repository, branch) is not { } entry) return new TreeHand(false, SessionTrees.NotLanded(branch), branch);
            var root = await CheckoutOfAsync(service ?? throw NotReady(), repository, ct).ConfigureAwait(false);
            return root is null
                ? new TreeHand(false, $"`{repository}` has no checkout on this machine, so there is no `{branch}` here to hand on.", branch)
                : await module.HandAsync(trees, root, entry, plugin, ct).ConfigureAwait(false);
        }

        // PLUGIN_INSTALL's own copy of one of the install's offers (PLUG9 d); nothing runs here.
        public void AddOffer(string id)
        {
            PluginInstall.AddOffer(module._loop.Home, module.OffersFolder, id, AdapterSet.Built().Names);
            module._loop.Nudge();
        }

        // PLUGIN_UPDATE's own apply (PLUG9 c): the hook stopped, the folder swapped, the loop asked to look.
        public async Task UpdatePluginAsync(string id, CancellationToken ct) =>
            await module.UpdatePluginAsync(id).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether a pending proposal is judged against every quest and ask (HELP6): a delete, and since ENTRY1f1 a go naming
    /// one on Quests, so the service is asked for the records only then. Public so the fast half holds the gate without a service.
    /// </summary>
    public static bool JudgedAgainstRecords(IEnumerable<HelpProposal> proposals) =>
        proposals.Any(proposal => proposal.Kind == "delete" || ItemOn(proposal, "quests"));

    /// <summary>
    /// Whether a pending proposal is judged against this machine's session records, closed ones included: a go naming a
    /// session on Sessions (ENTRY1f2), so the service is asked for them only then.
    /// </summary>
    public static bool JudgedAgainstSessions(IEnumerable<HelpProposal> proposals) =>
        proposals.Any(proposal => ItemOn(proposal, "sessions") || SessionKind(proposal));

    // An archive or unarchive (kind `session`) and a delete naming a session (SESSUX1h2c): judged against where the list places
    // each session, which the records alone do not say.
    private static bool SessionKind(HelpProposal proposal) =>
        proposal.Kind == "session" || (proposal.Kind == "delete" && proposal.Door == "session");

    /// <summary>
    /// Whether a pending proposal is a pause or a resume (PAUSE1f-c), judged by the work of the ask and the quest it names, so
    /// the service is asked for that only then. Public so the fast half holds the gate without a service.
    /// </summary>
    public static bool JudgedAgainstWork(IEnumerable<HelpProposal> proposals) => proposals.Any(proposal => proposal.Kind == "pause");

    /// <summary>
    /// Whether a pending proposal is a clear (HIST1f-c), judged by what this machine keeps of the quest, ask or workspace it names,
    /// so the service is asked for that only then. Public so the fast half holds the gate without a service.
    /// </summary>
    public static bool JudgedAgainstHistory(IEnumerable<HelpProposal> proposals) => proposals.Any(proposal => proposal.Kind == "clear");

    /// <summary>
    /// Whether a pending proposal is a workflow (WORKFLOW1h-c), judged by the named workflows saved here and the runs bound to
    /// them, so the home is read for those only then. Public so the fast half holds the gate without a service.
    /// </summary>
    public static bool JudgedAgainstWorkflows(IEnumerable<HelpProposal> proposals) => proposals.Any(proposal => proposal.Kind == "workflow");

    /// <summary>
    /// The runs bound on this machine, each as its file under the bindings folder reads (WORKFLOW1h-c): a card says how many keep the
    /// version they started on. A file that is not a binding is skipped, as the run's own reading skips it.
    /// </summary>
    public static IReadOnlyList<WorkflowRunBinding> BoundRuns(string home)
    {
        var folder = WorkflowRunBindings.FolderOf(home);
        if (!Directory.Exists(folder)) return [];
        var runs = new List<WorkflowRunBinding>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            var run = Path.GetFileNameWithoutExtension(file);
            if (WorkflowRunBindings.Read(home, run) is { } binding) runs.Add(binding);
        }

        return runs;
    }

    /// <summary>
    /// What a workflow card shows (WORKFLOW1h-c, design §8.2), the page's <c>HelpWorkflowShown</c>: the card's own fields only. The
    /// steps Apply saves and the choice it makes stay with the driver, which sends exactly those when the person presses it.
    /// Null for every other kind.
    /// </summary>
    public static object? WorkflowShown(HelpWorkflowCard? card) => card is null ? null : new
    {
        card.Id, card.Name, card.Base, card.Version, card.Scope,
        card.Diff, card.YourPart, card.Outward, card.Needs, card.AppliesTo, card.Choose,
    };

    // A go naming an item on the view, read as the judge reads its view (`HelpGoProposals.Plan`); an item on any other view
    // is refused unread.
    private static bool ItemOn(HelpProposal proposal, string view) =>
        proposal.Kind == "go" && !string.IsNullOrWhiteSpace(proposal.Item)
        && string.Equals(proposal.Target?.Trim(), view, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What a proposal of Ask Daoris's is judged against (HELP1c): the driver's file, and the names the
    /// machine holds — its registered repositories and their circles, the agents it has, and the quests the
    /// last tick parked (HELP10). For the
    /// kinds that need them (HELP6), each door as the Agents screen's roster reads it, and every quest
    /// and ask with the service's own reading of whether it may be deleted, or this machine's sessions for a go naming one
    /// (ENTRY1f2) — asked only then.
    /// </summary>
    private async Task<(DriverConfig Config, HelpMachineFacts Facts)> HelpFactsAsync(
        ServiceClient service, IReadOnlyCollection<HelpProposal> proposals, CancellationToken ct)
    {
        var snapshot = await service.SnapshotAsync(ct).ConfigureAwait(false);
        var workspaces = snapshot.Repositories.Select(known => known.Workspace)
            .Append(RemoteTarget.DefaultWorkspace).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var config = DriverConfig.Load(_loop.ConfigPath);
        var facts = new HelpMachineFacts(
            [.. snapshot.Repositories.Select(known => known.Repository)], workspaces, _loop.Harnesses.Adapters.Names)
        {
            // A landing rule naming a plugin is judged by the catalogue the landing route reads (HELP8, D100).
            Plugins = PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names),
            // A plugin is added from a registered checkout, never from the home, and may not shadow a
            // harness this build carries (PLUG9) — what `daoris plugin add` refuses.
            Home = _loop.Home,
            Checkouts = snapshot.Repositories
                .GroupBy(known => known.Repository, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(same => same.Key, same => same.First().Root, StringComparer.OrdinalIgnoreCase),
            Reserved = AdapterSet.Built().Names,
            // A hand-off names a branch this machine's landings recorded (WSR5b), read as its door reads it.
            Landed = new LandedBranches(_loop.Home).All(),
            // The install's own plugins (PLUG9 d), which an add may name by id, and where an offer's update reads.
            OffersFolder = OffersFolder,
            Offers = PluginOffers.Load(OffersFolder, _loop.Home, AdapterSet.Built().Names),
            // A retry names a quest the last tick parked by its strikes (HELP10), the verdict the drawer shows Retry by.
            Parked = _loop.Parked.Latest,
            // Or one its last look held by the person's stop (SESSUX1b), released from the session that look named.
            Held = HeldQuest.From(_loop.Look.Latest),
            // A review's procedure is looked for in the checkouts it reaches (REVIEWENV1a), as `SET_REVIEW` looks.
            Registered = [.. snapshot.Repositories.Select(known => (known.Repository, (string?)known.Workspace, known.Root))],
        };

        if (proposals.Any(proposal => proposal.Kind is "agent" or "account"))
        {
            // The HARNESSES route's own reading of each door, field by field.
            var roster = await _loop.Harnesses.RosterAsync(config, ct: ct).ConfigureAwait(false);
            var settings = _loop.Harnesses.Settings;
            facts = facts with
            {
                Doors = [.. roster.Select(report =>
                {
                    var toolchain = _loop.Harnesses.Toolchain(report.Adapter);
                    var pinned = settings.ResolveVersion(report.Adapter, null, null);
                    var owner = toolchain?.Owner(report.Adapter) ?? report.Adapter;
                    return new HelpDoorFacts(report.Adapter)
                    {
                        Present = report.Present,
                        Updates = toolchain is null ? null : HarnessActions.UpdateOf(toolchain, pinned),
                        Pinned = pinned,
                        Package = toolchain?.Package,
                        Channel = toolchain?.Channel,
                        Product = toolchain?.Product,
                        Owner = owner,
                        Accounts = HarnessSettings.Profiles(_loop.Harnesses.Home, owner),
                        SettingsKnown = _loop.Harnesses.AccountToolchain(report.Adapter)?.SettingsFile is { Length: > 0 },
                    };
                })],
                // A default's scope and a use's list, as ACCOUNT_USE and profile-default read them (TOOL4g).
                Wiring = settings,
                // The cool-offs a ready's judge reads, as the account rows show them (TOOL4l-b).
                Cooling = AccountCooling.Read(_loop.Home, DateTimeOffset.UtcNow),
            };
        }

        if (JudgedAgainstRecords(proposals))
        {
            var (quests, asks) = await HelpProposals.RecordsAsync(service, ct).ConfigureAwait(false);
            facts = facts with { Quests = quests, Asks = asks };
        }

        // This machine's own sessions as the Sessions list reads them (ENTRY1f2), read only when a go names one.
        if (JudgedAgainstSessions(proposals))
        {
            facts = facts with { Sessions = await service.SessionRecordsAsync(ct).ConfigureAwait(false) };
        }

        // A session proposal is judged by where the list places each session, and a delete by what this machine keeps and the
        // ledger's own answer (SESSUX1h2c, D126 §5.2, §5.4): the one look the Sessions screen takes, read only for those.
        if (proposals.Any(SessionKind))
        {
            var look = await SessionGroups.LookAsync(service, config, Door(config), _loop.Look.Latest, _loop.Home, null, ct).ConfigureAwait(false);
            look = look with { Waits = _loop.Look.Waits };
            var judged = new Dictionary<string, SessionDeleteAnswer>(StringComparer.Ordinal);
            foreach (var proposal in proposals.Where(each => each.Kind == "delete" && each.Door == "session"))
            {
                var target = proposal.Target?.Trim().TrimStart('#') ?? "";
                // Only a record the ledger can be asked of: a teammate's, a live one and Ask Daoris's own are refused before it.
                if (facts.Sessions.FirstOrDefault(each => string.Equals(each.Id, target, StringComparison.OrdinalIgnoreCase)) is
                    { Teammate: false, Live: false } record && !WordsNever.IsHelp(record))
                {
                    judged[record.Id] = await service.JudgeSessionDeleteAsync(record.Id, ct).ConfigureAwait(false);
                }
            }

            facts = facts with
            {
                Placed = SessionGroups.Read(look, null),
                Kept = new SessionDeletion(_loop.Home).Kept(facts.Sessions),
                SessionDeletes = judged,
            };
        }

        // A pause proposal is judged by what a pause would do with each piece of the work, read as the pages' plan reads it for
        // the id as an ask's and as a quest's, and by which workspaces sync with a remote (PAUSE1f-c, D132 §7.4, §5.1).
        if (JudgedAgainstWork(proposals))
        {
            var world = WorkWorldOf();
            var plans = new List<WorkPlan>();
            foreach (var proposal in proposals.Where(each => each.Kind == "pause"))
            {
                var target = proposal.Target?.Trim().TrimStart('#').Trim() ?? "";
                if (target.Length == 0) continue;
                foreach (var scope in new[] { WorkScope.Ask, WorkScope.Quest })
                {
                    if (await WorkPausing.PlanAsync(world, scope, target, ct).ConfigureAwait(false) is { } plan) plans.Add(plan);
                }
            }

            // As the REMOTES module reads the wiring: the environment names the machine's remote whole, else the file's workspaces.
            var fromEnvironment = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(RemoteTarget.UrlVariable))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(RemoteTarget.KeyVariable));
            facts = facts with
            {
                Work = plans,
                Wired = fromEnvironment ? facts.Workspaces : [.. RemoteTarget.Load().Keys],
            };
        }

        // A clear proposal is judged by what this machine keeps of the work it names, as HISTORY_PLAN reads it (HIST1f-c, design
        // §6.4). A service that cannot be read throws, and the proposal stays unsettled.
        if (JudgedAgainstHistory(proposals))
        {
            var world = HistoryWorldOf();
            var plans = new List<HistoryPlan>();
            foreach (var proposal in proposals.Where(each => each.Kind == "clear"))
            {
                var scope = proposal.Door switch { "quest" => HistoryScope.Quest, "ask" => HistoryScope.Ask, "failed" => HistoryScope.Failed, "workspace" => HistoryScope.Workspace, _ => (HistoryScope?)null };
                if (scope is not { } named) continue;
                var target = proposal.Target?.Trim() ?? "";
                if (named != HistoryScope.Workspace) target = target.TrimStart('#').Trim();
                if (target.Length == 0) continue;
                plans.Add(await HistoryClearing.PlanAsync(world, named, target, ct).ConfigureAwait(false));
            }

            facts = facts with { History = plans };
        }

        // Every quest as the service answers it, its requirements, answers and hold with it (DRIFT1d2), read only when an
        // accept is pending: the quest page's yes is shown by the same answer.
        if (proposals.Any(proposal => proposal.Kind == "accept"))
        {
            facts = facts with { QuestRecords = await service.EveryQuestAsync(ct).ConfigureAwait(false) };
        }

        // A workflow proposal is judged by the named workflows here and the runs bound to them (WORKFLOW1h-c, design §8.1, §8.2); the
        // registered repositories and plugins it also reads are in the facts already.
        if (JudgedAgainstWorkflows(proposals))
        {
            facts = facts with { Workflows = WorkflowStore.List(_loop.Home), WorkflowRuns = BoundRuns(_loop.Home) };
        }

        // Daoris's browser's files as the Browser screen reads them (HELP10), read only when a browser change is pending.
        if (proposals.Any(proposal => proposal.Kind == "browser"))
        {
            facts = facts with { Browser = BrowserModule.HelpFacts(BrowserModule.Home) };
        }

        return (config, facts);
    }

    /// <summary>
    /// A result said into Ask Daoris's conversation in the person's name (HELP1c): plain words, since a
    /// person's message renders verbatim and a sentence full of backticks read as noise on the window.
    /// </summary>
    private static string InPersonsWords(string told) => told.Replace("`", "", StringComparison.Ordinal);

    // ——— Ask Daoris's history (ASKHIST1): its conversations listed and searched, named, pinned and started from. Going on in
    // one is the person's words to it (`SESSION_INPUT`), and deleting one is `SESSION_DELETE`'s, so each has the one owner the
    // rest of the window uses; `daoris-driver help` is the terminal's door to the same (D50). This machine's and the person's:
    // the words are this machine's record (D76), what the person keeps of each a file beside them (`HelpConversations`), and
    // nothing here reaches a remote.

    // A page of Ask Daoris's conversations, pinned first, then the newest; with `q`, those whose name or words hold it, each with
    // where. `offset` and `limit` are the terminal's `--offset` and `--limit` (ASKHIST1d): every record is ordered and searched
    // before the page is taken, and the answer says how many there are and where the next page starts.
    [DriverRoute("HELP_CONVERSATIONS")]
    private async Task<object?> HelpConversationsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var records = SessionRecords.Parse(await service.SessionRecordsJsonAsync(cancellationToken).ConfigureAwait(false));
        var listing = new HelpConversations(_loop.Home).List(
            records, _loop.Events, Resumes, Optional(request, "q"),
            (int)Math.Clamp(Number(request, "offset") ?? 0, 0, int.MaxValue),
            (int)Math.Clamp(Number(request, "limit") ?? HelpConversations.PageLimit, 0, HelpConversations.PageLimit));
        return HistoryAnswer(listing);
    }

    /// <summary>
    /// A listing as the page reads it, its <c>HelpConversation</c>s: field for field what <c>daoris-driver help list --json</c>
    /// prints (<see cref="HelpCommand.JsonFields"/>). Public, as <see cref="Grouped"/> is, so its shape is tested without a service.
    /// </summary>
    public static object HistoryAnswer(HelpListing listing) => new
    {
        Conversations = listing.Conversations.Select(row => new
        {
            row.Session, row.Title, row.Name, row.Opening, row.About, row.Created, row.Last, row.Pinned, row.Live, row.Resumable,
            row.From, row.Handed, row.Found, row.FoundLine,
        }).ToArray(),
        listing.Cut,
        listing.Total,
        listing.Next,
    };

    // Name an Ask Daoris conversation, or clear its name with none: its first question is its title again.
    [DriverRoute("HELP_RENAME")]
    private async Task<object?> HelpRenameAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = await HelpConversationOfAsync(PayloadHelper.GetRequiredValue<string>(request.Payload, "id"), cancellationToken)
            .ConfigureAwait(false);
        var kept = new HelpConversations(_loop.Home);
        kept.Rename(id, Optional(request, "name"));
        return new { Session = id, kept.Read(id).Name };
    }

    // Pin an Ask Daoris conversation to the top of its history, or unpin it.
    [DriverRoute("HELP_PIN")]
    private async Task<object?> HelpPinAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = await HelpConversationOfAsync(PayloadHelper.GetRequiredValue<string>(request.Payload, "id"), cancellationToken)
            .ConfigureAwait(false);
        var kept = new HelpConversations(_loop.Home);
        kept.Pin(id, PayloadHelper.GetRequiredValue<bool>(request.Payload, "pinned"), DateTimeOffset.UtcNow);
        return new { Session = id, kept.Read(id).Pinned };
    }

    // A new Ask Daoris conversation from an earlier one's words (ASKHIST1): opened as START_HELP opens one, on the helper's
    // agent, the room's running conversation set aside first, and the earlier one's transcript handed with the person's first
    // words. Its end reaches the page as a start's does.
    [DriverRoute("HELP_START_FROM")]
    private async Task<object?> HelpStartFromAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var config = DriverConfig.Load(_loop.ConfigPath);
        if (config.HelperAdapter is not { Length: > 0 } helper)
        {
            throw new DriverException(
                "Ask Daoris has no agent to run on — name one under Settings → AI features, or "
                + "`daoris driver helper <agent>`. Its starters need none.");
        }

        var id = await HelpConversationOfAsync(PayloadHelper.GetRequiredValue<string>(request.Payload, "id"), cancellationToken)
            .ConfigureAwait(false);
        var chat = _loop.Chat ?? throw NotReady();
        var service = _loop.Service ?? throw NotReady();
        var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var machine = await HelpMachineAsync(service, config, snapshot, cancellationToken).ConfigureAwait(false);
        var start = await chat.StartHelpFromAsync(
            id, helper, config, machine, onEnded: (session, state) => DriverLoop.Ended(_events, session, state),
            ct: cancellationToken).ConfigureAwait(false);

        _loop.Nudge();
        return new { start.SessionId, start.Message, From = id };
    }

    /// <summary>
    /// The id when it names an Ask Daoris conversation of this machine's, judged from the records as the service answers them;
    /// otherwise the sentence why not, which the page says where it was pressed.
    /// </summary>
    private async Task<string> HelpConversationOfAsync(string id, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var record = SessionRecords.Parse(await service.SessionRecordsJsonAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null) throw new DriverException($"no conversation here is `{id}` any more.");
        if (record.Teammate || !WordsNever.IsHelp(record))
        {
            throw new DriverException($"`{id}` is no Ask Daoris conversation of this machine's.");
        }

        return id;
    }

    /// <summary>Whether an adapter's door resumes a conversation by its id: one this machine no longer has cannot.</summary>
    private bool Resumes(string adapter)
    {
        try
        {
            return _loop.Harnesses.Adapters.Resolve(adapter).Resumes;
        }
        catch (DriverException)
        {
            return false;
        }
    }
}
