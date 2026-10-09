using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// XAGENT1f (D155 points 9 and 10; the second-agent design §7–§8): the second opinion's part of the landing gate, as a state
/// table. The level first (nothing set, a repository's none, a rule that reads only before each next step, a conversation's work);
/// then the person's answer covering the tip; the chain's later step here; the newest pass asked and how far it went (being read,
/// with the working session, read again, disputed, unavailable, commits since, settled); D154's <i>Reviewed</i> and a press that
/// answer what they showed; and the one gate's order. Pure, with git's ancestry and the host stood in for: the fast half (MOD8).
/// <see cref="OpinionLandingTests"/> holds the doors over real git.
/// </summary>
public sealed class OpinionGateTests
{
    private const string Read = "1111111111111111111111111111111111111111";
    private const string Fixed = "2222222222222222222222222222222222222222";
    private const string Later = "3333333333333333333333333333333333333333";
    private static readonly DateTimeOffset Asked = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Answered = new(2026, 10, 9, 9, 40, 0, TimeSpan.Zero);

    private static ResolvedOpinion Rule(bool required = false, bool recheck = true, string[]? on = null) =>
        new(new OpinionRule(["codex-acp"], on ?? [OpinionRules.Landing], required, Recheck: recheck), OpinionSource.Repository);

    private static QuestView Quest(string id = "q1", string status = "Done", bool held = false) =>
        new(id, "ask #a1", "web-app", $"Work {id}", "", status) { Held = held, Hold = held ? EvidenceCodes.MissingHold : null };

    private static OpinionView Opinion(string id = "o1", string state = "given", string tip = Read, string pass = "first", params string[] weights) =>
        new(id, "landing", pass, "s1", "r-" + id, "web-app", "0000000000000000000000000000000000000000", tip, "codex-acp", ReviewerLabels.AnotherMaker, state)
        {
            Product = "Codex", Maker = "OpenAI", Minutes = 20, Why = state == "failed" ? "out-of-time" : null,
            Findings = state == "given" ? [.. weights.Select((weight, at) => new OpinionFindingView(at + 1, weight, "src/a.ts:1", "claim", "", "", "likely"))] : null,
            Read = "src/a.ts",
        };

    private static OpinionAskKept AskOf(string? opinion, string? code = null, DateTimeOffset? at = null, DateTimeOffset? until = null) =>
        new(at ?? Asked, "landing", "s1", Read) { Opinion = opinion, Code = code, Until = until };

    /// <summary>The host's opinions, the driver's deliveries, and git's ancestry, stood in: <paramref name="tip"/> is what would land.</summary>
    private sealed class World
    {
        public Dictionary<string, OpinionView> Opinions { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, OpinionDelivered> Delivered { get; } = new(StringComparer.Ordinal);

        /// <summary>Each commit, and the commits it holds: what <c>merge-base --is-ancestor</c> would answer.</summary>
        public Dictionary<string, string[]> Holds { get; } = new(StringComparer.Ordinal)
        {
            [Read] = [Read],
            [Fixed] = [Read, Fixed],
            [Later] = [Read, Fixed, Later],
        };

        public OpinionReads Reads(string tip) => new(
            id => Task.FromResult(Opinions.GetValueOrDefault(id)),
            id => Delivered.GetValueOrDefault(id) ?? OpinionDelivered.None,
            commit => Task.FromResult<bool?>(Holds.TryGetValue(commit, out var held) && held.Contains(tip)),
            from => Task.FromResult<int?>(Holds[tip].Length - Holds[from].Length));
    }

    private static OpinionGateFacts Facts(ResolvedOpinion? rule, string tip = Read, QuestView? quest = null) =>
        new("web-app", rule) { Session = "s1", Quest = quest ?? Quest(), Tip = tip };

    private static Task<OpinionGateState> JudgeAsync(OpinionGateFacts facts, World? world = null) =>
        OpinionGate.JudgeAsync(facts, (world ?? new World()).Reads(facts.Tip ?? Read));

