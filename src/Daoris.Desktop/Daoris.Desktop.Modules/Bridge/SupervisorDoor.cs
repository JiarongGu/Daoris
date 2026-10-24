using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The supervisor's door as its routes share it (SUPERVISOR1a-ii, D208; the supervisor design §2.2, §3.1, §5): the holder's terminal's
/// start, the sweep a revoke runs after the judge's step, and the park as the holder's answer is kept on it. The routes are the person's
/// <c>SET_APPOINTMENT</c> (<see cref="PersonRoutes"/>) and the sessions' <c>SUPERVISOR_ANSWER</c> and <c>SUPERVISOR_WAITING</c>
/// (<see cref="SessionsRoutes"/>). Lands dark: the judge does not offer appointments in this build.
/// </summary>
public static class SupervisorDoor
{
    /// <summary>
    /// The holders' tabs of Daoris's own terminal panel (<see cref="TerminalModule.OpenHolderAsync"/>), handed by the application as it
    /// composes the window. Null opens none, and an appointment then answers <c>unstarted</c>. Never a console window of the system's
    /// (the owner's direction of 2026-10-24).
    /// </summary>
    public static IHolderPanel? Panel { get; set; }

    /// <summary>A test's stand-in for the panel's open: it starts nothing. Wins over <see cref="Panel"/> where set.</summary>
    public static Func<HolderOpen, Task<HolderOpened>>? Starter { get; set; }

    private static (DriverLoop Loop, InstancePipe Pipe)? _attached;

    /// <summary>
    /// The judge's tabs and sweep wired to this loop (STEWARDROLE1a-i): a holder's end, the walk's ending of a holder and a role added
    /// to a holder each reach the panel and the decisions through here. Made at the first appointment, before its tab opens.
    /// </summary>
    public static void Attach(DriverLoop loop, InstancePipe pipe)
    {
        var standing = pipe.StandingConfirmation;
        _attached = (loop, pipe);
        standing.Tell ??= (tab, line, title) => Panel?.Tell(tab, line, title);
        standing.Close ??= tab => Panel?.Close(tab);
        standing.Ended ??= generations => _ = SweepAsync(loop, generations, CancellationToken.None);
    }

    /// <summary>
    /// A holder's tab ended (D209 §2.2's <c>HolderEnded</c>), told by the terminal module on every path it ends by: the judge drops the
    /// holder and its appointments in one step, and the decisions under them are swept after it (<see cref="StandingConfirmation.Ended"/>).
    /// </summary>
    public static void HolderEnded(string tab)
    {
        if (_attached is { Pipe: var pipe }) pipe.StandingConfirmation.HolderEnded(tab);
    }

    /// <summary>
    /// The owner's appointment (§2.3; D209 §2.2, §2.3), made by the judge in one step, a new holder's tab opened with the key first or
    /// the role added to a living holder; then every decision kept under a generation the step ended is swept, each under its own
    /// decision's lock (§5).
    /// </summary>
    public static async Task<StandingChange> AppointAsync(DriverLoop loop, InstancePipe pipe, AppointRequest request, CancellationToken ct)
    {
        var home = loop.Home;
        Attach(loop, pipe);
        var change = await pipe.StandingConfirmation.AppointAsync(request, open =>
            Starter is { } stand ? stand(open)
            : Panel is { } panel ? panel.OpenAsync(home, open, ct)
            : Task.FromResult(new HolderOpened(null, null, StandingConfirmation.Unstarted, null))).ConfigureAwait(false);
        if (change.Ended.Count == 0 && change.Narrowed.Count == 0) return change.Answer;
        await SweepAsync(loop, change.Ended, ct, change.Narrowed).ConfigureAwait(false);
        return change.Answer;
    }

