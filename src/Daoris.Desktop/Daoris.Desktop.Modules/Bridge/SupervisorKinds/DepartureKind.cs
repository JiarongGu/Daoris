using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The departure kind: a quest closed done departing from what the person required, held for their yes, its subject
/// <c>{ session, quest }</c> naming the quest's newest session here. Accepted under <c>departure.accept</c>, held for the role's window with
/// <i>Overrule</i>, then posted to the service's accept door as the role's yes (<see cref="SupervisorDoorAnswers"/>); its leave is the
/// person's at once. A set-up step its review holds waits for the person's review, which no yes lifts, so it is never listed here.
/// </summary>
internal sealed class DepartureKind : ISupervisorKind
{
    public string Kind => SupervisorKinds.Departure;

    public async Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, IReadOnlyList<PriorSession> records, IDecisionAdmission admission, CancellationToken ct)
    {
        var waiting = new List<object>();
        if (loop.Service is not { } service) return waiting;
        IReadOnlyList<QuestView> quests;
        try
        {
            quests = await service.EveryQuestAsync(ct).ConfigureAwait(false);
        }
        catch (DriverException)
        {
            // A host that answers no quests lists no departure for a supervisor; the person's own row reads them as it does.
            return waiting;
        }

        foreach (var quest in quests.Where(Waits))
        {
            if (records.LastOrDefault(each => each.Quest == quest.Id) is not { } session) continue;
            var repository = session.Repository ?? quest.To;
            if (SupervisorDecisionDoor.Covered(admission, Kind, new RolePlace(Repository: repository)) is not { } covered) continue;
            var subject = new SupervisorSubject(Kind, session.Session, quest.Id);
            if (!covered.Acts.Contains(SupervisorDoorActs.Accept) || !loop.Supervisor.RefersDoor(subject)) continue;
            waiting.Add(new { Kind, Subject = subject.Wire(), Repository = repository, covered.Generation, Acts = (IReadOnlyList<string>)[SupervisorDoorActs.Accept] });
        }

        return waiting;
    }

    public Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, form.Subject, form.Generation, form.Do, form.Reasons, holder, ct);

    public Task<SupervisorDecided> LeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, form.Subject, form.Generation, SupervisorDoorActs.Hold, form.Reasons, holder, ct);

    public async Task<RolePlace?> PlaceOfAsync(DriverLoop loop, SupervisorSubject subject, CancellationToken ct) =>
        loop.Service is { } service && await service.RecordAsync(subject.Session, ct).ConfigureAwait(false) is { Repository: { } repository }
            ? new RolePlace(Repository: repository)
            : null;

    // A done its departure holds for the person's yes, as What needs you lists it.
    private static bool Waits(QuestView quest) => quest.Held && quest.Hold != "unreviewed";

    private async Task<SupervisorDecided> KeepAsync(
        DriverLoop loop, SupervisorSubject subject, string generation, string act, IReadOnlyList<SupervisorReason> reasons, SupervisorHolder holder, CancellationToken ct)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        if (await service.RecordAsync(subject.Session, ct).ConfigureAwait(false) is not { Repository: { } repository, Quest: { } quest } || quest != subject.Id)
        {
            return SupervisorDecided.Refused(SupervisorWhy.NotReferred);
        }

        if (SupervisorDecisionDoor.Covers(loop, generation, Kind, repository) is { } uncovered) return SupervisorDecided.Refused(uncovered);
        var waits = (await service.EveryQuestAsync(ct).ConfigureAwait(false)).Any(each => each.Id == quest && Waits(each));
        var answer = new SupervisorDoorAnswer(subject, repository, generation, act, null, reasons, holder);
        var kept = await loop.Supervisor.DoorAsync(answer, waits ? null : SupervisorWhy.Answered, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        return SupervisorDecided.Of(kept, SupervisorWhy.NotReferred);
    }
}