    // ——— the level

    [Fact]
    public async Task Nothing_set_anywhere_is_today_and_lets_the_work_go()
    {
        var gate = await JudgeAsync(Facts(null));

        Assert.Equal(OpinionGateStates.None, gate.State);
        Assert.True(gate.LetsGo);
        Assert.Equal("Nothing waits for a second opinion here.", gate.Says);
        Assert.Null(gate.Landing);
    }

    [Fact]
    public async Task A_repositorys_none_and_a_rule_that_reads_only_before_each_next_step_ask_nothing_at_a_landing()
    {
        var none = await JudgeAsync(Facts(new ResolvedOpinion(OpinionRule.None, OpinionSource.Repository)));
        var steps = await JudgeAsync(Facts(Rule(on: [OpinionRules.Steps])) with { Asks = [AskOf(null, ReviewerUnavailable.NoReviewer)] });

        Assert.Equal((OpinionGateStates.None, true), (none.State, none.LetsGo));
        Assert.Equal((OpinionGateStates.None, true), (steps.State, steps.LetsGo));
        Assert.Equal("No second opinion waits for this work: `web-app`'s rule reads before each next step, not before landing.", steps.Says);
    }

    [Fact]
    public async Task A_conversations_work_waits_for_no_opinion()
    {
        var gate = await JudgeAsync(Facts(Rule(required: true)) with { Quest = null });

        Assert.Equal(OpinionGateStates.None, gate.State);
        Assert.True(gate.LetsGo);
    }

    // ——— before any pass

    [Fact]
    public async Task A_chain_with_a_later_step_here_waits_for_its_last_step_so_one_opinion_reads_the_whole_work()
    {
        var gate = await JudgeAsync(Facts(Rule()) with { Later = "q2" });

        Assert.Equal(OpinionGateStates.WaitsChain, gate.State);
        Assert.False(gate.LetsGo);
        Assert.Contains("quest `#q2` still works there", gate.Says);
        Assert.Contains("`daoris-driver opinion ask s1`", gate.Says);
        Assert.Contains("`daoris-driver opinion anyway s1 \"…\"`", gate.Says);
    }

    [Fact]
    public async Task Done_work_nothing_asked_of_waits_for_the_next_look_and_a_held_quest_comes_first()
    {
        var open = await JudgeAsync(Facts(Rule()));
        var held = await JudgeAsync(Facts(Rule(), quest: Quest(held: true)));

        Assert.Equal((OpinionGateStates.NotAsked, false, false), (open.State, open.LetsGo, open.Held));
        Assert.Equal("Waits for a second opinion: it is asked at the driver's next look. `daoris-driver opinion ask s1` asks it now.", open.Says);
        Assert.True(held.Held);
        Assert.Equal("Waits for a second opinion: it is asked once the hold on its quest is lifted, since that comes first.", held.Says);
        Assert.True(OpinionGateStates.AtWork(open.State));
    }

    // ——— unavailable: required governs only absence (§8.4)

    [Fact]
    public async Task No_reviewer_holds_where_the_rule_requires_one_and_rides_beside_where_it_does_not()
    {
        var required = await JudgeAsync(Facts(Rule(required: true)) with { Asks = [AskOf(null, ReviewerUnavailable.NoReviewer)] });
        var optional = await JudgeAsync(Facts(Rule()) with { Asks = [AskOf(null, ReviewerUnavailable.NoReviewer)] });

        Assert.Equal((OpinionGateStates.Unavailable, false), (required.State, required.LetsGo));
        Assert.StartsWith("No second opinion: no listed reviewer of another maker is installed. The rule requires one, so the work waits for you:", required.Says);
        Assert.Contains("with `--same-agent` asks the same agent in a fresh conversation", required.Says);
        Assert.Contains("`daoris-driver opinion myself s1 \"…\"`", required.Says);
        Assert.Equal((OpinionGateStates.Unavailable, true), (optional.State, optional.LetsGo));
        Assert.EndsWith("The rule does not require one, so nothing waits for it.", optional.Says);
        Assert.Equal(new LandingOpinion(OpinionGateStates.Unavailable) { Code = ReviewerUnavailable.NoReviewer }.Code, optional.Landing!.Code);
    }

