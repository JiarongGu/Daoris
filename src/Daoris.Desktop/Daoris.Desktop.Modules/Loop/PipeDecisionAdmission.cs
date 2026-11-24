using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The supervisor's decisions' admission (<see cref="DriverLoop.Supervisor"/>): the served pipe's judge where there is one, read at
/// each ask, so a pipe served after the first look still answers; with no pipe, none is admitted. A subject is admitted where it waits on
/// the reporting line now (<see cref="SupervisorClimb"/>): the machine's supervisor once its place's left it.
/// </summary>
internal sealed class PipeDecisionAdmission(DriverLoop loop) : IDecisionAdmission
{
    public DecisionAdmitted Admit(string kind, RolePlace place, string? act) =>
        loop.Pipe?.StandingConfirmation is { } judge ? judge.Admit(kind, place, act) : NoDecisionAdmission.Instance.Admit(kind, place, act);

    public DecisionAdmitted Admit(SupervisorSubject subject, RolePlace place, string? act) =>
        SupervisorClimb.PlaceOf(loop, subject, place) is { } now ? Admit(subject.Kind, now, act) : DecisionAdmitted.Refused(SupervisorWhy.PassedUp);

    /// <summary>The judge's risk record at <paramref name="place"/>, its workspace placed; with no pipe, high.</summary>
    public PlaceRiskNow RiskAt(RolePlace place) =>
        loop.Pipe?.StandingConfirmation is { } judge ? judge.RiskAt(place) : new PlaceRiskNow(PlaceRisks.High, PlaceRiskBook.DefaultSource, []);

    /// <summary>The risk policy's verdict on a go-ahead at <paramref name="place"/>, its workspace placed by the judge; with no pipe, the owner's.</summary>
    public string? GoAheadOwners(RolePlace place, string kind, string on) =>
        loop.Pipe?.StandingConfirmation is { } judge && place.Repository is { } repository
            ? GoAheadAuthority.OwnersOwn(DriverConfig.Load(loop.ConfigPath), judge.Placed(repository), kind, on)
            : SupervisorWhy.PolicyOwner;
}
