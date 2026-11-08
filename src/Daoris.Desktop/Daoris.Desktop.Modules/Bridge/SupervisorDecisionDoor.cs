using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The supervisor's one decision door: <c>SUPERVISOR_DECIDE</c> and <c>SUPERVISOR_LEAVE</c>, each naming its subject by kind and typed
/// reference, judged on the role's door alone (<see cref="PipeDoor.Role"/>), then handed to its kind's row, which reads the subject as it
/// stands and keeps the decision; and <c>SUPERVISOR_WAITING</c>, every kind's waiting subjects. The keeping's holder is the hold's.
/// </summary>
public static class SupervisorDecisionDoor
{
    private static readonly IReadOnlyList<ISupervisorKind> Rows = [new QuestionKind(), new CourseKind(), new LimitKind(), new AllowKind(), new LandingKind()];

    /// <summary>The kinds the door takes, each one row.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [.. Rows.Select(row => row.Kind)];

    /// <summary><c>SUPERVISOR_DECIDE</c>'s handler: kept with the decision and when it goes on, or refused as data, keeping nothing.</summary>
    public static async Task<SupervisorDecided> DecideAsync(DriverLoop loop, JsonElement? payload, CancellationToken ct)
    {
        if (PipeDoor.Role is not { Generation: { } generation } judged) return SupervisorDecided.Refused(SupervisorWhy.NotHolder);
        if (SupervisorDecideForm.Read(payload) is not { } form || RowOf(form.Kind) is not { } row) return SupervisorDecided.Refused(SupervisorWhy.Form);
        if (form.Generation != generation) return SupervisorDecided.Refused(SupervisorWhy.RoleChanged);
        return Nudged(loop, await row.DecideAsync(loop, form, HolderOf(judged), ct).ConfigureAwait(false));
    }

    /// <summary><c>SUPERVISOR_LEAVE</c>'s handler: kept as <c>left</c>, final at once, or refused as data, keeping nothing.</summary>
    public static async Task<SupervisorDecided> LeaveAsync(DriverLoop loop, JsonElement? payload, CancellationToken ct)
    {
        if (PipeDoor.Role is not { Generation: { } generation } judged) return SupervisorDecided.Refused(SupervisorWhy.NotHolder);
        if (SupervisorLeaveForm.Read(payload) is not { } form || RowOf(form.Kind) is not { } row) return SupervisorDecided.Refused(SupervisorWhy.Form);
        if (form.Generation != generation) return SupervisorDecided.Refused(SupervisorWhy.RoleChanged);
        return Nudged(loop, await row.LeaveAsync(loop, form, HolderOf(judged), ct).ConfigureAwait(false));
    }

    /// <summary>What waits for a terminal holder: every kind's subjects but an agent's, each with its generation and covered acts. Names no key.</summary>
    public static async Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, CancellationToken ct) =>
        [.. (await AllWaitingAsync(loop, ct).ConfigureAwait(false)).Where(each => !AgentHold.Is(AgentSupervisorDesk.Row(each)?.Generation))];

    internal static async Task<IReadOnlyList<object>> AllWaitingAsync(DriverLoop loop, CancellationToken ct)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        var admission = (IDecisionAdmission?)loop.Pipe?.StandingConfirmation ?? NoDecisionAdmission.Instance;
        var records = (await service.RecordsAsync(ct).ConfigureAwait(false)).ToList();
        var waiting = new List<object>();
        foreach (var row in Rows) waiting.AddRange(await row.WaitingAsync(loop, records, admission, ct).ConfigureAwait(false));
        return waiting;
    }

    /// <summary>
    /// An agent's decide or leave through its kind's row, its grant and hold read again first since no pipe judge stands before it.
    /// </summary>
    internal static async Task<SupervisorDecided> AgentAsync(
        DriverLoop loop, SupervisorSubject subject, string generation, string? act, SupervisorHolder holder, Func<ISupervisorKind, Task<SupervisorDecided>> keep)
    {
        if (RowOf(subject.Kind) is not { } row) return SupervisorDecided.Refused(SupervisorWhy.Form);
        if (loop.Pipe?.StandingConfirmation is not { } judge || loop.Service is not { } service) return SupervisorDecided.Refused(SupervisorWhy.NotStaffed);
        var place = await row.PlaceOfAsync(loop, subject, CancellationToken.None).ConfigureAwait(false);
        if (place is null)
        {
            if (await service.RecordAsync(subject.Session, CancellationToken.None).ConfigureAwait(false) is not { Repository: { } repository }) return SupervisorDecided.Refused(SupervisorWhy.NotParked);
            place = judge.Placed(repository);
        }

        var admitted = judge.Admit(subject.Kind, place.Value, act);
        if (admitted.Why is { } why) return SupervisorDecided.Refused(why);
        if (admitted.Generation != generation) return SupervisorDecided.Refused(SupervisorWhy.RoleChanged);
        return Nudged(loop, await keep(row).ConfigureAwait(false));
    }

    /// <summary>
    /// The hold that answers <paramref name="kind"/> at <paramref name="place"/>, and the kind's acts its grants cover there; null where
    /// no hold is admitted or it covers none, so a subject only the person may decide is never listed.
    /// </summary>
    internal static (string Generation, IReadOnlyList<string> Acts)? Covered(IDecisionAdmission admission, string kind, RolePlace place)
    {
        if (admission.Admit(kind, place, act: null) is not { Why: null, Generation: { } generation }) return null;
        var prefix = kind.Length + 1;
        IReadOnlyList<string> acts = [.. SupervisorKinds.ActsOf(kind).Select(act => act[prefix..])
            .Where(act => admission.Admit(kind, place, act) is { Why: null } admitted && admitted.Generation == generation)];
        return acts.Count == 0 ? null : (generation, acts);
    }

    /// <summary>
    /// Whether the hold of <paramref name="generation"/> is the one that answers <paramref name="kind"/> in <paramref name="repository"/>,
    /// a workspace's included: null where it is, else why not.
    /// </summary>
    internal static string? Covers(DriverLoop loop, string generation, string kind, string repository) =>
        loop.Pipe?.StandingConfirmation is { } judge ? judge.Covers(generation, kind, repository) : SupervisorWhy.NotStaffed;

    private static ISupervisorKind? RowOf(string kind) => Rows.FirstOrDefault(row => row.Kind == kind);

    // Who holds the role, as the judge read it from the catalogue's record of the hold.
    private static SupervisorHolder HolderOf(RoleJudgement judged) => new(judged.HolderKind ?? "", null);

    // The loop waits for the soonest decision's moment: told of a kept one now.
    private static SupervisorDecided Nudged(DriverLoop loop, SupervisorDecided decided)
    {
        if (decided.Kept) loop.Nudge();
        return decided;
    }
}
