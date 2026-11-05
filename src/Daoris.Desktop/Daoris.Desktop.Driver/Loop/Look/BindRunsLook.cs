namespace Daoris.Driver;

/// <summary>
/// The look's say of what the starts bound (WORKFLOW1e, design §4.5, as FABRIC-CUTOVER1g moves it, D190, the cutover design §8): a start
/// binds its run itself, before its take, and the look says what the starts bound since the last look, a start held or refused after
/// its bind included. PARALLEL1b: what a start an earlier look let go of bound is said by the look after it.
/// </summary>
[LookPass("bind", LookPhase.Begun)]
internal sealed class BindRunsLook(ILookStagesWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct) =>
        wiring.Stages.TimeAsync("bind", () =>
        {
            WorkflowRunStart.Say([.. wiring.Runs.TakeBound().Cast<WorkflowBind>()], round.Events, wiring.Lines);
            return Task.CompletedTask;
        });
}