    [Fact]
    public async Task A_cool_off_says_its_reset_and_a_pass_out_of_time_is_unavailable_with_its_code()
    {
        var until = new DateTimeOffset(2026, 10, 9, 14, 30, 0, TimeSpan.Zero);
        var cooling = await JudgeAsync(Facts(Rule(required: true)) with { Asks = [AskOf(null, ReviewerUnavailable.Cooling, until: until)] });
        var world = new World();
        world.Opinions["o1"] = Opinion(state: "failed");
        var late = await JudgeAsync(Facts(Rule(required: true)) with { Asks = [AskOf("o1")] }, world);

        Assert.Contains("every listed reviewer of another maker is cooling until 2026-10-09 14:30 UTC, when the driver asks again", cooling.Says);
        Assert.Equal((OpinionGateStates.Unavailable, "out-of-time"), (late.State, late.Code));
        Assert.Contains("Codex (OpenAI) ran out of its minutes before it said one", late.Says);
    }

    [Fact]
    public async Task The_newest_ask_decides_a_later_pass_after_none_could_be_had()
    {
        var world = new World();
        world.Opinions["o1"] = Opinion(weights: []);
        var gate = await JudgeAsync(Facts(Rule(required: true)) with
        {
            Asks = [AskOf(null, ReviewerUnavailable.NoReviewer), AskOf("o1", at: Asked.AddMinutes(5))],
        }, world);

        Assert.Equal(OpinionGateStates.Settled, gate.State);
        Assert.Equal(1, gate.Passes);
    }

    // ——— being read, answered, read again

    [Fact]
    public async Task Being_read_holds_and_names_its_reader_its_bound_and_the_stop()
    {
        var world = new World();
        world.Opinions["o1"] = Opinion(state: "reading");
        var gate = await JudgeAsync(Facts(Rule()) with { Asks = [AskOf("o1")] }, world);

        Assert.Equal((OpinionGateStates.Reading, false), (gate.State, gate.LetsGo));
        Assert.Equal("Waits for a second opinion: being read by Codex (OpenAI), another maker's agent, for at most 20 minutes. "
            + "`daoris-driver opinion stop o1` stops it.", gate.Says);
    }

    [Fact]
    public async Task Findings_not_yet_answered_are_with_the_working_session()
    {
        var world = new World();
        world.Opinions["o1"] = Opinion(weights: [OpinionViews.Must, "note"]);
        var gate = await JudgeAsync(Facts(Rule()) with { Asks = [AskOf("o1")] }, world);

        Assert.Equal(OpinionGateStates.WithSession, gate.State);
        Assert.Equal("Waits for a second opinion: the working session is answering 2 findings by Codex (OpenAI).", gate.Says);
    }

    [Fact]
    public async Task A_fix_counted_with_the_recheck_on_is_read_again_before_anything_settles()
    {
        var world = new World();
        world.Opinions["o1"] = Opinion(weights: [OpinionViews.Must]);
        world.Delivered["o1"] = new OpinionDelivered(null, null, Answers(Fixed, (OpinionViews.Must, OpinionViews.Fixed)));
        var due = await JudgeAsync(Facts(Rule(), Fixed) with { Asks = [AskOf("o1")] }, world);

        world.Delivered["o1"] = world.Delivered["o1"] with { Recheck = "o2" };
        world.Opinions["o2"] = Opinion("o2", state: "reading", tip: Fixed, pass: "recheck");
        var reading = await JudgeAsync(Facts(Rule(), Fixed) with { Asks = [AskOf("o1")] }, world);

        Assert.Equal(OpinionGateStates.ReadAgain, due.State);
        Assert.Equal(OpinionGateStates.ReadAgain, reading.State);
        Assert.Equal("Waits for a second opinion: read again by Codex (OpenAI), the commits made in answer.", reading.Says);
    }

