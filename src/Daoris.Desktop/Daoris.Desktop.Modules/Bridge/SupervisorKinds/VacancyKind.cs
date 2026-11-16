using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The vacancy kind: a second opinion nobody is free to give, its subject <c>{ session, step }</c> naming the gate's chain and repository.
/// It waits on the reporting line's level that holds it now: the lead at the repository (the head where no lead is appointed), and the
/// head at the machine once the lead left it. Its decide re-casts (naming a reviewer the vacancy offered, the prepaid one only with
/// <c>vacancy.prepaid</c> granted), relaxes the family difference, spends the prepaid balance or waits, journalled at once beside the gate
/// (<see cref="OpinionCasts"/>), which the look's next walk reads until a walk fills it; its leave is its pass up, and the head's ends it
/// as gave up, listed to nobody.
/// </summary>
internal sealed class VacancyKind : ISupervisorKind
{
    public string Kind => SupervisorKinds.Vacancy;

    public async Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, IReadOnlyList<PriorSession> records, IDecisionAdmission admission, CancellationToken ct)
    {
        var waiting = new List<object>();
        foreach (var (chain, repository, session, casts) in await Task.Run(() => new OpinionCasts(loop.Home).Waiting(), ct).ConfigureAwait(false))
        {
            if (Up(loop, casts, repository) is not { } place || SupervisorDecisionDoor.Covered(admission, Kind, place) is not { } covered) continue;
            waiting.Add(new
            {
                Kind, Subject = SupervisorSubject.Vacancy(session, chain, repository).Wire(), Repository = repository, Asked = Moments.Write(casts[^1].At),
                covered.Generation, covered.Acts,
            });
        }

        return waiting;
    }

    public Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder, CancellationToken ct) =>
        Task.FromResult(Keep(loop, form.Subject, form.Generation, form.Do, form.Choice, form.Reasons, holder));

    public Task<SupervisorDecided> LeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct) =>
        Task.FromResult(Keep(loop, form.Subject, form.Generation, LinePasses.Left, null, form.Reasons, holder, form.Bound));

    public Task<RolePlace?> PlaceOfAsync(DriverLoop loop, SupervisorSubject subject, CancellationToken ct) =>
        Task.FromResult(subject.VacancyParts is { } parts ? Up(loop, new OpinionCasts(loop.Home).Read(parts.Chain, parts.Repository, subject.Session), parts.Repository) : null);

    /// <summary>
    /// The act kept against the vacancy as it stands: still waiting, at the level whose hold is <paramref name="generation"/> and grants
    /// the act there; else refused, keeping nothing.
    /// </summary>
    private static SupervisorDecided Keep(
        DriverLoop loop, SupervisorSubject subject, string generation, string act, string? agent, IReadOnlyList<SupervisorReason> reasons, SupervisorHolder holder,
        string? bound = null)
    {
        if (subject.VacancyParts is not { } parts) return SupervisorDecided.Refused(SupervisorWhy.Form);
        var casts = new OpinionCasts(loop.Home);
        var kept = casts.Read(parts.Chain, parts.Repository, subject.Session);
        if (!OpinionCasting.Waiting(kept) || Up(loop, kept, parts.Repository) is not { } place) return SupervisorDecided.Refused(SupervisorWhy.SubjectChanged);
        if (loop.Pipe?.StandingConfirmation is not { } judge) return SupervisorDecided.Refused(SupervisorWhy.NotStaffed);
        var admitted = judge.Admit(SupervisorKinds.Vacancy, place, act == LinePasses.Left ? null : act);
        if (admitted.Why is { } why) return SupervisorDecided.Refused(why);
        if (admitted.Generation != generation) return SupervisorDecided.Refused(SupervisorWhy.RoleChanged);
        // A re-cast names a reviewer the vacancy offered; and every grant on the act's chain covers what it spends (D306).
        if (OpinionCasting.Refusal(kept, act, agent) is { } refused) return SupervisorDecided.Refused(refused);
        var role = judge.Supervising(SupervisorKinds.Vacancy, place).Role;
        if (act != LinePasses.Left && ActSpends.Missing([role?.Hold?.Acts ?? []], SupervisorKinds.Qualified(SupervisorKinds.Vacancy, act), OpinionCasting.Spends(act, agent))
            is { } missing)
        {
            return SupervisorDecided.Refused(SupervisorWhy.NotGranted) with { Missing = missing };
        }

        var actor = role is { Scope.Rank: > 0 } ? LineActors.Lead : LineActors.Head;
        var reading = string.Join(" ", reasons.Select(reason => reason.Says));
        casts.Keep(parts.Chain, parts.Repository, new OpinionCast(
            DateTimeOffset.UtcNow, subject.Session, actor, role?.Id ?? holder.Agent?.Role ?? holder.Kind, act, agent,
            bound is null ? reading : $"{bound}: {reading}"));
        return new SupervisorDecided(true, act, null, null);
    }

    /// <summary>
    /// Where the vacancy waits now: at the repository until a level leaves it, then at the machine, the head's place; null once the head
    /// left it too, gave up with what it tried.
    /// </summary>
    private static RolePlace? Up(DriverLoop loop, IReadOnlyList<OpinionCast> casts, string repository)
    {
        var left = OpinionCasting.Left(casts);
        if (left.ContainsKey(LineActors.Head)) return null;
        if (left.ContainsKey(LineActors.Lead)) return new RolePlace();
        return loop.Pipe?.StandingConfirmation is { } judge ? judge.Placed(repository) : new RolePlace(Repository: repository);
    }
}
