using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The reporting line above a landing (<see cref="ReportingLine"/>), climbed for its <c>accept</c> with the place's risk as <c>driver.json</c>
/// holds it now. A role record that cannot be read holds the place's level for the person, as the line reads one.
/// </summary>
internal sealed class LandingWaitLineOperations : ILandingWaitLine
{
    [ModuleInitializer]
    internal static void Install() => LandingWaitLines.Current = new LandingWaitLineOperations();

    public string Climb(string home, string repository, string? workspace) =>
        Line(home, repository, workspace, DriverConfig.Load(DriverConfig.ResolvePath(home)).PlaceRisk).Said();

    private static LineWait Line(string home, string repository, string? workspace, PlaceRiskBook risk)
    {
        try
        {
            return ReportingLine.Of(RoleCatalog.ReadHome(home).Slots, SupervisorKinds.Landing, new RolePlace(workspace, repository),
                new RuleWiderPlaceStore(home).Read(), "accept", risk).Climb(ownerClass: false);
        }
        catch (DriverException error)
        {
            return new(new LineLevel(LineLevels.Repository, null, LineHolders.Person, error.Message), []);
        }
    }
}
