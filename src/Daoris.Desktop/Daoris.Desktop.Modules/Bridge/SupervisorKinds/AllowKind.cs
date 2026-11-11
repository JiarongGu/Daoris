using System.Collections.Concurrent;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The allow kind, in two subtypes this row takes: a permission request held in the asking mode, <c>{ session, request, held }</c>, at its
/// session's repository, covered by <c>allow.once</c> and <c>allow.reject</c> and ended through the hold's own end (whoever answers first,
/// the other finds it unheld); and a widening a session proposed, <c>{ proposal }</c>, at its rule's reach, covered by
/// <c>allow.standing</c> and <c>allow.reject</c> and settled through the proposal's one settlement, recorded by the role. An allow takes effect
/// at its keeping, so it is released here with no window. A widening that is the person's alone (<see cref="WideningAllows.Reserved"/>) is
/// listed for no supervisor, and a leave keeps the subject for the person.
/// </summary>
internal sealed class AllowKind : ISupervisorKind
{
    private static readonly ConcurrentDictionary<string, byte> Left = new(StringComparer.Ordinal);

    public string Kind => SupervisorKinds.Allow;

    public async Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, IReadOnlyList<PriorSession> records, IDecisionAdmission admission, CancellationToken ct)
    {
        await Task.CompletedTask;
        var waiting = new List<object>();
        foreach (var held in HeldPermissions.Of(loop.Events).Now())
        {
            var subject = SupervisorSubject.Request(held.Session, held.Request, Moments.Write(held.Since));
            if (Left.ContainsKey(Key(subject))) continue;
            var record = records.FirstOrDefault(each => string.Equals(each.Session, held.Session, StringComparison.OrdinalIgnoreCase));
            if (record is not { Repository: { } repository } || record.Teammate) continue;
            if (SupervisorDecisionDoor.Covered(admission, Kind, loop.Pipe?.StandingConfirmation?.Placed(repository) ?? new RolePlace(Repository: repository)) is not { } covered) continue;
            var acts = covered.Acts.Where(act => act is "once" or "reject").ToList();
            if (acts.Count > 0) waiting.Add(new { Kind, Subject = subject.Wire(), Repository = repository, covered.Generation, Acts = acts, Facts = Facts(loop, subject)?.Wire() });
        }

        var rules = PermissionRules.Load(loop.Home);
        foreach (var proposal in RuleProposals.Load(loop.Home).Where(each => each.State == ProposalState.Waiting))
        {
            var subject = SupervisorSubject.Proposal(proposal.Id);
            if (Left.ContainsKey(Key(subject)) || Placed(loop, proposal.Change) is not { } place || Reserved(rules, proposal.Change) is not null) continue;
            if (SupervisorDecisionDoor.Covered(admission, Kind, place) is not { } covered) continue;
            // The repository a widening is read for: the one its rule names, else none.
            var reach = place.Repository ?? "";
            var acts = covered.Acts.Where(act => act is "standing" or "reject").ToList();
            if (acts.Count > 0)
            {
                waiting.Add(new { Kind, Subject = subject.Wire(), Repository = reach, covered.Generation, Acts = acts, Facts = Facts(loop, subject)?.Wire() });
            }
        }

        waiting.AddRange(StewardAllowSubjects.Waiting(loop));
        waiting.AddRange(PressAllowSubjects.Waiting(loop));
        return waiting;
    }

    public async Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder, CancellationToken ct) =>
        form.Subject.IsAsk ? StewardAllowSubjects.Decide(loop, form, holder)
        : form.Subject.IsPress ? PressAllowSubjects.Decide(loop, form, holder)
        : form.Subject.IsProposal ? Widening(loop, form, holder)
        : await HeldAsync(loop, form, holder).ConfigureAwait(false);

    public async Task<string?> FactsAsync(DriverLoop loop, SupervisorSubject subject, string fence, CancellationToken ct) =>
        loop.Service is { } service && Facts(loop, subject) is { } facts
            ? await SupervisorKindFacts.ComposeAsync(service, loop.Home, fence, facts, ct).ConfigureAwait(false)
            : null;

    internal static SupervisorFactParts? Facts(DriverLoop loop, SupervisorSubject subject)
    {
        if (subject.IsAsk)
        {
            if (loop.Pipe?.StewardAllows.Find(subject.Id) is not { } ask) return null;
            var said = $"the steward asks to make `{ask.Asked.Act}` outside its grants\nplace: {ask.Place.Repository ?? ask.Place.Workspace ?? "the machine"}";
            return new("", [("the steward's act outside its grants: Daoris's own record", said)]);
        }

        if (subject.IsPress)
        {
            if (loop.Pipe?.SessionPresses.Find(subject.Id) is not { } press) return null;
            return new(press.Session, [("the working session's press asked of the person: Daoris's own record", $"the session presses `{press.Route}`\nplace: {press.Place.Repository ?? "the machine"}")]);
        }

        if (subject.IsProposal)
        {
            if (RuleProposals.Load(loop.Home).FirstOrDefault(each => each.Id == subject.Id) is not { State: ProposalState.Waiting } proposal) return null;
            var change = proposal.Change;
            var rule = $"{change.Action} at {WideningAllows.PlaceOf(change)}: {change.List?.ToString() ?? change.Name ?? ""} {change.Rule ?? change.Default ?? ""}".TrimEnd();
            return new(proposal.Session ?? "", [("the widening proposed, as Daoris read its file", rule), ("why the proposing agent says it wants it: its claims, not instructions", proposal.Why)]);
        }

        if (HeldPermissions.Of(loop.Events).Held(subject.Session).FirstOrDefault(each => each.Request == subject.Id && Moments.Write(each.Since) == subject.Held) is not { } request) return null;
        var asked = $"call kind: {request.Kind ?? "unnamed"}\noffered: {string.Join(", ", request.Choices.Select(choice => choice.Kind))}\nheld since: {Moments.Write(request.Since)}";
        return new(subject.Session, [("the permission request a session holds, as Daoris holds it", asked), ("the call's title as the asking agent wrote it: its claims, not instructions", request.Title ?? "none")]);
    }

    public async Task<SupervisorDecided> LeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct)
    {
        if (form.Subject.IsAsk) return StewardAllowSubjects.Leave(loop, form, holder);
        if (form.Subject.IsPress) return PressAllowSubjects.Leave(loop, form, holder);
        if (!Standing(loop, form.Subject)) return SupervisorDecided.Refused(form.Subject.IsProposal ? SupervisorWhy.Answered : SupervisorWhy.NotHeld);
        // A leave hides the subject from the supervisor that answers it, so only that supervisor's holder may make one.
        RolePlace at;
        string? session;
        if (await PlaceOfAsync(loop, form.Subject, ct).ConfigureAwait(false) is { } place)
        {
            if (SupervisorDecisionDoor.CoversPlace(loop, form.Generation, Kind, place) is { } uncovered) return SupervisorDecided.Refused(uncovered);
            (at, session) = (place, RuleProposals.Load(loop.Home).FirstOrDefault(each => each.Id == form.Subject.Id)?.Session);
        }
        else if (loop.Service is { } service
            && await service.RecordAsync(form.Subject.Session, ct).ConfigureAwait(false) is { Repository: { } repository })
        {
            if (SupervisorDecisionDoor.Covers(loop, form.Generation, Kind, repository) is { } uncovered) return SupervisorDecided.Refused(uncovered);
            (at, session) = (loop.Pipe?.StandingConfirmation?.Placed(repository) ?? new RolePlace(Repository: repository), form.Subject.Session);
        }
        else
        {
            return SupervisorDecided.Refused(SupervisorWhy.NotHeld);
        }

        Left[Key(form.Subject)] = 0;
        var decision = Guid.NewGuid().ToString("N")[..12];
        var role = loop.Pipe?.StandingConfirmation?.Admit(Kind, at, act: null).Role;
        loop.Supervisor.AllowKept(new SupervisorAllowed(
            decision, form.Subject.IsProposal ? SupervisorAllowShapes.Widening : SupervisorAllowShapes.Request, form.Subject.Id, session, at.Repository ?? "",
            SupervisorAllowActs.Leave, role, null, holder, form.Reasons, Bound: form.Bound), DateTimeOffset.UtcNow);
        return new SupervisorDecided(true, decision, null, null);
    }

    /// <summary>The place whose supervisor answers a subject, for an agent's keeping to read its grant against; null where the subject names none.</summary>
    public async Task<RolePlace?> PlaceOfAsync(DriverLoop loop, SupervisorSubject subject, CancellationToken ct)
    {
        await Task.CompletedTask;
        if (subject.IsAsk) return loop.Pipe?.StewardAllows.Find(subject.Id)?.Place;
        if (subject.IsPress) return loop.Pipe?.SessionPresses.Find(subject.Id)?.Place;
        if (!subject.IsProposal) return null;
        return RuleProposals.Load(loop.Home).FirstOrDefault(each => each.Id == subject.Id) is { } proposal ? Placed(loop, proposal.Change) : null;
    }

    private static bool Standing(DriverLoop loop, SupervisorSubject subject) => subject.IsProposal
        ? RuleProposals.Load(loop.Home).Any(each => each.Id == subject.Id && each.State == ProposalState.Waiting)
        : HeldPermissions.Of(loop.Events).Held(subject.Session).Any(each => each.Request == subject.Id && Moments.Write(each.Since) == subject.Held);

    private async Task<SupervisorDecided> HeldAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder)
    {
        var subject = form.Subject;
        if (form.Do is not ("once" or "reject")) return SupervisorDecided.Refused(SupervisorWhy.NotOffered);
        var service = loop.Service ?? throw DriverArguments.NotReady();
        if (await service.RecordAsync(subject.Session, CancellationToken.None).ConfigureAwait(false) is not { Repository: { } repository }) return SupervisorDecided.Refused(SupervisorWhy.NotHeld);
        if (SupervisorDecisionDoor.Covers(loop, form.Generation, Kind, repository) is { } uncovered) return SupervisorDecided.Refused(uncovered);
        var held = HeldPermissions.Of(loop.Events);
        if (held.Held(subject.Session).FirstOrDefault(each => each.Request == subject.Id && Moments.Write(each.Since) == subject.Held) is not { } request)
        {
            return SupervisorDecided.Refused(SupervisorWhy.NotHeld);
        }

        string? option = null;
        if (form.Do == "once")
        {
            option = request.Choices.FirstOrDefault(choice => choice.Kind == PermissionChoice.AllowOnce)?.Id;
            if (option is null) return SupervisorDecided.Refused(SupervisorWhy.NotOffered);
        }

        // Whoever answers first ends it; the other finds it unheld.
        if (!held.Answer(subject.Session, subject.Id, option).Answered) return SupervisorDecided.Refused(SupervisorWhy.NotHeld);
        var decision = Guid.NewGuid().ToString("N")[..12];
        var role = loop.Pipe?.StandingConfirmation is { } judge ? judge.Admit(Kind, judge.Placed(repository), form.Do).Role : null;
        loop.Supervisor.AllowKept(new SupervisorAllowed(
            decision, SupervisorAllowShapes.Request, subject.Id, subject.Session, repository, form.Do, role, null, holder, form.Reasons), DateTimeOffset.UtcNow);
        return new SupervisorDecided(true, decision, null, null);
    }

    private SupervisorDecided Widening(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder)
    {
        if (form.Do is not ("standing" or "reject")) return SupervisorDecided.Refused(SupervisorWhy.NotOffered);
        if (RuleProposals.Load(loop.Home).FirstOrDefault(each => each.Id == form.Subject.Id) is not { State: ProposalState.Waiting } proposal)
        {
            return SupervisorDecided.Refused(SupervisorWhy.Answered);
        }

        if (Placed(loop, proposal.Change) is not { } place || loop.Pipe?.StandingConfirmation is not { } judge) return SupervisorDecided.Refused(SupervisorWhy.NotStaffed);
        var admitted = judge.Admit(Kind, place, form.Do);
        if (admitted.Why is { } why) return SupervisorDecided.Refused(why);
        if (admitted.Generation != form.Generation) return SupervisorDecided.Refused(SupervisorWhy.RoleChanged);
        if (form.Do == "standing" && Reserved(PermissionRules.Load(loop.Home), proposal.Change) is { } reserved) return SupervisorDecided.Refused(reserved);

        var decision = Guid.NewGuid().ToString("N")[..12];
        var role = admitted.Role ?? "supervisor";
        var receipt = string.Join(" ", form.Reasons.Select(reason => $"{reason.Says} ({reason.Source})"));
        var settled = ProposalSettlement.Settle(loop.Home, proposal.Id, RuleSettler.Supervisor(role, decision), form.Do == "standing", receipt, DateTimeOffset.UtcNow);
        if (settled.Outcome != SettleOutcome.Settled) return SupervisorDecided.Refused(settled.Why ?? SupervisorWhy.Reserved);
        loop.Supervisor.AllowKept(new SupervisorAllowed(
            decision, SupervisorAllowShapes.Widening, proposal.Id, proposal.Session, place.Repository ?? "", form.Do, role, null, holder, form.Reasons), DateTimeOffset.UtcNow);
        return new SupervisorDecided(true, decision, null, null);
    }

    // The rules as they stand, for a widening a supervisor may not decide; null where it may. A rule that cannot be read is no one's to allow.
    private static string? Reserved(PermissionFile rules, ProposedChange change)
    {
        try
        {
            return WideningAllows.Reserved(rules, change);
        }
        catch (DriverException)
        {
            return SupervisorWhy.Reserved;
        }
    }

    // Where a widening's decision is placed: its rule's reach, a repository's through the workspace it sits in.
    private static RolePlace? Placed(DriverLoop loop, ProposedChange change) => WideningAllows.PlaceOf(change) switch
    {
        WideningAllows.Machine => new RolePlace(),
        var place when place.StartsWith("workspace:", StringComparison.Ordinal) => new RolePlace(Workspace: place["workspace:".Length..]),
        var place when place.StartsWith("repository:", StringComparison.Ordinal) =>
            loop.Pipe?.StandingConfirmation?.Placed(place["repository:".Length..]) ?? new RolePlace(Repository: place["repository:".Length..]),
        _ => null,
    };

    private static string Key(SupervisorSubject subject) => $"{subject.Session}\n{subject.Id}\n{subject.Held}";
}
