using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A kind whose subject is a point a person's <c>refer</c> rule holds a line at, <c>{ session, point }</c>: the course kind and the limit
/// kind, each taking its own counts (<see cref="SupervisorRules.KindOf"/>). Its decide is held for the role's window and then released
/// through the count's own door between turns; its leave is a hold, the person's at once. The line's repository is the effective
/// supervisor's, and the point, its counts and the person's own holds on the line are read again at the keeping, so a decide naming a
/// point the line has passed is refused <c>answered</c> and one on a line the person stopped <c>owner-held</c>.
/// </summary>
internal abstract class ReferredPointKind : ISupervisorKind
{
    public abstract string Kind { get; }

    public Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, new SupervisorCourse(form.Subject.Session, form.Subject.Id, form.Generation, form.Do, form.Reasons, holder) { Kind = Kind }, ct);

    public Task<SupervisorDecided> LeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, new SupervisorCourse(form.Subject.Session, form.Subject.Id, form.Generation, SupervisorCourseActs.Hold, form.Reasons, holder, form.Bound) { Kind = Kind }, ct);

    /// <summary>The points of this kind a refer holds a line at and no decision answers yet, in a repository whose supervisor's hold covers an act of the kind.</summary>
    public Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, IReadOnlyList<PriorSession> records, IDecisionAdmission admission, CancellationToken ct)
    {
        var points = ReferredPoints.For(loop.Home, loop.Events, new GoOnMarks(loop.Home));
        var waiting = new List<object>();
        foreach (var record in records)
        {
            if (record.Teammate || record.Repository is not { } repository || points.Open(record.Session, loop.Look.Latest) is not { } open) continue;
            if (SupervisorRules.KindOf(open.Rule.Count) != Kind || !loop.Supervisor.Refers(record.Session, open.Id, Kind)) continue;
            if (SupervisorDecisionDoor.Covered(admission, Kind, new RolePlace(Repository: repository)) is not { } covered) continue;
            waiting.Add(new
            {
                Kind, Subject = SupervisorSubject.Point(record.Session, open.Id, Kind).Wire(), Repository = repository, Count = open.Rule.Count, N = open.N,
                Rule = open.Rule.Id, Place = open.Rule.Place, covered.Generation, covered.Acts,
            });
        }

        return Task.FromResult<IReadOnlyList<object>>(waiting);
    }

    private async Task<SupervisorDecided> KeepAsync(DriverLoop loop, SupervisorCourse course, CancellationToken ct)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        if (await service.RecordAsync(course.Session, ct).ConfigureAwait(false) is not { Repository: { } repository } record) return SupervisorDecided.Refused(SupervisorWhy.NotReferred);
        if (SupervisorDecisionDoor.Covers(loop, course.Generation, Kind, repository) is { } uncovered) return SupervisorDecided.Refused(uncovered);

        var counts = (await LineCountsRead.FactsAsync(service, loop.Home, course.Session, ct).ConfigureAwait(false)).Counts;
        var point = ReferredPoints.For(loop.Home, loop.Events, new GoOnMarks(loop.Home)).PointOf(record, counts, loop.Look.Latest);
        var kept = await loop.Supervisor.CourseAsync(course, point, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        return SupervisorDecided.Of(kept, SupervisorWhy.NotReferred);
    }
}
