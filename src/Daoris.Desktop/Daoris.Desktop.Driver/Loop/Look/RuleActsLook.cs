namespace Daoris.Driver;

/// <summary>
/// The rules' acts the plan made, said on their lines once (the context counters design §4.7): a quest parked by its failed sessions, held
/// on a transient episode or by the bound of moves, and a line a rule moved to another choice. Each is kept through
/// <see cref="SupervisorRuleActs"/> the first look that reads it, and not again for the same session, rule and point.
/// </summary>
[LookPass("rule-acts", LookPhase.Planned, "start")]
internal sealed class RuleActsLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        var staff = wiring.Harnesses.StaffFor(wiring.Home);
        var snapshot = round.State.Snapshot;
        IReadOnlyList<RuleActed> planned;
        try
        {
            planned = PlannedRuleActs.Of(
                snapshot, round.State.Plan, staff.Rules(), (quest, last) => StaffCourse.RoleOf(staff, snapshot, quest, last), wiring.Harnesses.Clock());
        }
        catch (Exception error) when (error is DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            return Task.CompletedTask;
        }

        var acts = new SupervisorRuleActs(wiring.Home, wiring.Events);
        foreach (var act in planned)
        {
            if (acts.Of(act.Session).Any(kept => kept.Rule.Count == act.Rule.Count && kept.Rule.Do == act.Rule.Do && kept.Point == act.Point)) continue;
            acts.Record(act);
        }

        return Task.CompletedTask;
    }
}
