using Daoris.Contracts.Asks;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// METHODS1 c wired into a team plan's run: under <c>one-scope</c> each part's request onto the ask's branch is frozen with no gates,
/// and the ask's delivery check asks the full set once with the two reviews; under <c>per-part</c> nothing changes.
/// </summary>
public sealed class ScopeRunTests : IDisposable
{
    private const string Tip = "3333333333333333333333333333333333333333";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-scoperun-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private sealed class Plans(string? integration) : ITrainPlans
    {
        public int Reads { get; private set; }

        public Task<TrainPlan?> OfAsync(string ask, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult<TrainPlan?>(new TrainPlan(ask, 2, [
                new TrainPlanPart("api", new HashSet<string>(), new HashSet<string>()) { Quest = "q1", Repository = "engine" },
                new TrainPlanPart("web", new HashSet<string>(), new HashSet<string>()) { Quest = "q2", Repository = "engine" },
            ]) { Integration = integration });
        }
    }

    private static LandedBranch Landed(string branch) =>
        new("engine", "default", branch, "main", Tip, "s1", null, null, DateTimeOffset.UnixEpoch)
        {
            AcceptedBy = AcceptedBy.Auto, Integrate = new LandedIntegrate(LandingIntegrate.Done, LandedIntegrate.Version),
        };

    private static IntegrationAsk Ask(string branch, TrainRef? train) =>
        new IntegrationAsk("engine", "default", "root", branch, IntegrationRoutes.Node, IntegrationRequester.NodeStart) { Unit = "u", Tip = Tip, Train = train };

    private static readonly LandingIntegrate Gated = new(Request: LandingIntegrate.Done, Gates: LandingIntegrate.Declared);

    [Fact]
    public async Task A_two_part_one_scope_plan_merges_both_with_no_gates_and_asks_the_full_set_and_two_reviews_once()
    {
        var plans = new Plans(ScopeLanding.OneScope);
        var api = await TrainPartOf.ReadAsync("q1", "a1/api/1", plans, default);
        var web = await TrainPartOf.ReadAsync("q2", "a1/web/1", plans, default);
        Assert.True(api!.OneScope);
        Assert.True(web!.OneScope);

        var frozen = new[] { api, web }.Select(part => IntegrationRequests.NodeJudgement(Ask($"work/{part.Part}", part), Landed($"work/{part.Part}"), Gated).Policy!).ToList();
        Assert.All(frozen, policy => Assert.Equal(LandingIntegrate.NoGates, policy.Gates));
        Assert.All(frozen, policy => Assert.Equal(TrainBranch.Of("a1"), policy.Line));

        Directory.CreateDirectory(_home);
        GitFolders.Remember(_home, [("checkout-engine", "engine", "default")]);
        var run = AskRunKey.Of("a1", "engine")!;
        Assert.True(WorkflowRunBindings.Bind(_home, new WorkflowRunBinding(run, "engine", "default", "ask-delivery", WorkflowLevels.Task, DateTimeOffset.UnixEpoch) { Ask = "a1" }));
        var send = new VerifySend(run, "inv-1", "engine", "checkout-engine", Tip, "blob") { Reached = true };
        var reads = plans.Reads;

        var end = await ScopeLanding.EndAsync(_home, send, plans, [new ScopeReviewer("claude", "anthropic", true), new ScopeReviewer("codex", "openai", true)], default);

        Assert.Equal(reads + 1, plans.Reads);
        Assert.True(end.Send.Full);
        Assert.False(end.Send.Reached);
        Assert.Equal([ScopeLanding.Wide, ScopeLanding.Close], end.Reviews.Select(review => review.Breadth));
        Assert.Equal(["claude", "codex"], end.Reviews.Select(review => review.Adapter));
    }

    [Fact]
    public async Task A_per_part_plan_gates_each_part_and_its_end_check_is_its_own()
    {
        var plans = new Plans(ScopeLanding.PerPart);
        var api = await TrainPartOf.ReadAsync("q1", "a1/api/1", plans, default);
        Assert.False(api!.OneScope);
        Assert.Equal(LandingIntegrate.Declared, IntegrationRequests.NodeJudgement(Ask("work/api", api), Landed("work/api"), Gated).Policy!.Gates);

        Directory.CreateDirectory(_home);
        var run = AskRunKey.Of("a1", "engine")!;
        Assert.True(WorkflowRunBindings.Bind(_home, new WorkflowRunBinding(run, "engine", "default", "ask-delivery", WorkflowLevels.Task, DateTimeOffset.UnixEpoch) { Ask = "a1" }));
        var send = new VerifySend(run, "inv-1", "engine", "checkout-engine", Tip, "blob") { Reached = true };

        var end = await ScopeLanding.EndAsync(_home, send, plans, [new ScopeReviewer("claude", "anthropic", true)], default);

        Assert.Same(send, end.Send);
        Assert.Empty(end.Reviews);
        var chain = send with { Run = "q7" };
        Assert.Same(chain, (await ScopeLanding.EndAsync(_home, chain, new Plans(ScopeLanding.OneScope), [], default)).Send);
    }

    [Fact]
    public void An_older_plan_answer_with_no_integration_reads_as_per_part()
    {
        var answer = new AskPlanResponse("a1", [new AskPlanRevisionResponse(null, 1, 0, "", [], [], [], [], [], [], [])], [], []);
        Assert.Null(TrainPlan.Of(answer)!.Integration);
        Assert.Equal(ScopeLanding.OneScope, TrainPlan.Of(answer with { Plans = [answer.Plans[0] with { Integration = ScopeLanding.OneScope }] })!.Integration);
    }
}
