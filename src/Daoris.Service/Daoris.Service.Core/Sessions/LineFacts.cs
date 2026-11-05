namespace Daoris.Knowledge;

/// <summary>What a walk of a line reads: who continues a record, a quest's last run here, a record by its id, and a quest's chain.</summary>
public interface ILineFacts
{
    /// <summary>The newest local record continuing <paramref name="id"/> by <c>from</c>, or null.</summary>
    Task<Session?> ContinuedByAsync(string id, CancellationToken ct);

    /// <summary>The id of the newest local run on <paramref name="quest"/>, or null.</summary>
    Task<string?> LastOwnIdForQuestAsync(string quest, CancellationToken ct);

    /// <summary>The record with this id, or null.</summary>
    Task<Session?> FindAsync(string id, CancellationToken ct);

    /// <summary>The chain <paramref name="quest"/> is on, each step after its parent.</summary>
    Task<IReadOnlyList<Quest>> ChainAsync(string quest, CancellationToken ct);
}

/// <summary>The walk's facts as the stores hold them, each read when it is asked.</summary>
public sealed class StoreLineFacts(SessionStore sessions, QuestStore? quests) : ILineFacts
{
    public Task<Session?> ContinuedByAsync(string id, CancellationToken ct) => sessions.ContinuedByAsync(id, ct);

    public Task<string?> LastOwnIdForQuestAsync(string quest, CancellationToken ct) => sessions.LastOwnIdForQuestAsync(quest, ct);

    public Task<Session?> FindAsync(string id, CancellationToken ct) => sessions.FindAsync(id, ct);

    public Task<IReadOnlyList<Quest>> ChainAsync(string quest, CancellationToken ct) =>
        quests is null ? Task.FromResult<IReadOnlyList<Quest>>([]) : quests.ChainAsync(quest, ct);
}
