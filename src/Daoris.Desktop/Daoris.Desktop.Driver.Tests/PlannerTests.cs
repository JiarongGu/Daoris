using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The plan is a pure function of what the service answered and what the person configured — the same
/// plan/apply split the CLI holds, for the same reason: a decision that can be asserted without
/// spawning anything is a decision that stays tested.
///
/// Every non-start carries its reason, because the Overview's question splits under a driver (D46 §3):
/// sitting because nobody CAN take it is the person's setup work; sitting because the driver has not
/// started it is the driver's state to explain.
/// </summary>
/// <summary>
/// The shell forwards a tick to the page only when it has something to say. Considerations are
/// "something to say" exactly when they CHANGED — a stable set of sitting quests would otherwise
/// arrive every poll, and the page refetches four queries on every tick it receives.
/// </summary>
public sealed class ConsiderationSignatureTests
{
    private static Consideration Sitting(string quest, StartVerdict verdict, string reason) =>
        new(new QuestView(quest, "asker", "receiver", "title", "body", "Open"), verdict, reason);

    [Fact]
    public void The_same_considerations_sign_the_same_whatever_their_order()
    {
        var a = new[] { Sitting("q1", StartVerdict.Held, "paused"), Sitting("q2", StartVerdict.NotDrivable, "not here") };
        var b = new[] { a[1], a[0] };

        Assert.Equal(Considerations.Signature(a), Considerations.Signature(b));
    }

    /// <summary>
    /// 🔴 The holds a real machine actually hits — a dirty tree, a logged-out harness, a trust flag
    /// never given — are decided at SPAWN, after the planner said Start. The report used to keep the
    /// plan, so the quest the deployed application was holding every ten seconds read `Start` in
    /// every consideration and the Overview had nothing to say under it.
    /// </summary>
    [Fact]
    public void A_start_held_at_spawn_is_reported_as_blocked_with_the_holds_own_sentence()
    {
        var plan = new[]
        {
            Sitting("q1", StartVerdict.Start, "starting"),
            Sitting("q2", StartVerdict.NotDrivable, "not here"),
        };
        var heldAt = new Dictionary<string, string>
        {
            ["q1"] = "the working tree has uncommitted changes (1 paths)",
            ["nobody"] = "a hold for a quest that was never planned is ignored",
        };

        var reported = Considerations.Blocked(plan, heldAt);

        Assert.Equal(StartVerdict.Blocked, reported[0].Verdict);
        Assert.Equal("the working tree has uncommitted changes (1 paths)", reported[0].Reason);
        Assert.Equal("q1", reported[0].Quest.Id);
        Assert.Equal(plan[1], reported[1]);
        Assert.Equal(2, reported.Count);
        // The plan itself is untouched: what was decided and what happened are two records.
        Assert.Equal(StartVerdict.Start, plan[0].Verdict);
    }

    [Fact]
    public void A_changed_reason_or_verdict_signs_differently_and_nothing_signs_empty()
    {
        var before = new[] { Sitting("q1", StartVerdict.Held, "paused") };
        var reason = new[] { Sitting("q1", StartVerdict.Held, "still paused") };
        var verdict = new[] { Sitting("q1", StartVerdict.Start, "paused") };

        Assert.NotEqual(Considerations.Signature(before), Considerations.Signature(reason));
        Assert.NotEqual(Considerations.Signature(before), Considerations.Signature(verdict));
        Assert.Equal(string.Empty, Considerations.Signature([]));
    }
}

/// <summary>
/// And when the ACTIVE SESSIONS changed (UX5 U13): a conversation started or ended from the main
/// window moves nothing a tick reports, so a quiet tick never told the other windows, and the monitor
/// never showed a chat the person had just opened. Seen on the window: a minute and two ticks later,
/// still absent.
/// </summary>
public sealed class ActiveSessionSignatureTests
{
    [Fact]
    public void The_same_sessions_sign_the_same_whatever_their_order()
    {
        var a = new[] { new SessionView("s1", "engine", "working"), new SessionView("c2", "game", "awaiting-person") };

        Assert.Equal(ActiveSessions.Signature(a), ActiveSessions.Signature([a[1], a[0]]));
    }

    [Fact]
    public void A_session_started_ended_or_moved_signs_differently()
    {
        var before = new[] { new SessionView("s1", "engine", "working") };

        Assert.NotEqual(ActiveSessions.Signature(before),
            ActiveSessions.Signature([.. before, new SessionView("c2", "game", "working")]));
        Assert.NotEqual(ActiveSessions.Signature(before), ActiveSessions.Signature([]));
        Assert.NotEqual(ActiveSessions.Signature(before),
            ActiveSessions.Signature([new SessionView("s1", "engine", "awaiting-person")]));
        Assert.Equal(string.Empty, ActiveSessions.Signature([]));
    }
}

/// <summary>
/// And when the REGISTRY changed (FG4): a folder imported from a terminal moves nothing a tick
/// reports, so the page kept saying *no workspace yet* until it was reloaded. Seen on the deployed
/// application, with 29 repositories just registered.
/// </summary>
public sealed class RepositorySignatureTests
{
    [Fact]
    public void The_same_registry_signs_the_same_whatever_its_order()
    {
        var a = new[] { new RepoView("engine", true, "/work/engine", "aurora"), new RepoView("game", false, "/work/game") };

        Assert.Equal(Repositories.Signature(a), Repositories.Signature([a[1], a[0]]));
    }

    [Fact]
    public void A_repository_added_retired_re_wired_moved_or_adopted_signs_differently()
    {
        var before = new[] { new RepoView("engine", false, "/work/engine", "aurora") };

        Assert.NotEqual(Repositories.Signature(before),
            Repositories.Signature([.. before, new RepoView("game", false, "/work/game", "aurora")]));
        Assert.NotEqual(Repositories.Signature(before), Repositories.Signature([]));
        Assert.NotEqual(Repositories.Signature(before), Repositories.Signature([new RepoView("engine", false, "/work/engine", "tools")]));
        Assert.NotEqual(Repositories.Signature(before), Repositories.Signature([new RepoView("engine", false, "/elsewhere/engine", "aurora")]));
        Assert.NotEqual(Repositories.Signature(before), Repositories.Signature([new RepoView("engine", true, "/work/engine", "aurora")]));
        Assert.Equal(string.Empty, Repositories.Signature([]));
    }
}

public sealed class PlannerTests
{
    private static QuestView Quest(string id = "q1", string to = "Game", string status = "Open") =>
        new(id, "Asker", to, $"Ask {id}", "Here is why.", status);

    private static RepoView Repo(string name = "Game", bool adopted = true, string? root = "D:/fam/Game") =>
        new(name, adopted, root);

    private static DriverConfig Config(
        string[]? drivable = null, string[]? holds = null, int cap = 2) =>
        DriverConfig.Empty with { Drivable = drivable ?? ["Game"], Holds = holds ?? [], Cap = cap };

    private static IReadOnlyList<Consideration> Plan(
        QuestView[] quests, RepoView[]? repos = null, SessionView[]? active = null, DriverConfig? config = null,
        SessionWire door = SessionWire.Pipe) =>
        Planner.Plan(new Snapshot(quests, repos ?? [Repo()], active ?? []), config ?? Config(), door);

