using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The supervisor's decisions' admission (<see cref="DriverLoop.Supervisor"/>): the served pipe's judge where there is one, read at
/// each ask, so a pipe served after the first look still answers; with no pipe, none is admitted.
/// </summary>
internal sealed class PipeDecisionAdmission(DriverLoop loop) : IDecisionAdmission
{
    public DecisionAdmitted Admit(string repository) =>
        loop.Pipe?.StandingConfirmation is { } judge ? judge.Admit(repository) : NoDecisionAdmission.Instance.Admit(repository);
}
