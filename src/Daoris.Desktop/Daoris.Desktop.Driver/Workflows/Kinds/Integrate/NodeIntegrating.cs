namespace Daoris.Driver;

/// <summary>
/// The node the runtime starts through the door the watch sets: a unit's <c>integrate</c>, asked of the integration door by its key. It is left
/// ready, saying why once a pass, where its door is not set or its landing's acceptance is not the request.
/// </summary>
internal sealed class NodeIntegrating(
    NodeRecordFiles records, Func<DateTimeOffset> clock, Func<IIntegrationDoor?> integration, Func<IIntegrationLines?> lines,
    Func<DriverConfig?> config, Func<Action<string, string>?> integrated, Func<ITrainPlans?> plans)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IntegrationCardWait> _cards = new(StringComparer.Ordinal);

    private IntegrateStart? _integrateStart;

    private IntegrateStart Integrate => _integrateStart ??= new IntegrateStart(records, integration, clock) { Lines = lines };

    /// <summary>The landed branches whose <c>integrate</c> waits for the owner's card, as each unit's last judgement found it, oldest landing first.</summary>
    public IReadOnlyList<IntegrationCardWait> CardWaits(IReadOnlyDictionary<string, GovernedUnit> governed) =>
        [.. _cards.Values.Where(each => governed.ContainsKey(each.Unit)).OrderBy(each => each.Since).ThenBy(each => each.Unit, StringComparer.Ordinal)];

    /// <summary>A card wait is the unit's judgement now; one its evaluation does not propose to start is gone.</summary>
    public void Forget(string unit) => _cards.TryRemove(unit, out _);

    /// <summary>
    /// A unit's request is asked after, by its key, before any new start: a request a restart finds, one a new landing tip invalidated its node
    /// of and the outcome that lifts the gate on a second one, and one the door never took.
    /// </summary>
    public async Task<(IReadOnlyList<WorkPerformed> Done, IReadOnlyList<string> Said)> ReconcileIntegrationsAsync(CancellationToken ct)
    {
        var done = await Integrate.ReconcileAsync(ct).ConfigureAwait(false);
        return (done, Integrate.TakeSaid());
    }

    /// <summary>
    /// An <c>integrate</c> the evaluation proposes to start: the landing's own branch at the tip <c>handed</c> recorded, asked of the door by
    /// the unit and that tip, once the landing's acceptance is the request and nothing is unreconciled on the node. Left ready, saying why
    /// once a pass, where the landing waits for the owner's card or the steward's act, or where no door is set; null where nothing is done.
    /// </summary>
    public async Task<WorkPerformed?> IntegrateAsync(
        GovernedUnit governed, NodeDeclaration node, WireStart start, IReadOnlyDictionary<string, NodeStatus> statuses, List<string> said, CancellationToken ct)
    {
        if (integration() is not { } door || config() is not { } settings) return null;
        var unit = governed.Unit.Id;
        if (NodeEvaluations.LandingOf(governed, statuses) is not { } landed)
        {
            var line = $"work  {unit}: its integration waits: its landing's record does not hold the tip `handed` recorded.";
            if (!said.Contains(line, StringComparer.Ordinal)) said.Add(line);
            return null;
        }

        var repository = governed.Watched.Repository;
        var ask = new IntegrationAsk(repository, governed.Watched.Workspace, landed.Root, landed.Entry.Branch, IntegrationRoutes.Node, IntegrationRequester.NodeStart)
        {
            Unit = unit, Tip = landed.Tip, Request = NodeOptions.Text(node, "request"), Conflicts = NodeOptions.Text(node, "conflicts"),
        };
        // A part's worker lands on its ask's branch through the same queue, its request tagged so the train reads it.
        if ((governed.Scope?.Quest?.Records.Quest ?? governed.Unit.Records.Quest) is { } quest
            && await TrainPartOf.ReadAsync(quest.Id, quest.Request, plans(), ct).ConfigureAwait(false) is { } train)
        {
            ask = ask with { Train = train };
        }

        // The landing's acceptance read first, with no git and no write: where it is no request, the node waits ready for the owner's card
        // or the steward's act, and nothing is written (a line gaining a policy over standing landings requests none of them).
        var policy = settings.LinePolicyOf(repository, governed.Watched.Workspace);
        if (IntegrationRequests.NodeJudgement(ask, landed.Entry, policy).Refused is { } waits)
        {
            // The owner's card made this landing's request at its tip (the recorded-landing door keeps its unit), so the node follows that
            // request as its own; until one is made, the wait is the owner's to answer where their card is the request.
            IntegrationRequest? carded;
            try
            {
                carded = await door.FindAsync(unit, landed.Tip, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (!ct.IsCancellationRequested && error is IOException or Microsoft.Data.Sqlite.SqliteException or TimeoutException or DriverException)
            {
                carded = null;
            }

            if (carded is null)
            {
                var asks = PlaceIntegrates.Of(WorkflowPorts.Store(records.Home), settings, repository, governed.Watched.Workspace).Request;
                if (IntegrationCardWait.Waits(waits.Refusal, asks)) _cards[unit] = new IntegrationCardWait(unit, repository, governed.Watched.Workspace, landed.Entry.Branch, landed.Tip, waits.Refusal!, waits.Message, landed.Entry.LandedAt);
                else _cards.TryRemove(unit, out _);
                var line = $"work  {unit}: its integration waits: {waits.Message}";
                if (!said.Contains(line, StringComparer.Ordinal)) said.Add(line);
                return null;
            }
        }

        _cards.TryRemove(unit, out _);
        var sent = await Integrate.StartAsync(node, start, statuses[start.Node], ask, settings, ct).ConfigureAwait(false);
        said.AddRange(Integrate.TakeSaid());
        return sent;
    }

    /// <summary>
    /// <c>work.integrated</c>: a <c>line</c> this round moved to reached, in a graph whose <c>integrate</c> is reached, is the line holding work
    /// the unit's integration promoted. Told once, as the move is kept once.
    /// </summary>
    public void IntegratedNow(GovernedUnit governed, IReadOnlyDictionary<string, NodeStatus> before, IReadOnlyDictionary<string, NodeStatus> after, List<string> said)
    {
        var nodes = governed.Declared.Values;
        if (!nodes.Any(node => node.Kind == NodeKinds.Integrate && after.GetValueOrDefault(node.Id)?.State == NodeStates.Reached)) return;
        foreach (var line in nodes.Where(node => node.Kind == NodeKinds.Line))
        {
            if (after.GetValueOrDefault(line.Id)?.State != NodeStates.Reached || before.GetValueOrDefault(line.Id)?.State == NodeStates.Reached) continue;
            said.Add($"work  {governed.Unit.Id}: integrated into its line.");
            integrated()?.Invoke(governed.Unit.Id, governed.Watched.Repository);
            return;
        }
    }
}
