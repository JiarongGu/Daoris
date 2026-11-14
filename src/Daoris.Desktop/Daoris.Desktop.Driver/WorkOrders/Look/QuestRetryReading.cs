namespace Daoris.Driver;

/// <summary>
/// *Try again* read from the verdicts a loop's last look planned, and from the records themselves where that look says the quest holds
/// nothing: a stop made since the look is in the records and not yet in its plan, so a refusal from a look is never final until the
/// records are read again.
/// </summary>
public static class QuestRetryReading
{
    /// <param name="latest">The loop's last look's verdicts, or null where none has looked.</param>
    /// <param name="fresh">The planner over the records as they stand now; null where none can be read here.</param>
    /// <exception cref="ChoiceRefusal">The records say the quest is paused, or neither parked nor held.</exception>
    public static async Task<RetryDecision> DecideAsync(
        string quest, IReadOnlyList<Consideration>? latest, Func<CancellationToken, Task<IReadOnlyList<Consideration>>>? fresh, CancellationToken ct)
    {
        if (latest is null)
        {
            return QuestRetry.Decide(quest, await (fresh ?? throw new DriverException("the driver has not read its service yet.")).Invoke(ct).ConfigureAwait(false));
        }

        try
        {
            return QuestRetry.Decide(quest, latest);
        }
        catch (ChoiceRefusal refused) when (refused.Check == ChoiceRefusal.NotHeld && fresh is not null)
        {
            IReadOnlyList<Consideration> now;
            try
            {
                now = await fresh(ct).ConfigureAwait(false);
            }
            catch (Exception unread) when (unread is HttpRequestException or TaskCanceledException or DriverException or InvalidOperationException
                                               && !ct.IsCancellationRequested)
            {
                throw refused;
            }

            return QuestRetry.Decide(quest, now);
        }
    }
}
