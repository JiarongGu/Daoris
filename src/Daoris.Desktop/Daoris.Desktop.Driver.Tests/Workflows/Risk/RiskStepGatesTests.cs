using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A review's Accept and a task's approval are held by the risk policy's judge: with nothing configured each passes, reached by the policy
/// with its rule on its words; classed <c>role</c> each waits on the step's role as the place casts it, an agent holder answering through
/// <c>step_settle</c> and the work's own session refused <c>own-work</c>; classed <c>owner</c> the owner role holds it. Seen failing first:
/// the Accept and the approval always waited, and no agent's channel settled either.
/// </summary>
public sealed class RiskStepGatesTests : NodeRunFixture
{
    private const string Seed = """{"risk":{}}""";

    private const string Reviewed = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"review"},
                  {"id":"r>h","kind":"after","from":"review","to":"handed"}]}
        """;

    private static string Signed(string act = "") => $$"""
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"sign","kind":"task","version":1,"reach":"pressed","outcomes":["approved"],"role":"approver"{{act}}},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>s","kind":"after","from":"claimed","to":"sign"},
                  {"id":"s>h","kind":"when","from":"sign","to":"handed","outcome":"approved"}]}
        """;

    private PlaceCastFiles Casts => new(_home, () => true);

    [Fact]
    public async Task With_nothing_configured_an_approval_passes_approved_by_the_policy()
    {
        OnBranch(Signed());
        await RunPassAsync(Runtime(), Seed);

        var sign = RunStatus("sign");
        Assert.Equal((NodeStates.Reached, NodeSource.Policy, "approved"), (sign.State, sign.By, sign.Acceptance?.Outcome));
    }

    [Fact]
    public async Task Classed_role_an_approval_waits_on_the_cast_approver_and_refuses_its_author()
    {
        Casts.Cast("machine", StepRoles.Approver, "agent:reviewer", "the reviewer signs", _now);
        OnBranch(Signed());
        await RunPassAsync(Runtime());
        Assert.Equal(NodeStates.Active, RunStatus("sign").State);

        Assert.Equal(SettleDoor.OwnWork, Settle("sign", "s-worker", "approved").Refused);
        Assert.True(Settle("sign", "s-reviewer", "approved").Done);
    }

    [Fact]
    public async Task An_approval_spending_a_destination_classed_owner_waits_on_the_owner()
    {
        OnBranch(Signed(""","options":{"act":"release.start/production"}"""));
        await RunPassAsync(Runtime(), """{"risk":{"machine":[{"kind":"release.start/production","class":"owner"}]}}""");

        Assert.Equal(NodeStates.Active, RunStatus("sign").State);
        var held = new StepTenureFiles(_home, () => true).Current(RunUnit, "sign", RunStatus("sign").Attempt);
        Assert.Equal((CastHolders.Person, TenureWhy.Marked), (held?.Holder, held?.Why));
    }

    [Fact]
    public async Task A_review_the_policy_passes_journals_its_verdict_once_and_a_wait_held_for_the_person_is_told_once()
    {
        Casts.Cast("machine", StepRoles.Acceptor, CastHolders.Person, "mine", _now);
        OnBranch(Reviewed);
        var runtime = Runtime();
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("review").State);

        var passed = await RunPassAsync(runtime, Seed);
        var again = await RunPassAsync(runtime, Seed);

        Assert.Equal(NodeStates.Reached, RunStatus("review").State);
        var line = Assert.Single(RiskJournal.Read(_home), each => each.Today == PolicyPasses.Today);
        Assert.Equal((RiskActs.WorkAccept, RiskVerdict.Pass, RiskVerdict.Seed, "review"), (line.Kind, line.Outcome, line.Source, line.Step));
        Assert.Single(passed.Said, said => said.Contains("passed by the risk policy: no rule gates `work.accept` here", StringComparison.Ordinal));
        Assert.DoesNotContain(again.Said, said => said.Contains(PolicyPasses.Today, StringComparison.Ordinal));
    }

    [Fact]
    public async Task With_the_shadow_wired_as_production_wires_it_a_wait_the_person_held_before_the_update_gets_its_pass_line_and_is_told_once()
    {
        Casts.Cast("machine", StepRoles.Acceptor, CastHolders.Person, "mine", _now);
        OnBranch(Reviewed);
        var runtime = Runtime();
        runtime.Risk = new RiskShadow(_home, () => _now);
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("review").State);
        Assert.Contains(RiskJournal.Read(_home), each => each.Stage == RiskJudged.Opened && each.Step == "review");

        var passed = await RunPassAsync(runtime, Seed);
        var again = await RunPassAsync(runtime, Seed);

        Assert.Equal((NodeStates.Reached, NodeSource.Policy), (RunStatus("review").State, RunStatus("review").By));
        var line = Assert.Single(RiskJournal.Read(_home), each => each.Today == PolicyPasses.Today);
        Assert.Equal((RiskActs.WorkAccept, RiskVerdict.Pass, "review"), (line.Kind, line.Outcome, line.Step));
        Assert.Single(passed.Said, said => said.Contains("passed by the risk policy: no rule gates `work.accept` here", StringComparison.Ordinal));
        Assert.DoesNotContain(again.Said, said => said.Contains(PolicyPasses.Today, StringComparison.Ordinal));
    }

    [Fact]
    public async Task At_a_place_marked_for_production_releases_an_approvers_approval_waits_on_the_owner_with_nothing_configured()
    {
        OnBranch(Signed());
        await RunPassAsync(Runtime(), """{"risk":{},"placeRisk":{"machine":{"risk":"low","marks":["release.production"]}}}""");

        Assert.Equal(NodeStates.Active, RunStatus("sign").State);
        var held = new StepTenureFiles(_home, () => true).Current(RunUnit, "sign", RunStatus("sign").Attempt);
        Assert.Equal((CastHolders.Person, TenureWhy.Marked), (held?.Holder, held?.Why));
    }

    [Theory]
    [InlineData(PersonWaits.Approval)]
    [InlineData(PersonWaits.Accept)]
    public void A_hold_whose_role_is_the_owners_is_the_owners_whatever_the_policy_says(string why)
    {
        var node = why == PersonWaits.Accept
            ? new NodeDeclaration("review", NodeKinds.Review, 1, NodeReach.Pressed) { Role = StepRoles.Owner }
            : new NodeDeclaration("sign", NodeKinds.Task, 1, NodeReach.Pressed) { Outcomes = ["approved"] };
        var judge = RiskStepGates.At(DriverConfig.Parse("""{"risk":{}}"""), null, "engine");

        Assert.Equal(RiskVerdict.OwnerOutcome, RiskStepGates.Verdict(node, why, judge)?.Outcome);
        Assert.Equal(RiskVerdict.Pass, RiskStepGates.Verdict(node with { Role = why == PersonWaits.Accept ? StepRoles.Acceptor : StepRoles.Approver }, why, judge)?.Outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("staging")]
    [InlineData("production")]
    public void At_a_marked_place_every_release_act_is_the_owners(string? qualifier)
    {
        var config = DriverConfig.Parse("""{"risk":{},"placeRisk":{"workspace/games":{"risk":"low","marks":["release.production"]}}}""");

        var verdict = RiskGate.Judge(RiskPolicy.Of(config), new RiskAct(RiskActs.ReleaseStart, qualifier), "", RiskPlace.Of(config, "games", null), "");

        Assert.Equal((RiskVerdict.OwnerOutcome, "workspace/games"), (verdict.Outcome, verdict.Source));
    }

    [Fact]
    public async Task A_branch_bound_review_passes_by_the_seed_and_its_words_carry_the_rule_and_its_revision()
    {
        OnBranch(Reviewed);
        await RunPassAsync(Runtime(), Seed);
        Assert.Matches(@"rule seed at [0-9a-f]{12}\)", RunStatus("review").Words);
    }

    [Fact]
    public async Task A_policy_row_elsewhere_gives_the_passed_review_the_written_revision_in_its_words()
    {
        OnBranch(Reviewed);
        await RunPassAsync(Runtime(), """{"risk":{"machine":[{"kind":"work.done","class":"none"}]}}""");

        var words = RunStatus("review").Words;
        Assert.Matches(@"rule seed at [0-9a-f]{12}\)", words);
        Assert.DoesNotContain("at seed)", words, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_approval_naming_an_act_passes_by_the_seed_and_journals_that_act()
    {
        OnBranch(Signed(""","options":{"act":"release.start/production"}"""));
        await RunPassAsync(Runtime(), Seed);

        Assert.Equal((NodeStates.Reached, NodeSource.Policy, "approved"), (RunStatus("sign").State, RunStatus("sign").By, RunStatus("sign").Acceptance?.Outcome));
        var line = Assert.Single(RiskJournal.Read(_home), each => each.Today == PolicyPasses.Today);
        Assert.Equal((RiskActs.ReleaseStart, "production", "sign"), (line.Kind, line.Qualifier, line.Step));
    }

    [Theory]
    [InlineData("""{"risk":5}""")]
    [InlineData("""{"risk":{"machine":"owner"}}""")]
    [InlineData("""{"risk":{"nowhere":[]}}""")]
    [InlineData("""{"risk":{"machine":[{"kind":"work.accept"}]}}""")]
    [InlineData("""{"risk":{"machine":[{"kind":"work.accept","class":"none","mode":"never"}]}}""")]
    public async Task A_malformed_risk_block_judges_every_hold_the_owners_and_holds_a_review_for_the_person(string config)
    {
        var policy = RiskPolicy.Of(DriverConfig.Parse(config));
        Assert.NotNull(policy.Fault);
        var place = RiskPlace.Of(DriverConfig.Parse(config), null, "engine");
        foreach (var kind in RiskActs.All)
        {
            var verdict = RiskGate.Judge(policy, new RiskAct(kind, null), "", place, "");
            Assert.Equal((RiskVerdict.OwnerOutcome, kind == RiskActs.PolicyChange ? RiskVerdict.Fixed : RiskVerdict.Unreadable), (verdict.Outcome, verdict.Source));
        }

        OnBranch(Reviewed);
        await RunPassAsync(Runtime(), config);
        Assert.Equal(NodeStates.Active, RunStatus("review").State);
        Assert.Equal(CastHolders.Person, ReviewHolder()?.Holder);
    }

    [Theory]
    [InlineData("workspace/games", "games", null)]
    [InlineData("repository/engine", null, "engine")]
    [InlineData("repository/engine", "games", "engine")]
    public void A_workspace_and_a_repository_place_key_class_an_act_at_its_own_place_only(string place, string? workspace, string? repository)
    {
        var config = DriverConfig.Parse("{\"risk\":{\"" + place + "\":[{\"kind\":\"work.accept\",\"class\":\"owner\"}]}}");
        var policy = RiskPolicy.Of(config);
        var accept = new RiskAct(RiskActs.WorkAccept, null);

        var here = RiskGate.Judge(policy, accept, "", RiskPlace.Of(config, workspace, repository), "");
        var elsewhere = RiskGate.Judge(policy, accept, "", RiskPlace.Of(config, "other", "elsewhere"), "");

        Assert.Equal((RiskVerdict.OwnerOutcome, place), (here.Outcome, here.Source));
        Assert.Equal((RiskVerdict.Pass, RiskVerdict.Seed), (elsewhere.Outcome, elsewhere.Source));
    }

    [Theory]
    [InlineData("\"release.strat/production\"")]
    [InlineData("\"release.start/Production\"")]
    [InlineData("\"Release.Start/production\"")]
    [InlineData("\"release.start/\"")]
    [InlineData("\"task.approve/x\"")]
    [InlineData("5")]
    [InlineData("[\"release.start/production\"]")]
    public void An_act_option_that_names_no_act_of_the_census_is_refused_in_the_graph_and_judged_owner_at_runtime(string written)
    {
        var node = new NodeDeclaration("sign", NodeKinds.Task, 1, NodeReach.Pressed)
        {
            Outcomes = ["approved"], Role = StepRoles.Approver, Options = new Dictionary<string, string> { ["act"] = written },
        };
        var judge = RiskStepGates.At(DriverConfig.Parse("""{"risk":{}}"""), null, "engine");

        Assert.Equal("act", NodeDeclarations.Problem(node));
        var verdict = RiskStepGates.Verdict(node, PersonWaits.Approval, judge);
        Assert.Equal(RiskVerdict.OwnerOutcome, verdict?.Outcome);
        Assert.Contains("names no act of the census", verdict?.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"release.start/production\"", RiskVerdict.Pass)]
    [InlineData("\"release.start\"", RiskVerdict.Pass)]
    [InlineData("\"task.approve\"", RiskVerdict.Pass)]
    public void An_act_option_that_is_spelled_as_the_census_names_it_is_judged_by_its_row(string written, string outcome)
    {
        var node = new NodeDeclaration("sign", NodeKinds.Task, 1, NodeReach.Pressed)
        {
            Outcomes = ["approved"], Role = StepRoles.Approver, Options = new Dictionary<string, string> { ["act"] = written },
        };
        var judge = RiskStepGates.At(DriverConfig.Parse("""{"risk":{}}"""), null, "engine");

        Assert.Null(NodeDeclarations.Problem(node));
        Assert.Equal(outcome, RiskStepGates.Verdict(node, PersonWaits.Approval, judge)?.Outcome);
    }

    private void OnBranch(string graph)
    {
        BoundTo(graph);
        _branchTips["daoris/s1"] = Tip1;
        var done = RunHome.Quest(RunId, "Done") with { Submission = new QuestSubmissionView("m1", 1, _now, "answer 1") };
        _units = [RunHome.QuestUnit(done) with { Executors = ["s-worker"] }, RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
    }

    private NodeWorded Settle(string node, string session, string? outcome = null, bool bare = false) =>
        StepSettles.Settle(Files, new StepSettleAsk(RunId, node, bare ? null : outcome ?? (node == "review" ? "accepted" : "approved"), "Read it: it does what was asked.", new Dictionary<string, string>()),
            new SettleBy("agent:reviewer") { Session = session }, () => _now, ["s-worker"]);

    [Fact]
    public async Task With_nothing_configured_a_runs_review_passes_by_the_policy_with_its_rule()
    {
        OnBranch(Reviewed);
        await RunPassAsync(Runtime(), Seed);

        var review = RunStatus("review");
        Assert.Equal((NodeStates.Reached, NodeSource.Policy), (review.State, review.By));
        Assert.Contains(RiskStepGates.Passes, review.Words, StringComparison.Ordinal);
        Assert.Contains("seed", review.Words, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Classed_role_the_review_waits_on_its_cast_agent_who_accepts_through_its_tool_and_the_worker_is_refused()
    {
        Assert.Null(AgentSettles.CastAt(Casts, "machine", StepRoles.Acceptor, "agent:reviewer", "the reviewer accepts", _now));
        OnBranch(Reviewed);
        await RunPassAsync(Runtime());
        Assert.Equal(NodeStates.Active, RunStatus("review").State);

        var worker = Settle("review", "s-worker");
        var reviewer = Settle("review", "s-reviewer");

        Assert.Equal(SettleDoor.OwnWork, worker.Refused);
        Assert.True(reviewer.Done, reviewer.Refused);
        await RunPassAsync(Runtime());
        Assert.Equal(NodeStates.Reached, RunStatus("review").State);
    }

    [Fact]
    public async Task Classed_role_a_review_its_agent_holds_by_default_is_said_vacant_once_and_gives_up_past_its_bound()
    {
        const string Bound = """{"vacancyHours":2}""";
        OnBranch(Reviewed);
        var runtime = Runtime();
        var first = await RunPassAsync(runtime, Bound);
        Assert.Equal(NodeStates.Active, RunStatus("review").State);
        Assert.Single(first.Said, line => line.Contains("is vacant:", StringComparison.Ordinal) && line.Contains("2 hours", StringComparison.Ordinal));

        _now = _now.AddHours(1);
        var again = await RunPassAsync(runtime, Bound);
        Assert.DoesNotContain(again.Said, line => line.Contains("vacant", StringComparison.Ordinal));
        Assert.Equal(NodeStates.Active, RunStatus("review").State);

        _now = _now.AddHours(2);
        var past = await RunPassAsync(runtime, Bound);
        Assert.Contains(past.Said, line => line.Contains("gave up:", StringComparison.Ordinal) && line.Contains("Tried:", StringComparison.Ordinal));
        await RunPassAsync(runtime, Bound);
        Assert.NotEqual(NodeStates.Active, RunStatus("review").State);
    }

    private const string AcceptOwner = """{"risk":{"machine":[{"kind":"work.accept","class":"owner"}]}}""";

    private StepTenure? ReviewHolder() => new StepTenureFiles(_home, () => true).Current(RunUnit, "review", RunStatus("review").Attempt);

    [Fact]
    public async Task A_settle_of_a_review_or_an_approval_with_no_outcome_is_refused_naming_the_outcomes_and_never_taken_as_the_yes()
    {
        OnBranch(Reviewed);
        await RunPassAsync(Runtime());
        var review = Settle("review", "s-reviewer", bare: true);
        Assert.Equal(RunNodeWords.NotOutcome, review.Refused);
        Assert.Contains("accepted, changes", StepSettles.Said(review), StringComparison.Ordinal);
        Assert.Equal(RunNodeWords.NotOutcome, Settle("review", "s-reviewer", "approved").Refused);
        Assert.Empty(Files.Read(RunUnit)!.Pending);
        Assert.True(Settle("review", "s-reviewer").Done);
    }

    [Fact]
    public async Task A_raise_to_owner_while_the_review_waits_moves_it_to_the_person_and_a_lowering_to_role_gives_it_back_to_its_cast_holder()
    {
        Casts.Cast("machine", StepRoles.Acceptor, "agent:reviewer", "the reviewer accepts", _now);
        OnBranch(Reviewed);
        var runtime = Runtime();
        await RunPassAsync(runtime);
        Assert.Equal(("agent:reviewer", TenureWhy.Cast, 1), (ReviewHolder()?.Holder, ReviewHolder()?.Why, ReviewHolder()?.Epoch));

        await RunPassAsync(runtime, AcceptOwner);
        Assert.Equal((CastHolders.Person, TenureWhy.Marked, 2), (ReviewHolder()?.Holder, ReviewHolder()?.Why, ReviewHolder()?.Epoch));
        await RunPassAsync(runtime, AcceptOwner);
        Assert.Equal(2, ReviewHolder()?.Epoch);

        await RunPassAsync(runtime);
        Assert.Equal(("agent:reviewer", TenureWhy.Cast, 3), (ReviewHolder()?.Holder, ReviewHolder()?.Why, ReviewHolder()?.Epoch));
        Assert.Equal(NodeStates.Active, RunStatus("review").State);
    }

    [Fact]
    public async Task A_lowering_of_an_owner_class_to_the_seed_releases_the_review_by_the_policy()
    {
        OnBranch(Reviewed);
        var runtime = Runtime();
        await RunPassAsync(runtime, AcceptOwner);
        Assert.Equal(NodeStates.Active, RunStatus("review").State);

        await RunPassAsync(runtime, Seed);

        Assert.Equal((NodeStates.Reached, NodeSource.Policy), (RunStatus("review").State, RunStatus("review").By));
    }

    [Fact]
    public async Task Classed_owner_the_review_waits_on_the_person()
    {
        OnBranch(Reviewed);
        await RunPassAsync(Runtime(), """{"risk":{"machine":[{"kind":"work.accept","class":"owner"}]}}""");

        Assert.Equal(NodeStates.Active, RunStatus("review").State);
        var held = new StepTenureFiles(_home, () => true).Current(RunUnit, "review", RunStatus("review").Attempt);
        Assert.Equal((CastHolders.Person, TenureWhy.Marked), (held?.Holder, held?.Why));
    }
}
