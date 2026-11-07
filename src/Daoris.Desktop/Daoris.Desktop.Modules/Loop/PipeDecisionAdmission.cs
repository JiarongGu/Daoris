using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The supervisor's decisions' admission (<see cref="DriverLoop.Supervisor"/>): the served pipe's judge where there is one, read at
/// each ask, so a pipe served after the first look still answers; with no pipe, none is admitted.
/// </summary>
internal sealed class PipeDecisionAdmission(DriverLoop loop) : IDecisionAdmission
{
    public DecisionAdmitted Admit(string kind, RolePlace place, string? act) =>
        loop.Pipe?.StandingConfirmation is { } judge ? judge.Admit(kind, place, act) : NoDecisionAdmission.Instance.Admit(kind, place, act);
}