    [Fact]
    public async Task A_recheck_that_withdraws_the_must_settles_the_work_it_read()
    {
        var world = new World();
        world.Opinions["o1"] = Opinion(weights: [OpinionViews.Must, "should"]);
        world.Delivered["o1"] = new OpinionDelivered(null, null, Answers(Fixed, (OpinionViews.Must, OpinionViews.Rejected), ("should", OpinionViews.Fixed)))
        {
            Recheck = "o2",
        };
        world.Opinions["o2"] = Opinion("o2", tip: Fixed, pass: "recheck", weights: []) with
        {
            Rechecked = new Dictionary<int, string> { [1] = OpinionViews.Withdrawn },
        };
        var gate = await JudgeAsync(Facts(Rule(), Fixed) with { Asks = [AskOf("o1")] }, world);

        Assert.Equal((OpinionGateStates.Settled, true), (gate.State, gate.LetsGo));
        Assert.Equal("Second opinion settled: Codex (OpenAI) read it at `22222222`, and its 2 findings were answered by the working session with no dispute open.", gate.Says);
        var landing = gate.Landing!;
        Assert.Equal((OpinionGateStates.Settled, "o1", "codex-acp", Fixed, 2, 0), (landing.Said, landing.Opinion, landing.Reviewer, landing.Tip, landing.Passes, landing.Disputes));
        Assert.Equal(1, landing.Weights[OpinionViews.Must]);
        Assert.Equal(1, landing.Answers[OpinionViews.Rejected]);
    }

    // ——— disputed, and commits since (§8.2–§8.3)

    [Fact]
    public async Task A_must_rejected_with_the_recheck_off_is_disputed_and_waits_for_the_persons_press()
    {
        var world = new World();
        world.Opinions["o1"] = Opinion(weights: [OpinionViews.Must]);
        world.Delivered["o1"] = new OpinionDelivered(null, null, Answers(Read, (OpinionViews.Must, OpinionViews.Rejected)));
        var gate = await JudgeAsync(Facts(Rule(recheck: false)) with { Asks = [AskOf("o1")] }, world);

        Assert.Equal((OpinionGateStates.Disputed, false, true), (gate.State, gate.LetsGo, gate.PressAnswers));
        Assert.StartsWith("1 finding by Codex (OpenAI) is disputed: a `must` the working session did not fix and no recheck withdrew.", gate.Says);
        Assert.Contains("`daoris-driver sessions say s1 \"…\"` sends it back", gate.Says);
    }

    [Fact]
    public async Task Findings_that_went_to_the_person_stand_unanswered_and_each_must_is_disputed()
    {
        var world = new World();
        world.Opinions["o1"] = Opinion(weights: [OpinionViews.Must, OpinionViews.Must, "note"]);
        world.Delivered["o1"] = new OpinionDelivered(ContinueWhy.StoodDown, Asked, null);
        var gate = await JudgeAsync(Facts(Rule()) with { Asks = [AskOf("o1")] }, world);

        Assert.Equal((OpinionGateStates.Disputed, 2), (gate.State, gate.Disputes!.Count));
        Assert.Equal(ContinueWhy.StoodDown, gate.ToPerson);
    }

    [Fact]
    public async Task Commits_after_what_was_read_hold_once_the_cap_is_spent_and_a_dispute_says_them_too()
    {
        var world = new World();
        world.Opinions["o1"] = Opinion(weights: []);
        var since = await JudgeAsync(Facts(Rule(), Later) with { Asks = [AskOf("o1")] }, world);

        Assert.Equal((OpinionGateStates.CommitsSince, 2, false), (since.State, since.Since, since.LetsGo));
        Assert.StartsWith("2 commits since were not read by another agent: Codex (OpenAI) read it at `11111111`", since.Says);
    }

