namespace Daoris.Driver;

/// <summary>
/// What the landing at done reads of the service (LAND2b): a seam, so its tests stand in for the ledger. Its quests and asks are
/// what the review's gate reads too (REVIEWENV1c), and the opinions the second opinion's part reads (XAGENT1f), so a look lands
/// nothing the gate holds.
/// </summary>
public interface IAutoLandingWorld : IOpinionWorld
{
    /// <summary>One quest as it stands, closed ones included; null where the service holds none.</summary>
    Task<QuestView?> QuestAsync(string id, CancellationToken ct);

    /// <summary>Every session record, closed ones included: which is the newest on a tree, and whether a live one holds it.</summary>
    Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct);

    /// <summary>The trees sessions still running or waiting name, which the rule's tidy keeps (LAND3).</summary>
    Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct);

    /// <summary>
    /// A world that reads the service afresh (FABRIC-END7's review): each chosen entry is judged again from it just before its own
    /// landing. A world that keeps nothing between reads answers itself.
    /// </summary>
    IAutoLandingWorld Again() => this;
}

/// <summary>The world over this machine's service: the quests read once per instance, since a look builds one.</summary>
public sealed class ServiceAutoLandingWorld(ServiceClient service) : IAutoLandingWorld
{
    private IReadOnlyList<QuestView>? _every;
    private IReadOnlyDictionary<string, QuestView>? _quests;

    public async Task<QuestView?> QuestAsync(string id, CancellationToken ct)
    {
        _quests ??= (await QuestsAsync(ct).ConfigureAwait(false))
            .GroupBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        return _quests.GetValueOrDefault(id.TrimStart('#'));
    }

    public async Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) =>
        _every ??= await service.EveryQuestAsync(ct).ConfigureAwait(false);

    public Task<AskView?> AskAsync(string id, CancellationToken ct) => service.FindAskAsync(id, ct);

    public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => service.Opinions.ReadAsync(id, ct);

    public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => service.SessionRecordsAsync(ct);

    public async Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) =>
        (await service.ActiveSessionsAsync(ct).ConfigureAwait(false))
        .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>A new world over the same service, so nothing this one read is kept.</summary>
    public IAutoLandingWorld Again() => new ServiceAutoLandingWorld(service);
}

/// <summary>A due session the look chose to try, with what it read of it there: its quest, and its tree's tip and status.</summary>
public sealed record AutoChoice(AutoLanding Entry, QuestView Quest, string? Tip, string Status)
{
    /// <summary>
    /// The landing gate as the look read it (XAGENT1f, REVIEWENV1c): the second opinion and the review, each letting it go, which
    /// its landing record keeps.
    /// </summary>
    public LandingGate? Gate { get; init; }

    /// <summary>
    /// The branch its landing made and recorded and whose push was held back (FABRIC-CLOSE1h2), which this try hands on through the
    /// hand-off rather than landing again; null for a landing.
    /// </summary>
    public LandedBranch? Hands { get; init; }
}

/// <summary>What the look chose to land beside it, and what it said of the rest as it looked.</summary>
public sealed record AutoChosen(IReadOnlyList<AutoChoice> Chosen, IReadOnlyList<string> Said);

