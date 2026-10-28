namespace Daoris.Driver;

/// <summary>
/// What the landing at done reads of the service (LAND2b): a seam, so its tests stand in for the ledger. Its quests and asks are
/// what the review's gate reads too (REVIEWENV1c), and the opinions the second opinion's part reads (XAGENT1f), so a look lands
/// nothing the gate holds.
/// </summary>
/// <remarks>
/// The workplace's <see cref="IWorkplaceLandingWorld"/> is what the look reads of the quests and the sessions; the opinions are the
/// review's (<see cref="IOpinionWorld"/>), and the whole gate the look reads is read over this one world.
/// </remarks>
public interface IAutoLandingWorld : IWorkplaceLandingWorld, IOpinionWorld
{
    /// <inheritdoc cref="IWorkplaceLandingWorld.Again"/>
    new IAutoLandingWorld Again() => this;

    IWorkplaceLandingWorld IWorkplaceLandingWorld.Again() => Again();
}

/// <summary>The world over this machine's service: the quests read once per instance, since a look builds one.</summary>
public sealed class ServiceAutoLandingWorld(ServiceClient service) : IAutoLandingWorld
{
    private IReadOnlyList<QuestView>? _every;
    private IReadOnlyDictionary<string, QuestView>? _quests;

    public async Task<QuestView?> QuestAsync(string id, CancellationToken ct)
    {
        _quests ??= (await QuestsAsync(ct).ConfigureAwait(false))
            .GroupBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        return _quests.GetValueOrDefault(id.TrimStart('#'));
    }

    public async Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) =>
        _every ??= await service.EveryQuestAsync(ct).ConfigureAwait(false);

    public Task<AskView?> AskAsync(string id, CancellationToken ct) => service.FindAskAsync(id, ct);

    public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => service.Opinions.ReadAsync(id, ct);

    public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => service.SessionRecordsAsync(ct);

    public async Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) =>
        (await service.ActiveSessionsAsync(ct).ConfigureAwait(false))
        .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>A new world over the same service, so nothing this one read is kept.</summary>
    public IAutoLandingWorld Again() => new ServiceAutoLandingWorld(service);

    public async Task<SayAnswer?> SayAsync(string session, string text, string key, CancellationToken ct) =>
        await service.SayAsync(session, text, null, ct, from: SaidFrom.Daoris, key: key).ConfigureAwait(false);
}
