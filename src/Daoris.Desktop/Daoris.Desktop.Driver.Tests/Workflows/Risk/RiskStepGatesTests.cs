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

    private PlaceCastFiles Casts => new(_home, () => true);

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
