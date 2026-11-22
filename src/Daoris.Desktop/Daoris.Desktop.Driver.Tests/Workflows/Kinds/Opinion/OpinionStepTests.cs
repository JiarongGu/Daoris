using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GateFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The <c>opinion</c> step on a graph run while it stands on someone: with its findings handed to the work's session it waits on
/// that session's answer, said so and never <i>working</i>; a pass whose reviewer ended without an opinion fails it, saying so.
/// </summary>
public sealed class OpinionStepTests
{
    private const string Run = "q1";

    private static readonly string Self = RunUnits.IdOf(Run);

    private const string Digest = "gph-0123456789abcdef0123456789abcdef";

    private const string ClaimedOpinionHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"opinion","kind":"opinion","version":1,"reach":"observed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
         "wires":[{"id":"c>o","kind":"after","from":"claimed","to":"opinion"},{"id":"o>h","kind":"after","from":"opinion","to":"handed"}]}
        """;

    private static WorkflowRun Derive(NodeStatus opinion, OpinionGateState gate)
    {
        var (graph, problems) = NodeGraphs.Read(ClaimedOpinionHanded);
        Assert.True(graph is not null, string.Join("; ", problems.Select(problem => problem.Words)));
        var binding = new WorkflowRunBinding(Run, "engine", "default", "docs", WorkflowLevels.Repository, Then) { Version = 1, Digest = Digest };
        var facts = new WorkflowRunFacts("engine", new CurrentWorkflow([], [], Digest))
        {
            Quests = [new QuestView(Run, "ask:a1", "engine", "The work", "Body.", "Taken")],
            Sessions = [new TracedSession("s1", "engine", "awaiting-step") { Quest = Run, Tree = Path.Combine("trees", "q1-tree"), Created = Then }],
            Process = WorkflowProcess.Declared(DriverConfig.Empty, "engine", "default") with { Binding = binding, Run = Run },
            Opinion = gate,
        };
        var record = new NodeRecord(Self)
        {
            Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["claimed"] = new(Self, "claimed", NodeStates.Reached) { Attempt = 1 },
                ["opinion"] = opinion,
            },
        };
        return WorkflowGraphRuns.Derive(new WorkflowGraphRunFacts(facts, Run, graph, record), "default", "a1");
    }

    private static NodeStatus Opinion(string state, string? holds = null, string? code = null) =>
        new(Self, "opinion", state) { Attempt = 1, Holds = holds, Code = code, Since = Then };

    /// <summary>
    /// 🔴 Its findings with the work's session, the step waits on that session's answer to the opinion: a wait of the opinion's kind naming
    /// the session, held by that agent, in the run's view and in the terminal's words; never <i>working</i>, and never the person's.
    /// </summary>
    [Fact]
    public void An_opinion_with_the_works_session_waits_on_its_answer_and_says_whose()
    {
        var gate = new OpinionGateState(OpinionGateStates.WithSession, "engine") { Session = "s1", Tip = Tip };

        var run = Derive(Opinion(NodeStates.Active, OpinionGateStates.WithSession), gate);

        var step = run.Steps.Single(each => each.Step.Id == "opinion");
        Assert.Equal(WorkflowRunStates.Waiting, step.State);
        Assert.Equal((OpinionStepWaits.Answer, "s1"), (step.Wait?.Code, step.Wait?.Values.GetValueOrDefault("session")));
        Assert.Equal((StepHolders.Agent, "s1"), (step.Holder?.Kind, step.Holder?.Name));
        Assert.Contains("Waits on `s1`'s answer to the second opinion.", WorkflowRunWords.Say([run], null));
        Assert.Contains(OpinionStepWaits.Answer, NodeKit.Of(NodeKinds.Opinion) is BuiltInNode built ? built.Waits : []);

        // A pass still reading is the reviewer's wait, never the session's answer.
        var reading = Derive(Opinion(NodeStates.Active, OpinionGateStates.Reading), gate with { State = OpinionGateStates.Reading, Opinion = Pass("reading") });
        Assert.NotEqual(OpinionStepWaits.Answer, reading.Steps.Single(each => each.Step.Id == "opinion").Wait?.Code);
    }

    private static OpinionView Pass(string state, string pass = "first", string id = "o1") =>
        new(id, "landing", pass, "s1", "r-" + id, "engine", "0000000000000000000000000000000000000000", Tip, "codex-acp", ReviewerLabels.AnotherMaker, state)
        {
            Product = "Codex", Maker = "OpenAI", Minutes = 20, Due = Then.AddMinutes(20),
        };

    private static WorkflowRunStep Opinion(WorkflowRun run) => run.Steps.Single(each => each.Step.Id == "opinion");

    /// <summary>🔴 A pass still reading is the reviewer's wait, said with who reads it and until when; a recheck is its own wait, naming its
    /// reviewer, never a bare <i>working</i> and never the person's.</summary>
    [Theory]
    [InlineData(OpinionGateStates.Reading, "first", true, OpinionStepWaits.ReadingUntil, "Waits on Codex (OpenAI)'s reading of the work")]
    [InlineData(OpinionGateStates.Reading, "first", false, OpinionStepWaits.Reading, "Waits on Codex (OpenAI)'s reading of the work")]
    [InlineData(OpinionGateStates.ReadAgain, "recheck", true, OpinionStepWaits.RecheckingUntil, "Waits on Codex (OpenAI)'s recheck of the commits made in answer")]
    [InlineData(OpinionGateStates.ReadAgain, "recheck", false, OpinionStepWaits.Rechecking, "Waits on Codex (OpenAI)'s recheck of the commits made in answer")]
    public void A_pass_still_reading_is_the_reviewers_wait_and_names_who_reads_it(string state, string pass, bool due, string code, string said)
    {
        var read = Pass("reading", pass, pass == "first" ? "o1" : "o2") with { Due = due ? Then.AddMinutes(20) : null };
        var gate = new OpinionGateState(state, "engine") { Session = "s1", Tip = Tip, Opinion = pass == "first" ? read : Pass("given"), Recheck = pass == "first" ? null : read };

        var run = Derive(Opinion(NodeStates.Active, state), gate);

        var step = Opinion(run);
        Assert.Equal(WorkflowRunStates.Waiting, step.State);
        Assert.Equal((code, "Codex (OpenAI)", due ? Then.AddMinutes(20) : (DateTimeOffset?)null), (step.Wait?.Code, step.Wait?.Values.GetValueOrDefault("agent"), step.Wait?.Until));
        Assert.Equal((StepHolders.Agent, "Codex (OpenAI)"), (step.Holder?.Kind, step.Holder?.Name));
        Assert.Contains(said, WorkflowRunWords.Say([run], null));
        Assert.Contains(code, ((BuiltInNode)NodeKit.Of(NodeKinds.Opinion)!).Waits);
    }

    /// <summary>🔴 A required gate no pass could settle for a reason no vacancy names (a provider that kept failing, a choice refused for another
    /// cause) is the person's wait, saying why: their doors answer it. An optional one is passed over and holds nothing.</summary>
    [Theory]
    [InlineData(OpinionViews.Transient, "transient")]
    [InlineData("unheard-of", "other")]
    public void A_required_gate_no_pass_could_settle_waits_on_the_person_saying_why(string code, string why)
    {
        var rule = new ResolvedOpinion(new OpinionRule(["codex-acp"], [OpinionRules.Landing], Required: true), OpinionSource.Repository);
        var gate = new OpinionGateState(OpinionGateStates.Unavailable, "engine") { Session = "s1", Tip = Tip, Code = code, Rule = rule };

        var run = Derive(Opinion(NodeStates.Active, OpinionGateStates.Unavailable), gate);

        var step = Opinion(run);
        Assert.Equal(WorkflowRunStates.Waiting, step.State);
        Assert.Equal((OpinionStepWaits.Unavailable, why, StepHolders.Person), (step.Wait?.Code, step.Wait?.Values["why"], step.Holder?.Kind));
        Assert.Contains("no second opinion could be had", WorkflowRunWords.Say([run], null));
        Assert.Contains(OpinionStepWaits.Unavailable, ((BuiltInNode)NodeKit.Of(NodeKinds.Opinion)!).Waits);
    }

    /// <summary>🔴 A gate nothing has asked yet says why: the driver's next look asks it (an agent's wait), the quest's own hold comes first
    /// or a chat run waits for the person's press (the person's wait); the recheck not yet asked is said as such.</summary>
    [Theory]
    [InlineData(false, false, false, false, OpinionStepWaits.NotAsked, StepHolders.Agent, "Waits for the driver's next look to ask the second opinion")]
    [InlineData(true, false, false, false, OpinionStepWaits.NotAskedHeld, StepHolders.Person, "Waits on the lift of the hold on its quest")]
    [InlineData(false, true, false, false, OpinionStepWaits.NotAskedPress, StepHolders.Person, "Waits for you to press Accept")]
    [InlineData(false, true, true, false, OpinionStepWaits.NotAsked, StepHolders.Agent, "Waits for the driver's next look to ask the second opinion")]
    [InlineData(false, false, false, true, OpinionStepWaits.NotAskedRecheck, StepHolders.Agent, "Waits for the driver's next look to ask the recheck")]
    public void A_gate_nothing_has_asked_yet_says_why_and_who_holds_it(bool held, bool chat, bool asked, bool recheck, string code, string holder, string said)
    {
        var gate = new OpinionGateState(recheck ? OpinionGateStates.ReadAgain : OpinionGateStates.NotAsked, "engine")
        {
            Session = "s1", Tip = Tip, Held = held, Run = chat ? "chat-1" : null, Asked = asked, Opinion = recheck ? Pass("given") : null,
        };

        var run = Derive(Opinion(NodeStates.Active, gate.State), gate);

        var step = Opinion(run);
        Assert.Equal(WorkflowRunStates.Waiting, step.State);
        Assert.Equal((code, holder), (step.Wait?.Code, step.Holder?.Kind));
        Assert.Contains(said, WorkflowRunWords.Say([run], null));
        Assert.Contains(code, ((BuiltInNode)NodeKit.Of(NodeKinds.Opinion)!).Waits);
    }

    /// <summary>🔴 A gate git could not read says so with why, and one a chain's later step still holds names that step's quest.</summary>
    [Fact]
    public void An_unread_gate_and_one_the_chain_holds_are_each_a_wait_of_their_own()
    {
        var unread = new OpinionGateState(OpinionGateStates.Unread, "engine") { Session = "s1", Tip = Tip, Problem = "git could not say whether the commits are read" };
        var run = Derive(Opinion(NodeStates.Active, OpinionGateStates.Unread), unread);
        var step = Opinion(run);
        Assert.Equal((WorkflowRunStates.Waiting, OpinionStepWaits.Unread, "git could not say whether the commits are read"),
            (step.State, step.Wait?.Code, step.Wait?.Values.GetValueOrDefault("why")));
        Assert.Contains("could not be read: git could not say whether the commits are read", WorkflowRunWords.Say([run], null));

        var chain = new OpinionGateState(OpinionGateStates.WaitsChain, "engine") { Session = "s1", Tip = Tip, Later = "q2" };
        run = Derive(Opinion(NodeStates.Active, OpinionGateStates.WaitsChain), chain);
        step = Opinion(run);
        Assert.Equal((WorkflowRunStates.Waiting, OpinionStepWaits.WaitsChain, "q2"), (step.State, step.Wait?.Code, step.Wait?.Values.GetValueOrDefault("quest")));
        Assert.Contains("quest #q2", WorkflowRunWords.Say([run], null));
        foreach (var code in new[] { OpinionStepWaits.Unread, OpinionStepWaits.WaitsChain }) Assert.Contains(code, ((BuiltInNode)NodeKit.Of(NodeKinds.Opinion)!).Waits);
    }

    /// <summary>🔴 A dispute names whose <c>must</c> each is: the first pass's the working session left unfixed, or the recheck's own, in the
    /// wait and in the gate's own sentence.</summary>
    [Fact]
    public void A_dispute_names_whose_must_it_is()
    {
        var gate = new OpinionGateState(OpinionGateStates.Disputed, "engine")
        {
            Session = "s1", Tip = Tip, Disputes = new OpinionDisputes([1, 3], [2]), Opinion = Pass("given") with { Working = "s1" },
        };

        var run = Derive(Opinion(NodeStates.Active, OpinionGateStates.Disputed), gate);

        var wait = Opinion(run).Wait!;
        Assert.Equal(("3", "2", "1"), (wait.Values["count"], wait.Values["first"], wait.Values["raised"]));
        Assert.Contains("3 disputed findings of the second opinion (2 the working session left unfixed, 1 the recheck's own)", WorkflowRunWords.Say([run], null));
        Assert.Contains("2 the working session did not fix and no recheck withdrew, 1 the recheck's own", gate.Says);

        var firstOnly = gate with { Disputes = new OpinionDisputes([1], []) };
        Assert.Contains("a `must` the working session did not fix and no recheck withdrew", firstOnly.Says);
        var recheckOnly = gate with { Disputes = new OpinionDisputes([], [2]) };
        Assert.Contains("a `must` its recheck raised itself", recheckOnly.Says);
    }

    /// <summary>🔴 A required gate nobody is free to read (no reviewer of another maker, a cool-off until a time, a signed-out account) is the
    /// supervisor's vacancy, a wait said on the step in the run's view, never a bare <i>working</i>.</summary>
    [Theory]
    [InlineData(ReviewerUnavailable.NoReviewer)]
    [InlineData(ReviewerUnavailable.Cooling)]
    [InlineData(ReviewerUnavailable.SignedOut)]
    public void A_required_gate_nobody_is_free_to_read_is_a_wait_on_the_step_never_working(string code)
    {
        var rule = new ResolvedOpinion(new OpinionRule(["codex-acp"], [OpinionRules.Landing], Required: true), OpinionSource.Repository);
        var gate = new OpinionGateState(OpinionGateStates.Unavailable, "engine")
        {
            Session = "s1", Tip = Tip, Code = code, Until = code == ReviewerUnavailable.Cooling ? Then.AddHours(2) : null, Rule = rule,
        };

        var run = Derive(Opinion(NodeStates.Active, OpinionGateReading.Vacant), gate);

        var step = Opinion(run);
        Assert.Equal(WorkflowRunStates.Waiting, step.State);
        Assert.Equal((code == ReviewerUnavailable.Cooling ? OpinionStepWaits.VacantUntil : OpinionStepWaits.Vacant, code), (step.Wait?.Code, step.Wait?.Values["why"]));
        Assert.Equal(code == ReviewerUnavailable.Cooling ? Then.AddHours(2) : null, step.Wait?.Until);
        Assert.Contains("no second opinion can be had", WorkflowRunWords.Say([run], null));
    }

    /// <summary>🔴 No state a gate holds an active opinion step in leaves it a bare <i>working</i>: every state of the gate that is not one
    /// the step ends on (or is passed over by) names a wait.</summary>
    [Fact]
    public void No_hold_of_the_gate_leaves_an_active_opinion_step_working()
    {
        var holds = typeof(OpinionGateStates).GetFields().Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!)
            .Except([OpinionGateStates.None, OpinionGateStates.Settled, OpinionGateStates.Anyway, OpinionGateStates.Myself, OpinionGateStates.Answered,
                OpinionGateStates.Unavailable, OpinionGateStates.CannotStart]);
        var unnamed = new List<string>();
        foreach (var state in holds)
        {
            var gate = new OpinionGateState(state, "engine")
            {
                Session = "s1", Tip = Tip, Later = "q2", Problem = "git could not say", Disputes = new OpinionDisputes([1], []), Since = 2,
                Opinion = Pass(state == OpinionGateStates.Reading ? "reading" : "given"),
            };
            if (Opinion(Derive(Opinion(NodeStates.Active, state), gate)).Wait is null) unnamed.Add(state);
        }

        Assert.Empty(unnamed);
    }

    /// <summary>🔴 A gate holding the work for the person (a dispute, commits nobody read) is their wait with its count, said in the
    /// run's view and the terminal's words, never a silent <i>working</i>.</summary>
    [Theory]
    [InlineData(OpinionGateStates.Disputed, OpinionStepWaits.Disputed, "Waits on your answer to 2 disputed findings of the second opinion")]
    [InlineData(OpinionGateStates.CommitsSince, OpinionStepWaits.CommitsSince, "Waits on your answer to 2 commits no other agent read")]
    public void An_opinion_whose_gate_holds_for_your_answer_says_so_and_never_working(string state, string code, string said)
    {
        var gate = new OpinionGateState(state, "engine")
        {
            Session = "s1", Tip = Tip, Disputes = new OpinionDisputes([1], [2]), Since = 2,
        };

        var run = Derive(Opinion(NodeStates.Active, state), gate);

        var step = run.Steps.Single(each => each.Step.Id == "opinion");
        Assert.Equal(WorkflowRunStates.Waiting, step.State);
        Assert.Equal((code, "2", "s1"), (step.Wait?.Code, step.Wait?.Values.GetValueOrDefault("count"), step.Wait?.Values.GetValueOrDefault("session")));
        Assert.Equal(StepHolders.Person, step.Holder?.Kind);
        Assert.Contains(said, WorkflowRunWords.Say([run], null));
        Assert.Contains(code, NodeKit.Of(NodeKinds.Opinion) is BuiltInNode built ? built.Waits : []);
    }

    private static RiskVerdict Verdict(string riskClass) =>
        new(RiskActs.WorkAccept, null, RiskVerdict.OutcomeOf(riskClass), riskClass, riskClass == RiskClasses.Role ? "supervisor" : null, "repository/engine", "r1", "classed");

    /// <summary>
    /// 🔴 A dispute the risk policy classes <c>role</c> waits on the step's role as the place casts it, never the person's: its holder is the
    /// role's agent, said with the policy's reason; one classed <c>owner</c>, or none judged, stays the person's. The terminal names the role
    /// the wait is held on, the step's own, never the row's <c>by</c>.
    /// </summary>
    [Theory]
    [InlineData(OpinionGateStates.Disputed, OpinionGateReading.RoleDisputed, OpinionStepWaits.Disputed)]
    [InlineData(OpinionGateStates.CommitsSince, OpinionGateReading.RoleCommitsSince, OpinionStepWaits.CommitsSince)]
    public void A_dispute_the_policy_gates_on_a_role_waits_on_that_role_and_not_the_person(string state, string holds, string code)
    {
        var gate = new OpinionGateState(state, "engine")
        {
            Session = "s1", Tip = Tip, Disputes = new OpinionDisputes([1], [2]), Since = 2, Risk = Verdict(RiskClasses.Role),
        };

        var run = Derive(Opinion(NodeStates.Active, holds), gate);

        var step = run.Steps.Single(each => each.Step.Id == "opinion");
        Assert.Equal((WorkflowRunStates.Waiting, code, StepHolders.Agent), (step.State, step.Wait?.Code, step.Holder?.Kind));
        Assert.Equal(StepRoles.Opinion, step.Role);
        Assert.Equal(StepRoles.Opinion, step.Wait!.Values[OpinionStepWaits.RoleKey]);
        Assert.Contains("The risk policy asks the `opinion` role, not you", WorkflowRunWords.Say([run], null));
        Assert.DoesNotContain("`supervisor`", WorkflowRunWords.Say([run], null));
        Assert.True(OpinionStepWaits.OnRole(step.Wait!));
        Assert.False(OpinionStepWaits.OnPerson(step.Wait!));
    }

    /// <summary>🔴 What the gate holds for the owner keeps its plain hold, and one the policy gates on a role says so in the step's hold.</summary>
    [Fact]
    public void An_opinion_holds_for_a_role_only_where_the_policy_gates_the_dispute_on_one()
    {
        var node = Node(NodeKinds.Opinion);
        string? HoldOf(RiskVerdict? verdict) =>
            Reads.Opinion(Branch(), node, Binding(NodeKinds.Opinion),
                new OpinionGateState(OpinionGateStates.Disputed, "engine") { Tip = Tip, Disputes = new OpinionDisputes([1], []), Risk = verdict }).Holds;

        Assert.Equal(OpinionGateReading.RoleDisputed, HoldOf(Verdict(RiskClasses.Role)));
        Assert.Equal(OpinionGateStates.Disputed, HoldOf(Verdict(RiskClasses.Owner)));
        Assert.Equal(OpinionGateStates.Disputed, HoldOf(null));
        Assert.Equal(PersonWaits.Accept, ((IWaitsOnRole)NodeKit.Of(NodeKinds.Opinion)!).Waits(node, Opinion(NodeStates.Active, OpinionGateReading.RoleDisputed)));
        Assert.Null(((IWaitsOnRole)NodeKit.Of(NodeKinds.Opinion)!).Waits(node, Opinion(NodeStates.Active, OpinionGateStates.Disputed)));
    }

    /// <summary>
    /// 🔴 A pass whose reviewer's session ended without giving an opinion, or whose minutes ran out first, fails the step with that said, its
    /// code kept; a gate with nobody free to read it is no failed pass and stands as before.
    /// </summary>
    [Theory]
    [InlineData("ended", "ended without giving an opinion")]
    [InlineData("out-of-time", "ran out before it gave an opinion")]
    public void A_pass_that_ended_without_an_opinion_fails_the_step_saying_so(string code, string said)
    {
        var node = Node(NodeKinds.Opinion);
        var gate = new OpinionGateState(OpinionGateStates.Unavailable, "engine") { Tip = Tip, Code = code };

        var moved = Applied(node, Reads.Opinion(Branch(), node, Binding(NodeKinds.Opinion), gate));

        Assert.Equal((NodeStates.Failed, code), (moved.Status.State, moved.Status.Code));
        Assert.True(((IObserves)NodeKit.Of(NodeKinds.Opinion)!).ObservesFailed(moved.Status));
        var run = Derive(Opinion(NodeStates.Failed, code: code), gate);
        Assert.Contains(said, WorkflowRunWords.Say([run], null));

        var nobody = Applied(node, Reads.Opinion(Branch(), node, Binding(NodeKinds.Opinion), gate with { Code = ReviewerUnavailable.Cooling }));
        Assert.Equal(NodeStates.Skipped, nobody.Status.State);
    }
}
