namespace Daoris.Driver;

/// <summary>
/// The agent supervisors' pass, after the courses' release: every subject an agent-held supervisor's hold covers, each attempted once
/// per choice of its role's order and one at a time per line; a turn whose subject no longer waits ended, its result dropped; an
/// attempt past its bound closed failed. A subject closed to the person, or whose attempts ended, is never attempted again.
/// </summary>
[LookPass("supervise", LookPhase.Planned, "landing-release")]
internal sealed class SuperviseLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        if (wiring.Supervising is not { } desk) return;
        var waiting = await desk.WaitingAsync(ct).ConfigureAwait(false);
        var supervisor = wiring.Supervisor;
        if (supervisor.Runs.StopUnwaited([.. waiting.Select(each => each.Subject)]) is > 0 and var stopped)
        {
            round.Events.Add($"supervisor: {stopped} turn(s) ended, the person having answered first.");
        }

        supervisor.Attempts.CloseOverdue(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(SupervisePass.Minutes + 1));
        foreach (var subject in waiting)
        {
            if (await wiring.Supervise(subject, desk, ct).ConfigureAwait(false) is { } said) round.Events.Add(said);
        }
    }
}
