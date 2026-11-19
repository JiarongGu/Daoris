using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A quest not yet taken has no bound run: its run says it has not started and which workflow its start will bind, never "no workflow is chosen" where its repository chooses one.</summary>
public sealed class WorkflowNotStartedTests
{
    private static async Task<WorkflowRun> ReadAsync(DriverConfig config)
    {
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(new OpenQuest())));
        var home = Path.Combine(AppContext.BaseDirectory, "no-home-" + Guid.NewGuid().ToString("N")[..8]);
        var sources = new WorkflowRunSources(service, home, config, [], _ => "aurora");
        return (await WorkflowRunReader.ReadAsync(new WorkflowRunAsk(Quest: "q1"), sources)).Runs.Single();
    }

    [Fact]
    public async Task An_open_quest_of_a_repository_with_a_default_reads_not_started_naming_it()
    {
        var run = await ReadAsync(DriverConfig.Parse("""{"workflows":{"engine":{"default":"daoris-dev"}}}"""));

        Assert.Equal("not started yet: waits to be taken (`daoris-dev`, by the repository's choice)", run.Problem);
        Assert.DoesNotContain("no workflow is chosen", run.Problem, StringComparison.Ordinal);
        Assert.Contains("Not started yet", WorkflowRunWords.Say([run], null), StringComparison.Ordinal);
        Assert.DoesNotContain("Workflow unread", WorkflowRunWords.Say([run], null), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_open_quest_of_a_repository_with_no_choice_still_says_no_workflow_is_chosen()
    {
        var run = await ReadAsync(DriverConfig.Empty);

        Assert.Equal(WorkflowProcess.NoWorkflow("engine"), run.Problem);
    }

    private sealed class OpenQuest : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.PathAndQuery == "/api/quests?includeClosed=true"
                    ? """[{"id":"q1","from":"daoris","to":"engine","title":"Quest q1","body":"","status":"Open","updated":"2026-10-09T09:30:00Z"}]"""
                    : "[]", System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
