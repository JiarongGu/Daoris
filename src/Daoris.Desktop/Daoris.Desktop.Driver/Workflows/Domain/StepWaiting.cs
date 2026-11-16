namespace Daoris.Driver;

/// <summary>
/// What a run's step waits on, one reading for every step (<see cref="Read"/>): the role it waits on and that role's holder, whoever holds
/// it, since when, and for a performed plugin step its progress (<see cref="StepProgress"/>) in its plugin's words. **Read, never stored**:
/// from the record read now, so a settled step, or one the run moved past, waits on no one. One rendering, whoever holds it.
/// </summary>
public static class StepWaiting
{
    /// <summary>The wait's code where it waits on an actor; its values name the actor, the holder, the words, the page and the stage.</summary>
    public const string Actor = "actor";

    /// <summary>The wait's code where it goes on and its plugin said where it stands.</summary>
    public const string Going = "progress";

    /// <summary>Where a wait whose act is taken in the window's run view opens: a return's per-file choice, a go-ahead, a vacancy.</summary>
    public const string OpensRun = "run";

    /// <summary>The progress of the plugin node's attempt out now, or null where it stands otherwise or its plugin said none.</summary>
    public static StepProgress? Of(NodeRecord? record, NodeDeclaration node, NodeStatus status) =>
        status.State == NodeStates.Active && NodeKit.IsPlugin(node.Kind)
        && record?.Newest(node.Id) is { State: AttemptStates.Recorded or AttemptStates.Sent, Progress: { } progress } attempt
        && status.Attempt == attempt.Attempt
            ? progress
            : null;

    /// <summary>
    /// The one reading of a step's wait: the wait it stands at (<see cref="PersonWaits"/>), the role it waits on and its holder
    /// (<see cref="StepHolders"/>), or null where the step waits on no role (not reached, under way with an agent or a check, settled).
    /// </summary>
    /// <param name="record">The run's node record; null where only the declaration and status are in hand, which reads no plugin's progress and no failure kept.</param>
    public static RoleWait? Read(NodeRecord? record, NodeDeclaration node, NodeStatus status)
    {
        if (Of(record, node, status) is { Actor: { } actor } progress)
        {
            return new RoleWait(PersonWaits.Actor, actor, StepHolders.Of(actor), progress.At) { Words = progress.Words, Url = progress.Url };
        }

        var why = status.State switch
        {
            NodeStates.Unknown => PersonWaits.Held,
            NodeStates.Active => NodeKit.Of(node.Kind) switch
            {
                ReviewNode => PersonWaits.Accept,
                HumanNode => HumanNode.Presses(node).Contains(NodeWordActs.Approve, StringComparer.Ordinal) ? PersonWaits.Approval : PersonWaits.Task,
                GoAheadNode => PersonWaits.GoAhead,
                ReturnNode when status.Code == ReturnNode.BothChanged => PersonWaits.Choose,
                OpinionNode when status.Holds == OpinionGateReading.Vacant => PersonWaits.Vacant,
                _ => null,
            },
            NodeStates.Failed when record is not null && Left(node, status, record) => PersonWaits.Left,
            _ => null,
        };
        if (why is null) return null;
        var role = RoleOf(why);
        return new RoleWait(why, role, StepHolders.Of(role), status.Since);
    }

    /// <summary>
    /// The role a wait is on where its step names none: until a step's declaration carries its role (ACTORFIELD1), each wait's own. A
    /// vacancy and a failure kept go up to the line's lead (<see cref="LineActors.Lead"/>), whose holder answers or passes them up.
    /// </summary>
    public static string RoleOf(string why) => why switch
    {
        PersonWaits.Accept => "acceptor",
        PersonWaits.Approval or PersonWaits.GoAhead => "approver",
        PersonWaits.Task or PersonWaits.Choose => "worker",
        PersonWaits.Held => "operations",
        _ => LineActors.Lead,
    };

    /// <summary>Where a wait's act is taken in the window when no press of its own settles it from a row: the run view, else nowhere.</summary>
    public static string? OpensAt(string why) => why is PersonWaits.Choose or PersonWaits.GoAhead or PersonWaits.Vacant ? OpensRun : null;

