using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The risk policy a version move reads (<see cref="IAgentVersionRisk"/>), installed when the driver loads: <c>agent.version</c> judged at the
/// workspace's place or the machine's, a <c>driver.json</c> that does not read reading it at the owner, and the verdict journaled once, a pass as
/// the move's release and a gate where it would hold, never released there.
/// </summary>
internal sealed class AgentVersionRiskOperations : IAgentVersionRisk
{
    [ModuleInitializer]
    internal static void Install() => AgentVersionRisk.Current = new AgentVersionRiskOperations();

    public RiskVerdict Judged(string home, string? workspace, string actor, string subject, DateTimeOffset at)
    {
        var (verdict, place) = Judge(home, workspace, actor, subject);
        var passed = verdict.Outcome == RiskVerdict.Pass;
        RiskJournal.Append(home, new RiskJudged(
            at, passed ? RiskJudged.Released : RiskJudged.Opened, $"{AgentVersionAct.Domain}:{actor}/{subject}#{at.UtcTicks}", RiskActs.AgentVersion,
            verdict.Kind, verdict.Qualifier, actor, place.Workspace, place.Repository, place.Mode, null, null, subject, verdict.Outcome, verdict.Class,
            verdict.By, verdict.Source, verdict.Revision, verdict.Reason, passed ? AgentVersionAct.Passed : AgentVersionAct.Waits, !passed));
        return verdict;
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
