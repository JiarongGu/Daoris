using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// WORKFLOW1i-a (D157, the workflow design §4.3): an intake proposes a kind for the ask on the person's quoted words, and the
/// service keeps the proposal on the ask with its reason, beside the person's choice. The reason must quote the ask, as a
/// review choice's words do; only the person's press applies a kind (v1: an agent never sets one).
/// </summary>
public sealed class WorkflowProposalTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-workflow-proposal-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-12T09:00:00Z");

    private const string Sentence = "write the guide for the export button";
    private const string Session = "s1a2b3c4";

    public async Task InitializeAsync()
    {
        foreach (var (name, domain) in new[]
        {
            ("reports", """{ "summary": "The reports.", "owns": ["report pages"], "accepts": ["a report"] }"""),
            ("checker", """{ "summary": "Checks things in a browser.", "owns": ["verification"], "accepts": ["a check"] }"""),
        })
        {
            var dir = Path.Combine(_root, "family", name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""{ "source": "s", "packs": [], "domain": {{domain}} }""");
        }

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var quests = await QuestStore.OpenAsync(_connection);
        var asks = await AskStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync()) await service.RegisterAsync(row with { Workspace = "work" }, Now);

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(service, quests, files: files, asks: asks);
        _desk = new AskDesk(service, asks, _exchange, files);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private async Task<Ask> Asked() => (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;

    private Task<AskOutcome> Publish(Ask ask, WorkflowProposed? proposal, string? session = Session, string title = "The guide") =>
        _desk.PublishAsync(ask.Id, "reports", Now, draft: new AskDraft(title, "The guide is written.") { WorkflowProposal = proposal }, session: session);

    [Fact]
    public async Task A_kind_proposed_on_the_persons_quoted_words_is_kept_on_the_ask_and_never_applied()
    {
        var ask = await Asked();
        var proposed = await Publish(ask, new WorkflowProposed("docs", "the guide for the export button"));

        Assert.Equal(AskRefusal.None, proposed.Refusal);
        Assert.Contains("only the person's press applies", proposed.Message);
        var kept = (await _desk.FindAsync(ask.Id))!;
        Assert.Equal(new AskWorkflowProposal("docs", "the guide for the export button", Now, Session, proposed.Quest!.Id), Assert.Single(kept.WorkflowProposals));
        Assert.Empty(kept.WorkflowChoices);

        // The person's press applies a choice, as it always did: the proposal stays beside it.
        var chosen = await _desk.ChooseWorkflowAsync(ask.Id, "docs", null, null, Now);
        Assert.Equal(AskRefusal.None, chosen.Refusal);
        Assert.Single(chosen.Ask!.WorkflowChoices);
        Assert.Single(chosen.Ask.WorkflowProposals);
    }

    [Fact]
    public async Task A_reason_that_does_not_quote_the_ask_is_refused_and_nothing_is_published()
    {
        var ask = await Asked();
        var refused = await Publish(ask, new WorkflowProposed("docs", "it only changes a document"));

        Assert.Equal(AskRefusal.QuestRefused, refused.Refusal);
        Assert.Contains("never said", refused.Message);
        Assert.Contains("\"it only changes a document\"", refused.Message);
        Assert.Empty((await _desk.FindAsync(ask.Id))!.Quests);
        Assert.Empty((await _desk.FindAsync(ask.Id))!.WorkflowProposals);
    }

    [Fact]
    public async Task A_kind_that_is_not_a_kinds_id_is_refused()
    {
        var ask = await Asked();
        var refused = await Publish(ask, new WorkflowProposed("Docs Only", "the guide for the export button"));

        Assert.Equal(AskRefusal.BadWorkflow, refused.Refusal);
        Assert.Contains("not a kind's id", refused.Message);
        Assert.Empty((await _desk.FindAsync(ask.Id))!.Quests);
    }

    [Fact]
    public async Task A_proposal_with_no_reason_a_reason_past_300_or_from_the_person_is_refused()
    {
        var ask = await Asked();
        foreach (var (proposal, session, said) in new (WorkflowProposed, string?, string)[]
        {
            (new WorkflowProposed("docs", ""), Session, "says why"),
            (new WorkflowProposed("docs", new string('a', WorkflowChoices.ReasonLimit + 1)), Session, "at most 300"),
            (new WorkflowProposed("docs", "the guide"), null, "the person sets their own"),
        })
        {
            var refused = await Publish(ask, proposal, session);
            Assert.Equal(AskRefusal.BadWorkflow, refused.Refusal);
            Assert.Contains(said, refused.Message);
        }

        Assert.Empty((await _desk.FindAsync(ask.Id))!.Quests);
    }

    [Fact]
    public async Task A_proposal_on_a_quest_no_ask_asks_has_no_words_to_quote_and_is_refused()
    {
        var refused = await _exchange.PublishAsync(
            new QuestAsk("checker", "reports", "A repository's quest", "b")
            {
                WorkflowProposal = new WorkflowProposed("docs", "words"), PublishedBy = Session,
            },
            Now);

        Assert.Equal(QuestPublishRefusal.BadWorkflow, refused.Refusal);
        Assert.Contains("on no ask", refused.Message);
    }
}