    [Fact]
    public async Task Git_that_cannot_say_what_was_read_holds_and_says_so()
    {
        var world = new World();
        world.Opinions["o1"] = Opinion(weights: []);
        var reads = world.Reads(Later) with { Covers = _ => Task.FromResult<bool?>(null), Since = _ => Task.FromResult<int?>(null) };
        var gate = await OpinionGate.JudgeAsync(Facts(Rule(), Later) with { Asks = [AskOf("o1")] }, reads);

        Assert.Equal((OpinionGateStates.Unread, false), (gate.State, gate.LetsGo));
        Assert.Contains("Nothing lands until it can be.", gate.Says);
    }

    // ——— the person's answers

    [Fact]
    public async Task Go_on_anyway_at_the_tip_lets_it_go_and_the_landing_keeps_what_it_answered()
    {
        var world = new World();
        world.Opinions["o1"] = Opinion(weights: [OpinionViews.Must]);
        world.Delivered["o1"] = new OpinionDelivered(null, null, Answers(Read, (OpinionViews.Must, OpinionViews.Unresolved)));
        var word = new OpinionPersonWord(OpinionPersonSaid.Anyway, Read, Answered) { Words = "a typo, it is fine", Door = ReviewDoors.Terminal };
        var gate = await JudgeAsync(Facts(Rule(recheck: false)) with { Asks = [AskOf("o1")], Person = [word] }, world);

        Assert.Equal((OpinionGateStates.Anyway, true, OpinionGateStates.Disputed), (gate.State, gate.LetsGo, gate.Answered));
        Assert.Equal("You went on without a settled second opinion over 1 disputed finding: \"a typo, it is fine\".", gate.Says);
        Assert.Equal((OpinionGateStates.Anyway, OpinionPersonSaid.Anyway, "a typo, it is fine", 1), (gate.Landing!.Said, gate.Landing.Person, gate.Landing.Words, gate.Landing.Disputes));
    }

    [Fact]
    public async Task An_answer_given_at_an_older_commit_does_not_cover_work_made_after_it()
    {
        var word = new OpinionPersonWord(OpinionPersonSaid.Myself, Read, Answered);
        var mine = await JudgeAsync(Facts(Rule(required: true)) with { Asks = [AskOf(null, ReviewerUnavailable.NoReviewer)], Person = [word] });
        var after = await JudgeAsync(Facts(Rule(required: true), Later) with { Asks = [AskOf(null, ReviewerUnavailable.NoReviewer)], Person = [word] });

        Assert.Equal((OpinionGateStates.Myself, true), (mine.State, mine.LetsGo));
        Assert.Equal("You looked at it yourself in place of another agent's reading.", mine.Says);
        Assert.Equal((OpinionGateStates.Unavailable, false), (after.State, after.LetsGo));
    }

    [Fact]
    public async Task D154s_reviewed_given_after_the_disputes_stood_answers_them_and_one_given_before_does_not()
    {
        var world = new World();
        world.Opinions["o1"] = Opinion(weights: [OpinionViews.Must]);
        world.Delivered["o1"] = new OpinionDelivered(null, null, Answers(Read, (OpinionViews.Must, OpinionViews.Rejected)));
        ReviewGateState Reviewed(DateTimeOffset at) => new(ReviewStates.Reviewed, new ReviewDecision(ReviewLevels.Repository, "local"))
        {
            Verdict = new QuestReviewVerdictView(ReviewVerdicts.Reviewed) { At = at, Words = "reads right" },
        };

        var after = await JudgeAsync(Facts(Rule(recheck: false)) with { Asks = [AskOf("o1")], Review = Reviewed(Answered.AddMinutes(1)) }, world);
        var before = await JudgeAsync(Facts(Rule(recheck: false)) with { Asks = [AskOf("o1")], Review = Reviewed(Answered.AddMinutes(-1)) }, world);

        Assert.Equal((OpinionGateStates.Answered, true), (after.State, after.LetsGo));
        Assert.Equal("Your Reviewed answered the second opinion over 1 disputed finding: \"reads right\".", after.Says);
        Assert.Equal(OpinionGateStates.Disputed, before.State);
    }

