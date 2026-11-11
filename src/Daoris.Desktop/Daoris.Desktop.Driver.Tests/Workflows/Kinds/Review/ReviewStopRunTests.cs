using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The review's Stop through the node runtime: before its Accept it stops the run, every live step; after it, only what follows, the review's
/// Accept and what already happened left as they are; and each says what stays for the kind of work the review read.
/// </summary>
public sealed class ReviewStopRunTests : NodeRunFixture
{
    private const string Graph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"review"},
                  {"id":"r>h","kind":"after","from":"review","to":"handed"}]}
        """;

    private void OnBranch()
    {
        BoundTo(Graph);
        _branchTips["daoris/s1"] = Tip1;
        var done = RunHome.Quest(RunId, "Done") with { From = "ask:a1", Submission = new QuestSubmissionView("m1", 1, _now, "answer 1") };
        _units = [RunHome.QuestUnit(done), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
    }

    private Task<ReviewStopped> StopAsync() =>
        ReviewStops.StopAsync(Files, NodeRuntime.HomeDoor(_home), RunId, "review", "Not this one.", () => _now);

    [Fact]
    public async Task Stop_before_the_accept_stops_every_live_step_of_the_run_and_says_the_branch_stays()
    {
        OnBranch();
        await RunPassAsync(Runtime());
        Assert.Equal(NodeStates.Active, RunStatus("review").State);

        var stopped = await StopAsync();
        await RunPassAsync(Runtime());

        Assert.True(stopped is { Done: true, Following: false });
        Assert.Equal(["review", "handed"], stopped.Stopped);
        Assert.Equal(ReviewStops.Stays(ReviewKinds.Branch, following: false), stopped.Stays);
        Assert.Equal((NodeStates.Stopped, NodeStates.Stopped), (RunStatus("review").State, RunStatus("handed").State));
        Assert.Contains("Discard tree", ReviewStops.Said(stopped), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stop_after_the_accept_stops_only_what_follows_and_leaves_the_accept_standing()
    {
        OnBranch();
        await RunPassAsync(Runtime());
        Assert.True(RunNodeWords.Word(Files, RunId, "review", NodeWordActs.Accept, null, () => _now).Done);
        await RunPassAsync(Runtime());
        Assert.Equal(NodeStates.Reached, RunStatus("review").State);

        var stopped = await StopAsync();
        await RunPassAsync(Runtime());

        Assert.True(stopped is { Done: true, Following: true });
        Assert.Equal(["handed"], stopped.Stopped);
        Assert.Equal((NodeStates.Reached, NodeStates.Stopped), (RunStatus("review").State, RunStatus("handed").State));
        Assert.Contains("nothing is undone", ReviewStops.Said(stopped), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ReviewKinds.Checkout, "commits stay")]
    [InlineData(ReviewKinds.Folder, "changes stay in the folder")]
    [InlineData(ReviewKinds.Copy, "Nothing more comes back")]
    [InlineData(ReviewKinds.Plan, "Nothing more is applied")]
    [InlineData(ReviewKinds.Answer, "answer stays")]
    public void Each_kind_says_what_stays_when_its_run_is_stopped(string kind, string stays) =>
        Assert.Contains(stays, ReviewStops.Stays(kind, following: false), StringComparison.Ordinal);

    [Fact]
    public async Task A_stop_on_a_node_that_is_not_a_review_is_refused_and_stops_nothing()
    {
        OnBranch();
        await RunPassAsync(Runtime());

        var refused = await ReviewStops.StopAsync(Files, NodeRuntime.HomeDoor(_home), RunId, "handed", null, () => _now);

        Assert.Equal(RunNodeWords.NotReview, refused.Refused);
        Assert.Empty(Files.Read(RunUnit)!.Pending);
    }
}
