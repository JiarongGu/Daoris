using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The <c>ask-daoris</c> kind: Ask Daoris's acts in one conversation's range of turns, its subject <c>{ session, range }</c>, placed at
/// the machine alone. Its ranges wait in their reports (<see cref="AskDaorisReports"/>) for the reading queue, never for a holder's
/// listing, so the door lists none and keeps no report through a decide or a leave.
/// </summary>
internal sealed class AskDaorisKind : ISupervisorKind
{
    public string Kind => SupervisorKinds.AskDaoris;

    public Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, IReadOnlyList<PriorSession> records, IDecisionAdmission admission, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<object>>([]);

    public Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder, CancellationToken ct) =>
        Task.FromResult(SupervisorDecided.Refused(SupervisorWhy.NotHeld));

    public Task<SupervisorDecided> LeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct) =>
        Task.FromResult(SupervisorDecided.Refused(SupervisorWhy.NotHeld));

    public Task<RolePlace?> PlaceOfAsync(DriverLoop loop, SupervisorSubject subject, CancellationToken ct) => Task.FromResult<RolePlace?>(new RolePlace());
}
