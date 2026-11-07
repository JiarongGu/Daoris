using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The holder's course on a point a person's <c>refer</c> rule holds a line at (CONTEXTDRIVE1j; the context counters design §4.6 point 3,
/// §5): listed beside the questions that wait, and decided on the role's door alone as an answer is, the line's repository the effective
/// supervisor's, the point and its counts read again at the keeping.
/// </summary>
public static class SupervisorCourseDoor
{
    /// <summary><c>SUPERVISOR_COURSE</c>'s handler: kept with the decision and when it goes on, or refused as data, keeping nothing.</summary>
    public static async Task<SupervisorDoor.KeptAnswer> CourseAsync(DriverLoop loop, System.Text.Json.JsonElement? payload, CancellationToken ct)
    {
        if (PipeDoor.Role is not { Generation: { } generation }) return Refused(SupervisorWhy.NotHolder);
        if (SupervisorCourseForm.Read(payload) is not { } form) return Refused(SupervisorWhy.Form);
        if (form.Generation != generation) return Refused(SupervisorWhy.RoleChanged);
        var service = loop.Service ?? throw DriverArguments.NotReady();
        if (await service.RecordAsync(form.Id, ct).ConfigureAwait(false) is not { Repository: { } repository } record) return Refused(SupervisorWhy.NotReferred);
        var uncovered = loop.Pipe?.StandingConfirmation is { } judge ? judge.Covers(generation, repository) : SupervisorWhy.NotStaffed;
        if (uncovered is not null) return Refused(uncovered);

        var counts = (await LineCountsRead.FactsAsync(service, loop.Home, form.Id, ct).ConfigureAwait(false)).Counts;
        var point = ReferredPoints.For(loop.Home, loop.Events, new GoOnMarks(loop.Home)).PointOf(record, counts);
        var kept = await loop.Supervisor.CourseAsync(
            new SupervisorCourse(form.Id, form.Point, form.Generation, form.Do, form.Reasons, new SupervisorHolder(RoleHolders.Terminal, null)),
            point, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        if (!kept.Kept) return Refused(kept.Why ?? SupervisorWhy.NotReferred);

        loop.Nudge();
        return new SupervisorDoor.KeptAnswer(true, kept.Decision, kept.GoesOnAt is { } at ? Moments.Write(at) : null, null);
    }

    /// <summary>The points a refer holds a line at and no decision answers yet, in a repository a live supervisor's hold covers, each with that hold's generation.</summary>
    public static IReadOnlyList<object> Waiting(DriverLoop loop, IEnumerable<PriorSession> records, IDecisionAdmission admission)
    {
        var points = ReferredPoints.For(loop.Home, loop.Events, new GoOnMarks(loop.Home));
        var waiting = new List<object>();
        foreach (var record in records)
        {
            if (record.Teammate || record.Repository is not { } repository || points.Open(record.Session) is not { } open) continue;
            if (!loop.Supervisor.Refers(record.Session, open.Id) || admission.Admit(repository) is not { Why: null, Generation: { } generation }) continue;
            waiting.Add(new { Session = record.Session, Repository = repository, Point = open.Id, Count = open.Rule.Count, N = open.N, Generation = generation });
        }

        return waiting;
    }

    private static SupervisorDoor.KeptAnswer Refused(string why) => new(false, null, null, why);
}
