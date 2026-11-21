using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A disputed second opinion, or commits nobody read, is answered by the risk policy: the gate asks the judge for
/// <c>work.accept</c> at the work's place. With nothing configured the seed passes it, answered by the policy with its rule on its words, and
/// the work goes on; classed <c>role</c> it waits on the role, never the owner's; classed <c>owner</c> it waits on the owner, as it did. A
/// gate whose place is not read waits as before. 
/// </summary>
public sealed class OpinionDisputeRiskTests
{
    private const string Read = "1111111111111111111111111111111111111111";
    private const string Later = "3333333333333333333333333333333333333333";
    private static readonly DateTimeOffset Asked = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Answered = new(2026, 11, 20, 9, 40, 0, TimeSpan.Zero);
    private static readonly RiskPlace Place = new("work", "web-app", Full: false);

    private static Func<RiskAct, RiskVerdict> Judge(string policy) =>
        act => RiskGate.Judge(RiskPolicy.Read(JsonDocument.Parse(policy).RootElement), act, "", Place, "");

    private static readonly Func<RiskAct, RiskVerdict> Nothing = Judge("{}");

    private static Func<RiskAct, RiskVerdict> Classed(string riskClass, string? by = null) =>
        Judge("{\"risk\":{\"repository/web-app\":[{\"kind\":\"" + RiskActs.WorkAccept + "\",\"class\":\"" + riskClass + "\""
            + (by is null ? "" : ",\"by\":\"" + by + "\"") + "}]}}");

    private static ResolvedOpinion Rule() => new(new OpinionRule(["codex-acp"], [OpinionRules.Landing], false, Recheck: false), OpinionSource.Repository);

    [Fact]
    public void An_opinion_step_its_gate_reached_after_the_policy_passed_its_dispute_counts_as_a_policy_pass_so_a_wait_the_person_held_is_told()
    {
        var opinion = new NodeDeclaration("opinion", NodeKinds.Opinion, 1, NodeReach.Observed);
        var review = new NodeDeclaration("review", NodeKinds.Review, 1, NodeReach.Pressed);
        var reached = new NodeStatus("run:q7", "opinion", NodeStates.Reached) { By = NodeSource.Opinion, Attempt = 1 };

        Assert.Equal(RiskActs.WorkAccept, PolicyPasses.PassedKind(opinion, reached, gatePassed: true));
        Assert.Null(PolicyPasses.PassedKind(opinion, reached, gatePassed: false));
        Assert.Null(PolicyPasses.PassedKind(opinion, reached with { State = NodeStates.Active }, gatePassed: true));
        Assert.Equal(RiskActs.WorkAccept, PolicyPasses.PassedKind(review, new NodeStatus("run:q7", "review", NodeStates.Reached) { By = NodeSource.Policy }, false));
        Assert.Null(PolicyPasses.PassedKind(review, new NodeStatus("run:q7", "review", NodeStates.Reached) { By = NodeSource.Person }, false));
    }

    private static OpinionView Opinion(params string[] weights) =>
        new("o1", "landing", "first", "s1", "r-o1", "web-app", "0000000000000000000000000000000000000000", Read, "codex-acp", ReviewerLabels.AnotherMaker, "given")
        {
            Product = "Codex", Maker = "OpenAI", Minutes = 20,
            Findings = [.. weights.Select((weight, at) => new OpinionFindingView(at + 1, weight, "src/a.ts:1", "claim", "", "", "likely"))],
            Read = "src/a.ts",
        };

    private static OpinionReads Reads(string tip, OpinionView opinion, OpinionDelivered delivered, Dictionary<string, string[]> holds) => new(
        id => Task.FromResult<OpinionView?>(id == opinion.Id ? opinion : null),
        id => id == opinion.Id ? delivered : OpinionDelivered.None,
        commit => Task.FromResult<bool?>(holds.TryGetValue(commit, out var held) && held.Contains(tip)),
        from => Task.FromResult<int?>(holds[tip].Length - holds[from].Length));

    private static readonly Dictionary<string, string[]> Holds = new() { [Read] = [Read], [Later] = [Read, Later] };

    private static OpinionGateFacts Facts(Func<RiskAct, RiskVerdict>? risk, string tip = Read) => new("web-app", Rule())
    {
        Session = "s1", Quest = new QuestView("q1", "ask #a1", "web-app", "Work q1", "", "Done"), Tip = tip, Asks = [new OpinionAskKept(Asked, "landing", "s1", Read) { Opinion = "o1" }],
        Risk = risk,
    };

