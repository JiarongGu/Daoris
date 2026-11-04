namespace Daoris.Driver;

/// <summary>
/// What sessions proposed about the rules (PERM2, D74), settled before the look spawns anything: a narrowing is applied at once, so
/// this look's sessions are already handed it, and a widening is held for the person, who alone may let an agent do more.
/// </summary>
[LookPass("rules", LookPhase.Local, "plugins")]
internal sealed class RuleSettling(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        try
        {
            // Each line classed where the proposals settle it (NOTIFY2h2).
            using (wiring.Stages.Begin("rules")) round.Events.AddRange(RuleProposals.Settle(wiring.Home, DateTimeOffset.UtcNow, wiring.Lines));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            round.Events.Add(wiring.Lines.Retry("rules", $"rules  the proposals could not be settled this tick: {error.Message}"));
        }

        return Task.CompletedTask;
    }
}
