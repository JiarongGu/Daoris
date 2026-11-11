using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The review a run's Accept is pressed on, as the landing's plan draws it for the window's Accept: the review under way with its attempt,
/// one not begun with neither, and none where the run's graph holds no review. Seen failing first.
/// </summary>
public sealed class ReviewDrawnTests : DriverModuleBridge
{
    private static readonly DateTimeOffset At = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private void Bound(string graph)
    {
        StoredSteps.Add(Home, new WorkflowVersionAdded("docs", "Docs", "terminal", At.ToString("O"), null, JsonNode.Parse(graph)), kept: []);
        Assert.True(WorkflowRunBindings.Bind(Home, WorkflowRunBindings.Plan(DriverConfig.Empty, Home, "q1", "engine", "aurora", null,
            new WorkflowTaskChoice(null, "docs"), [], At)));
    }

    private const string Reviewed = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"look","kind":"review","version":1,"reach":"pressed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"look"},{"id":"l>h","kind":"after","from":"look","to":"handed"}]}
        """;

    [Fact]
    public void A_review_under_way_is_drawn_with_its_attempt_and_one_not_begun_with_neither()
    {
        Bound(Reviewed);
        var unit = RunUnits.IdOf("q1");
        var files = new NodeRecordFiles(Home, () => true);

        Assert.Equal(new ReviewDrawn("q1", "look", null, null), ReviewDrawn.Of(Home, "q1"));

        files.Write(new NodeRecord(unit) { Statuses = new Dictionary<string, NodeStatus> { ["look"] = new(unit, "look", NodeStates.Active) { Attempt = 2 } } });

        Assert.Equal(new ReviewDrawn("q1", "look", 2, null), ReviewDrawn.Of(Home, "q1"));
    }

    [Fact]
    public void A_run_with_no_review_or_no_graph_draws_none()
    {
        Assert.Null(ReviewDrawn.Of(Home, "q1"));
        Bound(Reviewed.Replace("""{"id":"look","kind":"review","version":1,"reach":"pressed"}""",
            """{"id":"look","kind":"go-ahead","version":1,"reach":"pressed","options":{"act":"deploy the release"}}""", StringComparison.Ordinal));

        Assert.Null(ReviewDrawn.Of(Home, "q1"));
    }
}
