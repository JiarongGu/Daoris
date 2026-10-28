namespace Daoris.Driver;

/// <summary>
/// What the landing gate reads of the service (XAGENT1f): the review's quests and asks (<see cref="IReviewWorld"/>, the workplace's), and the
/// local host's opinions by id. A seam, so its tests stand in.
/// </summary>
public interface IOpinionWorld : IReviewWorld
{
    /// <summary>One second opinion as the local host answers it, or null where it holds none.</summary>
    Task<OpinionView?> OpinionAsync(string id, CancellationToken ct);
}

/// <summary>
/// The gate's world over this machine's service: the quests read once per instance, as a door reads them once, and the local
/// host's opinions (XAGENT1f), which the whole landing gate reads beside them.
/// </summary>
/// <remarks>
/// The service's reads behind the review's port (<see cref="IReviewWorld"/>) and the landing gate's (<see cref="IOpinionWorld"/>).
/// The loop and the hosts compose it, and another module names it as <c>new ServiceReviewWorld(service)</c>.
/// </remarks>
public sealed class ServiceReviewWorld(ServiceClient service) : IOpinionWorld
{
    private IReadOnlyList<QuestView>? _quests;

    public async Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) =>
        _quests ??= await service.EveryQuestAsync(ct).ConfigureAwait(false);

    public Task<AskView?> AskAsync(string id, CancellationToken ct) => service.FindAskAsync(id, ct);

    public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => service.Opinions.ReadAsync(id, ct);
}
