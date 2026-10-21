namespace Daoris.Driver;

/// <summary>A start the loop opened in a look, as binding its run reads it: the quest, and its repository's workspace.</summary>
public sealed record WorkflowStarted(QuestView Quest, string? Workspace);

/// <summary>
/// What binding a run at its first start reads and says beyond the home (WORKFLOW1e, the workflow design §4.5, §5.5): the service's
/// quests, which name a later quest's run, and its asks, whose latest choice is the task's; and the machine log's
/// <c>workflow.chosen</c> line. A port (STRUCT3b, D166): the loop hands it over its service client.
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

/// <summary>What a run's bind at a start came to (FABRIC-CUTOVER1g, D190's binding moment; the cutover design §8).</summary>
public enum WorkflowBindCame
{
    /// <summary>This start bound the run: the look says it, where a named workflow chose it or it could not be read.</summary>
    Bound,

    /// <summary>The run was bound before, by an earlier start or one beside this one; its binding is the one kept.</summary>
    Already,

    /// <summary>The quest's work names no run (its id names no file), so nothing is bound and nothing holds, as before.</summary>
    None,

    /// <summary>The run's binding is kept and does not read, so the workflow its take would name is not known: the start holds.</summary>
    Unread,

    /// <summary>The bind was not made (the service did not answer, its file could not be written): the start holds, and is tried again.</summary>
    Failed,
}

/// <summary>
/// A run's bind at a start (FABRIC-CUTOVER1g, the cutover design §8): the quest it was made for, the run, the binding kept, by this start or
/// before it, and why a run was not bound. The start's take reads <see cref="Binding"/> (<c>ClosureBinding.AtTakeAsync</c>).
/// </summary>
/// <param name="Quest">The quest whose start bound it.</param>
/// <param name="Came">What the bind came to.</param>
/// <param name="Run">The run its work is; null where its id names none.</param>
/// <param name="Binding">The run's binding as kept now; null where it is not bound, or its binding does not read.</param>
/// <param name="Why">Why its run is not bound, which the start's hold says; null where it is.</param>
public sealed record WorkflowBind(QuestView Quest, WorkflowBindCame Came, string? Run, WorkflowRunBinding? Binding, string? Why)
{
    /// <summary>Whether the start holds on it: its run is not bound, so no take may go on under a graph it is not bound to.</summary>
    public bool Holds => Came is WorkflowBindCame.Unread or WorkflowBindCame.Failed;
}

/// <summary>
/// A run bound to its workflow at its first start (WORKFLOW1e, D157 point 11 as D190 amends it; the workflow design §4.5, the cutover
/// design §8).
/// </summary>
/// <remarks>
/// <para><b>The binding moment</b> (FABRIC-CUTOVER1g): a start binds its run inside itself, once it is past the holds before its tree and
/// before its take names a closure graph (<see cref="AtStartAsync(WorkflowStarted, IWorkflowStartWorld, string, DriverConfig, IReadOnlyList{WorkflowPlugin}, Func{DateTimeOffset}, CancellationToken)"/>,
/// <c>Driver.RunAsync</c>), so the take names the graph its run is bound to. The binding stands though that start opens no session, and
/// the choice is read once, there: one changed after applies to the next run.</para>
/// <para><b>The look's bind</b> (<see cref="BindAsync(IReadOnlyList{WorkflowStarted}, IWorkflowStartWorld, string, DriverConfig, IReadOnlyList{WorkflowPlugin}, Func{DateTimeOffset}, List{string}, CancellationToken, LookSaid?, IReadOnlyList{WorkflowBind}?)"/>,
/// <c>Driver.BindRunsAsync</c>) says what the starts bound since the last look, and binds a run whose session opened and that is still
/// bound nowhere: one opened before this build, or by a start a test stands in for. STRUCT3b brought the use case here.</para>
/// </remarks>
public static class WorkflowRunStart
{
    /// <summary>
    /// The look's bind. First, each binding <paramref name="made"/> holds that a start made since the last look is said, once. Then each
    /// run whose session opened in this look and that no binding names yet is bound to the workflow §4.1 chooses for it now
    /// (<see cref="WorkflowRunBindings"/>): its chain's first quest in the repository names it, its ask's latest choice is the task's,
    /// or, for a quest no ask published, its own (WORKFLOW1e2), and a named workflow's newest version is kept. Said in the look where a named workflow chose it, or where it could not be
    /// bound; a run under Current is bound silently, as nothing about its work changes. A failure here binds nothing and costs the
    /// session nothing: the gate reads a run with no binding as it reads every run today.
    /// </summary>
    /// <param name="plugins">The plugins switched on and sound, as Current reads them (<see cref="WorkflowCurrent.PluginsOf"/>).</param>
    /// <param name="now">The moment each run is bound at, read as it is bound.</param>
    /// <param name="events">The look's lines, which a binding adds to.</param>
    /// <param name="said">Where each line is classed (NOTIFY2h): a binding's is <c>progress</c>, said on the run's own place, and a
    /// failure's <c>retry</c>.</param>
    /// <param name="made">What the starts' binds came to since the last look (FABRIC-CUTOVER1g); those this look says are those they bound.</param>
    public static Task BindAsync(
        IReadOnlyList<WorkflowStarted> opened, IWorkflowStartWorld world, string home, DriverConfig config,
        IReadOnlyList<WorkflowPlugin> plugins, Func<DateTimeOffset> now, List<string> events, CancellationToken ct, LookSaid? said = null,
        IReadOnlyList<WorkflowBind>? made = null) =>
        BindAsync(opened, world, WorkflowPorts.Store(home), WorkflowPorts.Runs(home), config, plugins, now, events, ct, said, made);

