using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// WORKFLOW1e (D157 point 10; the workflow design §4.1 row 1, §4.3): the ask's kind and workflow, the person's choice for every chain
/// it publishes. Kept with when and their words, from the composer and on the ask, the latest standing; one naming neither clears
/// it. The service judges only the shape: which kinds a workspace declares and which workflows read are the driving machine's.
/// </summary>
public sealed class AskWorkflowTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-ask-workflow-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private AskStore _asks = null!;
    private AskDesk _desk = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-09T09:00:00Z");

    private const string Sentence = "write the guide for the export button";

    public async Task InitializeAsync()
    {
        var dir = Path.Combine(_root, "family", "reports");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"),
            """{ "source": "s", "packs": [], "domain": { "summary": "The reports.", "owns": ["report pages"], "accepts": ["a report"] } }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var quests = await QuestStore.OpenAsync(_connection);
        _asks = await AskStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync()) await service.RegisterAsync(row with { Workspace = "work" }, Now);

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _desk = new AskDesk(service, _asks, new QuestExchange(service, quests, files: files, asks: _asks), files);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task The_composers_kind_and_workflow_are_kept_with_the_ask_and_its_words()
    {
        var asked = await _desk.AskAsync(new AskRequest("work", Sentence) { Kind = "docs", Workflow = "docs-to-pr", WorkflowWords = "  docs only  " }, Now);

        Assert.Equal(AskRefusal.None, asked.Refusal);
        Assert.Equal(new AskWorkflowChoice("docs", "docs-to-pr", Now, "docs only"), Assert.Single(asked.Ask!.WorkflowChoices));
        Assert.Equal(asked.Ask.WorkflowChoices, (await _desk.FindAsync(asked.Ask.Id))!.WorkflowChoices);
    }

    [Fact]
    public async Task An_ask_with_no_kind_and_no_workflow_keeps_none_as_it_always_did()
    {
        var asked = await _desk.AskAsync(new AskRequest("work", Sentence) { Kind = " ", Workflow = "" }, Now);

        Assert.Equal(AskRefusal.None, asked.Refusal);
        Assert.Empty(asked.Ask!.WorkflowChoices);
    }

    [Fact]
    public async Task The_latest_stands_and_one_naming_neither_clears_it()
    {
        var ask = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;

        var kind = await _desk.ChooseWorkflowAsync(ask.Id, "docs", null, null, Now.AddMinutes(1));
        Assert.Equal(AskRefusal.None, kind.Refusal);
        Assert.Equal(
            $"Ask `#{ask.Id}`: the chains it publishes are of kind `docs`; each repository's choice for it decides their workflow. "
            + "Work already started keeps the workflow it bound.", kind.Message);

        var both = await _desk.ChooseWorkflowAsync(ask.Id, "docs", "current", "  no pull request  ", Now.AddMinutes(2));
        Assert.Equal(
            $"Ask `#{ask.Id}`: the chains it publishes are of kind `docs`, and follow Current, each repository's rules as they stand, in "
            + "every repository they reach. Work already started keeps the workflow it bound. Your words are kept with it.", both.Message);

        var named = await _desk.ChooseWorkflowAsync(ask.Id, null, "docs-to-pr", null, Now.AddMinutes(3));
        Assert.Equal($"Ask `#{ask.Id}`: the chains it publishes follow `docs-to-pr` in every repository they reach. Work already started "
            + "keeps the workflow it bound.", named.Message);

        var cleared = await _desk.ChooseWorkflowAsync(ask.Id, " ", null, null, Now.AddMinutes(4));
        Assert.Equal($"Ask `#{ask.Id}`: no kind and no workflow; each repository's choice decides the workflow the chains it publishes "
            + "follow. Work already started keeps the workflow it bound.", cleared.Message);

        var stands = (await _desk.FindAsync(ask.Id))!.WorkflowChoices;
        Assert.Equal(
            [
                new AskWorkflowChoice("docs", null, Now.AddMinutes(1)),
                new AskWorkflowChoice("docs", "current", Now.AddMinutes(2), "no pull request"),
                new AskWorkflowChoice(null, "docs-to-pr", Now.AddMinutes(3)),
                new AskWorkflowChoice(null, null, Now.AddMinutes(4)),
            ],
            stands);
    }

    [Fact]
    public async Task A_kind_or_a_workflow_that_is_not_one_by_its_shape_is_refused_and_nothing_is_kept()
    {
        var ask = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;

        var kind = await _desk.ChooseWorkflowAsync(ask.Id, "Docs", null, null, Now);
        Assert.Equal(AskRefusal.BadWorkflow, kind.Refusal);
        Assert.Equal("The ask's kind `Docs` is not a kind's id: lower-case letters, digits and dashes, at most 24, such as `docs`. Nothing was kept.", kind.Message);

        var workflow = await _desk.ChooseWorkflowAsync(ask.Id, null, "Docs To PR", null, Now);
        Assert.Equal(AskRefusal.BadWorkflow, workflow.Refusal);
        Assert.Equal("The ask's workflow `Docs To PR` is not a workflow's id: lower-case letters, digits and dashes, at most 40, such as "
            + "`docs-to-pr`; or `current`. Nothing was kept.", workflow.Message);

        var words = await _desk.ChooseWorkflowAsync(ask.Id, "docs", null, new string('w', WorkflowChoices.WordsLimit + 1), Now);
        Assert.Equal(AskRefusal.BadWorkflow, words.Refusal);
        Assert.Equal(AskRefusal.BadWorkflow, (await _desk.ChooseWorkflowAsync(ask.Id, new string('k', 25), null, null, Now)).Refusal);
        Assert.Equal(AskRefusal.None, (await _desk.ChooseWorkflowAsync(ask.Id, new string('k', 24), null, null, Now)).Refusal);

        var composed = await _desk.AskAsync(new AskRequest("work", "another sentence") { Workflow = "../driver" }, Now);
        Assert.Equal(AskRefusal.BadWorkflow, composed.Refusal);
        Assert.Null(composed.Ask);
        Assert.Single((await _desk.FindAsync(ask.Id))!.WorkflowChoices);
    }

    [Fact]
    public async Task A_closed_or_unknown_ask_takes_no_choice()
    {
        var ask = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;
        await _desk.CloseAsync(ask.Id, "not needed", Now);

        Assert.Equal(AskRefusal.Closed, (await _desk.ChooseWorkflowAsync(ask.Id, "docs", null, null, Now)).Refusal);
        Assert.Equal(AskRefusal.NotFound, (await _desk.ChooseWorkflowAsync("ffffff", "docs", null, null, Now)).Refusal);
    }

    /// <summary>A choice this build cannot read, a newer build's or a hand edit, is passed over, never a failed read of the ask.</summary>
    [Fact]
    public async Task A_kept_choice_that_does_not_read_is_passed_over()
    {
        var ask = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;
        await using (var command = _connection.CreateCommand())
        {
            command.CommandText = """
                UPDATE asks SET workflow_choices = '[{"kind":"Docs","at":"2026-10-09T09:00:00Z"},{"workflow":"docs-to-pr"},7,
                  {"kind":"docs","at":"2026-10-09T09:01:00Z"}]' WHERE id = $id
                """;
            command.Parameters.AddWithValue("$id", ask.Id);
            await command.ExecuteNonQueryAsync();
        }

        Assert.Equal([new AskWorkflowChoice("docs", null, Now.AddMinutes(1))], (await _desk.FindAsync(ask.Id))!.WorkflowChoices);
    }
}
