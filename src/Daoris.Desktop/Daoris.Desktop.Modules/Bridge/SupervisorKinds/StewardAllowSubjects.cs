using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The allow kind's third subtype, a steward's act outside its grants <c>{ ask }</c>: listed with <c>allow.steward</c> for the supervisor
/// that answers its target's place, unless that supervisor's holder is the steward's own; allowed once through the pipe's one pending
/// entry (<see cref="StewardAllows"/>), or left to the person's card.
/// </summary>
internal static class StewardAllowSubjects
{
    public static IEnumerable<object> Waiting(DriverLoop loop)
    {
        if (loop.Pipe is not { } pipe) yield break;
        foreach (var ask in pipe.StewardAllows.Waiting())
        {
            var admitted = pipe.StandingConfirmation.AdmitStewardAllow(ask.Place, ask.Asked.Holder);
            if (admitted.Why is not null || admitted.Generation is not { } generation) continue;
            yield return new
            {
                Kind = SupervisorKinds.Allow, Subject = SupervisorSubject.StewardAsk(ask.Id).Wire(), Repository = ask.Place.Repository ?? "", Generation = generation,
                Acts = new[] { StewardAllowedAct.Act }, Act = ask.Asked.Act ?? "", Raised = Moments.Write(ask.Raised),
                Facts = AllowKind.Facts(loop, SupervisorSubject.StewardAsk(ask.Id))?.Wire(),
            };
        }
    }

    public static async Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder)
    {
        if (form.Do != StewardAllowedAct.Act) return SupervisorDecided.Refused(SupervisorWhy.NotOffered);
        if (loop.Pipe is not { } pipe) return SupervisorDecided.Refused(SupervisorWhy.NotStaffed);
        if (pipe.StewardAllows.Find(form.Subject.Id) is not { } ask) return SupervisorDecided.Refused(SupervisorWhy.Answered);
        var admitted = pipe.StandingConfirmation.AdmitStewardAllow(ask.Place, ask.Asked.Holder);
        if (admitted.Why is { } why) return SupervisorDecided.Refused(why);
        if (admitted.Generation != form.Generation) return SupervisorDecided.Refused(SupervisorWhy.RoleChanged);
        var decision = Guid.NewGuid().ToString("N")[..12];
        pipe.StewardAllows.Moved ??= (moved, to, why) => loop.Supervisor.AllowMoved(moved, to, DateTimeOffset.UtcNow, why);
        void Keep(PendingAllow held) => loop.Supervisor.AllowKept(new SupervisorAllowed(
            decision, SupervisorAllowShapes.Steward, held.Id, pipe.StewardAllows.TargetOf(held) is { Kind: OwnerKinds.Session } target ? target.Name : null,
            held.Place.Repository ?? "", StewardAllowedAct.Act, admitted.Role, held.Asked.Act, holder, form.Reasons, held.LandsAt), DateTimeOffset.UtcNow);
        return await pipe.StewardAllows.AllowAsync(ask.Id, decision, admitted.Window ?? TimeSpan.Zero, Keep).ConfigureAwait(false) is { } refused
            ? SupervisorDecided.Refused(refused)
            : new SupervisorDecided(true, decision, null, null);
    }

    public static SupervisorDecided Leave(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder)
    {
        if (loop.Pipe is not { } pipe || pipe.StewardAllows.Find(form.Subject.Id) is not { } ask) return SupervisorDecided.Refused(SupervisorWhy.Answered);
        // The record names the target and the role as they stand before the request falls to the person's card.
        var session = pipe.StewardAllows.TargetOf(ask) is { Kind: OwnerKinds.Session } target ? target.Name : null;
        var role = pipe.StandingConfirmation.AdmitStewardAllow(ask.Place, ask.Asked.Holder).Role;
        if (!pipe.StewardAllows.Leave(ask.Id)) return SupervisorDecided.Refused(SupervisorWhy.Answered);
        var decision = Guid.NewGuid().ToString("N")[..12];
        loop.Supervisor.AllowKept(new SupervisorAllowed(
            decision, SupervisorAllowShapes.Steward, ask.Id, session, ask.Place.Repository ?? "", SupervisorAllowActs.Leave, role, ask.Asked.Act, holder, form.Reasons,
            Bound: form.Bound), DateTimeOffset.UtcNow);
        return new SupervisorDecided(true, decision, null, null);
    }
}
