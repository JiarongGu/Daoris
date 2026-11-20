using System.Collections.Concurrent;
using Daoris.Contracts.Sessions;

namespace Daoris.Driver;

/// <summary>
/// What one look's owed opinions read of the service: every quest, every session record and each ask, each read once, by the
/// first judgment that asks, and shared by the rest of that look's. An opinion is read where a judgment asks for it.
/// </summary>
/// <remarks>
/// The reads run on the look's token, so one judgment's cancellation fails none of the others; a read that fails fails every
/// judgment of that look, as a service that does not answer did, and the next look reads again.
/// </remarks>
internal sealed class OpinionLookWorld(ServiceClient service, CancellationToken look) : IOpinionWorld
{
    private readonly Lazy<Task<IReadOnlyList<QuestView>>> _quests = new(() => service.EveryQuestAsync(look));

    private readonly Lazy<Task<SessionResponse[]>> _records = new(() => service.SessionList.Kept.ListedAsync(look));

    public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => _quests.Value.WaitAsync(ct);

    /// <summary>Every session record, closed ones included, as the list kept by its change holds them.</summary>
    public Task<SessionResponse[]> RecordsAsync(CancellationToken ct) => _records.Value.WaitAsync(ct);

    // Several chains of one ask read it once a look, as the review's decision reads it.
    private readonly ConcurrentDictionary<string, Lazy<Task<AskView?>>> _asks = new(StringComparer.OrdinalIgnoreCase);

    public Task<AskView?> AskAsync(string id, CancellationToken ct) =>
        _asks.GetOrAdd(id, ask => new(() => service.FindAskAsync(ask, look))).Value.WaitAsync(ct);

    public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => service.Opinions.ReadAsync(id, ct);
}
