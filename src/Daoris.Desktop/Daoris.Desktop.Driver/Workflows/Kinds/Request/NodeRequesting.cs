namespace Daoris.Driver;

/// <summary>
/// A run's <c>request</c>, published through the request port the watch sets: left ready, saying why once a pass, where its door is not set or
/// its say-so is not given.
/// </summary>
/// <param name="quests">Every quest the last look read, by id.</param>
internal sealed class NodeRequesting(NodeRecordFiles records, Func<DateTimeOffset> clock, Func<IRequestPort?> requests, Func<Dictionary<string, QuestView>> quests)
{
    private RequestStart? _start;

    private RequestStart Request => _start ??= new RequestStart(records, requests, clock);

    /// <summary>A run's request is read on this look's projection of its quest before any new start, its key found first.</summary>
    public async Task<(IReadOnlyList<WorkPerformed> Done, IReadOnlyList<string> Said)> ReconcileAsync(
        IEnumerable<GovernedUnit> governed, CancellationToken ct)
    {
        var done = await Request.ReconcileAsync(governed, quests(), ct).ConfigureAwait(false);
        return (done, Request.TakeSaid());
    }

    /// <summary>
    /// A <c>request</c> the evaluation proposes to start: sent only under <c>start: automatic</c>, the person's standing say-so, and never to
    /// the run's own repository. Left ready, saying why once a look; null where nothing is done.
    /// </summary>
    public async Task<WorkPerformed?> StartAsync(
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
}
