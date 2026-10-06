using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// What a pause does with one piece of the work (D132 point 2, design §2.1), in the public spelling the page and the terminal
/// share: a word, never a sentence, so each door says it in its own words and the page in the reader's language.
/// </summary>
public static class PauseAct
{
    /// <summary>A quest, open or taken here: nothing of it starts on this machine until *Resume*; its place, take, tree and strikes stay.</summary>
    public const string Paused = "paused";

    /// <summary>A quest done or declined: a pause changes no work.</summary>
    public const string Closed = "closed";

    /// <summary>A quest taken with no session of this machine's on it: its taker's, on another machine or outside Daoris.</summary>
    public const string Elsewhere = "elsewhere";

    /// <summary>A live session this machine runs: stopped, as the person's stop, which *Resume* releases.</summary>
    public const string Stopped = "stopped";

    /// <summary>A session waiting on you: left parked, since stopping it would end the question it asked.</summary>
    public const string Parked = "parked";

    /// <summary>The ask's running intake: left running, since an ask has one; what it publishes is paused with the rest.</summary>
    public const string Intake = "intake";

    /// <summary>A teammate's live session: its process is on their machine (D47 §4), and nothing here reaches it.</summary>
    public const string Teammate = "teammate";

    /// <summary>A session that ended: a record, untouched.</summary>
    public const string Ended = "ended";
}

/// <summary>A quest of the work and what a pause does with it, with the pause that holds it now where one does.</summary>
public sealed record WorkPlanQuest(WorkQuest Quest, string Pause)
{
    public PausedBy? PausedBy { get; init; }
}

/// <summary>A session of the work and what a pause does with it.</summary>
public sealed record WorkPlanSession(WorkSession Session, string Pause);

/// <summary>
/// The work of an ask or a quest with what a pause would do with each piece (PAUSE1b, design §1, §2.1): what <c>WORK_PLAN</c>
/// answers and what a pause acts on, so the screen and the pause cannot disagree. Abandon's half is PAUSE1d's.
/// </summary>
public sealed record WorkPlan(WorkPieces Pieces, IReadOnlyList<WorkPlanQuest> Quests, IReadOnlyList<WorkPlanSession> Sessions)
{
    /// <summary>This scope's own pause, as <c>driver.json</c> keeps it, or null where it is not paused here.</summary>
    public WorkPause? Paused { get; init; }

    /// <summary>Whether a pause would hold anything: a quest open or taken here, or for an ask still proposed, its intake.</summary>
    public bool Pausable { get; init; }

    /// <summary>The ask itself, for an ask's work, as the service answered it; null for a quest's.</summary>
    public AskView? Ask { get; init; }
}

/// <summary>What a pause or a resume reads and touches: the service, the home, <c>driver.json</c> and this machine's processes.</summary>
/// <param name="Processes">
/// The registry the door holds: the loop's own at the screen, which stops what it runs; at a terminal, the home's markers,
/// which run nothing, so a live session is reached through the request its loop takes (SESSUX1g).
/// </param>
public sealed record WorkWorld(ServiceClient Service, string Home, string ConfigPath, SessionProcesses Processes)
{
    /// <summary>The configured agent's wire, which a resume's plan is read by (D70).</summary>
    public SessionWire Door { get; init; } = SessionWire.Pipe;

    /// <summary>Where <c>work.paused</c> and <c>work.resumed</c> go; null writes none.</summary>
    public MachineLog? Log { get; init; }

