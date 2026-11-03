using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The conversion's doors on this machine (ONEWAY1ae): the workflow part's world through the service and the home's files, a part whose source does
/// not answer said as unread; the staffing part over the home's staffing (ONEWAY1b); the plans this Daoris answered; and the register's floor for
/// Undo. The routes, the census and the come-up's settling read the same doors.
/// </summary>
internal static class ConversionDoors
{
    /// <param name="change">The one edit of <c>driver.json</c>, for a press; a reading writes none.</param>
    /// <param name="quests">The quests read at the press, for the Current units open now; null where the service did not answer, so every Current run counts open.</param>
    public static ConvertDoors Of(
        DriverLoop loop, Action<Func<DriverConfig, DriverConfig>>? change = null, IReadOnlyList<QuestView>? quests = null, DateTimeOffset? read = null) => new(
        loop.Home, ct => WorldAsync(loop, ct), change ?? (_ => throw new InvalidOperationException("a reading of the conversion writes no configuration")),
        () => DriverConfig.Load(loop.ConfigPath),
        () => ConvertWorlds.OpenCurrentNow(loop.Home, quests ?? [], quests is null ? DateTimeOffset.MinValue : read ?? DateTimeOffset.UtcNow))
    {
        Others = [new StaffingPart(RoleStaffing.Of(loop.ConfigPath) ?? new RoleStaffing(loop.Home))],
        FloorNeeded = TransitionalRegister.FloorNeeded,
        Answered = loop.Conversion.Answered,
    };

    /// <summary>The workflow part's world now; a service that is not up or does not answer is the part's source not answering.</summary>
    private static async Task<ConvertWorld> WorldAsync(DriverLoop loop, CancellationToken ct)
    {
        var service = loop.Service ?? throw new ConversionUnread("the service is not up yet");
        try
        {
            return await ConvertWorlds.ReadAsync(service.RegistryAsync, service.EveryQuestAsync, DriverConfig.Load(loop.ConfigPath), loop.Home, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException || (error is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new ConversionUnread("the service did not answer");
        }
    }
}
