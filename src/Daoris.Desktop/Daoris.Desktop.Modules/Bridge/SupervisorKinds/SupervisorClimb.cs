using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// Every kind's leave climbs the reporting line: a leave records the level that made it (<see cref="LineLeaves"/>), and the
/// subject is listed, kept and released at the level <see cref="ReportingLine.Climb"/> finds with that map: the place's supervisor until it
/// left the subject, then the machine's. A leave by a level below the machine, of a bound no owner-class kind names, where the machine's
/// supervisor holds the kind, is kept as <c>left</c> with nothing final; every other leave is the kind's own, final at once, the owner
/// role's for an owner-class bound and otherwise ended as gave up with what it tried. The vacancy kind journals its own leaves beside its
/// gate and Ask Daoris's kind is the machine's alone, so neither climbs here; nor, yet, do the allow and files kinds.
/// </summary>
internal static class SupervisorClimb
{
    /// <summary>
    /// The place whose supervisor answers <paramref name="subject"/> now: <paramref name="place"/> where nobody left it, the machine once a
    /// level below it did; null once the climb found no supervisor above, the subject the owner role's or ended as gave up.
    /// </summary>
    public static RolePlace? PlaceOf(DriverLoop loop, SupervisorSubject subject, RolePlace place)
    {
        if (!Climbs(subject.Kind) || loop.Pipe?.StandingConfirmation is not { } judge) return place;
        var leaves = new LineLeaves(loop.Home);
        var made = leaves.Keeps(subject) ? leaves.Of(subject) : [];
        return made.Count == 0 ? place : LineClimb.Place(LineOf(judge, subject.Kind, place), place, made);
    }

    /// <summary>
    /// The holder's leave on <paramref name="subject"/> at <paramref name="place"/> (as the kind places it): passed up and kept as <c>left</c>
    /// where it climbs, the kind's own final keeping otherwise; the leave is recorded either way, with the level that made it.
    /// </summary>
    public static async Task<SupervisorDecided> LeaveAsync(
        DriverLoop loop, SupervisorLeaveForm form, RolePlace place, Func<Task<SupervisorDecided>> final)
    {
        var subject = form.Subject;
        var leaves = new LineLeaves(loop.Home);
        if (!Climbs(subject.Kind) || !leaves.Keeps(subject) || loop.Pipe?.StandingConfirmation is not { } judge) return await final().ConfigureAwait(false);
        if (PlaceOf(loop, subject, place) is not { } now) return SupervisorDecided.Refused(SupervisorWhy.PassedUp);
        var role = judge.Supervising(subject.Kind, now).Role;
        var leave = new LineLeave(
            DateTimeOffset.UtcNow, role is null ? LineLevels.Machine : LineLevels.Of(role.Scope), role?.Id, form.Bound,
            string.Join(" ", form.Reasons.Select(reason => reason.Says)));
        var made = (IReadOnlyList<LineLeave>)[.. leaves.Of(subject), leave];
        if (LineClimb.Place(LineOf(judge, subject.Kind, place), place, made) is not { } above || above == now)
        {
            var kept = await final().ConfigureAwait(false);
            if (kept.Kept) leaves.Keep(subject, leave);
            return kept;
        }

        // Passed up: judged as the kind's own leave would be (its form, and the hold answering the subject where it waits now), then recorded.
        if (form.Reasons.Any(reason => string.IsNullOrWhiteSpace(reason.Source))) return SupervisorDecided.Refused(SupervisorWhy.NoSource);
        if (form.Reasons.Count == 0 || !SupervisorBounds.Known(form.Bound)) return SupervisorDecided.Refused(SupervisorWhy.Form);
        var admitted = judge.Admit(subject.Kind, now, act: null);
        if (admitted.Why is { } why) return SupervisorDecided.Refused(why);
        if (admitted.Generation != form.Generation) return SupervisorDecided.Refused(SupervisorWhy.RoleChanged);
        leaves.Keep(subject, leave);
        loop.Log?.Info("supervisor.passed-up", ("kind", subject.Kind), ("session", subject.Session), ("level", leave.Level),
            ("waits", LineClimb.Wait(LineOf(judge, subject.Kind, place), made).Said()));
        return new SupervisorDecided(true, LinePasses.Left, null, null);
    }

    // The vacancy's leaves climb by its own journal, and Ask Daoris's kind has no level below the machine. The allow and files kinds list
    // and release by their own places (a proposal names no session), so their leaves stay final until those read the line too.
    private static bool Climbs(string kind) => kind is not (SupervisorKinds.Vacancy or SupervisorKinds.AskDaoris or SupervisorKinds.Allow or SupervisorKinds.Files);

    private static ReportingLine LineOf(StandingConfirmation judge, string kind, RolePlace place) =>
        ReportingLine.Of(judge.SlotsNow(), kind, place, judge.Wider(), act: null, judge.Risk());
}
