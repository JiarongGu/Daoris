namespace Daoris.Driver;

/// <summary>A run bound to its workflow at its first start (WORKFLOW1e, D157 point 11; the workflow design §4.5).</summary>
public sealed partial class Driver
{
    /// <summary>
    /// Each run whose session opened in this look and that no binding names yet is bound to the workflow §4.1 chooses for it now:
    /// Workflows' use case (<see cref="WorkflowRunStart"/>), composed here over the loop's service, home, choices and plugins, and
    /// the clock.
    /// </summary>
    private Task BindRunsAsync(IReadOnlyList<Consideration> opened, List<string> events, CancellationToken ct) =>
        WorkflowRunStart.BindAsync(
            [.. opened.Select(start => new WorkflowStarted(start.Quest, start.Workspace))], new StartWorld(service), home, config,
            WorkflowCurrent.PluginsOf(_catalog), () => DateTimeOffset.UtcNow, events, ct);

    /// <summary>What binding a run reads and says through the loop's service: its quests, its asks, and the machine log's line.</summary>
    private sealed class StartWorld(ServiceClient service) : IWorkflowStartWorld
    {
        public Task<IReadOnlyList<QuestView>> EveryQuestAsync(CancellationToken ct) => service.EveryQuestAsync(ct);

        public Task<AskView?> FindAskAsync(string id, CancellationToken ct) => service.FindAskAsync(id, ct);

        public void Chosen(LandingLine line) => service.LandingSaid(line);
    }
}
