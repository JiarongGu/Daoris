using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What a proposal of Ask Daoris's is judged against: the driver's file, and the names the machine holds, with each kind's extra
/// reading made only when a pending proposal needs it. The <c>JudgedAgainst</c> gates are public so the fast half holds each without a service.
/// </summary>
public sealed class HelpProposalFacts(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>Whether a pending proposal is a go naming a quest or an ask on Quests, judged against every one, so the service is asked for the records only then.</summary>
    public static bool JudgedAgainstRecords(IEnumerable<HelpProposal> proposals) => proposals.Any(proposal => ItemOn(proposal, "quests"));

    /// <summary>
    /// Whether a pending proposal is judged against this machine's session records, closed ones included: a go naming a
    /// session on Sessions, so the service is asked for them only then.
    /// </summary>
    public static bool JudgedAgainstSessions(IEnumerable<HelpProposal> proposals) => proposals.Any(proposal => ItemOn(proposal, "sessions"));

    public static bool JudgedAgainstHistory(IEnumerable<HelpProposal> proposals) => proposals.Any(proposal => proposal.Kind == "clear");

    /// <summary>
    /// Whether a pending proposal is a workflow, judged by the named workflows saved here and the runs bound to
    /// them, so the home is read for those only then.
    /// </summary>
    public static bool JudgedAgainstWorkflows(IEnumerable<HelpProposal> proposals) => proposals.Any(proposal => proposal.Kind == "workflow");

    /// <summary>
    /// Whether a pending proposal is a change to the open workflow draft, judged against the drafts its conversation's record
    /// says it has been told, so the record is read for those only then.
    /// </summary>
    public static bool JudgedAgainstDrafts(IEnumerable<HelpProposal> proposals) =>
        proposals.Any(proposal => proposal.Kind == "workflow" && proposal.Door == "draft");

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
            Plugins = PluginCatalog.Load(_loop.Home, ProviderRegistry.Built().Names),
            // A plugin is added from a registered checkout, never from the home, and may not shadow a
            // harness this build carries — what `daoris plugin add` refuses.
            Home = _loop.Home,
            Checkouts = snapshot.Repositories
                .GroupBy(known => known.Repository, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(same => same.Key, same => same.First().Root, StringComparer.OrdinalIgnoreCase),
            Reserved = ProviderRegistry.Built().Names,
            OffersFolder = context.OffersFolder,
            Offers = PluginOffers.Load(context.OffersFolder, _loop.Home, ProviderRegistry.Built().Names),
            Registered = [.. snapshot.Repositories.Select(known => (known.Repository, (string?)known.Workspace, known.Root))],
        };

        if (JudgedAgainstRecords(proposals))
        {
            var (quests, asks) = await HelpRecordFacts.ReadAsync(service, ct).ConfigureAwait(false);
            facts = facts with { Quests = quests, Asks = asks };
        }

        if (JudgedAgainstSessions(proposals))
        {
            facts = facts with { Sessions = records ?? await service.SessionRecordsAsync(ct).ConfigureAwait(false) };
        }

        // A clear proposal is judged by what this machine keeps of the work it names, as HISTORY_PLAN reads it.
        // A service that cannot be read throws, and the proposal stays unsettled.
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
                AcceptedGates = await IntegrateChoice.ReadForCardsAsync(config, workflows, facts.Registered, ct).ConfigureAwait(false),
            };
        }

        // a change to the open draft is judged against the drafts its conversation's own record says it has been told,
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
            var plugins = PluginCatalog.Load(_loop.Home, ProviderRegistry.Built().Names);
            var places = new Dictionary<string, PlacePlanned>(StringComparer.Ordinal);
            foreach (var proposal in proposals.Where(each => each.Kind == "setup" && each.Door == "place" && each.Place is not null))
            {
                places[proposal.Id] = await PlacePlans.PlanAsync(world, config, _loop.Home, plugins, context.Unadopted(config), proposal.Place!, ct).ConfigureAwait(false);
            }

            facts = facts with { Places = places };
        }

        return (config, facts);
    }
}
