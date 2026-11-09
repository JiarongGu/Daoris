using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The <c>rebase</c>'s door and reading over a home's records: its start's wait read from the operations and the trees a session turns in;
/// what an attempt came to reached through the one table with its <c>commit</c> or its <c>problem</c>; and the records let go of at the run's end.
/// </summary>
public sealed class RebaseDoorTests : TestHome
{
    private static readonly DateTimeOffset At = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

    private string TreeOf(string name)
    {
        var tree = DaorisHome.Entry(Home, HomeEntry.Trees, "default", "engine", name);
        Directory.CreateDirectory(tree);
        return tree;
    }

    private RebaseWork Work(string branch) => new(Path.Combine(Home, "engine"), "engine", "default", branch, "t1");

    [Fact]
    public async Task Its_start_waits_said_on_an_open_operation_and_a_turn_and_goes_on_once_still()
    {
        var member = TreeOf("member");
        var head = TreeOf("head");
        IReadOnlySet<string> turning = new HashSet<string>();
        var door = new RebaseDoor(Home) { Turning = _ => Task.FromResult(turning) };
        Assert.Null(await door.WaitsAsync(Work("daoris/member"), CancellationToken.None));

        var (opened, refused) = new TogetherOperations(Home).Open(
            "engine", new TogetherHead("main", "s-40", head, "daoris/head", "h0"), [new TogetherMember("s-41", member, "daoris/member", "b0", "t1")], At);
        Assert.Null(refused);

        Assert.Equal("Waits for `s-40`'s Accept, which brings this work in with it.", await door.WaitsAsync(Work("daoris/member"), CancellationToken.None));
        Assert.Equal("Waits for `s-41` to be brought in: asked.", await door.WaitsAsync(Work("daoris/head") with { Tip = "h0" }, CancellationToken.None));

        new TogetherOperations(Home).LetGo(opened!.Id, At);
        turning = new HashSet<string>([member]);
        Assert.Equal("Waits for its session's turn to end.", await door.WaitsAsync(Work("daoris/member"), CancellationToken.None));
    }

    [Theory]
    [InlineData(RebaseOutcomes.Ready)]
    [InlineData(RebaseOutcomes.Conflicted)]
    public void An_attempt_is_reached_through_the_one_table_with_what_it_carries(string outcome)
    {
        var unit = ObservedFixture.Unit(ObservedFixture.Grown());
        var node = ObservedFixture.Node(NodeKinds.Rebase) with { Outcomes = RebaseOutcomes.All };
        var made = new RebaseAttempt(unit.Id, node.Id, 1, "t1", "m0", outcome, "said")
        {
            Commit = outcome == RebaseOutcomes.Ready ? "c1" : null,
            StopSort = outcome == RebaseOutcomes.Ready ? null : ProblemSorts.Conflict, PatchId = "p1", Paths = ["src/a.cs"], StopCommit = "s1",
        };
        var git = new NodeGitReads { Rebased = (run, at, attempt) => run == unit.Id && at == node.Id && attempt == 1 ? made : null };

        var observation = NodeObserving.Observe(unit, node, ObservedFixture.Binding(unit, NodeKinds.Rebase), git);
        var moved = NodeLifecycle.Apply(node, ObservedFixture.Observing(unit, NodeKinds.Rebase), observation.Move!);

        Assert.Null(moved.Refused);
        var reached = moved.Status.Current!;
        Assert.Equal(outcome, reached.Outcome);
        var port = Assert.Single(reached.Outputs).Value;
        Assert.Equal(new NodeProducer(node.Id, 1), port.Producer);
        Assert.Equal(StepRefs.Of(unit.Id, node.Id, 1), port.Ref);
        if (outcome == RebaseOutcomes.Ready)
        {
            Assert.Equal((WireValues.Commit, "c1", "m0"), (port.Schema, port.Revision, port.Onto));
        }
        else
        {
            Assert.Equal((WireValues.Problem, "s1"), (port.Schema, port.Revision));
            Assert.Equal(new NodeStop(ProblemSorts.Conflict, "p1", ["src/a.cs"]), port.Stop);
        }
    }

    [Fact]
    public void A_run_s_attempts_are_let_go_of_at_its_settled_end()
    {
        var records = new RebaseRecords(Home);
        records.Write(new RebaseAttempt("q1", "rebase", 1, "t1", "m0", RebaseOutcomes.Ready, "said") { Commit = "c1" });
        Assert.NotNull(records.Of("q1", "rebase", 1));

        NodeStepReads.Release(Home, "q1");

        Assert.Empty(records.Of("q1"));
    }

    [Fact]
    public void A_run_that_never_rebased_is_let_go_of_in_a_home_with_no_rebase_records()
    {
        NodeStepReads.Release(Home, "q1");

        Assert.Empty(new RebaseRecords(Home).Of("q1"));
    }
}
