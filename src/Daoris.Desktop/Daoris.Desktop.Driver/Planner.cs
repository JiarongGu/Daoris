namespace Daoris.Driver;

/// <summary>A quest as the service answered it — enough to decide on, and enough to compose a target from.</summary>
public sealed record QuestView(string Id, string From, string To, string Title, string Body, string Status)
{
    /// <summary>Addresses the quest carries — a ticket, a page — handed to the session as given.</summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>Files the quest carries, each with where this machine keeps it, or null when it does not.</summary>
    public IReadOnlyList<QuestFileView> Attachments { get; init; } = [];

    /// <summary>What closing this done will publish next (D65 §4) — the service's to do, never the driver's.</summary>
    public IReadOnlyList<QuestStepView> Then { get; init; } = [];

    /// <summary>The quest whose close published this one, when it is a step of a chain.</summary>
    public string? Parent { get; init; }

    /// <summary>
    /// The question its taker waits on (D79) — another quest, asked of the repository that knows — or
    /// null. A taken quest carrying one is its taker's to resume once the question closes.
    /// </summary>
    public string? Awaits { get; init; }

    /// <summary>What its close said — a done's note or a decline's reason. The answer a waiting session resumes with.</summary>
    public string? Note { get; init; }
}

/// <summary>The session this machine last ran on a quest, and the tree it ran in (D79, D80).</summary>
/// <param name="Session">Its record's id.</param>
/// <param name="Tree">Where it ran — its own tree where the repository opted in, the root otherwise; null when unsaid.</param>
/// <param name="State">How its record ended — `failed` is a cut-off, which a later session carries on (D80).</param>
/// <param name="Note">What its record said about that ending — the words a session carrying on is told.</param>
/// <param name="Repository">Where it ran — whether a chain's next step can build on its tree (CHAIN2).</param>
/// <param name="Answer">The person's answer when it parked to ask them (STANDDOWN2) — what a carry-on is handed.</param>
public sealed record PriorSession(
    string Session, string? Tree, string State = "", string? Note = null, string? Repository = null,
    string? Answer = null);

/// <summary>One step of a chain, as the service answered it.</summary>
public sealed record QuestStepView(string To, string Title, string Body);

/// <summary>A file a quest carries, as the local service answered it.</summary>
/// <param name="Name">The file's own name.</param>
/// <param name="Sha256">Its content's hash.</param>
/// <param name="Bytes">Its size.</param>
/// <param name="Path">
/// Where THIS machine keeps it — told by the service, never derived here: the layout under the home is
/// the service's, and the driver's home is not always the service's (it is wherever `driver.json`
/// lives). Null is honest and common: a quest mirrored from another machine names files whose bytes
/// stayed where it was published.
/// </param>
public sealed record QuestFileView(string Name, string Sha256, long Bytes, string? Path);

/// <summary>A registration as the service answered it. The root is present only from a local service.</summary>
/// <param name="Workspace">
/// The circle this repository is wired into on THIS machine (D48 §2) — the registry row, which is the
/// only honest source for it. The toolchain reads it to pick a workspace's credential profile
/// (D49 §4): a work account for the work circle, a personal one at home.
/// </param>
public sealed record RepoView(
    string Repository, bool Adopted, string? Root, string Workspace = RemoteTarget.DefaultWorkspace);

public static class Repositories
{
    /// <summary>
    /// What the registry holds, as one string — equal when the same repositories stand in the same
    /// circles at the same roots, whatever the order. The shell forwards a tick to the page when this
    /// changes (FG4), as it does for <see cref="Asks.Signature"/>: a folder imported from a terminal
    /// moves nothing else a tick reports, and the page said *no workspace yet* until a reload.
    /// </summary>
    public static string Signature(IEnumerable<RepoView> registry) =>
        string.Join("\n", registry
            .Select(repo => $"{repo.Repository}\t{repo.Adopted}\t{repo.Workspace}\t{repo.Root}")
            .OrderBy(line => line, StringComparer.Ordinal));
}

