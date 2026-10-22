using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The measurements table's stand-in rows (COAGENT1b; the co-agent design §8.1), for the tests' stand-in agents alone: a row marked
/// <see cref="PartnerMeasurement.StandIn"/> that holds exactly the effective configuration a start on <paramref name="agent"/> would hand,
/// as the driver makes it. Handed in through <c>LoopContext.PartnerMeasured</c>, it admits a test's start and never a production one.
/// </summary>
internal static class PartnerStandIns
{
    /// <summary>The evidence a stand-in's row names: the design's section on the stand-ins, since no run on a machine measured them.</summary>
    public const string Evidence = "docs/2026-10-22-coagent-design.md";

    /// <summary>A stand-in's row for <paramref name="agent"/> at <paramref name="version"/>, for a lead in <paramref name="repository"/>.</summary>
    public static PartnerMeasurement Row(
        Daoris.Driver.Driver driver, string agent, string version, string? workspace, string repository)
    {
        var adapter = AdapterSet.Built().Resolve(agent);
        var (rules, hardDeny, guarded) = driver.PartnerHanded(workspace, repository);
        var configuration = PartnerConfigurations.Of(adapter, version, "<copy>", rules, hardDeny, guarded, DriverPartners.ThisSystem());
        return new PartnerMeasurement(
            configuration.Agent, version, null, configuration.Posture ?? "", configuration.WritableRoots, configuration.Network,
            configuration.System, configuration.SettingsDigest, PartnerMeasurements.Confinement, PartnerMeasurements.Held, Evidence)
        {
            StandIn = true,
        };
    }
}
