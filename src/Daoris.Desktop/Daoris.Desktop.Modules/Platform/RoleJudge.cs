using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The judge's role method and the decisions' admission: a role's act judged against its
/// hold, its holder's proof and the catalogue as it stands, and the effective supervisor's hold answered. It reaches the standing
/// confirmation's memory (<see cref="StandingState"/>) under its one lock, the same lock and caller look a person's press is judged under;
/// a misconfigured holder's tab is closed and its generations told after the step.
/// </summary>
internal sealed class RoleJudge(StandingConfirmation owner, StandingState state)
{
    private readonly MachineLog _log = owner.Log;

    /// <summary>
    /// The decisions' admission: the effective supervisor's hold for <paramref name="repository"/>, its
    /// generation and its role's window, read under the lock for this synchronous answer alone; refused <c>not-staffed</c> where the
    /// supervisor there is held by the person's rules alone, or <c>not-started</c> where its terminal is not started this run.
    /// </summary>
    /// <remarks>
    /// <b>One effective supervisor per question</b>, as a work's role is found: the most specific scope's, a repository's own over its
    /// workspace's over the machine's, the workspace the one <see cref="StandingConfirmation.WorkspaceOf"/> places the repository in now.
    /// </remarks>
    public DecisionAdmitted Admit(string repository)
    {
        var effective = Supervising(repository);
        lock (state.Gate)
        {
            if (effective is not { Hold.Terminal: true }) return DecisionAdmitted.Refused(SupervisorWhy.NotStaffed);
            return state.HoldOf(effective.Id) is { } hold
                ? new DecisionAdmitted(hold.Generation, null) { Window = TimeSpan.FromMinutes(effective.Hold!.Window) }
                : DecisionAdmitted.Refused(SupervisorWhy.NotStarted);
        }
    }

    /// <summary>
    /// Whether the supervisor's hold of <paramref name="generation"/> is the effective one for <paramref name="repository"/>, which the
    /// holder's answer and defer check against the park's repository: null where it is; <c>role-changed</c> where it holds a supervisor
    /// that covers the repository but another is effective there (a repository's own made over its workspace's); <c>not-staffed</c>
    /// where it covers nothing there.
    /// </summary>
    public string? Covers(string generation, string repository)
    {
        var staffed = owner.Appointed();
        var place = new RolePlace(owner.WorkspaceOf(repository), repository);
        var effective = Supervising(repository);
        lock (state.Gate)
        {
            if (effective is { Hold.Terminal: true } && state.HoldOf(effective.Id) is { } live && live.Generation == generation) return null;
            var named = state.Holds.FirstOrDefault(each => each.Kind == AppointedRoles.Supervisor && each.Generation == generation);
            var covering = named is not null && staffed.Any(role => role.Id == named.Role && role.Scope.Covers(place));
            return covering ? SupervisorWhy.RoleChanged : SupervisorWhy.NotStaffed;
        }
    }

    // The supervisor that acts for a repository now: the most specific scope's that covers it; null where none.
    private RoleRecord? Supervising(string repository) => owner.Staffed(AppointedRoles.Supervisor, new RolePlace(owner.WorkspaceOf(repository), repository));

    /// <summary>
    /// The judge's role method, for every route of the census's <c>Role</c>
    /// class: the caller look, the first denial, so a contained agent is refused before its proof is read; the form; the hold its
    /// generation names, live, its role still held by the terminal as the catalogue says now; the holder's proof over this connection's
    /// unused <paramref name="nonce"/> and the form, against the holder's hash in memory; then <b>the configuration walk</b>, one guard for
    /// the whole class, which ends a holder it finds started on another configuration; and for <c>ROLE_ACT</c>, the act in
    /// its role's table and the role's <c>authority</c>, its own form exact, and the act admitted once, its id drawn here. Anything
    /// uncovered is refused, never asked.
    /// </summary>
    /// <param name="nonce">The connection's hello's nonce, taken for this act (each is used once), or null where it was used or expired.</param>
    public RoleJudgement JudgeRole(string module, string route, JsonElement? payload, PipeClientProcess? client, string? nonce)
    {
        RoleJudgement judged;
        StandingState.Holding? misconfigured = null;
        string? tab = null;
        IReadOnlyList<string> ended = [];
        var cancelled = 0;
        var staffed = owner.Appointed();
        lock (state.Gate)
        {
            judged = Judged(module, route, payload, client, nonce, staffed, out misconfigured);
            if (misconfigured is not null)
            {
                tab = misconfigured.Tab;
                (ended, cancelled) = state.EndHolder(misconfigured);
            }
        }

        if (misconfigured is not null)
        {
            // A holder that started on another configuration may have run what it loaded: it is ended, its tab closed.
            _log.Warn(StandingConfirmation.RoleLogEvent, ("state", RoleWhy.HolderConfig), ("count", StandingState.Count(ended.Count)));
            if (cancelled > 0) _log.Info("role.cancelled", ("count", StandingState.Count(cancelled)));
            if (tab is not null) owner.Close?.Invoke(tab);
            if (ended.Count > 0) owner.Ended?.Invoke(ended);
        }

        if (judged is { Covered: true, ActId: not null }) _log.Info("role.act", ("role", judged.Role ?? ""), ("act", judged.Act ?? ""), ("outcome", "admitted"));
        return judged;
    }