/// <summary>
/// Done work lands itself (LAND2b, D145 point 2, design §2): each session the due list holds is landed at a look, exactly as
/// the review's Accept lands it, under a rule that accepts automatically.
/// </summary>
/// <remarks>
/// <para><b>Two halves.</b> <see cref="ChooseAsync"/> is the look's own: it reads the rule, the records, the quest and the tree's
/// tip and status, closes what can no longer land, keeps a hold, and chooses what is worth a try, with no landing and no
/// plugin, so a look that chooses nothing never waits on one. <see cref="LandAsync"/> runs beside the look, as a start does
/// (DEV3), so a plugin's two minutes (D100) never hold one: D88's proof read at that moment, then
/// <see cref="SessionTrees.LandAsync"/> — the branch, its record, the rule's plugin and its tidy — as the press runs it.</para>
///
/// <para><b>Nothing here pushes, merges or writes a quest.</b> The landing is the press's own act with the person's standing
/// say-so on the rule (D145 point 5): a merge never accepts automatically, the push is the plugin's (D37, D100), and a landing
/// writes no quest state (D46).</para>
///
/// <para><b>A refusal is not tried every look.</b> Each try keeps the tree's tip and status it was made at, and the next is
/// made only once either moves (<see cref="AutoLandingRules.ShouldTry"/>), or the person's press lands it, which the look
/// then reads as <see cref="AutoLandingCode.Already"/>.</para>
///
/// <para><b>A chain lands on one branch</b> (LAND2c, D149): a later step's done in the same repository moves the chain's branch
/// on as <see cref="AutoLandingCode.Advanced"/>, and so does a session's done after words went on with it past its landing
/// (D137), each through the press's own path. One whose pull request was merged waits as
/// <see cref="AutoLandingCode.Completed"/>.</para>
/// </remarks>
/// <param name="log">Where a try's <c>landing.auto</c> line goes (D94): the service client's watchers, which write the machine log.</param>
public sealed class AutoLander(
    string home, IAutoLandingWorld world, SessionTrees trees, SessionEvents events, Action<LandingLine>? log = null,
    Func<DateTimeOffset>? clock = null)
{
    /// <summary>The due list this lander works.</summary>
    public AutoLandings Due { get; } = new(home);

    /// <summary>
    /// The look's half: every open entry read against its rule, its records, its quest and its tree. What can no longer land is
    /// closed and said, a hold is kept once, uncommitted work is a try of its own, and the rest that is worth a try is chosen.
    /// </summary>
    /// <param name="woke">
    /// The wakes a plan was woken by (FABRIC-END7): only the entries they can release are read (<see cref="LandingConcerns"/>), found
    /// without git, so a wake that concerns none starts no process. Null, the whole look, reads every entry: the deadline.
    /// </param>
    public async Task<AutoChosen> ChooseAsync(DriverConfig config, CancellationToken ct = default, IReadOnlyCollection<LandingWake>? woke = null)
    {
        var open = Due.Open();
        // FABRIC-END7b: what the looks kept of an entry no longer due goes with it.
        LandingJudged.Keep(home, open);
        if (open.Count == 0) return new([], []);

        var said = new List<string>();
        var chosen = new List<AutoChoice>();
        var records = new LookRecords(world);
        foreach (var entry in open)
        {
            if (!LandingConcerns.Concerns(entry, woke, TreeTips.Read, leftLive: each => LandingJudged.LeftLive(home, each))) continue;
            var (line, choice) = await JudgeAsync(entry, config, world, records, ct).ConfigureAwait(false);
            if (line is not null) said.Add(line);
            if (choice is not null) chosen.Add(choice);
        }

        return new(chosen, said);
    }

    /// <summary>The service's session records, read once for a look's entries, and only once one needs them.</summary>
    private sealed class LookRecords(IAutoLandingWorld world)
    {
        private IReadOnlyList<SessionRecord>? _read;

        public async Task<IReadOnlyList<SessionRecord>> ReadAsync(CancellationToken ct) =>
            _read ??= await world.RecordsAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// One entry judged against its rule, its records, its quest, its tree and its gate, as they stand now: the look's line for it where
    /// one is said, and the choice where it is worth a try. The look judges with it, and the landing again just before each landing.
    /// </summary>
    private async Task<(string? Said, AutoChoice? Choice)> JudgeAsync(
        AutoLanding entry, DriverConfig config, IAutoLandingWorld world, LookRecords looked, CancellationToken ct)
    {
        // The switch is read as it stands now: switched off, the person's press is the way again. Under a named workflow it is
        // its run's bound version's landing step (WORKFLOW1f), read once here and handed to the gate below; a workflow that
        // cannot be read here holds the entry at the gate, never closes it as off.
        var process = await WorkflowProcesses.ReadAsync(home, config, world, entry.Quest, entry.Repository, entry.Workspace, ct)
            .ConfigureAwait(false);
        // FABRIC-END7b: the run whose binding the gate reads, which the gate's files' watch lists by name.
        LandingJudged.Read(home, entry, process.Binding?.Run);
        var rule = process.Landing.Rule;
        if (process is { Problem: null, Unread: null } && rule is not { Form: LandingForm.Branch, AutoAccept: true })
        {
            return (Closed(entry, AutoLandingCode.Off), null);
        }

        // FABRIC-CLOSE1h2: its branch is made and recorded and its push was held back, so it is handed on, never made again.
        if (entry.Last is { Code: AutoLandingCode.PushHeld } pushHeld
            && await PushHeldAsync(entry, pushHeld, world, looked, process, ct).ConfigureAwait(false) is { } handOn)
        {
            return handOn;
        }

        // Landed since another way: the person's press lands it at once, whatever the last try was (design §2). A press that
        // moved a chain's branch on lands it too, so its own acceptance is what is compared (LAND2c).
        var since = entry.Last is { } last && last.At > entry.DueAt ? last.At : entry.DueAt;
        if (trees.Recorded.Landing(entry.Session) is { } landing && landing.AcceptedAt(entry.Session) >= since)
        {
            return (Closed(entry, AutoLandingCode.Already, branch: landing.Branch), null);
        }

        if (TreeFolders.TreeGone(entry.Tree)) return (Closed(entry, AutoLandingCode.Gone), null);

        // ReviewableTree's rule: a live session in the tree is using it, and a newer session on it stands for it now.
        var records = await looked.ReadAsync(ct).ConfigureAwait(false);
        var onTree = records.Where(record => !record.Teammate && record.Tree is { } tree && SameTree(tree, entry.Tree)).ToList();
        // FABRIC-END7b: left with no try, so the session's end concerns it whatever its last try waits on (LandingConcerns).
        var live = onTree.Any(record => record.Live);
        LandingJudged.Live(home, entry, live);
        if (live) return (null, null);
        var newest = onTree.OrderByDescending(record => record.Created).ThenByDescending(record => record.Id, StringComparer.Ordinal).FirstOrDefault();
        if (newest is not null && !string.Equals(newest.Id, entry.Session, StringComparison.OrdinalIgnoreCase))
        {
            return (Closed(entry, AutoLandingCode.Superseded), null);
        }

        // FABRIC-CLOSE1h (the fabric design §E5): in a closure graph Daoris writes no Done before the graph's end, so the landing waits on
        // the quest's current submission where it waits on a done, with every hold, opinion and look below still applying. A reopen takes
        // the submission back, and closes the entry as a done turned down does; the agent's next `ready` makes its session due again.
        var quest = await world.QuestAsync(entry.Quest, ct).ConfigureAwait(false);
        if (!QuestResults.Judged(quest)) return (Closed(entry, AutoLandingCode.Undone), null);

        // Held for the person's yes (D133) or evidence's verdict (D144): kept once, and read again at every look. A set-up step
        // held for its review alone is the gate's below, which says what lets it go (REVIEWENV1c). A submission's holds are the same (§E5).
        if (quest is { Held: true } && quest.Hold != EvidenceCodes.Unreviewed)
        {
            return (entry.Last?.Code != AutoLandingCode.Held ? Tried(entry, AutoLandingCode.Held, tip: null, status: null) : null, null);
        }

        var (tip, status, uncommitted) = await ReadTreeAsync(entry.Tree, ct).ConfigureAwait(false);
        if (!AutoLandingRules.ShouldTry(entry, tip, status)) return (null, null);

        // The landing gate, read once (XAGENT1f, the second-agent design §7), with the process read above (WORKFLOW1f), by the one
        // helper the push and a push held back's recovery read it with (FABRIC-CLOSE1h12). Its first part that holds, in §7's order:
        var (gate, held, line) = await GateAsync(entry, entry.Session, entry.Quest, entry.Tree, world, ct, process).ConfigureAwait(false);

        // The run's own part first (WORKFLOW1f, the workflow design §3.7, §4.4): a workflow that cannot be read here, or a kind's
        // paths, holds it, said once; and the named workflow's landing step, last before the landing: one that cannot start sits. Kept
        // with no tip and no status, so the next look reads the gate again, and lands it once nothing holds it: what moves it is a
        // declaration or the person's say-so, never the tree.
        if (held is { Refusal: AutoLandingCode.Refused }) return (Workflow(entry, held, line), null);

        // The second opinion. Where the level says one is read before landing, the session stays due as `opinion`, said once and
        // read again at every look, until it is settled or the person answers it; a chain step whose chain has a later step here
        // waits for that step (§8.1). The review's gate (REVIEWENV1c, design §3.1): where the level says review, the session stays
        // due as `unreviewed`, said once and read again at every look, until the person's reviewed on a set-up that holds its tip,
        // or their skip.
        if (held is { Refusal: { } waiting })
        {
            // FABRIC-CLOSE1h17: the opinion holding it is owed here, as a push held back's recovery owes it (FABRIC-CLOSE1h15), so the
            // loop's opinion look asks it as the gate says, also where the rule started reading after the done. Read at every look that
            // reaches the gate, so an entry already kept as `opinion` is owed too.
            if (waiting == AutoLandingCode.Opinion) Owe(entry, entry.Session, entry.Quest, entry.Tree, since: entry.DueAt);
            if (entry.Last?.Code == waiting) return (null, null);
            var waits = Tried(entry, waiting, tip, status, held);
            if (line is not null) log?.Invoke(line);
            return (waits, null);
        }

        // Uncommitted work would not travel with the branch (design §5): it stays to review, with the count of its paths.
        if (uncommitted > 0) return (Tried(entry, AutoLandingCode.Uncommitted, tip, status, uncommitted: uncommitted), null);

        // The tip judged is pinned into the landing (FABRIC-END7's review): a commit made after this judgement is refused, never landed unseen.
        return (null, new AutoChoice(entry, quest, tip, status) { Gate = gate with { Tip = tip } });
    }

    /// <summary>
    /// The half beside the look: each chosen session landed as the review's Accept lands it, behind D88's proof read at that
    /// moment, and each try kept, said in the conversation and logged. Never throws for one session's sake.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Each is judged again just before its own landing</b> (FABRIC-END7's review, finding 1): a pass lands its choices one after
    /// another, and an earlier one's plugin may take minutes, so the quest, the holds, the records, the switch, the tree and the gate
    /// the look read are read again, from a fresh world, and what moved since is said with the look's own words. The gate handed to
    /// the landing is the one read here, pinned to the tip read here (<see cref="LandingGate.Tip"/>). It costs the tree's two reads
    /// and the gate's own again for each landing, where the look read them a moment before.
    /// </remarks>
    public async Task<IReadOnlyList<string>> LandAsync(IReadOnlyList<AutoChoice> chosen, CancellationToken ct = default)
    {
        var said = new List<string>();
        foreach (var choice in chosen)
        {
            try
            {
                var (line, again, fresh) = await AgainAsync(choice, ct).ConfigureAwait(false);
                if (line is not null) said.Add(line);
                if (again is not null) said.Add(await LandOneAsync(again, fresh, ct).ConfigureAwait(false));
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                              or IOException or UnauthorizedAccessException)
            {
                // Kept as a refusal at the tip and status it was tried at, so a look tries it again only once its tree moves,
                // or the person's press lands it: an error that repeats never makes every look a try.
                try
                {
                    Due.Tried(choice.Entry.Session, new AutoTry(Now(), AutoLandingCode.Refused) { Tip = choice.Tip, Status = choice.Status }, close: false);
                }
                catch (Exception unkept) when (unkept is IOException or UnauthorizedAccessException)
                {
                    // The due list does not write either: the line below says what happened.
                }

                said.Add($"landing  session {choice.Entry.Session} (#{choice.Entry.Quest} → {choice.Entry.Repository}): could not be "
                    + $"tried: {error.Message.TrimEnd().TrimEnd('.')}. It is tried again once its tree changes, or your Accept lands it.");
            }
        }

        return said;
    }

    /// <summary>
    /// The choice judged again from what stands now (FABRIC-END7's review): the entry as the due list holds it, the rule as the home's
    /// configuration says it, and the service read afresh. Null where it is no longer the due list's to land, or no longer worth a try.
    /// </summary>
    private async Task<(string? Said, AutoChoice? Choice, IAutoLandingWorld World)> AgainAsync(AutoChoice choice, CancellationToken ct)
    {
        var fresh = world.Again();
        if (Due.Of(choice.Entry.Session) is not { Closed: null } entry) return (null, null, fresh);
        var config = DriverConfig.Load(DriverConfig.ResolvePath(home));
        var (said, again) = await JudgeAsync(entry, config, fresh, new LookRecords(fresh), ct).ConfigureAwait(false);
        return (said, again, fresh);
    }

    /// <param name="fresh">The world the choice was judged again from, which names the landing and the trees in use.</param>
    private async Task<string> LandOneAsync(AutoChoice choice, IAutoLandingWorld fresh, CancellationToken ct)
    {
        // FABRIC-CLOSE1h2: a branch made whose push was held back is handed on, never landed again.
        if (choice.Hands is { } made) return await HandOnAsync(choice, made, ct).ConfigureAwait(false);
        var entry = choice.Entry;
        // LANDGIT1: the try's reads before the landing and the landing's own ask git's guess at the line once; the look the landing
        // opens is this one's. The table of what ids answer is the planning's too, closed by the landing before the tidy.
        using var look = trees.OneLook();
        using var facts = GitFacts.Within();

        // D88's proof, read at that moment (design §2): work no branch of the person's holds. None is a done that made no
        // commits, or work a branch of theirs already holds, or a squashing landing's record does (PROCESS1b-ii).
        var unlanded = await trees.UnlandedHereAsync(entry.Tree, ct).ConfigureAwait(false);
        if (unlanded == 0)
        {
            var ahead = await trees.AheadOfLineAsync(entry.Tree, ct).ConfigureAwait(false);
            return Tried(entry, ahead == 0 ? AutoLandingCode.Nothing : AutoLandingCode.Already, choice.Tip, choice.Status);
        }

        // Named for the chain's first quest (WSR5), as the press names it, and a part of an entry with others for its entry's first
        // part, on the entry's one branch, joining it where it ran alongside (ACCEPTALL1f, D167's 2026-10-15 amendment point 2): the
        // press's own call, over the records as they stand now.
        var subject = await trees.OwnSubjectAsync(
            entry.Tree, entry.Session, entry.Quest, await fresh.RecordsAsync(ct).ConfigureAwait(false), id => fresh.QuestAsync(id, ct),
            named => events.Openings([named]).GetValueOrDefault(named), choice.Gate?.Process, ct).ConfigureAwait(false);
        var plan = await trees.PlanAsync(entry.Tree, subject, ct, choice.Gate?.Process).ConfigureAwait(false);
        string? judgedAgain = null;
        var landed = await trees.LandAsync(entry.Tree, subject, ct, fresh.InUseAsync, AcceptedBy.Auto, choice.Gate, subject.Joins,
            judged: async (waited, token) =>
            {
                judgedAgain = await UnderHoldAsync(choice, waited, token).ConfigureAwait(false);
                return judgedAgain is null ? null : new TreeLanding(false, judgedAgain) { Refusal = AutoLandingCode.Refused };
            },
            beforePush: (record, waited, token) => BeforePushAsync(choice, record, waited, token)).ConfigureAwait(false);
        // Judged again under the tree's hold and refused there: what that judgement kept is the try, so nothing more is kept for it.
        if (judgedAgain is not null) return judgedAgain;
        var code = AutoLandingRules.CodeOf(landed);
        return Tried(entry, code, choice.Tip, choice.Status, landed, plan.Plugin, plan.Target, unlanded);
    }

    /// <summary>
    /// The choice judged once more under its tree's hold (FABRIC-CLOSE1h, the fabric design §E5): what the look and the judgement just
    /// before the landing read may have moved while it waited behind another landing of the tree (LANDRACE1's wait is bounded only at
    /// minutes), so the quest, its submission, its holds, the switch, the opinion and the look are read again from a fresh world. A
    /// closure graph's submission is judged again every time, since the person's reopen or a later <c>ready</c> may come at any moment
    /// before the landing commits; a plain done only where the hold waited, so a landing that waited for nothing reads no more than it did.
    /// </summary>
    /// <returns>The look's line where it no longer lands now, already kept as its judgement keeps it; null where it lands.</returns>
    private async Task<string?> UnderHoldAsync(AutoChoice choice, bool waited, CancellationToken ct)
    {
        if (!waited && !QuestResults.Submitted(choice.Quest)) return null;
        var (said, again, _) = await AgainAsync(choice, ct).ConfigureAwait(false);
        if (again is not null && QuestResults.Same(choice.Quest, again.Quest)) return null;
        var entry = choice.Entry;
        // A later `ready` while it waited is another submission, judged again from the start (§E5): nothing is kept, so the next look
        // judges the one standing then, and lands it where nothing holds it.
        return said ?? $"landing  session {entry.Session} (#{entry.Quest} → {entry.Repository}): "
            + (again is null
                ? "what it was chosen on moved while it waited to land, so nothing was landed: the next look judges it again."
                : "its quest's submission was made again while it waited to land, so nothing was landed: the next look judges the new one.");
    }

    /// <summary>
    /// The choice's quest judged once more just before its plugin is told (FABRIC-CLOSE1h2, D173's note: a hold stops a landing up to the
    /// moment its push begins). Its branch is made and recorded by now, so only what can still stop the push is read: the quest afresh,
    /// for a hold, a reopen or a later submission. A closure graph's submission is read every time; a plain done only where the push
    /// waited behind another push of its branch, so a landing that waited for nothing reads nothing more (LANDGIT1's counts stand).
    /// </summary>
    /// <param name="landing">
    /// The branch's record as it reads under the branch's hold, null where none was recorded. Its last session (FABRIC-CLOSE1h10, D173's
    /// note) is another than the choice's where a later session's done moved it on since the look chose it, and then that session's quest
    /// is judged instead, read every time, by the same rule (taken back or held stops the push). FABRIC-CLOSE1h11 (D173's CLOSELAND1
    /// note): every earlier session whose work the push carries unpushed is judged too, on its quest alone, read every time.
    /// </param>
    /// <returns>Why the push is held back, as the landing's sentence says it; null where it goes on.</returns>
    /// <remarks>
    /// FABRIC-CLOSE1h12 (D173's note): where the quest is read again, and stands, the landing's gate is read again too, for the same
    /// session, its quest and its tree (<see cref="GateAsync"/>), so an opinion, a review or the run's workflow that started holding
    /// after the look chose the landing holds its push back as a hold does. The review's own <c>unreviewed</c> hold is the gate's to
    /// judge, read here. Where the quest is not read again, neither is the gate, so an uncontended landing of a plain done reads nothing
    /// more, unless its push carries an earlier session's unpushed work. An earlier session's gate is not read again: its work went by its
    /// gate when it landed, and the pushing session's gate reads its chain's work.
    /// </remarks>
    private async Task<string?> BeforePushAsync(AutoChoice choice, LandedBranch? landing, bool waited, CancellationToken ct)
    {
        var entry = choice.Entry;
        var last = landing?.LastSession() ?? entry.Session;
        var later = !string.Equals(last, entry.Session, StringComparison.OrdinalIgnoreCase);
        var earlier = Earlier(landing, entry.Session, last);
        if (!later && earlier.Count == 0 && !waited && !QuestResults.Submitted(choice.Quest)) return null;
        var fresh = world.Again();
        IReadOnlyList<SessionRecord>? records = null;
        async Task<SessionRecord?> RecordAsync(string session) =>
            RecordOf(records ??= await fresh.RecordsAsync(ct).ConfigureAwait(false), session);

        if (later || waited || QuestResults.Submitted(choice.Quest))
        {
            // A later session on no quest holds the push to nothing, as at the person's door: its work landed by the person's own press.
            var record = later ? await RecordAsync(last).ConfigureAwait(false) : null;
            if ((later ? record?.Quest : entry.Quest) is { } id)
            {
                var quest = await fresh.QuestAsync(id, ct).ConfigureAwait(false);
                var why = !QuestResults.Judged(quest) ? "its quest was taken back"
                    : !later && !QuestResults.Same(choice.Quest, quest) ? "its quest's submission was made again"
                    : quest is { Held: true } && quest.Hold != EvidenceCodes.Unreviewed ? "its quest was held for your yes"
                    : null;
                if (why is not null)
                {
                    return $"{why} before its plugin was told, so its push was not tried: the branch stands, and the first look once its quest "
                        + $"stands again hands it on, or `daoris-driver trees hand {last}` does now";
                }

                // FABRIC-CLOSE1h16: the choice's own work is judged at the commit its branch was judged and made at (LandingGate.Tip, the
                // try's tip for a hand-on), so a commit made on its tree since, which this push does not carry, holds nothing back. A later
                // session's tree is read at its head, as before.
                var (_, held, line) = await GateAsync(entry, last, id, record?.Tree ?? entry.Tree, fresh, ct, tip: later ? null : choice.Gate?.Tip ?? choice.Tip)
                    .ConfigureAwait(false);
                if (held is not null)
                {
                    if (line is not null) log?.Invoke(line);
                    return "its landing's gate held it before its plugin was told, so its push was not tried: the branch stands, and the first "
                        + $"look once the gate lets it go hands it on, or `daoris-driver trees hand {last}` does now. {held.Message}";
                }
            }
        }

        // FABRIC-CLOSE1h11: the earlier sessions' work this push carries, which no push carried yet (an earlier landing's push held back,
        // or advances after the commit last pushed), each by the look's rule on its quest: taken back or held stops it, naming which.
        foreach (var work in earlier)
        {
            if ((work.Quest ?? (await RecordAsync(work.Session).ConfigureAwait(false))?.Quest) is not { } carried) continue;
            var id = carried.TrimStart('#');
            var quest = await fresh.QuestAsync(id, ct).ConfigureAwait(false);
            var why = !QuestResults.Judged(quest) ? "was taken back"
                : quest is { Held: true } && quest.Hold != EvidenceCodes.Unreviewed ? "was held for your yes"
                : null;
            if (why is not null)
            {
                return $"session {work.Session}'s quest #{id}, whose work this push carries, {why} before its plugin was told, so its push was "
                    + $"not tried: the branch stands, and the first look once that quest stands again hands it on, or "
                    + $"`daoris-driver trees hand {last}` does now";
            }
        }

        return null;
    }

    /// <summary>
    /// The earlier sessions whose work a push of <paramref name="landing"/> carries unpushed (FABRIC-CLOSE1h11): every one besides the
    /// choice's own and the branch's last, which are judged on their own rules. None where no record was read.
    /// </summary>
    private static IReadOnlyList<LandedWork> Earlier(LandedBranch? landing, string own, string last) =>
        landing is null ? []
        : [.. landing.UnpushedBesides(own).Where(work => !string.Equals(work.Session, last, StringComparison.OrdinalIgnoreCase))];

    /// <summary>A session's record, null where the records hold no such session.</summary>
    private static SessionRecord? RecordOf(IReadOnlyList<SessionRecord> records, string session) =>
        records.FirstOrDefault(record => string.Equals(record.Id, session, StringComparison.OrdinalIgnoreCase));

    /// <summary>A quest the look would land on now: its result in, and no hold for the person's yes but the review's own.</summary>
    private static bool Stands([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] QuestView? quest) =>
        quest is not null && QuestResults.Judged(quest) && !(quest.Held && quest.Hold != EvidenceCodes.Unreviewed);

    /// <summary>
    /// A due entry whose push was held back after its branch was made (FABRIC-CLOSE1h2): its quest read as the look reads a hold, then
    /// its tree's tip. While its quest is held, taken back or not submitted again, it waits, said once already. Once it stands, a tree
    /// still at the commit its landing judged is handed on (<see cref="TreeHandOffs"/>): one push of the branch as made, never a second
    /// branch. A tree whose work went on past it is landed as any later done of the session is (null here), which moves the made
    /// branch on and pushes the whole once. Where a later session's done moved the branch on since (FABRIC-CLOSE1h10), its work rides
    /// the hand-on, so that session's quest is read too and must stand as well.
    /// </summary>
    /// <remarks>
    /// FABRIC-CLOSE1h12 (D173's note): the landing's gate is read again before a hand-on is chosen, for the session whose work the push
    /// carries, so a gate that started holding while the push was held back, or before a restart, holds the hand-on as the push would.
    /// Held there, it waits as the quest's wait does, said once already at the push. FABRIC-CLOSE1h15: held by a second opinion, that
    /// opinion is owed (<see cref="Owe"/>), so the loop asks it rather than the person alone.
    /// </remarks>
    /// <param name="process">The run's process the look read for the entry's quest (WORKFLOW1f), handed to the gate where it is read for it.</param>
    private async Task<(string? Said, AutoChoice? Choice)?> PushHeldAsync(
        AutoLanding entry, AutoTry held, IAutoLandingWorld world, LookRecords looked, WorkflowProcess process, CancellationToken ct)
    {
        var quest = await world.QuestAsync(entry.Quest, ct).ConfigureAwait(false);
        if (!Stands(quest)) return (null, null);

        // Handed on meanwhile by the person's own `trees hand`, or its record gone with its branch: nothing is handed on twice.
        if (trees.Recorded.Landing(entry.Session) is not { } landing
            || landing is { Pushed: true } && string.Equals(landing.PushedTip, landing.Tip, StringComparison.OrdinalIgnoreCase))
        {
            return (Closed(entry, AutoLandingCode.Already, branch: held.Branch), null);
        }

        // FABRIC-CLOSE1h10: a hand-on its later session's quest would hold back again (BeforePushAsync, under the branch's hold) waits
        // instead, said once already, so no look keeps a try of it while that quest is held or taken back.
        var last = landing.LastSession();
        var later = !string.Equals(last, entry.Session, StringComparison.OrdinalIgnoreCase);
        var record = later ? RecordOf(await looked.ReadAsync(ct).ConfigureAwait(false), last) : null;
        if (record?.Quest is { } laterQuest && !Stands(await world.QuestAsync(laterQuest, ct).ConfigureAwait(false))) return (null, null);

        // FABRIC-CLOSE1h11: and every earlier session whose work the hand-on carries unpushed, so a later entry's hand-on never pushes an
        // earlier one's work held back on its quest; it waits as that entry waits, said once already at the push.
        foreach (var work in Earlier(landing, entry.Session, last))
        {
            var carried = work.Quest ?? RecordOf(await looked.ReadAsync(ct).ConfigureAwait(false), work.Session)?.Quest;
            if (carried is not null && !Stands(await world.QuestAsync(carried, ct).ConfigureAwait(false))) return (null, null);
        }

        var (tip, status, _) = await ReadTreeAsync(entry.Tree, ct).ConfigureAwait(false);
        if (!string.Equals(tip, held.Tip, StringComparison.OrdinalIgnoreCase)) return null;

        // FABRIC-CLOSE1h12: the gate, for the session whose work the hand-on pushes, as BeforePushAsync reads it: a later session on no
        // quest, whose push is held to nothing, reads no part that needs one.
        var (session, sessionQuest, sessionTree) = later ? (last, record?.Quest, record?.Tree ?? entry.Tree) : (entry.Session, entry.Quest, entry.Tree);
        var (_, gateHeld, _) = later
            ? await GateAsync(entry, session, sessionQuest, sessionTree, world, ct).ConfigureAwait(false)
            : await GateAsync(entry, session, sessionQuest, sessionTree, world, ct, process).ConfigureAwait(false);
        if (gateHeld is not null)
        {
            // FABRIC-CLOSE1h15: a second opinion holding it is owed here, so the loop's opinion look asks it, as the gate says it will.
            if (gateHeld.Refusal == AutoLandingCode.Opinion && sessionQuest is not null) Owe(entry, session, sessionQuest, sessionTree);
            return (null, null);
        }

        return (null, new AutoChoice(entry, quest, tip, status) { Hands = landing });
    }

    /// <summary>
    /// A second opinion holding a landing, owed where its gate is read: a push held back's recovery (FABRIC-CLOSE1h15) and the look's
    /// judgement of a done (FABRIC-CLOSE1h17). Owed at the landing's occasion, the one the gate read, whose refusal shows the rule reads
    /// there, for the session whose work would land, unless one is owed there already, as the person's ask owes one. The loop's opinion
    /// look then asks it as any (<see cref="OpinionLook.ClosesAsync"/> keeps it owed while a push is held back). Never throws: a due list
    /// that cannot be written leaves the person's ask, which the gate's sentence names, and the next look writes it again.
    /// </summary>
    /// <param name="since">
    /// The entry's done, where the look owes it (FABRIC-CLOSE1h17): a due the loop closed as landed since then is not opened again, since its
    /// look would only close it again at every look. Null, as a push held back's recovery owes it, opens one whatever closed the last.
    /// </param>
    private void Owe(AutoLanding entry, string session, string quest, string tree, DateTimeOffset? since = null)
    {
        try
        {
            var dues = new OpinionDues(home);
            var owed = dues.All()
                .Where(due => string.Equals(due.Session, session, StringComparison.OrdinalIgnoreCase) && due.Occasion == OpinionRules.Landing)
                .ToList();
            if (owed.Any(due => due.Closed is null)) return;
            if (since is { } done && owed.Any(due => due.Why == OpinionDueClosed.Landed && due.Closed >= done)) return;

            dues.Due(new OpinionDue(session, quest, entry.Repository, entry.Workspace, tree, OpinionRules.Landing, Now()));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Owed at the next look instead.
        }
    }

    /// <summary>
    /// A branch made and held back from its push, handed on now its quest stands again (FABRIC-CLOSE1h2): the hand-off's own door
    /// (<see cref="TreeHandOffs.HandAsync"/>), which takes the branch's hold, reads the record under it and pushes once, from the
    /// repository the tree is a checkout of. Kept as a landing is: <c>landed</c> where it pushed, <c>plugin-failed</c> where the plugin
    /// did not, <c>push-held</c> again where its quest moved just before the plugin was told (FABRIC-CLOSE1h8), and a refusal where the
    /// hand-off spoke to none.
    /// </summary>
    private async Task<string> HandOnAsync(AutoChoice choice, LandedBranch made, CancellationToken ct)
    {
        var entry = choice.Entry;
        var root = TreeTips.CommonDirectory(entry.Tree) is { } common
            ? Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(common)) ?? entry.Tree
            : entry.Tree;
        // FABRIC-CLOSE1h8: its quest judged once more just before the plugin is told, by the look's own rule (a hold stops it too), so a
        // hand-off that waited behind another push of the branch tells no plugin where its quest moved meanwhile. FABRIC-CLOSE1h10: the
        // quest of the session that last moved the branch as the record reads there, so a later session's work rides no earlier quest;
        // FABRIC-CLOSE1h11: and every earlier session's whose work the push carries unpushed.
        var handed = await trees.HandAsync(root, made, made.Rule?.Plugin, ct, (record, waited, token) => BeforePushAsync(choice, record, waited, token))
            .ConfigureAwait(false);
        // Held back there, the branch stands made as it was, so it stays `push-held` for the first look once its quest stands again: a
        // refusal would be kept at this tip and never tried again, since the tree does not move.
        var landed = handed.Withheld is { } withheld ? new TreeLanding(true, handed.Message, handed.Branch) { Withheld = withheld }
            : handed.Plugin is not null ? new TreeLanding(true, handed.Message, handed.Branch, handed.Plugin)
            : new TreeLanding(false, handed.Message, handed.Branch) { Refusal = AutoLandingCode.Refused };
        return Tried(entry, AutoLandingRules.CodeOf(landed), choice.Tip, choice.Status, landed, handed.Plugin?.Plugin ?? made.Rule?.Plugin,
            made.Branch);
    }

    /// <summary>
    /// Keep one try (design §8): on the due list with its facts, in the conversation where its code is said there, and in the
    /// machine log by its code; and the look's line for it.
    /// </summary>
    /// <param name="named">The branch the pattern names, which a refusal's note names where the landing made none.</param>
    private string Tried(
        AutoLanding entry, string code, string? tip, string? status, TreeLanding? landed = null, string? plugin = null,
        string? named = null, int? commits = null, int? uncommitted = null)
    {
        var made = landed is { Landed: true } ? landed.Branch : null;
        Due.Tried(entry.Session, new AutoTry(Now(), code)
        {
            Tip = tip,
            Status = status,
            Uncommitted = uncommitted,
            Branch = made ?? (code is AutoLandingCode.Exists or AutoLandingCode.Completed or AutoLandingCode.Already ? named : null),
            Commits = made is null ? null : commits,
        }, AutoLandingCode.Closes(code));

        var line = Words(code, landed);
        if (AutoLandingNotes.Says(code))
        {
            var note = AutoLandingNotes.Of(code, landed, plugin, uncommitted, named);
            events.Keep(entry.Session, note, say: null);
            line = note.Text!;
        }

        log?.Invoke(LandingLine.Auto(entry.Session, entry.Repository, entry.Workspace, code, made is null ? null : commits, uncommitted,
            plugin, landed?.Plugin?.Pushed));
        return $"landing  session {entry.Session} (#{entry.Quest} → {entry.Repository}): {line}";
    }

    /// <summary>An entry closed with no landing, said and kept as a try of its own.</summary>
    private string Closed(AutoLanding entry, string code, string? branch = null) =>
        Tried(entry, code, tip: null, status: null, named: branch);

    /// <summary>
    /// What the run's named workflow holds (WORKFLOW1f): a refusal kept once, with no tip and no status, which a refused landing
    /// always has, so the next look reads the gate again whatever the tree does, and says nothing more while it still holds.
    /// </summary>
    /// <param name="held">The gate's refusal (<see cref="LandingGate.Refusal"/>), the run's part's or its landing step's.</param>
    /// <param name="line">The machine log's line for it (<see cref="LandingGateWords.Held"/>), which names which of the two held.</param>
    /// <returns>The look's line, or null where it was said already.</returns>
    private string? Workflow(AutoLanding entry, TreeLanding held, LandingLine? line)
    {
        // Said once: a hold kept as a refusal with no tip and no status is the workflow's (a refused landing is kept with both).
        if (entry.Last is { Code: AutoLandingCode.Refused, Tip: null, Status: null }) return null;
        var said = Tried(entry, AutoLandingCode.Refused, tip: null, status: null, held);
        if (line is not null) log?.Invoke(line);
        return said;
    }

    /// <summary>
    /// The landing gate read for one of the look's landings, and what of it holds the work (FABRIC-CLOSE1h12, out of the look's
    /// judgement): the gate as every landing door reads it (XAGENT1f), its first part that holds as a landing's refusal in §7's order
    /// (<see cref="LandingGate.Refusal"/>: the run's own part, the second opinion, the review, the landing step), and the machine log's
    /// line for that part. The look, the push and a push held back's recovery judge by it alike, so a gate that starts holding after the
    /// look chose a landing holds its push too.
    /// </summary>
    /// <param name="session">The session whose work would land: the entry's, or a later one whose done moved its branch on (CLOSE1h10).</param>
    /// <param name="quest">That session's quest, whose chain the opinion and the review read; null reads no part that needs one.</param>
    /// <param name="tree">That session's tree, whose tip the opinion and the review judge.</param>
    /// <param name="process">The run's process where the caller read it already (WORKFLOW1f); otherwise the gate reads it, once.</param>
    /// <param name="tip">The commit to judge the work at, where a push is judged (FABRIC-CLOSE1h16); null reads the tree's head.</param>
    private async Task<(LandingGate Gate, TreeLanding? Held, LandingLine? Line)> GateAsync(
        AutoLanding entry, string session, string? quest, string tree, IAutoLandingWorld world, CancellationToken ct,
        WorkflowProcess? process = null, string? tip = null)
    {
        var gate = await trees.GateAsync(tree, quest, world, session, ct, process, tip).ConfigureAwait(false);
        return gate.Refusal is { } held
            ? (gate, held, LandingGateWords.Held(session, (entry.Workspace, entry.Repository), gate, held, ReviewDoors.Look))
            : (gate, null, null);
    }

    /// <summary>What the look's line says of a code the conversation is not told of.</summary>
    private static string Words(string code, TreeLanding? landed) => code switch
    {
        AutoLandingCode.Already => "its work is already on a branch of yours, so nothing more lands automatically.",
        AutoLandingCode.Superseded => "a newer session went on in its tree, which stands for it now.",
        AutoLandingCode.Gone => "its tree was discarded before it landed, so nothing lands.",
        AutoLandingCode.Undone => "its quest is no longer done, so nothing lands automatically.",
        AutoLandingCode.Off => "its repository's rule no longer accepts automatically, so it waits for your press.",
        _ => landed?.Message ?? code,
    };

    /// <summary>The tree's HEAD and status, as a try is kept with them, and how many paths are uncommitted.</summary>
    private static async Task<(string? Tip, string Status, int Uncommitted)> ReadTreeAsync(string tree, CancellationToken ct)
    {
        var (headCode, head, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--verify", "--quiet", "HEAD"], ct).ConfigureAwait(false);
        var (statusCode, porcelain, _) = await WorkingTree.GitAsync(tree, ["status", "--porcelain"], ct).ConfigureAwait(false);
        var tip = headCode == 0 ? head.Trim() : null;
        if (statusCode != 0) return (tip, "unreadable", 0);
        var status = AutoLandingRules.Fingerprint(porcelain);
        return (tip, status, porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length);
    }

    private DateTimeOffset Now() => clock?.Invoke() ?? DateTimeOffset.UtcNow;

    private static bool SameTree(string left, string right) =>
        string.Equals(SessionGroups.Normal(left), SessionGroups.Normal(right), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// How a driven record that just concluded stands to its repository's rule (design §2), acted on: due, it joins the due
    /// list; a done in no tree of its own, or an end that is not a done, is said in its conversation. Never throws: the record
    /// has concluded, and a due list that cannot be written leaves the person's press, as before the switch.
    /// </summary>
    /// <param name="status">Its quest's status as the conclusion read it.</param>
    /// <param name="state">The state its record concluded in.</param>
    /// <param name="tree">Where it ran: a tree of its own, or the repository's checkout.</param>
    /// <param name="workspace">The workspace its start was planned in, for a session in no tree of its own.</param>
    /// <param name="process">
    /// Its run's process (WORKFLOW1f, <see cref="ProcessAsync"/>): under a named workflow, whether it lands with no press is its
    /// version's landing step. Null reads the rule live, as before.
    /// </param>
    /// <returns>The verdict acted on (<see cref="AutoConcluded"/>), or null.</returns>
    public static string? Concluded(
        string home, DriverConfig config, SessionEvents events, string session, QuestView quest, string? status, string state,
        string tree, string? workspace, DateTimeOffset? at = null, WorkflowProcess? process = null)
    {
        try
        {
            var trees = new SessionTrees(home);
            var ownTree = trees.Holds(tree);
            var (where, repository) = ownTree ? trees.Owner(tree) : (RemoteTarget.Workspace(workspace), quest.To);
            // The rule it lands by, its access setup's landing composed in (AUTONOMY1c2), as the look and the landing read it.
            var landing = process is { Named: true } named ? named.Landing : LandingRules.Effective(config, repository, where);
            var verdict = AutoLandingRules.Concluded(landing, status, ownTree, state);
            if (verdict == AutoConcluded.Due)
            {
                new AutoLandings(home).Due(new AutoLanding(session, quest.Id, repository, where, tree, at ?? DateTimeOffset.UtcNow));
            }
            else if (verdict is not null)
            {
                events.Keep(session, AutoLandingNotes.Concluded(verdict, landing.Access), say: null);
            }

            return verdict;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// The process a concluded record's run follows (WORKFLOW1f): its repository and workspace read as <see cref="Concluded"/> reads
    /// them, and its run's binding from the chain the service holds. Read once at the conclusion, for the landing and the second
    /// opinion owed alike. Never throws: a run that could not be read is read live, as before.
    /// </summary>
    public static async Task<WorkflowProcess?> ProcessAsync(
        string home, DriverConfig config, IReviewWorld world, QuestView quest, string tree, string? workspace, CancellationToken ct = default)
    {
        try
        {
            var trees = new SessionTrees(home);
            var (where, repository) = trees.Holds(tree) ? trees.Owner(tree) : (RemoteTarget.Workspace(workspace), quest.To);
            return await WorkflowProcesses.ReadAsync(home, config, world, quest.Id, repository, where, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or DriverException)
        {
            return null;
        }
    }
}
