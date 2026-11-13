namespace Daoris.Driver;

/// <summary>
/// Binds the delivery run of each planned ask: for every repository on this machine the ask's plan gives a part to, one run of the saved
/// <c>ask-delivery</c> workflow, keyed by the ask (<see cref="AskRunKey"/>) and bound once, the plan's parts read from the service.
/// </summary>
public sealed class AskRunBinder(string home, IWorkflowStore store, IWorkflowRunStore runs, ITrainPlans plans)
{
    /// <summary>The shipped workflow an ask's delivery run follows.</summary>
    public const string Shipped = "ask-delivery";

    private readonly Dictionary<string, int> _read = new(StringComparer.Ordinal);

    /// <summary>
    /// The runs bound for <paramref name="asks"/> at this look, each said once. An ask whose plan names no part, or is not read, binds none
    /// and is asked again at the next look; one whose plan was read with its quests as they are now is not asked again until they change.
    /// </summary>
    /// <param name="registry">The repositories registered here: a run is bound only where its repository has a checkout on this machine.</param>
    public async Task<IReadOnlyList<string>> BindAsync(
        DriverConfig config, IReadOnlyList<AskView> asks, IReadOnlyList<RepoView> registry, IReadOnlyList<WorkflowPlugin> plugins, DateTimeOffset now,
        CancellationToken ct)
    {
        var said = new List<string>();
        foreach (var ask in asks.Where(each => each.Quests.Count > 0))
        {
            if (_read.TryGetValue(ask.Id, out var quests) && quests == ask.Quests.Count) continue;
            if (await plans.OfAsync(ask.Id, ct).ConfigureAwait(false) is not { Parts.Count: > 0 } plan) continue;
            var bound = true;
            foreach (var repository in plan.Parts.Select(part => part.Repository).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (registry.FirstOrDefault(each => each.Root is { Length: > 0 } && string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)) is not { } at)
                {
                    continue;
                }

                var (done, line) = Bind(config, ask.Id, at, plugins, now);
                bound &= done;
                if (line is not null) said.Add(line);
            }

            if (bound) _read[ask.Id] = ask.Quests.Count;
        }

        return said;
    }

    /// <summary>One run's bind: whether the run stands bound now, and the line saying it where this bind made it; a bind that cannot be kept is tried again.</summary>
    private (bool Done, string? Said) Bind(DriverConfig config, string ask, RepoView at, IReadOnlyList<WorkflowPlugin> plugins, DateTimeOffset now)
    {
        if (AskRunKey.Of(ask, at.Repository) is not { } run) return (true, null);
        if (runs.Bound(run)) return (true, null);
        try
        {
            var workflow = SavedWorkflow();
            var binding = WorkflowRunBindings.Plan(
                config, store, run, at.Repository, at.Workspace, ask, new WorkflowTaskChoice(null, workflow), plugins, now, () => RoleCatalog.ReadHome(home));
            return (true, runs.Bind(binding) ? $"work  ask #{ask}: its delivery in `{at.Repository}` is run {run}, following `{workflow}` v{binding.Version}." : null);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException or ChoiceWords)
        {
            return (false, null);
        }
    }

    /// <summary>
    /// The saved workflow an ask's delivery follows: the home's first whose versions are a copy of the shipped one (a person's later version of it is
    /// the one that binds), saved from the shipped one where the home has none.
    /// </summary>
    private string SavedWorkflow() =>
        store.List().FirstOrDefault(file => file.Read is { Problem: null } read && read.Versions.Any(version => version.From?.Default == Shipped))?.Id
        ?? HomeSeed.Save(home, store, WorkflowDefaults.Of(Shipped)!);
}