    // Under the lock, in the judge's order.
    private RoleJudgement Judged(
        string module, string route, JsonElement? payload, PipeClientProcess? client, string? nonce, IReadOnlyList<RoleRecord> staffed,
        out StandingState.Holding? misconfigured)
    {
        misconfigured = null;
        var roleAct = route == RoleRoutes.Act;
        if (!string.Equals(module, StandingConfirmation.DriverModuleName, StringComparison.OrdinalIgnoreCase) || !(roleAct || SupervisorRoutes.Acts.Contains(route)))
        {
            return RoleJudgement.Refused(StandingWhy.NotNamed);
        }

        var role = roleAct ? RoleActForm.RoleOf(payload) : AppointedRoles.Supervisor;
        if (role is null || !AppointedRoles.Kinds.Contains(role, StringComparer.Ordinal)) return RoleJudgement.Refused(SupervisorWhy.Form);

        var place = owner.Caller.Place(client);
        if (place == StandingPlace.InAgentJob) return RoleJudgement.Refused(StandingWhy.InAgentJob);
        if (place != StandingPlace.Outside) return RoleJudgement.Refused(StandingWhy.Untold);

        // The supervisor's answer or defer, each proved over its own route's canonical bytes; or ROLE_ACT's form.
        RoleActForm? act = null;
        string generation, proof;
        byte[] canonical;
        if (roleAct)
        {
            if (RoleActForm.Read(payload) is not { } read || read.Role != role) return RoleJudgement.Refused(SupervisorWhy.Form);
            (act, generation, proof, canonical) = (read, read.Generation, read.Proof, read.Canonical);
        }
        else
        {
            if (SupervisorActs.Read(route, payload) is not { } read) return RoleJudgement.Refused(SupervisorWhy.Form);
            (generation, proof, canonical) = (read.Generation, read.Proof, read.Canonical);
        }

        if (state.Holds.FirstOrDefault(each => each.Kind == role && each.Generation == generation) is not { } held)
        {
            // A role of this kind staffed by the terminal and not started this run says so; another hold of it is a changed role.
            return RoleJudgement.Refused(state.Holds.Any(each => each.Kind == role) ? SupervisorWhy.RoleChanged
                : staffed.Any(each => each.Hold is { Terminal: true } hold && hold.Kind == role) ? SupervisorWhy.NotStarted
                : SupervisorWhy.NotStaffed);
        }

        // The role as the catalogue says now: a hold whose role no longer reads as the terminal's is not the role's.
        if (staffed.FirstOrDefault(each => each.Id == held.Role) is not { Hold: { Terminal: true } grant } found || grant.Kind != held.Kind)
        {
            return RoleJudgement.Refused(SupervisorWhy.RoleChanged);
        }

        if (!state.Holders.TryGetValue(held.Holder, out var holding)) return RoleJudgement.Refused(SupervisorWhy.NotStarted);

        if (nonce is null || !SupervisorProof.Verify(holding.Hash, nonce, canonical, proof)) return RoleJudgement.Refused(SupervisorWhy.NotHolder);

        // One guard for every route of the class, never a list of routes.
        if (owner.Walk.Check(client, holding.Process) is not null)
        {
            misconfigured = holding;
            return RoleJudgement.Refused(RoleWhy.HolderConfig);
        }

        var roles = state.RolesOf(holding.Id);
        var repository = found.Scope.Kind == Daoris.Driver.RoleScope.Repository ? found.Scope.Id : null;
        if (act is null)
        {
            state.Answers[held.Generation] = state.Answers.GetValueOrDefault(held.Generation) + 1;
            return new RoleJudgement(true, null, held.Kind, repository, held.Generation, holding.Id, Roles: roles);
        }

        if (!owner.TableOf(role).TryGetValue(act.Act, out var row)) return RoleJudgement.Refused(RoleWhy.NotRole);
        // The desk's decline changes nothing of Daoris's controls, so every steward may make it.
        var desk = role == AppointedRoles.Steward && StewardActs.Desk.Contains(act.Act, StringComparer.Ordinal);
        if (!desk && !grant.Acts.Contains(act.Act, StringComparer.Ordinal)) return StewardAsk.Outside(row, act, holding.Id, roles);

        if (!row.IsForm(act.Form)) return RoleJudgement.Refused(SupervisorWhy.Form);

        // `owner-set`: what the owner acted on this run is theirs until Daoris restarts.
        var target = new RoleScope(row.TargetKind, row.Target(act.Form));
        if (state.OwnerHolds(target)) return RoleJudgement.Refused(RoleWhy.OwnerSet);

        // Admitted once, its id drawn here and registered as admitted; the lock is released as this returns.
        var admitted = state.Acts.Admit(role, act.Act, held.Generation, holding.Id, target.Name, target.Kind);
        state.Answers[held.Generation] = state.Answers.GetValueOrDefault(held.Generation) + 1;
        return new RoleJudgement(true, null, role, repository, held.Generation, holding.Id, act.Act, admitted, roles);
    }
}
