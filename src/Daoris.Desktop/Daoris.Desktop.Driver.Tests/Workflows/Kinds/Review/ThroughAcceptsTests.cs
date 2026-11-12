using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's through words on a run's ask: the runtime passes an unchecked done and keeps the person's Accept on them, step by
/// step, only where no requirement departed or waits unmet, nothing else waits on the person and nothing is stopped; else the run waits.
/// </summary>
public sealed class ThroughAcceptsTests
{
    private const string Unit = "run:q1";

    private static readonly NodeGraph Graph = new(
        [Node("claimed", NodeKinds.Claimed), Node("holds", NodeKinds.QuestHolds), Node("look", NodeKinds.Look), Node("review", NodeKinds.Review),
         Node("land", NodeKinds.Land)],
        [], ["claimed"], "land");

    private static NodeDeclaration Node(string id, string kind) => new(id, kind, 1, NodeReach.Performed);

    private static NodeRecord Record(params (string Node, string State, string? Holds)[] statuses) => new(Unit)
    {
        Statuses = statuses.ToDictionary(each => each.Node, each => new NodeStatus(Unit, each.Node, each.State) { Attempt = 1, Holds = each.Holds }),
    };

    private static QuestView Quest(params QuestRequirementView[] requirements) =>
        new("q1", "ask #a1", "reports", "ship it", "", "Done") { Requirements = requirements };

    private static readonly string Unchecked = UncheckedDones.Of(Quest())!;

    [Fact]
    public void An_unchecked_done_is_passed_first_and_the_review_accepted_once_it_waits()
    {
        var waiting = Record(("claimed", NodeStates.Reached, null), ("holds", NodeStates.Active, Unchecked), ("look", NodeStates.Skipped, null),
            ("review", NodeStates.Blocked, null));
        Assert.Equal(("holds", NodeWordActs.Skip), ThroughAccepts.Due(Graph, waiting, [Quest()]));

        var review = Record(("claimed", NodeStates.Reached, null), ("holds", NodeStates.Skipped, null), ("look", NodeStates.Skipped, null),
            ("review", NodeStates.Active, null));
        Assert.Equal(("review", NodeWordActs.Accept), ThroughAccepts.Due(Graph, review, [Quest()]));
    }

    [Fact]
    public void A_departure_an_unmet_requirement_a_look_a_stop_or_a_word_already_kept_leaves_the_step_to_the_person()
    {
        var review = Record(("claimed", NodeStates.Reached, null), ("holds", NodeStates.Reached, null), ("look", NodeStates.Reached, null),
            ("review", NodeStates.Active, null));
        var required = new QuestRequirementView("merge all changes to remote too", "it reaches its pull request");

        Assert.Equal(("review", NodeWordActs.Accept), ThroughAccepts.Due(Graph, review, [Quest(required) with { Answers = [new(1, "it does", null, null)] }]));
        Assert.Null(ThroughAccepts.Due(Graph, review, [Quest(required) with { Answers = [new(1, null, "kept it local", "too")] }]));
        Assert.Null(ThroughAccepts.Due(Graph, review, [Quest(required)]));
        Assert.Null(ThroughAccepts.Due(Graph, review with { Statuses = new Dictionary<string, NodeStatus>(review.Statuses) { ["look"] = new(Unit, "look", NodeStates.Active) } }, [Quest()]));
        Assert.Null(ThroughAccepts.Due(Graph, review with { Statuses = new Dictionary<string, NodeStatus>(review.Statuses) { ["land"] = new(Unit, "land", NodeStates.Stopped) } }, [Quest()]));
        Assert.Null(ThroughAccepts.Due(Graph, review with { Pending = [new NodeWord("review", NodeWordActs.Accept, null, DateTimeOffset.UnixEpoch)] }, [Quest()]));
        var changed = new NodeHistory(DateTimeOffset.UnixEpoch, "changes", new NodeStatus(Unit, "review", NodeStates.Rejected) { Words = "not yet" });
        Assert.Null(ThroughAccepts.Due(Graph, review with { History = [changed] }, [Quest()]));
    }

    [Fact]
    public void Its_words_name_the_quote_and_the_ask_they_are_kept_on()
    {
        Assert.Equal("accepted on your words, \"please do without my accept\", kept on ask #a1", ThroughAccepts.Words("a1", "please do without my accept"));
    }
}
