using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A run's review over the bridge, opened by run: <c>RUN_REVIEW</c> answers the record and where the review stands, by run or by any session of
/// the run's chain (an earlier part's included), with no tree or session needed; <c>RUN_REVIEW_FILE</c> draws a file at the record's revision.
/// </summary>
public sealed class DriverModuleRunReviewTests : DriverModuleBridge
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-12T09:00:00Z");

    private const string Graph = """
        {"schemaVersion":2,"entries":["work"],"end":"review",
         "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},{"id":"review","kind":"review","version":1,"reach":"pressed"}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"review"}]}
        """;

    private void Recorded()
    {
        StoredSteps.Add(Home, new WorkflowVersionAdded("docs", "Docs", "terminal", T.ToString("O"), null, System.Text.Json.Nodes.JsonNode.Parse(Graph)), kept: []);
        Assert.True(WorkflowRunBindings.Bind(Home, WorkflowRunBindings.Plan(DriverConfig.Empty, Home, "q1", "engine", "aurora", null, new WorkflowTaskChoice(null, "docs"), [], T)));
        var unit = RunUnits.IdOf("q1");
        new NodeRecordFiles(Home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new SortedDictionary<string, NodeStatus>(StringComparer.Ordinal) { ["review"] = new(unit, "review", NodeStates.Active) { Attempt = 1, Since = T } },
        });
        ReviewRecords.Keep(Home, new ReviewRecord("q1", "review", 1, ReviewKinds.Answer, "aaaaaaa1", T) { Place = "engine" });
    }

    private async Task<DriverModule> UpAsync()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(new StandInService()))));
        return module;
    }

    [Fact]
    public async Task A_runs_review_is_read_by_run_with_its_record_and_presses()
    {
        Recorded();
        var module = await UpAsync();

        var review = await AnswerAsync(module, "RUN_REVIEW", new { run = "q1", attempt = 0 });

        Assert.Equal(("q1", "review", "aaaaaaa1"), (review.GetProperty("run").GetString(), review.GetProperty("node").GetString(),
            review.GetProperty("record").GetProperty("revision").GetString()));
        Assert.Equal(["accept", "changes", "stop"], review.GetProperty("presses").EnumerateArray().Select(press => press.GetString()));
    }

    [Theory]
    [InlineData("s1")]
    [InlineData("s2")]
    public async Task Every_session_of_the_runs_chain_opens_its_review(string session)
    {
        Recorded();
        var module = await UpAsync();

        var review = await AnswerAsync(module, "RUN_REVIEW", new { session, attempt = 0 });

        Assert.Equal("q1", review.GetProperty("run").GetString());
    }

    [Fact]
    public async Task A_run_not_bound_here_is_answered_not_run_and_a_file_outside_the_record_not_row()
    {
        Recorded();
        var module = await UpAsync();

        var none = await AnswerAsync(module, "RUN_REVIEW", new { run = "q9", attempt = 0 });
        var file = await AnswerAsync(module, "RUN_REVIEW_FILE", new { run = "q1", attempt = 1, path = "notes.md", side = "after" });

        Assert.Equal("not-run", none.GetProperty("refused").GetString());
        Assert.Equal("not-kept", file.GetProperty("refused").GetString());
    }

    /// <summary>A chain of two parts in <c>engine</c>, q1 then q2, served by s1 and s2.</summary>
    private sealed class StandInService : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = path switch
            {
                _ when path.StartsWith("/api/quests", StringComparison.Ordinal) => """
                    [{"id":"q1","from":"ask #a1","to":"engine","title":"Part one","body":"","status":"Done","updated":"2026-11-12T09:00:00Z"},
                     {"id":"q2","from":"ask #a1","to":"engine","title":"Part two","body":"","status":"Taken","parent":"q1","updated":"2026-11-12T09:10:00Z"}]
                    """,
                _ when path.StartsWith("/api/sessions", StringComparison.Ordinal) => """
                    [{"id":"s1","repository":"engine","state":"completed","quest":"q1","door":"claude-code","created":"2026-11-12T09:00:00Z"},
                     {"id":"s2","repository":"engine","state":"working","quest":"q2","door":"claude-code","created":"2026-11-12T09:10:00Z"}]
                    """,
                _ when path.StartsWith("/api/registry", StringComparison.Ordinal) => """{"repositories":[]}""",
                _ => "[]",
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
