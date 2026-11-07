namespace Daoris.Driver;

/// <summary>A start the loop opened in a look, as binding its run reads it: the quest, and its repository's workspace.</summary>
public sealed record WorkflowStarted(QuestView Quest, string? Workspace);

/// <summary>
/// What binding a run at its first start reads and says beyond the home: the service's
/// quests, which name a later quest's run, and its asks, whose latest choice is the task's; and the machine log's
/// <c>workflow.chosen</c> line. A port: the loop hands it over its service client.
/// </summary>
public interface IWorkflowStartWorld
{
    /// <summary>Every quest the service holds, closed ones included, from which a chain is read.</summary>
    Task<IReadOnlyList<QuestView>> EveryQuestAsync(CancellationToken ct);

    /// <summary>One ask, or null where the service holds none.</summary>
    Task<AskView?> FindAskAsync(string id, CancellationToken ct);

    /// <summary>The machine log's line for a run bound (<see cref="WorkflowLines.Chosen"/>).</summary>
    void Chosen(LandingLine line);
}

/// <summary>What a run's bind at a start came to.</summary>
public enum WorkflowBindCame
{
    /// <summary>This start bound the run: the look says it, where a named workflow chose it or it could not be read.</summary>
    Bound,

    /// <summary>The run is bound already, by an earlier start or one beside this one; its binding is the one kept.</summary>
    Already,

    /// <summary>The quest's work names no run (its id names no file), so nothing is bound and nothing holds.</summary>
    None,

    /// <summary>The run's binding is kept and does not read, so the workflow its take would name is not known: the start holds.</summary>
    Unread,

    /// <summary>The bind is not made (the service does not answer, its file cannot be written): the start holds, and is tried again.</summary>
    Failed,
}

/// <summary>
/// A run's bind at a start: the quest it is made for, the run, the binding kept, by this start or
/// before it, and why a run is not bound. The start's take reads <see cref="Binding"/> (<c>ClosureBinding.AtTakeAsync</c>).
/// </summary>
/// <param name="Quest">The quest whose start bound it.</param>
/// <param name="Came">What the bind came to.</param>
/// <param name="Run">The run its work is; null where its id names none.</param>
/// <param name="Binding">The run's binding as kept now; null where it is not bound, or its binding does not read.</param>
/// <param name="Why">Why its run is not bound, which the start's hold says; null where it is.</param>
public sealed record WorkflowBind(QuestView Quest, WorkflowBindCame Came, string? Run, WorkflowRunBinding? Binding, string? Why) : IRunBound
{
    /// <summary>Whether the start holds on it: its run is not bound, so no take may go on under a graph it is not bound to.</summary>
    public bool Holds => Came is WorkflowBindCame.Unread or WorkflowBindCame.Failed;
}

/// <summary>
/// A run bound to its workflow at its first start.
/// </summary>
/// <remarks>
/// <para><b>The binding moment</b>: a start binds its run inside itself, once it is past the holds before its tree and
/// before its take names a closure graph (<see cref="AtStartAsync(WorkflowStarted, IWorkflowStartWorld, string, DriverConfig, IReadOnlyList{WorkflowPlugin}, Func{DateTimeOffset}, CancellationToken)"/>,
/// <c>Driver.RunAsync</c>), so the take names the graph its run is bound to. The binding stands though that start opens no session, and
/// the choice is read once, there: one changed after applies to the next run.</para>
/// <para><b>The look's say</b> (<see cref="Say"/>, <c>Driver.BindRunsAsync</c>) says what the starts bound since the last look.</para>
/// </remarks>
public static class WorkflowRunStart
{
    /// <summary>
    /// The look's say: each binding <paramref name="made"/> holds that a start made since the last look, said once where a named
    /// workflow chose it; a run under Current is bound silently, as nothing about its work changes.
    /// </summary>
    /// <param name="events">The look's lines, which a binding adds to.</param>
    /// <param name="said">Where each line is classed: a binding's is <c>progress</c>, said on the run's own place.</param>
    /// <param name="made">What the starts' binds came to since the last look; those this look says are those they bound.</param>
    public static void Say(IReadOnlyList<WorkflowBind> made, List<string> events, LookSaid? said = null)
    {
        said ??= LookSaid.None;
        foreach (var bound in made)
        {
            if (bound is { Came: WorkflowBindCame.Bound, Binding: { } binding } && Says(binding))
            {
                events.Add(said.Progress("workflow", WorkflowRunBindings.Said(binding, bound.Quest.Id)));
            }
        }
    }

