using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The allow kind's fourth subtype, a working session's press asked of the person <c>{ press }</c>: listed with <c>allow.once</c> for the
/// supervisor that answers its session's place; allowed once through the pipe's pending entry (<see cref="SessionPressAllows"/>), or
/// left to the person's card.
/// </summary>
internal static class PressAllowSubjects
{
    public static IEnumerable<object> Waiting(DriverLoop loop)
    {
        if (loop.Pipe is not { } pipe) yield break;
        foreach (var press in pipe.SessionPresses.Waiting())
        {
            var admitted = pipe.StandingConfirmation.Admit(SupervisorKinds.Allow, press.Place, SessionPressAllows.Act);
            if (admitted.Why is not null || admitted.Generation is not { } generation) continue;
            yield return new
            {
                Kind = SupervisorKinds.Allow, Subject = SupervisorSubject.SessionPress(press.Id).Wire(), Repository = press.Place.Repository ?? "", Generation = generation,
                Acts = new[] { SessionPressAllows.Act }, Act = press.Route, Raised = Moments.Write(press.Raised),
                Facts = AllowKind.Facts(loop, SupervisorSubject.SessionPress(press.Id))?.Wire(),
            };
        }
    }

    public static SupervisorDecided Decide(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder)
    {
        if (form.Do != SessionPressAllows.Act) return SupervisorDecided.Refused(SupervisorWhy.NotOffered);
        if (loop.Pipe is not { } pipe) return SupervisorDecided.Refused(SupervisorWhy.NotStaffed);
        if (pipe.SessionPresses.Find(form.Subject.Id) is not { } press) return SupervisorDecided.Refused(SupervisorWhy.Answered);
        var admitted = pipe.StandingConfirmation.Admit(SupervisorKinds.Allow, press.Place, SessionPressAllows.Act);
        if (admitted.Why is { } why) return SupervisorDecided.Refused(why);
        if (admitted.Generation != form.Generation) return SupervisorDecided.Refused(SupervisorWhy.RoleChanged);
        var decision = Guid.NewGuid().ToString("N")[..12];
        void Keep(PendingPress held) => loop.Supervisor.AllowKept(new SupervisorAllowed(
            decision, SupervisorAllowShapes.Press, held.Id, held.Session, held.Place.Repository ?? "", SessionPressAllows.Act, admitted.Role, held.Route, holder, form.Reasons),
            DateTimeOffset.UtcNow);
        return pipe.SessionPresses.Allow(press.Id, Keep) is { } refused ? SupervisorDecided.Refused(refused) : new SupervisorDecided(true, decision, null, null);
    }

    public static SupervisorDecided Leave(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder)
    {
        if (loop.Pipe is not { } pipe || pipe.SessionPresses.Find(form.Subject.Id) is not { } press) return SupervisorDecided.Refused(SupervisorWhy.Answered);
        var role = pipe.StandingConfirmation.Admit(SupervisorKinds.Allow, press.Place, SessionPressAllows.Act).Role;
        if (!pipe.SessionPresses.Leave(press.Id)) return SupervisorDecided.Refused(SupervisorWhy.Answered);
        var decision = Guid.NewGuid().ToString("N")[..12];
        loop.Supervisor.AllowKept(new SupervisorAllowed(
            decision, SupervisorAllowShapes.Press, press.Id, press.Session, press.Place.Repository ?? "", SupervisorAllowActs.Leave, role, press.Route, holder, form.Reasons,
            Bound: form.Bound), DateTimeOffset.UtcNow);
        return new SupervisorDecided(true, decision, null, null);
    }
}
