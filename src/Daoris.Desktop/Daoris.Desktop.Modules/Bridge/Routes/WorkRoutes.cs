using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// The work of an ask or a quest, the page's <c>bridge/work.ts</c>: what it holds and what a
/// pause and an abandon would do with each piece, a pause, a resume and an abandon. The driver library's
/// <see cref="WorkPausing"/> and <see cref="WorkAbandoning"/> are the one implementation, which <c>daoris ask
/// --pause|--resume|--abandon</c> and <c>quest pause|resume|abandon</c> call too.
/// </summary>
/// <remarks>
/// <para><b>Nothing machine-local comes back.</b> A tree is named by its repository and branch, never its path, as the
/// platform language says of every page; the ids and the words are the record's.</para>
///
/// <para><b>Each answer is facts and words from fixed lists</b> (<see cref="PauseAct"/>, <c>why</c>), so the page says them in
/// the reader's language; a still-held quest carries the planner's verdict beside its sentence, which the page translates by
/// the verdict, as it does a tick's.</para>
/// </remarks>
public sealed class WorkRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>
    /// What a move on a work changes: its quests, the plan of the work among them, its ask, the sessions it
    /// stopped, released or archived, and the driver's state its loop, nudged, reads again. About no one session.
    /// </summary>
    private const StateScopes Moves = StateScopes.Sessions | StateScopes.Quests | StateScopes.Asks | StateScopes.Driver;

    /// <summary>
    /// The work and what a pause and an abandon would do with each piece, why an abandon keeps what it keeps, and its own pause
    /// and last abandon here. The abandon's half is the first press: <c>abandon.pieces</c> is
    /// what the second press sends back.
    /// </summary>
    [DriverRoute("WORK_PLAN", PipeClass.Open)]
    private async Task<object?> WorkPlanAsync(WorkPlanRead press, CancellationToken cancellationToken)
    {
        var (scope, id) = WorkNamed(press.Ask, press.Quest);
        // `ask --abandon <id>` and `quest abandon <id>`, the abandon's list in the verb's words.
        if (press.Said) return await SaidAsync(new WorkAsk(scope, "abandon", id), cancellationToken).ConfigureAwait(false);
        var abandon = await WorkAbandoning.PlanAsync(WorldOf(_context), scope, id, cancellationToken).ConfigureAwait(false)
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
    /// Abandon the work: exactly the pieces the first press listed, each judged again, with the person's reason.
    /// What went, what stayed, how many changed since the list and each shared decline's answer come back as facts and words
    /// from fixed lists; an abandon with nothing listed is said, never refused.
    /// </summary>
    [DriverRoute("WORK_ABANDON", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("WORK_ABANDON", Ask = "ask", Quest = "quest")]
    [GroundAsk("WORK_ABANDON", "ask")]
    [GroundQuest("WORK_ABANDON", "quest")]
    [GroundRead("WORK_ABANDON", nameof(PipeReads.WorkToAbandon))]
    [Publishes("WORK_ABANDON", Moves)]
    private async Task<object?> WorkAbandonAsync(WorkAbandonPress press, CancellationToken cancellationToken)
    {
        var (scope, id) = WorkNamed(press.Ask, press.Quest);
        var reason = press.Reason;
        // The terminal's `--yes`, which abandons what the list holds now, as the verb does alone; its words say a missing reason.
        if (press.Said)
        {
            return await SaidAsync(new WorkAsk(scope, "abandon", id) { Reason = reason, Yes = true }, cancellationToken).ConfigureAwait(false);
        }

        // The reason before anything is asked or written: each declined quest keeps it.
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw Refusals.Because(Refusals.WorkReason, WorkAbandoning.NeedsReason);
        }

        var pieces = press.Pieces
            ?? throw new DriverException("an abandon sends the pieces its list held: the first press's `pieces`, as they were listed.");
        var outcome = await WorkAbandoning.AbandonAsync(WorldOf(_context), scope, id, reason, pieces, PluginEvents.Screen, cancellationToken)
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

    /// <summary>The last abandon here, for the page's *What went* and *What stayed*.</summary>
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
    /// Pause the work on this machine: what of it ran here stopped as the person's stop, nothing of it started
    /// until *Resume*. What it stopped and what it could not reach come back; a pause with nothing to hold is said, never
    /// refused.
    /// </summary>
    [DriverRoute("WORK_PAUSE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("WORK_PAUSE", Ask = "ask", Quest = "quest")]
    [GroundAsk("WORK_PAUSE", "ask")]
    [GroundQuest("WORK_PAUSE", "quest")]
    [GroundRead("WORK_PAUSE", nameof(PipeReads.WorkToPause))]
    [Publishes("WORK_PAUSE", Moves)]
    private async Task<object?> WorkPauseAsync(WorkPausePress press, CancellationToken cancellationToken)
    {
        var (scope, id) = WorkNamed(press.Ask, press.Quest);
        if (press.Said) return await SaidAsync(new WorkAsk(scope, "pause", id), cancellationToken).ConfigureAwait(false);
        var outcome = await WorkPausing.PauseAsync(WorldOf(_context), scope, id, PluginEvents.Screen, cancellationToken).ConfigureAwait(false);
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
    /// Resume the work: the pause gone, each stop it made released, and what still holds each quest named by the
    /// planner's verdict. A resume of what is not paused is said, never refused.
    /// </summary>
    [DriverRoute("WORK_RESUME", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("WORK_RESUME", Ask = "ask", Quest = "quest")]
    [GroundAsk("WORK_RESUME", "ask")]
    [GroundQuest("WORK_RESUME", "quest")]
    [GroundRead("WORK_RESUME", nameof(PipeReads.PauseToResume))]
    [Says("WORK_RESUME", "quest:quest", "ask:ask")]
    [Publishes("WORK_RESUME", Moves)]
    private async Task<object?> WorkResumeAsync(WorkPausePress press, CancellationToken cancellationToken)
    {
        var (scope, id) = WorkNamed(press.Ask, press.Quest);
        if (press.Said) return await SaidAsync(new WorkAsk(scope, "resume", id), cancellationToken).ConfigureAwait(false);
        var outcome = await WorkPausing.ResumeAsync(WorldOf(_context), scope, id, PluginEvents.Screen, cancellationToken).ConfigureAwait(false);
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
    /// One quest a resume leaves held, as the page receives it: the quest, the verdict and the driver's sentence,
    /// and beside them the facts the page says that sentence from in the reader's language, by the tick's own names
    /// and shapes (<see cref="DriverLoop.TickConsideration"/>), so <c>sittingSentence</c> words a hold as it words a sitting
    /// quest. Each is null for every verdict but its own, which the bridge leaves out; the sentence stays, unchanged, for an
    /// older page. Public for the fast half's table.
    /// </summary>
    public static object WorkHoldShown(WorkHold hold) => new
    {
        hold.Quest,
        Verdict = hold.Verdict.ToString(),
        hold.Reason,
        // The person's stop by its session; its tree stays here.
        hold.HeldBy,
        // Another pause, an ask's or the quest's own.
        PausedBy = hold.PausedBy is { } pause ? new { Scope = pause.Word, pause.Id } : null,
        // A park by the count its sentence says.
        hold.Strikes,
        // A take that is not this machine's: the machine and its session, a take made here, the session not carried on.
        TakenBy = hold.TakenBy is { } taken ? new { taken.Machine, taken.Session, taken.Here, taken.Last } : null,
        CannotShow = hold.CannotShow is { } sat ? new { sat.Code, sat.Environment, sat.Address } : null,
        // Which of its sentences, by code and facts.
        Said = DriverLoop.SaidShown(hold.Said),
    };

    /// <summary>
    /// The verb's own lines and exit code for a work route a terminal sent: <see cref="WorkCommand.RunAsync"/> with this Daoris's
    /// world, whose registry stops what this loop runs, at the terminal's door. A move nudges the loop, as the page's press does.
    /// </summary>
    private async Task<object> SaidAsync(WorkAsk ask, CancellationToken cancellationToken)
    {
        var said = await TerminalSaid.RunAsync(output => WorkCommand.RunAsync(ask, WorldOf(_context), output, cancellationToken)).ConfigureAwait(false);
        if (ask.Verb != "abandon" || ask.Yes) _loop.Nudge();
        return said;
    }

    /// <summary>The ask or the quest a work route names: exactly one of <c>ask</c> and <c>quest</c>.</summary>
    /// <exception cref="DriverException">Neither, or both: the driver's sentence, said verbatim.</exception>
    private static (WorkScope Scope, string Id) WorkNamed(string? ofAsk, string? ofQuest) =>
        (ofAsk, ofQuest) switch
        {
            ({ } ask, null) => (WorkScope.Ask, ask.Trim().TrimStart('#')),
            (null, { } quest) => (WorkScope.Quest, quest.Trim().TrimStart('#')),
            _ => throw new DriverException("a work is named by its ask or by its quest: one of them, never both."),
        };

    /// <summary>
    /// The loop's world for a pause or an abandon: its service, its home, its file and its own registry, which stops what it
    /// runs; and for an abandon's pass, the loop's own sync, where the workspace has a remote in the map its passes read.
    /// Ask Daoris's pause cards and its pause proposals' judging go through it too, as the screen's own door.
    /// </summary>
    internal static WorkWorld WorldOf(DriverContext context)
    {
        var loop = context.Loop;
        var service = loop.Service ?? throw NotReady();
        return new WorkWorld(service, loop.Home, loop.ConfigPath, loop.Processes)
        {
            Unadopted = context.Unadopted(DriverConfig.Load(loop.ConfigPath)),
            Log = loop.Log,
            Sync = async (workspace, ct) =>
            {
                if (!RemoteTarget.Load().ContainsKey(RemoteTarget.Workspace(workspace))) return null;
                // Before the loop's sync set is up there is no pass to run: the decline travels on the loop's first.
                return loop.SyncNowAsync(workspace, ct) is { } pass
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
