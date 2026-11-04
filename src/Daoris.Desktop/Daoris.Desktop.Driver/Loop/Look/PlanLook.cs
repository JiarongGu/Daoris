namespace Daoris.Driver;

/// <summary>
/// The look's plan: the snapshot with the starts still opening held, planned under the cap the passes' slots left, and the starts it
/// chose, each with its line of work on an ask whose channel its instruction is handed what waited on.
/// </summary>
[LookPass("plan", LookPhase.Read, "meetings")]
internal sealed class PlanLook(ILookStagesWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        var state = round.State;
        var config = wiring.Config;
        var passes = state.Passes;
        var snapshot = state.Snapshot;
        IReadOnlyList<Consideration> plan;
        using (wiring.Stages.Begin("plan"))
        {
            snapshot = snapshot with { Starting = [.. state.ReservedBefore.Union(wiring.Runs.Reserved)] };
            // Whether this loop carries Daoris's browser decides whether a local set-up step can be shown here.
            plan = Planner.Plan(
                snapshot, passes > 0 ? config with { Cap = Math.Max(0, config.Cap - passes) } : config,
                StartDoor.Of(wiring.Home, config, wiring.Context.Adapters), window: wiring.Context.Browser is not null, roles: wiring.Context.Roles);
        }

        state.Snapshot = snapshot;
        state.Plan = plan;
        state.Starts = [.. plan.Where(c => c.Verdict == StartVerdict.Start).Select(c => c with { Channel = ChannelEntry.ForQuest(c.Quest, snapshot) })];
        return Task.CompletedTask;
    }
}
