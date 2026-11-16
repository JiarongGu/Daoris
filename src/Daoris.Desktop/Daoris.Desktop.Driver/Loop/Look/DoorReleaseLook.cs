using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// The supervisor's answers at the person's accept-step doors released after the landings: each held answer whose window passed, asked
/// again of the admission, its subject read again against the pin it judged, posted to the service's own door as the role's, named with
/// its agent, its decision and its turn's record; and each one a restart cut off after its release settled by what the service shows.
/// </summary>
[LookPass("door-release", LookPhase.Planned, "landing-release")]
internal sealed class DoorReleaseLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var moved = await wiring.Supervisor.ReleaseDoorsAsync(Release(wiring.Service), DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        if (moved.Contains(SupervisorState.WentOn))
        {
            round.Events.Add("supervisor posted a role's answer it held at a door the person's appointment gives it: memory kept or decided, or a departure accepted.");
        }

        if (moved.Contains(SupervisorState.Stale))
        {
            round.Events.Add("supervisor's held answer at a door was not posted: what it judged changed, its role did, or the door refused it; the person answers it.");
        }
    }

    /// <summary>The release's reads of <paramref name="service"/>: the pin read again, the post, and a cut-off post's outcome.</summary>
    internal static DoorRelease Release(ServiceClient service) => new(
        async (due, ct) =>
        {
            try
            {
                return await SupervisorDoorSubjects.PinAsync(service, due.Subject, ct).ConfigureAwait(false) == due.Pin ? null : SupervisorWhy.SubjectChanged;
            }
            catch (Exception error) when (error is HttpRequestException or IOException or DriverException)
            {
                return SupervisorWhy.NotReferred;
            }
        },
        (due, ct) => PostAsync(service, due, ct),
        (due, ct) => SupervisorDoorSubjects.MadeAsync(service, due, ct));

    /// <summary>
    /// The service's door for <paramref name="due"/>, as the role's: null where it kept the answer, else why not. A keep or a drop names the
    /// staged rows its pin holds, so the service moves exactly what the role judged, and refuses the post where one has moved since.
    /// </summary>
    internal static async Task<string?> PostAsync(ServiceClient service, SupervisorDoorDue due, CancellationToken ct)
    {
        var role = new RoleAnswerWire(due.Role, due.Agent, due.Id, due.Turn);
        var staged = due.Subject.Id == SupervisorDoorActs.Staged;
        try
        {
            var (ok, _) = due.Subject.Kind == SupervisorKinds.Departure
                ? await service.AcceptDepartureAsync(due.Subject.Id, ct, role).ConfigureAwait(false)
                : await service.DecideMemoryAsync(due.Subject.Session, staged ? null : due.Subject.Id, due.Yes, ct, role,
                    staged ? (due.Pin ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries) : null).ConfigureAwait(false);
            return ok ? null : SupervisorWhy.Answered;
        }
        catch (Exception error) when (error is HttpRequestException or IOException or DriverException)
        {
            return SupervisorWhy.NotReferred;
        }
    }
}