    /// <summary>A <c>must</c> the working session did not fix: the dispute.</summary>
    private static Task<OpinionGateState> DisputedAsync(Func<RiskAct, RiskVerdict>? risk) => OpinionGate.JudgeAsync(Facts(risk),
        Reads(Read, Opinion(OpinionViews.Must), new OpinionDelivered(null, null, new OpinionReading("s1", Read, Answered,
            [new OpinionAnswerRead(1, OpinionViews.Must, OpinionViews.Rejected) { Said = OpinionViews.Rejected }])), Holds));

    /// <summary>The same pass read at a later tip: commits nobody read, no dispute.</summary>
    private static Task<OpinionGateState> CommitsSinceAsync(Func<RiskAct, RiskVerdict>? risk) => OpinionGate.JudgeAsync(Facts(risk, Later),
        Reads(Later, Opinion(), new OpinionDelivered(null, null, null), Holds));

    [Fact]
    public async Task A_fixture_dispute_passes_with_nothing_configured_answered_by_the_policy_with_its_line()
    {
        var gate = await DisputedAsync(Nothing);

        Assert.Equal((OpinionGateStates.Answered, true, OpinionGateStates.Disputed), (gate.State, gate.LetsGo, gate.Answered));
        Assert.Equal((OpinionPersonSaid.Policy, Read), (gate.Person!.Said, gate.Person.Tip));
        Assert.Contains(RiskVerdict.Passes, gate.Person.Words);
        Assert.Contains("rule seed", gate.Person.Words);
        Assert.StartsWith("The risk policy answered the second opinion over 1 disputed finding", gate.Says);
        Assert.Equal((OpinionGateStates.Answered, OpinionPersonSaid.Policy, 1), (gate.Landing!.Said, gate.Landing.Person, gate.Landing.Disputes));
    }

    [Fact]
    public async Task Commits_nobody_read_pass_the_same_way()
    {
        var gate = await CommitsSinceAsync(Nothing);

        Assert.Equal((OpinionGateStates.Answered, true, OpinionGateStates.CommitsSince), (gate.State, gate.LetsGo, gate.Answered));
        Assert.StartsWith("The risk policy answered the second opinion with 1 commit no other agent read", gate.Says);
    }

    [Fact]
    public async Task Classed_role_the_dispute_waits_on_the_role_and_is_never_the_owners()
    {
        var gate = await DisputedAsync(Classed("role", "reviewer"));

        Assert.Equal((OpinionGateStates.Disputed, false, true), (gate.State, gate.LetsGo, gate.PressAnswers));
        Assert.Equal((RiskVerdict.Gate, "reviewer"), (gate.Risk!.Outcome, gate.Risk.By));
        Assert.False(gate.OwnerHolds);
        // The gate names no role of its own: the opinion step's own role holds it (OpinionStepWaits), never the row's `by`.
        Assert.Contains("asks the role holding the second opinion's step, not you, for the answer", gate.Says);
        Assert.DoesNotContain("`reviewer`", gate.Says);
    }

    [Fact]
    public async Task Classed_owner_the_dispute_waits_on_the_owner_as_it_did()
    {
        var gate = await DisputedAsync(Classed("owner"));

        Assert.Equal((OpinionGateStates.Disputed, false), (gate.State, gate.LetsGo));
        Assert.Equal(RiskVerdict.OwnerOutcome, gate.Risk!.Outcome);
        Assert.True(gate.OwnerHolds);
    }

    [Fact]
    public async Task A_gate_judged_with_no_policy_read_waits_for_the_person_as_it_did()
    {
        var gate = await DisputedAsync(null);

        Assert.Equal((OpinionGateStates.Disputed, false, null), (gate.State, gate.LetsGo, gate.Risk));
        Assert.True(gate.OwnerHolds);
    }

    [Fact]
    public async Task The_persons_own_word_at_the_tip_stands_before_the_policy_is_asked()
    {
        var word = new OpinionPersonWord(OpinionPersonSaid.Anyway, Read, Answered) { Words = "a typo" };
        var gate = await OpinionGate.JudgeAsync(Facts(Classed("owner")) with { Person = [word] },
            Reads(Read, Opinion(OpinionViews.Must), new OpinionDelivered(null, null, new OpinionReading("s1", Read, Answered,
                [new OpinionAnswerRead(1, OpinionViews.Must, OpinionViews.Rejected) { Said = OpinionViews.Rejected }])), Holds));

        Assert.Equal((OpinionGateStates.Anyway, OpinionPersonSaid.Anyway), (gate.State, gate.Person!.Said));
    }

