namespace Daoris.Driver;

/// <summary>
/// A performed plugin step's progress as a run's step says it (<see cref="StepProgress"/>): waiting on the actor its plugin names, filled by
/// the holder <see cref="StepHolders"/> gives, or going on in its plugin's words. **Read, never stored**: from the attempt out now, so a
/// settled one, or one the step moved past, waits on no one. One rendering, whoever holds it.
/// </summary>
public static class StepWaiting
{
    /// <summary>The wait's code where it waits on an actor; its values name the actor, the holder, the words, the page and the stage.</summary>
    public const string Actor = "actor";

    /// <summary>The wait's code where it goes on and its plugin said where it stands.</summary>
    public const string Going = "progress";

    /// <summary>The progress of the plugin node's attempt out now, or null where it stands otherwise or its plugin said none.</summary>
    public static StepProgress? Of(NodeRecord? record, NodeDeclaration node, NodeStatus status) =>
        status.State == NodeStates.Active && NodeKit.IsPlugin(node.Kind)
        && record?.Newest(node.Id) is { State: AttemptStates.Recorded or AttemptStates.Sent, Progress: { } progress } attempt
        && status.Attempt == attempt.Attempt
            ? progress
            : null;

    /// <summary>The run step's wait for <paramref name="progress"/>: its code and the facts its words name, since its plugin said it.</summary>
    public static WorkflowRunWait WaitOf(StepProgress progress)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Put(string name, string? value)
        {
            if (value is { Length: > 0 }) values[name] = value;
        }

        Put("words", progress.Words);
        Put("url", progress.Url);
        Put("environment", progress.Environment);
        if (progress.Actor is { } actor)
        {
            var holder = StepHolders.Of(actor);
            Put("actor", actor);
            Put("holder", holder.Kind);
            Put("holderName", holder.Name);
        }

        return new WorkflowRunWait(progress.Actor is null ? Going : Actor, values, progress.At);
    }

    /// <summary>The terminal's words for a step's wait of these codes: on the actor, by its holder, then the plugin's words and the page.</summary>
    public static string Said(WorkflowRunWait wait)
    {
        var words = wait.Values.GetValueOrDefault("words") is { Length: > 0 } said ? $" {said.TrimEnd('.')}." : "";
        var page = wait.Values.GetValueOrDefault("url") is { Length: > 0 } url ? $" Open {url}" : "";
        if (wait.Code != Actor) return $"Under way.{words}{page}";
        var holder = wait.Values.GetValueOrDefault("holder") == StepHolders.Agent ? $"`{wait.Values.GetValueOrDefault("holderName") ?? "an agent"}`" : "you";
        return $"Waiting on `{wait.Values.GetValueOrDefault("actor")}`: {holder}.{words}{page}";
    }
}

/// <summary>A run whose plugin step waits on an actor the person holds: what <i>What needs you</i> lists, the act and its page.</summary>
/// <param name="Run">The run, as its binding names it.</param>
/// <param name="Node">The plugin node's id in the run's graph.</param>
/// <param name="Repository">The run's repository.</param>
/// <param name="Workspace">The run's workspace.</param>
/// <param name="Actor">The actor it waits on.</param>
/// <param name="Words">What its plugin said of the act.</param>
/// <param name="Url">The page where the act is taken.</param>
/// <param name="Work">The run's newest work quest here, or null.</param>
/// <param name="Since">Since when it waits.</param>
public sealed record ActorWait(string Run, string Node, string Repository, string Workspace, string Actor, string? Words, string? Url, string? Work, DateTimeOffset Since);

/// <summary>
/// The runs whose plugin step waits on an actor the person holds, judged from the runs the runtime last governed and each run's record read
/// now, as <see cref="HumanTasks"/> are. An actor an agent holds is that agent's, and no row of the person's.
/// </summary>
public static class ActorWaits
{
    public static IReadOnlyList<ActorWait> Of(IEnumerable<GovernedUnit> governed, Func<string, NodeRecord?> record)
    {
        var waits = new List<ActorWait>();
        foreach (var each in governed)
        {
            if (each.Scope is not { } scope || record(each.Unit.Id) is not { } kept) continue;
            foreach (var node in each.Graph.Nodes)
            {
                if (StepWaiting.Of(kept, node, WorkPerformer.StatusOf(kept, node.Id)) is not { Actor: { } actor } progress) continue;
                if (StepHolders.Of(actor).Kind != StepHolders.Person) continue;
                waits.Add(new ActorWait(each.Run, node.Id, each.Unit.Owner.Repository ?? "", each.Unit.Owner.Workspace, actor, progress.Words, progress.Url,
                    scope.Quests.LastOrDefault()?.Id, progress.At));
            }
        }

        return [.. waits.OrderBy(wait => wait.Run, StringComparer.Ordinal).ThenBy(wait => wait.Node, StringComparer.Ordinal)];
    }
}
