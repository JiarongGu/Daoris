using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A step the pass judged vacant: its attempt, since when it is vacant, whether it is announced, and whether it reached the owner's final exit.
/// <see cref="Served"/> marks a wait a live session serves: never listed as vacant, its bound runs from <see cref="Since"/> all the same.
/// </summary>
public sealed record StepVacancy(string Node, int Attempt, DateTimeOffset Since, bool Told, bool Exited)
{
    public bool Served { get; init; }
}

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
/// or decision, handed to the run's working session (bounded all the same: <see cref="Bounded"/>), and a plugin's actor or a spent red's role (<see cref="SpentChecks"/>) whose holder a cast
/// or a staffing gives an agent that a live session of serves (<see cref="CastReading.Serves"/>). An actor or a spent red a live session serves
/// is bounded all the same: one it never answers reaches the final exit past the same bound. No dispatcher hands a role-classed Accept,
/// approval or opinion dispute to a session yet, so one an agent holds is a vacancy, though an Accept's or approval's agent may still answer it
/// by <c>step_settle</c>. A line and a notice are said only once their record is kept, so a file that will not write repeats neither.
/// </summary>
public static class StepVacancies
{
    public static bool Served(RoleWait wait, Func<string, bool> serves) =>
        wait.Why == PersonWaits.Task || (wait.Why is PersonWaits.Actor or PersonWaits.Left && serves(wait.Role));

    /// <summary>Whether an agent's holding is judged here: every wait but an attempt held and a part left, which carry rows of their own; a role-held spent red is judged.</summary>
    public static bool Holdable(RoleWait wait) =>
        wait.Why is not (PersonWaits.Left or PersonWaits.Held) || wait is { Why: PersonWaits.Left, Decided: NodeTold.RoleHeld };

    public static bool Is(RoleWait wait, Func<string, bool> serves) =>
        wait.Holder.Kind == StepHolders.Agent && Holdable(wait) && !Served(wait, serves);

    /// <summary>A served wait nothing may ever settle (an actor's, a spent red's, a task's, whether or not a session lives): bounded as a vacancy is, never listed.</summary>
    public static bool Bounded(RoleWait wait, Func<string, bool> serves) =>
        wait.Holder.Kind == StepHolders.Agent && Holdable(wait) && wait.Why is PersonWaits.Actor or PersonWaits.Left or PersonWaits.Task && Served(wait, serves);

    /// <summary>
    /// The vacancy kept for the step's attempt, judged again as it is read: one at the final exit whoever holds or serves it now, else one kept
    /// vacant whose holder is an agent and, where <paramref name="serves"/> is given, nothing serving it now.
    /// </summary>
    public static StepVacancy? Kept(IReadOnlyList<StepVacancy>? vacancies, RoleWait wait, string node, int attempt, Func<string, bool>? serves = null) =>
        vacancies?.FirstOrDefault(each => each.Node == node && each.Attempt == attempt) is { } kept
        && (kept.Exited || (wait.Holder.Kind == StepHolders.Agent && !kept.Served && (serves is null || Is(wait, serves))))
            ? kept
            : null;

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

    public static string Unanswered(string step, RoleWait wait, DateTimeOffset since, DateTimeOffset now) =>
        FinalExits.Notice($"{step} stood unanswered", [
            new($"the role `{wait.Role}`'s agent{Named(wait)}", since,
                wait.Why == PersonWaits.Task
                    ? "the run's working session was handed it and never settled it"
                    : $"a live session of it serves the place and never answered its {wait.Why}"),
        ], FinalExits.Answer, now);

