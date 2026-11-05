using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What a proposal of Ask Daoris's is judged against: the driver's file, and the names the machine holds, with each kind's extra
/// reading made only when a pending proposal needs it. The <c>JudgedAgainst</c> gates are public so the fast half holds each without a service.
/// </summary>
public sealed class HelpProposalFacts(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>
    /// Whether a pending proposal is judged against every quest and ask: a delete, and since ENTRY1f1 a go naming
    /// one on Quests, so the service is asked for the records only then.
    /// </summary>
    public static bool JudgedAgainstRecords(IEnumerable<HelpProposal> proposals) =>
        proposals.Any(proposal => proposal.Kind == "delete" || ItemOn(proposal, "quests"));

    /// <summary>
    /// Whether a pending proposal is judged against this machine's session records, closed ones included: a go naming a
    /// session on Sessions, so the service is asked for them only then.
    /// </summary>
    public static bool JudgedAgainstSessions(IEnumerable<HelpProposal> proposals) =>
        proposals.Any(proposal => ItemOn(proposal, "sessions") || SessionKind(proposal));

    // An archive or unarchive (kind `session`) and a delete naming a session: judged against where the list places
    // each session, which the records alone do not say.
    private static bool SessionKind(HelpProposal proposal) =>
        proposal.Kind == "session" || (proposal.Kind == "delete" && proposal.Door == "session");

    /// <summary>
    /// Whether a pending proposal is a pause or a resume, judged by the work of the ask and the quest it names, so
    /// the service is asked for that only then.
    /// </summary>
    public static bool JudgedAgainstWork(IEnumerable<HelpProposal> proposals) => proposals.Any(proposal => proposal.Kind == "pause");

    /// <summary>
    /// Whether a pending proposal is a clear, judged by what this machine keeps of the quest, ask or workspace it names,
    /// so the service is asked for that only then.
    /// </summary>
    public static bool JudgedAgainstHistory(IEnumerable<HelpProposal> proposals) => proposals.Any(proposal => proposal.Kind == "clear");

    /// <summary>
    /// Whether a pending proposal is a workflow, judged by the named workflows saved here and the runs bound to
    /// them, so the home is read for those only then.
    /// </summary>
    public static bool JudgedAgainstWorkflows(IEnumerable<HelpProposal> proposals) => proposals.Any(proposal => proposal.Kind == "workflow");

    /// <summary>
    /// Whether a pending proposal is a change to the open workflow draft, judged against the drafts its conversation's record
    /// says it was told, so the record is read for those only then.
    /// </summary>
    public static bool JudgedAgainstDrafts(IEnumerable<HelpProposal> proposals) =>
        proposals.Any(proposal => proposal.Kind == "workflow" && proposal.Door == "draft");

    /// <summary>Whether any proposal is a place's set-up, judged by the place's plan.</summary>
    public static bool JudgedAgainstPlaces(IEnumerable<HelpProposal> proposals) =>
        proposals.Any(proposal => proposal.Kind == "setup" && proposal.Door == "place" && proposal.Place is not null);

    /// <summary>
    /// The runs bound on this machine, each as its file under the bindings folder reads: a card says how many keep the
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

    // A go naming an item on the view, read as the judge reads its view (`HelpGoProposals.Plan`); an item on any other view
    // is refused unread.
    private static bool ItemOn(HelpProposal proposal, string view) =>
        proposal.Kind == "go" && !string.IsNullOrWhiteSpace(proposal.Item)
        && string.Equals(proposal.Target?.Trim(), view, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The driver's file and the names the machine holds: its registered repositories and their circles, the agents it has, and the quests
    /// the last tick parked. For the kinds that need them, each door as the Agents screen's roster reads it, and every quest
    /// and ask with the service's own reading of whether it may be deleted, or this machine's sessions for a go naming one
    /// — asked only then.
    /// </summary>
    internal async Task<(DriverConfig Config, HelpMachineFacts Facts)> FactsAsync(
        ServiceClient service, IReadOnlyCollection<HelpProposal> proposals, CancellationToken ct)
    {
        var snapshot = await service.SnapshotAsync(ct).ConfigureAwait(false);
        var workspaces = snapshot.Repositories.Select(known => known.Workspace)
            .Append(RemoteTarget.DefaultWorkspace).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var config = DriverConfig.Load(_loop.ConfigPath);
        // the workspace the conversation that proposed them is bound to, read from its record, and only where one is
        // pending; the records are read once, for a go or a session's proposal too.
        var conversation = proposals.Select(proposal => proposal.Session).FirstOrDefault(session => !string.IsNullOrWhiteSpace(session));
        var records = conversation is null ? null : await service.SessionRecordsAsync(ct).ConfigureAwait(false);
        var facts = new HelpMachineFacts(
            [.. snapshot.Repositories.Select(known => known.Repository)], workspaces, _loop.Harnesses.Adapters.Names)
        {
            Circle = HelpCircle.OfRecord(_loop.Home, records?.FirstOrDefault(record => string.Equals(record.Id, conversation, StringComparison.OrdinalIgnoreCase))),
            // A landing rule naming a plugin is judged by the catalogue the landing route reads.
            Plugins = PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names),
            // The identities a `git-use` proposal may point at: labels and hosts, never a key or a token.
            GitIdentities = [.. GitIdentities.Read(_loop.Home).Select(each => new HelpGitIdentity(each.Id, each.Label, each.Host, each.Way))],
            // A plugin is added from a registered checkout, never from the home, and may not shadow a
            // harness this build carries — what `daoris plugin add` refuses.
            Home = _loop.Home,
            Checkouts = snapshot.Repositories
                .GroupBy(known => known.Repository, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(same => same.Key, same => same.First().Root, StringComparer.OrdinalIgnoreCase),
            Reserved = AdapterSet.Built().Names,
            // The install's own plugins, which an add may name by id, and where an offer's update reads.
            OffersFolder = context.OffersFolder,
            Offers = PluginOffers.Load(context.OffersFolder, _loop.Home, AdapterSet.Built().Names),
            // A retry names a quest the last tick parked by its strikes, the verdict the drawer shows Retry by.
            Parked = _loop.Parked.Latest,
            // Or one its last look held by the person's stop, released from the session that look named.
            Held = HeldQuest.From(_loop.Look.Latest),
            // A review's procedure is looked for in the checkouts it reaches, as `SET_REVIEW` looks.
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
                    var owner = _loop.Harnesses.Adapters.OwnerOf(report.Adapter);
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
                // A default's scope and a use's list, as ACCOUNT_USE and profile-default read them.
                Wiring = settings,
                // The cool-offs a ready's judge reads, as the account rows show them.
                Cooling = AccountCooling.Read(_loop.Home, DateTimeOffset.UtcNow),
            };
        }

        if (JudgedAgainstRecords(proposals))
        {
            var (quests, asks) = await HelpDeleteProposals.RecordsAsync(service, ct).ConfigureAwait(false);
            facts = facts with { Quests = quests, Asks = asks };
        }

        // This machine's own sessions as the Sessions list reads them, read only when a go names one.
        if (JudgedAgainstSessions(proposals))
        {
            facts = facts with { Sessions = records ?? await service.SessionRecordsAsync(ct).ConfigureAwait(false) };
        }

        // A session proposal is judged by where the list places each session, and a delete by what this machine keeps and the
        // ledger's own answer: the one look the Sessions screen takes, read only for those.
        if (proposals.Any(SessionKind))
        {
            var look = await SessionGroups.LookAsync(service, config, context.Door(config), _loop.Look.Latest, _loop.Home, null, ct).ConfigureAwait(false);
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
        // the id as an ask's and as a quest's, and by which workspaces sync with a remote.
        if (JudgedAgainstWork(proposals))
        {
            var world = WorkRoutes.WorldOf(context);
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
            var world = HistoryRoutes.WorldOf(_loop);
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

        // A workflow proposal is judged by the named workflows here and the runs bound to them; the
        // registered repositories and plugins it also reads are in the facts already.
        if (JudgedAgainstWorkflows(proposals))
        {
            var workflows = WorkflowStore.List(_loop.Home);
            facts = facts with
            {
                Workflows = workflows,
                WorkflowRuns = BoundRuns(_loop.Home),
                // A graph that asks with done or steward is judged by the gates the accepted declaration names.
                AcceptedGates = await IntegrateChoice.ReadForCardsAsync(config, workflows, facts.Registered, ct).ConfigureAwait(false),
            };
        }

        // a change to the open draft is judged against the drafts its conversation's own record says it was told,
        // read from this machine's record only for one.
        if (JudgedAgainstDrafts(proposals) && conversation is not null && SessionEventText.IsLocalId(conversation))
        {
            facts = facts with { Drafts = WorkflowDraftTold.ToldAcross(conversation, id => _loop.Events.After(id, 0).Events) };
        }

        // A place proposal is judged by its quick set-up's plan, as `daoris place … --plan` and PLACE_PLAN read it: the
        // registry, the quests and each line's tip as git objects; written and probed nowhere.
        if (JudgedAgainstPlaces(proposals))
        {
            var world = new PlaceWorld(service);
            var plugins = PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names);
            var places = new Dictionary<string, PlacePlanned>(StringComparer.Ordinal);
            foreach (var proposal in proposals.Where(each => each.Kind == "setup" && each.Door == "place" && each.Place is not null))
            {
                places[proposal.Id] = await PlacePlans.PlanAsync(world, config, _loop.Home, plugins, context.Door(config), proposal.Place!, ct).ConfigureAwait(false);
            }

            facts = facts with { Places = places };
        }

        // Daoris's browser's files as the Browser screen reads them, read only when a browser change is pending.
        if (proposals.Any(proposal => proposal.Kind == "browser"))
        {
            facts = facts with { Browser = BrowserModule.HelpFacts(BrowserModule.Home) };
        }

        return (config, facts);
    }
}