    /// <summary>How long a request to another Daoris process is waited for: ten seconds, as the sessions verb waits (D126 §7.1).</summary>
    public TimeSpan Wait { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>How often the record is read while a request waits.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromMilliseconds(250);

    public Func<DateTimeOffset> Clock { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>
    /// How an abandon judges a tree and its branch, and discards them (PAUSE1d, D132 §3.3): this home's
    /// <see cref="SessionTrees"/>, which asks git; null is that. A test hands in its own.
    /// </summary>
    public IWorkTrees? Trees { get; init; }

    /// <summary>
    /// One sync pass of a workspace (D69), as the door runs it (PAUSE1d, D132 §3.4 step 4): the loop's own pass at the screen,
    /// the terminal's set at a terminal. A null answer is a workspace with no remote here; a null delegate is a machine with
    /// none wired, whose declines travel nowhere.
    /// </summary>
    public Func<string, CancellationToken, Task<SyncReport?>>? Sync { get; init; }

    /// <summary>The trees an abandon judges and discards: <see cref="Trees"/>, or this home's own.</summary>
    internal IWorkTrees TreesHere => Trees ?? new SessionTrees(Home);
}

/// <summary>How a pause came out.</summary>
public enum PauseVerdict
{
    /// <summary>The pause is written, and what of the work ran here was stopped or asked to stop.</summary>
    Paused,

    /// <summary>Nothing of the work is open or taken here, and no intake would start: nothing was written (D48 §6, information).</summary>
    Nothing,

    /// <summary>No such ask on this machine, or no such quest here: <c>WORK_UNKNOWN</c>.</summary>
    Unknown,
}

/// <summary>How a resume came out.</summary>
public enum ResumeVerdict
{
    /// <summary>The pause is gone and every stop it made released.</summary>
    Resumed,

    /// <summary>It was not paused here: nothing changed (D48 §6, information).</summary>
    NotPaused,

    /// <summary>No such ask on this machine, or no such quest here, and no pause of it: <c>WORK_UNKNOWN</c>.</summary>
    Unknown,
}

/// <summary>A session a pause stopped, or a stop a resume released, and its quest.</summary>
public sealed record WorkStop(string Session, string? Quest);

/// <summary>
/// A live session a pause did not stop, and why: <c>parked</c>, <c>intake</c> and <c>teammate</c> by design; <c>elsewhere</c>
/// (the Daoris process that runs it took no request in time), <c>not-running</c> (queued, and nothing here runs it yet) and
/// <c>unanswered</c> (the service did not move its record), which the person can try again.
/// </summary>
public sealed record WorkKept(string Session, string? Quest, string Why)
{
    /// <summary>For a teammate's session, the machine it runs on.</summary>
    public string? Machine { get; init; }

    /// <summary>Whether the pause meant to stop it and could not: the ones a person may try again.</summary>
    public bool Unreached => Why is WorkPausing.Elsewhere or WorkPausing.NotRunning or WorkPausing.Unanswered;
}

/// <summary>What a pause did.</summary>
public sealed record PauseOutcome(WorkScope Scope, string Id, PauseVerdict Verdict)
{
    /// <summary>It was paused already: its time and its earlier stops stand, and what started since was stopped.</summary>
    public bool Already { get; init; }

    public IReadOnlyList<WorkStop> Stopped { get; init; } = [];

    public IReadOnlyList<WorkKept> Kept { get; init; } = [];
}

/// <summary>A quest of the work something still holds after a resume (design §2.4): the planner's verdict and sentence.</summary>
public sealed record WorkHold(string Quest, StartVerdict Verdict, string Reason);

/// <summary>What a resume did.</summary>
public sealed record ResumeOutcome(WorkScope Scope, string Id, ResumeVerdict Verdict)
{
    /// <summary>Each stop the pause made, now released (SESSUX1b's <c>released</c>).</summary>
    public IReadOnlyList<WorkStop> Released { get; init; } = [];

    /// <summary>What still holds each quest of the work, named and never released; null where the plan could not be read.</summary>
    public IReadOnlyList<WorkHold>? Holds { get; init; }
}

/// <summary>
/// Pause and resume an ask's work, or one quest's, on this machine (PAUSE1b, D132 points 2–4, design §2): one implementation
/// the screen's <c>WORK_PLAN</c>, <c>WORK_PAUSE</c> and <c>WORK_RESUME</c> and the terminal's <c>ask --pause|--resume</c> and
/// <c>quest pause|resume</c> call (D50).
/// </summary>
/// <remarks>
/// <para><b>A pause takes nothing Resume cannot give back.</b> It writes itself into <c>driver.json</c> first, so nothing of the
/// work starts between the steps that follow; it stops every live session of the work this machine runs as the person's own
/// stop, through the routes the screen's *Stop…* takes and the request folder SESSUX1g made (<c>by: pause</c>); and it
/// records each stop as its own. A session waiting on you stays parked, a running intake goes on, and a teammate's session
/// is named, never reached. Each open quest keeps its place, and each taken one its take, tree and strikes: the pause is a
/// verdict and a file, never a state (D46 §3).</para>
///
/// <para><b>Resume releases what the pause made</b>, <c>released</c> for each stop, and removes the pause. It starts nothing
/// itself: the next look plans the work as it would have, a taken quest carried on in its tree. What still holds a quest (a
/// stop made before the pause, the strikes, a repository's hold, another pause) is named from the planner's own verdict,
/// never released.</para>
/// </remarks>
public static class WorkPausing
{
    /// <summary><see cref="WorkKept.Why"/>: the Daoris process that runs it took no request in time.</summary>
    public const string Elsewhere = "elsewhere";

    /// <summary><see cref="WorkKept.Why"/>: queued, and nothing on this machine runs it yet to stop.</summary>
    public const string NotRunning = "not-running";

    /// <summary><see cref="WorkKept.Why"/>: the service did not answer, or refused, the move of its record.</summary>
    public const string Unanswered = "unanswered";

    /// <summary>The verdicts that hold a quest after a resume (design §2.4): each says what holds it, where a wait only says when.</summary>
    private static readonly HashSet<StartVerdict> Holding =
    [
        StartVerdict.Paused, StartVerdict.Stopped, StartVerdict.Exhausted, StartVerdict.Held,
        StartVerdict.NotDrivable, StartVerdict.NotAdopted, StartVerdict.NoRoot,
        // A take that is not this machine's (CARRY2b): a resume carries nothing on over it.
        StartVerdict.TakenElsewhere,
    ];

    /// <summary>
    /// What a pause would do with each piece of a work (design §2.1). Pure: the work as <see cref="AskWork"/> read it, the
    /// pauses in the file and the paused set the look would compute, and the ask itself for an ask's work.
    /// </summary>
    public static WorkPlan Plan(WorkPieces pieces, DriverConfig config, IReadOnlyDictionary<string, PausedBy> paused, AskView? ask)
    {
        // A take is this machine's where a session of its own worked the quest and did not stand down to another's take.
        var ours = pieces.Sessions
            .Where(session => !session.Teammate && session.Record.Quest is not null && session.Record.State != "stood-down")
            .Select(session => session.Record.Quest!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var quests = pieces.Quests.Select(quest => new WorkPlanQuest(quest, quest.Quest.Status switch
            {
                "Open" => PauseAct.Paused,
                "Taken" when ours.Contains(quest.Quest.Id) => PauseAct.Paused,
                "Taken" => PauseAct.Elsewhere,
                _ => PauseAct.Closed,
            })
            {
                PausedBy = paused.GetValueOrDefault(quest.Quest.Id),
            })
            .ToList();

        var sessions = pieces.Sessions.Select(session => new WorkPlanSession(session, session switch
            {
                { Record.Live: false } => PauseAct.Ended,
                { Teammate: true } => PauseAct.Teammate,
                { Intake: true } => PauseAct.Intake,
                { Record.State: "awaiting-person" } => PauseAct.Parked,
                _ => PauseAct.Stopped,
            }))
            .ToList();

        return new WorkPlan(pieces, quests, sessions)
        {
            Paused = Entry(config, pieces.Scope, pieces.Id),
            Pausable = quests.Any(quest => quest.Pause == PauseAct.Paused) || ask is { State: "Proposed" },
        };
    }

    /// <summary>The work and what a pause would do with it, read now; null where no such ask or quest is on this machine.</summary>
    public static async Task<WorkPlan?> PlanAsync(WorkWorld world, WorkScope scope, string id, CancellationToken ct = default)
    {
        var named = Named(id);
        var quests = await world.Service.EveryQuestAsync(ct).ConfigureAwait(false);
        AskView? ask = null;
        if (scope == WorkScope.Ask)
        {
            ask = await world.Service.FindAskAsync(named, ct).ConfigureAwait(false);
            if (ask is null) return null;
        }
        else if (!quests.Any(quest => string.Equals(quest.Id, named, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var look = new AskWorkLook(quests, await world.Service.SessionRecordsAsync(ct).ConfigureAwait(false))
        {
            Landings = new LandedBranches(world.Home).Entries(),
        };
        var config = DriverConfig.Load(world.ConfigPath);
        return Plan(AskWork.Read(look, scope, named, new SessionTrees(world.Home).Holds), config, PausedWork.Set(look, config), ask) with
        {
            Ask = ask,
        };
    }

    /// <summary>
    /// Pause a work on this machine (design §2.1): the pause written first, then every live session of it this machine runs
    /// stopped as the person's, each stop recorded as the pause's own.
    /// </summary>
    /// <param name="door">The machine log's <c>door</c>: <c>screen</c> or <c>terminal</c>.</param>
    public static async Task<PauseOutcome> PauseAsync(WorkWorld world, WorkScope scope, string id, string door, CancellationToken ct = default)
    {
        var named = Named(id);
        if (await PlanAsync(world, scope, named, ct).ConfigureAwait(false) is not { } plan)
        {
            return new PauseOutcome(scope, named, PauseVerdict.Unknown);
        }

        if (plan.Paused is null && !plan.Pausable) return new PauseOutcome(scope, named, PauseVerdict.Nothing);

        // 1. The pause first, so nothing of the work starts while its sessions are stopped (design §3.4's order, a pause's half).
        var config = DriverConfig.Load(world.ConfigPath);
        var earlier = Entry(config, scope, named);
        var at = earlier?.At ?? ToSecond(world.Clock());
        WithPause(config, scope, named, new WorkPause(at, earlier?.Stopped ?? new Dictionary<string, string>())).Save(world.ConfigPath);

        // 2. Every live session of the work this machine runs, stopped as the person's; the rest named with why.
        var note = new PausedBy(scope, named).Noted;
        var stopped = new List<WorkStop>();
        var kept = new List<WorkKept>();
        foreach (var each in plan.Sessions)
        {
            var record = each.Session.Record;
            switch (each.Pause)
            {
                case PauseAct.Stopped:
                    string? why;
                    try
                    {
                        why = await StopAsync(world, record, note, RequestDoor.Pause, ct).ConfigureAwait(false);
                    }
                    catch (Exception error) when (error is DriverException or HttpRequestException)
                    {
                        // The pause is written and holds; the session the service would not move is named, to be asked again.
                        why = Unanswered;
                    }

                    if (why is null) stopped.Add(new WorkStop(record.Id, record.Quest));
                    else kept.Add(new WorkKept(record.Id, record.Quest, why));
                    break;
                case PauseAct.Parked or PauseAct.Intake:
                    kept.Add(new WorkKept(record.Id, record.Quest, each.Pause));
                    break;
                case PauseAct.Teammate:
                    kept.Add(new WorkKept(record.Id, record.Quest, each.Pause) { Machine = record.Id[..record.Id.IndexOf('/')] });
                    break;
            }
        }

        // 3. Each stop recorded as the pause's, for Resume to release: read again, since the file may have moved meanwhile.
        if (stopped.Any(stop => stop.Quest is not null))
        {
            var now = DriverConfig.Load(world.ConfigPath);
            var entry = Entry(now, scope, named) ?? new WorkPause(at, new Dictionary<string, string>());
            var stops = new Dictionary<string, string>(entry.Stopped, StringComparer.OrdinalIgnoreCase);
            foreach (var stop in stopped.Where(stop => stop.Quest is not null)) stops[stop.Quest!] = stop.Session;
            WithPause(now, scope, named, entry with { Stopped = stops }).Save(world.ConfigPath);
        }

        world.Log?.Info("work.paused", ("scope", Word(scope)), ("stopped", stopped.Count), ("door", door));
        return new PauseOutcome(scope, named, PauseVerdict.Paused) { Already = earlier is not null, Stopped = stopped, Kept = kept };
    }

    /// <summary>
    /// Resume a work (design §2.4): the pause removed, each stop it made released, and what still holds each quest named from
    /// the planner's verdict over the file as it now stands.
    /// </summary>
    /// <param name="door">The machine log's <c>door</c>: <c>screen</c> or <c>terminal</c>.</param>
    public static async Task<ResumeOutcome> ResumeAsync(WorkWorld world, WorkScope scope, string id, string door, CancellationToken ct = default)
    {
        var named = Named(id);
        var config = DriverConfig.Load(world.ConfigPath);
        // A pause of something since deleted still resumes: the entry is this machine's, and only Resume takes it away.
        if (Entry(config, scope, named) is not { } pause)
        {
            var known = scope == WorkScope.Ask
                ? await world.Service.FindAskAsync(named, ct).ConfigureAwait(false) is not null
                : await world.Service.FindQuestAsync(named, ct).ConfigureAwait(false) is not null;
            return new ResumeOutcome(scope, named, known ? ResumeVerdict.NotPaused : ResumeVerdict.Unknown);
        }

        var resumed = WithPause(config, scope, named, null);
        foreach (var (quest, session) in pause.Stopped) resumed = resumed.WithReleased(quest, session);
        resumed.Save(world.ConfigPath);
        var released = pause.Stopped.Select(stop => new WorkStop(stop.Value, stop.Key)).OrderBy(stop => stop.Quest, StringComparer.Ordinal).ToList();
        world.Log?.Info("work.resumed", ("scope", Word(scope)), ("released", released.Count), ("door", door));

        return new ResumeOutcome(scope, named, ResumeVerdict.Resumed)
        {
            Released = released,
            Holds = await HoldsAsync(world, scope, named, ct).ConfigureAwait(false),
        };
    }

    /// <summary>What a terminal prints of a pause, one line each, in English (D50): the screen says it from the answer's facts.</summary>
    public static IReadOnlyList<string> Lines(PauseOutcome outcome)
    {
        var what = Named(outcome.Scope, outcome.Id);
        var door = new PausedBy(outcome.Scope, outcome.Id).Door;
        switch (outcome.Verdict)
        {
            case PauseVerdict.Unknown:
                return [Unknown(outcome.Scope, outcome.Id)];
            case PauseVerdict.Nothing:
                return [$"nothing of {what} is open or taken on this machine, so there is nothing to pause."];
        }

        var lines = new List<string>
        {
            outcome.Already
                ? $"{what} was already paused; nothing of it starts on this machine until you resume it — {door}"
                : $"paused {what}: nothing of it starts on this machine until you resume it — {door}",
        };
        lines.AddRange(outcome.Stopped.Select(stop =>
            $"  stopped {stop.Session}{On(stop.Quest)}: its tree keeps what it wrote, and Resume takes its quest up again."));
        lines.AddRange(outcome.Kept.Select(kept => kept.Why switch
        {
            PauseAct.Parked => $"  left {kept.Session}{On(kept.Quest)} waiting on you: your answer is kept, and what it starts waits for Resume.",
            PauseAct.Intake => $"  its intake {kept.Session} keeps reading; what it publishes waits too.",
            PauseAct.Teammate => $"  {kept.Session}{On(kept.Quest)} runs on {kept.Machine}; nothing this machine sends reaches it.",
            Elsewhere => $"  could not stop {kept.Session}{On(kept.Quest)}: the Daoris process that runs it took no request in time "
                         + $"(a terminal's chat stops with its own Ctrl+C); `daoris-driver sessions stop {kept.Session}` asks again.",
            Unanswered => $"  could not stop {kept.Session}{On(kept.Quest)}: the service did not move its record; "
                          + $"`daoris-driver sessions stop {kept.Session}` asks again.",
            _ => $"  could not stop {kept.Session}{On(kept.Quest)}: it is queued, and nothing on this machine runs it yet.",
        }));
        return lines;
    }

    /// <summary>What a terminal prints of a resume, one line each, in English (D50).</summary>
    public static IReadOnlyList<string> Lines(ResumeOutcome outcome)
    {
        var what = Named(outcome.Scope, outcome.Id);
        switch (outcome.Verdict)
        {
            case ResumeVerdict.Unknown:
                return [Unknown(outcome.Scope, outcome.Id)];
            case ResumeVerdict.NotPaused:
                return [$"{what} is not paused on this machine, so nothing changed."];
        }

        var lines = new List<string> { $"resumed {what}: the driver's next look plans its work as it would have." };
        lines.AddRange(outcome.Released.Select(stop =>
            $"  released the pause's stop of {stop.Session}{On(stop.Quest)}: a taken quest is carried on in its tree."));
        if (outcome.Holds is null)
        {
            lines.Add("  what still holds its quests could not be read; `daoris-driver sessions` says where each stands.");
        }
        else
        {
            lines.AddRange(outcome.Holds.Select(hold => $"  #{hold.Quest} still sits: {hold.Reason}"));
        }

        return lines;
    }

    /// <summary>The refusal both doors say for an ask or a quest this machine does not have (<c>WORK_UNKNOWN</c>).</summary>
    public static string Unknown(WorkScope scope, string id) =>
        scope == WorkScope.Ask ? $"no ask #{id} on this machine." : $"no quest #{id} here.";

    /// <summary><c>ask</c> or <c>quest</c>, as the machine log and the page spell a scope.</summary>
    public static string Word(WorkScope scope) => scope == WorkScope.Ask ? "ask" : "quest";

    /// <summary>This scope's own pause in the file, or null.</summary>
    public static WorkPause? Entry(DriverConfig config, WorkScope scope, string id) =>
        scope == WorkScope.Ask ? config.PausedAsk(id) : config.PausedQuest(id);

    /// <summary>The file with this scope's pause written, or with null taken away: the pause's and the abandon's one writer.</summary>
    internal static DriverConfig WithPause(DriverConfig config, WorkScope scope, string id, WorkPause? pause) =>
        scope == WorkScope.Ask ? config.WithPausedAsk(id, pause) : config.WithPausedQuest(id, pause);

    /// <summary>An id as a person writes it, without its <c>#</c>.</summary>
    /// <exception cref="DriverException">A blank id: a pause names what it pauses.</exception>
    internal static string Named(string id) =>
        id?.Trim().TrimStart('#').Trim() is { Length: > 0 } named
            ? named
            : throw new DriverException("a pause names the ask or the quest it pauses.");

    private static string Named(WorkScope scope, string id) => scope == WorkScope.Ask ? $"ask #{id}" : $"#{id}";

    private static string On(string? quest) => quest is null ? "" : $" on #{quest}";

    /// <summary>When, to the second, as <c>driver.json</c> writes a pause's time (PAUSE1a).</summary>
    internal static DateTimeOffset ToSecond(DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, TimeSpan.Zero);
    }

    /// <summary>
    /// Stop one live session as the person's, with the pause's or the abandon's words on its record: null when it stopped,
    /// else why not. One this registry runs is stopped here; one nothing here runs is ended as an orphan; one another Daoris
    /// process here runs is asked through the request its loop takes (SESSUX1g), and waited for. A session waiting on you
    /// (an abandon's, PAUSE1d) is ended <c>stopped</c> unanswered, as <c>RESOLVE_SESSION</c> ends it, its process first.
    /// </summary>
    /// <param name="by">Who asks, as the request names it: <see cref="RequestDoor.Pause"/> or <see cref="RequestDoor.Abandon"/>.</param>
    internal static async Task<string?> StopAsync(WorkWorld world, SessionRecord record, Noted note, string by, CancellationToken ct)
    {
        var parked = record.State == "awaiting-person";
        // A parked session this registry runs, or one nothing on this machine runs: its process (if any) goes, then its record.
        if (parked && (world.Processes.Running.Contains(record.Id, StringComparer.OrdinalIgnoreCase)
                || !world.Processes.AliveOnThisMachine(record.Id)))
        {
            await SessionMoves.ResolveAsync(id => world.Processes.Stop(id), world.Service, record.Id, "stopped", note, ct).ConfigureAwait(false);
            return null;
        }

        if (!parked)
        {
            var answer = await SessionMoves.StopAsync(world.Processes, world.Service, record.Id, ct, note).ConfigureAwait(false);
            if (answer.Stopped) return null;
            if (!answer.Elsewhere) return NotRunning;
        }

        var requests = new SessionRequests(world.Home);
        requests.Write(new SessionRequest(record.Id, SessionMove.Stop, world.Clock()) { By = by, Note = note.Note, NoteParts = note.Parts, Parked = parked });
        var waited = Stopwatch.StartNew();
        while (waited.Elapsed < world.Wait)
        {
            await Task.Delay(world.Poll, ct).ConfigureAwait(false);
            var now = (await world.Service.SessionRecordsAsync(ct).ConfigureAwait(false))
                .FirstOrDefault(each => string.Equals(each.Id, record.Id, StringComparison.Ordinal));
            if (now is null || now.State != record.State) return null;
        }

        // Withdrawn, no loop acts on it after the person was told; taken and not yet moved, the loop that runs it is stopping it.
        return requests.Withdraw(record.Id) ? Elsewhere : null;
    }

    /// <summary>What still holds each quest of the work once resumed: the planner's verdict over a fresh look, or null where it could not be read.</summary>
    private static async Task<IReadOnlyList<WorkHold>?> HoldsAsync(WorkWorld world, WorkScope scope, string id, CancellationToken ct)
    {
        try
        {
            var config = DriverConfig.Load(world.ConfigPath);
            var snapshot = await PausedWork.LookAsync(
                world.Service, config, await world.Service.SnapshotAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false);
            var plan = Planner.Plan(snapshot, config, world.Door);
            var work = AskWork.Read(
                new AskWorkLook(await world.Service.EveryQuestAsync(ct).ConfigureAwait(false),
                    await world.Service.SessionRecordsAsync(ct).ConfigureAwait(false)),
                scope, id, _ => false);
            return
            [
                .. plan.Where(consideration => Holding.Contains(consideration.Verdict) && work.Has(consideration.Quest.Id))
                    .Select(consideration => new WorkHold(consideration.Quest.Id, consideration.Verdict, consideration.Reason)),
            ];
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            // The resume is written; what still holds is said by the quests' pages and the next look.
            return null;
        }
    }
}
