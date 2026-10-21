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

/// <summary>A run bound to its workflow at its first start (WORKFLOW1e, D157 point 11; the workflow design §4.5).</summary>
/// <remarks>The loop's look calls it for the starts it opened (<c>Driver.BindRunsAsync</c>); STRUCT3b brought the use case here.</remarks>
public static class WorkflowRunStart
{
    /// <summary>
    /// Each run whose session opened in this look and that no binding names yet is bound to the workflow §4.1 chooses for it now
    /// (<see cref="WorkflowRunBindings"/>): its chain's first quest in the repository names it, its ask's latest choice is the task's,
    /// or, for a quest no ask published, its own (WORKFLOW1e2), and a named workflow's newest version is kept. Said in the look where a named workflow chose it, or where it could not be
    /// bound; a run under Current is bound silently, as nothing about its work changes. A failure binds nothing and costs the
    /// session nothing: the gate reads a run with no binding as it reads every run today.
    /// </summary>
    /// <param name="plugins">The plugins switched on and sound, as Current reads them (<see cref="WorkflowCurrent.PluginsOf"/>).</param>
    /// <param name="now">The moment each run is bound at, read as it is bound.</param>
    /// <param name="events">The look's lines, which a binding adds to.</param>
    public static Task BindAsync(
        IReadOnlyList<WorkflowStarted> opened, IWorkflowStartWorld world, string home, DriverConfig config,
        IReadOnlyList<WorkflowPlugin> plugins, Func<DateTimeOffset> now, List<string> events, CancellationToken ct) =>
        BindAsync(opened, world, WorkflowPorts.Store(home), WorkflowPorts.Runs(home), config, plugins, now, events, ct);

    /// <summary>Each run that opened in this look and is bound nowhere, bound through the ports.</summary>
    public static async Task BindAsync(
        IReadOnlyList<WorkflowStarted> opened, IWorkflowStartWorld world, IWorkflowStore store, IWorkflowRunStore runs, DriverConfig config,
        IReadOnlyList<WorkflowPlugin> plugins, Func<DateTimeOffset> now, List<string> events, CancellationToken ct)
    {
        IReadOnlyList<QuestView>? every = null;
        foreach (var start in opened)
        {
            var quest = start.Quest;
            try
            {
                // A chain's first quest has no parent, and names its run without the chain being read.
                var run = quest.Id;
                if (quest.Parent is { Length: > 0 })
                {
                    every ??= await world.EveryQuestAsync(ct).ConfigureAwait(false);
                    run = WorkflowRunBindings.RunOf(ReviewGate.ChainOf(every, quest.Id), quest.To) ?? quest.Id;
                }

                if (!WorkflowRunBindings.IsRunId(run) || runs.Bound(run)) continue;

                var askId = AskWords.AskOf(quest.From);
                var ask = askId is null ? null : await world.FindAskAsync(askId, ct).ConfigureAwait(false);
                // A quest no ask published carries its own choice, made on its page before it started (WORKFLOW1e2).
                var task = askId is null ? WorkflowRunBindings.TaskOf(quest, every) : WorkflowRunBindings.TaskOf(ask);
                var binding = WorkflowRunBindings.Plan(
                    config, store, run, quest.To, start.Workspace, askId, task, plugins, now());
                if (runs.Bind(binding))
                {
                    // The machine log keeps which level chose and whether a named version or Current (WORKFLOW1f, design §5.5).
                    world.Chosen(WorkflowLines.Chosen(binding));
                    if (!binding.IsCurrent || binding.Problem is not null) events.Add(WorkflowRunBindings.Said(binding, quest.Id));
                }
            }
            catch (Exception error) when (error is HttpRequestException or System.Text.Json.JsonException or DriverException or IOException
                                              or UnauthorizedAccessException || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                events.Add($"workflow  #{quest.Id} → {quest.To}: its run was not bound to a workflow at this start: {error.Message}");
            }
        }
    }
}
