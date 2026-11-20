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
    public static RoleWait? Read(NodeRecord? record, NodeDeclaration node, NodeStatus status, StepTenure? held = null, Func<string, string>? cast = null)
    {
        StepHolder HolderOf(string role) => CastHolders.StepOf(StepWaiting.HolderOf(node, status, role, held, cast));
        if (Of(record, node, status) is { Actor: { } actor } progress)
        {
            return new RoleWait(PersonWaits.Actor, actor, HolderOf(actor), progress.At) { Words = progress.Words, Url = progress.Url };
        }

        var why = status.State switch
        {
            NodeStates.Unknown => PersonWaits.Held,
            NodeStates.Active => (NodeKit.Of(node.Kind) as IWaitsOnRole)?.Waits(node, status),
            NodeStates.Failed when record is not null && Left(node, status, record) => PersonWaits.Left,
            _ => null,
        };
        if (why is null) return null;
        var role = OnOwnRole(why) ? StepRoles.Of(node) : RoleOf(why);
        return new RoleWait(why, role, HolderOf(role), status.Since);
    }

    /// <summary>Who holds the role a wait is on: the step's own role its tenure, any other role its place's cast, else the cast's rule (<see cref="PlaceCastBook.Uncast"/>).</summary>
    public static string HolderOf(NodeDeclaration node, NodeStatus status, string role, StepTenure? held, Func<string, string>? cast)
    {
        var own = role == StepRoles.Of(node);
        if (own && held is { } tenure && tenure.Node == node.Id && tenure.Attempt == status.Attempt) return tenure.Holder;
        return (own ? HolderWords.First(node) : null) ?? cast?.Invoke(role) ?? PlaceCastBook.Uncast(role);
    }

    /// <summary>
    /// The role a wait is on where its step names none: until a step's declaration carries its role, each wait's own. A
    /// vacancy and a failure kept go up to the line's first level, the place's supervisor (<see cref="AppointedRoles.Supervisor"/>), whose
    /// holder answers or passes them up.
    /// </summary>
    public static string RoleOf(string why) => why switch
    {
        PersonWaits.Accept => "acceptor",
        PersonWaits.Approval or PersonWaits.GoAhead => "approver",
        PersonWaits.Task or PersonWaits.Choose => "worker",
        PersonWaits.Held => "operations",
        _ => AppointedRoles.Supervisor,
    };

    public static bool OnOwnRole(string why) =>
        why is PersonWaits.Accept or PersonWaits.Approval or PersonWaits.Task or PersonWaits.GoAhead or PersonWaits.Pass;

    /// <summary>Where a wait's act is taken in the window when no press of its own settles it from a row: the run view, else nowhere.</summary>
    public static string? OpensAt(string why) => why is PersonWaits.Choose or PersonWaits.GoAhead or PersonWaits.Vacant ? OpensRun : null;

    /// <summary>A step failed and kept for the person, no word on it yet: the declined request has a row of its own.</summary>
    private static bool Left(NodeDeclaration node, NodeStatus status, NodeRecord record) =>
        !(node.Kind == NodeKinds.Request && status.Code == RequestOptions.DeclinedCode)
        && record.Attempts.LastOrDefault(attempt => attempt.Node == node.Id && attempt.Attempt == status.Attempt) is { Resolution: null } attempt
        && WorkPerformer.KeptFailure(record, attempt);

    /// <summary>The run step's wait for <paramref name="progress"/>: its code and the facts its words name, since its plugin said it.</summary>
    public static WorkflowRunWait WaitOf(StepProgress progress, StepHolder? holder = null)
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
            var filled = holder ?? CastHolders.StepOf(PlaceCastBook.Uncast(actor));
            Put("actor", actor);
            Put("holder", filled.Kind);
            Put("holderName", filled.Name);
        }

        return new WorkflowRunWait(progress.Actor is null ? Going : Actor, values, progress.At);
    }

    /// <summary>The terminal's words for a step's wait of these codes: on the actor, by its holder, then the plugin's words and the page.</summary>
    public static string Said(WorkflowRunWait wait)
    {
        var words = wait.Values.GetValueOrDefault("words") is { Length: > 0 } said ? $" {said.TrimEnd('.')}." : "";
        var page = wait.Values.GetValueOrDefault("url") is { Length: > 0 } url ? $" Open {url}" : "";
        if (wait.Code != Actor) return $"Under way.{words}{page}";
        var holder = new StepHolder(wait.Values.GetValueOrDefault("holder") ?? StepHolders.Person, wait.Values.GetValueOrDefault("holderName"));
        return $"{Line(wait.Values.GetValueOrDefault("actor") ?? "", holder)}{words}{page}";
    }

    /// <summary>
    /// The one line of every wait, whoever holds it: <i>Waiting on `role`: holder.</i>, the holder as the person reads it (<i>you</i> for their
    /// own holding, an agent by its name). Nothing else branches on who holds it.
    /// </summary>
    public static string Line(string role, StepHolder holder) =>
        $"Waiting on `{role}`: {(holder.Kind == StepHolders.Agent ? $"`{holder.Name ?? "an agent"}`" : "you")}.";
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
/// <param name="Presses">The step's own words that settle it (<see cref="RunNodeWords.Acts"/>), and its stop, which ends the work, where it offers one.</param>
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

    public int? Epoch { get; init; }

    public IReadOnlyList<string>? Holds { get; init; }

    /// <summary>
    /// The attempt out now of a plugin's role wait with no page, which its <i>Done outside Daoris</i> (<see cref="HeldResolutions.DoneOutside"/>)
    /// is said on; null on every other wait.
    /// </summary>
    public WorkflowRunHeld? Held { get; init; }

    public bool Handed { get; init; }
}

