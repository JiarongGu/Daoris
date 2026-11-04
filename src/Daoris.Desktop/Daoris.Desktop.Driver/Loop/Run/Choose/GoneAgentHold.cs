namespace Daoris.Driver;

/// <summary>
/// The person's words waiting on a record whose agent this machine no longer has wait for the person, its conversation saying so with
/// the hand-over's offer, since only they can choose the agent it goes on with. Asked where the roster or the access setup refuses the
/// agent, before anything is spawned; any other start's refusal holds with its own sentence.
/// </summary>
internal static class GoneAgentHold
{
    /// <summary>The hold of a go-on whose words wait on an agent this machine no longer has; null for any other start.</summary>
    public static StartRun? Held(IRunWiring wiring, RunRound run)
    {
        var start = run.Start;
        var agent = run.State.Agent;
        if (!start.GoesOn || start.Resumes is not { WordsWaiting: true } waiting
            || wiring.Context.Adapters.Names.Contains(agent, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var gone = Continuations.AgentGone(agent, waiting.Session);
        wiring.GoOns.HeldSaying(waiting, gone);
        return run.Hold(gone.Note);
    }
}