/// <summary>An ACTIVE session as the service answered it — closed ones never reach the planner.</summary>
/// <param name="State">
/// Where the record stands. The planner has never needed it — "is this repository busy" is the only
/// question it asks — but <see cref="AttentionWatch"/> does, because a park is a state change nothing
/// local performs and is therefore only visible by looking (SURF5b).
/// </param>
/// <param name="Note">What the session said about that state, where it said anything.</param>
public sealed record SessionView(
    string Id, string Repository, string State = "", string? Note = null)
{
    /// <summary>
    /// The ask an INTAKE answers (D65 §1b), or null for every other session. Its "repository" is
    /// <c>ask #id</c>, which no quest names — so the planner never finds it busy with anything.
    /// </summary>
    public string? Ask { get; init; }

    /// <summary>
    /// The tree it holds (D51), or null when unsaid — where a repository opens a tree per session, the
    /// tree is the lock the planner asks about (PAR1).
    /// </summary>
    public string? Tree { get; init; }
}

public static class ActiveSessions
{
    /// <summary>
    /// Which sessions are active and where each stands, as one string — equal when the same ones stand
    /// in the same states, whatever the order. The shell forwards a tick to the page when this changes
    /// (UX5 U13), as it does for <see cref="Asks.Signature"/>: a conversation started or ended from the
    /// main window moves nothing else a tick reports, so the other windows never heard of it.
    /// </summary>
    public static string Signature(IEnumerable<SessionView> sessions) =>
        string.Join("\n", sessions
            .Select(session => $"{session.Id}\t{session.State}")
            .OrderBy(line => line, StringComparer.Ordinal));
}

/// <summary>Everything a tick's decisions are made from, fetched once so the plan is coherent.</summary>
/// <param name="Strikes">
/// How many sessions have <b>failed</b> on each quest, by quest id — <b>derived</b> from the session
/// records this machine already wrote, never a tally the driver keeps (DRV6). Only `failed` counts: a
/// stand-down means somebody else got there first, a decline is a real answer, and a stop was the
/// person. A quest nobody has failed is simply absent.
/// </param>
public sealed record Snapshot(
    IReadOnlyList<QuestView> Quests,
    IReadOnlyList<RepoView> Repositories,
    IReadOnlyList<SessionView> Active,
    IReadOnlyDictionary<string, int>? Strikes = null)
{
    public IReadOnlyDictionary<string, int> Strikes { get; init; } =
        Strikes ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The session THIS machine last ran on each quest, by quest id — derived from the same records as
    /// the strikes, never kept (D79). It is how a waiting quest is known to be this machine's to resume,
    /// and where: a taken quest no session here ran is somebody else's take.
    /// </summary>
    public IReadOnlyDictionary<string, PriorSession> LastRun { get; init; } =
        new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase);
}

public enum StartVerdict
{
    /// <summary>Start a session for this quest, now.</summary>
    Start,

    /// <summary>The receiver has not opted into driving on this machine — the person's choice (D46 §2).</summary>
    NotDrivable,

    /// <summary>The person paused this repository.</summary>
    Held,

    /// <summary>
    /// Nothing here could answer it: the receiver is not registered on this machine, or it has not
    /// adopted and either has no root or would be carried by the pipe door, which has no connector to
    /// hand it (D70). The name predates D70, and the reason says which.
    /// </summary>
    NotAdopted,

    /// <summary>No filesystem root is known — nowhere to spawn. `connect` from the repository fixes it.</summary>
    NoRoot,

    /// <summary>An active session (or an older quest this tick) holds the repository.</summary>
    RepositoryBusy,

    /// <summary>The concurrency cap is spent.</summary>
    AtCapacity,

    /// <summary>
    /// Enough sessions have failed on this quest that trying again is spending an account rather than
    /// making progress (DRV6). The person restarts it deliberately.
    /// </summary>
    Exhausted,

    /// <summary>
    /// Planned to start, and held at SPAWN by what only the spawn can see — a dirty tree, an absent
    /// or logged-out harness, a tree that would not grow, a trust flag never given. The reason is the
    /// hold's own sentence, which names the fix. Never produced by the planner: it is what a tick
    /// reports when what happened differs from what was decided, so "sitting" still says why.
    /// </summary>
    Blocked,

