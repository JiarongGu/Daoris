namespace Daoris.Driver;

/// <summary>The run's graph: Current live, or the exact steps of its saved version.</summary>
internal static class WorkflowRunGraph
{
    public static CurrentWorkflow Read(WorkflowProcess process, CurrentWorkflow current, IReadOnlyList<WorkflowPlugin> plugins)
    {
        if (process.Problem is not null || process.Unread is not null)
            return new(current.StartHolds, [], process.Binding?.Digest ?? "unread");
        if (!process.Named) return current;

        return new(current.StartHolds, process.Steps.Select(named =>
        {
            var row = WorkflowKindTable.Of(named.Kind)!;
            var plugin = process.Landing.Rule.Plugin;
            var speaker = plugins.FirstOrDefault(each => string.Equals(each.Id, plugin, StringComparison.OrdinalIgnoreCase));
            var lands = speaker?.Points.Contains(HookPoints.Land, StringComparer.Ordinal) == true;
            var limit = named.Kind is WorkflowKinds.Landing or WorkflowKinds.PullRequest && plugin is not null
                ? !lands ? WorkflowLimits.PluginUnready
                    : named.Kind == WorkflowKinds.PullRequest && speaker!.Points.Contains(HookPoints.State, StringComparer.Ordinal) == false
                        ? WorkflowLimits.PullRequestUnread : row.Limit
                : row.Limit;
            var automatic = named.Kind == WorkflowKinds.Landing && process.Landing.Rule.AutoAccept;
            var participation = named.Kind switch
            {
                WorkflowKinds.Work or WorkflowKinds.Opinion => WorkflowParticipation.Agent,
                WorkflowKinds.Look => WorkflowParticipation.AgentAndYou,
                WorkflowKinds.Landing when automatic => WorkflowParticipation.Automatic,
                _ => WorkflowParticipation.You,
            };
            var executor = named.Kind switch
            {
                WorkflowKinds.Work or WorkflowKinds.Opinion or WorkflowKinds.Look => WorkflowExecutor.Agent,
                WorkflowKinds.PullRequest => WorkflowExecutor.Plugin,
                _ => WorkflowExecutor.Daoris,
            };
            var press = named.Kind switch
            {
                WorkflowKinds.Look => WorkflowPress.Reviewed,
                WorkflowKinds.Landing when !automatic => WorkflowPress.Accept,
                WorkflowKinds.PullRequest => WorkflowPress.Merge,
                _ => null,
            };
            IReadOnlyList<WorkflowSetting> settings = named.Kind switch
            {
                WorkflowKinds.Work => current.Steps.First(step => step.Kind == WorkflowKinds.Work).Settings,
                WorkflowKinds.Look => [new("environment", process.Look?.Environment)],
                WorkflowKinds.Landing => [new("form", process.Landing.Rule.Form),
                    new("accept", automatic ? "automatic" : "you"), new("pattern", process.Landing.Rule.Pattern),
                    new("plugin", process.Landing.Rule.Plugin), new("tidy", process.Landing.Rule.Tidy)],
                WorkflowKinds.PullRequest => [new("plugin", process.Landing.Rule.Plugin)],
                WorkflowKinds.Opinion when process.Opinion is { } opinion => [new("reviewers", opinion.Rule.Reviewers.ToList()),
                    new("on", opinion.Rule.On.ToList()), new("required", opinion.Rule.Required), new("recheck", opinion.Rule.Recheck)],
                _ => named.Fields,
            };
            return new WorkflowStep(named.Id, named.Kind, participation, executor, press,
                new WorkflowSource("workflow", process.Binding!.Level), settings, row.Runtime, limit);
        }).ToList(), process.Binding!.Digest!);
    }
}
