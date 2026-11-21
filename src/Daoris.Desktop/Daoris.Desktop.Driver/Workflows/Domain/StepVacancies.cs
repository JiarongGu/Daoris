namespace Daoris.Driver;

/// <summary>A step the pass judged vacant: its attempt, since when it is vacant, whether it is announced, and whether it reached the owner's final exit.</summary>
public sealed record StepVacancy(string Node, int Attempt, DateTimeOffset Since, bool Told, bool Exited);

/// <summary>Where the pass keeps each run's vacancies, and every reader reads them: <see cref="StepVacancyFiles"/> on the machine.</summary>
public interface IStepVacancyStore
{
    /// <summary>The vacancies kept for <paramref name="unit"/>'s run; none where none are kept or they do not read.</summary>
    IReadOnlyList<StepVacancy> Of(string unit);

    /// <summary><paramref name="vacancies"/> kept as the whole set of <paramref name="unit"/>'s run; an empty set keeps nothing.</summary>
    void Keep(string unit, IReadOnlyList<StepVacancy> vacancies);

    IReadOnlyList<string> Units();
}

/// <summary>
/// A vacancy: a run's step waiting on a role an agent holds, that no agent's session serves with a tool that answers it. Never hidden and
/// never the owner's: judged by the pass alone, which keeps it (<see cref="StepVacancyFiles"/>) from the moment it became vacant, so every
/// row, view and list reads the one judgement; listed under <i>Vacant</i>, announced once, and past its bound (<see cref="VacancySetting"/>) at
/// the owner's final exit (<see cref="FinalExits"/>), the person's row from then on, never stopped by Daoris. Served are a task's Done
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

    /// <summary>The vacancy kept for the step's attempt, judged again as it is read: its holder an agent and, where <paramref name="serves"/> is given, nothing serving it now.</summary>
    public static StepVacancy? Kept(IReadOnlyList<StepVacancy>? vacancies, RoleWait wait, string node, int attempt, Func<string, bool>? serves = null) =>
        wait.Holder.Kind == StepHolders.Agent && (serves is null || Is(wait, serves)) ? vacancies?.FirstOrDefault(each => each.Node == node && each.Attempt == attempt) : null;

    /// <summary>Whether a role's wait is served, for a reader; one that knows no live session passes none, and only a staffing serves, so the pass alone ends a cast agent's vacancy.</summary>
    public static Func<string, bool> ServesAt(PlaceCastBook book, PlaceRiskBook? risk, string? workspace, string? repository, Func<string?, string?, bool>? live = null) =>
        CastReading.Serves(book, risk, workspace, repository, live ?? ((_, _) => false));

    public static string Said(RoleWait wait, TimeSpan bound) =>
        $"vacant: `{wait.Role}` is its agent's{Named(wait)}, and no agent session answers its {wait.Why}; listed under Vacant for anyone to take, it goes "
        + $"to the owner as Daoris's final exit {VacancySetting.Said(bound)} after it became vacant";

    /// <summary>What a run's view says of a step standing vacant: its role's agent holds it and no agent session answers it.</summary>
    public static string RunSaid(RoleWait wait) =>
        $"vacant: `{wait.Role}` is its agent's, and no agent session answers its {wait.Why}; it is listed under Vacant for anyone to take";

    public static string Notice(string step, RoleWait wait, DateTimeOffset since, DateTimeOffset now) =>
        FinalExits.Notice($"{step} stood vacant", [
            new("the place's cast", null, $"no cast names a holder whose session answers its {wait.Why}"),
            new($"the role `{wait.Role}`'s agent{Named(wait)}", since, "no session of it answers it"),
            new("the Vacant list", since, "nobody took it"),
        ], FinalExits.Answer, now);

    /// <summary>
    /// Each run's vacancies judged now and kept: one first seen is kept from now and announced; one announced and past its bound gives its
    /// one notice and is kept as exited, never said again. A step no longer vacant drops its vacancy, so its clock starts again. <paramref name="live"/>
    /// says whether a live session of a cast agent serves the place (<see cref="CastReading.Serves"/>); null reads the cast alone.
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
                else if (!vacancy.Exited && vacancy.Since + bound <= now)
                {
                    said.Add(Notice(step, wait, vacancy.Since, now));
                    vacancy = vacancy with { Exited = true };
                }

                next.Add(vacancy);
            }

            if (!next.SequenceEqual(kept)) Keep(vacancies, each.Unit.Id, next, said);
        }

        Sweep(records, vacancies, governed, said);
    }

    /// <summary>A file that will not write never ends the pass: it is said, and the vacancies are judged again at the next look.</summary>
    private static void Keep(IStepVacancyStore vacancies, string unit, IReadOnlyList<StepVacancy> next, List<string> said)
    {
        try
        {
            vacancies.Keep(unit, next);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            said.Add($"work  {unit}: its vacancies could not be kept ({error.Message}), so Daoris says them again at its next look.");
        }
    }

    /// <summary>Drops the vacancies kept for a run no pass governs whose record is gone; a run that stands keeps its own.</summary>
    private static void Sweep(NodeRecordFiles records, IStepVacancyStore vacancies, IEnumerable<GovernedUnit> governed, List<string> said)
    {
        var standing = governed.Select(each => each.Unit.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var unit in vacancies.Units().Where(unit => !standing.Contains(unit) && !records.Exists(unit))) Keep(vacancies, unit, [], said);
    }

    private static string Named(RoleWait wait) => wait.Holder.Name is { } name ? $" (`{name}`)" : "";
}
