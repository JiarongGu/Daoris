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
}

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
}

/// <param name="Quest">The quest considered.</param>
/// <param name="Verdict"><see cref="StartVerdict.Start"/>, or why not.</param>
/// <param name="Reason">The sentence a person reads. "Sitting" must always say why (D46 §3).</param>
/// <param name="Root">Where a start would spawn — carried so the executor never re-derives it.</param>
/// <param name="Workspace">The receiver's circle, carried for the same reason — and read by the toolchain.</param>
public sealed record Consideration(
    QuestView Quest, StartVerdict Verdict, string Reason, string? Root = null, string? Workspace = null);

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
        foreach (var quest in snapshot.Quests.Where(q => q.Status == "Open"))
        {
            considerations.Add(Consider(quest));
        }

        return considerations;

        Consideration Consider(QuestView quest)
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

            if (blockedBy.TryGetValue(quest.To, out var session))
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"session `{session}` is active in `{quest.To}` — one session per repository.");
            }

            if (startedThisTick.TryGetValue(quest.To, out var ahead))
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
}
