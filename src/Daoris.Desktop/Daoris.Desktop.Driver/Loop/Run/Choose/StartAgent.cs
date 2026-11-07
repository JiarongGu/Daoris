namespace Daoris.Driver;

/// <summary>
/// The agent a start runs on, and the roles it answers to: the one a hand-over named, held until the quest leaves this machine; else a
/// continuation's record's own, the agent its asked choice is asked of, never the machine's adapter key, so a part that ran on a
/// role's second choice goes on there; else the work's, which the walk may replace. Starts run side by side, so it is the run's alone.
/// </summary>
[RunStage("agent", RunPhase.Choose)]
internal sealed class StartAgent(IRunWiring wiring) : IRunStage
{
    public async ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct)
    {
        var (start, quest, state, context) = (run.Start, run.Quest, run.State, wiring.Context);
        var handedQuest = new HandedQuests(context.Home).Read(quest.Id);
        state.HandedQuest = handedQuest;

        // The role the run's work step names, which every walk of this start takes in place of the work's, never another.
        var stepRole = await wiring.StepRoleAsync(quest, start.Workspace, ct).ConfigureAwait(false);
        state.StepRole = stepRole;

        // Its step's role, else the role its line is bound to, else the work's at its place: never the seeded Build's by default.
        state.PolicyRole = stepRole ?? RuledLine.OfQuest(context.Home, quest, start.Resumes, start.Workspace).Role;
        state.Agent = StartPlan.AgentFor(
            Driver.StartWork(quest), context.Config, handed: handedQuest?.Agent, catalogue: wiring.CatalogueForReport(),
            place: Driver.PlaceOf(start), role: stepRole);
        if (handedQuest is null && start.Resumes is { } goesOnFrom)
        {
            try
            {
                state.Agent = StartPlan.ContinuedOn(
                    goesOnFrom, context.Config, Driver.StartWork(quest), context.Conversations.RoleOf(goesOnFrom.Session)?.Role,
                    wiring.CatalogueForReport(), Driver.PlaceOf(start));
            }
            catch (DriverException error)
            {
                return run.Hold(error.Message);
            }
        }

        // The session the quest is handed over from, whose instruction and note this start carries on from.
        state.HandedFrom = handedQuest is not null && start.Resumes is { } earlier
                           && string.Equals(earlier.Session, handedQuest.From, StringComparison.Ordinal);
        return null;
    }
}