    [Fact]
    public async Task A_press_answers_only_the_gate_it_drew_and_the_gates_order_is_the_opinion_then_the_look()
    {
        var world = new World();
        world.Opinions["o1"] = Opinion(weights: []);
        var since = await JudgeAsync(Facts(Rule(), Later) with { Asks = [AskOf("o1")] }, world);
        var unreviewed = new ReviewGateState(ReviewStates.NotShown, new ReviewDecision(ReviewLevels.Repository, "local") { Repository = "web-app" });
        var both = new LandingGate(since, unreviewed);

        Assert.True(both.Answers(since.Token));
        Assert.False(both.Answers("o1:" + Read + ":commits-since:0:2"));
        Assert.False(both.Answers(null));
        Assert.Equal((AutoLandingCode.Opinion, since.Says), (both.Refusal!.Refusal, both.Refusal.Message));

        var settled = since with { State = OpinionGateStates.Settled };
        Assert.Equal(AutoLandingCode.Unreviewed, new LandingGate(settled, unreviewed).Refusal!.Refusal);
        Assert.Null(LandingGate.Open("web-app").Refusal);
    }

    // ——— the chain's later step, and what `trees land --plan` says

    [Fact]
    public void A_later_step_is_a_work_step_here_still_to_run_or_one_still_composed()
    {
        var chain = new List<QuestView>
        {
            Quest("q1"),
            Quest("q2", "Open") with { Parent = "q1", To = "web-app" },
        };
        var composed = new List<QuestView>
        {
            Quest("q1"),
            Quest("q2", "Taken") with { Parent = "q1", To = "api", Then = [new QuestStepView("web-app", "Next", "")] },
        };
        var setUp = new List<QuestView> { Quest("q1"), Quest("q2", "Open") with { Parent = "q1", SetUpIn = "local" } };

        Assert.Equal("q2", OpinionGate.LaterStep(chain, "q1", "web-app"));
        Assert.Null(OpinionGate.LaterStep(chain, "q2", "web-app"));
        Assert.Null(OpinionGate.LaterStep(chain, "q1", "api"));
        Assert.Equal("q2", OpinionGate.LaterStep(composed, "q1", "web-app"));
        Assert.Null(OpinionGate.LaterStep(setUp, "q1", "web-app"));
    }

    [Fact]
    public async Task The_plan_says_each_part_that_asks_anything_in_the_gates_order()
    {
        var world = new World();
        world.Opinions["o1"] = Opinion(state: "reading");
        var reading = await JudgeAsync(Facts(Rule()) with { Asks = [AskOf("o1")] }, world);
        var none = new ReviewGateState(ReviewStates.None, new ReviewDecision(ReviewLevels.Nothing, null) { Repository = "web-app" });
        var shown = new ReviewGateState(ReviewStates.NotShown, new ReviewDecision(ReviewLevels.Repository, "local") { Repository = "web-app" });

        Assert.Empty(LandingGateWords.Plan(LandingGate.Open("web-app")));
        Assert.Equal([$"trees: {reading.Says}"], LandingGateWords.Plan(new LandingGate(reading, none)));
        Assert.Equal([$"trees: {reading.Says}", $"trees: {shown.Says}"], LandingGateWords.Plan(new LandingGate(reading, shown)));
    }

    // ——— the planner's sit (§7, §8.1)

    [Fact]
    public void The_planner_sits_a_quest_the_look_found_waiting_for_a_second_opinion_saying_why_and_starts_the_rest()
    {
        var step = Quest("q2", "Open") with { Parent = "q1" };
        var other = Quest("q3", "Open");
        var registered = new RepoView("web-app", Adopted: true, Root: "C:/work/web-app", Workspace: "work");
        var said = "waits for a second opinion on the step before it, `#q1`, before it starts: Waits for a second opinion: being read by Codex.";
        var snapshot = new Snapshot([step, other], [registered], [])
        {
            Opinions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["q2"] = said },
        };

