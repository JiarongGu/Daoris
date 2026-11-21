namespace Daoris.Driver;

/// <summary>A step the pass judged vacant: its attempt, since when it is vacant, and whether it is announced and its refused stop said.</summary>
public sealed record StepVacancy(string Node, int Attempt, DateTimeOffset Since, bool Told, bool Refused);

/// <summary>Where the pass keeps each run's vacancies, and every reader reads them: <see cref="StepVacancyFiles"/> on the machine.</summary>
public interface IStepVacancyStore
{
    /// <summary>The vacancies kept for <paramref name="unit"/>'s run; none where none are kept or they do not read.</summary>
    IReadOnlyList<StepVacancy> Of(string unit);

    /// <summary><paramref name="vacancies"/> kept as the whole set of <paramref name="unit"/>'s run; an empty set keeps nothing.</summary>
    void Keep(string unit, IReadOnlyList<StepVacancy> vacancies);
}

/// <summary>
/// A vacancy: a run's step waiting on a role an agent holds, that no agent's session serves with a tool that answers it. Never hidden and
/// never the owner's: judged by the pass alone, which keeps it (<see cref="StepVacancyFiles"/>) from the moment it became vacant, so every
/// row, view and list reads the one judgement; listed under <i>Vacant</i>, announced once, and stopped by Daoris (<see cref="NodeSource.Daoris"/>)
/// once it has been announced and has stood vacant past its bound (<see cref="VacancySetting"/>), with what it tried. Served are a task's Done
/// or decision, handed to the run's working session, and a plugin's actor whose role a cast or a staffing gives an agent that a live session
/// of serves (<see cref="CastReading.Serves"/>). No dispatcher hands a role-classed Accept, approval or opinion dispute to a session yet, so one
/// an agent holds is a vacancy, though an Accept's or approval's agent may still answer it by <c>step_settle</c>.
/// </summary>
public static class StepVacancies
{
    public static bool Served(RoleWait wait, Func<string, bool> serves) =>
        wait.Why == PersonWaits.Task || (wait.Why == PersonWaits.Actor && serves(wait.Role));

    /// <summary>A part left and an attempt held are carried by rows of their own.</summary>
    public static bool Is(RoleWait wait, Func<string, bool> serves) =>
        wait.Holder.Kind == StepHolders.Agent && wait.Why is not (PersonWaits.Left or PersonWaits.Held) && !Served(wait, serves);

    /// <summary>The vacancy <paramref name="vacancies"/> keep for the step's attempt now, where its holder is still an agent.</summary>
    public static StepVacancy? Kept(IReadOnlyList<StepVacancy>? vacancies, RoleWait wait, string node, int attempt) =>
        wait.Holder.Kind == StepHolders.Agent ? vacancies?.FirstOrDefault(each => each.Node == node && each.Attempt == attempt) : null;

    public static string Said(RoleWait wait, TimeSpan bound) =>
        $"vacant: `{wait.Role}` is its agent's{Named(wait)}, and no agent session answers its {wait.Why}; listed under Vacant for anyone to take, Daoris gives "
        + $"it up {VacancySetting.Said(bound)} after it became vacant";

    /// <summary>What a run's view says of a step standing vacant: its role's agent holds it and no agent session answers it.</summary>
    public static string RunSaid(RoleWait wait) =>
        $"vacant: `{wait.Role}` is its agent's, and no agent session answers its {wait.Why}; it is listed under Vacant for anyone to take";

    /// <summary>The words Daoris's stop keeps on the step, which its run view says after <i>Daoris gave it up:</i>.</summary>
    public static string Tried(RoleWait wait, TimeSpan bound) =>
        $"nobody could answer it in {VacancySetting.Said(bound)}. Tried: the place's cast (no cast names a holder whose session answers its {wait.Why}), "
        + $"the role's agent{Named(wait)} (no session of it answers it), and listing it under Vacant, where nobody took it";

    /// <summary>
    /// Each run's vacancies judged now and kept: one first seen is kept from now and announced; one announced and past its bound is stopped as
    /// Daoris's own, so its run ends as gave up; a refused stop is said once. A step no longer vacant drops its vacancy, so its clock starts
    /// again if it becomes one. <paramref name="live"/> says whether a live session of a cast agent serves the place (<see cref="CastReading.Serves"/>);
    /// null reads the cast alone.
    /// </summary>
    internal static void Look(
        NodeRecordFiles records, IStepVacancyStore vacancies, IEnumerable<GovernedUnit> governed, DriverConfig? config, DateTimeOffset now, List<string> said,
        Func<string?, string?, bool>? live = null)
    {
        var bound = VacancySetting.Of(config);
        var casts = new PlaceCastFiles(records.Home).Book();
        var tenures = new StepTenureFiles(records.Home, records.HoldsLease);
        foreach (var each in governed)
        {
            if (each.Scope is null || records.Read(each.Unit.Id) is not { } record) continue;
            var kept = vacancies.Of(each.Unit.Id);
            var next = new List<StepVacancy>();
            var (workspace, repository) = (each.Unit.Owner.Workspace, each.Unit.Owner.Repository);
            var holding = tenures.Holding(each.Unit.Id, record.Statuses.ToDictionary(pair => pair.Key, pair => pair.Value.Attempt, StringComparer.Ordinal));
            var cast = CastReading.At(casts, config?.PlaceRisk, workspace, repository);
            var serves = CastReading.Serves(casts, config?.PlaceRisk, workspace, repository, live);
            foreach (var node in record.Ended is null ? each.Graph.Nodes : [])
            {
                var status = WorkPerformer.StatusOf(record, node.Id);
                if (StepWaiting.Read(record, node, status, holding.GetValueOrDefault(node.Id), cast) is not { } wait || record.Pending.Any(word => word.Node == node.Id)
                    || !Is(wait, serves))
                {
                    continue;
                }

                var vacancy = kept.FirstOrDefault(one => one.Node == node.Id && one.Attempt == status.Attempt) ?? new StepVacancy(node.Id, status.Attempt, now, false, false);
                var step = $"work  {each.Unit.Id}: step `{node.Id}` of #{each.Run}";
                if (!vacancy.Told)
                {
                    said.Add($"{step} is {Said(wait, bound)}.");
                    vacancy = vacancy with { Told = true };
                }
                else if (vacancy.Since + bound <= now)
                {
                    var words = Tried(wait, bound);
                    var stopped = RunNodeWords.Word(records, each.Run, node.Id, NodeWordActs.Stop, words, () => now, settle: SettleBy.Observation);
                    if (stopped.Done)
                    {
                        said.Add($"{step}: Daoris gave it up: {words}. Its run ends there.");
                        continue;
                    }

                    if (!vacancy.Refused) said.Add($"{step} is past its vacancy's bound, and Daoris's stop is refused ({stopped.Refused}).");
                    vacancy = vacancy with { Refused = true };
                }

                next.Add(vacancy);
            }

            if (!next.SequenceEqual(kept)) vacancies.Keep(each.Unit.Id, next);
        }
    }

    private static string Named(RoleWait wait) => wait.Holder.Name is { } name ? $" (`{name}`)" : "";
}
