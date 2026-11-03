using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The conversion's doors on this machine (ONEWAY1ae): the staffing part over the home's staffing (ONEWAY1b); the plans this Daoris answered; and
/// the register's floor for Undo. The routes, the census and the come-up's settling read the same doors.
/// </summary>
internal static class ConversionDoors
{
    /// <param name="change">The one edit of <c>driver.json</c>, for a press; a reading writes none.</param>
    public static ConvertDoors Of(DriverLoop loop, Action<Func<DriverConfig, DriverConfig>>? change = null) => new(
        loop.Home, change ?? (_ => throw new InvalidOperationException("a reading of the conversion writes no configuration")),
        () => DriverConfig.Load(loop.ConfigPath))
    {
        Others = [new StaffingPart(RoleStaffing.Of(loop.ConfigPath) ?? new RoleStaffing(loop.Home))],
        FloorNeeded = TransitionalRegister.FloorNeeded,
        Answered = loop.Conversion.Answered,
    };
}