/// <summary>
/// The steps waiting on a role the person holds, from the runs the runtime last governed and each run's record read now: one row each in
/// <i>What needs you</i>, but for the waits another row already carries with its press (a review's Accept card, a part left, an attempt held).
/// </summary>
public static class HeldSteps
{
    public static IReadOnlyList<HeldStep> Of(
        IEnumerable<GovernedUnit> governed, Func<string, NodeRecord?> record, StepTenureFiles? tenures = null, PlaceCastBook? casts = null, PlaceRiskBook? risk = null)
    {
        var steps = new List<HeldStep>();
        foreach (var each in governed)
        {
            if (each.Scope is not { } scope || record(each.Unit.Id) is not { } kept) continue;
            var holding = tenures?.Holding(each.Unit.Id, kept.Statuses.ToDictionary(pair => pair.Key, pair => pair.Value.Attempt, StringComparer.Ordinal));
            var cast = casts is null ? null : CastReading.At(casts, risk, each.Unit.Owner.Workspace, each.Unit.Owner.Repository);
            foreach (var node in each.Graph.Nodes)
            {
                var held = holding?.GetValueOrDefault(node.Id);
                if (Step(each.Run, each.Unit.Owner.Repository ?? "", each.Unit.Owner.Workspace, scope.Quests.LastOrDefault()?.Id, node, kept, held, cast) is { } step) steps.Add(step);
            }
        }

        return [.. steps.OrderBy(step => step.Run, StringComparer.Ordinal).ThenBy(step => step.Node, StringComparer.Ordinal)];
    }

    /// <summary>The row of one step, or null where a word is pending on it, another row carries it, or an agent's channel settles it.</summary>
    public static HeldStep? Step(
        string run, string repository, string workspace, string? work, NodeDeclaration node, NodeRecord record, StepTenure? held = null, Func<string, string>? cast = null)
    {
        var status = WorkPerformer.StatusOf(record, node.Id);
        if (StepWaiting.Read(record, node, status, held, cast) is not { } wait || record.Pending.Any(word => word.Node == node.Id)) return null;
        var carried = wait.Why is PersonWaits.Left or PersonWaits.Held || (wait.Why == PersonWaits.Accept && wait.Holder.Kind == StepHolders.Person);
        var tenure = held is { } kept && kept.Node == node.Id && kept.Attempt == status.Attempt ? kept : null;

        // A step its agent's working session settles by its tool or its acts is handed to that session: the person's one press is Take.
        if (!carried && AgentSettles.Handed(node, status, wait))
        {
            return new HeldStep(run, node.Id, repository, workspace, wait.Why, wait.Role, [], work, wait.Since)
            {
                Holder = wait.Holder.Kind, HolderName = wait.Holder.Name, Words = wait.Words, Url = wait.Url, Epoch = tenure?.Epoch,
                Holds = StepHoldActs.Handed(tenure), Handed = true,
            };
        }

        if (carried || (wait.Holder.Kind != StepHolders.Person && AgentSettles.Wait(wait.Why))) return null;
        var presses = RunNodeWords.Acts(node, status).ToList();
        // A plugin's role wait with no page is acted on where nothing observes it: its holder says it is done outside, with the link.
        var outside = wait is { Why: PersonWaits.Actor, Url: null } && PluginFailureHeld.Of(record, node, status) is { } attempt
            && attempt.Words.Contains(HeldResolutions.DoneOutside) ? attempt : null;
        if (outside is not null) presses.Add(HeldResolutions.DoneOutside);
        return new HeldStep(run, node.Id, repository, workspace, wait.Why, wait.Role, presses, work, wait.Since)
        {
            Holder = wait.Holder.Kind, HolderName = wait.Holder.Name, Words = wait.Words, Url = wait.Url, Opens = StepWaiting.OpensAt(wait.Why),
            Held = outside, Epoch = tenure?.Epoch, Holds = HoldsOn(node, wait, tenure),
        };
    }

    /// <summary>The hold acts a row offers: they pass the step's own tenure on, so never on a go-ahead, which the grants answer.</summary>
    public static IReadOnlyList<string>? HoldsOn(NodeDeclaration node, RoleWait? wait, StepTenure? tenure) =>
        tenure is not null && wait?.Why != PersonWaits.GoAhead && (wait is null || wait.Role == StepRoles.Of(node)) ? StepHoldActs.Of(tenure) : null;
}
