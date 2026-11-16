namespace Daoris.Driver;

/// <summary>
/// The two kinds Daoris gives a planned ask's work itself, so a place chooses the workflow each follows as it chooses any kind's:
/// <see cref="Part"/>, the run of a quest the ask's plan gave a part to, and <see cref="Delivery"/>, the ask's delivery run in a repository.
/// A place declares the kind in its workspace and maps it like any other (<c>daoris workflow kind</c>, <c>daoris workflow use --kind</c>);
/// where it maps none, a part follows the place's ordinary choice and a delivery the home's saved copy of the shipped <c>ask-delivery</c>.
/// </summary>
public static class TeamKinds
{
    /// <summary>The kind of a part's run.</summary>
    public const string Part = "team-part";

    /// <summary>The kind of an ask's delivery run.</summary>
    public const string Delivery = "ask-delivery";

    /// <summary>
    /// The task's choice <paramref name="kind"/> makes in <paramref name="repository"/>: the kind itself, where the repository or its workspace maps it to a
    /// workflow; null where neither does, so the run is chosen as it would be without it.
    /// </summary>
    public static WorkflowTaskChoice? Chosen(DriverConfig config, string repository, string? workspace, string kind)
    {
        var task = new WorkflowTaskChoice(kind, null);
        return WorkflowSelection.Resolve(config, repository, workspace, task).Level is WorkflowLevels.RepositoryKind or WorkflowLevels.WorkspaceKind ? task : null;
    }

    /// <summary>
    /// The choice a quest's run takes as a part: <see cref="Part"/> where the quest is the one its ask's plan gave a part to (its publish key names the pair,
    /// <see cref="TrainPartOf"/>), the ask makes no choice of its own, and the place maps the kind; else <paramref name="task"/>, the choice it had.
    /// The plan is read only where all the rest hold, so a place that maps no part asks the service nothing more.
    /// </summary>
    /// <param name="plans">The plans read; one the service would not answer throws, so the bind is not made and is tried again.</param>
    public static async Task<WorkflowTaskChoice?> OfPartAsync(
        DriverConfig config, QuestView quest, string? ask, string? workspace, WorkflowTaskChoice? task, ITrainPlans plans, CancellationToken ct)
    {
        if (task is not null || ask is null || Chosen(config, quest.To, workspace, Part) is not { } part) return task;
        return await TrainPartOf.ReadAsync(quest.Id, quest.Request, plans, reviewed: false, ct).ConfigureAwait(false) is null ? task : part;
    }
}
