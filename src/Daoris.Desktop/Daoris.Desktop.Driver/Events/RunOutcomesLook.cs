namespace Daoris.Driver;

/// <summary>
/// The look's claim of the run outcomes (<see cref="RunOutcomeNotices"/>) after everything it did, so a step it settled this look is in
/// <i>Done and failed</i> by its end, and one a lost write left is claimed at the next.
/// </summary>
[LookPass("run-outcomes", LookPhase.After, "members")]
internal sealed class RunOutcomesLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var occurrences = RunOutcomeNotices.Occurrences(wiring.Home, now);
        if (occurrences.Count == 0) return Task.CompletedTask;
        var claimed = new News(wiring.Home).Claim(RunOutcomeNotices.Teller, occurrences, null, now);
        if (claimed.Told.Count > 0) round.Events.Add($"{claimed.Told.Count} run step outcome(s) kept in Done and failed.");
        return Task.CompletedTask;
    }
}
