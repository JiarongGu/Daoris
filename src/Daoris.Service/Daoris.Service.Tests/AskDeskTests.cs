using System.Text;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// An ask (D65 §1a): a sentence, with links and files, entered at a WORKSPACE rather than at a
/// repository — held as a record of what was asked and what became of it. With no intake harness
/// the declarations tier proposes and says so; a named receiver is published to at once; a person
/// turns a proposal into a quest. Nothing is ever published on a guess.
/// </summary>
public sealed class AskDeskTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-asks-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskDesk _desk = null!;
    private QuestFiles _files = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-23T10:00:00Z");

    public async Task InitializeAsync()
    {
        Repo("media-api", """{ "summary": "The media service and its config.", "owns": ["media config", "video and image field names"], "accepts": ["a media bug"] }""");
        Repo("storefront", """{ "summary": "The storefront.", "owns": ["product pages", "checkout"], "accepts": ["a UI bug"] }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        var asks = await AskStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync())
        {
            await service.RegisterAsync(row with { Workspace = "work" }, Now);
        }

        _files = new QuestFiles(Path.Combine(_root, "home"));
        _desk = new AskDesk(service, asks, new QuestExchange(service, _quests, files: _files), _files);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Repo(string name, string domain)
    {
        var dir = Path.Combine(_root, "family", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""{ "source": "s", "packs": [], "domain": {{domain}} }""");
    }

    private const string Sentence =
        "check the ticket and use the media config instead of hard coding the video/image field name";

    [Fact]
    public async Task With_no_harness_the_declarations_propose_and_say_so_and_nothing_is_published()
    {
        var outcome = await _desk.AskAsync(new AskRequest("work", Sentence), Now);

        Assert.Equal(AskRefusal.None, outcome.Refusal);
        var ask = outcome.Ask!;
        Assert.Equal(AskState.Proposed, ask.State);
        Assert.Equal(AskDesk.ByDeclarations, ask.Tier);
        Assert.Equal("media-api", ask.Proposal[0].Repository);
        Assert.Contains("by declarations only", outcome.Message);
        Assert.Contains("no intake harness ran", outcome.Message);
        Assert.Contains("media-api", outcome.Message);
        Assert.Null(outcome.Quest);
        Assert.Empty(await _quests.ListAsync());
    }

    [Fact]
    public async Task A_sentence_nobody_declared_is_proposed_to_nobody_and_says_so()
    {
        var outcome = await _desk.AskAsync(new AskRequest("work", "rotate the office wifi password"), Now);

        Assert.Equal(AskState.Proposed, outcome.Ask!.State);
        Assert.Empty(outcome.Ask.Proposal);
        Assert.Contains("no repository's declarations share its words", outcome.Message);
    }

    /// <summary>
    /// Naming the receiver is the asker deciding, so the quest is published at once — asked BY the
    /// ask, in the ask's circle, carrying its links and files — and the ask records it.
    /// </summary>
    [Fact]
    public async Task A_named_receiver_is_published_to_at_once_carrying_the_links_and_files()
    {
        var outcome = await _desk.AskAsync(
            new AskRequest("work", Sentence)
            {
                To = "media-api",
                Links = ["https://tickets.example/MEDIA-142"],
                Uploads = [new QuestUpload("broken.png", Encoding.UTF8.GetBytes("pixels"))],
            },
            Now);

        Assert.Equal(AskRefusal.None, outcome.Refusal);
        var quest = outcome.Quest!;
        Assert.Equal($"ask #{outcome.Ask!.Id}", quest.From);
        Assert.Equal("media-api", quest.To);
        Assert.Equal("work", quest.Workspace);
        Assert.Equal(["https://tickets.example/MEDIA-142"], quest.Links);
        Assert.True(_files.Has(quest.Id, Assert.Single(quest.Attachments)));
        Assert.Contains(Sentence, quest.Body);
        Assert.Equal(AskState.Published, outcome.Ask.State);
        Assert.Equal(AskDesk.ByName, outcome.Ask.Tier);
        Assert.Equal([quest.Id], outcome.Ask.Quests);
    }

    /// <summary>
    /// A receiver that cannot be asked is refused in the exchange's own words — and the ask is KEPT,
    /// with the declarations' proposal, because the sentence is still what the person meant.
    /// </summary>
    [Fact]
    public async Task A_named_receiver_that_cannot_be_asked_keeps_the_ask_with_its_proposal()
    {
        var outcome = await _desk.AskAsync(new AskRequest("work", Sentence) { To = "nobody-here" }, Now);

        Assert.Equal(AskRefusal.QuestRefused, outcome.Refusal);
        Assert.Contains("nobody-here", outcome.Message);
        Assert.Equal(AskState.Proposed, outcome.Ask!.State);
        Assert.Equal("media-api", outcome.Ask.Proposal[0].Repository);
        Assert.Empty(await _quests.ListAsync());
    }

    /// <summary>A person turns a proposal into a quest — the files the ask kept travel with it.</summary>
    [Fact]
    public async Task A_person_publishes_a_proposal_and_the_asks_files_travel_with_it()
    {
        var asked = await _desk.AskAsync(
            new AskRequest("work", Sentence) { Uploads = [new QuestUpload("trace.log", Encoding.UTF8.GetBytes("stack"))] },
            Now);

        var published = await _desk.PublishAsync(asked.Ask!.Id, "media-api", Now.AddMinutes(5));

        Assert.Equal(AskRefusal.None, published.Refusal);
        Assert.Equal(AskState.Published, published.Ask!.State);
        var quest = published.Quest!;
        Assert.Equal("stack", await File.ReadAllTextAsync(_files.PathOf(quest.Id, Assert.Single(quest.Attachments))));
        Assert.Equal([quest.Id], published.Ask.Quests);
    }

    [Fact]
    public async Task Asking_the_same_sentence_again_in_a_circle_is_the_same_ask()
    {
        var first = await _desk.AskAsync(new AskRequest("work", Sentence), Now);
        var second = await _desk.AskAsync(new AskRequest("work", "  " + Sentence + " "), Now.AddMinutes(1));

        Assert.Equal(first.Ask!.Id, second.Ask!.Id);
        Assert.Contains("already asked", second.Message);
        Assert.Single(await _desk.ListAsync("work"));
    }

    [Fact]
    public async Task An_ask_in_a_circle_this_machine_does_not_hold_is_refused_naming_the_ones_it_does()
    {
        var outcome = await _desk.AskAsync(new AskRequest("elsewhere", Sentence), Now);

        Assert.Equal(AskRefusal.UnknownWorkspace, outcome.Refusal);
        Assert.Contains("elsewhere", outcome.Message);
        Assert.Contains("work", outcome.Message);
        Assert.Null(outcome.Ask);
    }

    [Fact]
    public async Task An_empty_sentence_is_refused()
    {
        Assert.Equal(AskRefusal.Empty, (await _desk.AskAsync(new AskRequest("work", "   "), Now)).Refusal);
    }

    /// <summary>Closing is a person's answer to their own ask — it needs a reason, and it is final.</summary>
    [Fact]
    public async Task A_closed_ask_keeps_its_reason_and_publishes_nothing_more()
    {
        var asked = await _desk.AskAsync(new AskRequest("work", Sentence), Now);

        var noReason = await _desk.CloseAsync(asked.Ask!.Id, "  ", Now);
        var closed = await _desk.CloseAsync(asked.Ask.Id, "Handled on a call.", Now);
        var late = await _desk.PublishAsync(asked.Ask.Id, "media-api", Now);

        Assert.Equal(AskRefusal.Empty, noReason.Refusal);
        Assert.Equal(AskState.Closed, closed.Ask!.State);
        Assert.Equal("Handled on a call.", closed.Ask.Note);
        Assert.Equal(AskRefusal.Closed, late.Refusal);
        Assert.Empty(await _quests.ListAsync());
    }

    [Fact]
    public async Task Publishing_an_ask_that_does_not_exist_is_refused()
    {
        Assert.Equal(AskRefusal.NotFound, (await _desk.PublishAsync("zzzzzz", "media-api", Now)).Refusal);
    }
}
