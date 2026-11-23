using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The risk policy a version move reads (<see cref="IAgentVersionRisk"/>), installed when the driver loads: <c>agent.version</c> judged at the
/// workspace's place or the machine's, a <c>driver.json</c> that does not read reading it at the owner, and each line journaled under the key
/// the move gives it, so a hold's open and its release are one hold.
/// </summary>
internal sealed class AgentVersionRiskOperations : IAgentVersionRisk
{
    [ModuleInitializer]
    internal static void Install() => AgentVersionRisk.Current = new AgentVersionRiskOperations();

    public RiskVerdict Verdict(string home, string? workspace, string actor, string subject) => Judge(home, workspace, actor, subject).Verdict;

    public void Journal(string home, string? workspace, RiskVerdict verdict, RiskLine line)
    {
        var place = Judge(home, workspace, line.Actor, line.Subject).Place;
        RiskJournal.Append(home, new RiskJudged(
            line.At, line.Release ? RiskJudged.Released : RiskJudged.Opened, line.Key, RiskActs.AgentVersion, verdict.Kind, verdict.Qualifier, line.Actor,
            place.Workspace, place.Repository, place.Mode, null, null, line.Subject, verdict.Outcome, verdict.Class, verdict.By, verdict.Source,
            verdict.Revision, verdict.Reason, line.Today, line.Held));
    }

    private static (RiskVerdict Verdict, RiskPlace Place) Judge(string home, string? workspace, string actor, string subject)
    {
        var act = new RiskAct(RiskActs.AgentVersion, null);
        DriverConfig config;
        try
        {
            config = DriverConfig.Load(DriverConfig.ResolvePath(home));
        }
        catch (DriverException unread)
        {
            var place = new RiskPlace(workspace, null, Full: false);
            var policy = new RiskPolicy(new Dictionary<string, IReadOnlyList<RiskRow>>(), PlaceRiskBook.Unwritten, RiskPolicy.SeedRevision) { Fault = unread.Message };
            return (RiskGate.Judge(policy, act, actor, place, subject), place);
        }

        var at = RiskPlace.Of(config, workspace, null);
        return (RiskGate.Judge(RiskPolicy.Of(config), act, actor, at, subject), at);
    }
}
