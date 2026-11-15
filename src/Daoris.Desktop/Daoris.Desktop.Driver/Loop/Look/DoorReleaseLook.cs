using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// The supervisor's answers at the person's accept-step doors released after the landings: each held answer whose window passed, asked
/// again of the admission, posted to the service's own door as the role's, named with its agent and its decision.
/// </summary>
[LookPass("door-release", LookPhase.Planned, "landing-release")]
internal sealed class DoorReleaseLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var service = wiring.Service;
        if (await wiring.Supervisor.ReleaseDoorsAsync((due, token) => PostAsync(service, due, token), DateTimeOffset.UtcNow, ct).ConfigureAwait(false) is { Count: > 0 })
        {
            round.Events.Add("supervisor posted a role's answer it held at a door the person's appointment gives it: memory kept or decided, or a departure accepted.");
        }
    }

    /// <summary>The service's door for <paramref name="due"/>, as the role's: null where it kept the answer, else why not.</summary>
    internal static async Task<string?> PostAsync(ServiceClient service, SupervisorDoorDue due, CancellationToken ct)
    {
        var role = new RoleAnswerWire(due.Role, due.Agent, due.Id);
        try
        {
            var (ok, _) = due.Subject.Kind == SupervisorKinds.Departure
                ? await service.AcceptDepartureAsync(due.Subject.Id, ct, role).ConfigureAwait(false)
                : await service.DecideMemoryAsync(
                    due.Subject.Session, due.Subject.Id == SupervisorDoorActs.Staged ? null : due.Subject.Id, due.Yes, ct, role).ConfigureAwait(false);
            return ok ? null : SupervisorWhy.Answered;
        }
        catch (Exception error) when (error is HttpRequestException or IOException or DriverException)
        {
            return SupervisorWhy.NotReferred;
        }
    }
}