    /// <summary>
    /// Each run's vacancies judged now and kept: one first seen is kept from now and announced; one announced and past its bound gives its
    /// one notice (<paramref name="noticed"/>) and its <c>risk.judged</c> line and is kept as exited, never said again whoever serves it later; a
    /// served wait an actor or a spent red's role holds is kept from its first serving and reaches the exit past the same bound. A step
    /// no longer vacant drops its vacancy, so its clock starts again. Each line, notice and journal line is said once its record is kept.
    /// </summary>
    internal static void Look(
        NodeRecordFiles records, IStepVacancyStore vacancies, IEnumerable<GovernedUnit> governed, DriverConfig? config, DateTimeOffset now, List<string> said,
        List<LookLine> noticed, Func<string?, string?, bool>? live = null)
    {
        var bound = VacancySetting.Of(config);
        var casts = new PlaceCastFiles(records.Home).Book();
        var tenures = new StepTenureFiles(records.Home, records.HoldsLease);
        foreach (var each in governed)
        {
            if (each.Scope is null || records.Read(each.Unit.Id) is not { } record) continue;
            var kept = vacancies.Of(each.Unit.Id);
            var next = new List<StepVacancy>();
            var lines = new List<string>();
            var exits = new List<(LookLine Line, NodeDeclaration Node, int Attempt, RoleWait Wait)>();
            var (workspace, repository) = (each.Unit.Owner.Workspace, each.Unit.Owner.Repository);
            var holding = tenures.Holding(each.Unit.Id, record.Statuses.ToDictionary(pair => pair.Key, pair => pair.Value.Attempt, StringComparer.Ordinal));
            var cast = CastReading.At(casts, config?.PlaceRisk, workspace, repository);
            var serves = CastReading.Serves(casts, config?.PlaceRisk, workspace, repository, live);
            foreach (var node in record.Ended is null ? each.Graph.Nodes : [])
            {
                var status = WorkPerformer.StatusOf(record, node.Id);
                if (StepWaiting.Read(record, node, status, holding.GetValueOrDefault(node.Id), cast) is not { } wait || record.Pending.Any(word => word.Node == node.Id)) continue;
                var was = kept.FirstOrDefault(one => one.Node == node.Id && one.Attempt == status.Attempt);
                if (was is { Exited: true })
                {
                    next.Add(was);
                    continue;
                }

                var vacant = Is(wait, serves);
                if (!vacant && !Bounded(wait, serves)) continue;
                var vacancy = was is null ? new StepVacancy(node.Id, status.Attempt, now, false, false) { Served = !vacant } : was with { Served = !vacant };
                var step = $"work  {each.Unit.Id}: step `{node.Id}` of #{each.Run}";
                if (vacant && !vacancy.Told)
                {
                    lines.Add($"{step} is {Said(wait, bound)}.");
                    vacancy = vacancy with { Told = true };
                }
                else if (vacancy.Since + bound <= now)
                {
                    var story = vacant ? Notice(step, wait, vacancy.Since, now) : Unanswered(step, wait, vacancy.Since, now);
                    exits.Add((FinalExits.Line(each.Run, node.Id, status.Attempt, workspace, story), node, status.Attempt, wait));
                    vacancy = vacancy with { Told = true, Exited = true };
                }

                next.Add(vacancy);
            }

            if (next.SequenceEqual(kept) || !Keep(vacancies, records.Home, each.Unit.Id, next, said)) continue;
            said.AddRange(lines);
            foreach (var exit in exits)
            {
                noticed.Add(exit.Line);
                Judged(records.Home, config, workspace, repository, each.Run, exit.Node, exit.Attempt, exit.Wait, now);
            }
        }

        Sweep(records, vacancies, governed, said);
    }

    /// <summary>A vacancy's exit journaled as a spent red's is: the act the wait holds (<c>work.recover</c> where none is a step's own), at the run's place.</summary>
    private static void Judged(string home, DriverConfig? config, string? workspace, string? repository, string run, NodeDeclaration node, int attempt, RoleWait wait, DateTimeOffset now)
    {
        var kind = (wait.Why == PersonWaits.Left ? null : RiskStepHolds.KindOf(wait.Why, node)) ?? RiskActs.WorkRecover;
        if (config is null || RiskStepGates.Verdict(RiskStepGates.At(config, workspace, repository), kind, node) is not { } verdict) return;
        var place = RiskPlace.Of(config, workspace, repository);
        try
        {
            RiskJournal.Append(home, new RiskJudged(now, RiskJudged.Released, $"vacant:{run}/{node.Id}#{attempt}", wait.Why, verdict.Kind, verdict.Qualifier, RiskPolicy.Key,
                place.Workspace, place.Repository, place.Mode, run, node.Id, $"run {run}, step {node.Id}, attempt {attempt}", verdict.Outcome, verdict.Class, verdict.By,
                verdict.Source, verdict.Revision, verdict.Reason, SpentChecks.Today, true));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The exit is kept whether or not the journal takes it.
        }
    }

    private static readonly ConcurrentDictionary<string, byte> Unkept = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A file that will not write never ends the pass: it is said once, and the vacancies are judged again at the next look.</summary>
    private static bool Keep(IStepVacancyStore vacancies, string home, string unit, IReadOnlyList<StepVacancy> next, List<string> said)
    {
        try
        {
            vacancies.Keep(unit, next);
            Unkept.TryRemove($"{home}|{unit}", out _);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if (Unkept.TryAdd($"{home}|{unit}", 0)) said.Add($"work  {unit}: its vacancies could not be kept ({error.Message}), so Daoris judges them again at each look.");
            return false;
        }
    }

    /// <summary>Drops the vacancies kept for a run no pass governs whose record is gone; a run that stands keeps its own.</summary>
    private static void Sweep(NodeRecordFiles records, IStepVacancyStore vacancies, IEnumerable<GovernedUnit> governed, List<string> said)
    {
        var standing = governed.Select(each => each.Unit.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var unit in vacancies.Units().Where(unit => !standing.Contains(unit) && !records.Exists(unit))) Keep(vacancies, records.Home, unit, [], said);
    }

    private static string Named(RoleWait wait) => wait.Holder.Name is { } name ? $" (`{name}`)" : "";
}
