using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's Accept pressed while the run is still on its way to its review (D256): kept on the run's record as the review's Accept, left
/// there by every pass the review has not begun at, and applied at the first that finds it waiting at the commit pressed on; dropped where the
/// work moved to another commit, and giving way to the person's own later word.
/// </summary>
public sealed class EarlyAcceptsTests : NodeRunFixture
{
    private const string ReviewGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"sign","kind":"human","version":1,"reach":"pressed","outcomes":["approved"]},
                  {"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>s","kind":"after","from":"claimed","to":"sign"},
                  {"id":"s>r","kind":"when","from":"sign","to":"review","outcome":"approved"},
                  {"id":"r>h","kind":"after","from":"review","to":"handed"}]}
        """;

    private void Read()
    {
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
    }

    private async Task<NodeRuntime> OnTheWayAsync()
    {
        BoundTo(ReviewGraph);
        Read();
        var runtime = Runtime();
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("sign").State);
        Assert.Equal(NodeStates.Blocked, RunStatus("review").State);
        return runtime;
    }

    private async Task PassAsync(NodeRuntime runtime)
    {
        Read();
        await RunPassAsync(runtime, whole: false);
    }

    private void Approve() => Assert.True(RunNodeWords.Word(Files, RunId, "sign", NodeWordActs.Approve, null, () => _now).Done);

    [Fact]
    public async Task An_accept_pressed_before_the_review_begins_is_kept_and_applied_once_the_review_waits_at_its_commit()
    {
        var runtime = await OnTheWayAsync();

        Assert.True(EarlyAccepts.Keep(Files, RunId, Tip1, () => _now).Done);
        await PassAsync(runtime);
        var kept = Files.Read(RunUnit)!.Pending.Single();
        Assert.Equal(("review", NodeWordActs.Accept, true, Tip1), (kept.Node, kept.Act, kept.Kept, kept.Tip));
        Assert.Equal(NodeStates.Blocked, RunStatus("review").State);

        Approve();
        await PassAsync(runtime);
        await PassAsync(runtime);

        var review = RunStatus("review");
        Assert.Equal((NodeStates.Reached, NodeSource.Person), (review.State, review.By));
        Assert.Empty(Files.Read(RunUnit)!.Pending);
    }

    [Fact]
    public async Task A_kept_accept_is_dropped_where_the_review_begins_at_another_commit()
    {
        var runtime = await OnTheWayAsync();

        Assert.True(EarlyAccepts.Keep(Files, RunId, "0000000000000000000000000000000000000000", () => _now).Done);
        Approve();
        await PassAsync(runtime);
        await PassAsync(runtime);

        Assert.Equal(NodeStates.Active, RunStatus("review").State);
        Assert.Empty(Files.Read(RunUnit)!.Pending);
    }

    [Fact]
    public async Task The_persons_later_stop_takes_the_place_of_a_kept_accept()
    {
        await OnTheWayAsync();

        Assert.True(EarlyAccepts.Keep(Files, RunId, Tip1, () => _now).Done);
        Assert.True(RunNodeWords.Word(Files, RunId, "review", NodeWordActs.Stop, "Not this one.", () => _now).Done);

        var word = Files.Read(RunUnit)!.Pending.Single();
        Assert.Equal((NodeWordActs.Stop, false), (word.Act, word.Kept));
    }

    [Fact]
    public void A_kept_accept_is_answered_as_accepted_naming_the_step_it_waits_on()
    {
        var waits = new GraphGateState(GraphGateStates.Waits, "q7") { Handed = "land", Node = "content", Kind = NodeKinds.ContentHolds };

        Assert.StartsWith("Accepted; it lands once `content`", EarlyAccepts.Said(waits));
        Assert.StartsWith("Accepted; it lands at the next look", EarlyAccepts.Said(new GraphGateState(GraphGateStates.NotJudged, "q7")));
        Assert.DoesNotContain("has not judged", EarlyAccepts.Said(new GraphGateState(GraphGateStates.NotJudged, "q7")));
    }
}