    [Fact]
    public async Task A_pass_by_the_policy_is_journaled_once_with_its_rule_and_revision()
    {
        var home = Path.Combine(TestHomes.Root, "daoris-dispute-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var gate = await DisputedAsync(Nothing);
            var at = new PolicyPassAt("q1", "work", "web-app", "q1", "opinion");

            PolicyPasses.Gate(home, DriverConfig.Empty, at, gate, Answered);
            PolicyPasses.Gate(home, DriverConfig.Empty, at, gate, Answered.AddHours(1));
            PolicyPasses.Gate(home, DriverConfig.Empty, at, await DisputedAsync(Classed("owner")), Answered);

            var line = Assert.Single(RiskJournal.Read(home));
            Assert.Equal((RiskActs.WorkAccept, RiskVerdict.Pass, RiskVerdict.Seed, PolicyPasses.Today), (line.Kind, line.Outcome, line.Source, line.Today));
            Assert.Equal((RiskJudged.Released, "web-app", false), (line.Stage, line.Repository, line.TodayHeld));
            Assert.Equal(("q1", "opinion"), (line.Run, line.Step));
            Assert.Contains(line, RiskWhy.Rows(home, "q1", "opinion"));
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, true);
        }
    }

    private const string OpinionGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"second","kind":"opinion","version":1,"reach":"observed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
         "wires":[{"id":"c>o","kind":"after","from":"claimed","to":"second"},{"id":"o>h","kind":"after","from":"second","to":"handed"}]}
        """;

    /// <summary>A pass line names the run the gate is read for and its opinion step: a chat run's key, else the process's bound run and step.</summary>
    [Fact]
    public void A_pass_line_is_placed_on_the_run_and_the_opinion_step_the_process_binds()
    {
        var (graph, _) = NodeGraphs.Read(OpinionGraph);
        var process = WorkflowProcess.Declared(DriverConfig.Empty, "web-app", "work") with
        {
            Graph = graph, Binding = new WorkflowRunBinding("q1", "web-app", "work", "docs", WorkflowLevels.Repository, Answered),
        };

        Assert.Equal(new PolicyPassAt("q1", "work", "web-app", "q1", "second"), PolicyPassAt.Of("q1", "work", "web-app", null, process));
        Assert.Equal(new PolicyPassAt("chat-a", "work", "web-app", "chat-a", "second"), PolicyPassAt.Of("chat-a", "work", "web-app", "chat-a", process));
        Assert.Equal(new PolicyPassAt("q1", "work", "web-app", null, null), PolicyPassAt.Of("q1", "work", "web-app", null, null));
    }

    /// <summary>A pass whose line would not append is not counted as journaled: the next read of the gate writes it.</summary>
    [Fact]
    public async Task A_pass_whose_line_did_not_append_is_journaled_at_the_next_read()
    {
        var home = Path.Combine(TestHomes.Root, "daoris-dispute-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var gate = await DisputedAsync(Nothing);
            var at = new PolicyPassAt("q1", "work", "web-app", "q1", "opinion");
            // A folder where the journal's file goes: the append fails.
            Directory.CreateDirectory(RiskJournal.PathOf(home));

            Assert.ThrowsAny<Exception>(() => PolicyPasses.Gate(home, DriverConfig.Empty, at, gate, Answered));
            Directory.Delete(RiskJournal.PathOf(home));
            PolicyPasses.Gate(home, DriverConfig.Empty, at, gate, Answered.AddMinutes(1));

            Assert.Single(RiskJournal.Read(home));
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, true);
        }
    }

    [Fact]
    public async Task A_policy_lowered_after_the_gate_opened_releases_it_and_a_raise_opens_it_again()
    {
        Assert.Equal(OpinionGateStates.Disputed, (await DisputedAsync(Classed("owner"))).State);
        Assert.Equal(OpinionGateStates.Answered, (await DisputedAsync(Classed("none"))).State);
        Assert.Equal(OpinionGateStates.Disputed, (await DisputedAsync(Classed("role"))).State);
    }
}