    /// <summary>
    /// 🔴 Two active sessions in ONE repository is a state D51 allows — a conversation in the checkout
    /// and another in a tree of its own. The plan keyed the active sessions by repository and threw on
    /// the second, so every tick failed for as long as both were open and nothing could start.
    /// </summary>
    [Fact]
    public void Two_active_sessions_in_one_repository_block_it_rather_than_break_the_tick()
    {
        var plan = Plan([Quest()], active: [new SessionView("s1", "Game"), new SessionView("s2", "Game")], config: Config(cap: 5));

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.RepositoryBusy, only.Verdict);
        Assert.Contains("s1", only.Reason);
    }

    [Fact]
    public void An_open_quest_for_a_drivable_repository_starts()
    {
        var plan = Plan([Quest()]);

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.Start, only.Verdict);
        Assert.Equal("D:/fam/Game", only.Root);
    }

    /// <summary>Quests someone already has, and finished ones, are not the driver's to consider.</summary>
    [Fact]
    public void Only_open_quests_are_considered()
    {
        var plan = Plan([Quest(status: "Taken"), Quest("q2", status: "Done")]);

        Assert.Empty(plan);
    }

    /// <summary>Drivable is opt-in per repository, per machine (D46 §2) — silence means untouched.</summary>
    [Fact]
    public void A_repository_that_never_opted_in_is_not_driven_and_the_reason_says_so()
    {
        var plan = Plan([Quest()], config: Config(drivable: []));

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.NotDrivable, only.Verdict);
        Assert.Contains("opt", only.Reason);
    }

    [Fact]
    public void A_held_repository_waits_for_the_person()
    {
        var plan = Plan([Quest()], config: Config(holds: ["Game"]));

        Assert.Equal(StartVerdict.Held, Assert.Single(plan).Verdict);
    }

    /// <summary>
    /// The pipe door keeps its own requirement (D70): its session reaches the knowledge tools only
    /// through the repository's own `.mcp.json`, which adoption writes and the driver may never write
    /// for it (D32). So an unadopted receiver sits there, saying which door would carry it.
    /// </summary>
    [Fact]
    public void Over_the_pipe_door_a_receiver_that_has_not_adopted_sits_saying_why()
    {
        var plan = Plan([Quest()], repos: [Repo(adopted: false)]);

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.NotAdopted, only.Verdict);
        Assert.Contains("connector", only.Reason);
        Assert.Contains("protocol door", only.Reason);
    }

    /// <summary>
    /// Registered is drivable over the protocol door (D70): the session is handed its connector on the
    /// wire, so a repository registered with a root starts like an adopter — opted in and not held.
    /// </summary>
    [Fact]
    public void Over_the_protocol_door_a_registered_receiver_that_has_not_adopted_starts()
    {
        var plan = Plan([Quest()], repos: [Repo(adopted: false)], door: SessionWire.Acp);

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.Start, only.Verdict);
        Assert.Equal("D:/fam/Game", only.Root);
    }

    /// <summary>Opting in and holding are the person's, whatever the door: registered is not consent.</summary>
    [Fact]
    public void Over_the_protocol_door_an_unadopted_receiver_still_needs_opting_in()
    {
        var plan = Plan([Quest()], repos: [Repo(adopted: false)], config: Config(drivable: []), door: SessionWire.Acp);

        Assert.Equal(StartVerdict.NotDrivable, Assert.Single(plan).Verdict);
    }

    /// <summary>
    /// With neither a manifest nor a root there is nothing to start and nothing to see the quest, and
    /// `connect` is not the fix for a repository that has not adopted — so the reason does not say it.
    /// </summary>
    [Fact]
    public void An_unadopted_receiver_with_no_root_here_is_not_told_to_connect()
    {
        var plan = Plan([Quest()], repos: [Repo(adopted: false, root: null)], door: SessionWire.Acp);

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.NotAdopted, only.Verdict);
        Assert.Contains("no root", only.Reason);
        Assert.DoesNotContain("connect", only.Reason);
    }

    /// <summary>A quest to a repository this machine's registry does not hold says exactly that.</summary>
    [Fact]
    public void A_receiver_the_registry_does_not_hold_says_so()
    {
        var plan = Plan([Quest(to: "Elsewhere")], door: SessionWire.Acp);

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.NotAdopted, only.Verdict);
        Assert.Contains("not registered", only.Reason);
    }

    /// <summary>No root, no working tree to spawn in — the reason names `connect` as the fix.</summary>
    [Fact]
    public void A_repository_without_a_root_cannot_be_spawned_into()
    {
        var plan = Plan([Quest()], repos: [Repo(root: null)]);

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.NoRoot, only.Verdict);
        Assert.Contains("connect", only.Reason);
    }

    /// <summary>One session per repository: an active session holds the tree (D46 §3).</summary>
    [Fact]
    public void An_active_session_blocks_its_repository_and_is_named()
    {
        var plan = Plan([Quest()], active: [new SessionView("s1", "Game")]);

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.RepositoryBusy, only.Verdict);
        Assert.Contains("s1", only.Reason);
    }

    // ── parallel sessions, one per tree (PAR1) ────────────────────────────────────────────────────
    //
    // Separate repositories and sessions exist for clean domain separation and parallel running
    // (PAR1). D51 made the TREE the lock; a repository whose sessions each open their
    // own tree has no reason to run one at a time, and the ledger already locks per tree.

    [Fact]
    public void Where_sessions_open_their_own_trees_an_active_session_does_not_hold_the_repository()
    {
        var plan = Plan([Quest()], active: [new SessionView("s1", "Game")], config: Config() with { Trees = ["Game"] });

        Assert.Equal(StartVerdict.Start, Assert.Single(plan).Verdict);
    }

    [Fact]
    public void Where_sessions_open_their_own_trees_several_quests_start_together_up_to_the_cap()
    {
        var plan = Plan([Quest("q1"), Quest("q2"), Quest("q3")], config: Config(cap: 2) with { Trees = ["Game"] });

        Assert.Equal([StartVerdict.Start, StartVerdict.Start, StartVerdict.AtCapacity], plan.Select(c => c.Verdict));
    }

    /// <summary>
    /// 🔴 DEV3: a session outlives the look that started it, so the next look can find its quest still open —
    /// before the session has taken it, or when it never will. Where every session opens its own tree, nothing
    /// else held that quest, and a second session would start on it in a second tree. The session serving a
    /// quest holds it, named, and the rest of the repository stays free (PAR1).
    /// </summary>
    [Fact]
    public void A_quest_whose_own_session_still_works_is_not_started_again()
    {
        var plan = Plan(
            [Quest("q1"), Quest("q2")],
            active: [new SessionView("s1", "Game", "working") { Quest = "q1" }],
            config: Config(cap: 3) with { Trees = ["Game"] });

        Assert.Equal(StartVerdict.RepositoryBusy, plan[0].Verdict);
        Assert.Contains("`s1`", plan[0].Reason);
        Assert.Contains("#q1", plan[0].Reason);
        Assert.Equal(StartVerdict.Start, plan[1].Verdict);
    }

    /// <summary>
    /// 🔴 A resume or a carry-on goes back into its earlier session's tree, so a live session IN that
    /// tree still holds it — the tree is the lock, and one tree takes one session.
    /// </summary>
    [Fact]
    public void A_resume_into_a_tree_a_live_session_holds_waits_for_it()
    {
        var prior = new PriorSession("s1", "D:/trees/s-1", "failed", "timed out.", Repository: "Game");
        var snapshot = Ran([Quest(status: "Taken")], ("q1", prior)) with
        {
            Active = [new SessionView("s9", "Game") { Tree = "D:/trees/s-1" }],
        };

        Assert.Equal(StartVerdict.RepositoryBusy,
            Assert.Single(Planner.Plan(snapshot, Config() with { Trees = ["Game"] })).Verdict);
    }

    /// <summary>Oldest first, one per repository — the second ask queues behind the first.</summary>
    [Fact]
    public void The_oldest_open_quest_starts_and_the_next_queues_behind_it()
    {
        var plan = Plan([Quest("q1"), Quest("q2")]);

        Assert.Equal(StartVerdict.Start, plan[0].Verdict);
        Assert.Equal(StartVerdict.RepositoryBusy, plan[1].Verdict);
        Assert.Contains("q1", plan[1].Reason);
    }

    /// <summary>Concurrency across repositories is the point; the cap bounds it.</summary>
    [Fact]
    public void Different_repositories_start_together_until_the_cap()
    {
        var repos = new[] { Repo("A", root: "/a"), Repo("B", root: "/b"), Repo("C", root: "/c") };
        var quests = new[] { Quest("q1", "A"), Quest("q2", "B"), Quest("q3", "C") };

        var plan = Plan(quests, repos, config: Config(drivable: ["A", "B", "C"], cap: 2));

        Assert.Equal(StartVerdict.Start, plan[0].Verdict);
        Assert.Equal(StartVerdict.Start, plan[1].Verdict);
        Assert.Equal(StartVerdict.AtCapacity, plan[2].Verdict);
    }

    /// <summary>Sessions already running count against the cap before anything new starts.</summary>
    [Fact]
    public void Active_sessions_elsewhere_consume_the_cap()
    {
        var repos = new[] { Repo("A", root: "/a"), Repo("B", root: "/b") };

        var plan = Plan(
            [Quest("q1", "A")], repos,
            active: [new SessionView("s1", "B")],
            config: Config(drivable: ["A", "B"], cap: 1));

        Assert.Equal(StartVerdict.AtCapacity, Assert.Single(plan).Verdict);
    }

    /// <summary>Repository names compare the way the exchange compares them — case is not identity.</summary>
    [Fact]
    public void Drivable_matching_ignores_case()
    {
        var plan = Plan([Quest()], config: Config(drivable: ["game"]));

        Assert.Equal(StartVerdict.Start, Assert.Single(plan).Verdict);
    }

    // ── strikes (DRV6) ────────────────────────────────────────────────────────────────────────────
    //
    // 🔴 Measured, not imagined: ACP2's driven run failed for a reason no retry could fix, and the
    // driver started the same quest 18 times — each one a real login — because a failed spawn leaves
    // the quest `Open` and untouched (design §3), which is exactly what makes it eligible again.
    //
    // The count is DERIVED from the session records the driver already writes, so there is no second
    // register to drift. What a person sets is the limit and the forgiveness.

    private static Snapshot With(QuestView[] quests, IReadOnlyDictionary<string, int> strikes) =>
        new(quests, [Repo()], [], strikes);

    [Fact]
    public void A_quest_that_has_failed_too_often_is_parked_rather_than_started_again()
    {
        var plan = Planner.Plan(
            With([Quest()], new Dictionary<string, int> { ["q1"] = 3 }),
            Config() with { Strikes = 3 });

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.Exhausted, only.Verdict);
        // The reason a person reads has to carry the COUNT and the verb that clears it — a park with
        // neither is a quest that has silently stopped moving (`autonomous-development`: a step
        // needing a human choice surfaces as a decision, not a pause).
        Assert.Contains("3", only.Reason);
        Assert.Contains("retry", only.Reason);
    }

    /// <summary>One short of the limit still starts — the limit is a ceiling, not a suspicion.</summary>
    [Fact]
    public void A_quest_below_the_limit_still_starts()
    {
        var plan = Planner.Plan(
            With([Quest()], new Dictionary<string, int> { ["q1"] = 2 }),
            Config() with { Strikes = 3 });

        Assert.Equal(StartVerdict.Start, Assert.Single(plan).Verdict);
    }

    /// <summary>
    /// 🔴 The additive escape hatch, and the one that must never regress: <c>strikes: 0</c> is
    /// today's behaviour byte for byte, for a person who wants the loop to keep trying.
    /// </summary>
    [Fact]
    public void A_limit_of_zero_never_parks_anything()
    {
        var plan = Planner.Plan(
            With([Quest()], new Dictionary<string, int> { ["q1"] = 99 }),
            Config() with { Strikes = 0 });

        Assert.Equal(StartVerdict.Start, Assert.Single(plan).Verdict);
    }

    /// <summary>
    /// Forgiveness raises the bar rather than erasing the history: the person said "try again from
    /// here", and the records still say what happened. Erasing would make the count a second
    /// register after all — one the driver writes and the record contradicts.
    /// </summary>
    [Fact]
    public void Forgiving_a_quest_lets_it_run_again_without_rewriting_what_happened()
    {
        var config = (Config() with { Strikes = 3 })
            .WithForgiven("q1", 3);

        Assert.Equal(
            StartVerdict.Start,
            Assert.Single(Planner.Plan(With([Quest()], new Dictionary<string, int> { ["q1"] = 3 }), config)).Verdict);

        // …and three more failures past the mark park it again.
        Assert.Equal(
            StartVerdict.Exhausted,
            Assert.Single(Planner.Plan(With([Quest()], new Dictionary<string, int> { ["q1"] = 6 }), config)).Verdict);
    }

    /// <summary>
    /// RETRY1b: a park carries the quest's failures as the records count them, before any mark, which is where *Try again*
    /// marks it. Parked a second time (six failures, retried once at three), a mark at the limit left it parked, as both
    /// doors did on the install; a mark at its failures starts it, and the limit's count parks it again. No other verdict
    /// carries a count.
    /// </summary>
    [Fact]
    public void A_second_park_carries_the_quests_failures_and_a_mark_at_them_starts_it()
    {
        var snapshot = With([Quest()], new Dictionary<string, int> { ["q1"] = 6 });
        var config = (Config() with { Strikes = 3 }).WithForgiven("q1", 3);

        var parked = Assert.Single(Planner.Plan(snapshot, config));
        Assert.Equal(StartVerdict.Exhausted, parked.Verdict);
        Assert.Equal(6, parked.Failures);

        Assert.Equal(StartVerdict.Exhausted, Assert.Single(Planner.Plan(snapshot, config.WithForgiven("q1", config.Strikes))).Verdict);
        var started = Assert.Single(Planner.Plan(snapshot, config.WithForgiven("q1", parked.Failures!.Value)));
        Assert.Equal(StartVerdict.Start, started.Verdict);
        Assert.Null(started.Failures);
        Assert.Equal(
            StartVerdict.Exhausted,
            Assert.Single(Planner.Plan(With([Quest()], new Dictionary<string, int> { ["q1"] = 9 }), config.WithForgiven("q1", 6))).Verdict);
    }

    /// <summary>
    /// A repository held by the person creates no session at all, so a hold can never accumulate
    /// strikes. Asserted rather than assumed: "a held tick must not count" is the question this
    /// design answers structurally, and structure is what a later refactor breaks silently.
    /// </summary>
    [Fact]
    public void A_held_repository_is_held_and_never_exhausted()
    {
        var plan = Planner.Plan(
            With([Quest()], new Dictionary<string, int> { ["q1"] = 99 }),
            Config(holds: ["Game"]) with { Strikes = 3 });

        Assert.Equal(StartVerdict.Held, Assert.Single(plan).Verdict);
    }

    // ── ask and wait (D79) ────────────────────────────────────────────────────────────────────────
    //
    // A session that needs another repository's knowledge asks it and waits: its quest stays TAKEN,
    // marked with the question, and this machine resumes it in the same tree once the question closes.

    private static QuestView WaitingOn(string question, string id = "q1") =>
        Quest(id, status: "Taken") with { Awaits = question };

    private static Snapshot Ran(QuestView[] quests, params (string Quest, PriorSession Session)[] ran) =>
        new(quests, [Repo()], [])
        {
            LastRun = ran.ToDictionary(r => r.Quest, r => r.Session, StringComparer.OrdinalIgnoreCase),
        };

    [Fact]
    public void A_quest_waiting_on_an_open_question_sits_naming_the_question()
    {
        var plan = Planner.Plan(
            Ran([WaitingOn("q2"), Quest("q2", to: "Backend")], ("q1", new PriorSession("s1", "D:/trees/s1"))),
            Config());

        var waiting = Assert.Single(plan, c => c.Quest.Id == "q1");
        Assert.Equal(StartVerdict.Waiting, waiting.Verdict);
        Assert.Contains("#q2", waiting.Reason);
        Assert.Contains("Backend", waiting.Reason);
        Assert.Null(waiting.Resumes);
    }

    /// <summary>
    /// DRIFT1d (D133 §4): a question closed done departing from what the person required is held for their yes, and
    /// stays on the open list — so the quest waiting on it waits too, saying it is the person's yes it waits for, and
    /// the terminal's door to give it.
    /// </summary>
    [Fact]
    public void A_quest_waiting_on_a_question_a_departure_holds_waits_for_the_persons_yes()
    {
        var held = Quest("q2", to: "Backend", status: "Done") with { Held = true };

        var plan = Planner.Plan(Ran([WaitingOn("q2"), held], ("q1", new PriorSession("s1", "D:/trees/s1"))), Config());

        var waiting = Assert.Single(plan, c => c.Quest.Id == "q1");
        Assert.Equal(StartVerdict.Waiting, waiting.Verdict);
        Assert.Contains("#q2", waiting.Reason);
        Assert.Contains("your yes", waiting.Reason);
        Assert.Contains("`daoris-driver quest accept q2`", waiting.Reason);
        Assert.Null(waiting.Resumes);
        Assert.DoesNotContain(plan, c => c.Quest.Id == "q2");
    }

    /// <summary>
    /// The open list holds open and taken quests, and a done a departure holds (DRIFT1d), so a question absent from
    /// it has closed — and the quest resumes where its earlier session left off, carrying which session that was.
    /// </summary>
    [Fact]
    public void Once_its_question_closes_the_waiting_quest_resumes_from_the_session_that_asked()
    {
        var prior = new PriorSession("s1", "D:/trees/s1");

        var only = Assert.Single(Planner.Plan(Ran([WaitingOn("q2")], ("q1", prior)), Config()));

        Assert.Equal(StartVerdict.Start, only.Verdict);
        Assert.Equal(prior, only.Resumes);
        Assert.Contains("#q2", only.Reason);
    }

    /// <summary>
    /// A resume is still a start: the person's hold and the strikes outrank it, exactly as they do an
    /// open quest's — a question answered is no reason to spend an account the person paused.
    /// </summary>
    [Fact]
    public void A_resume_waits_for_the_persons_hold_like_any_start()
    {
        var plan = Planner.Plan(
            Ran([WaitingOn("q2")], ("q1", new PriorSession("s1", null))),
            Config(holds: ["Game"]));

        Assert.Equal(StartVerdict.Held, Assert.Single(plan).Verdict);
    }

    /// <summary>
    /// A waiting quest no session of THIS machine ran is somebody else's take — another machine's, or
    /// a person's — and resuming it here would double their work.
    /// </summary>
    [Fact]
    public void A_waiting_quest_this_machine_never_ran_is_not_its_to_resume()
    {
        Assert.Empty(Planner.Plan(Ran([WaitingOn("q2")]), Config()));
    }

    // ── carrying on after a cut-off (D80) ─────────────────────────────────────────────────────────
    //
    // Found on FG5's second run: the session took its quest, worked half an hour, and the timeout
    // killed it. A failed START leaves a quest open and is retried; a session cut off AFTER its take
    // left the quest taken for good, with the work in its tree. It is carried on, like a retry.

    [Fact]
    public void A_taken_quest_whose_last_session_here_was_cut_off_is_carried_on_from_that_session()
    {
        var prior = new PriorSession("s1", "D:/trees/s1", "failed", "timed out after 30 minutes and was killed.");

        var only = Assert.Single(Planner.Plan(Ran([Quest(status: "Taken")], ("q1", prior)), Config()));

        Assert.Equal(StartVerdict.Start, only.Verdict);
        Assert.Equal(prior, only.Resumes);
        Assert.Contains("s1", only.Reason);
        Assert.Contains("timed out", only.Reason);
    }

    /// <summary>
    /// The strikes bound it, exactly as they bound a retry: a cut-off IS a failed session, so the third
    /// one parks the quest, with the retry door that clears it.
    /// </summary>
    [Fact]
    public void Carrying_on_is_bounded_by_the_strikes()
    {
        var snapshot = Ran([Quest(status: "Taken")], ("q1", new PriorSession("s3", null, "failed", "timed out.")))
            with { Strikes = new Dictionary<string, int> { ["q1"] = 3 } };

        Assert.Equal(StartVerdict.Exhausted, Assert.Single(Planner.Plan(snapshot, Config() with { Strikes = 3 })).Verdict);
    }

    /// <summary>
    /// Only a FAILED last run is a cut-off. A completed one ended well (its close or its wait), and a
    /// taken quest that just sits after one is a person's or another machine's to move.
    /// </summary>
    // ── a chain's next step starts on the step before (CHAIN2) ───────────────────────────────────
    //
    // FG5: the verify step grew a fresh tree from the canonical line and could not see the develop
    // step's work, which sat unmerged on that session's branch. The owner: start on the parent's branch.

    [Fact]
    public void A_next_step_in_the_same_repository_builds_on_its_parents_last_run()
    {
        var develop = new PriorSession("s1", "D:/trees/s-a900f1ad", "completed", "reached done.", Repository: "Game");
        var verify = Quest("q2") with { Parent = "q1" };

        var only = Assert.Single(Planner.Plan(Ran([verify], ("q1", develop)), Config()));

        Assert.Equal(StartVerdict.Start, only.Verdict);
        Assert.Equal(develop, only.BuildsOn);
        Assert.Null(only.Resumes);
    }

    /// <summary>A step to ANOTHER repository has nothing of the parent's in its tree to build on.</summary>
    [Fact]
    public void A_next_step_in_another_repository_starts_from_its_own_canonical_line()
    {
        var elsewhere = new PriorSession("s1", "D:/trees/s-1", "completed", "reached done.", Repository: "Engine");

        var only = Assert.Single(Planner.Plan(Ran([Quest("q2") with { Parent = "q1" }], ("q1", elsewhere)), Config()));

        Assert.Null(only.BuildsOn);
    }

    /// <summary>
    /// STANDDOWN2: the person answered a session that parked to ask them, and its quest is carried on
    /// in the same tree, the reason naming what they said.
    /// </summary>
    [Fact]
    public void A_parked_session_the_person_answered_is_carried_on_from_it()
    {
        var answered = new PriorSession("s1", "D:/trees/s-1", "completed", "asked the person", Answer: "Signed in; go ahead.");

        var only = Assert.Single(Planner.Plan(Ran([Quest(status: "Taken")], ("q1", answered)), Config()));

        Assert.Equal(StartVerdict.Start, only.Verdict);
        Assert.Equal(answered, only.Resumes);
        Assert.Contains("you answered", only.Reason);
        Assert.Contains("Signed in; go ahead.", only.Reason);
    }

    // ── a take the sweep or a shutdown cut off (D104) ─────────────────────────────────────────────
    //
    // Found running the owner's ticket: the orphan sweep ended the take's record `stopped`, and a
    // stopped take was never carried on, so the quest sat taken with nothing to move it. A stop that was
    // not the person's is a cut-off; the person's own stop is their decision.

    [Theory]
    [InlineData(Orphans.Note)]
    [InlineData("the driver was stopped while this ran; the session's process was ended with it.")]
    public void A_taken_quest_whose_last_session_here_was_interrupted_is_carried_on_from_that_session(string note)
    {
        var prior = new PriorSession("s1", "D:/trees/s1", "stopped", note, Interrupted: true);

        var only = Assert.Single(Planner.Plan(Ran([Quest(status: "Taken")], ("q1", prior)), Config()));

        Assert.Equal(StartVerdict.Start, only.Verdict);
        Assert.Equal(prior, only.Resumes);
        Assert.Contains("s1", only.Reason);
        Assert.Contains(note, only.Reason);
    }

    /// <summary>An interrupted take counts against the strikes like any cut-off, and the third parks it.</summary>
    [Fact]
    public void Carrying_on_an_interrupted_take_is_bounded_by_the_strikes()
    {
        var snapshot = Ran([Quest(status: "Taken")], ("q1", new PriorSession("s3", null, "stopped", Orphans.Note, Interrupted: true)))
            with { Strikes = new Dictionary<string, int> { ["q1"] = 3 } };

        Assert.Equal(StartVerdict.Exhausted, Assert.Single(Planner.Plan(snapshot, Config() with { Strikes = 3 })).Verdict);
    }

    [Fact]
    public void A_taken_quest_whose_last_session_here_ended_well_is_not_carried_on()
    {
        Assert.Empty(Planner.Plan(
            Ran([Quest(status: "Taken")], ("q1", new PriorSession("s1", null, "completed", "reached done."))), Config()));
    }

    // ── a carry-on asks whose the take is (CARRY2b) ───────────────────────────────────────────────
    //
    // Since CARRY2 the ledger refuses a carry-on over a take that is not this machine's (`TakenElsewhere`, a 409), and the
    // planner still planned one for every cut-off, so each look asked and was refused: no strike, and the same line again.
    // The take is read as an abandon reads it, by the claim and a record here marking `took`, and the quest sits with the
    // ledger's own sentence.

    private static readonly PriorSession Cut = new("s1", "D:/trees/s1", "failed", "timed out after 30 minutes and was killed.")
    {
        Updated = new DateTimeOffset(2026, 10, 7, 9, 30, 0, TimeSpan.Zero),
    };

    private static Snapshot TakenAfter(PriorSession last, QuestTake? take, DateTimeOffset? takenAt = null) =>
        Ran([Quest(status: "Taken") with { Updated = takenAt }], ("q1", last)) with
        {
            Takes = take is null
                ? new Dictionary<string, QuestTake>()
                : new Dictionary<string, QuestTake>(StringComparer.OrdinalIgnoreCase) { ["q1"] = take },
        };

    [Fact]
    public void A_cut_off_whose_quest_another_machine_took_sits_naming_that_machine_s_session()
    {
        var only = Assert.Single(Planner.Plan(TakenAfter(Cut, new QuestTake("none", Teammate: "alice-laptop/ab12cd34")), Config()));

        Assert.Equal(StartVerdict.TakenElsewhere, only.Verdict);
        Assert.Equal(
            "Quest `#q1` is taken on `alice-laptop`, by session `alice-laptop/ab12cd34`: the take is theirs, so session `s1` is "
            + "not carried on over it.",
            only.Reason);
        Assert.Null(only.Resumes);
    }

    /// <summary>A take that lost the race to the remote (D68 §5) is another machine's; with no record of theirs here, unnamed.</summary>
    [Fact]
    public void A_cut_off_whose_take_was_lost_sits_naming_another_machine()
    {
        var only = Assert.Single(Planner.Plan(TakenAfter(Cut, new QuestTake("lost")), Config()));

        Assert.Equal(StartVerdict.TakenElsewhere, only.Verdict);
        Assert.Equal("Quest `#q1` is taken on another machine: the take is theirs, so session `s1` is not carried on over it.", only.Reason);
    }

    /// <summary>A take made here after the session ended, which no record here marks, is a chat's or work outside Daoris.</summary>
    [Fact]
    public void A_take_made_here_after_the_session_ended_that_no_record_marks_sits()
    {
        var only = Assert.Single(Planner.Plan(TakenAfter(Cut, new QuestTake("unconfirmed"), Cut.Updated!.Value.AddMinutes(5)), Config()));

        Assert.Equal(StartVerdict.TakenElsewhere, only.Verdict);
        Assert.Equal(
            "Quest `#q1` was taken here after session `s1` ended, by a chat or by work outside Daoris: the take is theirs, so "
            + "session `s1` is not carried on over it.",
            only.Reason);
    }

    /// <summary>
    /// 🔴 What the ledger still carries on, the planner plans: a take a record here marks, and an unmarked one made while the
    /// session ran, which the HTTP door makes and the family rehearsal's stub takes by. A take with no time said, and one
    /// not read, are the ledger's to judge, as every carry-on was.
    /// </summary>
    [Fact]
    public void This_machine_s_take_is_carried_on_marked_or_made_while_the_session_ran_and_an_unread_one_is_planned()
    {
        var after = Cut.Updated!.Value.AddMinutes(5);
        var during = Cut.Updated!.Value.AddMinutes(-5);

        foreach (var (why, snapshot) in new[]
                 {
                     ("marked", TakenAfter(Cut, new QuestTake("held", Took: true), after)),
                     ("made while it ran", TakenAfter(Cut, new QuestTake("unconfirmed"), during)),
                     ("no time said", TakenAfter(Cut, new QuestTake("held"))),
                     ("unread", TakenAfter(Cut, take: null, after)),
                 })
        {
            var only = Assert.Single(Planner.Plan(snapshot, Config()));
            Assert.True(only.Verdict == StartVerdict.Start, $"{why}: {only.Verdict} {only.Reason}");
            Assert.Equal(Cut, only.Resumes);
        }
    }

    /// <summary>Every carry-on asks it, as the ledger does: an interrupted stop, a released stop and an answered park too.</summary>
    [Fact]
    public void Every_carry_on_asks_whose_the_take_is()
    {
        var interrupted = new PriorSession("s1", "D:/trees/s1", "stopped", Orphans.Note, Interrupted: true);
        var answered = new PriorSession("s1", "D:/trees/s1", "completed", "asked the person", Answer: "Go ahead.");

        foreach (var (why, last, config) in new[]
                 {
                     ("interrupted", interrupted, Config()),
                     ("released", PersonStop, Config().WithReleased("q1", "s1")),
                     ("answered", answered, Config()),
                 })
        {
            var only = Assert.Single(Planner.Plan(TakenAfter(last, new QuestTake("none")), config));
            Assert.True(only.Verdict == StartVerdict.TakenElsewhere, $"{why}: {only.Verdict} {only.Reason}");
        }
    }

    /// <summary>
    /// Said before every other reason a carry-on sits for, since a take elsewhere stays so whatever is opted in or held here;
    /// and it spends no slot, so the next quest starts where it would have.
    /// </summary>
    [Fact]
    public void A_take_elsewhere_is_said_before_a_hold_and_spends_no_slot()
    {
        var held = Assert.Single(Planner.Plan(TakenAfter(Cut, new QuestTake("none")), Config(holds: ["Game"])));
        Assert.Equal(StartVerdict.TakenElsewhere, held.Verdict);

        var snapshot = TakenAfter(Cut, new QuestTake("none")) with { Quests = [Quest(status: "Taken"), Quest("q2")] };
        var plan = Planner.Plan(snapshot, Config(cap: 1));

        Assert.Equal(StartVerdict.TakenElsewhere, Assert.Single(plan, c => c.Quest.Id == "q1").Verdict);
        Assert.Equal(StartVerdict.Start, Assert.Single(plan, c => c.Quest.Id == "q2").Verdict);
    }

    /// <summary>
    /// The records say the rest of whose a take is: a record of this machine's in any state marks `took` (as the ledger
    /// asks it), and the newest teammate's record that did not stand down names another machine's take. A quest whose
    /// claim was not read is not read.
    /// </summary>
    [Fact]
    public void Whose_a_take_is_is_read_from_the_claim_and_the_records()
    {
        var takes = ServiceClient.ReadTakes("""
            [{ "id": "s1", "quest": "q1", "state": "stood-down", "took": true, "created": "2026-10-07T09:00:00Z" },
             { "id": "s2", "quest": "q1", "state": "failed", "created": "2026-10-07T09:10:00Z" },
             { "id": "alice-laptop/a1", "quest": "q1", "state": "failed", "created": "2026-10-07T09:20:00Z" },
             { "id": "bob-desk/b1", "quest": "q1", "state": "stood-down", "created": "2026-10-07T09:30:00Z" },
             { "id": "s3", "quest": "q2", "state": "failed", "created": "2026-10-07T09:00:00Z" },
             { "id": "carol/c1", "quest": "Q2", "state": "working", "took": true, "created": "2026-10-07T09:00:00Z" },
             { "id": "s4", "quest": "q3", "state": "failed", "took": true, "created": "2026-10-07T09:00:00Z" }]
            """, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["q1"] = "held", ["q2"] = "none" });

        Assert.Equal(new QuestTake("held", Took: true, Teammate: "alice-laptop/a1"), takes["q1"]);
        Assert.Equal(new QuestTake("none", Took: false, Teammate: "carol/c1"), takes["Q2"]);
        Assert.False(takes.ContainsKey("q3"));
    }

    /// <summary>
    /// A look asks the claim of each taken quest it would carry on, and of nothing else: a taken quest whose session still
    /// works is not asked. What the claim says reaches the plan, and a record's and a quest's moments reach the views.
    /// </summary>
    [Fact]
    public async Task A_look_asks_whose_the_take_is_of_each_carry_on_and_of_nothing_else()
    {
        var ledger = new StandInLedger { Claim = "lost" };
        ledger.Publish("q1", "Game");
        ledger.Publish("q2", "Game");
        using var service = ledger.Client();
        var (cut, _) = await service.OpenSessionAsync("q1", "stub");
        await service.AdvanceAsync(cut!, "failed", note: "timed out.");
        var (working, _) = await service.OpenSessionAsync("q2", "stub");
        await service.AdvanceAsync(working!, "working");
        ledger.Move("q1", "Taken");
        ledger.Move("q2", "Taken");

        var snapshot = await service.SnapshotAsync();

        Assert.Equal(new QuestTake("lost"), snapshot.Takes["q1"]);
        Assert.False(snapshot.Takes.ContainsKey("q2"));
        Assert.Equal(1, ledger.ClaimsAsked("q1"));
        Assert.Equal(0, ledger.ClaimsAsked("q2"));
        var sits = Assert.Single(Planner.Plan(snapshot with { Repositories = [Repo()] }, Config()));
        Assert.Equal(StartVerdict.TakenElsewhere, sits.Verdict);
        Assert.Equal($"Quest `#q1` is taken on another machine: the take is theirs, so session `{cut}` is not carried on over it.", sits.Reason);
    }

    /// <summary>A claim the host does not answer in time is unread, and the look plans the carry-on as before: the ledger judges.</summary>
    [Fact]
    public async Task A_claim_the_host_does_not_answer_leaves_the_carry_on_planned()
    {
        var ledger = new StandInLedger { ClaimHangs = true };
        ledger.Publish("q1", "Game");
        using var service = ledger.Client(TimeSpan.FromMilliseconds(300));
        var (cut, _) = await service.OpenSessionAsync("q1", "stub");
        await service.AdvanceAsync(cut!, "failed", note: "timed out.");
        ledger.Move("q1", "Taken");

        var snapshot = await service.SnapshotAsync();

        Assert.Empty(snapshot.Takes);
        Assert.Equal(1, ledger.ClaimsAsked("q1"));
        Assert.Equal(StartVerdict.Start, Assert.Single(Planner.Plan(snapshot with { Repositories = [Repo()] }, Config())).Verdict);
    }

    /// <summary>A record's and a quest's last move are read as the service writes them: what a take here is compared by.</summary>
    [Fact]
    public void A_record_s_and_a_quest_s_last_move_are_read_as_the_service_writes_them()
    {
        var last = ServiceClient.ReadLastRun("""
            [{ "id": "s1", "quest": "q1", "state": "failed", "created": "2026-10-07T09:00:00Z", "updated": "2026-10-07T09:30:00+00:00" },
             { "id": "s2", "quest": "q2", "state": "failed", "created": "2026-10-07T09:00:00Z" }]
            """);
        var quests = ServiceClient.ReadQuests("""
            [{ "id": "q1", "to": "Game", "status": "Taken", "updated": "2026-10-07T09:35:00Z" },
             { "id": "q2", "to": "Game", "status": "Taken" }]
            """);

        Assert.Equal(new DateTimeOffset(2026, 10, 7, 9, 30, 0, TimeSpan.Zero), last["q1"].Updated);
        Assert.Null(last["q2"].Updated);
        Assert.Equal(new DateTimeOffset(2026, 10, 7, 9, 35, 0, TimeSpan.Zero), quests[0].Updated);
        Assert.Null(quests[1].Updated);
    }

    // ── a person's stop holds its quest, and Try again releases it (SESSUX1b, D126 §3.3, §3.4) ───────
    //
    // Read from the code (M3): a take the person stopped was never looked at again, so it sat with no sentence and no
    // way back, and an open quest whose session they stopped before its take was planned again at the next look. A
    // stop that undoes itself, or leaves its quest in silence, is not one a person can manage with.

    private static readonly PriorSession PersonStop = new("s1", "D:/trees/s1", "stopped", "the person stopped it.");

    /// <summary>🔴 The person's stop is their decision: a take they stopped is held, saying so and how to release it, never carried on unasked.</summary>
    [Fact]
    public void A_taken_quest_the_person_stopped_is_held_saying_why()
    {
        var only = Assert.Single(Planner.Plan(Ran([Quest(status: "Taken")], ("q1", PersonStop)), Config()));

        Assert.Equal(StartVerdict.Stopped, only.Verdict);
        Assert.Equal("you stopped session `s1`; Try again carries it on — `daoris driver retry q1 --session s1`.", only.Reason);
        Assert.Equal(PersonStop, only.HeldBy);
        Assert.Null(only.Resumes);
    }

    /// <summary>🔴 An open quest whose session the person stopped before its take is held too, never planned again at the next look.</summary>
    [Fact]
    public void An_open_quest_the_person_stopped_before_its_take_is_held_rather_than_planned_again()
    {
        var stop = PersonStop with { Tree = null };

        var only = Assert.Single(Planner.Plan(Ran([Quest()], ("q1", stop)), Config()));

        Assert.Equal(StartVerdict.Stopped, only.Verdict);
        Assert.Equal("you stopped session `s1`; Try again starts it again — `daoris driver retry q1 --session s1`.", only.Reason);
        Assert.Equal(stop, only.HeldBy);
    }

    /// <summary>
    /// The person's stop is the reason the quest sits, whatever else would hold it: a hold, an opt-out or the strikes would each
    /// say something true, and none of them is what moves it. The stop leaves its tree for review (SessionGroups), which a
    /// verdict naming the quest's take would hide.
    /// </summary>
    [Fact]
    public void A_stop_is_the_reason_a_quest_sits_before_any_other()
    {
        var snapshot = Ran([Quest(status: "Taken")], ("q1", PersonStop)) with { Strikes = new Dictionary<string, int> { ["q1"] = 9 } };

        Assert.Equal(StartVerdict.Stopped, Assert.Single(Planner.Plan(snapshot, Config(holds: ["Game"]))).Verdict);
        Assert.Equal(StartVerdict.Stopped, Assert.Single(Planner.Plan(snapshot, Config(drivable: []))).Verdict);
        Assert.Equal(StartVerdict.Stopped, Assert.Single(Planner.Plan(snapshot, Config() with { Strikes = 3 })).Verdict);
    }

    /// <summary>A stop the sweep or a shutdown made is not the person's (D104): an open quest so stopped is planned as before.</summary>
    [Fact]
    public void An_interrupted_stop_of_an_open_quest_is_planned_as_before_and_never_held()
    {
        var only = Assert.Single(Planner.Plan(
            Ran([Quest()], ("q1", new PriorSession("s1", null, "stopped", Orphans.Note, Interrupted: true))), Config()));

        Assert.Equal(StartVerdict.Start, only.Verdict);
        Assert.Null(only.HeldBy);
    }

    /// <summary>Released, a taken quest is carried on in the tree its stopped session worked in, as a cut-off is (D80).</summary>
    [Fact]
    public void A_released_stop_of_a_taken_quest_is_carried_on_in_its_tree()
    {
        var only = Assert.Single(Planner.Plan(Ran([Quest(status: "Taken")], ("q1", PersonStop)), Config().WithReleased("q1", "s1")));

        Assert.Equal(StartVerdict.Start, only.Verdict);
        Assert.Equal(PersonStop, only.Resumes);
        Assert.Null(only.HeldBy);
        Assert.Equal("carrying on in `Game` — you stopped session `s1`, and released it.", only.Reason);
    }

    /// <summary>Released, an open quest is planned as a first start: its stopped session never took it, so there is no tree to go back into.</summary>
    [Fact]
    public void A_released_stop_of_an_open_quest_is_planned_as_a_first_start()
    {
        var only = Assert.Single(Planner.Plan(Ran([Quest()], ("q1", PersonStop with { Tree = null })), Config().WithReleased("q1", "s1")));

        Assert.Equal(StartVerdict.Start, only.Verdict);
        Assert.Null(only.Resumes);
        Assert.Equal("starting in `Game`.", only.Reason);
    }

    /// <summary>A release names the stop it released, so a later stop holds the quest again: its session differs.</summary>
    [Fact]
    public void A_second_stop_holds_the_quest_again_since_its_session_differs()
    {
        var again = PersonStop with { Session = "s2" };

        var only = Assert.Single(Planner.Plan(Ran([Quest(status: "Taken")], ("q1", again)), Config().WithReleased("q1", "s1")));

        Assert.Equal(StartVerdict.Stopped, only.Verdict);
        Assert.Contains("session `s2`", only.Reason);
    }

    /// <summary>A stop of a quest that waits on a question is held too; released, the quest waits for its answer as before (D79).</summary>
    [Fact]
    public void A_stopped_quest_that_waits_on_a_question_is_held_and_once_released_waits_for_its_answer()
    {
        var snapshot = Ran([WaitingOn("q2"), Quest("q2", to: "Backend")], ("q1", PersonStop));

        Assert.Equal(StartVerdict.Stopped, Assert.Single(Planner.Plan(snapshot, Config()), c => c.Quest.Id == "q1").Verdict);
        Assert.Equal(
            StartVerdict.Waiting,
            Assert.Single(Planner.Plan(snapshot, Config().WithReleased("q1", "s1")), c => c.Quest.Id == "q1").Verdict);
    }

    /// <summary>
    /// RETRY1's mark still starts a parked quest, and a stop after it holds the quest rather than parking it: a stop is not
    /// a strike (D58). Released with its strikes unforgiven, the park is what the quest meets next.
    /// </summary>
    [Fact]
    public void Forgiven_strikes_still_work_and_a_stop_after_them_holds_rather_than_parks()
    {
        var parked = Ran([Quest(status: "Taken")], ("q1", new PriorSession("s3", "D:/trees/s1", "failed", "timed out.")))
            with { Strikes = new Dictionary<string, int> { ["q1"] = 3 } };
        var forgiven = (Config() with { Strikes = 3 }).WithForgiven("q1", 3);
        Assert.Equal(StartVerdict.Exhausted, Assert.Single(Planner.Plan(parked, Config() with { Strikes = 3 })).Verdict);
        Assert.Equal(StartVerdict.Start, Assert.Single(Planner.Plan(parked, forgiven)).Verdict);

        var stopped = parked with { LastRun = new Dictionary<string, PriorSession> { ["q1"] = PersonStop with { Session = "s4" } } };
        Assert.Equal(StartVerdict.Stopped, Assert.Single(Planner.Plan(stopped, forgiven)).Verdict);
        Assert.Equal(StartVerdict.Start, Assert.Single(Planner.Plan(stopped, forgiven.WithReleased("q1", "s4"))).Verdict);
        Assert.Equal(
            StartVerdict.Exhausted,
            Assert.Single(Planner.Plan(stopped, (Config() with { Strikes = 3 }).WithReleased("q1", "s4"))).Verdict);
    }

    /// <summary>
    /// D125 §5.2 stands beside the hold: read from the records as the snapshot reads them, a failure an account's limit made
    /// is no strike and neither is the person's stop, so with one real failure under a limit of two the stop holds the
    /// quest, and released it is carried on, never parked.
    /// </summary>
    [Fact]
    public void A_limit_is_still_not_a_strike_and_neither_is_the_stop_that_holds()
    {
        const string records = """
            [{ "id": "s1", "quest": "q1", "repository": "Game", "state": "failed", "created": "2026-10-02T09:00:00Z" },
             { "id": "s2", "quest": "q1", "repository": "Game", "state": "failed", "limit": true, "created": "2026-10-02T09:10:00Z" },
             { "id": "s3", "quest": "q1", "repository": "Game", "state": "stopped", "note": "the person stopped it.",
               "tree": "D:/trees/s1", "created": "2026-10-02T09:20:00Z" }]
            """;
        var snapshot = new Snapshot([Quest(status: "Taken")], [Repo()], [], ServiceClient.ReadStrikes(records))
        {
            LastRun = ServiceClient.ReadLastRun(records),
        };
        var config = Config() with { Strikes = 2 };

        Assert.Equal(1, snapshot.Strikes["q1"]);
        Assert.Equal(StartVerdict.Stopped, Assert.Single(Planner.Plan(snapshot, config)).Verdict);
        var released = Assert.Single(Planner.Plan(snapshot, config.WithReleased("q1", "s3")));
        Assert.Equal(StartVerdict.Start, released.Verdict);
        Assert.Equal("s3", released.Resumes!.Session);
    }

    // ── the pause (PAUSE1b, D132 points 2–4, design §2.3) ─────────────────────────────────────────────────────
    //
    // A pause stops an ask's work, or one quest's, on this machine and keeps everything: the look hands the planner the quests
    // of each paused work, and the planner's `Paused` verdict is the reason each sits, before every other, the person's stop
    // included. Its sentence names the pause and Resume, with the terminal's line.

    private static readonly PausedBy ByAsk = new(WorkScope.Ask, "a1");

    private static Snapshot Paused(Snapshot snapshot, params (string Quest, PausedBy By)[] paused) =>
        snapshot with { Paused = paused.ToDictionary(p => p.Quest, p => p.By, StringComparer.OrdinalIgnoreCase) };

    /// <summary>An open quest of a paused ask sits, naming the pause and the door that starts it again.</summary>
    [Fact]
    public void An_open_quest_of_a_paused_ask_sits_naming_the_pause_and_its_door()
    {
        var only = Assert.Single(Planner.Plan(Paused(Ran([Quest()]), ("q1", ByAsk)), Config()));

        Assert.Equal(StartVerdict.Paused, only.Verdict);
        Assert.Equal("paused with ask `#a1`; Resume starts it — `daoris-driver ask --resume a1`.", only.Reason);
        Assert.Equal(ByAsk, only.PausedBy);
        Assert.Null(only.HeldBy);
        Assert.Null(only.Root);
    }

    /// <summary>A take the pause stopped sits too: its take, its tree and its strikes stay, and Resume carries it on.</summary>
    [Fact]
    public void A_take_the_pause_stopped_sits_and_Resume_carries_it_on()
    {
        var snapshot = Paused(Ran([Quest(status: "Taken")], ("q1", PersonStop)), ("q1", ByAsk));

        var only = Assert.Single(Planner.Plan(snapshot, Config()));

        Assert.Equal(StartVerdict.Paused, only.Verdict);
        Assert.Equal("paused with ask `#a1`; Resume carries it on — `daoris-driver ask --resume a1`.", only.Reason);
        Assert.Null(only.Resumes);
    }

    /// <summary>A quest paused on its own names itself and the quest's own door.</summary>
    [Fact]
    public void A_quest_paused_on_its_own_names_itself_and_its_own_door()
    {
        var byQuest = new PausedBy(WorkScope.Quest, "q1");

        var open = Assert.Single(Planner.Plan(Paused(Ran([Quest()]), ("q1", byQuest)), Config()));
        var taken = Assert.Single(Planner.Plan(Paused(Ran([Quest(status: "Taken")], ("q1", PersonStop)), ("q1", byQuest)), Config()));

        Assert.Equal("you paused `#q1`; Resume starts it — `daoris-driver quest resume q1`.", open.Reason);
        Assert.Equal("you paused `#q1`; Resume carries it on — `daoris-driver quest resume q1`.", taken.Reason);
        Assert.Equal(byQuest, taken.PausedBy);
    }

    /// <summary>
    /// 🔴 The pause comes before every other reason, the person's stop included (design §2.3, amending SESSUX1b's note): while
    /// it holds, a release of a stop starts nothing, and Resume is the one press that moves the quest.
    /// </summary>
    [Fact]
    public void A_pause_is_the_reason_a_quest_sits_before_any_other_the_persons_stop_included()
    {
        var snapshot = Paused(Ran([Quest(status: "Taken")], ("q1", PersonStop)), ("q1", ByAsk))
            with { Strikes = new Dictionary<string, int> { ["q1"] = 9 } };

        Assert.Equal(StartVerdict.Paused, Assert.Single(Planner.Plan(snapshot, Config())).Verdict);
        Assert.Equal(StartVerdict.Paused, Assert.Single(Planner.Plan(snapshot, Config().WithReleased("q1", "s1"))).Verdict);
        Assert.Equal(StartVerdict.Paused, Assert.Single(Planner.Plan(snapshot, Config(holds: ["Game"]))).Verdict);
        Assert.Equal(StartVerdict.Paused, Assert.Single(Planner.Plan(snapshot, Config(drivable: []))).Verdict);
        Assert.Equal(StartVerdict.Paused, Assert.Single(Planner.Plan(snapshot, Config() with { Strikes = 3 })).Verdict);
    }

    /// <summary>
    /// What an answer would start, a carry-on or the answered park going on (D131), and a waiting quest's resume once its
    /// question is answered, each wait for Resume (design §2.1, §2.2).
    /// </summary>
    [Fact]
    public void An_answered_park_and_an_answered_wait_each_wait_for_Resume()
    {
        var answered = new PriorSession("s1", "D:/trees/s1", "awaiting-person", Answer: "use the second one");
        var waited = new PriorSession("s1", "D:/trees/s1", "completed");

        Assert.Equal(
            StartVerdict.Paused,
            Assert.Single(Planner.Plan(Paused(Ran([Quest(status: "Taken")], ("q1", answered)), ("q1", ByAsk)), Config())).Verdict);
        Assert.Equal(
            StartVerdict.Paused,
            Assert.Single(Planner.Plan(Paused(Ran([WaitingOn("q9")], ("q1", waited)), ("q1", ByAsk)), Config())).Verdict);
    }

    /// <summary>
    /// A paused quest keeps its place and takes nobody else's: it spends no slot of the cap and no turn of its repository, so
    /// the next quest starts where it would have, and on Resume the paused one starts first again, being the older.
    /// </summary>
    [Fact]
    public void A_paused_quest_spends_no_slot_and_no_turn_so_the_next_starts_where_it_would_have()
    {
        var plan = Planner.Plan(Paused(Ran([Quest("q1"), Quest("q2")]), ("q1", ByAsk)), Config(cap: 1));

        Assert.Equal(StartVerdict.Paused, Assert.Single(plan, c => c.Quest.Id == "q1").Verdict);
        Assert.Equal(StartVerdict.Start, Assert.Single(plan, c => c.Quest.Id == "q2").Verdict);
        Assert.Equal(["q1", "q2"], Planner.Plan(Ran([Quest("q1"), Quest("q2")]), Config(cap: 1)).Select(c => c.Quest.Id));
        Assert.Equal(StartVerdict.Start, Planner.Plan(Ran([Quest("q1"), Quest("q2")]), Config(cap: 1))[0].Verdict);
    }

    /// <summary>
    /// A take this machine never ran, another machine's or one made outside Daoris, is still not this planner's: a pause
    /// changes nothing there, so no verdict is said of it (design §2.1). A parked session's quest, which waits on the
    /// person, is not planned either: the pause leaves it parked.
    /// </summary>
    [Fact]
    public void A_paused_quest_this_machine_would_not_plan_gets_no_verdict()
    {
        var parked = new PriorSession("s1", "D:/trees/s1", "awaiting-person");

        Assert.Empty(Planner.Plan(Paused(Ran([Quest(status: "Taken")]), ("q1", ByAsk)), Config()));
        Assert.Empty(Planner.Plan(Paused(Ran([Quest(status: "Taken")], ("q1", parked)), ("q1", ByAsk)), Config()));
    }

    /// <summary>The look's paused set, from the work (design §2.3): every quest of each paused ask's work, and of each paused quest's.</summary>
    [Fact]
    public void The_paused_set_is_every_quest_of_each_paused_work()
    {
        QuestView Asked(string id, string from, string status = "Open") => new(id, from, "Game", $"Ask {id}", "Body.", status);
        var look = new AskWorkLook(
            [Asked("q1", "ask #a1", "Done"), Asked("q2", "ask #a1"), Asked("q3", "Game") with { PublishedBy = "s1" },
             Asked("q4", "ask #b2"), Asked("q5", "Asker"), Asked("q6", "Asker")],
            [new SessionRecord("s1", "Game", "completed") { Quest = "q1" }]);
        var config = Config()
            .WithPausedAsk("a1", new WorkPause(null, new Dictionary<string, string>()))
            .WithPausedQuest("q5", new WorkPause(null, new Dictionary<string, string>()));

        var paused = PausedWork.Set(look, config);

        Assert.Equal(["q1", "q2", "q3", "q5"], paused.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new PausedBy(WorkScope.Ask, "a1"), paused["q3"]);
        Assert.Equal(new PausedBy(WorkScope.Quest, "q5"), paused["Q5"]);
        Assert.Empty(PausedWork.Set(look, Config()));
    }

    /// <summary>A quest paused on its own inside a paused ask's work is said by its own pause: the narrower, which the person made of it.</summary>
    [Fact]
    public void A_quests_own_pause_is_said_before_its_asks()
    {
        var look = new AskWorkLook([new QuestView("q1", "ask #a1", "Game", "Ask q1", "Body.", "Open")], []);
        var config = Config()
            .WithPausedAsk("a1", new WorkPause(null, new Dictionary<string, string>()))
            .WithPausedQuest("q1", new WorkPause(null, new Dictionary<string, string>()));

        Assert.Equal(new PausedBy(WorkScope.Quest, "q1"), PausedWork.Set(look, config)["q1"]);
    }
}
