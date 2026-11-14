namespace Daoris.Driver;

/// <summary>
/// The look's claim of the run outcomes (<see cref="RunOutcomeNotices"/>) after everything it did, so a step that failed this look is in
/// <i>Done and failed</i> by its end, and one a lost write left is claimed at the next.
/// The same pass clears a notice whose subject moved on.
/// </summary>
[LookPass("run-outcomes", LookPhase.After, "members")]
internal sealed class RunOutcomesLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var occurrences = RunOutcomeNotices.Occurrences(wiring.Home, now);
        if (occurrences.Count > 0)
        {
            var claimed = new News(wiring.Home).Claim(RunOutcomeNotices.Teller, occurrences, null, now);
            if (claimed.Told.Count > 0) round.Events.Add($"{claimed.Told.Count} run step outcome(s) kept in Done and failed.");
        }

        if (Notices.Read(wiring.Home, now) is { Count: > 0 } held)
        {
            string[] gone = [.. RunOutcomeNotices.Moved(wiring.Home, held), .. Notices.Spent(held)];
            if (gone.Length > 0 && Notices.Dismiss(wiring.Home, gone, now) is null) round.Events.Add($"{gone.Length} notice(s) whose subject moved on cleared from Done and failed.");
        }

        return Task.CompletedTask;
    }
}
