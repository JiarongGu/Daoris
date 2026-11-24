using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The go-ahead kind: a go-ahead a session asked for on its ask, its subject <c>{ session, goahead }</c> naming the newest
/// session that asked it and the ask's number. Listed where a role granted <c>goahead.answer</c> may answer it (<see cref="GoAheadAuthority"/>:
/// wherever the risk policy does not class its act the owner's, and never a sign-in); its decide answers approved or
/// refused, pinned to the go-ahead's number, kind, target and act, held for the role's window with <i>Overrule</i> in the held decisions'
/// table, judged by the policy again at the release, then posted to the service's go-ahead door as the role's answer
/// (<see cref="SupervisorDoorAnswers"/>); its leave climbs the line. Its listing reads the service only where a role could answer one
/// (<see cref="GoAheadsOpen"/>).
/// </summary>
internal sealed class GoAheadKind : ISupervisorKind
{
    public string Kind => SupervisorKinds.GoAhead;

    public async Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, IReadOnlyList<PriorSession> records, IDecisionAdmission admission, CancellationToken ct)
    {
        var waiting = new List<object>();
        if (loop.Service is not { } service || loop.Pipe?.StandingConfirmation is not { } judge) return waiting;
        IReadOnlyList<OpenGoAhead> open;
        try
        {
            open = await GoAheadsOpen.ReadAsync(judge.Appointed(), records, service.AsksAsync, ct).ConfigureAwait(false);
        }
        catch (DriverException)
        {
            // A host that answers no ask lists no go-ahead for a supervisor; the ask's page reads them as it does.
            return waiting;
        }

        foreach (var (ask, goAhead, repository, session) in open)
        {
            var subject = SupervisorSubject.GoAhead(session, ask, goAhead.Number);
            if (!loop.Supervisor.RefersDoor(subject) || SupervisorDecisionDoor.Covered(admission, subject, new RolePlace(Repository: repository)) is not { } covered) continue;
            // Only a go-ahead a role may answer here is listed: one the policy classes the owner's and a sign-in wait on the owner role.
            if (Refusal(loop, judge, repository, goAhead) is not null) continue;
            waiting.Add(new
            {
                Kind, Subject = subject.Wire(), Repository = repository, covered.Generation, Acts = (IReadOnlyList<string>)[SupervisorKinds.Answer],
                Choices = (IReadOnlyList<string>)[SupervisorDoorActs.Approved, SupervisorDoorActs.Refused],
                GoAhead = new { goAhead.Number, goAhead.Kind, goAhead.On, goAhead.Act },
            });
        }

        return waiting;
    }

    public Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, form.Subject, form.Generation, form.Do, form.Choice, form.Reasons, holder, ct);

    public Task<SupervisorDecided> LeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, form.Subject, form.Generation, SupervisorDoorActs.Hold, null, form.Reasons, holder, ct);

    public async Task<string?> FactsAsync(DriverLoop loop, SupervisorSubject subject, string fence, CancellationToken ct)
    {
        if (loop.Service is not { } service || await SupervisorDoorSubjects.GoAheadAsync(service, subject, ct).ConfigureAwait(false) is not { Answer: null } goAhead) return null;
        var asked = string.Join("\n", goAhead.Asked.Select(request => $"- {request.Session}: {request.Why}"));
        IReadOnlyList<(string, string)> facts =
        [
            ("the go-ahead as its ask holds it: Daoris's own record", $"go-ahead {goAhead.Number}: {goAhead.Kind} on {goAhead.On}: \"{goAhead.Act}\""),
            ("each session's request for it, in its own words", asked),
        ];
        return await SupervisorKindFacts.ComposeAsync(service, loop.Home, subject.Session, fence, facts, ct).ConfigureAwait(false);
    }

    public async Task<RolePlace?> PlaceOfAsync(DriverLoop loop, SupervisorSubject subject, CancellationToken ct) =>
        loop.Service is { } service && await service.RecordAsync(subject.Session, ct).ConfigureAwait(false) is { Repository: { } repository }
            ? new RolePlace(Repository: repository)
            : null;

    private async Task<SupervisorDecided> KeepAsync(
        DriverLoop loop, SupervisorSubject subject, string generation, string act, string? choice, IReadOnlyList<SupervisorReason> reasons,
        SupervisorHolder holder, CancellationToken ct)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        if (loop.Pipe?.StandingConfirmation is not { } judge) return SupervisorDecided.Refused(SupervisorWhy.NotStaffed);
        if (await service.RecordAsync(subject.Session, ct).ConfigureAwait(false) is not { Repository: { } repository }) return SupervisorDecided.Refused(SupervisorWhy.NotReferred);
        if (SupervisorDecisionDoor.Covers(loop, generation, subject, repository) is { } uncovered) return SupervisorDecided.Refused(uncovered);
        // The go-ahead as it stands, pinned to its number, kind, target and act; one answered since is no longer the role's to answer.
        var goAhead = await SupervisorDoorSubjects.GoAheadAsync(service, subject, ct).ConfigureAwait(false);
        var open = goAhead is { Answer: null };
        // The risk policy as it stands at the answer, and every grant on the role's chain; a leave needs none.
        var refused = !open ? SupervisorWhy.Answered : act == SupervisorDoorActs.Hold ? null : Refusal(loop, judge, repository, goAhead!);
        var answer = new SupervisorDoorAnswer(subject, repository, generation, act, choice, reasons, holder) { Pin = open ? SupervisorDoorSubjects.Pin(goAhead!).Said : null };
        var kept = await loop.Supervisor.DoorAsync(answer, refused, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        return SupervisorDecided.Of(kept, SupervisorWhy.NotReferred);
    }

    // Why a role may not answer it at the repository's place now: the owner role's code by the policy, else the grant missing on the role's chain.
    private static string? Refusal(DriverLoop loop, StandingConfirmation judge, string repository, GoAheadView goAhead)
    {
        var place = judge.Placed(repository);
        var grants = judge.Supervising(SupervisorKinds.GoAhead, place).Role?.Hold?.Acts ?? [];
        var config = DriverConfig.Load(loop.ConfigPath);
        return GoAheadAuthority.Refusal(RiskPolicy.Of(config), RiskPlace.Of(config, place.Workspace, place.Repository), goAhead.Kind, goAhead.On, [grants]);
    }
}
