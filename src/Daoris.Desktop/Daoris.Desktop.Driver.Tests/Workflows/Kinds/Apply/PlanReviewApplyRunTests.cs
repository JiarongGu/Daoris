using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A system's run on the shipped <i>Plan, review and apply</i> graph, through the node runtime: its review binds the plan's record, the person
/// accepts it, and the apply writes the plan that record names though a newer plan came meanwhile, which waits for a review of its own.
/// </summary>
public sealed class PlanReviewApplyRunTests : NodeRunFixture
{
    private const string System = "acme.console/store";

    /// <summary>The system's adapter: each apply answered applied, the values each wrote kept in order.</summary>
    private sealed class Recorder : IHookChannel
    {
        public List<string> Wrote { get; } = [];

        public IReadOnlyList<string> Points => HookPoints.System;

        public bool Alive => true;

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => throw new NotSupportedException();

        public Task EndedAsync(object payload, CancellationToken ct) => throw new NotSupportedException();

        public Task<JsonElement> SystemAsync(string point, object payload, CancellationToken ct)
        {
            var operations = JsonSerializer.SerializeToElement(payload).GetProperty("operations").EnumerateArray().ToList();
            Wrote.AddRange(operations.Select(operation => operation.GetProperty("after").GetString()!));
            return Task.FromResult(JsonSerializer.SerializeToElement(new { state = "applied", applied = operations.Select(operation => operation.GetProperty("resource").GetString()).ToList() }));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>The door the runtime hands an apply to: it keeps the start, as an apply in flight, for the test to perform.</summary>
    private sealed class Held : IApplyDoor
    {
        public ApplyStart? Start { get; private set; }

        bool IApplyDoor.Start(ApplyStart start)
        {
            Start ??= start;
            return true;
        }
    }

    private readonly Held _held = new();

    private NodeRuntime Applying() => new(Files, _port, () => _now, () => true) { Doors = NodeDoors.None.With(NodeKinds.Apply, _held) };

    private void Plan(string value) =>
        new SystemPlans(_home).Keep("rec-1", System, [new SystemOperation("release", "update", "1", value, new SystemPrecondition("etag", "e1"))], _now);

    [Fact]
    public async Task The_apply_writes_the_plan_its_review_accepted_and_a_newer_plan_waits_for_its_own_review()
    {
        BoundTo(WorkflowDefaults.Of("plan-review-apply")!.GraphJson);
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")) with { Executors = ["rec-1"] }];
        Plan("2");
        var runtime = Applying();
        await RunPassAsync(runtime);
        var review = RunStatus("review");
        Assert.Equal((NodeStates.Active, 1), (review.State, ReviewRecords.Read(_home, RunId, "review", review.Attempt)?.Reference.Plan));

        Assert.True(RunNodeWords.Word(Files, RunId, "review", NodeWordActs.Accept, null, () => _now).Done);
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("apply").State);

        // A newer plan, written while the apply is in flight, is never what it writes.
        Plan("3");
        var recorder = new Recorder();
        await new ApplyDoor(_home, (_, _) => Task.FromResult<IHookChannel>(recorder), holdsLease: () => true).PerformAsync(_held.Start!, CancellationToken.None);

        Assert.Equal(["2"], recorder.Wrote);
        Assert.Equal((NodeStates.Reached, "plan 1 applied: 1 operation(s)"), (RunStatus("apply").State, RunStatus("apply").Words));

        await RunPassAsync(runtime);
        var again = RunStatus("review");
        Assert.Equal((NodeStates.Active, 2), (again.State, ReviewRecords.Read(_home, RunId, "review", again.Attempt)?.Reference.Plan));
    }
}
