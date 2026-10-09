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

    public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => service.ReadOpinionAsync(id, ct);

    public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => service.SessionRecordsAsync(ct);

    public async Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) =>
        (await service.ActiveSessionsAsync(ct).ConfigureAwait(false))
        .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A due session the look chose to try, with what it read of it there: its quest, and its tree's tip and status.</summary>
public sealed record AutoChoice(AutoLanding Entry, QuestView Quest, string? Tip, string Status)
{
    /// <summary>
    /// The landing gate as the look read it (XAGENT1f, REVIEWENV1c): the second opinion and the review, each letting it go, which
    /// its landing record keeps.
    /// </summary>
    public LandingGate? Gate { get; init; }
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
    public async Task<AutoChosen> ChooseAsync(DriverConfig config, CancellationToken ct = default)
    {
        var open = Due.Open();
        if (open.Count == 0) return new([], []);

        var said = new List<string>();
        var chosen = new List<AutoChoice>();
        IReadOnlyList<SessionRecord>? records = null;
        foreach (var entry in open)
        {
            // The switch is read as it stands now: switched off, the person's press is the way again.
            var rule = LandingRules.Choose(config, entry.Repository, entry.Workspace).Rule;
            if (rule is not { Form: LandingForm.Branch, AutoAccept: true })
            {
                said.Add(Closed(entry, AutoLandingCode.Off));
                continue;
            }

            // Landed since another way: the person's press lands it at once, whatever the last try was (design §2). A press that
            // moved a chain's branch on lands it too, so its own acceptance is what is compared (LAND2c).
            var since = entry.Last is { } last && last.At > entry.DueAt ? last.At : entry.DueAt;
            if (trees.Recorded.Landing(entry.Session) is { } landing && landing.AcceptedAt(entry.Session) >= since)
            {
                said.Add(Closed(entry, AutoLandingCode.Already, branch: landing.Branch));
                continue;
            }

            if (SessionTrees.TreeGone(entry.Tree))
            {
                said.Add(Closed(entry, AutoLandingCode.Gone));
                continue;
            }

            // ReviewableTree's rule: a live session in the tree is using it, and a newer session on it stands for it now.
            records ??= await world.RecordsAsync(ct).ConfigureAwait(false);
            var onTree = records.Where(record => !record.Teammate && record.Tree is { } tree && SameTree(tree, entry.Tree)).ToList();
            if (onTree.Any(record => record.Live)) continue;
            var newest = onTree.OrderByDescending(record => record.Created).ThenByDescending(record => record.Id, StringComparer.Ordinal).FirstOrDefault();
            if (newest is not null && !string.Equals(newest.Id, entry.Session, StringComparison.OrdinalIgnoreCase))
            {
                said.Add(Closed(entry, AutoLandingCode.Superseded));
                continue;
            }

            var quest = await world.QuestAsync(entry.Quest, ct).ConfigureAwait(false);
            if (quest is not { Status: "Done" })
            {
                said.Add(Closed(entry, AutoLandingCode.Undone));
                continue;
            }

            // Held for the person's yes (D133) or evidence's verdict (D144): kept once, and read again at every look. A set-up step
            // held for its review alone is the gate's below, which says what lets it go (REVIEWENV1c).
            if (quest is { Held: true } && quest.Hold != EvidenceCodes.Unreviewed)
            {
                if (entry.Last?.Code != AutoLandingCode.Held) said.Add(Tried(entry, AutoLandingCode.Held, tip: null, status: null));
                continue;
            }

            var (tip, status, uncommitted) = await ReadTreeAsync(entry.Tree, ct).ConfigureAwait(false);
            if (!AutoLandingRules.ShouldTry(entry, tip, status)) continue;

            // The landing gate, read once (XAGENT1f, the second-agent design §7): the second opinion first. Where the level says
            // one is read before landing, the session stays due as `opinion`, said once and read again at every look, until it is
            // settled or the person answers it; a chain step whose chain has a later step here waits for that step (§8.1).
            var gate = await trees.GateAsync(entry.Tree, entry.Quest, world, entry.Session, ct).ConfigureAwait(false);
            if (!gate.Opinion.LetsGo)
            {
                if (entry.Last?.Code != AutoLandingCode.Opinion)
                {
                    said.Add(Tried(entry, AutoLandingCode.Opinion, tip, status,
                        new TreeLanding(false, gate.Opinion.Says) { Refusal = AutoLandingCode.Opinion }));
                    log?.Invoke(OpinionLines.Held(entry.Session, entry.Repository, entry.Workspace, gate.Opinion, ReviewDoors.Look));
                }

                continue;
            }

            // The review's gate (REVIEWENV1c, design §3.1): where the level says review, the session stays due as `unreviewed`, said
            // once and read again at every look, until the person's reviewed on a set-up that holds its tip, or their skip.
            var review = gate.Review;
            if (!review.LetsGo)
            {
                if (entry.Last?.Code != AutoLandingCode.Unreviewed)
                {
                    said.Add(Tried(entry, AutoLandingCode.Unreviewed, tip, status,
                        new TreeLanding(false, review.Says) { Refusal = AutoLandingCode.Unreviewed }));
                    log?.Invoke(ReviewLines.Held(entry.Session, entry.Repository, entry.Workspace, review, ReviewDoors.Look));
                }

                continue;
            }

            // Uncommitted work would not travel with the branch (design §5): it stays to review, with the count of its paths.
            if (uncommitted > 0)
            {
                said.Add(Tried(entry, AutoLandingCode.Uncommitted, tip, status, uncommitted: uncommitted));
                continue;
            }

            chosen.Add(new AutoChoice(entry, quest, tip, status) { Gate = gate });
        }

        return new(chosen, said);
    }

