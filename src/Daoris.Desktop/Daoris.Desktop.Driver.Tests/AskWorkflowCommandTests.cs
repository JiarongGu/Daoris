using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOW1e (D157 point 10; the workflow design §4.1 row 1, §4.3, D50): the ask's kind and workflow at a terminal.
/// <c>daoris-driver ask … --kind &lt;kind&gt; [--workflow &lt;id&gt;|current]</c> sends them with a new ask;
/// <c>daoris-driver ask --set-workflow &lt;id&gt; [--kind …] [--workflow …]|--clear</c> sets them on one. A kind is one the ask's
/// workspace declares and a workflow one saved here whose newest version reads, judged before the service is asked; every other
/// answer is the service's sentence, its person's door's refusal included. A service stood in: the fast half (MOD8).
/// </summary>
public sealed class AskWorkflowCommandTests : IDisposable
{
    private const string Chosen = "Ask `#a1b2c3`: the chains it publishes are of kind `docs`, and follow `docs-to-pr` in every repository they reach.";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-ask-workflow-" + Guid.NewGuid().ToString("N")[..8]);

    public AskWorkflowCommandTests()
    {
        Directory.CreateDirectory(_home);
        WorkflowStore.Add(_home, new WorkflowVersionAdded("docs-to-pr", "Documentation to a pull request", "terminal", "2026-10-09T09:00:00Z",
            JsonNode.Parse("""[{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"merge","accept":"you"}]""")), kept: []);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    public static TheoryData<string?, string?, string?, string?, string?, string?> Composed => new()
    {
        // Nothing chosen sends nothing: each repository's choice decides.
        { null, null, null, null, null, null },
        { "docs", null, null, "docs", null, null },
        { null, "current", "  no pull request ", null, "current", "no pull request" },
        { "docs", "docs-to-pr", null, "docs", "docs-to-pr", null },
    };

    [Theory]
    [MemberData(nameof(Composed))]
    public void The_composers_kind_and_workflow_are_read_as_it_sends_them(
        string? kind, string? workflow, string? words, string? keptKind, string? keptWorkflow, string? keptWords)
    {
        Assert.Equal(new AskWorkflowComposed(keptKind, keptWorkflow, keptWords), AskWorkflowCommand.Compose(kind, workflow, words, out var problem));
        Assert.Null(problem);
    }

    public static TheoryData<string?, string?, string?, string> ComposeProblems => new()
    {
        { "Docs", null, null, "`Docs` is not a kind's id" },
        { null, "Docs To PR", null, "`Docs To PR` is not a workflow's id" },
        { null, null, "docs only", "--workflow-words" },
    };

    [Theory]
    [MemberData(nameof(ComposeProblems))]
    public void A_bad_value_with_a_new_ask_is_a_problem(string? kind, string? workflow, string? words, string named)
    {
        Assert.Null(AskWorkflowCommand.Compose(kind, workflow, words, out var problem));
        Assert.Contains(named, problem);
    }

    [Fact]
    public void It_reads_the_choice_on_an_ask_and_a_clear()
    {
        Assert.Equal(new AskWorkflowAsk("a1b2c3", "docs", null, "docs only"),
            AskWorkflowCommand.Read("#a1b2c3", "docs", null, clear: false, ["docs", "only"], out _));
        Assert.Equal(new AskWorkflowAsk("a1b2c3", null, null, null), AskWorkflowCommand.Read("a1b2c3", null, null, clear: true, [], out _));

        foreach (var (id, kind, workflow, clear) in new[] { ("a1b2c3", (string?)null, (string?)null, false), ("a1b2c3", "docs", null, true), ("--yes", "docs", null, false) })
        {
            Assert.Null(AskWorkflowCommand.Read(id, kind, workflow, clear, [], out var problem));
            Assert.StartsWith("--set-workflow takes one ask's id", problem);
        }
    }

    /// <summary>A kind is one the ask's workspace declares; a workflow one saved here whose newest version reads; <c>current</c> needs none.</summary>
    [Fact]
    public void A_kind_is_one_the_workspace_declares_and_a_workflow_one_saved_here()
    {
        var world = World(ReviewChoiceStandIn.Of((_, _, _) => null).Service);

        Assert.Null(AskWorkflowCommand.Unchoosable("docs", "docs-to-pr", "work", world));
        Assert.Null(AskWorkflowCommand.Unchoosable(null, "current", "elsewhere", world));
        Assert.Equal(
            "workspace `elsewhere` declares no kind `docs`: a kind is declared first, with its label. `daoris driver workflow kind elsewhere docs "
            + "--label \"…\"` declares one. Nothing was kept.",
            AskWorkflowCommand.Unchoosable("docs", null, "elsewhere", world));
        Assert.Equal("No workflow `release-train` is saved here: `daoris driver workflow list` names them. Nothing was kept.",
            AskWorkflowCommand.Unchoosable(null, "release-train", "work", world));
    }

    /// <summary>A new ask carries its kind and workflow to the ask's door, and nothing where none was chosen.</summary>
    [Fact]
    public async Task A_new_ask_carries_the_choice_to_the_asks_door()
    {
        var (service, heard) = ReviewChoiceStandIn.Of((method, path, _) => (method, path) is ("POST", "/api/asks")
            ? (HttpStatusCode.OK, """{"ask":{"id":"a1b2c3"},"message":"Asked as `#a1b2c3` in `work`."}""")
            : null);

        await service.AskAsync("work", "write the guide", [], [], null, workflow: new AskWorkflowComposed("docs", "docs-to-pr", "docs only"));
        await service.AskAsync("work", "write the guide", [], [], null, workflow: new AskWorkflowComposed(null, null, null));

        using var chosen = JsonDocument.Parse(heard[0].Body);
        Assert.Equal(("docs", "docs-to-pr", "docs only"), (chosen.RootElement.GetProperty("kind").GetString(),
            chosen.RootElement.GetProperty("workflow").GetString(), chosen.RootElement.GetProperty("workflowWords").GetString()));
        using var none = JsonDocument.Parse(heard[1].Body);
        Assert.False(none.RootElement.TryGetProperty("kind", out _));
        Assert.False(none.RootElement.TryGetProperty("workflow", out _));
    }

    /// <summary>The ask's press: its workflow door, with the kind, the workflow and the words, and the service's sentence verbatim.</summary>
    [Fact]
    public async Task The_choice_on_an_ask_goes_to_its_workflow_door()
    {
        var (service, heard) = Asks((_, _) => (HttpStatusCode.OK, $$"""{"ask":{"id":"a1b2c3"},"message":"{{Chosen}}"}"""));
        var output = new StringWriter();

        Assert.Equal(0, await AskWorkflowCommand.RunAsync(new AskWorkflowAsk("a1b2c3", "docs", "docs-to-pr", "docs only"), World(service), output));

        var (_, path, body) = heard.Single(each => each.Method == "POST");
        Assert.Equal("/api/asks/a1b2c3/workflow", path);
        using var sent = JsonDocument.Parse(body);
        Assert.Equal(("docs", "docs-to-pr", "docs only"), (sent.RootElement.GetProperty("kind").GetString(),
            sent.RootElement.GetProperty("workflow").GetString(), sent.RootElement.GetProperty("words").GetString()));
        Assert.Equal($"{Chosen}\n", output.ToString().ReplaceLineEndings("\n"));

        // A clear sends neither, and is not judged against anything.
        await AskWorkflowCommand.RunAsync(new AskWorkflowAsk("a1b2c3", null, null, null), World(service), new StringWriter());
        using var cleared = JsonDocument.Parse(heard.Last(each => each.Method == "POST").Body);
        Assert.Empty(cleared.RootElement.EnumerateObject());
    }

    /// <summary>A kind the ask's workspace does not declare is refused here, and nothing is sent.</summary>
    [Fact]
    public async Task A_kind_on_an_ask_is_judged_against_its_workspace()
    {
        var (service, heard) = Asks((_, _) => (HttpStatusCode.OK, $$"""{"ask":{"id":"a1b2c3"},"message":"{{Chosen}}"}"""));
        var output = new StringWriter();

        Assert.Equal(1, await AskWorkflowCommand.RunAsync(new AskWorkflowAsk("a1b2c3", "design", null, null), World(service), output));

        Assert.DoesNotContain(heard, each => each.Method == "POST");
        Assert.StartsWith("workspace `work` declares no kind `design`", output.ToString());
    }

    /// <summary>
    /// The person's door (D156): a keyless call on a keyed host is refused in the service's words, passed through whole, and exit 1.
    /// </summary>
    [Fact]
    public async Task The_persons_door_refusal_is_the_services_sentence_passed_through()
    {
        const string Refusal = "Only the person can give an ask's kind and workflow, and this call carried no key of theirs. Nothing was kept. "
            + "In Daoris's window, press it there; from a terminal, run it again and confirm it in the window. An agent asks the person "
            + "instead: a go-ahead, or its closing note.";
        var (service, _) = Asks((_, _) => (HttpStatusCode.Forbidden, JsonSerializer.Serialize(new { error = Refusal, code = "person-only" })));
        var output = new StringWriter();

        Assert.Equal(1, await AskWorkflowCommand.RunAsync(new AskWorkflowAsk("a1b2c3", null, "current", null), World(service), output));

        Assert.Equal($"{Refusal}\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void The_usage_names_both_forms_and_the_console_reads_them()
    {
        var usage = DriverCommand.Usage.ReplaceLineEndings("\n");
        Assert.Contains("\n  ask … --kind <kind> [--workflow <id>|current] [--workflow-words \"…\"]\n", usage);
        Assert.Contains("\n  ask --set-workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear [\"…\"]\n", usage);

        var console = File.ReadAllText(Path.Combine(
            Daoris.Driver.Tests.HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "AskConsole.cs"));
        Assert.Contains("case \"--kind\": kind = Value(); break;", console);
        Assert.Contains("case \"--set-workflow\": setWorkflow = Value(); break;", console);
        Assert.Contains("AskWorkflowCommand.Read(setWorkflow, kind, workflow, clearWorkflow, words, out var workflowProblem)", console);
        Assert.Contains("AskWorkflowCommand.Compose(kind, workflow, workflowWords, out var workflowComposeProblem)", console);
        Assert.Contains("workflow: chosenWorkflow", console);
    }

    private AskWorkflowWorld World(ServiceClient service) => new(service, () => DriverConfig.Parse("""
        {"workspaceWorkflows":{"work":{"kinds":{"docs":{"label":"Documentation"}}}}}
        """), _home);

    /// <summary>A service holding ask <c>a1b2c3</c> in <c>work</c>, answering each post by <paramref name="post"/>.</summary>
    private static (ServiceClient Service, List<(string Method, string Path, string Body)> Heard) Asks(
        Func<string, string, (HttpStatusCode Status, string Body)?> post) =>
        ReviewChoiceStandIn.Of((method, path, body) => (method, path) switch
        {
            ("GET", "/api/asks/a1b2c3") => (HttpStatusCode.OK,
                """{"id":"a1b2c3","workspace":"work","sentence":"write the guide","state":"Open","tier":"declarations"}"""),
            ("POST", _) => post(path, body),
            _ => null,
        });
}
