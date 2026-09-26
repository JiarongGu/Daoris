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
    /// The open list holds open and taken quests only, so a question absent from it has closed — and
    /// the quest resumes where its earlier session left off, carrying which session that was.
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
    [Fact]
    public void A_taken_quest_whose_last_session_here_ended_well_is_not_carried_on()
    {
        Assert.Empty(Planner.Plan(
            Ran([Quest(status: "Taken")], ("q1", new PriorSession("s1", null, "completed", "reached done."))), Config()));
    }
}
