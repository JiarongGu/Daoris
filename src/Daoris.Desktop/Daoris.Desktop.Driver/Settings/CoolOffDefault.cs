namespace Daoris.Driver;

/// <summary>
/// The cool-off a limit naming no time takes where <c>driver.json</c>'s <c>cooloff</c> sets none (TOOL4e, D125 §2.2): the settings' own
/// default for the key, which the account limits' <see cref="AccountLimits.DefaultCoolOff"/> answers through, so the person's
/// setting and the limit's reading share one hour.
/// </summary>
public static class CoolOffDefault
{
    /// <summary>
    /// Long enough that a spent account is not started on at every look, short enough that a wrong guess costs an hour (D125 §2.2).
    /// </summary>
    public static readonly TimeSpan Value = TimeSpan.FromMinutes(60);
}
