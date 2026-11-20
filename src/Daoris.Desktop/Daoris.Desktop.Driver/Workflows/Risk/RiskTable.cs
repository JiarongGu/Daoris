namespace Daoris.Driver;

/// <summary>
/// The resolved act table at a place as <c>daoris settings risk</c> prints it: each kind of the census, its class there, the role that
/// answers a <c>role</c>, and where its rule comes from where it is not the seed. Read only: no verb sets a row yet.
/// </summary>
public static class RiskTable
{
    /// <summary>The table at the place <paramref name="workspace"/> and <paramref name="repository"/> name (both none: the machine), from <paramref name="config"/>.</summary>
    public static IReadOnlyList<string> Of(DriverConfig config, string? workspace, string? repository) =>
        Lines(RiskPolicy.Of(config), RiskPlace.Of(config, workspace, repository));

    public static IReadOnlyList<string> Lines(RiskPolicy policy, RiskPlace place)
    {
        var lines = new List<string>
        {
            policy.Fault is { } fault
                ? $"  The risk policy does not read ({fault}): every act reads `owner` until it is mended."
                : $"  Each act here under {place.Mode} access, by the risk policy (revision {policy.Revision}):",
        };
        foreach (var verdict in RiskGate.Table(policy, place))
        {
            var by = verdict.By is { } role ? $", by `{role}`" : "";
            var from = verdict.Source switch
            {
                RiskVerdict.Seed => verdict.Kind == RiskActs.CheckNarrow ? ", said in one line" : "",
                RiskVerdict.Fixed => ", fixed",
                RiskVerdict.Unreadable => "",
                var key => $", set at `{key}`",
            };
            lines.Add($"    {verdict.Kind.PadRight(17)}{verdict.Class}{by}{from}");
        }

        if (policy.Unknown.Count > 0) lines.Add($"  Rows naming no act this build knows, not read: {string.Join(", ", policy.Unknown)}.");
        return lines;
    }
}
