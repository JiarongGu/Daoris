namespace Daoris.Driver;

/// <summary>The roster's one ledger of role starts, made by the roles above it (STAFFPOLICY1c), shared by every start of the process.</summary>
public static class StaffRoster
{
    /// <summary>
    /// The ledger of <paramref name="home"/>'s role starts this process shares: made once beside <paramref name="roster"/>, whose
    /// <c>Release</c> gives a refused start's reservation back with its account count. <paramref name="settled"/> and <paramref name="clock"/> are a test's.
    /// </summary>
    public static StaffLedger StaffFor(this HarnessRoster roster, string home, Func<StoreSettled>? settled = null, TimeProvider? clock = null) =>
        (StaffLedger)roster.StartLedger(() => new StaffLedger(home, settled, clock));

    /// <summary>The ledger when a start has asked for it; null before.</summary>
    internal static StaffLedger? Staff(this HarnessRoster roster) => roster.StartLedgerMade as StaffLedger;
}
