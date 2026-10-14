using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// What the review-step test classes stand on (PART6): two repositories, an ask desk, an exchange and a ledger over in-memory
/// stores, and helpers that build a chain to its set-up step and show it. xunit builds one per test, so a derived class shares
/// the setup and no state.
/// </summary>
public abstract class ReviewStepFixture : IAsyncLifetime
{
    protected readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-review-" + Guid.NewGuid().ToString("N")[..8]);

    protected SqliteConnection _connection = null!;
    protected QuestStore _quests = null!;
    protected AskStore _asks = null!;
    protected SessionStore _sessions = null!;
    protected SessionLedger _ledger = null!;
    protected QuestExchange _exchange = null!;
    protected AskDesk _desk = null!;
    protected static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-08T09:00:00Z");

    protected const string Sentence = "add the compare setting to the report, and run it locally against dev data";
    protected const string Commit = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";
    protected const string Later = "0123456789abcdef0123456789abcdef01234567";
    protected const string Look = "http://localhost:4200/reports/7";

    protected static readonly QuestRequirement Setting =
        new("add the compare setting to the report", "The report shows the compare setting.");

    public async Task InitializeAsync()
    {
        Repo("reports", """{ "summary": "The reports.", "owns": ["report pages"], "accepts": ["a report"] }""");
        Repo("checker", """{ "summary": "Checks things in a browser.", "owns": ["verification"], "accepts": ["a check"] }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _asks = await AskStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync())
        {
            await service.RegisterAsync(row with { Workspace = "work" }, Now);
        }

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(service, _quests, files: files, sessions: _sessions, asks: _asks);
        _desk = new AskDesk(service, _asks, _exchange, files);
        _ledger = new SessionLedger(_quests, _sessions, service, _asks);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    protected void Repo(string name, string domain)
    {
        var dir = Path.Combine(_root, "family", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""{ "source": "s", "packs": [], "domain": {{domain}} }""");
    }

    protected async Task<Ask> Asked() => (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;

    protected static QuestStep SetUpStep(string to = "reports", string environment = "local") =>
        new(to, "Show {parent} in local for review", "Set the work up and show it.") { SetUpIn = environment };

    protected Task<AskOutcome> Publish(
        Ask ask, IReadOnlyList<QuestStep> then, QuestReview? review = null, string? session = null, string title = "Add the compare setting",
        ReviewProposed? proposal = null) =>
        _desk.PublishAsync(ask.Id, "reports", Now, draft: new AskDraft(title, "The report gains a setting.")
        {
            Requirements = [Setting], Then = then, Review = review, ReviewProposal = proposal,
        }, session: session);

    protected static QuestAnswer Met => new(1, Met: "The setting is on the report.");

    /// <summary>
    /// The build quest, taken and closed done, and the set-up step its close published, with a note step to `checker` after
    /// it. One ask holds every quest a test makes, so each is told apart by its title.
    /// </summary>
    protected async Task<(Ask Ask, Quest Build, Quest SetUp)> Built(string title = "Add the compare setting", QuestReview? review = null)
    {
        var ask = await Asked();
        var published = await Publish(ask, [SetUpStep(), new QuestStep("checker", "Note {parent}", "Write it down.")], review, title: title);
        Assert.Equal(AskRefusal.None, published.Refusal);
        var build = published.Quest!;
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(build.Id, "take", null, Now)).Refusal);
        var done = await _exchange.RespondAsync(build.Id, "done", "Built.", Now, answers: [Met]);
        Assert.Equal(QuestRespondRefusal.None, done.Refusal);
        var setUp = (await FollowUps(ask, build)).Single();
        return (ask, build, setUp);
    }

    /// <summary>A session working the set-up step, which took it through its own door, in a tree of its own.</summary>
    protected async Task<Session> Working(Quest setUp, string tree = "setup")
    {
        var path = Path.Combine(_root, "trees", tree);
        Directory.CreateDirectory(Path.Combine(path, "dist", "app"));
        var session = (await _ledger.OpenAsync(setUp.Id, "stub", Now, tree: path)).Session!;
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(setUp.Id, "take", null, Now)).Refusal);
        return session;
    }

    /// <summary>The set-up step shown: its session says a set-up and closes it done, and its driver posts it at <paramref name="commit"/>.</summary>
    protected async Task<(Session Session, Quest Shown)> Shown(Quest setUp, string commit = Commit)
    {
        var session = await Working(setUp);
        Assert.Equal(ReviewSayRefusal.None,
            (await _ledger.ReadyAsync(session.Id, Look, "The report with the compare setting on.", "Open the report and turn it on.", null, Now)).Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(setUp.Id, "done", "Shown.", Now, answers: [Met])).Refusal);
        var posted = await _exchange.PostSetUpAsync(setUp.Id, new QuestSetUpPost(commit, "local", Session: session.Id), Now);
        Assert.Equal(QuestRespondRefusal.None, posted.Refusal);
        return (session, posted.Quest!);
    }

    protected async Task<IReadOnlyList<Quest>> FollowUps(Ask ask, Quest parent) =>
        [.. (await _quests.FromAsync(AskDesk.SenderOf(ask.Id))).Where(quest => quest.Parent == parent.Id)];
}
