using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The performer of a run's <c>land</c>: <see cref="LandingDoor"/> starts it off the pass, once per node and tip,
/// and it judges the run's work as it stands now, then lands it as the review's Accept lands it, authorized by the review's word or the
/// standing authority. It is the one lander.
/// </summary>
/// <remarks>
/// <para><b>Nothing here pushes, merges or writes a quest.</b> The landing is the session trees' own act through the delivery: the push is the
/// plugin's, and a landing writes no quest state.</para>
///
/// <para><b>A refusal is not tried every pass.</b> Each try is kept in memory with the tree's tip and status it is made at, and the next is made
/// only once either moves (<see cref="LandTries.ShouldTry"/>), or the person's press asks again (<see cref="Retry"/>). A restart reads as
/// no try, and the next pass judges afresh.</para>
///
/// <para><b>Judged again before each effect.</b> A landing waits behind another of its tree, and a push behind another of its branch, so the
/// quest, its holds and the gate are read again under the tree's hold and just before the plugin is told.</para>
/// </remarks>
/// <param name="log">Where a try's <c>landing.auto</c> line goes: the service client's watchers, which write the machine log.</param>
public sealed class LandPerformer(
    string home, IWorkplaceLandingWorld world, SessionTrees trees, IWorkplaceEventHistory events, Action<LandingLine>? log = null,
    Func<DateTimeOffset>? clock = null)
{
    /// <summary>Whether this process holds the home's lease, as the node records ask it; null asks the lease itself. A test's stand-in sets it.</summary>
    public Func<bool>? HoldsLease { get; init; }

    /// <summary>Where a try that landed a branch or moved a chain's on is told at <c>work/event</c>, once, as it is kept.</summary>
    public Action<WorkEvent>? Told { get; init; }

    private IWorkplaceWorkflows Flows => WorkplaceWorkflows.Current;

    // The last try of each session: a refusal is tried again only once the tree moves.
    private static readonly ConcurrentDictionary<string, AutoTry> Tries = new(StringComparer.OrdinalIgnoreCase);

    private AutoTry? LastOf(string session) => Tries.GetValueOrDefault($"{home}\n{session}");

    private void TakeRepair(LandStart run) { if (run.Review is { } review) Flows.TakeRepair(home, HoldsLease, run.Unit, review); }

    /// <summary>A session chosen to land, with what is read of it: its quest, its tree's tip and status, the gate pinned to that tip.</summary>
    private sealed record Choice(LandEntry Entry, QuestView Quest, string? Tip, string Status, LandStart Run)
    {
        public IWorkplaceGate? Gate { get; init; }

        /// <summary>How many paths the tree's status named, so the landing's dirty check asks git no more; null where git could not say.</summary>
        public int? Uncommitted { get; init; }
    }

    /// <summary>
    /// The person's press on a run whose <c>land</c> is refused: the refusal is forgotten, so the next pass tries the landing again though
    /// neither its tip nor its tree's status moved. Nothing else repeats a refusal that nothing changed.
    /// </summary>
    public static void Retry(string home, string session, string? unit = null, string? tip = null)
    {
        Tries.TryRemove($"{home}\n{session}", out _);
        if (unit is not null && tip is not null) LandOutcomes.Forget(unit, tip);
    }

    /// <summary>Lands the work of a run whose <c>land</c> the runtime started. Null where there is nothing to land from here.</summary>
    public async Task<string?> PerformAsync(LandStart start, CancellationToken ct = default)
    {
        if (WorkplaceRunFiles.IsChatRun(start.Run)) return await PerformChatAsync(start, ct).ConfigureAwait(false);
        var fresh = world.Again();
        var records = await fresh.RecordsAsync(ct).ConfigureAwait(false);
        // The branch unit's newest executor with a tree; else the newest session on the run's own quest.
        var record = start.Executors.Reverse().Select(id => RecordOf(records, id)).FirstOrDefault(each => each is { Tree.Length: > 0, Quest.Length: > 0 })
            ?? records.Where(each => !each.Teammate && each is { Tree.Length: > 0 } && string.Equals(each.Quest, start.Run, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(each => each.Created).FirstOrDefault();
        if (record is null) return null;
        var entry = EntryOf(record.Id, record.Quest!, start, record.Tree!);
        if (PieceLandings.Refused(record) is { } partner) return Workflow(entry, partner, line: null);
        var (said, choice) = await JudgeAsync(entry, fresh, new Records(fresh), start, ct).ConfigureAwait(false);
        if (choice is null) return said;
        var landed = await LandOneAsync(choice, fresh, ct).ConfigureAwait(false);
        // A repair outlives nothing that ended the landing: a landing or a refusal spends it.
        if (LastOf(record.Id) is { Code: var code } && LandingCode.Closes(code) && code != LandingCode.CarryBound) TakeRepair(start);
        return landed;
    }

    private LandEntry EntryOf(string session, string quest, LandStart start, string tree) =>
        new(session, quest, start.Repository, start.Workspace, tree, Now()) { Tries = LastOf(session) is { } tried ? [tried] : [] };

    /// <summary>
    /// A conversation's <c>land</c>: the run has no quest, so its record is the newest in the run's branch's tree, and its gate is read by the run
    /// at the tip the node is active at. A tree that moved past the tip, or holds uncommitted paths, is refused and tried again once it changes.
    /// </summary>
    private async Task<string?> PerformChatAsync(LandStart start, CancellationToken ct)
    {
        var fresh = world.Again();
        var records = await fresh.RecordsAsync(ct).ConfigureAwait(false);
        var record = start.Executors.Reverse().Select(id => RecordOf(records, id)).FirstOrDefault(each => each is { Tree.Length: > 0 })
            ?? (WorkplaceRunFiles.RecordOf(start.Run) is { } first ? RecordOf(records, first) : null);
        if (record is not { Tree: { Length: > 0 } tree }) return null;
        var entry = EntryOf(record.Id, "", start, tree);
        if (PieceLandings.Refused(record) is { } partner) return Workflow(entry, partner, line: null);
        if (LandTries.Landed(entry, start.Tip)) return null;
        var standing = new LinePlaces(home).Of(tree);
        // A line whose slot another line took lands once its tree is brought back, so it is neither closed nor tried now.
        if (standing.Stand == LineStand.CanBeBroughtBack) return null;
        if (standing.GoneForGood) return Tried(entry, LandingCode.Gone, start.Tip, "gone", new TreeLanding(false, Words(LandingCode.Gone, null)));
        var (tip, status, uncommitted) = await ReadTreeAsync(tree, ct).ConfigureAwait(false);
        if (!LandTries.ShouldTry(entry, tip, status, await RootStatusAsync(entry, ct).ConfigureAwait(false))) return null;
        var (gate, held, _) = await GateAsync(entry, record.Id, null, tree, fresh, ct, tip: start.Tip).ConfigureAwait(false);
        if (held is not null) return Tried(entry, held.Refusal ?? LandingCode.Refused, tip, status, held);
        if (!string.Equals(tip, start.Tip, StringComparison.OrdinalIgnoreCase))
        {
            return Tried(entry, LandingCode.Refused, tip, status, new TreeLanding(false, MovedWords) { Refusal = LandingCode.Refused });
        }

        if (uncommitted > 0) return Tried(entry, LandingCode.Uncommitted, tip, status, uncommitted: uncommitted);
        var subject = await trees.OwnSubjectAsync(
            tree, record.Id, null, records, id => fresh.QuestAsync(id, ct), named => events.Openings([named]).GetValueOrDefault(named), gate.Process, ct)
            .ConfigureAwait(false);
        var landed = await trees.LandAsync(tree, subject, ct, fresh.InUseAsync, start.Reviewed ? AcceptedBy.Person : AcceptedBy.Auto, gate.WithTip(start.Tip),
            subject.Joins, beside: fresh.BesideAsync).ConfigureAwait(false);
        LandOutcomes.Kept(start.Unit, start.Tip, landed);
        return Tried(entry, LandTries.CodeOf(landed), tip, status, landed);
    }

    /// <summary>The service's session records, read once for a judgement, and only once it needs them.</summary>
    private sealed class Records(IWorkplaceLandingWorld world)
    {
        private IReadOnlyList<SessionRecord>? _read;

        public async Task<IReadOnlyList<SessionRecord>> ReadAsync(CancellationToken ct) =>
            _read ??= await world.RecordsAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The run's work judged against its process, its records, its quest, its tree and its gate as they stand now: the line said for it, and the
    /// choice where it is worth a try.
    /// </summary>
    private async Task<(string? Said, Choice? Choice)> JudgeAsync(
        LandEntry entry, IWorkplaceLandingWorld world, Records looked, LandStart run, CancellationToken ct)
    {
        if (LandTries.Landed(entry, run.Tip)) return (null, null);

        // The review's word stands for the person's Accept, and its carry-on is a repair kept beside it on the run's record.
        var carried = CarriedOf(entry, run);
        var config = DriverConfig.Load(DriverConfig.ResolvePath(home));
        var process = await Flows.ReadProcessAsync(home, config, world.QuestsAsync, entry.Quest, entry.Repository, entry.Workspace, ct)
            .ConfigureAwait(false);

        // Landed since another way, from when the node started; an unclocked node has none, and the content proof below closes it.
        if (run.Since is { } since && trees.Recorded.Landing(entry.Session) is { } landing && landing.AcceptedAt(entry.Session) >= since)
        {
            return (Closed(entry, LandingCode.Already, branch: landing.Branch), null);
        }

        var placed = new LinePlaces(home).Of(entry.Tree);
        if (placed.Stand == LineStand.CanBeBroughtBack) return (null, null);
        if (placed.GoneForGood) return (Closed(entry, LandingCode.Gone), null);

        // A live session in the tree is using it, and a newer session on it stands for it now.
        var records = await looked.ReadAsync(ct).ConfigureAwait(false);
        var onTree = records.Where(record => !record.Teammate && record.Tree is { } tree && SameTree(tree, entry.Tree)).ToList();
        if (onTree.Any(record => record.Live)) return (null, null);
        var newest = onTree.OrderByDescending(record => record.Created).ThenByDescending(record => record.Id, StringComparer.Ordinal).FirstOrDefault();
        if (newest is not null && !string.Equals(newest.Id, entry.Session, StringComparison.OrdinalIgnoreCase))
        {
            return (Closed(entry, LandingCode.Superseded), null);
        }

        // A stop, a decline or a stand-down takes the yes back: a carry-on is never sent to a session the person ended.
        if (carried is not null && LandCarry.Skips(newest))
        {
            TakeRepair(run);
            carried = null;
        }

        // In a closure graph the landing waits on the quest's current submission; a reopen takes it back.
        var quest = await world.QuestAsync(entry.Quest, ct).ConfigureAwait(false);
        if (!QuestResults.Judged(quest)) return (Closed(entry, LandingCode.Undone), null);

        // Held for the person's yes or evidence's verdict, said once; a graph with a `land` judges them as its `quest.holds` step instead.
        if (process.JudgesHolds && quest is { Held: true } && quest.Hold != EvidenceCodes.Unreviewed)
        {
            return (entry.Last?.Code != LandingCode.Held ? Tried(entry, LandingCode.Held, tip: null, status: null) : null, null);
        }

        var (tip, status, uncommitted) = await ReadTreeAsync(entry.Tree, ct).ConfigureAwait(false);
        if (!LandTries.ShouldTry(entry, tip, status, await RootStatusAsync(entry, ct).ConfigureAwait(false))) return (null, null);

        if (!string.Equals(tip, run.Tip, StringComparison.OrdinalIgnoreCase))
        {
            return (Tried(entry, LandingCode.Refused, run.Tip, status, new TreeLanding(false, MovedWords) { Refusal = LandingCode.Refused }), null);
        }

        // Concluded again with nothing new, it is already on the branch its landing made: closed before the gate, behind the content proof.
        if (uncommitted == 0 && trees.Recorded.Landing(entry.Session) is { GoneAt: null } made
            && await trees.UnlandedHereAsync(entry.Tree, ct).ConfigureAwait(false) == 0)
        {
            return (Closed(entry, LandingCode.Already, branch: made.Branch), null);
        }

        // The landing gate, read once, its first part that holds as a refusal.
        var (gate, held, line) = await GateAsync(entry, entry.Session, entry.Quest, entry.Tree, world, ct, process).ConfigureAwait(false);

        // The run's own part, kept with no tip and no status, so the next pass reads the gate again whatever the tree does.
        if (held is { Refusal: LandingCode.Refused }) return (Workflow(entry, held, line), null);

        // The second opinion or the review waits, said once and read again at every pass.
        if (held is { Refusal: { } waiting })
        {
            if (waiting == LandingCode.Opinion) Owe(entry, entry.Session, entry.Quest, entry.Tree, since: entry.DueAt);
            if (entry.Last?.Code == waiting) return (null, null);
            var waits = Tried(entry, waiting, tip, status, held);
            if (line is not null) log?.Invoke(line);
            return (waits, null);
        }

        // Uncommitted work would not travel with the branch: under the person's yes the session is carried on to clear it.
        if (uncommitted > 0)
        {
            return (carried is null
                ? Tried(entry, LandingCode.Uncommitted, tip, status, uncommitted: uncommitted)
                : await CarryOnAsync(entry, carried, LandCarry.Uncommitted, landed: null, tip, status, uncommitted, world, run, ct).ConfigureAwait(false), null);
        }

        // The tip judged is pinned into the landing: a commit made after this judgement is refused, never landed unseen.
        return (null, new Choice(entry, quest, tip, status, run) { Gate = gate.WithTip(tip), Uncommitted = status == Unreadable ? null : uncommitted });
    }

    /// <param name="fresh">The world the choice is judged from, which names the landing and the trees in use.</param>
    private async Task<string> LandOneAsync(Choice choice, IWorkplaceLandingWorld fresh, CancellationToken ct)
    {
        var entry = choice.Entry;
        using var look = trees.OneLook();
        using var facts = GitFacts.Within();

        // The content proof, read at that moment: work no branch of the person's holds. A graph with a `land` judges it as its `content.holds` step.
        var unlanded = choice.Gate?.Process?.JudgesHolds == false ? null : await trees.UnlandedHereAsync(entry.Tree, ct).ConfigureAwait(false);
        if (unlanded == 0)
        {
            var ahead = await trees.AheadOfLineAsync(entry.Tree, ct).ConfigureAwait(false);
            return Tried(entry, ahead == 0 ? LandingCode.Nothing : LandingCode.Already, choice.Tip, choice.Status);
        }

        var subject = await trees.OwnSubjectAsync(
            entry.Tree, entry.Session, entry.Quest, await fresh.RecordsAsync(ct).ConfigureAwait(false), id => fresh.QuestAsync(id, ct),
            named => events.Openings([named]).GetValueOrDefault(named), choice.Gate?.Process, ct).ConfigureAwait(false);
        var plan = await trees.PlanRuleAsync(entry.Tree, subject, ct, choice.Gate?.Process).ConfigureAwait(false);
        string? judgedAgain = null;
        var carried = CarriedOf(entry, choice.Run);
        async Task<TreeLanding?> JudgedAsync(bool waited, CancellationToken token)
        {
            judgedAgain = await UnderHoldAsync(choice, waited, token).ConfigureAwait(false);
            return judgedAgain is null ? null : new TreeLanding(false, judgedAgain) { Refusal = LandingCode.Refused };
        }

        // A head bound into an operation bringing sessions together lands by its combined Accept, its members brought into its one delivery.
        var operation = TogetherHeadOf(entry.Tree);
        if (operation is not null && operation.Phase != TogetherPhase.Ready)
        {
            return $"landing  session {entry.Session}: brought together with other sessions' work ({operation.Phase}); it lands when the operation is ready.";
        }

        var landed = operation is not null
            ? await trees.LandTogetherAsync(
                operation.Id, subject, await MemberGatesAsync(operation, fresh, ct).ConfigureAwait(false), ct, fresh.InUseAsync, choice.Gate,
                JudgedAsync, fresh.BesideAsync).ConfigureAwait(false)
            : await trees.LandAsync(entry.Tree, subject, ct, fresh.InUseAsync, carried is null ? AcceptedBy.Auto : AcceptedBy.Person, choice.Gate, subject.Joins,
                judged: JudgedAsync, uncommitted: choice.Uncommitted, beside: fresh.BesideAsync).ConfigureAwait(false);
        LandOutcomes.Kept(choice.Run.Unit, choice.Run.Tip, landed);
        if (judgedAgain is not null) return judgedAgain;
        // A landing the session can clear (a conflict, work left uncommitted since) carries it on again under the review's word.
        if (carried is not null && LandCarry.WhyOf(landed) is { } why)
        {
            var (tip, status, paths) = await ReadTreeAsync(entry.Tree, ct).ConfigureAwait(false);
            return await CarryOnAsync(entry, carried, why, landed, tip, status, paths, fresh, choice.Run, ct).ConfigureAwait(false);
        }

        var code = LandTries.CodeOf(landed);
        var told = landed is { Landed: false, Conflict: { } conflict } && await ToldRebaseAsync(fresh, entry, choice.Tip, conflict, ct).ConfigureAwait(false);
        return Tried(entry, code, choice.Tip, choice.Status, landed, plan.Target, unlanded, rebaseTold: told,
            carry: carried is null ? null : LandCarry.Accepted);
    }

    /// <summary>The open operation whose head is <paramref name="tree"/>; null where none, or where the record does not read (the landing's own guard refuses then).</summary>
    private TogetherOperation? TogetherHeadOf(string tree)
    {
        try
        {
            return new TogetherOperations(home).All().FirstOrDefault(each => each.IsOpen && TogetherOperation.SameTree(each.Head.Tree, tree));
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Each member of <paramref name="operation"/> with its gate read as its own Accept reads it, for the head's combined landing.</summary>
    private async Task<IReadOnlyDictionary<string, IWorkplaceGate?>> MemberGatesAsync(TogetherOperation operation, IWorkplaceLandingWorld fresh, CancellationToken ct)
    {
        var gates = new Dictionary<string, IWorkplaceGate?>(StringComparer.Ordinal);
        foreach (var member in operation.Members)
        {
            gates[member.Session] = await trees.GateOfAsync(member.Tree, member.Quest, fresh, member.Session, ct).ConfigureAwait(false);
        }

        return gates;
    }

    /// <summary>
    /// The review's word carrying the session on again: its tree is still dirty, or its landing conflicts. It carries a session on
    /// <see cref="LandCarry.Bound"/> times; past that the yes is taken back, and the session waits for the person's review with the reason.
    /// </summary>
    private async Task<string> CarryOnAsync(
        LandEntry entry, CarriedAccept carried, string why, TreeLanding? landed, string? tip, string status, int paths,
        IWorkplaceLandingWorld world, LandStart run, CancellationToken ct)
    {
        if (carried.Carries >= LandCarry.Bound) return Tried(entry, LandingCode.CarryBound, tip, status, landed, uncommitted: paths, carry: why);

        var words = LandCarry.WordsOf(why, landed?.Conflict, paths);
        var key = CarryKeys.Of(entry.Session, LandCarry.BasisOf(why, tip, status), carried.Carries + 1);
        // Kept before the words go: a session that goes on at once may end its turn before this landing is done.
        if (tip is not null) Flows.KeepRepair(home, HoldsLease, run.Unit, run.Review!, tip, key, carried.Carries + 1, Now());
        if (await LandCarry.TellAsync(world.SayAsync, entry.Session, words, key, ct).ConfigureAwait(false) is not { Kept: true })
        {
            TakeRepair(run);
            return why == LandCarry.Conflicted
                ? Tried(entry, LandingCode.Refused, tip, status, landed)
                : Tried(entry, LandingCode.Uncommitted, tip, status, uncommitted: paths);
        }

        return Tried(entry, LandingCode.CarriedOn, tip, status, landed, uncommitted: paths, carry: why);
    }

    /// <summary>
    /// The rebase words said to the session itself: where its replay onto the line conflicts, the words the press would send go to the session
    /// through the say door as Daoris's, keyed by the session and the tip that conflicted. True where the door kept them.
    /// </summary>
    private static async Task<bool> ToldRebaseAsync(IWorkplaceLandingWorld world, LandEntry entry, string? tip, LandingConflict conflict, CancellationToken ct) =>
        await RebaseTelling.TellAsync(world.SayAsync, entry.Session, tip, conflict, ct).ConfigureAwait(false) is { Kept: true };

    /// <summary>
    /// The choice judged once more under its tree's hold, from a fresh world: a closure graph's submission every time, since a reopen or a later
    /// <c>ready</c> may come at any moment; a plain done only where the hold waited.
    /// </summary>
    /// <returns>The line said where it no longer lands now, already kept as a try; null where it lands.</returns>
    private async Task<string?> UnderHoldAsync(Choice choice, bool waited, CancellationToken ct)
    {
        if (!waited && !QuestResults.Submitted(choice.Quest)) return null;
        var fresh = world.Again();
        var (said, again) = await JudgeAsync(choice.Entry, fresh, new Records(fresh), choice.Run, ct).ConfigureAwait(false);
        if (again is not null && QuestResults.Same(choice.Quest, again.Quest)) return null;
        var entry = choice.Entry;
        return said ?? $"landing  session {entry.Session} (#{entry.Quest} → {entry.Repository}): "
            + (again is null
                ? "what it was chosen on moved while it waited to land, so nothing was landed: the next pass judges it again."
                : "its quest's submission was made again while it waited to land, so nothing was landed: the next pass judges the new one.");
    }

    private static SessionRecord? RecordOf(IReadOnlyList<SessionRecord> records, string session) =>
        records.FirstOrDefault(record => string.Equals(record.Id, session, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A second opinion holding a landing, owed where its gate is read, for the session whose work would land, unless one is owed there already.
    /// Never throws: a due list that cannot be written leaves the person's ask, which the gate's sentence names.
    /// </summary>
    /// <param name="since">The done it is owed for: a due the loop closed as landed since then is not opened again. Null opens one whatever closed the last.</param>
    private void Owe(LandEntry entry, string session, string quest, string tree, DateTimeOffset? since = null)
    {
        WorkplaceReview.Current.OweLanding(home, session, quest, entry.Repository, entry.Workspace, tree, Now, since);
    }

    /// <summary>Keep one try: in memory with its facts, in the conversation where its code is said there, and in the machine log by its code.</summary>
    /// <param name="named">The branch the pattern names, which a refusal's note names where the landing made none.</param>
    /// <param name="rebaseTold">Its replay conflicted and its session has been told to rebase, which its note says.</param>
    /// <param name="carry">What the review's word is carried on for, or that it accepted the landing (<see cref="LandTryNotes.Of"/>).</param>
    private string Tried(
        LandEntry entry, string code, string? tip, string? status, TreeLanding? landed = null,
        string? named = null, int? commits = null, int? uncommitted = null, bool rebaseTold = false, string? carry = null)
    {
        var made = landed is { Landed: true } ? landed.Branch : null;
        Tries[$"{home}\n{entry.Session}"] = new AutoTry(Now(), code)
        {
            Tip = tip,
            Status = LandingCode.WaitsOnRoot(code) ? landed?.RootStatus : status,
            Uncommitted = uncommitted,
            Branch = made ?? (code is LandingCode.Exists or LandingCode.Completed or LandingCode.Already ? named : null),
            Commits = made is null ? null : commits,
        };

        var line = Words(code, landed);
        if (LandTryNotes.Says(code))
        {
            var note = LandTryNotes.Of(code, landed, uncommitted, named, rebaseTold, carry);
            events.Keep(entry.Session, note, say: null);
            line = note.Text!;
        }

        log?.Invoke(LandingLine.Auto(entry.Session, entry.Repository, entry.Workspace, code, made is null ? null : commits, uncommitted));
        if (code is LandingCode.Landed or LandingCode.Advanced) Told?.Invoke(new WorkEvent(WorkEventKinds.WorkLanded, Now()) { Session = entry.Session, Quest = entry.Quest, Code = code });
        return $"landing  session {entry.Session} (#{entry.Quest} → {entry.Repository}): {line}";
    }

    private string Closed(LandEntry entry, string code, string? branch = null) =>
        Tried(entry, code, tip: null, status: null, named: branch);

    /// <summary>
    /// What the run's graph holds, or a partner's record that lands nothing (<see cref="PieceLandings"/>): a refusal kept once, with no tip and no
    /// status, so the next pass reads the gate again whatever the tree does.
    /// </summary>
    /// <returns>The line said, or null where it is said already.</returns>
    private string? Workflow(LandEntry entry, TreeLanding held, LandingLine? line)
    {
        if (entry.Last is { Code: LandingCode.Refused, Tip: null, Status: null }) return null;
        var said = Tried(entry, LandingCode.Refused, tip: null, status: null, held);
        if (line is not null) log?.Invoke(line);
        return said;
    }

    /// <summary>
    /// The landing gate read for a landing, its first part that holds as a refusal (<see cref="IWorkplaceGate.Refusal"/>), and the machine log's
    /// line for that part. The judgement, the push and a push held back's recovery read it alike.
    /// </summary>
    /// <param name="session">The session whose work would land: the run's, or a later one whose done moved its branch on.</param>
    /// <param name="quest">That session's quest, whose chain the opinion and the review read; null reads no part that needs one.</param>
    /// <param name="process">The run's process where the caller read it already; otherwise the gate reads it, once.</param>
    /// <param name="tip">The commit to judge the work at, where a push is judged; null reads the tree's head.</param>
    private async Task<(IWorkplaceGate Gate, TreeLanding? Held, LandingLine? Line)> GateAsync(
        LandEntry entry, string session, string? quest, string tree, IWorkplaceLandingWorld world, CancellationToken ct,
        IWorkplaceProcess? process = null, string? tip = null)
    {
        var gate = await trees.GateOfAsync(tree, quest, world, session, ct, process, tip).ConfigureAwait(false);
        return gate.Refusal is { } held
            ? (gate, held, gate.Held(session, (entry.Workspace, entry.Repository), held, LandingDoors.Look))
            : (gate, null, null);
    }

    /// <summary>
    /// The yes a run's review stands for while its landing is judged: the person's Accept, with the carries its repair counts; null where the
    /// graph holds no review, whose landing is the standing authority's.
    /// </summary>
    private CarriedAccept? CarriedOf(LandEntry entry, LandStart run) => run.Review is null
        ? null
        : new CarriedAccept(entry.Session, entry.Tree, Now(), Flows.RepairCarries(home, HoldsLease, run.Unit, run.Review), Awaiting: false);

    /// <summary>What the line says of a code the conversation is not told of.</summary>
    private static string Words(string code, TreeLanding? landed) => code switch
    {
        LandingCode.Already => "its work is already on a branch of yours, so nothing more lands.",
        LandingCode.Superseded => "a newer session went on in its tree, which stands for it now.",
        LandingCode.Gone => "its tree was discarded before it landed, so nothing lands.",
        LandingCode.Undone => "its quest is no longer done, so nothing lands.",
        _ => landed?.Message ?? code,
    };

    private const string MovedWords = "the work moved since it was judged; press Accept again.";

    /// <summary>The status a try is kept with where git could not give one.</summary>
    private const string Unreadable = "unreadable";

    /// <summary>The tree's HEAD and status, as a try is kept with them, and how many paths are uncommitted.</summary>
    internal static async Task<(string? Tip, string Status, int Uncommitted)> ReadTreeAsync(string tree, CancellationToken ct)
    {
        var (headCode, head, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--verify", "--quiet", "HEAD^{commit}"], ct).ConfigureAwait(false);
        var (statusCode, porcelain, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
        var tip = headCode == 0 ? head.Trim() : null;
        if (statusCode != 0) return (tip, Unreadable, 0);
        var status = LandTries.Fingerprint(porcelain);
        return (tip, status, porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length);
    }

    /// <summary>The checkout's status where the entry's last try waits on it; null otherwise, and where git could not say.</summary>
    private static async Task<string?> RootStatusAsync(LandEntry entry, CancellationToken ct)
    {
        if (entry.Last is not { Code: var code } || !LandingCode.WaitsOnRoot(code)) return null;
        var (commonCode, common, _) = await WorkingTree.GitAsync(entry.Tree, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        if (commonCode != 0 || Path.GetDirectoryName(common.Trim()) is not { Length: > 0 } root) return null;
        var (statusCode, porcelain, _) = await WorkingTree.GitAsync(root, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
        return statusCode == 0 ? LandTries.Fingerprint(porcelain) : null;
    }

    private DateTimeOffset Now() => clock?.Invoke() ?? DateTimeOffset.UtcNow;

    private static bool SameTree(string left, string right) =>
        string.Equals(TreeNames.Normal(left), TreeNames.Normal(right), StringComparison.OrdinalIgnoreCase);
}