    /// <summary>The look's bind through the ports: what the starts bound, said; then each run that opened in this look and is bound nowhere, bound.</summary>
    public static async Task BindAsync(
        IReadOnlyList<WorkflowStarted> opened, IWorkflowStartWorld world, IWorkflowStore store, IWorkflowRunStore runs, DriverConfig config,
        IReadOnlyList<WorkflowPlugin> plugins, Func<DateTimeOffset> now, List<string> events, CancellationToken ct, LookSaid? said = null,
        IReadOnlyList<WorkflowBind>? made = null)
    {
        said ??= LookSaid.None;
        foreach (var bound in made ?? [])
        {
            if (bound is { Came: WorkflowBindCame.Bound, Binding: { } binding } && Says(binding))
            {
                events.Add(said.Progress("workflow", WorkflowRunBindings.Said(binding, bound.Quest.Id)));
            }
        }

        var every = new Every();
        foreach (var start in opened)
        {
            var quest = start.Quest;
            var bound = await BindOneAsync(start, world, store, runs, config, plugins, now, every, readKept: false, ct).ConfigureAwait(false);
            if (bound is { Came: WorkflowBindCame.Bound, Binding: { } binding } && Says(binding))
            {
                events.Add(said.Progress("workflow", WorkflowRunBindings.Said(binding, quest.Id)));
            }
            else if (bound is { Came: WorkflowBindCame.Failed, Why: { } failed })
            {
                events.Add(said.Retry("workflow", $"workflow  #{quest.Id} → {quest.To}: its run was not bound to a workflow at this start: {failed}"));
            }
        }
    }

    /// <summary>
    /// The start's bind (FABRIC-CUTOVER1g, D190's binding moment; the cutover design §8): its run bound to the workflow §4.1 chooses for it now
    /// where no binding names it yet, its binding moved into place without overwriting so two starts of one run at once keep the first's; or
    /// the binding already kept, read. A bind not made, or a binding that does not read, says why, and the start holds on it: a take that
    /// went on would name a graph its run may not be bound to. The machine log's line is written as a binding is made; the look says it.
    /// </summary>
    public static Task<WorkflowBind> AtStartAsync(
        WorkflowStarted start, IWorkflowStartWorld world, string home, DriverConfig config, IReadOnlyList<WorkflowPlugin> plugins,
        Func<DateTimeOffset> now, CancellationToken ct) =>
        AtStartAsync(start, world, WorkflowPorts.Store(home), WorkflowPorts.Runs(home), config, plugins, now, ct);

    /// <summary>The start's bind through the ports.</summary>
    public static Task<WorkflowBind> AtStartAsync(
        WorkflowStarted start, IWorkflowStartWorld world, IWorkflowStore store, IWorkflowRunStore runs, DriverConfig config,
        IReadOnlyList<WorkflowPlugin> plugins, Func<DateTimeOffset> now, CancellationToken ct) =>
        BindOneAsync(start, world, store, runs, config, plugins, now, new Every(), readKept: true, ct);

    /// <summary>What the start's hold says of a bind that was not made.</summary>
    private const string NotBound = "its run was not bound to a workflow, and its quest is taken only once it is";

    /// <summary>
    /// One run's bind: the run its quest's work is, bound where no binding names it; with <paramref name="readKept"/>, a binding already
    /// kept is read (the start's take reads it), else only seen (the look's).
    /// </summary>
    private static async Task<WorkflowBind> BindOneAsync(
        WorkflowStarted start, IWorkflowStartWorld world, IWorkflowStore store, IWorkflowRunStore runs, DriverConfig config,
        IReadOnlyList<WorkflowPlugin> plugins, Func<DateTimeOffset> now, Every every, bool readKept, CancellationToken ct)
    {
        var quest = start.Quest;
        string? run = null;
        try
        {
            // A chain's first quest has no parent, and names its run without the chain being read.
            run = quest.Id;
            if (quest.Parent is { Length: > 0 })
            {
                every.Quests ??= await world.EveryQuestAsync(ct).ConfigureAwait(false);
                run = WorkflowRunBindings.RunOf(ReviewGate.ChainOf(every.Quests, quest.Id), quest.To) ?? quest.Id;
            }

            if (!WorkflowRunBindings.IsRunId(run)) return new(quest, WorkflowBindCame.None, null, null, null);
            if (runs.Bound(run)) return readKept ? Kept(quest, runs, run) : new(quest, WorkflowBindCame.Already, run, null, null);

            var askId = AskWords.AskOf(quest.From);
            var ask = askId is null ? null : await world.FindAskAsync(askId, ct).ConfigureAwait(false);
            // A quest no ask published carries its own choice, made on its page before it started (WORKFLOW1e2).
            var task = askId is null ? WorkflowRunBindings.TaskOf(quest, every.Quests) : WorkflowRunBindings.TaskOf(ask);
            var binding = WorkflowRunBindings.Plan(config, store, run, quest.To, start.Workspace, askId, task, plugins, now());
            if (!runs.Bind(binding))
            {
                // Another start of the run bound it first, between the look above and this write (design §8, one first binding): its
                // binding is the run's, and this start takes under it.
                return readKept ? Kept(quest, runs, run) : new(quest, WorkflowBindCame.Already, run, null, null);
            }

            // The machine log keeps which level chose and whether a named version or Current (WORKFLOW1f, design §5.5).
            world.Chosen(WorkflowLines.Chosen(binding));
            return new(quest, WorkflowBindCame.Bound, run, binding, null);
        }
        catch (Exception error) when (error is HttpRequestException or System.Text.Json.JsonException or DriverException or IOException
                                          or UnauthorizedAccessException || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return new(quest, WorkflowBindCame.Failed, run, null, readKept ? $"{NotBound}: {error.Message}" : error.Message);
        }
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
