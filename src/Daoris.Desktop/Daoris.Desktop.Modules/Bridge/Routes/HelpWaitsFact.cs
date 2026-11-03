using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The judge of Ask Daoris's fact of what each line waits on: the one read <i>At work</i> draws, over the home's helper, piece and
/// hand-over files and the service's records and quests.
/// </summary>
internal static class HelpWaitsFact
{
    /// <summary>What each line waits on; null where no service answers or it could not be read, which the room says not read.</summary>
    public static async Task<HelpWaits?> JudgeAsync(ServiceClient? service, string home, CancellationToken ct)
    {
        if (service is null) return null;
        try
        {
            var records = await service.SessionRecordsAsync(ct).ConfigureAwait(false);
            var quests = await service.EveryQuestAsync(ct).ConfigureAwait(false);
            return AtWorkExchangeReading.Waits(home, records, quests);
        }
        catch (DriverException)
        {
            return null;
        }
    }
}
