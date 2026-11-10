namespace Daoris.Driver;

/// <summary>
/// The units a run whose work is still going is bound on: every subject a node of its record observes, while any node is ready, active
/// or unknown. A run's landing binds its branch unit only after the person's review, and a session branch whose trace is forgotten before
/// then leaves the landing ready with no subject for good; held here, the trace stays, and the landing finds the branch it lands. A run
/// whose steps have all settled holds nothing. Read-only and pure over the record.
/// </summary>
internal static class RunHeldUnits
{
    /// <summary>The units other than its own that <paramref name="record"/>'s live work is bound on; none once nothing in it waits.</summary>
    public static IEnumerable<string> Of(NodeRecord record) =>
        record.Statuses.Values.Any(status => status.State is NodeStates.Ready or NodeStates.Active or NodeStates.Unknown)
            ? record.Statuses.Values.SelectMany(status => new[] { status.Binding?.Observes, status.Acceptance?.Observes })
                .OfType<string>().Where(unit => !string.Equals(unit, record.Unit, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal)
            : [];
}
