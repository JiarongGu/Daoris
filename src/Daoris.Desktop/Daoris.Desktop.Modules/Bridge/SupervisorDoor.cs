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
        if (change.Ended.Count == 0) return change.Answer;
        await SweepAsync(loop, change.Ended, ct).ConfigureAwait(false);
        return change.Answer;
    }

    /// <summary>Every decision kept under <paramref name="generations"/> swept, each under its own decision's lock (§5), and the loop told.</summary>
    private static async Task SweepAsync(DriverLoop loop, IReadOnlyList<string> generations, CancellationToken ct)
    {
        var service = loop.Service;
        foreach (var generation in generations)
        {
            await loop.Supervisor.RevokedAsync(
                generation,
                async (session, word, token) => service is not null && (await service.TakeBackAsync(session, word, ct: token).ConfigureAwait(false)).Done,
                DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        }

        loop.Nudge();
    }

    /// <summary>
    /// <c>ROLE_ACT</c>'s handler (STEWARDROLE1a-i, D209 §3.3, §3.4 step 8), on the role's door alone: the act admitted under the judge's
    /// lock is checked once more, still admitted, its holder's hash present and its appointment live, and moved to running; only then
    /// does its table's handler run. Refused as data. Its answer names the holder's roles and the brief's revision (§2.2).
    /// </summary>
    public static async Task<object?> ActAsync(InstancePipe? pipe, System.Text.Json.JsonElement? payload, CancellationToken ct)
    {
        if (PipeDoor.Role is not { ActId: { } id, Role: { } role, Act: { } act } judged || pipe is null) return Acted(false, SupervisorWhy.NotHolder, null);
        var judge = pipe.StandingConfirmation;
        if (judge.RunAct(id) is { } why) return Acted(false, why, judged.Roles);
        try
        {
            if (!judge.TableOf(role).TryGetValue(act, out var row) || RoleActForm.Read(payload) is not { } form)
            {
                return Acted(false, RoleWhy.NotRole, judged.Roles);
            }

            var answer = await row.RunAsync(new RoleActRun(id, role, act, form.Form, judge), ct).ConfigureAwait(false);
            return new { Done = true, Why = (string?)null, Answer = answer, Roles = judged.Roles ?? [], Brief = RolesBrief.Revision };
        }
        finally
        {
            judge.ActDone(id);
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
        if (PipeDoor.Role is not { Repository: { } appointed, Generation: { } generation }) return Refused(SupervisorWhy.NotHolder);
        if (SupervisorForm.Read(payload) is not { } form) return Refused(SupervisorWhy.Form);
        if (form.Generation != generation) return Refused(SupervisorWhy.AppointmentChanged);
        var service = loop.Service ?? throw DriverArguments.NotReady();
        if (await ParkAsync(loop, service, form.Id, ct).ConfigureAwait(false) is not { } park) return Refused(SupervisorWhy.NotParked);
        if (!string.Equals(park.Repository, appointed, StringComparison.OrdinalIgnoreCase)) return Refused(SupervisorWhy.NotAppointed);

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
        if (PipeDoor.Role is not { Repository: { } appointed, Generation: { } generation }) return Refused(SupervisorWhy.NotHolder);
        if (SupervisorDeferForm.Read(payload) is not { } form) return Refused(SupervisorWhy.Form);
        if (form.Generation != generation) return Refused(SupervisorWhy.AppointmentChanged);
        var service = loop.Service ?? throw DriverArguments.NotReady();
        if (await ParkAsync(loop, service, form.Id, ct).ConfigureAwait(false) is not { } park) return Refused(SupervisorWhy.NotParked);
        if (!string.Equals(park.Repository, appointed, StringComparison.OrdinalIgnoreCase)) return Refused(SupervisorWhy.NotAppointed);

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
