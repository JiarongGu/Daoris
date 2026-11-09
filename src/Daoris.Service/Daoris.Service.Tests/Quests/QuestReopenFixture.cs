using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>The family, the store and the helpers the reopen tests share: a bound quest, a sealed submission and its step.</summary>
public abstract class QuestReopenFixture : IAsyncLifetime
{
    protected readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-reopen-" + Guid.NewGuid().ToString("N")[..8]);

    protected SqliteConnection _connection = null!;
    protected QuestStore _quests = null!;
    protected QuestExchange _exchange = null!;
    protected AskDesk _desk = null!;
    protected static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-18T09:00:00Z");

    protected const string Sentence = "write the bridge report into docs/report-bridge.md, and this will need the v3 bridge";
    protected const string Graph = "sha256:0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0";
    protected const string Commit = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";
    protected const string Blob = "fedcba9876543210fedcba9876543210fedcba98";
    protected const string Words = "The bridge is wrong; build it again.";

    protected static readonly QuestRequirement Report =
        new("write the bridge report into docs/report-bridge.md", "The report is in docs/report-bridge.md.")
        {
            Evidence = [new QuestEvidence("docs/report-bridge.md")],
        };

    protected static readonly QuestRequirement Bridge =
        new("this will need the v3 bridge", "The report opens through the bridge's route.");

    public async Task InitializeAsync()
    {
        Repo("reports", """{ "summary": "The reports.", "owns": ["report pages", "the v3 bridge"], "accepts": ["a report"] }""");
        Repo("checker", """{ "summary": "Checks things in a browser.", "owns": ["verification"], "accepts": ["a check"] }""");

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

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(service, _quests, files: files, asks: asks);
        _desk = new AskDesk(service, asks, _exchange, files);
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

    /// <summary>The report quest, taken and bound to the closure graph, with a verifying step in the checker to follow.</summary>
    protected async Task<Quest> Bound(string title = "Write the bridge report", IReadOnlyList<QuestStep>? then = null)
    {
        var ask = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;
        var published = await _desk.PublishAsync(ask.Id, "reports", Now, draft: new AskDraft(title, "Through the bridge.")
        {
            Requirements = [Report, Bridge],
            Then = then ?? [new QuestStep("checker", "Verify {parent} in the browser", "Open it and look.")],
        });
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(published.Quest!.Id, "take", null, Now)).Refusal);
        var bound = await _quests.SeedBound(published.Quest.Id, Graph, Now);
        Assert.True(bound.Moved);
        return bound.Quest!;
    }

    protected static QuestAnswer Met(int requirement) => new(requirement, Met: "Done as the check says.");

    protected Task<QuestRespondOutcome> Ready(Quest quest) =>
        _exchange.RespondAsync(quest.Id, "ready", "Built.", Now, answers: [Met(1), Met(2)]);

    /// <summary>The report found, read for the quest's current submission, as the driver asks it.</summary>
    protected async Task<QuestRespondOutcome> Found(Quest quest)
    {
        var current = (await Find(quest.Id)).Submission!;
        return await _exchange.EvidenceAsync(quest.Id, new QuestEvidenceVerdict(Commit, "session-end",
            [new QuestEvidenceRead(1, "docs/report-bridge.md", null, "found") { Object = Blob, Changed = true }])
        {
            Submission = Ref(current),
        }, Now);
    }

    /// <summary>A sealed submission: the bound quest made ready, its evidence found, and its step published from it.</summary>
    protected async Task<(Quest Quest, QuestSubmission Sealed, Quest Step)> Sealed()
    {
        var quest = await Bound();
        Assert.Equal(QuestRespondRefusal.None, (await Ready(quest)).Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await Found(quest)).Refusal);
        var standing = await Find(quest.Id);
        var step = Assert.Single(await _quests.ListAsync(), q => q.Parent == quest.Id);
        return (standing, standing.Submission!, step);
    }

    /// <summary>
    /// FABRIC-CLOSE1c14: a three-step chain. The report quest sealed by its verifying step, which took, bound and submitted, so that
    /// submission published the chain's third step.
    /// </summary>
    protected async Task<(Quest Quest, QuestSubmission First, Quest Step, QuestSubmission Verified, Quest Next)> Chained()
    {
        var quest = await Bound(then:
        [
            new QuestStep("checker", "Verify {parent} in the browser", "Open it and look."),
            new QuestStep("reports", "Publish {parent}", "Put it live."),
        ]);
        Assert.Equal(QuestRespondRefusal.None, (await Ready(quest)).Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await Found(quest)).Refusal);
        var first = (await Find(quest.Id)).Submission!;
        var step = Assert.Single(await _quests.ListAsync(), q => q.Parent == quest.Id);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(step.Id, "take", null, Now)).Refusal);
        Assert.True((await _quests.SeedBound(step.Id, Graph, Now)).Moved);
        var verified = await StepReady(step);
        Assert.Equal(QuestRespondRefusal.None, verified.Refusal);
        var next = Assert.Single(await _quests.ListAsync(), q => q.Parent == step.Id);
        return (quest, first, step, verified.Quest!.Submission!, next);
    }

    protected Task<QuestRespondOutcome> StepReady(Quest step) =>
        _exchange.RespondAsync(step.Id, "ready", "Verified.", Now, answers: [Met(1), Met(2)]);

    protected static QuestOperationRef Ref(QuestSubmission submission) => new(submission.Machine, submission.Sequence);

    protected Task<QuestRespondOutcome> Reopen(Quest quest, QuestSubmission submission) =>
        _exchange.ReopenAsync(quest.Id, Ref(submission), Words, Now);

    protected static (string, QuestOperationRef) Named(Quest quest) => (quest.Id, Ref(quest.Submission!));

    protected Task<QuestCloseOutcome> Close(string key, Quest quest) =>
        _exchange.CloseAsync(quest.Id, key, "token-" + key, [Named(quest)], Now);

    protected Task<QuestRespondOutcome> StepDone(Quest step) =>
        _exchange.RespondAsync(step.Id, "done", "Verified.", Now, answers: [Met(1), Met(2)]);

    protected async Task<Quest> Find(string id) => (await _quests.FindAsync(id))!;

    protected async Task<IReadOnlyList<QuestOperationKind>> Kinds(string id) =>
        [.. (await _quests.HistoryAsync(id)).Select(op => op.Kind)];
}
