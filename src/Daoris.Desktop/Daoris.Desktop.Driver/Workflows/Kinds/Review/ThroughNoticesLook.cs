namespace Daoris.Driver;

/// <summary>
/// The look's claim of the steps passed on the person's words (<see cref="ThroughNotices"/>) after everything it did, so a step the runtime
/// moved on them this look is in <i>Done and failed</i> by its end, and one a lost write left is claimed at the next.
/// </summary>
[LookPass("through-notices", LookPhase.After, "plugin-commands")]
internal sealed class ThroughNoticesLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var occurrences = ThroughNotices.Occurrences(wiring.Home, now);
        if (occurrences.Count == 0) return Task.CompletedTask;
        var claimed = new News(wiring.Home).Claim(ThroughNotices.Teller, occurrences, null, now);
        if (claimed.Told.Count > 0) round.Events.Add($"{claimed.Told.Count} step(s) passed on the person's words kept in Done and failed.");
        return Task.CompletedTask;
    }
}
