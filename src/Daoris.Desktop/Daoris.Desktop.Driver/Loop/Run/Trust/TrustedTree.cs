namespace Daoris.Driver;

/// <summary>
/// The tree the session runs in must be one its harness trusts, asked with the other holds before the spawn because the whole value is
/// not spending a login and minutes on a session that cannot close its quest. Held rather than failed: nothing is wrong with the quest,
/// and one command fixes it. An adapter name this machine does not know is its own error, said where it already was, so the check does
/// not run for one.
/// </summary>
[RunStage("trust", RunPhase.Cleared)]
internal sealed class TrustedTree(IRunWiring wiring) : IRunStage
{
    public ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct)
    {
        AgentDoor? preflight = null;
        try { preflight = wiring.Context.Adapters.Door(run.State.Agent); } catch (DriverException) { }
        run.State.Preflight = preflight;
        return ValueTask.FromResult(TrustAsk.Untrusted(wiring, run, run.State.Selection));
    }
}
