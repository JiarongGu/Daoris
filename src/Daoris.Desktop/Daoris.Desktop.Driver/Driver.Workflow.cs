namespace Daoris.Driver;

/// <summary>A run bound to its workflow at its first start (WORKFLOW1e, D157 point 11; the workflow design §4.5).</summary>
public sealed partial class Driver
{
    /// <summary>
    /// Each run whose session opened in this look and that no binding names yet is bound to the workflow §4.1 chooses for it now
    /// (<see cref="WorkflowRunBindings"/>): its chain's first quest in the repository names it, its ask's latest choice is the task's,
    /// and a named workflow's newest version is kept. Said in the look where a named workflow chose it, or where it could not be
    /// bound; a run under Current is bound silently, as nothing about its work changes. A failure binds nothing and costs the
    /// session nothing: the gate reads a run with no binding as it reads every run today.
    /// </summary>
    private async Task BindRunsAsync(IReadOnlyList<Consideration> opened, List<string> events, CancellationToken ct)
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
                    every ??= await service.EveryQuestAsync(ct).ConfigureAwait(false);
                    run = WorkflowRunBindings.RunOf(ReviewGate.ChainOf(every, quest.Id), quest.To) ?? quest.Id;
                }

                if (!WorkflowRunBindings.IsRunId(run) || WorkflowRunBindings.Bound(home, run)) continue;

                var askId = AskWords.AskOf(quest.From);
                var ask = askId is null ? null : await service.FindAskAsync(askId, ct).ConfigureAwait(false);
                var binding = WorkflowRunBindings.Plan(
                    config, home, run, quest.To, start.Workspace, askId, WorkflowRunBindings.TaskOf(ask),
                    WorkflowCurrent.PluginsOf(_catalog), DateTimeOffset.UtcNow);
                if (WorkflowRunBindings.Bind(home, binding) && (!binding.IsCurrent || binding.Problem is not null))
                {
                    events.Add(WorkflowRunBindings.Said(binding, quest.Id));
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
