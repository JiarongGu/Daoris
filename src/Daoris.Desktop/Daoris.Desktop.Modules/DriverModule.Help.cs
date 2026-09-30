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
                "Ask Daoris has no agent to run on — name one under Settings → Daoris's own AI, or "
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
        var machine = HelpRoom.Describe(
            config, snapshot, lines, roster, adapter => _loop.Harnesses.Toolchain(adapter)?.Product, asks, standing,
            PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names), new LandedBranches(_loop.Home).All(),
            // The install's own plugins (PLUG9 d), which the helper may propose installing by id.
            PluginOffers.Load(OffersFolder, _loop.Home, AdapterSet.Built().Names),
            // What the last tick parked by its strikes (HELP10), so a retry names a quest the drawer offers Retry on.
            _loop.Parked.Latest,
            // Daoris's browser as its files hold it (HELP10), so a favorite removed is one kept.
            BrowserModule.HelpFacts(BrowserModule.Home));

        var start = await chat.StartHelpAsync(
            helper, config, machine,
            onEnded: (session, state) =>
                _events.EmitAsync("DAORIS", "SESSION_ENDED", new { Session = session, State = state }),
            ct: cancellationToken).ConfigureAwait(false);

        _loop.Nudge();
        return new { start.SessionId, start.Message, Running = false };
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
            });
        }

        return new { Session = session, Proposals = shown.ToArray() };
    }

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
        if (proposal.Kind is "ask" or "delete") _loop.Nudge();

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
        // Where a go takes the person: the page navigates, as its starters' doors do (HELP6).
        Go = applied.Go is { } place ? new { place.View, place.Domain, place.Part } : null,
        // The action an update or a pin started, so the Agents screen follows its console and its end. A default
        // (HELP10) is a file edit that starts nothing, so there is nothing to follow.
        HarnessAction = applied.Applied && proposal.Kind == "agent" && proposal.Door is "update" or "pin"
            ? new { Harness = proposal.Target!.Trim(), Action = proposal.Door }
            : null,
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

        // The ask composer's door, the local host's `POST /api/asks`.
        public Task<AskAnswer> AskAsync(string workspace, string sentence, CancellationToken ct) =>
            (service ?? throw NotReady()).AskAsync(workspace, sentence, [], [], null, ct);

        // The quest drawer's Delete: the local host's `DELETE /api/quests/{id}`.
        public Task<(bool Ok, string Message)> DeleteQuestAsync(string id, CancellationToken ct) =>
            (service ?? throw NotReady()).DeleteQuestAsync(id, ct);

        // The ask's record's Delete: the local host's `DELETE /api/asks/{id}`.
        public Task<(bool Ok, string Message)> DeleteAskAsync(string id, CancellationToken ct) =>
            (service ?? throw NotReady()).DeleteAskAsync(id, ct);

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

        // HARNESS_ACTION's own profile-default (HELP10): the door's owner (AGT7), the same write, and the roster asked
        // again as the route asks it after every file edit, since its answer is cached with the default in it.
        public async Task SetDefaultAccountAsync(string harness, string account, string? workspace, CancellationToken ct)
        {
            var config = DriverConfig.Load(module._loop.ConfigPath);
            var toolchain = module._loop.Harnesses.Toolchain(harness)
                ?? throw new DriverException($"Daoris manages no toolchain for `{harness}` — its accounts are its own tooling's.");
            module.ProfileDefault(toolchain.Owner(harness), account, workspace);
            await module._loop.Harnesses.RosterAsync(config, refresh: true, ct).ConfigureAwait(false);
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
                module._loop.Home, say: (id, line) => module._loop.Output.Append($"plugin:{id}", line)));
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
            };
        }

        if (proposals.Any(proposal => proposal.Kind == "delete"))
        {
            var (quests, asks) = await HelpProposals.RecordsAsync(service, ct).ConfigureAwait(false);
            facts = facts with { Quests = quests, Asks = asks };
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
}
