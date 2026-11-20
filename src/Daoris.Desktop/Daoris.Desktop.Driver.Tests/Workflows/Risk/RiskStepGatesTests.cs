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

    private void OnBranch(string graph)
    {
        BoundTo(graph);
        _branchTips["daoris/s1"] = Tip1;
        var done = RunHome.Quest(RunId, "Done") with { Submission = new QuestSubmissionView("m1", 1, _now, "answer 1") };
        _units = [RunHome.QuestUnit(done) with { Executors = ["s-worker"] }, RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
    }

    private NodeWorded Settle(string node, string session, string? outcome = null) =>
        StepSettles.Settle(Files, new StepSettleAsk(RunId, node, outcome, "Read it: it does what was asked.", new Dictionary<string, string>()),
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
    public async Task Classed_owner_the_review_waits_on_the_person()
    {
        OnBranch(Reviewed);
        await RunPassAsync(Runtime(), """{"risk":{"machine":[{"kind":"work.accept","class":"owner"}]}}""");

        Assert.Equal(NodeStates.Active, RunStatus("review").State);
        var held = new StepTenureFiles(_home, () => true).Current(RunUnit, "review", RunStatus("review").Attempt);
        Assert.Equal((CastHolders.Person, TenureWhy.Marked), (held?.Holder, held?.Why));
    }
}