    /// <summary>
    /// The half beside the look: each chosen session landed as the review's Accept lands it, behind D88's proof read at that
    /// moment, and each try kept, said in the conversation and logged. Never throws for one session's sake.
    /// </summary>
    public async Task<IReadOnlyList<string>> LandAsync(IReadOnlyList<AutoChoice> chosen, CancellationToken ct = default)
    {
        var said = new List<string>();
        foreach (var choice in chosen)
        {
            try
            {
                said.Add(await LandOneAsync(choice, ct).ConfigureAwait(false));
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

    private async Task<string> LandOneAsync(AutoChoice choice, CancellationToken ct)
    {
        var entry = choice.Entry;

        // D88's proof, read at that moment (design §2): work no branch of the person's holds. None is a done that made no
        // commits, or work a branch of theirs already holds.
        var unlanded = await SessionTrees.UnlandedAsync(entry.Tree, "HEAD", ct).ConfigureAwait(false);
        if (unlanded == 0)
        {
            var ahead = await trees.AheadOfLineAsync(entry.Tree, ct).ConfigureAwait(false);
            return Tried(entry, ahead == 0 ? AutoLandingCode.Nothing : AutoLandingCode.Already, choice.Tip, choice.Status);
        }

        // Named for the chain's first quest (WSR5), as the press names it.
        var subject = await LandingRules.SubjectAsync(
            entry.Session, entry.Quest, id => world.QuestAsync(id, ct), events.Openings([entry.Session]).GetValueOrDefault(entry.Session))
            .ConfigureAwait(false);
        var plan = await trees.PlanAsync(entry.Tree, subject, ct).ConfigureAwait(false);
        var landed = await trees.LandAsync(entry.Tree, subject, ct, world.InUseAsync, AcceptedBy.Auto, choice.Gate).ConfigureAwait(false);
        var code = AutoLandingRules.CodeOf(landed);
        return Tried(entry, code, choice.Tip, choice.Status, landed, plan.Plugin, plan.Target, unlanded);
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
    /// <returns>The verdict acted on (<see cref="AutoConcluded"/>), or null.</returns>
    public static string? Concluded(
        string home, DriverConfig config, SessionEvents events, string session, QuestView quest, string? status, string state,
        string tree, string? workspace, DateTimeOffset? at = null)
    {
        try
        {
            var trees = new SessionTrees(home);
            var ownTree = trees.Holds(tree);
            var (where, repository) = ownTree ? trees.Owner(tree) : (RemoteTarget.Workspace(workspace), quest.To);
            var verdict = AutoLandingRules.Concluded(LandingRules.Choose(config, repository, where).Rule, status, ownTree, state);
            if (verdict == AutoConcluded.Due)
            {
                new AutoLandings(home).Due(new AutoLanding(session, quest.Id, repository, where, tree, at ?? DateTimeOffset.UtcNow));
            }
            else if (verdict is not null)
            {
                events.Keep(session, AutoLandingNotes.Concluded(verdict), say: null);
            }

            return verdict;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