    /// <summary>A step failed and kept for the person, no word on it yet: the declined request has a row of its own.</summary>
    private static bool Left(NodeDeclaration node, NodeStatus status, NodeRecord record) =>
        !(node.Kind == NodeKinds.Request && status.Code == RequestOptions.DeclinedCode)
        && record.Attempts.LastOrDefault(attempt => attempt.Node == node.Id && attempt.Attempt == status.Attempt) is { Resolution: null } attempt
        && WorkPerformer.KeptFailure(record, attempt);

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

/// <summary>A step's wait as <see cref="StepWaiting.Read"/> reads it: what it waits for, on which role, held by whom, since when.</summary>
/// <param name="Why">One of <see cref="PersonWaits"/>' codes.</param>
/// <param name="Role">The role it waits on: a plugin's actor as its plugin names it, else <see cref="StepWaiting.RoleOf"/>.</param>
/// <param name="Holder">The role's holder now.</param>
/// <param name="Since">Since when it waits; null on a record written before it carried a clock.</param>
public sealed record RoleWait(string Why, string Role, StepHolder Holder, DateTimeOffset? Since)
{
    /// <summary>What its plugin said of the act; null on every wait but a plugin's.</summary>
    public string? Words { get; init; }

    /// <summary>The page where the act is taken, as its plugin names it; null where none.</summary>
    public string? Url { get; init; }
}

/// <summary>A run step waiting on a role the person holds, as <i>What needs you</i> rows it: the wait, its presses and its page.</summary>
/// <param name="Run">The run, as its binding names it: what a press is said on.</param>
/// <param name="Node">The step's id in the run's graph.</param>
/// <param name="Repository">The run's repository.</param>
/// <param name="Workspace">The run's workspace.</param>
/// <param name="Why">The wait it stands at (<see cref="PersonWaits"/>).</param>
/// <param name="Role">The role it waits on.</param>
/// <param name="Presses">The step's own words that settle it (<see cref="RunNodeWords.Acts"/>), never its stop, which ends the work.</param>
/// <param name="Work">The run's newest work quest here, or null.</param>
/// <param name="Since">Since when it waits.</param>
public sealed record HeldStep(
    string Run, string Node, string Repository, string Workspace, string Why, string Role, IReadOnlyList<string> Presses, string? Work, DateTimeOffset? Since)
{
    /// <summary>The role's holder (<see cref="StepHolders"/>).</summary>
    public string Holder { get; init; } = StepHolders.Person;

    /// <summary>The agent's name where an agent holds it.</summary>
    public string? HolderName { get; init; }

    /// <summary>What its plugin said of the act.</summary>
    public string? Words { get; init; }

    /// <summary>The page where the act is taken, outside the window.</summary>
    public string? Url { get; init; }

    /// <summary>Where in the window its act is taken where no press of its own settles it (<see cref="StepWaiting.OpensRun"/>).</summary>
    public string? Opens { get; init; }
}

/// <summary>
/// The steps waiting on a role the person holds, from the runs the runtime last governed and each run's record read now: one row each in
/// <i>What needs you</i>, but for the waits another row already carries with its press (a review's Accept card, a part left, an attempt held).
/// </summary>
public static class HeldSteps
{
    public static IReadOnlyList<HeldStep> Of(IEnumerable<GovernedUnit> governed, Func<string, NodeRecord?> record)
    {
        var steps = new List<HeldStep>();
        foreach (var each in governed)
        {
            if (each.Scope is not { } scope || record(each.Unit.Id) is not { } kept) continue;
            foreach (var node in each.Graph.Nodes)
            {
                if (Step(each.Run, each.Unit.Owner.Repository ?? "", each.Unit.Owner.Workspace, scope.Quests.LastOrDefault()?.Id, node, kept) is { } step) steps.Add(step);
            }
        }

        return [.. steps.OrderBy(step => step.Run, StringComparer.Ordinal).ThenBy(step => step.Node, StringComparer.Ordinal)];
    }

    /// <summary>The row of one step, or null where it waits on no role the person holds, a word of theirs is pending on it, or another row carries it.</summary>
    public static HeldStep? Step(string run, string repository, string workspace, string? work, NodeDeclaration node, NodeRecord record)
    {
        var status = WorkPerformer.StatusOf(record, node.Id);
        if (StepWaiting.Read(record, node, status) is not { } wait || wait.Holder.Kind != StepHolders.Person
            || wait.Why is PersonWaits.Accept or PersonWaits.Left or PersonWaits.Held
            || record.Pending.Any(word => word.Node == node.Id))
        {
            return null;
        }

        var presses = RunNodeWords.Acts(node, status).Where(act => act != NodeWordActs.Stop).ToList();
        return new HeldStep(run, node.Id, repository, workspace, wait.Why, wait.Role, presses, work, wait.Since)
        {
            Holder = wait.Holder.Kind, HolderName = wait.Holder.Name, Words = wait.Words, Url = wait.Url, Opens = StepWaiting.OpensAt(wait.Why),
        };
    }
}
