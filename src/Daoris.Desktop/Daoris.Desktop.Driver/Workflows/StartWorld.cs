namespace Daoris.Driver;

/// <summary>What binding a run reads and says through the loop's service: its quests, its asks and their plans, and the machine log's line.</summary>
internal sealed class StartWorld(ServiceClient service) : IWorkflowStartWorld, ITrainPlans
{
    public Task<IReadOnlyList<QuestView>> EveryQuestAsync(CancellationToken ct) => service.EveryQuestAsync(ct);

    public Task<AskView?> FindAskAsync(string id, CancellationToken ct) => service.FindAskAsync(id, ct);

    public ITrainPlans Plans => this;

    public async Task<TrainPlan?> OfAsync(string ask, CancellationToken ct) => TrainPlan.Of(await service.AskPlanAsync(ask, ct).ConfigureAwait(false));

    public void Chosen(LandingLine line) => service.LandingSaid(line);
}
