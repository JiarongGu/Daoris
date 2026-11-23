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
    private static readonly IReadOnlyList<ISupervisorKind> Rows = [new QuestionKind(), new CourseKind(), new LimitKind(), new AllowKind(), new FilesKind(), new LandingKind(), new MemoryKind(), new DepartureKind(), new GoAheadKind(), new VacancyKind(), new AskDaorisKind()];

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
        var holder = HolderOf(judged);
        if (await PlaceAsync(loop, row, form.Subject).ConfigureAwait(false) is not { } place) return Nudged(loop, await row.LeaveAsync(loop, form, holder, ct).ConfigureAwait(false));
        return Nudged(loop, await SupervisorClimb.LeaveAsync(loop, form, place, () => row.LeaveAsync(loop, form, holder, ct)).ConfigureAwait(false));
    }

    /// <summary>An agent's leave through its kind's row, climbing the line as a terminal's does, its hold read again first.</summary>
    internal static async Task<SupervisorDecided> AgentLeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct)
    {
        if (RowOf(form.Subject.Kind) is not { } row) return SupervisorDecided.Refused(SupervisorWhy.Form);
        if (await PlaceAsync(loop, row, form.Subject).ConfigureAwait(false) is not { } place) return SupervisorDecided.Refused(SupervisorWhy.NotParked);
        return await AgentAsync(loop, form.Subject, form.Generation, null, holder, kind =>
            SupervisorClimb.LeaveAsync(loop, form, place, () => kind.LeaveAsync(loop, form, holder, ct))).ConfigureAwait(false);
    }

    // The place a subject is answered at: its row's, else its session's repository as the judge places it; none where neither reads.
    private static async Task<RolePlace?> PlaceAsync(DriverLoop loop, ISupervisorKind row, SupervisorSubject subject)
    {
        if (await row.PlaceOfAsync(loop, subject, CancellationToken.None).ConfigureAwait(false) is { } place) return place;
        if (loop.Pipe?.StandingConfirmation is not { } judge || loop.Service is not { } service) return null;
        return await service.RecordAsync(subject.Session, CancellationToken.None).ConfigureAwait(false) is { Repository: { } repository } ? judge.Placed(repository) : null;
    }

    /// <summary>What waits for a terminal holder: every kind's subjects but an agent's, each with its generation and covered acts. Names no key.</summary>
    public static async Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, CancellationToken ct) =>
        [.. (await AllWaitingAsync(loop, ct).ConfigureAwait(false)).Where(each => !AgentHold.Is(AgentSupervisorDesk.Row(each)?.Generation))];

    internal static async Task<IReadOnlyList<object>> AllWaitingAsync(DriverLoop loop, CancellationToken ct)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        var admission = new PipeDecisionAdmission(loop);
        var records = (await service.RecordsAsync(ct).ConfigureAwait(false)).ToList();
        var waiting = new List<object>();
        foreach (var row in Rows) waiting.AddRange(await row.WaitingAsync(loop, records, admission, ct).ConfigureAwait(false));
        return waiting;
    }

    public static Task<string?> FactsAsync(DriverLoop loop, SupervisorWaiting waiting, string fence, CancellationToken ct) =>
        RowOf(waiting.Kind) is { } row ? row.FactsAsync(loop, waiting.Subject, fence, ct) : Task.FromResult<string?>(null);

    /// <summary>
    /// An agent's decide or leave through its kind's row, its grant and hold read again first since no pipe judge stands before it.
    /// </summary>
    internal static async Task<SupervisorDecided> AgentAsync(
        DriverLoop loop, SupervisorSubject subject, string generation, string? act, SupervisorHolder holder, Func<ISupervisorKind, Task<SupervisorDecided>> keep)
    {
        if (RowOf(subject.Kind) is not { } row) return SupervisorDecided.Refused(SupervisorWhy.Form);
        if (loop.Pipe?.StandingConfirmation is null || loop.Service is null) return SupervisorDecided.Refused(SupervisorWhy.NotStaffed);
        if (await PlaceAsync(loop, row, subject).ConfigureAwait(false) is not { } place) return SupervisorDecided.Refused(SupervisorWhy.NotParked);
        var admitted = new PipeDecisionAdmission(loop).Admit(subject, place, act);
        if (admitted.Why is { } why) return SupervisorDecided.Refused(why);
        if (admitted.Generation != generation) return SupervisorDecided.Refused(SupervisorWhy.RoleChanged);
        if (SupervisorOwnWork.RunOf(subject) is { } run && SupervisorOwnWork.Refused(holder, loop.RunWorkers(run)) is { } own) return SupervisorDecided.Refused(own);
        return Nudged(loop, await keep(row).ConfigureAwait(false));
    }

    /// <summary>
    /// The hold that answers <paramref name="kind"/> at <paramref name="place"/>, and the kind's acts its grants cover there; null where
    /// no hold is admitted or it covers none, so a subject only the person may decide is never listed.
    /// </summary>
    internal static (string Generation, IReadOnlyList<string> Acts)? Covered(IDecisionAdmission admission, string kind, RolePlace place) =>
        Covered((act) => admission.Admit(kind, place, act), kind);

    /// <summary>The hold that answers <paramref name="subject"/> where it waits on the line now (<see cref="SupervisorClimb"/>), and the acts it covers.</summary>
    internal static (string Generation, IReadOnlyList<string> Acts)? Covered(IDecisionAdmission admission, SupervisorSubject subject, RolePlace place) =>
        Covered((act) => admission.Admit(subject, place, act), subject.Kind);

    private static (string Generation, IReadOnlyList<string> Acts)? Covered(Func<string?, DecisionAdmitted> admit, string kind)
    {
        if (admit(null) is not { Why: null, Generation: { } generation }) return null;
        var prefix = kind.Length + 1;
        IReadOnlyList<string> acts = [.. SupervisorKinds.ActsOf(kind).Select(act => act[prefix..])
            .Where(act => admit(act) is { Why: null } admitted && admitted.Generation == generation)];
        return acts.Count == 0 ? null : (generation, acts);
    }

    /// <summary>
    /// Whether the hold of <paramref name="generation"/> is the one that answers <paramref name="kind"/> in <paramref name="repository"/>,
    /// a workspace's included: null where it is, else why not.
    /// </summary>
    internal static string? Covers(DriverLoop loop, string generation, string kind, string repository) =>
        loop.Pipe?.StandingConfirmation is { } judge ? judge.Covers(generation, kind, repository) : SupervisorWhy.NotStaffed;

    /// <summary>
    /// Whether the hold of <paramref name="generation"/> answers <paramref name="subject"/> in <paramref name="repository"/> where it waits on
    /// the line now: the repository's own judgement until its supervisor left the subject, then the machine's.
    /// </summary>
    internal static string? Covers(DriverLoop loop, string generation, SupervisorSubject subject, string repository)
    {
        if (loop.Pipe?.StandingConfirmation is not { } judge) return SupervisorWhy.NotStaffed;
        var place = judge.Placed(repository);
        return SupervisorClimb.PlaceOf(loop, subject, place) switch
        {
            null => SupervisorWhy.PassedUp,
            var now when now == place => judge.Covers(generation, subject.Kind, repository),
            var now => CoversPlace(loop, generation, subject.Kind, now.Value),
        };
    }

    /// <summary>Whether the hold of <paramref name="generation"/> answers <paramref name="kind"/> at any <paramref name="place"/>, asking no grant; null where it does, else why not.</summary>
    internal static string? CoversPlace(DriverLoop loop, string generation, string kind, RolePlace place)
    {
        if (loop.Pipe?.StandingConfirmation is not { } judge) return SupervisorWhy.NotStaffed;
        var admitted = judge.Admit(kind, place, act: null);
        return admitted.Why ?? (admitted.Generation == generation ? null : SupervisorWhy.RoleChanged);
    }

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