    /// <summary>
    /// Every decision kept under <paramref name="generations"/> swept, and each <paramref name="narrowed"/> workspace's generation's for
    /// its one repository (SUPERVISOR1e), each under its own decision's lock (§5), and the loop told.
    /// </summary>
    private static async Task SweepAsync(
        DriverLoop loop, IReadOnlyList<string> generations, CancellationToken ct, IReadOnlyList<(string Generation, string Repository)>? narrowed = null)
    {
        var service = loop.Service;
        SupervisorTakeBack takeBack = async (session, word, token) =>
            service is not null && (await service.TakeBackAsync(session, word, ct: token).ConfigureAwait(false)).Done;
        foreach (var generation in generations)
        {
            await loop.Supervisor.RevokedAsync(generation, takeBack, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        }

        foreach (var (generation, repository) in narrowed ?? [])
        {
            await loop.Supervisor.RevokedAsync(generation, takeBack, DateTimeOffset.UtcNow, ct, repository).ConfigureAwait(false);
        }

        // STEWARDROLE1b (§5.1a): the steward's releases the revoke cancelled are withdrawn from the driver's file, the owner's kept.
        if (loop.Pipe is { } pipe) RoleStarts.Sweep(pipe.StandingConfirmation, loop.ConfigPath);
        loop.Nudge();
    }

    /// <summary>
    /// <c>ROLE_ACT</c>'s handler (STEWARDROLE1a-i, D209 §3.3, §3.4 step 8), on the role's door alone: the act admitted under the judge's
    /// lock is checked once more, still admitted, its holder's hash present and its appointment live, and moved to running; only then
    /// does its table's handler run. Refused as data. Its answer names the holder's roles and the brief's revision (§2.2).
    /// </summary>
    /// <remarks>
    /// STEWARDROLE1a-ii (§3.4 step 8, §5.4): before that step the act's target is resolved to its dependencies (a quest's repository, a
    /// repository's workspace) and every one of their gates is taken alone in the fixed order; under them the target's records are read
    /// for a person's door's write this run (<see cref="OwnerRecords"/>), the step refuses an <c>owner-set</c> target, and the act's
    /// dependent conditions are judged again (<see cref="IRoleAct.UnderGatesAsync"/>). A dependency or a record that cannot be read
    /// refuses the act <c>unread</c>. The gates are held across the handler and released as it returns.
    /// </remarks>
    public static async Task<object?> ActAsync(
        InstancePipe? pipe, System.Text.Json.JsonElement? payload, CancellationToken ct, IOwnerScopes? scopes = null, IStewardWorld? world = null)
    {
        if (PipeDoor.Role is not { ActId: { } id, Role: { } role, Act: { } act } judged || pipe is null) return Acted(false, SupervisorWhy.NotHolder, null);
        var judge = pipe.StandingConfirmation;
        var target = judge.ActTarget(id);

        // STEWARDROLE1b (§5.4: a session resolved to its quest): a session's act is its quest's, its gates and `owner-set` the quest's;
        // a session that names no quest is no quest's own session, refused before any gate.
        if (target is { Kind: OwnerKinds.Session } session)
        {
            string? resolved;
            try
            {
                resolved = scopes is null ? null : await scopes.QuestOfSessionAsync(session.Name, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not (OperationCanceledException or OutOfMemoryException))
            {
                resolved = null;
            }

            if (resolved is null)
            {
                judge.ActDone(id);
                return Acted(false, StewardWhy.NotQuestSession, judged.Roles);
            }

            target = new RoleScope(OwnerKinds.Quest, resolved);
            judge.Retarget(id, target);
        }
        IReadOnlyList<RoleScope>? gates = target is null ? [] : await DependenciesAsync(target, scopes, ct).ConfigureAwait(false);
        if (gates is null)
        {
            judge.ActDone(id);
            return Acted(false, RoleWhy.Unread, judged.Roles);
        }

        await using var held = await judge.Gates.EnterAsync(gates, alone: true, ct).ConfigureAwait(false);
        if (target is { Kind: OwnerKinds.Quest } quest && scopes is not null)
        {
            bool? wrote;
            try
            {
                wrote = await scopes.PersonWroteAsync(quest.Name, judge.RunStarted, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not (OperationCanceledException or OutOfMemoryException))
            {
                wrote = null;
            }

            if (wrote is not false)
            {
                // §5.4's service doors mark by the record: a person's write on the quest this run makes it theirs, as a mark does.
                if (wrote is true) judge.MarkOwner(new OwnerResolved([quest], [], false), standing: false);
                judge.ActDone(id);
                return Acted(false, wrote is true ? RoleWhy.OwnerSet : RoleWhy.Unread, judged.Roles);
            }
        }

        if (judge.RunAct(id) is { } why) return Acted(false, why, judged.Roles);
        try
        {
            if (!judge.TableOf(role).TryGetValue(act, out var row) || RoleActForm.Read(payload) is not { } form)
            {
                return Acted(false, RoleWhy.NotRole, judged.Roles);
            }

            var run = new RoleActRun(id, role, act, form.Form, judge) { World = world };
            if (await row.UnderGatesAsync(run, ct).ConfigureAwait(false) is { } refused) return Acted(false, refused, judged.Roles);
            var answer = await row.RunAsync(run, ct).ConfigureAwait(false);
            return new { Done = true, Why = (string?)null, Answer = answer, Roles = judged.Roles ?? [], Brief = RolesBrief.Revision };
        }
        finally
        {
            judge.ActDone(id);
        }
    }

    /// <summary>
    /// An act's target and its dependencies (§5.4): a quest, its repository and that repository's workspace; a repository and its
    /// workspace. Null where one could not be read; a target with no scopes to read by is its own alone.
    /// </summary>
    public static async Task<IReadOnlyList<RoleScope>?> DependenciesAsync(RoleScope target, IOwnerScopes? scopes, CancellationToken ct)
    {
        // STEWARDROLE1c: a desk request is no repository's nor quest's, and depends on nothing.
        if (scopes is null || target.Kind == StewardDesk.Kind) return [target];
        try
        {
            var gates = new List<RoleScope> { target };
            var repository = target.Kind == OwnerKinds.Repository
                ? target.Name
                : await scopes.RepositoryOfQuestAsync(target.Name, ct).ConfigureAwait(false);
            if (repository is null) return target.Kind == OwnerKinds.Quest ? null : gates;
            gates.Add(new RoleScope(OwnerKinds.Repository, repository));
            if (await scopes.WorkspaceOfAsync(repository, ct).ConfigureAwait(false) is { Length: > 0 } workspace)
            {
                gates.Add(new RoleScope(OwnerKinds.Workspace, workspace));
            }

            return RoleScope.Order(gates);
        }
        catch (Exception error) when (error is not (OperationCanceledException or OutOfMemoryException))
        {
            return null;
        }
    }

    private static object Acted(bool done, string? why, IReadOnlyList<string>? roles) =>
        new { Done = done, Why = why, Answer = (object?)null, Roles = roles ?? [], Brief = RolesBrief.Revision };

    /// <summary>
    /// The holder's answer (§3.1), on the role's door alone: the session's repository the appointment's, the park read, and the decision
    /// kept in memory, held for the overrule window. Refused as data, keeping nothing.
    /// </summary>
    public static async Task<KeptAnswer> AnswerAsync(DriverLoop loop, System.Text.Json.JsonElement? payload, CancellationToken ct)
    {
        if (PipeDoor.Role is not { Generation: { } generation }) return Refused(SupervisorWhy.NotHolder);
        if (SupervisorForm.Read(payload) is not { } form) return Refused(SupervisorWhy.Form);
        if (form.Generation != generation) return Refused(SupervisorWhy.AppointmentChanged);
        var service = loop.Service ?? throw DriverArguments.NotReady();
        if (await ParkAsync(loop, service, form.Id, ct).ConfigureAwait(false) is not { } park) return Refused(SupervisorWhy.NotParked);
        if (Covers(loop, generation, park.Repository) is { } uncovered) return Refused(uncovered);

        var kept = await loop.Supervisor.KeepAsync(
            new SupervisorAnswer(form.Id, form.Question, form.Generation, form.Choice, form.Reasons, new SupervisorHolder(RoleHolders.Terminal, null)),
            park, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        if (!kept.Kept) return Refused(kept.Why ?? SupervisorWhy.NotParked);

        // The loop waits for the soonest decision's moment (§3.4's nudge): told of this one now.
        loop.Nudge();
        return new KeptAnswer(true, kept.Decision, kept.GoesOnAt is { } at ? Moments.Write(at) : null, null);
    }

    /// <summary>
    /// The holder's leaving a question to the owner (SUPERVISOR1b; §3.1 <c>SUPERVISOR_DEFER</c>), on the role's door alone as the answer
    /// is: kept as a decision of state <c>left</c>, final at once, nothing said to the session. Refused as data, keeping nothing.
    /// </summary>
    public static async Task<KeptAnswer> DeferAsync(DriverLoop loop, System.Text.Json.JsonElement? payload, CancellationToken ct)
    {
        if (PipeDoor.Role is not { Generation: { } generation }) return Refused(SupervisorWhy.NotHolder);
        if (SupervisorDeferForm.Read(payload) is not { } form) return Refused(SupervisorWhy.Form);
        if (form.Generation != generation) return Refused(SupervisorWhy.AppointmentChanged);
        var service = loop.Service ?? throw DriverArguments.NotReady();
        if (await ParkAsync(loop, service, form.Id, ct).ConfigureAwait(false) is not { } park) return Refused(SupervisorWhy.NotParked);
        if (Covers(loop, generation, park.Repository) is { } uncovered) return Refused(uncovered);

        var kept = await loop.Supervisor.LeaveAsync(
            new SupervisorDefer(form.Id, form.Question, form.Generation, form.Bound, form.Reasons, new SupervisorHolder(RoleHolders.Terminal, null)),
            park, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        return kept.Kept ? new KeptAnswer(true, kept.Decision, null, null) : Refused(kept.Why ?? SupervisorWhy.NotParked);
    }

    /// <summary>
    /// What waits for the holder (§3.1, §3.2): the parked sessions here with a recorded question and no decision on it, in a repository a
    /// live appointment names, each with that appointment's generation. Names no key.
    /// </summary>
    public static async Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, CancellationToken ct)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        var admission = (IDecisionAdmission?)loop.Pipe?.StandingConfirmation ?? NoDecisionAdmission.Instance;
        var waiting = new List<object>();
        foreach (var record in (await service.RecordsAsync(ct).ConfigureAwait(false)).Where(each => each.Parked && !each.Teammate))
        {
            if (record.Repository is not { } repository || admission.Admit(repository) is not { Why: null, Generation: { } generation }) continue;
            var asked = await Task.Run(() => new QuestionFiles(loop.Home).Read(record.Session), ct).ConfigureAwait(false);
            // A question its agent marked the owner's (SUPERVISOR1b, §4.1) is never the holder's to look at.
            if (asked is null || asked.Owner is not null || loop.Supervisor.Decided(record.Session, asked.Id)) continue;
            waiting.Add(new
            {
                Session = record.Session, Repository = repository, Question = asked.Id, Asked = Moments.Write(asked.At), Generation = generation,
            });
        }

        return waiting;
    }

    /// <summary>
    /// The park as the keeping reads it (§3.1's checks the driver makes): its repository, whether it is parked here and holds a quest, the
    /// question on file, whether a word of the owner's waits, whether its ask's go-aheads are open or cannot be read, and whether its line
    /// is at D188's answers-alone bound (SUPERVISOR1b). Null where no record of this machine has the id.
    /// </summary>
    public static async Task<SupervisedPark?> ParkAsync(DriverLoop loop, ServiceClient service, string id, CancellationToken ct)
    {
        if (await service.RecordAsync(id, ct).ConfigureAwait(false) is not { } record) return null;
        var filed = await Task.Run(() => new QuestionFiles(loop.Home).Read(id), ct).ConfigureAwait(false);
        var ownerWaits = record.Waiting.Any(word => word.Persons);
        var (open, unread) = (false, false);
        if (record.Quest is { } quest)
        {
            var asked = await service.FindQuestAsync(quest, ct).ConfigureAwait(false);
            if (AskWords.AskOf(asked?.From) is { } askId)
            {
                var ask = await service.FindAskAsync(askId, ct).ConfigureAwait(false);
                unread = ask?.GoAheads is null;
                open = ask?.GoAheads?.Any(each => each.Answer is null) == true;
            }
            else
            {
                unread = true;
            }
        }

        // SUPERVISOR1b (§4.3): D188's one count, read from this machine's events across the line's parts, as the look's hold reads it.
        var atBound = record.Parked && new AnswersAloneHold(loop.Events, new GoOnMarks(loop.Home))
            .AtBound(id, await AnswersAloneHold.RecordsAsync(service, ct).ConfigureAwait(false));
        return new SupervisedPark(record.Repository ?? "", record.Parked, record.Quest is not null, filed, ownerWaits, open, unread, atBound);
    }

    private static KeptAnswer Refused(string why) => new(false, null, null, why);

    // SUPERVISOR1e (§5): the act's appointment the park's repository's effective one, a workspace's included; why not, else null.
    private static string? Covers(DriverLoop loop, string generation, string repository) =>
        loop.Pipe?.StandingConfirmation is { } judge ? judge.Covers(generation, repository) : SupervisorWhy.NotAppointed;

    /// <summary>
    /// The answer's answer (§3.1): kept with the decision and when it goes on, or refused with why, keeping nothing; with the roles the
    /// holder holds now and the brief's revision (STEWARDROLE1a-i, D209 §2.2).
    /// </summary>
    public sealed record KeptAnswer(bool Kept, string? Decision, string? GoesOnAt, string? Why)
    {
        public IReadOnlyList<string> Roles { get; init; } = PipeDoor.Role?.Roles ?? [];

        public string Brief { get; init; } = RolesBrief.Revision;
    }
}

/// <summary>The panel's door for the holders' tabs (STEWARDROLE1a-i): open one, tell one a line, close one.</summary>
public interface IHolderPanel
{
    Task<HolderOpened> OpenAsync(string home, HolderOpen open, CancellationToken ct);

    bool Tell(string tab, string line, string? title);

    bool Close(string tab);
}

/// <summary>The terminal module as the holders' panel, found as the appointment is pressed.</summary>
public sealed class TerminalHolderPanel(Func<TerminalModule?> module) : IHolderPanel
{
    public Task<HolderOpened> OpenAsync(string home, HolderOpen open, CancellationToken ct) =>
        module() is { } terminals
            ? terminals.OpenHolderAsync(home, open, ct)
            : Task.FromResult(new HolderOpened(null, null, StandingConfirmation.Unstarted, null));

    public bool Tell(string tab, string line, string? title) => module()?.TellHolder(tab, line, title) == true;

    public bool Close(string tab) => module()?.CloseHolder(tab) == true;
}
