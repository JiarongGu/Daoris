using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The landing kind: a <c>review</c> step waiting for the person's Accept, or a <c>task</c> approval waiting for Approve or Reject, its
/// subject <c>{ session, landing }</c> naming the run, the node and the attempt. Its decide is held for the role's window with <i>Overrule</i>
/// and then released as the node's own word on the run's record; its leave is a hold, the person's at once. The step and the person's stop or
/// pause of its line are read again at the keeping: a moved step is refused <c>answered</c>, a held line <c>owner-held</c>, a go-ahead's run <c>go-ahead-open</c>.
/// </summary>
internal sealed class LandingKind : ISupervisorKind
{
    public string Kind => SupervisorKinds.Landing;

    public Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, new Keeping(form.Subject, form.Generation, form.Do, form.Reasons, holder, null), ct);

    public Task<SupervisorDecided> LeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, new Keeping(form.Subject, form.Generation, SupervisorLandingActs.Hold, form.Reasons, holder, form.Bound), ct);

    /// <summary>The steps of runs here waiting for a press a landing supervisor makes and no decision answers yet, in a repository whose supervisor's hold covers an act the step offers.</summary>
    public async Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, IReadOnlyList<PriorSession> records, IDecisionAdmission admission, CancellationToken ct)
    {
        var waiting = new List<object>();
        var steps = LandingSteps.Waiting(loop.Home);
        var serves = LandingSessions.Serves(steps.Count == 0 || loop.Service is not { } service ? null : await service.EveryQuestAsync(ct).ConfigureAwait(false));
        foreach (var step in steps)
        {
            if (step.GoAheadOpen || LandingSessions.Of(records, step, serves) is not { } session) continue;
            var subject = SupervisorSubject.Landing(session.Session, step.Run, step.Node, step.Attempt);
            if (SupervisorDecisionDoor.Covered(admission, subject, new RolePlace(Repository: step.Repository)) is not { } covered) continue;
            if (step.Offered(covered.Acts) is not { Count: > 0 } acts) continue;
            if (!loop.Supervisor.Refers(subject)) continue;
            waiting.Add(new
            {
                Kind, Subject = subject.Wire(), Repository = step.Repository, step.Run, step.Node, step.Attempt, covered.Generation,
                Acts = acts, Revision = SupervisorReviewBinding.RevisionOf(loop.Home, step.Run, step.Node, step.Attempt),
            });
        }

        return waiting;
    }

    public async Task<string?> FactsAsync(DriverLoop loop, SupervisorSubject subject, string fence, CancellationToken ct)
    {
        if (subject.LandingParts is not { } parts || loop.Service is not { } service) return null;
        if (LandingSteps.Find(loop.Home, parts.Run, parts.Node, parts.Attempt) is not { GoAheadOpen: false } step) return null;
        var recorded = $"run: {step.Run}\nstep: {step.Node}, attempt {step.Attempt}\nrepository: {step.Repository}\nit waits for a press: {string.Join(", ", step.Acts)}";
        IReadOnlyList<(string, string)> facts = ReviewRecords.Read(loop.Home, step.Run, step.Node, step.Attempt) is { } record
            ? [("the step a landing waits on: Daoris's own record, not the asking agent's", recorded),
               ("the work under review, as its review record read it once and keeps it: Daoris's own record", LandingReviewFacts.Of(record))]
            : [("the step a landing waits on: Daoris's own record, not the asking agent's", recorded)];
        return await SupervisorKindFacts.ComposeAsync(service, loop.Home, subject.Session, fence, facts, ct).ConfigureAwait(false);
    }

    private async Task<SupervisorDecided> KeepAsync(DriverLoop loop, Keeping keeping, CancellationToken ct)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        if (keeping.Subject.LandingParts is not { } parts) return SupervisorDecided.Refused(SupervisorWhy.Form);
        if (await service.RecordAsync(keeping.Subject.Session, ct).ConfigureAwait(false) is not { Repository: { } repository, Quest: { Length: > 0 } quest } record
            || !LandingSessions.Serves(await service.EveryQuestAsync(ct).ConfigureAwait(false))(quest, parts.Run))
        {
            return SupervisorDecided.Refused(SupervisorWhy.NotReferred);
        }

        if (SupervisorDecisionDoor.Covers(loop, keeping.Generation, keeping.Subject, repository) is { } uncovered) return SupervisorDecided.Refused(uncovered);
        var step = LandingSteps.Find(loop.Home, parts.Run, parts.Node, parts.Attempt);
        var landing = new SupervisorLanding(
            keeping.Subject.Session, parts.Run, parts.Node, parts.Attempt, keeping.Generation, keeping.Do, keeping.Reasons, keeping.Holder, keeping.Bound)
        {
            Revision = SupervisorReviewBinding.RevisionOf(loop.Home, parts.Run, parts.Node, parts.Attempt), Epoch = step?.Epoch,
        };
        var refused = LandingJudge.Refused(step, keeping.Do, OwnerHolds.Holds(record, loop.Look.Latest));
        var kept = await loop.Supervisor.LandingAsync(landing, repository, refused, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        return SupervisorDecided.Of(kept, SupervisorWhy.NotReferred);
    }

    private sealed record Keeping(
        SupervisorSubject Subject, string Generation, string Do, IReadOnlyList<SupervisorReason> Reasons, SupervisorHolder Holder, string? Bound);
}