        var plan = Planner.Plan(snapshot, DriverConfig.Empty with { Drivable = ["web-app"], Cap = 3 });

        var sits = Assert.Single(plan, considered => considered.Quest.Id == "q2");
        Assert.Equal((StartVerdict.WaitsForOpinion, said), (sits.Verdict, sits.Reason));
        Assert.NotEqual(StartVerdict.WaitsForOpinion, Assert.Single(plan, considered => considered.Quest.Id == "q3").Verdict);
    }

    // ——— what the driver keeps at the gate

    [Fact]
    public void The_gates_record_keeps_each_ask_request_and_answer_and_a_session_takes_only_its_own_requests()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-opinion-gates-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var gates = new OpinionGates(home);
            gates.Asked("q1", "web-app", AskOf("o1") with { Reviewer = "codex-acp", Label = ReviewerLabels.AnotherMaker });
            gates.Asked("q1", "web-app", AskOf(null, ReviewerUnavailable.Cooling, until: Answered) with { By = OpinionAskers.Person });
            gates.Requested("q1", "web-app", new OpinionRequest(Asked, "s1", "asked") { SameAgent = true, Words = "fresh eyes" });
            gates.Requested("q1", "web-app", new OpinionRequest(Asked, "s2", "failure") { Reviewer = "dsh" });
            gates.Said("q1", "Web-App", new OpinionPersonWord(OpinionPersonSaid.Anyway, Read, Answered) { Words = "fine", Door = ReviewDoors.Screen });

            var kept = gates.Read("q1", "web-app");
            Assert.Equal(2, kept.Asks.Count);
            Assert.Equal(("o1", "codex-acp", ReviewerLabels.AnotherMaker), (kept.Asks[0].Opinion, kept.Asks[0].Reviewer, kept.Asks[0].Label));
            Assert.Equal((ReviewerUnavailable.Cooling, Answered, OpinionAskers.Person), (kept.Asks[1].Code, kept.Asks[1].Until, kept.Asks[1].By));
            Assert.Equal(new OpinionPersonWord(OpinionPersonSaid.Anyway, Read, Answered) { Words = "fine", Door = ReviewDoors.Screen }, Assert.Single(kept.Person));

            var taken = Assert.Single(gates.Take("q1", "web-app", "s1"));
            Assert.Equal((true, "fresh eyes"), (taken.SameAgent, taken.Words));
            Assert.Equal("s2", Assert.Single(gates.Read("q1", "web-app").Requests).Session);
            Assert.Same(OpinionGateKept.None, gates.Read("q1", "api"));
            Assert.Null(gates.PathOf("../q1"));
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void An_opinion_owed_is_opened_again_for_the_same_session_and_closed_with_why()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-opinion-dues-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var dues = new OpinionDues(home);
            dues.Due(new OpinionDue("s1", "q1", "web-app", "work", "/trees/a", OpinionRules.Landing, Asked));
            dues.Due(new OpinionDue("s1", "q1", "web-app", "work", "/trees/a", OpinionRules.Steps, Asked));
            dues.Close("s1", OpinionRules.Steps, OpinionDueClosed.Past, Answered);
            dues.Due(new OpinionDue("s1", "q1", "web-app", "work", "/trees/a", OpinionRules.Landing, Answered));

            Assert.Equal((OpinionRules.Landing, Answered), (Assert.Single(dues.Open()).Occasion, dues.Open()[0].At));
            Assert.Equal((OpinionDueClosed.Past, Answered), (dues.All()[0].Why, dues.All()[0].Closed));
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
        }
    }

    private static OpinionReading Answers(string tip, params (string Weight, string Counts)[] rows) =>
        new("s1", tip, Answered, [.. rows.Select((row, at) => new OpinionAnswerRead(at + 1, row.Weight, row.Counts)
        {
            Said = row.Counts, Fix = row.Counts == OpinionViews.Fixed ? Fixed : null,
        })]);
}
