using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The departure kind: a quest closed done departing from what the person required, held by that departure alone for their yes, its
/// subject <c>{ session, quest }</c> naming the quest's newest session here. Listed with its facts (what the quest required beside how the
/// done answered it, <see cref="SupervisorDoorSubjects.FactsAsync"/>) and accepted under <c>departure.accept</c>, pinned to the quest as it
/// stood when judged, held for the role's window with <i>Overrule</i>, then posted to the service's accept door as the role's yes
/// (<see cref="SupervisorDoorAnswers"/>); its leave is the person's at once. A done held for its evidence, a set-up step's review or a
/// reopened source is the person's, so it is never listed here.
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

        foreach (var quest in quests.Where(SupervisorDoorSubjects.Departed))
        {
            if (records.LastOrDefault(each => each.Quest == quest.Id) is not { } session) continue;
            var repository = session.Repository ?? quest.To;
            var subject = new SupervisorSubject(Kind, session.Session, quest.Id);
            if (SupervisorDecisionDoor.Covered(admission, subject, new RolePlace(Repository: repository)) is not { } covered) continue;
            if (!covered.Acts.Contains(SupervisorDoorActs.Accept) || !loop.Supervisor.RefersDoor(subject)) continue;
            waiting.Add(new
            {
                Kind, Subject = subject.Wire(), Repository = repository, covered.Generation, Acts = (IReadOnlyList<string>)[SupervisorDoorActs.Accept],
                Facts = (await SupervisorDoorSubjects.FactsAsync(service, subject, ct).ConfigureAwait(false))?.Wire(),
            });
        }

        return waiting;
    }

    public Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, form.Subject, form.Generation, form.Do, form.Reasons, holder, ct);

    public Task<SupervisorDecided> LeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, form.Subject, form.Generation, SupervisorDoorActs.Hold, form.Reasons, holder, ct);

    public async Task<string?> FactsAsync(DriverLoop loop, SupervisorSubject subject, string fence, CancellationToken ct) =>
        loop.Service is { } service && await SupervisorDoorSubjects.FactsAsync(service, subject, ct).ConfigureAwait(false) is { } facts
            ? await SupervisorKindFacts.ComposeAsync(service, loop.Home, fence, facts, ct).ConfigureAwait(false)
            : null;

    public async Task<RolePlace?> PlaceOfAsync(DriverLoop loop, SupervisorSubject subject, CancellationToken ct) =>
        loop.Service is { } service && await service.RecordAsync(subject.Session, ct).ConfigureAwait(false) is { Repository: { } repository }
            ? new RolePlace(Repository: repository)
            : null;

    private async Task<SupervisorDecided> KeepAsync(
        DriverLoop loop, SupervisorSubject subject, string generation, string act, IReadOnlyList<SupervisorReason> reasons, SupervisorHolder holder, CancellationToken ct)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        if (await service.RecordAsync(subject.Session, ct).ConfigureAwait(false) is not { Repository: { } repository, Quest: { } quest } || quest != subject.Id)
        {
            return SupervisorDecided.Refused(SupervisorWhy.NotReferred);
        }

        if (SupervisorDecisionDoor.Covers(loop, generation, subject, repository) is { } uncovered) return SupervisorDecided.Refused(uncovered);
        // The quest as it stands, pinned: a done redone, answered again or held for anything but its departure is no longer what the role judged.
        var pin = await SupervisorDoorSubjects.PinAsync(service, subject, ct).ConfigureAwait(false);
        var answer = new SupervisorDoorAnswer(subject, repository, generation, act, null, reasons, holder) { Pin = pin };
        var kept = await loop.Supervisor.DoorAsync(answer, pin is null ? SupervisorWhy.Answered : null, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        return SupervisorDecided.Of(kept, SupervisorWhy.NotReferred);
    }
}
