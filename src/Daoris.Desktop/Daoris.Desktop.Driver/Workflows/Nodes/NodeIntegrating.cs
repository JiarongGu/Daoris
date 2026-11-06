namespace Daoris.Driver;

/// <summary>
/// The two nodes the runtime starts through doors the watch sets: a unit's <c>integrate</c>, asked of the integration door by its key, and a
/// run's <c>request</c>, published through the request port. Each is left ready, saying why once a pass, where its door is not set or its
/// say-so is not given.
/// </summary>
/// <param name="quests">Every quest the last look read, by id.</param>
internal sealed class NodeIntegrating(
    NodeRecordFiles records, Func<DateTimeOffset> clock, Func<IIntegrationDoor?> integration, Func<IIntegrationLines?> lines,
    Func<IRequestPort?> requests, Func<DriverConfig?> config, Func<Dictionary<string, QuestView>> quests, Func<Action<string, string>?> integrated)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IntegrationCardWait> _cards = new(StringComparer.Ordinal);

    private IntegrateStart? _integrateStart;

    private RequestStart? _requestStart;

    private IntegrateStart Integrate => _integrateStart ??= new IntegrateStart(records, integration, clock) { Lines = lines };

    private RequestStart Request => _requestStart ??= new RequestStart(records, requests, clock);

    /// <summary>The landed branches whose <c>integrate</c> waits for the owner's card, as each unit's last judgement found it, oldest landing first.</summary>
    public IReadOnlyList<IntegrationCardWait> CardWaits(IReadOnlyDictionary<string, GovernedUnit> governed) =>
        [.. _cards.Values.Where(each => governed.ContainsKey(each.Unit)).OrderBy(each => each.Since).ThenBy(each => each.Unit, StringComparer.Ordinal)];

    /// <summary>A card wait is the unit's judgement now; one its evaluation no longer proposes to start is gone.</summary>
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

    /// <summary>A run's request is read on this look's projection of its quest before any new start, its key found first.</summary>
    public async Task<(IReadOnlyList<WorkPerformed> Done, IReadOnlyList<string> Said)> ReconcileRequestsAsync(
        IEnumerable<GovernedUnit> governed, CancellationToken ct)
    {
        var done = await Request.ReconcileAsync(governed, quests(), ct).ConfigureAwait(false);
        return (done, Request.TakeSaid());
    }

    /// <summary>
    /// An <c>integrate</c> the evaluation proposes to start: the landing's own branch at the tip <c>handed</c> recorded, asked of the door by
    /// the unit and that tip, once the landing's acceptance was the request and nothing is unreconciled on the node. Left ready, saying why
    /// once a pass, where the landing waits for the owner's card or the steward's act, or where no door is set; null where nothing was done.
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

        // The landing's acceptance read first, with no git and no write: where it was no request, the node waits ready for the owner's card
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
    /// A <c>request</c> the evaluation proposes to start: sent only under <c>start: automatic</c>, the person's standing say-so, and never to
    /// the run's own repository. Left ready, saying why once a look; null where nothing was done.
    /// </summary>
    public async Task<WorkPerformed?> RequestAsync(
        GovernedUnit governed, NodeDeclaration node, WireStart start, NodeStatus status, List<string> said, CancellationToken ct)
    {
        if (requests() is null) return null;
        var unit = governed.Unit.Id;
        string? why = RequestOptions.Problem(node) is { } option ? $"its `{option}` option is not one a request takes"
            : string.Equals(RequestOptions.Repository(NodeOptions.Text(node, "to") ?? ""), governed.Watched.Repository, StringComparison.OrdinalIgnoreCase)
                ? "a request never goes to the run's own repository: a quest to itself is the chain's"
            : NodeOptions.Text(node, "start") != RequestOptions.Automatic
                && records.Read(unit)?.Fired.Contains(RunNodeWords.SendKey(node.Id, status.Attempt), StringComparer.Ordinal) != true ? "it waits for your send" : null;
        if (why is not null)
        {
            var line = $"work  {unit}: its request `{node.Id}` was not sent: {why}.";
            if (!said.Contains(line, StringComparer.Ordinal)) said.Add(line);
            return null;
        }

        var sent = await Request.StartAsync(node, start, status, governed.Run, governed.Watched.Repository, quests(), ct).ConfigureAwait(false);
        said.AddRange(Request.TakeSaid());
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
