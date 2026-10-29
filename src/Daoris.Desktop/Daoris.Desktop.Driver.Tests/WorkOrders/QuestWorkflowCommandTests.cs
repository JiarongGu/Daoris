using System.Net;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOW1e2 (D157; the workflow design §4.1 row 1, §4.3, D50): a quest's own kind and workflow at a terminal,
/// <c>daoris-driver quest workflow &lt;id&gt; [--kind …] [--workflow …]|--clear ["…"]</c>, for a quest no ask published. A kind is one
/// the quest's workspace declares, judged before the service is asked; every other answer is the service's sentence. A service
/// stood in: the fast half (MOD8).
/// </summary>
public sealed class QuestWorkflowCommandTests
{
    private const string Chosen = "Quest `#a1b2c3d4e5f6` is of kind `docs`: its work follows the workflow its repository or workspace maps it to.";

    [Fact]
    public void It_reads_a_kind_a_workflow_both_or_a_clear_and_the_words_after()
    {
        Assert.Equal(new QuestWorkflowAsk("a1b2c3d4e5f6", "docs", null, "docs only"),
            QuestWorkflowCommand.Read(["workflow", "#a1b2c3d4e5f6", "--kind", "docs", "docs", "only"], out _));
        Assert.Equal(new QuestWorkflowAsk("a1b2c3d4e5f6", "docs", "current", null),
            QuestWorkflowCommand.Read(["workflow", "a1b2c3d4e5f6", "--workflow", "current", "--kind", "docs"], out _));
        Assert.Equal(new QuestWorkflowAsk("a1b2c3d4e5f6", null, null, null),
            QuestWorkflowCommand.Read(["workflow", "a1b2c3d4e5f6", "--clear"], out _));
        Assert.True(QuestWorkflowCommand.Asks(["workflow", "a1b2c3d4e5f6"]));
        Assert.False(QuestWorkflowCommand.Asks(["review", "a1b2c3d4e5f6"]));
    }

    public static TheoryData<string[], string> Problems => new()
    {
        { ["workflow"], "quest workflow takes one quest's id" },
        { ["workflow", "--kind", "docs"], "quest workflow takes one quest's id" },
        { ["workflow", "a1b2c3d4e5f6"], "never neither" },
        { ["workflow", "a1b2c3d4e5f6", "--kind", "docs", "--clear"], "never --clear with one" },
        { ["workflow", "a1b2c3d4e5f6", "--kind"], "does not take `--kind`" },
        { ["workflow", "a1b2c3d4e5f6", "--kind", "Docs"], "`Docs` is not a kind's id" },
        { ["workflow", "a1b2c3d4e5f6", "--workflow", "Docs To PR"], "`Docs To PR` is not a workflow's id" },
        { ["workflow", "a1b2c3d4e5f6", "--yes"], "does not take `--yes`" },
    };

    [Theory]
    [MemberData(nameof(Problems))]
    public void A_line_that_is_not_one_is_a_problem(string[] args, string named)
    {
        Assert.Null(QuestWorkflowCommand.Read(args, out var problem));
        Assert.Contains(named, problem);
    }

    /// <summary>The quest page's press: its workflow door, with the kind and the words, and the service's sentence verbatim.</summary>
    [Fact]
    public async Task The_choice_goes_to_the_quests_workflow_door()
    {
        var (service, heard) = Quests((HttpStatusCode.OK, $$"""{"quest":{"id":"a1b2c3d4e5f6"},"message":"{{Chosen}}"}"""));
        var output = new StringWriter();

        Assert.Equal(0, await QuestWorkflowCommand.RunAsync(new QuestWorkflowAsk("a1b2c3d4e5f6", "docs", null, "docs only"), World(service), output));

        var (_, path, body) = heard.Single(each => each.Method == "POST");
        Assert.Equal("/api/quests/a1b2c3d4e5f6/workflow", path);
        using var sent = JsonDocument.Parse(body);
        Assert.Equal(("docs", "docs only"), (sent.RootElement.GetProperty("kind").GetString(), sent.RootElement.GetProperty("words").GetString()));
        Assert.False(sent.RootElement.TryGetProperty("workflow", out _));
        Assert.Equal($"{Chosen}\n", output.ToString().ReplaceLineEndings("\n"));
    }

    /// <summary>A kind the quest's workspace does not declare is refused here, and nothing is sent.</summary>
    [Fact]
    public async Task A_kind_is_judged_against_the_quests_workspace()
    {
        var (service, heard) = Quests((HttpStatusCode.OK, $$"""{"quest":{"id":"a1b2c3d4e5f6"},"message":"{{Chosen}}"}"""));
        var output = new StringWriter();

        Assert.Equal(1, await QuestWorkflowCommand.RunAsync(new QuestWorkflowAsk("a1b2c3d4e5f6", "design", null, null), World(service), output));

        Assert.DoesNotContain(heard, each => each.Method == "POST");
        Assert.StartsWith("workspace `work` declares no kind `design`", output.ToString());
    }

    /// <summary>The service's refusal, a quest an ask published here, is passed through whole, and exit 1.</summary>
    [Fact]
    public async Task A_service_refusal_is_its_sentence_passed_through()
    {
        const string Refusal = "Quest `#a1b2c3d4e5f6` was asked by ask `#f00d01`, whose kind and workflow are its work's: choose them on the ask. "
            + "Nothing was kept.";
        var (service, _) = Quests((HttpStatusCode.Conflict, JsonSerializer.Serialize(new { error = Refusal })));
        var output = new StringWriter();

        Assert.Equal(1, await QuestWorkflowCommand.RunAsync(new QuestWorkflowAsk("a1b2c3d4e5f6", null, null, null), World(service), output));

        Assert.Equal($"{Refusal}\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void The_usage_names_the_door_and_the_host_reads_it()
    {
        Assert.Contains("\n  quest workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear [\"…\"]\n",
            DriverCommand.Usage.ReplaceLineEndings("\n"));
        var host = File.ReadAllText(Path.Combine(
            Daoris.Driver.Tests.HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "Program.cs"));
        Assert.Contains("QuestWorkflowCommand.Read(questArgs, out var workflowProblem)", host);
    }

    /// <summary>A kind's workspace declared; a workflow is never read here, since only a kind is judged.</summary>
    private static AskWorkflowWorld World(ServiceClient service) => new(service, () => DriverConfig.Parse("""
        {"workspaceWorkflows":{"work":{"kinds":{"docs":{"label":"Documentation"}}}}}
        """), "no-home-is-read");

    /// <summary>A service holding quest <c>a1b2c3d4e5f6</c> in <c>work</c>, answering its workflow door with <paramref name="post"/>.</summary>
    private static (ServiceClient Service, List<(string Method, string Path, string Body)> Heard) Quests((HttpStatusCode Status, string Body) post) =>
        ReviewChoiceStandIn.Of((method, path, _) => (method, path) switch
        {
            ("GET", "/api/quests") => (HttpStatusCode.OK,
                """[{"id":"a1b2c3d4e5f6","from":"daoris","to":"web-app","title":"Docs","body":"b","status":"Open","workspace":"work"}]"""),
            ("POST", "/api/quests/a1b2c3d4e5f6/workflow") => post,
            _ => null,
        });
}
