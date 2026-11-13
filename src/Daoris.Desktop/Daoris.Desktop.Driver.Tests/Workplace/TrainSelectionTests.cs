using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The train over the line's one queue, on a scratch home's store and no git: a part's request is offered to the next candidate only once
/// every part it builds on has landed, in the plan's landing order; one the plan does not name, or whose plan is not read, waits; a request
/// no part's is the queue's as it was. A part is landed by its member's promotion alone, never by its quest's Done.
/// </summary>
public sealed class TrainSelectionTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "train-selection", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private IntegrationStore Store => _store ??= new IntegrationStore(_home, () => StoreSettled.Yes);

    private IntegrationStore? _store;

    private static readonly DateTimeOffset At = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private sealed class Plans(TrainPlan? plan) : ITrainPlans
    {
        public Task<TrainPlan?> OfAsync(string ask, CancellationToken ct) => Task.FromResult(plan);
    }

    private static TrainPlanPart Part(string id, string[]? after = null, string[]? orders = null) =>
        new(id, new HashSet<string>(after ?? []), new HashSet<string>(orders ?? []));

    private static TrainPlan Plan(params TrainPlanPart[] parts) => new("a1", 1, parts);

    /// <summary>A part's request on the ask's branch, tagged, arriving <paramref name="second"/> seconds in.</summary>
    private IntegrationRequest Queue(string id, string? part, int second)
    {
        var request = new IntegrationRequest(
            id, "engine", "aurora", $"feature/{id}", $"{second:D40}", IntegrationRoutes.Node, TrainBranch.Of("a1"), IntegrationRequester.NodeStart,
            new LandingIntegrate(Batch: 8).Resolved(), At.AddSeconds(second));
        Assert.Null(Store.Add(request));
        if (part is not null) new TrainParts(Store).Tag(id, "a1", part);
        return request;
    }

    private async Task<string[]> ReadyAsync(TrainPlan? plan) =>
        [.. (await new TrainSelection(new TrainParts(Store), new Plans(plan)).ReadyAsync(Store.Queued("engine", TrainBranch.Of("a1")), default)).Select(each => each.Id)];

    /// <summary>The part's request promoted onto the ask's branch, as a green batch leaves its member.</summary>
    private void Promote(string request)
    {
        var formed = Store.Form(new IntegrationForming("engine", "aurora", TrainBranch.Of("a1"), new string('0', 40), [request]));
        using var writer = Store.Writer();
        writer.Write(IntegrationStore.Family, write => write.Execute(
            "UPDATE integration_members SET state = $state WHERE request = $request AND candidate = $candidate",
            ("$state", IntegrationMemberStates.Promoted), ("$request", request), ("$candidate", formed.Candidate.Id)));
    }

    [Fact]
    public async Task Ready_parts_are_offered_in_the_plans_landing_order_not_by_arrival()
    {
        Queue("r3", "p3", 1);
        Queue("r1", "p1", 2);
        Queue("r2", "p2", 3);

        // p1 lands before p2, and p2 before p3, though they arrived the other way round.
        var ready = await ReadyAsync(Plan(Part("p1"), Part("p2", orders: ["p1"]), Part("p3", orders: ["p2"])));

        Assert.Equal(["r1", "r2", "r3"], ready);
    }

    [Fact]
    public async Task A_part_waits_for_every_part_it_builds_on_to_land_and_is_offered_once_they_have()
    {
        Queue("r1", "p1", 1);
        Queue("r2", "p2", 2);
        var plan = Plan(Part("p1"), Part("p2", after: ["p1"]));

        Assert.Equal(["r1"], await ReadyAsync(plan));

        Promote("r1");
        Assert.Equal(["r2"], await ReadyAsync(plan));
    }

    [Fact]
    public async Task A_part_the_plan_does_not_name_and_a_plan_that_is_not_read_wait_and_a_request_that_is_no_parts_stays_in_the_queue()
    {
        Queue("loose", null, 1);
        Queue("r1", "p1", 2);
        Queue("gone", "dropped", 3);

        Assert.Equal(["loose", "r1"], await ReadyAsync(Plan(Part("p1"))));
        Assert.Equal(["loose"], await ReadyAsync(null));
    }

    [Fact]
    public void A_part_is_landed_by_its_promoted_member_alone()
    {
        Queue("r1", "p1", 1);
        Queue("r2", "p2", 2);
        var parts = new TrainParts(Store);

        Assert.Empty(parts.Landed("a1"));

        Promote("r2");
        Assert.Equal(["p2"], parts.Landed("a1"));
        Assert.Equal(["a1"], parts.Asks());
    }

    [Fact]
    public void The_parts_node_is_reached_once_every_required_part_landed_and_a_done_quest_is_not_a_landing()
    {
        var node = new NodeDeclaration("parts", NodeKinds.Parts, 1, NodeReach.Observed);
        var self = new WorkUnit("run:r", WorkUnitKind.Run, new WorkOwner("engine", "default", null, WorkRecord.DriverWriter),
            new WorkSubject("r", null), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "running"), []);
        var binding = new NodeAcceptance("run:r", null, "graph-1", "parts", 1, 1);
        NodeObservation Read(PartsReport? report) => PartsReading.Observe(new NodeReading(self, node, binding) { Parts = report });
        string[] required = ["p1", "p2", "p3"];

        Assert.Null(Read(null).Outcome);

        var waiting = Read(new PartsReport("a1", 2, required, new HashSet<string> { "p1", "p2" }));
        Assert.Null(waiting.Outcome);
        Assert.Contains("p3", waiting.Said);

        var reached = Read(new PartsReport("a1", 2, required, new HashSet<string> { "p1", "p2", "p3" }));
        Assert.Equal(NodeStates.Reached, reached.Outcome);
        Assert.Equal(NodeSource.Integration, reached.Source);
        Assert.Equal("integration#a1@2", reached.Acceptance!.Evidence!.Ref);

        // A landed part a revision dropped is no longer required; a plan naming no part reaches nothing.
        Assert.Equal(NodeStates.Reached, Read(new PartsReport("a1", 3, ["p1"], new HashSet<string> { "p1", "p2" })).Outcome);
        Assert.Null(Read(new PartsReport("a1", 3, [], new HashSet<string>())).Outcome);
    }
}