    /// <summary>
    /// Taken, and waiting on a question asked of another repository (D79). It resumes, in the same
    /// tree, once that quest is answered — nothing for the person to do.
    /// </summary>
    Waiting,
}

/// <param name="Quest">The quest considered.</param>
/// <param name="Verdict"><see cref="StartVerdict.Start"/>, or why not.</param>
/// <param name="Reason">The sentence a person reads. "Sitting" must always say why (D46 §3).</param>
/// <param name="Root">Where a start would spawn — carried so the executor never re-derives it.</param>
/// <param name="Workspace">The receiver's circle, carried for the same reason — and read by the toolchain.</param>
public sealed record Consideration(
    QuestView Quest, StartVerdict Verdict, string Reason, string? Root = null, string? Workspace = null)
{
    /// <summary>
    /// For a start that RESUMES a waiting quest (D79): the session that asked and waited, whose tree
    /// the resume carries on in. Null for every first start.
    /// </summary>
    public PriorSession? Resumes { get; init; }

    /// <summary>
    /// For a chain's next step in the same repository (CHAIN2): the step before's last run, whose
    /// branch this step's tree grows from, so it sees the unmerged work it builds on or checks.
    /// </summary>
    public PriorSession? BuildsOn { get; init; }
}

public static class Considerations
{
    /// <summary>
    /// What a set of considerations SAYS, as one string — equal when the same quests carry the same
    /// verdicts and reasons, whatever the order. The shell forwards a tick to the page when this
    /// changes: a page that shows why each quest is sitting needs the change, and nothing else,
    /// because every tick it receives refetches four queries.
    /// </summary>
    public static string Signature(IEnumerable<Consideration> considered) =>
        string.Join("\n", considered
            .Select(c => $"{c.Quest.Id}\t{c.Verdict}\t{c.Reason}")
            .OrderBy(line => line, StringComparer.Ordinal));

    /// <summary>
    /// The plan as it turned out: every start that was held at spawn becomes
    /// <see cref="StartVerdict.Blocked"/> with the hold's own sentence, and everything else is the
    /// plan's word. The plan is not edited — what was decided and what happened are two records.
    /// </summary>
    /// <remarks>
    /// 🔴 Found on the deployed application: the quest it held every tick — trust flag never given —
    /// read <c>Start</c> in every consideration, because the hold lived only in the event line. The
    /// Overview, asked to say why each quest sits, had nothing to say under the one that mattered.
    /// </remarks>
    public static IReadOnlyList<Consideration> Blocked(
        IReadOnlyList<Consideration> plan, IReadOnlyDictionary<string, string> heldAt) =>
        plan.Select(c =>
                c.Verdict == StartVerdict.Start && heldAt.TryGetValue(c.Quest.Id, out var why)
                    ? c with { Verdict = StartVerdict.Blocked, Reason = why }
                    : c)
            .ToList();
}

