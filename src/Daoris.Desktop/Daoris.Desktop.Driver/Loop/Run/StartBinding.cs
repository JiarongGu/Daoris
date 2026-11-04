namespace Daoris.Driver;

/// <summary>A quest's run bound to its workflow at its start, and the role its work step names.</summary>
internal static class StartBinding
{
    /// <summary>
    /// A start's bind of its run before its take (D190's binding moment): Workflows' use case over the loop's service, home, choices
    /// and plugins. A binding it made is kept for the next look's bind to say.
    /// </summary>
    public static async Task<WorkflowBind> AtStartAsync(LoopContext context, QuestView quest, string? workspace, CancellationToken ct)
    {
        var bound = await WorkflowRunStart.AtStartAsync(
                new WorkflowStarted(quest, workspace), new StartWorld(context.Service), context.Home, context.Config,
                WorkflowSteps.PluginsOf(context.Catalog), () => DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        if (bound.Came == WorkflowBindCame.Bound) context.Runs.Bound(bound);
        return bound;
    }

    /// <summary>
    /// The role a quest's run names for its work step, which its start walks: its kept binding's, or where the run is bound nowhere
    /// yet, the binding its bind would make now, since the walk comes before the bind; null where it names none.
    /// </summary>
    public static async Task<string?> StepRoleAsync(LoopContext context, QuestView quest, string? workspace, CancellationToken ct) =>
        (await WorkflowRunStart.PlannedAsync(
                new WorkflowStarted(quest, workspace), new StartWorld(context.Service), context.Home, context.Config,
                WorkflowSteps.PluginsOf(context.Catalog), () => DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false))?.RoleOf(NodeKinds.Claimed)?.Role;
}
