namespace Daoris.Driver;

/// <summary>
/// The pause that holds a quest with no pause of its own: one over a work the quest is part of (a quest its sessions' work published,
/// or one its ask asked), read through <see cref="PausedWork"/> as the look reads it, so a resume says the pause *Try again* names.
/// </summary>
public static class PauseHolder
{
    /// <summary>The pause holding <paramref name="quest"/>, or null where none does.</summary>
    /// <exception cref="HttpRequestException">The service did not answer the quests or the records.</exception>
    public static async Task<PausedBy?> OfAsync(ServiceClient service, DriverConfig config, string quest, CancellationToken ct = default)
    {
        if (config.PausedAsks.Count == 0 && config.PausedQuests.Count == 0) return null;
        var look = new AskWorkLook(
            await service.EveryQuestAsync(ct).ConfigureAwait(false), await service.SessionRecordsAsync(ct).ConfigureAwait(false));
        return PausedWork.Set(look, config).TryGetValue(quest, out var holder) ? holder : null;
    }
}
