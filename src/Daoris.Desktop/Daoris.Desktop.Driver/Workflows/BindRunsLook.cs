namespace Daoris.Driver;

/// <summary>
/// The look's bind (WORKFLOW1e, design §4.5, as FABRIC-CUTOVER1g moves it, D190, the cutover design §8): a start binds its run itself,
/// before its take, and the look says what the starts bound since the last look, a start held or refused after its bind included. A
/// run whose session opened in this look and that no binding names yet (one opened before that build, or a start a test stands in
/// for) is bound here. PARALLEL1b: what a start an earlier look let go of bound is said by the look after it.
/// </summary>
[LookPass("bind", LookPhase.Begun)]
internal sealed class BindRunsLook(ILookStagesWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct) =>
        wiring.Stages.TimeAsync("bind", () => WorkflowRunStart.BindAsync(
            [.. round.State.OpenedStarts.Select(start => new WorkflowStarted(start.Quest, start.Workspace))], new StartWorld(wiring.Service),
            wiring.Home, wiring.Config, WorkflowSteps.PluginsOf(wiring.Context.Catalog), () => DateTimeOffset.UtcNow, round.Events, ct, wiring.Lines,
            made: [.. wiring.Runs.TakeBound().Cast<WorkflowBind>()]));
}