    /// <summary>
    /// The start's bind: its run bound to the workflow its selection chooses for it now
    /// where no binding names it yet, its binding moved into place without overwriting so two starts of one run at once keep the first's; or
    /// the binding already kept, read. A bind not made, or a binding that does not read, says why, and the start holds on it: a take that
    /// went on would name a graph its run may not be bound to. The machine log's line is written as a binding is made; the look says it.
    /// </summary>
    public static Task<WorkflowBind> AtStartAsync(
        WorkflowStarted start, IWorkflowStartWorld world, string home, DriverConfig config, IReadOnlyList<WorkflowPlugin> plugins,
        Func<DateTimeOffset> now, CancellationToken ct) =>
        AtStartAsync(start, world, WorkflowPorts.Store(home), WorkflowPorts.Runs(home), config, plugins, now, ct, () => RoleCatalog.ReadHome(home));

    /// <summary>The start's bind through the ports.</summary>
    /// <param name="roles">The role catalogue a step's role is judged by; null judges by the seed's.</param>
    public static Task<WorkflowBind> AtStartAsync(
        WorkflowStarted start, IWorkflowStartWorld world, IWorkflowStore store, IWorkflowRunStore runs, DriverConfig config,
        IReadOnlyList<WorkflowPlugin> plugins, Func<DateTimeOffset> now, CancellationToken ct, Func<RoleCatalog>? roles = null) =>
        BindOneAsync(start, world, store, runs, config, plugins, now, new Every(), roles, ct);

