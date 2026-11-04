namespace Daoris.Driver;

/// <summary>
/// What a steward's act may start, judged where the start's directory is actually chosen.
/// </summary>
[RunStage("steward", RunPhase.Placed)]
internal sealed class StewardPlace(IRunWiring wiring) : IRunStage
{
    public async ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct)
    {
        var (start, quest, context) = (run.Start, run.Quest, wiring.Context);

        // 🔴 A start a steward's act caused works only in a tree this home opened: a new part resumes its record's directory, and an
        // earlier session may have worked in the checkout. Otherwise it is held for the owner, never relocated, and the steward's
        // operation on it ends; the owner's own Try again or Drive makes it theirs.
        if (context.Roles is { } roles && roles.Caused(quest.To, quest.Id) && !context.Trees.Holds(run.State.WorkTree))
        {
            await run.Fresh.LetGoAsync().ConfigureAwait(false);
            roles.HeldForOwner(quest.Id);
            return run.Hold(Considerations.CheckoutHeld);
        }

        // The steward's Try again is consumed here, its final consumer, in one step of the run's memory after every await before it:
        // a revoke or the owner's act since cancelled it, and the start runs nothing of the steward's.
        if (start.Resumes is { StewardStopped: true } released && context.Config.RoleReleaseFor(quest.Id) is { Kind: RoleReleaseKinds.Stop } release
            && string.Equals(release.Session, released.Session, StringComparison.Ordinal) && !context.Config.Releases(quest.Id, released.Session)
            && context.Roles?.Take(quest.Id, release) != true)
        {
            await run.Fresh.LetGoAsync().ConfigureAwait(false);
            return run.Hold($"the steward's Try again of session `{released.Session}` was withdrawn before it started, so nothing of the steward's starts it.");
        }

        return null;
    }
}
