using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The work of an ask or a quest, the page's <c>bridge/work.ts</c> (PAUSE1b, PAUSE1d, D132 §7.3): what it holds and what a
/// pause and an abandon would do with each piece, a pause, a resume and an abandon. The driver library's
/// <see cref="WorkPausing"/> and <see cref="WorkAbandoning"/> are the one implementation, which <c>daoris-driver ask
/// --pause|--resume|--abandon</c> and <c>quest pause|resume|abandon</c> call too (D50).
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
    /// <summary>
    /// The work and what a pause and an abandon would do with each piece, why an abandon keeps what it keeps, and its own pause
    /// and last abandon here (design §1, §2.1, §3.2, §4.2). The abandon's half is the first press: <c>abandon.pieces</c> is
    /// what the second press sends back.
    /// </summary>
    [DriverRoute("WORK_PLAN")]
    private async Task<object?> WorkPlanAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (scope, id) = WorkNamed(request);
        var abandon = await WorkAbandoning.PlanAsync(WorkWorldOf(), scope, id, cancellationToken).ConfigureAwait(false)
                      ?? throw WorkUnknown(scope, id);
        var plan = abandon.Work;
        var quests = abandon.Quests.ToDictionary(each => each.Quest.Quest.Id, StringComparer.OrdinalIgnoreCase);
        var sessions = abandon.Sessions.ToDictionary(each => each.Session.Record.Id, StringComparer.Ordinal);
        return new
        {
            Scope = WorkPausing.Word(scope),
            plan.Pieces.Id,
            plan.Pausable,
            Paused = plan.Paused is { } paused
                ? new { paused.At, Stopped = paused.Stopped.Select(stop => new { Quest = stop.Key, Session = stop.Value }).ToArray() }
                : null,
            Quests = plan.Quests.Select(each =>
            {
                var gives = quests[each.Quest.Quest.Id];
                return new
                {
                    Quest = each.Quest.Quest.Id,
                    each.Quest.Quest.Title,
                    each.Quest.Quest.To,
                    each.Quest.Quest.Status,
                    Joined = each.Quest.Joined.ToString().ToLowerInvariant(),
                    each.Quest.By,
                    each.Pause,
                    PausedBy = each.PausedBy is { } by ? new { Scope = by.Word, by.Id } : null,
                    gives.Key,
                    Abandon = gives.Act,
                    Kept = gives.Why,
                    gives.Machine,
                    gives.WhileOpen,
                };
            }).ToArray(),
            Sessions = plan.Sessions.Select(each =>
            {
                var gives = sessions[each.Session.Record.Id];
                return new
                {
                    Session = each.Session.Record.Id,
                    each.Session.Record.Repository,
                    each.Session.Record.State,
                    each.Session.Record.Quest,
                    each.Session.Intake,
                    each.Session.Teammate,
                    each.Session.Branch,
                    each.Pause,
                    gives.Key,
                    Abandon = gives.Act,
                    gives.Archive,
                    Kept = gives.Why,
                    gives.Machine,
                };
            }).ToArray(),
            // Its repository, its branch, who worked in it and what the proof found: never a path, which is this machine's.
            Trees = abandon.Trees.Select(each => new
            {
                each.Tree.Repository,
                each.Tree.Branch,
                each.Tree.Sessions,
                each.Key,
                Abandon = each.Act,
                Kept = each.Why,
                Gone = !each.Judged.TreeHere,
                each.Judged.Tip,
                each.Judged.Commits,
                each.Judged.Uncommitted,
                each.Judged.Files,
                each.Judged.Where,
            }).ToArray(),
            Landings = plan.Pieces.Landings.Select(landing => new { landing.Session, landing.Repository, landing.Branch }).ToArray(),
            Abandon = new
            {
                abandon.Abandonable,
                abandon.Pieces,
                abandon.Closes,
                Abandoned = abandon.Abandoned is { } last ? Entry(last) : null,
            },
        };
    }

    /// <summary>
    /// Abandon the work (design §3.4): exactly the pieces the first press listed, each judged again, with the person's reason.
    /// What went, what stayed, how many changed since the list and each shared decline's answer come back as facts and words
    /// from fixed lists; an abandon with nothing listed is said, never refused (D48 §6).
    /// </summary>
    [DriverRoute("WORK_ABANDON")]
    private async Task<object?> WorkAbandonAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (scope, id) = WorkNamed(request);
        var reason = Optional(request, "reason");
        // The reason before anything is asked or written: each declined quest keeps it.
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw Refusals.Because(Refusals.WorkReason, WorkAbandoning.NeedsReason);
        }

        var pieces = Pieces(request);
        var outcome = await WorkAbandoning.AbandonAsync(WorkWorldOf(), scope, id, reason, pieces, PluginEvents.Screen, cancellationToken)
            .ConfigureAwait(false);
        if (outcome.Verdict == AbandonVerdict.Unknown) throw WorkUnknown(scope, id);
        _loop.Nudge();
        return new
        {
            Scope = WorkPausing.Word(scope),
            outcome.Id,
            Did = outcome.Verdict == AbandonVerdict.Abandoned ? "abandoned" : "nothing",
            outcome.Listed,
            outcome.Went,
            Changed = outcome.Changed.Select(Keep).ToArray(),
            Failed = outcome.Failed.Select(Keep).ToArray(),
            outcome.Joined,
            outcome.Declined,
            outcome.Closed,
            Stopped = outcome.Stopped.Select(stop => new { stop.Session, stop.Quest }).ToArray(),
            Discarded = outcome.Discarded.Select(Tree).ToArray(),
            outcome.Archived,
            Stayed = outcome.Stayed.Select(Keep).ToArray(),
            Declines = outcome.Declines.Select(decline => new { decline.Quest, decline.Answer }).ToArray(),
            outcome.StillPaused,
        };
    }

    /// <summary>The pieces the first press listed: a list of keys, which a second press must send.</summary>
    /// <exception cref="DriverException">No list: the driver's sentence, said verbatim.</exception>
    private static IReadOnlyList<string> Pieces(IpcRequest request) =>
        request.Payload is { ValueKind: System.Text.Json.JsonValueKind.Object } payload
        && payload.TryGetProperty("pieces", out var pieces) && pieces.ValueKind == System.Text.Json.JsonValueKind.Array
            ? [.. pieces.EnumerateArray().Where(piece => piece.ValueKind == System.Text.Json.JsonValueKind.String).Select(piece => piece.GetString()!)]
            : throw new DriverException("an abandon sends the pieces its list held: the first press's `pieces`, as they were listed.");

    /// <summary>A kept piece as the page reads it. What refused it goes only for a quest or the ask, whose words are the service's: git's may name a path.</summary>
    private static object Keep(AbandonKeep keep) => new
    {
        keep.Piece,
        keep.Why,
        keep.Machine,
        keep.Where,
        keep.Changed,
        Detail = keep.Piece.StartsWith("quest:", StringComparison.Ordinal) || keep.Piece.StartsWith("ask:", StringComparison.Ordinal)
            || keep.Piece.StartsWith("session:", StringComparison.Ordinal)
            ? keep.Detail
            : null,
    };

    private static object Tree(AbandonedTree tree) =>
        new { tree.Repository, tree.Branch, tree.Tip, tree.Commits, tree.Uncommitted, tree.Sessions, tree.Alone };

    /// <summary>The last abandon here, for the page's *What went* and *What stayed* (design §4.2).</summary>
    private static object Entry(AbandonEntry entry) => new
    {
        entry.At,
        entry.Door,
        entry.Reason,
        entry.Declined,
        entry.Closed,
        Trees = entry.Trees.Select(Tree).ToArray(),
        entry.Stopped,
        entry.Archived,
        Stayed = entry.Stayed.Select(Keep).ToArray(),
        Declines = entry.Declines.Select(decline => new { decline.Quest, decline.Answer }).ToArray(),
    };

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
            Holds = outcome.Holds?.Select(WorkHoldShown).ToArray(),
        };
    }

    /// <summary>
    /// One quest a resume leaves held, as the page receives it (design §2.4): the quest, the verdict and the driver's sentence,
    /// and beside them the facts the page says that sentence from in the reader's language (CARRY2d), by the tick's own names
    /// and shapes (<see cref="DriverLoop.TickConsideration"/>), so <c>sittingSentence</c> words a hold as it words a sitting
    /// quest. Each is null for every verdict but its own, which the bridge leaves out; the sentence stays, unchanged, for an
    /// older page. Public for the fast half's table.
    /// </summary>
    public static object WorkHoldShown(WorkHold hold) => new
    {
        hold.Quest,
        Verdict = hold.Verdict.ToString(),
        hold.Reason,
        // The person's stop by its session (SESSUX1d); its tree stays here.
        hold.HeldBy,
        // Another pause, an ask's or the quest's own (PAUSE1b).
        PausedBy = hold.PausedBy is { } pause ? new { Scope = pause.Word, pause.Id } : null,
        // A park by the count its sentence says (SESSUX1i).
        hold.Strikes,
        // A take that is not this machine's (CARRY2c): the machine and its session, a take made here, the session not carried on.
        TakenBy = hold.TakenBy is { } taken ? new { taken.Machine, taken.Session, taken.Here, taken.Last } : null,
    };

    /// <summary>The ask or the quest a work route names: exactly one of <c>ask</c> and <c>quest</c>.</summary>
    /// <exception cref="DriverException">Neither, or both: the driver's sentence, said verbatim.</exception>
    private static (WorkScope Scope, string Id) WorkNamed(IpcRequest request) =>
        (Optional(request, "ask"), Optional(request, "quest")) switch
        {
            ({ } ask, null) => (WorkScope.Ask, ask.Trim().TrimStart('#')),
            (null, { } quest) => (WorkScope.Quest, quest.Trim().TrimStart('#')),
            _ => throw new DriverException("a work is named by its ask or by its quest: one of them, never both."),
        };

    /// <summary>
    /// The loop's world for a pause or an abandon: its service, its home, its file and its own registry, which stops what it
    /// runs; and for an abandon's pass, the loop's own sync (SYNC6b), where the workspace has a remote in the map its passes read.
    /// </summary>
    private WorkWorld WorkWorldOf()
    {
        var service = _loop.Service ?? throw NotReady();
        return new WorkWorld(service, _loop.Home, _loop.ConfigPath, _loop.Processes)
        {
            Door = Door(DriverConfig.Load(_loop.ConfigPath)),
            Log = _loop.Log,
            Sync = async (workspace, ct) =>
            {
                if (!RemoteTarget.Load().ContainsKey(RemoteTarget.Workspace(workspace))) return null;
                // Before the loop's sync set is up there is no pass to run: the decline travels on the loop's first.
                return _loop.SyncNowAsync(workspace, ct) is { } pass
                    ? await pass.ConfigureAwait(false)
                    : new SyncReport("the driver's sync is not running yet; the next pass carries the decline");
            },
        };
    }

    /// <summary><c>WORK_UNKNOWN</c>, the quest's sentence through the catalogue's <c>context</c>.</summary>
    private static Exception WorkUnknown(WorkScope scope, string id) => scope == WorkScope.Ask
        ? Refusals.Because(Refusals.WorkUnknown, WorkPausing.Unknown(scope, id), ("id", id))
        : Refusals.Because(Refusals.WorkUnknown, WorkPausing.Unknown(scope, id), ("id", id), ("context", "quest"));
}