    /// <summary>
    /// The binding a start's bind would come to now, written nowhere: its run's kept binding, or the one its first start would
    /// make, which the start's walk reads its work step's role from before its bind. Null where its work names no run, or the binding cannot
    /// be read or planned; the bind then says why and holds the start.
    /// </summary>
    public static async Task<WorkflowRunBinding?> PlannedAsync(
        WorkflowStarted start, IWorkflowStartWorld world, string home, DriverConfig config, IReadOnlyList<WorkflowPlugin> plugins,
        Func<DateTimeOffset> now, CancellationToken ct)
    {
        var (store, runs, every) = (WorkflowPorts.Store(home), WorkflowPorts.Runs(home), new Every());
        try
        {
            var run = await RunOfAsync(start.Quest, world, every, ct).ConfigureAwait(false);
            if (!WorkflowRunBindings.IsRunId(run)) return null;
            if (runs.Bound(run)) return runs.Read(run);
            return await PlanAsync(start, run, world, store, config, plugins, now, every, () => RoleCatalog.ReadHome(home), ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Unbound(error, ct))
        {
            return null;
        }
    }

    /// <summary>What the start's hold says of a bind that is not made.</summary>
    private const string NotBound = "its run was not bound to a workflow, and its quest is taken only once it is";

    /// <summary>
    /// One run's bind: the run its quest's work is, bound where no binding names it; a binding already kept is read (the start's take
    /// reads it).
    /// </summary>
    private static async Task<WorkflowBind> BindOneAsync(
        WorkflowStarted start, IWorkflowStartWorld world, IWorkflowStore store, IWorkflowRunStore runs, DriverConfig config,
        IReadOnlyList<WorkflowPlugin> plugins, Func<DateTimeOffset> now, Every every, Func<RoleCatalog>? roles, CancellationToken ct)
    {
        var quest = start.Quest;
        string? run = null;
        try
        {
            run = quest.Id;
            run = await RunOfAsync(quest, world, every, ct).ConfigureAwait(false);
            if (!WorkflowRunBindings.IsRunId(run)) return new(quest, WorkflowBindCame.None, null, null, null);
            if (runs.Bound(run)) return Kept(quest, runs, run);

            var binding = await PlanAsync(start, run, world, store, config, plugins, now, every, roles, ct).ConfigureAwait(false);
            if (!runs.Bind(binding))
            {
                // Another start of the run bound it first, between the look above and this write: its
                // binding is the run's, and this start takes under it.
                return Kept(quest, runs, run);
            }

            // The machine log keeps which level chose and whether a named version or Current.
            world.Chosen(WorkflowLines.Chosen(binding));
            return new(quest, WorkflowBindCame.Bound, run, binding, null);
        }
        catch (Exception error) when (Unbound(error, ct))
        {
            return new(quest, WorkflowBindCame.Failed, run, null, $"{NotBound}: {error.Message}");
        }
    }

    /// <summary>The failures a bind is not made on, said and tried again at the next look.</summary>
    private static bool Unbound(Exception error, CancellationToken ct) =>
        error is HttpRequestException or System.Text.Json.JsonException or DriverException or IOException or UnauthorizedAccessException
        || (error is OperationCanceledException && !ct.IsCancellationRequested);

    /// <summary>The run a quest's work is: a chain's first quest has no parent, and names its run without the chain being read.</summary>
    private static async Task<string> RunOfAsync(QuestView quest, IWorkflowStartWorld world, Every every, CancellationToken ct)
    {
        if (quest.Parent is not { Length: > 0 }) return quest.Id;
        every.Quests ??= await world.EveryQuestAsync(ct).ConfigureAwait(false);
        return WorkflowRunBindings.RunOf(ReviewGate.ChainOf(every.Quests, quest.Id), quest.To) ?? quest.Id;
    }

    /// <summary>The binding the run's first start makes now, its task's choice read from its ask, or for a quest no ask published, its own.</summary>
    private static async Task<WorkflowRunBinding> PlanAsync(
        WorkflowStarted start, string run, IWorkflowStartWorld world, IWorkflowStore store, DriverConfig config, IReadOnlyList<WorkflowPlugin> plugins,
        Func<DateTimeOffset> now, Every every, Func<RoleCatalog>? roles, CancellationToken ct)
    {
        var quest = start.Quest;
        var askId = AskWords.AskOf(quest.From);
        var ask = askId is null ? null : await world.FindAskAsync(askId, ct).ConfigureAwait(false);
        // A quest no ask published carries its own choice, made on its page before it starts.
        var task = askId is null ? WorkflowRunBindings.TaskOf(quest, every.Quests) : WorkflowRunBindings.TaskOf(ask);
        return WorkflowRunBindings.Plan(config, store, run, quest.To, start.Workspace, askId, task, plugins, now(), roles);
    }

    /// <summary>The binding kept for <paramref name="run"/>, read; one that does not read holds the start, since what its take names is not known.</summary>
    private static WorkflowBind Kept(QuestView quest, IWorkflowRunStore runs, string run) =>
        runs.Read(run) is { } kept
            ? new(quest, WorkflowBindCame.Already, run, kept, null)
            : new(quest, WorkflowBindCame.Unread, run, null,
                "its run's binding could not be read here, so the workflow its quest is taken under is not known, and none is put in its place.");

    /// <summary>Whether the look says a binding: where a named workflow chose it, or it could not be read; Current is bound silently.</summary>
    private static bool Says(WorkflowRunBinding binding) => !binding.IsCurrent || binding.Problem is not null;

    /// <summary>The service's quests, read once for every bind of a look that needs a chain.</summary>
    private sealed class Every
    {
        public IReadOnlyList<QuestView>? Quests { get; set; }
    }
}
