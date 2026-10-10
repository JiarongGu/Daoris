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
            HeldQuest.From(_loop.Look.Latest));
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
        if (proposal.Kind is "ask" or "delete" or "accept") _loop.Nudge();

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
        // names in it, the driver's judged spelling (ENTRY1f1).
        Go = applied.Go is { } place ? new { place.View, place.Domain, place.Part, place.Item } : null,
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
    /// one, so the service is asked for the records only then. Public so the fast half holds the gate without a service.
    /// </summary>
    public static bool JudgedAgainstRecords(IEnumerable<HelpProposal> proposals) =>
        proposals.Any(proposal => proposal.Kind == "delete" || proposal.Kind == "go" && !string.IsNullOrWhiteSpace(proposal.Item));

    /// <summary>
    /// What a proposal of Ask Daoris's is judged against (HELP1c): the driver's file, and the names the
    /// machine holds — its registered repositories and their circles, the agents it has, and the quests the
    /// last tick parked (HELP10). For the
    /// kinds that need them (HELP6), each door as the Agents screen's roster reads it, and every quest
    /// and ask with the service's own reading of whether it may be deleted — asked only then.
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
            };
        }

        if (JudgedAgainstRecords(proposals))
        {
            var (quests, asks) = await HelpProposals.RecordsAsync(service, ct).ConfigureAwait(false);
            facts = facts with { Quests = quests, Asks = asks };
        }

        // Every quest as the service answers it, its requirements, answers and hold with it (DRIFT1d2), read only when an
        // accept is pending: the quest page's yes is shown by the same answer.
        if (proposals.Any(proposal => proposal.Kind == "accept"))
        {
            facts = facts with { QuestRecords = await service.EveryQuestAsync(ct).ConfigureAwait(false) };
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
