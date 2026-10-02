using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The work of an ask or a quest, the page's <c>bridge/work.ts</c> (PAUSE1b, D132 §7.3): what it holds and what a pause
/// would do with each piece, a pause, and a resume. The driver library's <see cref="WorkPausing"/> is the one implementation,
/// which <c>daoris-driver ask --pause|--resume</c> and <c>quest pause|resume</c> call too (D50).
/// </summary>
/// <remarks>
/// <para><b>Nothing machine-local comes back.</b> A tree is named by its repository and branch, never its path, as the
/// platform language says of every page (§4); the ids and the words are the record's.</para>
///
/// <para><b>Each answer is facts and words from fixed lists</b> (<see cref="PauseAct"/>, <c>why</c>), so the page says them in
/// the reader's language; a still-held quest carries the planner's verdict beside its sentence, which the page translates by
/// the verdict, as it does a tick's (UX5 U27).</para>
/// </remarks>
public sealed partial class DriverModule
{
    /// <summary>The work and what a pause would do with each piece, and its own pause where it is paused here (design §1, §2.1).</summary>
    [DriverRoute("WORK_PLAN")]
    private async Task<object?> WorkPlanAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (scope, id) = WorkNamed(request);
        var plan = await WorkPausing.PlanAsync(WorkWorldOf(), scope, id, cancellationToken).ConfigureAwait(false)
                   ?? throw WorkUnknown(scope, id);
        return new
        {
            Scope = WorkPausing.Word(scope),
            plan.Pieces.Id,
            plan.Pausable,
            Paused = plan.Paused is { } paused
                ? new { paused.At, Stopped = paused.Stopped.Select(stop => new { Quest = stop.Key, Session = stop.Value }).ToArray() }
                : null,
            Quests = plan.Quests.Select(each => new
            {
                Quest = each.Quest.Quest.Id,
                each.Quest.Quest.Title,
                each.Quest.Quest.To,
                each.Quest.Quest.Status,
                Joined = each.Quest.Joined.ToString().ToLowerInvariant(),
                each.Quest.By,
                each.Pause,
                PausedBy = each.PausedBy is { } by ? new { Scope = by.Word, by.Id } : null,
            }).ToArray(),
            Sessions = plan.Sessions.Select(each => new
            {
                Session = each.Session.Record.Id,
                each.Session.Record.Repository,
                each.Session.Record.State,
                each.Session.Record.Quest,
                each.Session.Intake,
                each.Session.Teammate,
                each.Session.Branch,
                each.Pause,
            }).ToArray(),
            // Its repository, its branch and who worked in it: never the path, which is this machine's.
            Trees = plan.Pieces.Trees.Select(tree => new { tree.Repository, tree.Branch, tree.Sessions }).ToArray(),
            Landings = plan.Pieces.Landings.Select(landing => new { landing.Session, landing.Repository, landing.Branch }).ToArray(),
        };
    }

    /// <summary>
    /// Pause the work on this machine (design §2.1): what of it ran here stopped as the person's stop, nothing of it started
    /// until *Resume*. What it stopped and what it could not reach come back; a pause with nothing to hold is said, never
    /// refused (D48 §6).
    /// </summary>
    [DriverRoute("WORK_PAUSE")]
    private async Task<object?> WorkPauseAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (scope, id) = WorkNamed(request);
        var outcome = await WorkPausing.PauseAsync(WorkWorldOf(), scope, id, PluginEvents.Screen, cancellationToken).ConfigureAwait(false);
        if (outcome.Verdict == PauseVerdict.Unknown) throw WorkUnknown(scope, id);
        _loop.Nudge();
        return new
        {
            Scope = WorkPausing.Word(scope),
            outcome.Id,
            Did = outcome.Verdict == PauseVerdict.Paused ? "paused" : "nothing",
            outcome.Already,
            Stopped = outcome.Stopped.Select(stop => new { stop.Session, stop.Quest }).ToArray(),
            Kept = outcome.Kept.Select(kept => new { kept.Session, kept.Quest, kept.Why, kept.Machine }).ToArray(),
        };
    }

    /// <summary>
    /// Resume the work (design §2.4): the pause gone, each stop it made released, and what still holds each quest named by the
    /// planner's verdict. A resume of what is not paused is said, never refused (D48 §6).
    /// </summary>
    [DriverRoute("WORK_RESUME")]
    private async Task<object?> WorkResumeAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (scope, id) = WorkNamed(request);
        var outcome = await WorkPausing.ResumeAsync(WorkWorldOf(), scope, id, PluginEvents.Screen, cancellationToken).ConfigureAwait(false);
        if (outcome.Verdict == ResumeVerdict.Unknown) throw WorkUnknown(scope, id);
        _loop.Nudge();
        return new
        {
            Scope = WorkPausing.Word(scope),
            outcome.Id,
            Did = outcome.Verdict == ResumeVerdict.Resumed ? "resumed" : "not-paused",
            Released = outcome.Released.Select(stop => new { stop.Quest, stop.Session }).ToArray(),
            Holds = outcome.Holds?.Select(hold => new { hold.Quest, Verdict = hold.Verdict.ToString(), hold.Reason }).ToArray(),
        };
    }

    /// <summary>The ask or the quest a work route names: exactly one of <c>ask</c> and <c>quest</c>.</summary>
    /// <exception cref="DriverException">Neither, or both: the driver's sentence, said verbatim.</exception>
    private static (WorkScope Scope, string Id) WorkNamed(IpcRequest request) =>
        (Optional(request, "ask"), Optional(request, "quest")) switch
        {
            ({ } ask, null) => (WorkScope.Ask, ask.Trim().TrimStart('#')),
            (null, { } quest) => (WorkScope.Quest, quest.Trim().TrimStart('#')),
            _ => throw new DriverException("a work is named by its ask or by its quest: one of them, never both."),
        };

    /// <summary>The loop's world for a pause: its service, its home, its file and its own registry, which stops what it runs.</summary>
    private WorkWorld WorkWorldOf()
    {
        var service = _loop.Service ?? throw NotReady();
        return new WorkWorld(service, _loop.Home, _loop.ConfigPath, _loop.Processes)
        {
            Door = Door(DriverConfig.Load(_loop.ConfigPath)),
            Log = _loop.Log,
        };
    }

    /// <summary><c>WORK_UNKNOWN</c>, the quest's sentence through the catalogue's <c>context</c>.</summary>
    private static Exception WorkUnknown(WorkScope scope, string id) => scope == WorkScope.Ask
        ? Refusals.Because(Refusals.WorkUnknown, WorkPausing.Unknown(scope, id), ("id", id))
        : Refusals.Because(Refusals.WorkUnknown, WorkPausing.Unknown(scope, id), ("id", id), ("context", "quest"));
}
