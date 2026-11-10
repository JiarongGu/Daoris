using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// An ask asks each receiver once: a publish to a receiver it already holds a live quest for answers that quest, a session's publish beside
/// the ask's intake is refused while the intake answers it or is still to, the creation's answer says the intake publishes, and a publish
/// names its session and says when it carries no requirement.
/// </summary>
public sealed class AskPublishOnceTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-ask-once-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-10T06:41:28Z");
    private const string Sentence = "check the ticket and use the media config instead of hard coding the video/image field name";

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private KnowledgeService _service = null!;
    private AskStore _asks = null!;
    private AskDesk _desk = null!;

    public async Task InitializeAsync()
    {
        var dir = Path.Combine(_root, "family", "media-api");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"),
            """{ "source": "s", "packs": [], "domain": { "summary": "The media service.", "owns": ["media config"], "accepts": ["a media bug"] } }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        _asks = await AskStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await _service.RegistryAsync()) await _service.RegisterAsync(row with { Workspace = "work" }, Now);

        var files = new QuestFiles(Path.Combine(_root, "home"));
        var exchange = new QuestExchange(_service, _quests, remotes: null, files, _sessions, _asks);
        _desk = new AskDesk(_service, _asks, exchange, files, _sessions);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private SessionLedger Ledger() => new(_quests, _sessions, _service, _asks);

    [Fact]
    public async Task A_second_publish_to_a_receiver_the_ask_holds_a_quest_for_answers_that_quest_and_publishes_nothing()
    {
        var asked = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;
        var first = (await _desk.PublishAsync(asked.Id, "media-api", Now.AddSeconds(29))).Quest!;

        var again = await _desk.PublishAsync(asked.Id, "media-api", Now.AddMinutes(5), byAgent: true);

        Assert.Equal(AskRefusal.None, again.Refusal);
        Assert.Equal(first.Id, again.Quest!.Id);
        Assert.Contains($"`#{first.Id}`", again.Message);
        Assert.Contains("nothing new was published", again.Message);
        Assert.Single(await _quests.FromAsync(AskDesk.SenderOf(asked.Id)));
    }

    [Fact]
    public async Task A_sessions_publish_while_the_asks_intake_answers_it_is_refused_naming_the_intake_and_the_intakes_own_goes_on()
    {
        var asked = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;
        var intake = (await Ledger().OpenIntakeAsync(asked.Id, "stub", Path.Combine(_root, "room"), Now.AddSeconds(4))).Session!;
        var helper = (await Ledger().OpenHelpAsync("stub", Path.Combine(_root, "help"), Now, workspace: "work")).Session!;

        var beside = await _desk.PublishAsync(asked.Id, "media-api", Now.AddSeconds(10), session: helper.Id);

        Assert.Equal(AskRefusal.IntakeAnswering, beside.Refusal);
        Assert.Contains($"`{intake.Id}`", beside.Message);
        Assert.Contains("nothing was published", beside.Message);
        Assert.Empty(await _quests.FromAsync(AskDesk.SenderOf(asked.Id)));

        var own = await _desk.PublishAsync(asked.Id, "media-api", Now.AddSeconds(20), session: intake.Id);
        Assert.Equal(AskRefusal.None, own.Refusal);
        Assert.Equal(intake.Id, own.Quest!.PublishedBy);
    }

    [Fact]
    public async Task A_sessions_publish_on_an_ask_no_intake_has_served_yet_is_refused_and_the_persons_press_is_not()
    {
        var asked = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;
        var helper = (await Ledger().OpenHelpAsync("stub", Path.Combine(_root, "help"), Now, workspace: "work")).Session!;

        var early = await _desk.PublishAsync(asked.Id, "media-api", Now.AddSeconds(2), session: helper.Id);

        Assert.Equal(AskRefusal.IntakeAnswering, early.Refusal);
        Assert.Contains("its intake", early.Message);
        Assert.Empty(await _quests.FromAsync(AskDesk.SenderOf(asked.Id)));
        Assert.Equal(AskRefusal.None, (await _desk.PublishAsync(asked.Id, "media-api", Now.AddSeconds(3))).Refusal);
    }

    [Fact]
    public async Task The_creations_answer_says_the_intake_publishes_and_never_that_nothing_was_published()
    {
        var outcome = await _desk.AskAsync(new AskRequest("work", Sentence), Now);

        Assert.DoesNotContain("Nothing was published", outcome.Message);
        Assert.Contains("the intake publishes", outcome.Message);
        Assert.Contains("by declarations only", outcome.Message);
    }

    [Fact]
    public async Task A_publish_names_the_session_that_made_it_carries_its_requirements_and_says_when_it_carries_none()
    {
        var asked = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;
        var intake = (await Ledger().OpenIntakeAsync(asked.Id, "stub", Path.Combine(_root, "room"), Now.AddSeconds(4))).Session!;
        var draft = new AskDraft(null, null) { Requirements = [new QuestRequirement("use the media config", "the field name is read from the config")] };

        var carried = await _desk.PublishAsync(asked.Id, "media-api", Now.AddSeconds(20), draft: draft, session: intake.Id);
        Assert.Equal(intake.Id, carried.Quest!.PublishedBy);
        Assert.Single(carried.Quest.Requirements);
        Assert.DoesNotContain("carries no requirement", carried.Message);

        var other = (await _desk.AskAsync(new AskRequest("work", "the media config needs a second look"), Now)).Ask!;
        var bare = await _desk.PublishAsync(other.Id, "media-api", Now.AddSeconds(30));
        Assert.Empty(bare.Quest!.Requirements);
        Assert.Contains("carries no requirement", bare.Message);
    }
}
