using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A vacancy: a run's step waiting on a role an agent holds, that no agent's session serves with a tool that answers it. Never hidden and
/// never the owner's: listed under <i>Vacant</i>, announced once, said once in the run's lines, and stopped by Daoris past its bound
/// (<see cref="VacancySetting"/>) with what it tried. Served are a task's Done or decision, handed to the run's working session, and a
/// plugin's actor whose role a cast or a staffing gives an agent (<see cref="CastReading.Serves"/>). No dispatcher hands a role-classed
/// Accept or approval to a session yet, so one an agent holds is a vacancy, though that agent may still answer it by <c>step_settle</c>.
/// </summary>
public static class StepVacancies
{
    private static readonly ConcurrentDictionary<string, byte> Told = new(StringComparer.Ordinal);

    public static bool Served(RoleWait wait, Func<string, bool>? serves) =>
        wait.Why == PersonWaits.Task || (wait.Why == PersonWaits.Actor && serves?.Invoke(wait.Role) == true);

    /// <summary>A part left and an attempt held are carried by rows of their own.</summary>
    public static bool Is(RoleWait wait, Func<string, bool>? serves) =>
        wait.Holder.Kind == StepHolders.Agent && wait.Why is not (PersonWaits.Left or PersonWaits.Held) && !Served(wait, serves);

    public static string Said(HeldStep step, TimeSpan bound) =>
        $"vacant: `{step.Role}` is its agent's{Named(step)}, and no agent session answers its {step.Why}; listed under Vacant for anyone to take, it gives up "
        + $"{VacancySetting.Said(bound)} after it began waiting";

    public static string Tried(HeldStep step, TimeSpan bound) =>
        $"gave up: `{step.Role}` stood vacant past {VacancySetting.Said(bound)}. Tried: the place's cast (no cast names a holder whose session answers "
        + $"its {step.Why}), the role's agent{Named(step)} (no session of it answers it), and listing it under Vacant, where nobody took it";

    /// <summary>Each vacancy first seen by this process said once; one past its bound stopped as an observation, so its run ends as gave up.</summary>
    internal static void Look(NodeRecordFiles records, IEnumerable<GovernedUnit> governed, DriverConfig? config, DateTimeOffset now, List<string> said)
    {
        var bound = VacancySetting.Of(config);
        var casts = new PlaceCastFiles(records.Home).Book();
        var tenures = new StepTenureFiles(records.Home, records.HoldsLease);
        foreach (var each in governed)
        {
            if (each.Scope is null || records.Read(each.Unit.Id) is not { Ended: null } record) continue;
            foreach (var step in HeldSteps.Of([each], _ => record, tenures, casts, config?.PlaceRisk, bound).Where(step => step.Vacant))
            {
                var key = $"{records.Home}|{step.Run}/{step.Node}#{WorkPerformer.StatusOf(record, step.Node).Attempt}";
                if (step.GivesUp is { } at && at <= now)
                {
                    var words = Tried(step, bound);
                    var stopped = RunNodeWords.Word(records, step.Run, step.Node, NodeWordActs.Stop, words, () => now, settle: SettleBy.Observation);
                    said.Add(stopped.Done
                        ? $"work  {each.Unit.Id}: step `{step.Node}` of #{step.Run} {words}; Daoris stopped it, and its run ends there."
                        : $"work  {each.Unit.Id}: step `{step.Node}` of #{step.Run} is past its vacancy's bound, and its stop is refused ({stopped.Refused}).");
                }
                else if (Told.TryAdd(key, 0))
                {
                    said.Add($"work  {each.Unit.Id}: step `{step.Node}` of #{step.Run} is {Said(step, bound)}.");
                }
            }
        }
    }

    private static string Named(HeldStep step) => step.HolderName is { } name ? $" (`{name}`)" : "";
}