/// <summary>
/// The decision half of a tick: which open quests start, and why every other one is sitting.
/// </summary>
/// <remarks>
/// <para><b>Pure, deliberately.</b> Plan and apply are separate functions — the CLI's own convention,
/// held here for the same reason: a decision that can be asserted without spawning anything stays
/// tested, and a printed plan is a driver that can explain itself.</para>
///
/// <para><b>The plan never writes quest state and never outranks the ledger.</b> The service re-judges
/// every open through <c>SessionLedger</c>; this planner exists to avoid asking for work the ledger
/// would refuse, and to give every refusal a sentence before it happens.</para>
/// </remarks>
public static class Planner
{
    /// <param name="door">
    /// Which door this machine's starts ride (D53): the configured adapter's wire. It decides whether a
    /// repository that registered without adopting can be driven (D70). Defaults to the pipe, the door
    /// with the stricter requirement, so a caller that does not know cannot start more than it should.
    /// </param>
    public static IReadOnlyList<Consideration> Plan(
        Snapshot snapshot, DriverConfig config, SessionWire door = SessionWire.Pipe)
    {
        var considerations = new List<Consideration>();
        // 🔴 Grouped, never keyed straight off the list: two active sessions in one repository is a
        // state D51 allows (a conversation in the checkout, another in a tree of its own), and a
        // dictionary built from the list threw on the second — every tick failed while both were open.
        // Either blocks the repository; the first is the one the reason names.
        var blockedBy = snapshot.Active
            .GroupBy(s => s.Repository, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
        var startedThisTick = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var slots = config.Cap - snapshot.Active.Count;

        // The service already orders open-oldest-first; keeping its order is what makes "oldest starts
        // first" one implementation rather than two that drift.
        foreach (var quest in snapshot.Quests)
        {
            if (quest.Status == "Open")
            {
                var considered = Consider(quest);
                // A chain's next step in the repository its parent ran in builds on that run (CHAIN2).
                considerations.Add(considered.Verdict == StartVerdict.Start
                                   && quest.Parent is { } parent
                                   && snapshot.LastRun.TryGetValue(parent, out var before)
                                   && string.Equals(before.Repository, quest.To, StringComparison.OrdinalIgnoreCase)
                    ? considered with { BuildsOn = before }
                    : considered);
            }
            else if (quest is { Status: "Taken", Awaits: { Length: > 0 } awaits }
                     && snapshot.LastRun.TryGetValue(quest.Id, out var prior))
            {
                considerations.Add(Resume(quest, awaits, prior));
            }
            else if (quest is { Status: "Taken", Awaits: null or "" }
                     && snapshot.LastRun.TryGetValue(quest.Id, out var cutOff)
                     && (string.Equals(cutOff.State, "failed", StringComparison.OrdinalIgnoreCase)
                         // The person answered a session that parked to ask them (STANDDOWN2).
                         || cutOff is { State: "completed", Answer: not null }))
            {
                considerations.Add(CarryOn(quest, cutOff));
            }
        }

        return considerations;

        // A cut-off (D80): this machine's session took the quest and failed before closing it — timed
        // out, refused, crashed — so the take is still here and the work is in its tree. Carried on like
        // a failed start is retried: the strikes count every cut-off, and the third parks it.
        Consideration CarryOn(QuestView quest, PriorSession cutOff)
        {
            var considered = Consider(quest, into: cutOff);
            return considered.Verdict == StartVerdict.Start
                ? considered with
                {
                    Reason = cutOff.Answer is { } answer
                        ? $"carrying on in `{quest.To}` — you answered session `{cutOff.Session}`: {answer}"
                        : $"carrying on in `{quest.To}` — session `{cutOff.Session}` was cut off: "
                          + (cutOff.Note is { Length: > 0 } note ? note : "it ended before closing the quest."),
                    Resumes = cutOff,
                }
                : considered;
        }

        // A waiting quest (D79): the open list holds open and taken quests only, so a question still in
        // it is unanswered, and one absent from it has closed. A resume is a start in every other way —
        // the person's hold, the strikes, busy and the cap all still stand between it and a spawn.
        Consideration Resume(QuestView quest, string awaits, PriorSession prior)
        {
            var question = snapshot.Quests.FirstOrDefault(q =>
                string.Equals(q.Id, awaits, StringComparison.OrdinalIgnoreCase));
            if (question is not null)
            {
                return new(quest, StartVerdict.Waiting,
                    $"waits on `#{question.Id}`, asked of `{question.To}` — it resumes, in the same tree, "
                    + "once that is answered.");
            }

            var considered = Consider(quest, into: prior);
            return considered.Verdict == StartVerdict.Start
                ? considered with { Reason = $"resuming in `{quest.To}` — `#{awaits}` is answered.", Resumes = prior }
                : considered;
        }

        // `into`: the earlier session whose tree a resume or a carry-on goes back into — the one tree a
        // live session there would hold (PAR1). Null for a first start, which grows a tree of its own.
        Consideration Consider(QuestView quest, PriorSession? into = null)
        {
            var repo = snapshot.Repositories.FirstOrDefault(r =>
                string.Equals(r.Repository, quest.To, StringComparison.OrdinalIgnoreCase));

            if (repo is null)
            {
                return new(quest, StartVerdict.NotAdopted,
                    $"`{quest.To}` is not registered on this machine, so there is nowhere to start it.");
            }

            // Registered is drivable over the protocol door; adopted is disciplined (D70). The pipe
            // door's session reaches the knowledge tools only through the repository's own `.mcp.json`,
            // which adoption writes and the driver may never write for it (D32) — so there it sits.
            if (!repo.Adopted)
            {
                if (repo.Root is null)
                {
                    return new(quest, StartVerdict.NotAdopted,
                        $"`{quest.To}` has not adopted Daoris and no root is known for it on this machine, "
                        + "so there is nothing to start and nothing there to see the quest.");
                }

                if (door != SessionWire.Acp)
                {
                    return new(quest, StartVerdict.NotAdopted,
                        $"`{quest.To}` has not adopted Daoris, so a session there would have no connector to "
                        + "take the quest with — only the protocol door hands it one. Drive with an ACP "
                        + "agent, or adopt it.");
                }
            }

            if (!config.Drivable.Contains(quest.To, StringComparer.OrdinalIgnoreCase))
            {
                return new(quest, StartVerdict.NotDrivable,
                    $"`{quest.To}` has not been opted into driving on this machine.");
            }

            if (config.Holds.Contains(quest.To, StringComparer.OrdinalIgnoreCase))
            {
                return new(quest, StartVerdict.Held, $"`{quest.To}` is held by the person.");
            }

            if (repo.Root is null)
            {
                return new(quest, StartVerdict.NoRoot,
                    $"no root is known for `{quest.To}` — run `daoris connect` from that repository.");
            }

            // 🔴 Before busy and before capacity, because those are waits and this is a stop: a
            // parked quest must not read as "queued" in a surface that renders the reason.
            var strikes = snapshot.Strikes.TryGetValue(quest.Id, out var failures)
                ? failures - config.ForgivenAt(quest.Id)
                : 0;
            if (config.Strikes > 0 && strikes >= config.Strikes)
            {
                return new(quest, StartVerdict.Exhausted,
                    $"{strikes} session(s) have failed on `#{quest.Id}` without landing anything — "
                    + $"parked, because trying again spends an account rather than making progress. "
                    + $"`daoris driver retry {quest.Id}` starts it again once you know why.");
            }

            // 🔴 The TREE is the lock (D51), and where every session here opens its own there is no
            // reason to run one at a time (PAR1, the owner: "clean domain separation and parallel
            // running"). What still holds is the one tree a resume or a carry-on goes back into.
            if (config.OpensOwnTree(quest.To))
            {
                if (into is { Tree: { Length: > 0 } tree }
                    && snapshot.Active.FirstOrDefault(s => SameTree(s.Tree, tree)) is { } holder)
                {
                    return new(quest, StartVerdict.RepositoryBusy,
                        $"session `{holder.Id}` is active in the tree `#{quest.Id}` goes back into — one session per tree.");
                }
            }
            else if (blockedBy.TryGetValue(quest.To, out var session))
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"session `{session}` is active in `{quest.To}` — one session per repository, whose root "
                    + "is its one tree. Sessions there run side by side once they open trees of their own.");
            }
            else if (startedThisTick.TryGetValue(quest.To, out var ahead))
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"queued behind quest `#{ahead}` in `{quest.To}` — oldest first, one at a time.");
            }

            if (slots <= 0)
            {
                return new(quest, StartVerdict.AtCapacity,
                    $"the concurrency cap ({config.Cap}) is spent — it frees as sessions finish.");
            }

            slots--;
            startedThisTick[quest.To] = quest.Id;
            return new(quest, StartVerdict.Start, $"starting in `{quest.To}`.", repo.Root, repo.Workspace);
        }
    }

    /// <summary>Two tree paths are one tree — separators and case aside, as Windows sees them.</summary>
    private static bool SameTree(string? a, string? b) =>
        a is { Length: > 0 } && b is { Length: > 0 }
        && string.Equals(
            a.Replace('\\', '/').TrimEnd('/'), b.Replace('\\', '/').TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
}
